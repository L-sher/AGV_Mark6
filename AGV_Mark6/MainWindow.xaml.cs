using AGV_Mark6.Web_Connections;
using AGV_Mark6.Model;
using AGV_Mark6.SeriLog;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using static AGV_Mark6.Web_Connections.AGV_Connect;
using Newtonsoft.Json;
using AGV_Mark6.Modbus_Server_For_Buttons;
using System.Windows.Forms;
using AGV_Mark6.MainTaskManager_Methods;
using AGV_Mark6.Catchers;
using System.Text.RegularExpressions;

namespace AGV_Mark6
{
   
    public partial class MainWindow : Window
    {
        //Уточню терминалогию по завершении работы:
        //База - стартовая Точка для агв, которая находится в определенной локации(Например Склад). АГВ в нее приезжает, получает задание ехать в определенный ловитель и возвращается в эту точку.
        //home(или Home) - уникальная точка через которую соединяются базы. АГВ проходит через нее Пустая.
        //crosspoint(или Crosspoint)- уникальная точка через которую соединяются базы. АГВ проходит через нее Загруженная или с тележкой(эти состояния определяются как Загружена)

        //Поскольку терминология менялась в течении разработки, возможны неточности в понятиях базы и home. Изначально все базы назывались home.

        //пустая или загруженная АГВ определяется по положению лифта в данной точке. статус лифта заносится в ручную(а не мониторится из программы)на странице NewAgvProgram.

        //Используется база данных SQLite

        //Для модернизации есть идея учитывать метки, при переходах на следующие шаги. Если была прочитана метка то переход можно выполнить если нет то не выполнять.(Не реализовано)

        //Сохранение локальных параметров.
        public static AGV_Statuses_On_App_Load SavedAGVStatuses = new AGV_Statuses_On_App_Load();

        public static MainWindow mainWindowClient;

        private static ObservableCollection<catchersCurrentStates> catchersCurrentStatesForDataGrid=new ObservableCollection<catchersCurrentStates>();


        //public static int Programm_Changing_Marker = 0;
        //List для передачи данных очереди в ListBox На MainWindow
        List<string> MainWindowListBox;

        //Заранее инициализируем окно редактирования программы
        static NewAgvProgram wind;

        public static ObservableCollection<Model.Prog1>? program1 = new ObservableCollection<Model.Prog1>();

        //Режимы работы АГВ
        //Стандарт
        public static bool Mode_Standart = true;

        //Только от кнопок
        public static bool Mode_ButtonsOnly = false;

        public MainWindow()
        {

            InitializeComponent();

            Log.Logger.Information("Строка 49");
            mainWindowClient = System.Windows.Application.Current.Windows[0] as MainWindow;

            //Если Приложение падает, то нужно возвращать состояние АГВ
            RestoreAgvStatusParameters();
            Log.Logger.Information("Строка 54");
            //Подключение к AGV, чтение регистров
            new Thread(AGV_Connect.StartListeningAGV).Start();

            //Логгирование
            Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.File(new MyOwnCompactJsonFormatter(), "AGV-Log.txt").CreateLogger();

            //Запускаем пинги и мониторим
            new Thread(Ping_Agv.Method_Ping_Agv).Start();
            Log.Logger.Information("Строка 63");
            new Thread(MainTaskManager.StartAGVManager).Start();

            ShowCurrentQueue(MainWindowListBox);

            //Заполняем списки маршрутов
            RoutesEditClass.RoutesListUpdate("!Routes.txt", ref RoutesEditClass.RoutesList);
            RoutesEditClass.RoutesListUpdate("!RoutesBetweenBases.txt", ref RoutesEditClass.RoutesForHomesList);
            RoutesEditClass.RoutesListUpdate("!RoutesMain.txt", ref RoutesEditClass.RoutesMainForButtonsList);

            new Thread(ProgramIsRunningWatcher).Start();
            Log.Logger.Information("Строка 76");
            //Заранее инициализируем окно редактирования программы
            new Thread(ObservableCollectionProcessing).Start();

            //ограничиваю количество строк в мониторинге
            TB_Ping_Log.MaxLines = 200;

            //Мониторинг физических Кнопок(Пока отключен в связи их отсутствием. Возможно отключу совсем)
            new Thread(HarmonyHub_Monitoring.HarmonyHub_Monitoring.HarmonyHub_Client).Start();
            
            //Сервер мониторинга нажатия ПО Кнопок.
            new Thread(Class_ModbusServer_for_Buttons.ButtonServer_Start).Start();
            Log.Logger.Information("Строка 88");

            //Мониторинг заряда АГВ
            new Thread(FluentClientForChargeMonitoring.FluentClientForChargeMonitoring_Class.ChargeMonitoring).Start();
            Log.Logger.Information("Строка 91");
            new Thread(TCPServerForButtons.HTTPServer).Start();
            FillStuffOfMainWindow();
          

            Log.Logger.Information("Строка 117");
        }

        public void FillStuffOfMainWindow()
        {
            //Заполняем Combobox  с маршрутами для программирования кнопок
            CB_RoutesForButtons.ItemsSource = RoutesEditClass.RoutesMainForButtonsList;
            DG_CatchersStatusesFill();
        }

        public static void DG_CatchersStatusesFill()
        {
          
            mainWindowClient.Dispatcher.Invoke(() =>
            {
                catchersCurrentStatesForDataGrid = new ObservableCollection<catchersCurrentStates>();
                foreach (var item in MainTaskManager.db.CatchersCurrentStates)
                {
                    catchersCurrentStatesForDataGrid.Add(item);
                }
                mainWindowClient.DG_CatchersStates.ItemsSource = catchersCurrentStatesForDataGrid;

            });

        }

       

        //Сохранение нового Base с маршрутами для ловителей
        private void BT_SaveNewBase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TB_NewBaseName.Text != "")
                {

                    if (db.AdditionCoordinates.FirstOrDefault(e => e.PointName == TB_NewBaseName.Text) == null)
                    {

                        //Колонка Home идентифицирует точку как Home а PointName Название этой точки.
                        db.Add(new additionCoordinates { Home = "Home", PointName = TB_NewBaseName.Text });

                        db.SaveChanges();

                        //Создаем маршруты для Кнопок и переходов между Home
                        AddRoutesForButtonsAndForHomeTranzitions(TB_NewBaseName.Text);

                        //Добавление ловителей текущего home в таблицу СatchersRoutes
                        db.Add(new catchersRoutes { CatcherRoute = TB_NewBaseName.Text + "->" + "Ловитель Лев Взять", CatcherName = "Ловитель Лев", StateChange = 0, Direction = 1, CatchersHome = TB_NewBaseName.Text });
                        NewRoutesAdd("!Routes.txt", TB_NewBaseName.Text + "->" + "Ловитель Лев Взять:p0s0", ref RoutesEditClass.RoutesList);

                        db.Add(new catchersRoutes { CatcherRoute=TB_NewBaseName.Text+"->"+"Ловитель Пр Взять", CatcherName="Ловитель Пр", StateChange=0, Direction=1, CatchersHome = TB_NewBaseName.Text });
                        NewRoutesAdd("!Routes.txt", TB_NewBaseName.Text + "->" + "Ловитель Пр Взять:p0s0", ref RoutesEditClass.RoutesList);

                        db.Add(new catchersRoutes { CatcherRoute = TB_NewBaseName.Text + "->" + "Ловитель Лев Дать", CatcherName = "Ловитель Лев", StateChange = 1, Direction = 1, CatchersHome = TB_NewBaseName.Text });
                        NewRoutesAdd("!Routes.txt", TB_NewBaseName.Text + "->" + "Ловитель Лев Дать:p0s0", ref RoutesEditClass.RoutesList);

                        db.Add(new catchersRoutes { CatcherRoute=TB_NewBaseName.Text+"->"+"Ловитель Пр Дать", CatcherName="Ловитель Пр", StateChange=1, Direction=1, CatchersHome = TB_NewBaseName.Text });
                        NewRoutesAdd("!Routes.txt", TB_NewBaseName.Text + "->" + "Ловитель Пр Дать:p0s0", ref RoutesEditClass.RoutesList);

                        //Добавление ловителей текущего home в таблицу СatchersCurrentStates
                        db.Add(new catchersCurrentStates { CatcherName = "Ловитель Лев", State = 1, CatchersHome = TB_NewBaseName.Text, CatcherMemoryState=1 });
                        db.Add(new catchersCurrentStates { CatcherName = "Ловитель Пр", State = 0, CatchersHome = TB_NewBaseName.Text, CatcherMemoryState = 0 });


                        db.SaveChanges();

                        Popup_HomeSaved.IsOpen = true;
                        FillStuffOfMainWindow();

                        Class_ModbusServer_for_Buttons.FillRegistersForButtons();
                    }

                }

            }
            catch (Exception ex)
            {

                Log.Information(ex.Message + "строка 144");

            }

        }

        //метод для добавления в файлы !Routes.txt и !RoutesForHomes.txt и !RoutesMain.txt
        public static void NewRoutesAdd(string TextFileName, string NewRoute, ref ObservableCollection<string> RoutesListToSave)
        {

            File.AppendAllText(TextFileName, "\n"+NewRoute);
            RoutesEditClass.RoutesListUpdate(TextFileName, ref RoutesListToSave);

        }

        //Создаем маршруты для Кнопок и для переходов между базами
        public static void AddRoutesForButtonsAndForHomeTranzitions(string NewHomeName)
        {
            try
            {
                if (NewHomeName == "home" || NewHomeName == "Home") { return; }
                List<additionCoordinates> ListOfHomes = db.AdditionCoordinates.Where(e=>e.InUse==0).ToList();

                


                int registersnumber = db.RegistersForButtons.Count() + 1;
                RoutesEditClass.routesMain = true;
                if (ListOfHomes.Count > 1)
                {
                    foreach (var item in ListOfHomes)
                    {
                        if (item.PointName == NewHomeName) { continue; }

                        NewRoutesAdd("!RoutesMain.txt", item.PointName + "->" + NewHomeName, ref RoutesEditClass.RoutesMainForButtonsList);
                        db.RegistersForButtons.Add(new registersForButtons { RegisterNumber = registersnumber, CommandForRegister = item.PointName + "->" + NewHomeName, MechanicalButtonRegister = registersnumber });

                        registersnumber++;

                        NewRoutesAdd("!RoutesMain.txt", NewHomeName + "->" + item.PointName, ref RoutesEditClass.RoutesMainForButtonsList);
                        db.RegistersForButtons.Add(new registersForButtons { RegisterNumber = registersnumber, CommandForRegister = NewHomeName + "->" + item.PointName, MechanicalButtonRegister = registersnumber });

                        registersnumber++;
                    }
                    foreach (var item in db.AdditionCoordinates.Where(e => e.InUse == 0).ToList())
                    {
                        item.InUse = 1;
                    }
                    db.SaveChanges();

                }

               


                RoutesEditClass.routesMain = false;
                //Добавляем маршруты для перехода от home позиции
                NewRoutesAdd("!RoutesBetweenBases.txt", "home" + "->" + NewHomeName+ ":p0s0", ref RoutesEditClass.RoutesForHomesList);
                NewRoutesAdd("!RoutesBetweenBases.txt", NewHomeName + "->" + "home:p0s0", ref RoutesEditClass.RoutesForHomesList);

                //Добавляем маршруты для перехода от crosspoint позиции
                NewRoutesAdd("!RoutesBetweenBases.txt", "crosspoint" + "->" + NewHomeName + ":p0s0", ref RoutesEditClass.RoutesForHomesList);
                NewRoutesAdd("!RoutesBetweenBases.txt", NewHomeName + "->" + "crosspoint:p0s0", ref RoutesEditClass.RoutesForHomesList);

            }
            catch (Exception ex)
            {
                Log.Information(ex.Message+ "Строка 192");
            }
        }

        //Кнопка удаления Базы
        private void BT_DeleteNewBase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TB_NewBaseName.Text != "")
                {

                    if (MainTaskManager.db.AdditionCoordinates.FirstOrDefault(e => e.PointName == TB_NewBaseName.Text) != null)
                    {

                        //Удаление ловителей текущего home в таблице СatchersRoutes
                        MainTaskManager.db.CatchersRoutes.Where(e => e.CatchersHome == TB_NewBaseName.Text).ExecuteDelete();

                        RoutesDeleteFromFile("!Routes.txt", TB_NewBaseName.Text, ref RoutesEditClass.RoutesList);
                        
                        RoutesDeleteFromFile("!RoutesBetweenBases.txt", TB_NewBaseName.Text, ref RoutesEditClass.RoutesForHomesList);

                        RoutesDeleteFromFile("!RoutesMain.txt", TB_NewBaseName.Text, ref RoutesEditClass.RoutesMainForButtonsList);

                        //Освобождаем точку, которая сопряжена с удаляемой базой.
                        FreeAdjointPoint(TB_NewBaseName.Text);

                        db.RegistersForButtons.Where(e => e.CommandForRegister.Contains(TB_NewBaseName.Text + "->") | e.CommandForRegister.Contains("->" + TB_NewBaseName.Text)).ExecuteDelete();

                        //Удаление ловителей текущего home в таблице СatchersCurrentStates
                        db.CatchersCurrentStates.Where(e => e.CatchersHome == TB_NewBaseName.Text).ExecuteDelete();

                        //Колонка Home идентифицирует точку как Home, а PointName Название этой точки.
                        db.AdditionCoordinates.Where(e => e.PointName == TB_NewBaseName.Text).ExecuteDelete();
                        db.SaveChanges();

                        Popup_HomeSaved.IsOpen = true;

                        FillStuffOfMainWindow();

                        Class_ModbusServer_for_Buttons.FillRegistersForButtons();
                    }
                }
            }
            catch (Exception ex)
            {

                Log.Information(ex.Message + "строка 229");

            }
        }

        private void FreeAdjointPoint(string PointToRemove)
        {
            
            string? FreeAdjointPoint = db.RegistersForButtons.FirstOrDefault(e => e.CommandForRegister.Contains(PointToRemove + "->") | e.CommandForRegister.Contains("->" + PointToRemove))?.CommandForRegister ?? "";

            if (FreeAdjointPoint == "")
            {

                return;

            }
            string?[] splittedAdjointPoints = FreeAdjointPoint.Split("->");

            for (int i = 0; i < splittedAdjointPoints.Length-1; i++)
            {

                if (splittedAdjointPoints[i] != PointToRemove)
                {

                    db.AdditionCoordinates.FirstOrDefault(e => e.PointName == splittedAdjointPoints[i]).InUse = 0;
                    db.SaveChanges();
                    return;

                }

            }

        }


        public static void RoutesDeleteFromFile(string TextFileName,string HomeNameToDelete, ref ObservableCollection<string> RoutesListToSave)
        {
            List<string> RoutesFromFileList = new List<string>();
            StreamReader f = new StreamReader(TextFileName);

            while (!f.EndOfStream)
            {
                string? strFromFile = f.ReadLine() ?? "";

                if (strFromFile != "")
                {
                    try
                    { 
                        //В файлах строчки находятся с разделителями -> и :  и если перед одним из них название нашей базы что мы удаляем то пропускаем. остальное попадает в файл
                        string[] splittedString = strFromFile.Split("->");
                        if(TextFileName== "!RoutesBetweenBases.txt")
                        {

                            string[] splittedWithColon = splittedString[1].Split(":");
                            if (splittedWithColon[0]== HomeNameToDelete)
                            {
                                continue;
                            }

                        }

                        if (splittedString[0] == HomeNameToDelete || splittedString[1] == HomeNameToDelete) { continue; }

                        RoutesFromFileList.Add(strFromFile);

                    }
                    catch (Exception ex)
                    {
                        Log.Information(ex.Message + "строка 266");
                        continue;

                    }
                }
            }

            f.Dispose();
            File.WriteAllText(TextFileName, string.Empty);
            //записываем в файл марщруты 
            File.WriteAllLines(TextFileName, RoutesFromFileList);

            if (TextFileName == "!RoutesMain.txt")
            {
                RoutesEditClass.routesMain = true;
                //Считываем из файла маршруты
                RoutesEditClass.RoutesListUpdate(TextFileName, ref RoutesListToSave);
                RoutesEditClass.routesMain = false;
                return;
            }
            RoutesEditClass.RoutesListUpdate(TextFileName, ref RoutesListToSave);

        }


        #region//Режим Эмулятора
        public static bool Emulator = false;

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            Emulator = true;
            AGVReconnect();
        }

        private void RadioButton_Unchecked(object sender, RoutedEventArgs e)
        {
            Emulator = false;
            AGVReconnect();
        }
        #endregion

        //Программирование кнопок
        private void BT_Button1Programming_Click(object sender, RoutedEventArgs e)
        {
            ButtonProgramming("1");
        }

        private void BT_Button2Programming_Click(object sender, RoutedEventArgs e)
        {
            ButtonProgramming("2");
        }

        private void ButtonProgramming(string ButtonNumber)
        {
            if (CB_RoutesForButtons.SelectedValue != null)
            {

                if (Class_ModbusServer_for_Buttons.RegistersAndRoutes.Values.Contains(CB_RoutesForButtons.SelectedValue.ToString()))
                {

                    int registerNumberForButton = Class_ModbusServer_for_Buttons.RegistersAndRoutes.FirstOrDefault(e => e.Value == CB_RoutesForButtons.SelectedValue.ToString()).Key;
                    File.WriteAllText($@"!ProgrammingButtonsHere\Button{ButtonNumber}.txt", CB_RoutesForButtons.SelectedValue.ToString());
                    File.WriteAllText($@"!ProgrammingButtonsHere\Register{ButtonNumber}.txt", registerNumberForButton.ToString());

                    string Host = System.Net.Dns.GetHostName();
                    System.Net.IPAddress[] IP = System.Net.Dns.GetHostByName(Host).AddressList;

                    File.WriteAllText($@"!ProgrammingButtonsHere\IpSetting.txt", System.Net.Dns.GetHostByName(Host).AddressList[1].ToString());

                }
                else
                {

                    int registerNumberForButton = Class_ModbusServer_for_Buttons.RegistersAndRoutes.Count + 1;
                    
                    db.Add(new registersForButtons()
                    {
                        RegisterNumber = registerNumberForButton,
                        CommandForRegister = CB_RoutesForButtons.SelectedValue.ToString()
                    });
                    db.SaveChanges();
                    
                    //Передаем данные кнопке
                    File.WriteAllText($@"!ProgrammingButtonsHere\Button{ButtonNumber}.txt", CB_RoutesForButtons.SelectedValue.ToString());
                    File.WriteAllText($@"!ProgrammingButtonsHere\Register{ButtonNumber}.txt", registerNumberForButton.ToString());
                    string Host = System.Net.Dns.GetHostName();
                    System.Net.IPAddress[] IP = System.Net.Dns.GetHostByName(Host).AddressList;

                    File.WriteAllText($@"!ProgrammingButtonsHere\IpSetting.txt", System.Net.Dns.GetHostByName(Host).AddressList[1].ToString());
                    //Обновляем данные регистров в классе модбас сервер
                    Class_ModbusServer_for_Buttons.RegistersAndRoutes.Add(registerNumberForButton, CB_RoutesForButtons.SelectedValue.ToString());

                }

                Popup_TextBlock.Text = $"Кнопка {ButtonNumber} запрограммирована";
                Popup_ButtonProgrammed.IsOpen = true;

            }
        }

        //Сохранение кнопки
        private void BT_SaveNewButton_Click(object sender, RoutedEventArgs e)
        {

            FolderBrowserDialog fbd = new FolderBrowserDialog();

            fbd.Description = "Custom Description";
            DialogResult result = fbd.ShowDialog();
            string sSelectedPath = fbd.SelectedPath;

            string destFile = fbd.SelectedPath + @"/Button/";

            string ButtonPath = @"./!ProgrammingButtonsHere/";

            string[] files = Directory.GetFiles(ButtonPath);

            Directory.CreateDirectory(destFile);

            try
            {

                foreach (string path in files)
                {
                    File.Copy(path, destFile + Path.GetFileName(path), true);
                }
                Popup_TextBlock.Text="Кнопка Сохранена";
                Popup_ButtonProgrammed.IsOpen = true;
                
            }
            catch (Exception ex)
            {

                System.Windows.MessageBox.Show("Ошибка при создании кнопки. Нет доступа к папке для сохранения. Проверьте, не открыты ли у вас экземпляры кнопок");

            }
        }

        static AGV_Storage_Context db = new AGV_Storage_Context();

        //При запуске сразу получаем данные из БД
        public static void ObservableCollectionProcessing()
        {
            try
            {
                db.Prog1.Load();
                db.Prog2.Load();
                db.Prog3.Load();
                db.Prog4.Load();
                db.Prog5.Load();
                db.Prog6.Load();
                db.Prog7.Load();
                db.Prog8.Load();
                db.Prog9.Load();
                db.Prog10.Load();
            }
            catch (Exception ex)
            {
                Log.Logger.Information("ObservableCollectionProcessing Ошибка загрузки БД" + ex.Message);

            }
           

        }

        //метод добавления строки в Окно мониторинга
        public static void AddToTB_TB_Ping_Log(string TextForLogWindow)//
        {

            mainWindowClient.Dispatcher.Invoke(() =>
            {
                mainWindowClient.TB_Ping_Log.Text += $"\n{TextForLogWindow}";
                mainWindowClient.TB_Ping_Log.ScrollToEnd();
                
            });

        }


        //Мониторинг Статуса программы работает или не работает
        public static void ProgramIsRunningWatcher()
        {
            while (true)
            {
                if (ProgramIsRunning)
                {
                    mainWindowClient.Dispatcher.Invoke(() =>
                    {
                        mainWindowClient.TBlock_ProgramIsRunningStatus.Text = "Программа Включена";
                        mainWindowClient.Ell_ProgramIsRunningStatus_Watcher.Fill = Brushes.Green;
                        mainWindowClient.Ell_MonitoringRegistersStatus_Watcher.Fill = Brushes.Green;
                    });
                    
                    Thread.Sleep(400);
                }
                else
                {
                    mainWindowClient.Dispatcher.Invoke(() =>
                    {

                        mainWindowClient.TBlock_ProgramIsRunningStatus.Text = "Программа Выключена";
                        mainWindowClient.Ell_ProgramIsRunningStatus_Watcher.Fill = Brushes.Red;

                        if (OnlyReadRegistersAGV)
                        {
                            mainWindowClient.Ell_MonitoringRegistersStatus_Watcher.Fill = Brushes.Green;
                        }
                        else
                        {
                            mainWindowClient.Ell_MonitoringRegistersStatus_Watcher.Fill = Brushes.Red;
                        }
                    });
                    Thread.Sleep(400);
                }
            }
        }

        //Обновление Listbox очереди задач На главном окне
        public static void ShowCurrentQueue(List<string> Queue)
        {
            //Выводим информацию по текущей очереди в Listbox
            mainWindowClient.Dispatcher.Invoke(() => mainWindowClient.LB_MainQueue.ItemsSource = Queue);
        }

        //обновление Label очереди задач на главном окне
        public static void showCurrentTaskInLabel(string CurrentTask)
        {
            //Выводим информацию по текущей очереди в Listbox
            mainWindowClient.Dispatcher.Invoke(() =>
            {
                mainWindowClient.Label_AGV_CurrentTask.Text = $"{CurrentTask}";

            }
            );
        }

        //При закрытии приложения полное закрытие приложения
        private void AGV_Application_Closed(object sender, System.EventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        //Переменная для отслеживания открытого окна управления. чтоб не открывать больше одного окна
        public static bool controlAGVOpened = false;

        //Открытие окна управления АГВ
        private void Control_AGV_Window_Open_Click(object sender, RoutedEventArgs e)
        {

            if (controlAGVOpened == false)
            {
                Control_AGV Control_AGV_Wind = new Control_AGV();
                Control_AGV_Wind.Owner = this;
                Control_AGV_Wind.Show();
                controlAGVOpened = true;
            }

        }

        //Очистка окна Логов
        private void BT_EraseLogs_Click(object sender, RoutedEventArgs e)
        {

            TB_Ping_Log.Text = "";

        }


        //переменная , чтобы не открывать больше 10 окон(10 программ всего может быть)
        public static int AddProgramWindowOpened = 0;
        //Открытие окна редактирования программ
        private void BT_AddProgram_Click(object sender, RoutedEventArgs e)
        {

            if (AddProgramWindowOpened < 10)
            {
                wind = new NewAgvProgram()
                {

                };
                wind.Show();
                AddProgramWindowOpened++;
            }
        }


        //Чтобы не открывать больше одного окна
        public static bool RouteSelectorOpened = false;
        //Кнопка загрузки окна редактирования маршрутов
        private void BT_RouteSelector_Click(object sender, RoutedEventArgs e)
        {
            if (RouteSelectorOpened == false)
            {
                RoutesEditClass se = new RoutesEditClass();
                se.Owner = this;
                se.Show();

                RouteSelectorOpened = true;
            }
        }


        private void BT_Button1Programming_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Popup_ButtonProgrammed.IsOpen = false;
        }

        private void BT_Button2Programming_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Popup_ButtonProgrammed.IsOpen = false;
        }

        private void BT_SaveNewButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Popup_ButtonProgrammed.IsOpen = false;
        }


        //действия при закрытии программы
        private void Window_Closed(object sender, System.EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.DisableProcessing();
            System.Windows.Application.Current.Shutdown();
        }


        private void BT_StopAgv_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                AGV_Connect.ProgramIsRunning = false;
                if (MainTaskManager.AGVOnCharge) { MainWindow.AddToTB_TB_Ping_Log("АГВ На зарядке"); return; }
                if (Control_AGV.CommandDoneSuccessfully == false) { MainWindow.AddToTB_TB_Ping_Log("Дождитесь завершения предыдущей команды"); return; }

                Control_AGV.CommandDoneSuccessfully = false;

                new Thread(Control_AGV.StopCommandInitializer).Start();

            }
            catch (Exception ex)
            {
                Log.Information(ex.Message);

            }

        }

        private void BT_StartAgv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainTaskManager.AGVOnCharge) { MainWindow.AddToTB_TB_Ping_Log("АГВ На зарядке"); return; }
                if (Control_AGV.CommandDoneSuccessfully == false) { MainWindow.AddToTB_TB_Ping_Log("Дождитесь завершения предыдущей команды"); return; }

                if (MainTaskManager.Task_Canceled)
                {
                    AddToTB_TB_Ping_Log("Задание было отменено. Старт невозможен");
                    return;
                }

                //AGV_Connect.OnlyReadRegistersAGV = true;

                Control_AGV.CommandDoneSuccessfully = false;
                new Thread(Control_AGV.StartCommandInitializer).Start();

            }
            catch (Exception ex)
            {
                Log.Information(ex.Message);
            }


        }

        //переменная, которая отслеживает текущий шаг и не дает выполнить действия больше одного раза.
        static int StepwatcherForDataGrid = 0;
        static int programWatcherForDatagrid = 0;
        static Stopwatch sdsd = new Stopwatch();

        //В окне мониторинга отрисовываем текущие важные параметры на данном шаге по АГВ
        public static async Task AGVRegistersShow()
        {
            try
            {

                if (registerMap == null) { return; }

                if (registerMap["Текущий шаг"] != StepwatcherForDataGrid)
                {

                    await mainWindowClient.Dispatcher.Invoke(async () =>
                    {

                        double StopWatchView = StepsStopwatch.ElapsedMilliseconds;

                        //Выводим информацию в на экран
                        mainWindowClient.TB_Ping_Log.Text += "\nПрограмма: " + registerMap["Текущая программа"].ToString() + 
                        ", Шаг: " + registerMap["Текущий шаг"].ToString() +
                        "\n" + MainTaskManager.LoadStatus +
                        "\nВремя шага:" + (StopWatchView / 1000).ToString();

                        MainWindow.AddToTB_TB_Ping_Log($"последний base:{MainTaskManager.LastBase}" +
                        "\n");

                        await TextBlockLinesRestrictor();

                    });
                    StepwatcherForDataGrid = registerMap["Текущий шаг"];
                    programWatcherForDatagrid = registerMap["Текущая программа"];
                    //то есть текущий шаг и программа успели отрисываться в мониторинге
                    MainTaskManager.awaitingMonitoringShow = false;

                    MainTaskManager.ProgrammsLog.Information($"Время выполнения предыдущего шага: {StepsStopwatch.ElapsedMilliseconds / 1000} Секунд ");
                    StepsStopwatch.Restart();
                    return;

                }

                if (registerMap["Текущая программа"] != programWatcherForDatagrid)
                {
                    await mainWindowClient.Dispatcher.Invoke(async() =>
                    {

                        double StopWatchView = StepsStopwatch.ElapsedMilliseconds;

                        //Выводим информацию в на экран
                        mainWindowClient.TB_Ping_Log.Text += "\nПрограмма: " + registerMap["Текущая программа"].ToString() + ", Шаг: " + registerMap["Текущий шаг"].ToString() +
                        "\n" + MainTaskManager.LoadStatus + "\nВремя шага:" + (StopWatchView / 1000).ToString();

                        MainWindow.AddToTB_TB_Ping_Log($"последний base:{MainTaskManager.LastBase}" + "\n");

                        await TextBlockLinesRestrictor();

                    });
                    programWatcherForDatagrid = registerMap["Текущая программа"];
                    StepwatcherForDataGrid = registerMap["Текущий шаг"];

                    //то есть текущий шаг и программа успели отрисоваться в мониторинге
                    MainTaskManager.awaitingMonitoringShow = false;

                    MainTaskManager.ProgrammsLog.Information($"Время выполнения предыдущего шага: {StepsStopwatch.ElapsedMilliseconds / 1000} Секунд ");
                    StepsStopwatch.Restart();
                    return;
                }

            }
            catch (NullReferenceException ex)
            {

            }

        }

        //Окно мониторинга АГВ чтобы не переполнялось, придуман вот такой метод. При переполнении первые строчки затираются
        public static async Task TextBlockLinesRestrictor()
        {
            if (mainWindowClient.TB_Ping_Log.LineCount > mainWindowClient.TB_Ping_Log.MaxLines)
            {

                string[] alllines = mainWindowClient.TB_Ping_Log.Text.Split("\n");
                mainWindowClient.TB_Ping_Log.Text = "";
                List<string> LineList = alllines.ToList();

                while (LineList.Count >= mainWindowClient.TB_Ping_Log.MaxLines - 20)
                {
                    LineList.RemoveAt(0);
                }

                foreach (string str in LineList)
                {
                    mainWindowClient.TB_Ping_Log.Text += str + "\n";
                }

            }

            mainWindowClient.TB_Ping_Log.ScrollToEnd();
        }

        //Выводим информацию в таблицу datagrid
        public static void DG_Registers_ShowActual()
        {
            mainWindowClient.Dispatcher.Invoke(() =>
            {
                mainWindowClient.DG_Registers.ItemsSource = registerMap;
            });

        }
        
        //Кнопка очистки очереди и текущего задания
        private void BT_ClearQueue_Click(object sender, RoutedEventArgs e)
        {

            ClearCurrentQueue();

        }

        //Кнопка отмены текущего задания
        private void BT_CancelTask_Click(object sender, RoutedEventArgs e)
        {

            if (MainTaskManager.AGV_Quest != "")
            {
                CancelCurrentAGVTask();
            }
            showCurrentTaskInLabel(MainTaskManager.AGV_Quest);

        }

        //Очистка текущего задания АГВ
        public static void CancelCurrentAGVTask()
        {

            MainTaskManager.AwaitingTransition = false;

            MainTaskManager.CurrentTaskStepNumber = 0;

            MainTaskManager.AGV_Task_Route.Clear();

            if (MainTaskManager.AGV_Quest != "")
            {

                MainTaskManager.Task_Canceled = true;

                SavedAGVStatuses.OnAppStart_TaskCanceled = true;
                MainWindow.SavedAGVStatuses.Save();

            }

            MainTaskManager.AGV_Quest = "";


            SavedAGVStatuses.OnAppStart_AGV_Quest = "";
            MainWindow.SavedAGVStatuses.Save();
            SavedAGVStatuses.OnAppStart_CurrentTaskSteps = "";
            MainWindow.SavedAGVStatuses.Save();

            showCurrentTaskInLabel(MainTaskManager.AGV_Quest);

            Control_AGV.AGV_ChargedUp = 0;

            AddToTB_TB_Ping_Log("Задание Отменено");

        }

        //Очистка очереди АГВ
        public static void ClearCurrentQueue()
        {

            if (MainTaskManager.AGVQueue.Count == 0) { return; }
            MainTaskManager.AGVQueue.Clear();
            File.WriteAllText("!CurrentQueue.txt", String.Empty);
            ShowCurrentQueue(MainTaskManager.AGVQueue.ToList());

        }

        public static void catcherMemoryOrder()
        {
            mainWindowClient.Dispatcher.Invoke(() =>
            {
                //Параметр текущих статусов Ловителей
                List<catchersCurrentStates> CurrentStatesMemoryList = MainTaskManager.db.CatchersCurrentStates.Where(e => e.CatcherMemoryState != 0).ToList();
                CurrentStatesMemoryList = CurrentStatesMemoryList.OrderBy(e => e.CatcherMemoryState).ToList();
                CatcherWatcher.CatcherStateMemory.Clear();
                foreach (var item in CurrentStatesMemoryList)
                {

                    CatcherWatcher.CatcherStateMemory.Add(new string[] { $"{item.CatcherName}", $"{item.CatchersHome}" });

                }
            });
        } 

     
        //После закрытия приложения восстанавливаем параметры текущего состояния АГВ
        private void RestoreAgvStatusParameters()
        {
            //Восстанавливаем параметры
            //Параметр загрузки
            MainTaskManager.LoadStatus = SavedAGVStatuses.OnAppStart_loadStatus;

            if (SavedAGVStatuses.OnAppStart_TransitionMissCount.Length > 0)
            {
                MainTaskManager.TransitionMissCount = int.Parse(SavedAGVStatuses.OnAppStart_TransitionMissCount);
            }

            //Параметр текущей очереди
            StreamReader f = new StreamReader("!CurrentQueue.txt");
            while (!f.EndOfStream)
            {

                string stringFromFile = f.ReadLine();
                if (stringFromFile != "")
                {
                    MainTaskManager.AGVQueue.Enqueue(stringFromFile);
                }
                //Заносим данные очереди в список, чтобы потом выводить в ListBox главного окна.
                MainWindowListBox = MainTaskManager.AGVQueue.ToList();

            }
            f.Dispose();

            //Параметр текущих статусов Ловителей
            catcherMemoryOrder();

            //параметр последней базы

            MainTaskManager.LastBase = SavedAGVStatuses.OnAppStart_LastBase;

            //Параметр остановки задачи.

            MainTaskManager.Task_Canceled = SavedAGVStatuses.OnAppStart_TaskCanceled;

            MainTaskManager.AGV_Task_Route = JsonConvert.DeserializeObject<List<RouteEditorStorage>>(SavedAGVStatuses.OnAppStart_CurrentTaskSteps);
            if (MainTaskManager.AGV_Task_Route == null)
            {
                MainTaskManager.AGV_Task_Route = new List<RouteEditorStorage>();
            }

            MainTaskManager.AGV_Quest = SavedAGVStatuses.OnAppStart_AGV_Quest;
            MainWindow.showCurrentTaskInLabel(MainTaskManager.AGV_Quest);

            MainTaskManager.Task_Canceled = SavedAGVStatuses.OnAppStart_TaskCanceled;
            if (MainTaskManager.Task_Canceled == true)
            {
                MainWindow.AddToTB_TB_Ping_Log("Во время Сбоя программа находилась в режиме перехода между точками. Восстановление шагов задания невозможно. Пожалуйста, Скорректируйте АГВ вручную");

            }
        }

        //Для того чтобы включать только мониторинг АГВ, без работы программы АГВ. 
        private void Ell_MonitoringRegistersStatus_Watcher_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

            AGV_Connect.OnlyReadRegistersAGV = !AGV_Connect.OnlyReadRegistersAGV;

            //Если мониторинг регистров включен то окрашиваем в зеленый
            if (AGV_Connect.OnlyReadRegistersAGV)
            {
                Ell_MonitoringRegistersStatus_Watcher.Fill = Brushes.Green;
            }
            else
            {
                Ell_MonitoringRegistersStatus_Watcher.Fill = Brushes.Red;
                ProgramIsRunning = false;
            }

        }

        private void Grid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Popup_ButtonProgrammed.IsOpen = false;
        }


        //Далее переключение режимов АГВ
        private void CheckB_Standart_Checked(object sender, RoutedEventArgs e)
        {
            Mode_Standart = true;
            Mode_ButtonsOnly = false;
        }

        private void CheckB_Standart_Unchecked(object sender, RoutedEventArgs e)
        {
            Mode_Standart = false;
            Mode_ButtonsOnly = true;
        }

        private void CheckB_ButtonsOnly_Checked(object sender, RoutedEventArgs e)
        {
            Mode_Standart = false;
            Mode_ButtonsOnly = true;
        }

        private void CheckB_ButtonsOnly_Unchecked(object sender, RoutedEventArgs e)
        {
            Mode_Standart = true;
            Mode_ButtonsOnly = false;
        }

        private void DG_CatchersStates_CurrentCellChanged(object sender, EventArgs e)
        {
            
            DG_CatchersStates.CommitEdit();
            DG_CatchersStates.CommitEdit();
            MainTaskManager.db.SaveChanges();
            
            catcherMemoryOrder();

        }

        private void BT_mechanicalButtonProgramming_Click(object sender, RoutedEventArgs e)
        {

            if (CB_RoutesForButtons.SelectedValue != null)
            {
                if (TB_mechanicalButton.Text == "") { return; }

                db.RegistersForButtons.FirstOrDefault(e => e.CommandForRegister == CB_RoutesForButtons.SelectedValue).MechanicalButtonRegister = int.Parse(TB_mechanicalButton.Text);
                db.SaveChanges();
                HarmonyHub_Monitoring.HarmonyHub_Monitoring.RegistersData = db.RegistersForButtons.Where(e => e.MechanicalButtonRegister >= 0).ToList(); 

            }
           
        }

        //Методы для ограничения ввода в TextBox только цифр в заданном диапазоне от 0 до x.
        static object locker = new object();
        public static void OnlyNumbersAllowed(object sender, System.Windows.Input.TextCompositionEventArgs e, System.Windows.Controls.TextBox tb_text, int EndRange)
        {
            Regex regex = new Regex(@"^[0-9][0]?$");

            lock (locker)
            {
                e.Handled = !IsValid(((System.Windows.Controls.TextBox)sender).Text + e.Text, EndRange);
            }

        }
        private static bool IsValid(string str, int Endrange)
        {
            int i;
            return int.TryParse(str, out i) && i >= 0 && i <= Endrange;
        }

        //Это TextBox 
        private void TB_mechanicalButton_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            OnlyNumbersAllowed(sender, e, TB_mechanicalButton, 10000);
        }
        private void TB_mechanicalButton_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            NoSpaces(TB_mechanicalButton);
        }


        public static void NoSpaces(System.Windows.Controls.TextBox tb_text)
        {
            lock (locker)
            {
                if (tb_text.Text.Contains(" "))
                {
                    tb_text.Text = "";
                }
            }
        }

        private void DG_CatchersStates_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            int val;
            if (!Int32.TryParse(e.Text, out val))
            {
                e.Handled = true; // отклоняем ввод
                return;
            }
        }

        private void DG_CatchersStates_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Space) { e.Handled = true; }
        }

        private void CB_RoutesForButtons_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {

                if(db.RegistersForButtons.FirstOrDefault(e => e.CommandForRegister == CB_RoutesForButtons.SelectedValue.ToString()).MechanicalButtonRegister != null)
                {
                    TB_mechanicalButton.Text = db.RegistersForButtons.FirstOrDefault(e => e.CommandForRegister == CB_RoutesForButtons.SelectedValue.ToString()).MechanicalButtonRegister.ToString();
                }
                
            }
            catch (NullReferenceException)
            {

            }
          
        }

        //private void MainWindowPreview_StateChanged(object sender, EventArgs e)
        //{
        //    if (WindowState == WindowState.Minimized) this.Hide();
        //    base.OnStateChanged(e);
        //}
    }

}

