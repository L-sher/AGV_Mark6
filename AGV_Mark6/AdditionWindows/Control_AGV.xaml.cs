using AGV_Mark6.Web_Connections;
using AGV_Mark6.MainTaskManager_Methods;
using AGV_Mark6.SeriLog;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;




namespace AGV_Mark6
{
    public partial class Control_AGV : Window
    {
        //Класс для ручного управления АГВ


        public static string CommandMassive = "";
        public static Serilog.Core.Logger ControlAGVLog = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.File(new MyOwnCompactJsonFormatter(), "AGV-Log_Control_AGV.txt").CreateLogger();
        public enum AGVregisters : int
        {
            Controll = 62207,               // Сигналы управления AGV
            Current_Queue = 60208,          // Текущая очередь
            Current_Programm = 60209,       // Текущая программа
            Current_Step = 60210            // Текущий шаг
        }

        //Временное хранилище данных команды
        int programFromTB = 0;
        int StepFromTB = 0;

        public Control_AGV()
        {
            InitializeComponent();
        }

        private void Send_Command_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CommandDoneSuccessfully == false) { MainWindow.AddToTB_TB_Ping_Log("Сброс задач"); CommandDoneSuccessfully = true; return; }
                if (MainTaskManager_Methods.MainTaskManager.AGVOnCharge) { MainWindow.AddToTB_TB_Ping_Log("АГВ На зарядке"); return; }

                if (TB_Steps.Text != "" & TB_Program.Text != "")
                {
                    if (MainTaskManager.Task_Canceled)
                    {

                        MainWindow.AddToTB_TB_Ping_Log("Отправка в указанную точку после отмены задания");
                        MainTaskManager.Task_Canceled = false;
                        MainWindow.SavedAGVStatuses.OnAppStart_TaskCanceled = false;
                        MainWindow.SavedAGVStatuses.Save();

                    }
                    //Если висит задача то мы ее отменяем
                    if (MainTaskManager.AGV_Quest != "")
                    {

                        AGV_Connect.ProgramIsRunning = false;
                        
                        MainTaskManager.ClearCurrentTask();

                    }

                    //отправляем команду на агв асинхронно, чтоб интерфейс не зависал
                    programFromTB = int.Parse(TB_Program.Text);
                    StepFromTB = int.Parse(TB_Steps.Text);

                    CommandDoneSuccessfully = false;
                    new Thread(() =>{
                        SendCommandToAGVAsyncInThread(programFromTB, StepFromTB); }).Start();
                    //обнуляем запомнившиеся шаг и программу в MainTaskManager
                    MainTaskManager.programm = 0;
                    MainTaskManager.step = 0;

                    if (CB_LoadList.SelectedValue != null)
                    {
                        if (CB_LoadList.SelectedValue.ToString() != "")
                        {
                            MainWindow.SavedAGVStatuses.OnAppStart_loadStatus = "Загруженность:" + CB_LoadList.SelectedValue.ToString();
                            MainWindow.SavedAGVStatuses.Save();
                            MainTaskManager.LoadStatus = CB_LoadList.SelectedValue.ToString();
                        }
                    }

                    if (TB_TransitionMissCount.Text != "")
                    {
                        MainWindow.SavedAGVStatuses.OnAppStart_TransitionMissCount = TB_TransitionMissCount.Text;
                        MainWindow.SavedAGVStatuses.Save();
                        MainTaskManager.TransitionMissCount = int.Parse(TB_TransitionMissCount.Text);
                    }
                   // MainTaskManager.StartEventMethod();
                }
                else
                {

                    MainWindow.AddToTB_TB_Ping_Log("Пожалуйста, Заполните поля");
                    return;

                }

               

            }
            catch(Exception ex)
            {
                ControlAGVLog.Information(ex.Message);
            }
        }

        //Отправляем задание по кнопке "Отправить"
        private void SendCommandToAGVAsyncInThread(int program, int step)
        {
            try
            {
                if (MainTaskManager.AGVOnCharge) { MainWindow.AddToTB_TB_Ping_Log("АГВ На зарядке"); return; }

                if (MainTaskManager.Task_Canceled)
                {

                    MainWindow.AddToTB_TB_Ping_Log("Задание было отменено. Старт невозможен");
                    return;

                }

                while (CommandDoneSuccessfully == false)
                {

                    try
                    {

                        if (MainWindow.Emulator)
                        {

                            //TODO:Эмулятор
                            AGV_Connect.AGV_Client.WriteMultipleRegisters(62218, new int[] { program });
                            AGV_Connect.AGV_Client.WriteMultipleRegisters(62219, new int[] { step });
                            CommandDoneSuccessfully = true;
                            //Очистка всех параметров.
                            
                            MainWindow.SavedAGVStatuses.OnAppStart_TransitionMissCount = "";
                            MainWindow.SavedAGVStatuses.Save();
                            MainTaskManager.TransitionMissCount = 0;

                            MainTaskManager.AwaitingTransition = false;

                            if (CB_LoadList.SelectedValue != null)
                            {
                                MainTaskManager.LoadStatus = CB_LoadList.SelectedValue.ToString();
                                MainWindow.SavedAGVStatuses.OnAppStart_loadStatus = "Загруженность:" + CB_LoadList.SelectedValue.ToString();
                                MainWindow.SavedAGVStatuses.Save();
                            }
                            else
                            {
                                MainTaskManager.LoadStatus = "Пустая";
                                MainWindow.SavedAGVStatuses.OnAppStart_loadStatus = "Загруженность:" + "Пустая";
                                MainWindow.SavedAGVStatuses.Save();
                            }


                        }
                        else
                        {

                            //TODO:Рабочая
                            AGV_Connect.AGV_Client.WriteMultipleRegisters((int)AGVregisters.Controll, new int[] { 0 });
                            Thread.Sleep(100);

                            AGV_Connect.AGV_Client.WriteMultipleRegisters((int)AGVregisters.Controll, new int[] { 2 });//Остановка
                            Thread.Sleep(100);

                            AGV_Connect.AGV_Client.WriteMultipleRegisters(62218, new int[] { program });
                            Thread.Sleep(100);

                            AGV_Connect.AGV_Client.WriteMultipleRegisters((int)AGVregisters.Controll, new int[] { 16 });
                            Thread.Sleep(300);

                            AGV_Connect.AGV_Client.WriteMultipleRegisters(62219, new int[] { step });
                            Thread.Sleep(100);

                            AGV_Connect.AGV_Client.WriteMultipleRegisters((int)AGVregisters.Controll, new int[] { 32 });
                            Thread.Sleep(300);

                            
                            CommandDoneSuccessfully = true;

                            //Очистка всех параметров.
                            MainWindow.SavedAGVStatuses.OnAppStart_TransitionMissCount = "";
                            MainWindow.SavedAGVStatuses.Save();
                            MainTaskManager.TransitionMissCount = 0;

                            MainTaskManager.AwaitingTransition = false;

                            if (CB_LoadList.SelectedValue != null)
                            {
                                MainTaskManager.LoadStatus = CB_LoadList.SelectedValue.ToString();
                                MainWindow.SavedAGVStatuses.OnAppStart_loadStatus = "Загруженность:" + CB_LoadList.SelectedValue.ToString();
                                MainWindow.SavedAGVStatuses.Save();
                            }
                            else 
                            {
                                MainTaskManager.LoadStatus = "Пустая";
                                MainWindow.SavedAGVStatuses.OnAppStart_loadStatus = "Загруженность:" + "Пустая";
                                MainWindow.SavedAGVStatuses.Save();
                            }

                            

                        }

                    }
                    catch (Exception)
                    {

                        ControlAGVLog.Information("Команда в процессе выполнения, возможно отсутствует связь");
                        Thread.Sleep(200);

                    }

                }

            }
            catch (Exception ex)
            {


            }

        }

        //Переменная для хранения информации о том, выполняется ли сейчас какая то команда.
        public static bool CommandDoneSuccessfully=true;
        private void BT_Programm1_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CommandDoneSuccessfully == false) { MainWindow.AddToTB_TB_Ping_Log("Дождитесь завершения предыдущей команды"); return; }

                if (MainTaskManager.AGVOnCharge) { MainWindow.AddToTB_TB_Ping_Log("АГВ На зарядке"); return; }

                if (MainTaskManager.Task_Canceled)
                {

                    MainWindow.AddToTB_TB_Ping_Log("Отправка в указанную точку после отмены задания");

                    MainTaskManager.Task_Canceled = false;
                   
                    MainWindow.SavedAGVStatuses.OnAppStart_TaskCanceled = false;
                    MainWindow.SavedAGVStatuses.Save();

                }

                //Если висит задача то мы ее отменяем
                if (MainTaskManager.AGV_Quest != "")
                {

                    AGV_Connect.ProgramIsRunning = false;
                    MainTaskManager.ClearCurrentTask();

                }
                if (MainWindow.Emulator)
                {
                    //TODO:Эмулятор
                    AGV_Connect.AGV_Client.WriteMultipleRegisters(62218, new int[] { 1 });
                    AGV_Connect.AGV_Client.WriteMultipleRegisters(62219, new int[] { 1 });
                }
                else
                {

                    CommandDoneSuccessfully = false;
                    //TODO:Рабочая
                    new Thread(() =>
                    {
                        SendCommandToAGVAsyncInThread(1, 1);
                    }).Start();

                }
               
               
             
            }
            catch (Exception ex)
            {

                ControlAGVLog.Information(ex.Message);
            }
        }

        //Команда Стоп
        private void StopAgv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AGV_Connect.ProgramIsRunning = false;
                if (CommandDoneSuccessfully == false) { MainWindow.AddToTB_TB_Ping_Log("Отправка команды прерывается"); CommandDoneSuccessfully = true; return; }
                if (MainTaskManager.AGVOnCharge) { MainWindow.AddToTB_TB_Ping_Log("АГВ На зарядке"); return; }

                CommandDoneSuccessfully = false;
                    
                    new Thread(StopCommandInitializer).Start();

            }
            catch (Exception ex)
            {
                ControlAGVLog.Information(ex.Message);

            }
        }
        public static void StopCommandInitializer()
        {
            
            while (CommandDoneSuccessfully == false)
            {
                try
                {
                    if (MainWindow.Emulator)
                    {
                        //TODO: Эмулятор
                        AGV_Connect.AGV_Client.WriteMultipleRegisters(60, new int[] { 0 });
                        CommandDoneSuccessfully = true;
                    }
                    else
                    {
                        //TODO:Рабочая
                        AGV_Connect.AGV_Client.WriteMultipleRegisters((int)AGVregisters.Controll, new int[] { 2 });//остановка
                        CommandDoneSuccessfully = true;
                        ControlAGVLog.Information("АГВ остановлено вручную StopCommandInitializer 326");
                    }
                   
                }
                catch (Exception)
                {
                    ControlAGVLog.Information("Команда в процессе выполнения, возможно отсутствует связь");
                    Thread.Sleep(200);

                }
            }
        }

        //Команда старт
        private void StartAgv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainTaskManager.AGVOnCharge) { MainWindow.AddToTB_TB_Ping_Log("АГВ На зарядке"); return; }
                if (CommandDoneSuccessfully == false) { MainWindow.AddToTB_TB_Ping_Log("Дождитесь завершения предыдущей команды"); return; }
                if (MainTaskManager.Task_Canceled)
                {
                    MainWindow.AddToTB_TB_Ping_Log("Задание было отменено. Старт невозможен");
                    return;
                }
                //AGV_Connect.OnlyReadRegistersAGV = true;

                CommandDoneSuccessfully = false;
                new Thread(StartCommandInitializer).Start();

            }
            catch (Exception ex)
            {
                ControlAGVLog.Information(ex.Message);
            }
        }
        public static void StartCommandInitializer()
        {
            
            while (CommandDoneSuccessfully == false)
            {
                try
                {

                    if (MainWindow.Emulator)
                    {
                        //TODO: Эмулятор

                        //AGV_Connect.AGV_Client.WriteMultipleRegisters(60, new int[] { 1 });
                        AGV_Connect.ProgramIsRunning = true;
                        CommandDoneSuccessfully = true;
                    }
                    else
                    {
                        //TODO: Рабочая
                        AGV_Connect.ProgramIsRunning = true;
                        //AGV_Connect.AGV_Client.WriteMultipleRegisters((int)AGVregisters.Controll, new int[] { 0 });
                        //Thread.Sleep(100);
                        //AGV_Connect.AGV_Client.WriteMultipleRegisters((int)AGVregisters.Controll, new int[] { 1 });//Запуск
                        CommandDoneSuccessfully = true;
                        
                        //AGV_Connect.OnlyReadRegistersAGV = true;
                    }
                }
                catch (Exception)
                {
                    ControlAGVLog.Information("Команда в процессе выполнения, возможно отсутствует связь");
                    Thread.Sleep(200);

                }
            }
        }

        //Сохранение очереди задач в файл.
        public static void SaveCurrentQueue()
        {

            File.WriteAllText("!CurrentQueue.txt", string.Empty);
            File.WriteAllLines("!CurrentQueue.txt", MainTaskManager.AGVQueue.ToList().ToArray());

        }

        //Обработка ввода TextBox Кол. Пропусков(только цифры)
        private void TB_TransitionMissCount_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            int val;
            if (!Int32.TryParse(e.Text, out val)) //&& e.Text != "-")
            {
                e.Handled = true; // отклоняем ввод
                return;
            }
        }

        //Обработка ввода TextBox Шаги(только цифры)
        private void TB_Steps_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            int val;
            if (!Int32.TryParse(e.Text, out val)) //&& e.Text != "-")
            {
                e.Handled = true; // отклоняем ввод
                return;
            }
        }
        //Обработка ввода TextBox Программа(только цифры)
        private void TB_Program_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            int val;
            if (!Int32.TryParse(e.Text, out val)) //&& e.Text != "-")
            {
                e.Handled = true; // отклоняем ввод
                return;
            }
        }

        //Закрытие Окна
        private void Window_Closed(object sender, EventArgs e)
        {
            MainWindow.controlAGVOpened = false;
        }

        //переменная для того чтобы при выходе с зарядки она бы не встала на нее снова, так как чтобы после п4ш2 перейти на п4ш1 нужно сначала завершить п4ш2. после завершения мы увидим п4ш3 снова, и чтобы она не стала на зарядку, вот эта переменная
        //1 чтобы не запускался поток MainTaskManager
        //2 чтобы не ставился на зарядку пока не выполнит задание доехать до home
        public static int AGV_ChargedUp = 0;

        //Кнопка отвечающая за отдачу команды ехать в home После Зарядки
        private void BT_AGVChargedUp_Click(object sender, RoutedEventArgs e)
        {
            AGVChargedUp();
        }
      
        public static void AGVChargedUp()
        {

            //Если АГВ не на зарядке
            if (!MainTaskManager.AGVOnCharge) { MainWindow.AddToTB_TB_Ping_Log("АГВ Не на зарядке"); return; }

            //так я нахожу необходимый маршрут
            Regex ChargeHome = new Regex(@"^Зарядка.*home.*");
            if (ChargeHome.IsMatch(MainTaskManager.AGV_Quest)) { MainWindow.AddToTB_TB_Ping_Log("АГВ выполняет уже эту задачу"); return; }
            foreach (string str in MainTaskManager.AGVQueue)
            {
                if (ChargeHome.IsMatch(str))
                {
                    MainWindow.AddToTB_TB_Ping_Log("В очереди присутствует задачи постановки АГВ на зарядку"); return;
                }
            }

            //Снимаем АГВ с зарядки
            MainTaskManager.AGVOnCharge = false;

            //очередь для промежуточного хранения данных текущей очереди АГВ

            List<string> TempList = new List<string>();

            while (MainTaskManager.AGVQueue.Count > 0)
            {
                TempList.Add(MainTaskManager.AGVQueue.Dequeue());
            }

            MainTaskManager.AGVQueue.Clear();


            MainTaskManager.AGVQueue.Enqueue(RoutesEditClass.RoutesForHomesList.FirstOrDefault(e => ChargeHome.IsMatch(e)) ?? "");

            //Получаем следующую задачу
            MainTaskManager.AGV_Quest = MainTaskManager.DequeueTask();
            //Запоминаем
            MainWindow.SavedAGVStatuses.OnAppStart_AGV_Quest = MainTaskManager.AGV_Quest;
            MainWindow.SavedAGVStatuses.Save();

            //извлекаем маршрут по заданию
            MainTaskManager.ExtractRouteForHomesFromCurrentTask();

            if (MainTaskManager.AGV_Task_Route.Count == 0)
            {

                MainWindow.AddToTB_TB_Ping_Log("Критическая ошибка при чтении шагов маршрута. Проверьте Маршрут");
                return;

            }


            AGV_ChargedUp = 1;
            AGV_Connect.ProgramIsRunning = true;
            //Переходим к началу маршрута
            MainTaskManager.TransitionMethod(int.Parse(MainTaskManager.AGV_Task_Route.FirstOrDefault(e => e.id == 0).Program), int.Parse(MainTaskManager.AGV_Task_Route.FirstOrDefault(e => e.id == 0).Step));

            MainWindow.AddToTB_TB_Ping_Log($"АГВ получило задачу: {MainTaskManager.AGV_Quest}");

            foreach (string item in TempList)
            {

                MainTaskManager.AGVQueue.Enqueue(item);

            }

            //выводим список текущих задач и полученную задачу на главное окно
            MainTaskManager.ShowCurrentTaskAndCurrentQueueInMainWindow();

            SaveCurrentQueue();
            AGV_ChargedUp = 2;
        }

        private void BT_GoToCharge_Click(object sender, RoutedEventArgs e)
        {
            if (MainTaskManager.AGVOnCharge) { MainWindow.AddToTB_TB_Ping_Log("АГВ На зарядке"); return; }
            MainTaskManager.GoToCharge();
            
            SaveCurrentQueue();
        }

    }

}
