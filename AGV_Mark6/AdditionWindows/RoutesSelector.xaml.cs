using AGV_Mark6.Model;
using AGV_Mark6.SeriLog;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using AGV_Mark6.MainTaskManager_Methods;



namespace AGV_Mark6
{
    public partial class RoutesEditClass : Window
    {
        //Здесь соответственно задаются точки для маршрутов, автоматически создаваемых при добавлении нового Base.
        //маршруты хранятся в txt файлах. 
        //есть 3 типа маршрутов - список маршрутов ловителей,  маршрутов для кнопок(не имеют координат), для передвижения между базами


        public ObservableCollection<RouteEditorStorage> RoutesEditor;
        public static AGV_Mark6.RoutesEditClass RoutesSelectorClient;
        public static Serilog.Core.Logger RoutesSelectorLog = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.File(new MyOwnCompactJsonFormatter(), "AGV-Log_RoutesSelector.txt").CreateLogger();

        static bool routesForHomesAdding = false;
        public static bool routesMain = true;
        static bool routesCatchers = false;

        public RoutesEditClass()
        {
            RoutesEditor = new ObservableCollection<RouteEditorStorage>();
            RoutesEditor.Add(new RouteEditorStorage() { Step = "1", Program = "1" });
            
            InitializeComponent();

            DG_RouteEditor.ItemsSource = RoutesEditor;

            foreach (Window wind in Application.Current.Windows)
            {
                if (wind.GetType() == typeof(RoutesEditClass))
                {
                    RoutesSelectorClient = wind as RoutesEditClass;
                }
            }

            //Заполняем ListBox маршрутами, которые сейчас существуют
            RoutesListUpdate("!Routes.txt", ref RoutesList);
            RoutesListUpdate("!RoutesBetweenBases.txt", ref RoutesForHomesList);
            routesMain = true;
            RoutesListUpdate("!RoutesMain.txt", ref RoutesMainForButtonsList);

            MainWindow.mainWindowClient.CB_RoutesForButtons.ItemsSource = RoutesMainForButtonsList;

            LB_AddingQueue.ItemsSource = RoutesMainForButtonsList;

            //Заполняем ListBox с текущей очередью 
            CurrentQueueList = MainTaskManager.AGVQueue.ToList();
            foreach (string item in CurrentQueueList)
            {
                LB_CurrentQueue.Items.Add(item);
            }

            //Привязываем коллекцию RoutesEditor К таблицу в которой добавляются новые маршруты.

        }

        static Regex RouteName = new Regex("^.*:");

        //список маршрутов для выполнения действий
        public static ObservableCollection<string> RoutesList;

        //маршруты для передвижения между Home
        public static ObservableCollection<string> RoutesForHomesList;

        //Маршруты для кнопок
        public static ObservableCollection<string> RoutesMainForButtonsList;


        static List<string> CurrentQueueList;
        //При инициализации окна заполняем список RoutesList существующими маршрутами


        //Вызывается каждый раз когда меняем один из списков маршрутов 
        public static void RoutesListUpdate(string TextFileName, ref ObservableCollection<string> RoutesListToUpdate)
        {
            RoutesListToUpdate = new ObservableCollection<string>();
           
            string[] splittedString = new string[3];

            StreamReader f = new StreamReader(TextFileName);
            while (!f.EndOfStream)
            {
                string stringFromFile = f.ReadLine();
                if (stringFromFile == "") { continue; }

                if (RouteName.IsMatch(stringFromFile))
                {
                    splittedString = stringFromFile.Split(":");
                    RoutesListToUpdate.Add(splittedString[0]);
                    continue;
                }
                if (routesMain)
                {
                    RoutesListToUpdate.Add(stringFromFile);
                    continue;
                }
            }
            f.Dispose();

            RoutesListToUpdate = new ObservableCollection<string>(RoutesListToUpdate.OrderBy(i => i));

        }

        //При двойном клике передаем маршрут в очередь(но пока предварительно, не сохраняем)
        private void LB_AddingQueue_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            //Programms.Programms.AGVQueue.Enqueue(LB_AddingQueue.SelectedItem.ToString());
            CurrentQueueList.Clear();
            LB_CurrentQueue.Items.Add(LB_AddingQueue.SelectedItem.ToString());

            foreach (var item in LB_CurrentQueue.Items)
            {
                CurrentQueueList.Add(item.ToString());
            }

        }

        //Если выбрать в ListBox маршрут, он добавляется в CurrentQueueList 
        private void LB_AddingQueue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (LB_AddingQueue.SelectedItem == null) { return; }
                //Переменная для сравнения того что из файла получили с тем что хранится в AGV_Quest

                string[] splittedString = new string[3];
                //Регулярное выражение для вычленения нужного маршрута из файла !Route.txt
                
                Regex RouteName = new Regex("^*:");

                StreamReader f = new StreamReader("!Routes.txt");
                f.Dispose();
                //Находим строку в файле с нужным маршрутом
                if (routesForHomesAdding)
                {
                    f = new StreamReader("!RoutesBetweenBases.txt");
                }
                if(routesCatchers)
                {
                    f = new StreamReader("!Routes.txt");
                }
                if (routesMain)
                {
                    f = new StreamReader("!RoutesMain.txt");
                    f.Dispose();
                    return;
                }

                
                while (!f.EndOfStream)
                {
                    string stringFromFile = f.ReadLine();
                    if (RouteName.IsMatch(stringFromFile))
                    {
                        splittedString = stringFromFile.Split(":");
                        if (splittedString[0] == LB_AddingQueue.SelectedItem.ToString())
                        {

                            break;
                        }
                    }
                }
                f.Dispose();
                TB_RouteName.Text = splittedString[0];
                //Получаем Список всех координат маршрута
                string[] RouteCoordinates = splittedString[1].Split(";");

                int step = 0;
                int program = 0;

                //Привязываем коллекцию RoutesEditor К таблице в которой добавляются новые маршруты.
                RoutesEditor = new ObservableCollection<RouteEditorStorage>();
                foreach (string str in RouteCoordinates)
                {
                    if (str == "") { continue; }
                    MainTaskManager.RouteConverterFromTxtFile(str, out program, out step);
                    RoutesEditor.Add(new RouteEditorStorage() { Program = program.ToString(), Step = step.ToString() });
                }
                //DG_RouteEditor.Items.Clear();
                DG_RouteEditor.ItemsSource=RoutesEditor;
                //foreach (var item in RoutesEditor)
                //{
                //    DG_RouteEditor.Items.Add(item);
                //}
                //DG_RouteEditor.ItemsSource = RoutesEditor;
            }
            catch (Exception ex)
            {
                RoutesSelectorLog.Information(ex.Message + "Строка 97");
               
            }
            
        }

        //Сохранение очереди соответственно
        private void BT_QueueSave_Click(object sender, RoutedEventArgs e)
        {

            //очистка очереди и списка очереди
            MainTaskManager.AGVQueue.Clear();
            CurrentQueueList.Clear();

            foreach (string item in LB_CurrentQueue.Items)
            {

                //Тут сохраняем в очередь
                MainTaskManager.AGVQueue.Enqueue(item);

                //для сохранения в файл txt нужен список
                CurrentQueueList.Add(item);

            }

            MainWindow.ShowCurrentQueue(MainTaskManager.AGVQueue.ToList());

            string[] convert = CurrentQueueList.ToArray();

            File.WriteAllText("!CurrentQueue.txt", string.Empty);
            File.WriteAllLines("!CurrentQueue.txt", convert);

            Popup_QueueSaved.IsOpen = true;

            //sageBox.Show("Текущая очередь изменена и сохранена");

        }
       
        List<string> FileContents = new List<string>();
        //Сохранение нового маршрута из таблицы или редактирование старого

        //Сохранение маршрутов
        private void BT_SaveRoute_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                
                if (routesForHomesAdding)
                {
                    saveRoute("!RoutesBetweenBases.txt", ref RoutesForHomesList);
                }
                if (routesCatchers)
                {
                    saveRoute("!Routes.txt", ref RoutesList);
                }
                if (routesMain)
                {
                    RoutesMainForButtonsList.Add(TB_RouteName.Text);
                    File.WriteAllText("!RoutesMain.txt", string.Empty);
                    File.WriteAllLines("!RoutesMain.txt", RoutesMainForButtonsList.ToArray());
                    LB_AddingQueue.ItemsSource = RoutesMainForButtonsList;


                }
            }
            catch (Exception)
            {
                
            }
            
        }

        private void saveRoute(string TextFileName, ref ObservableCollection<string> RoutesListToSave)
        {
            Regex regex = new Regex(@"^\S+.*");
            if (!regex.IsMatch(TB_RouteName.Text)) { return; }
            RouteEditorStorage editor = new RouteEditorStorage();
            FileContents.Clear();
            //Если маршрут, который пытаемся добавить уже есть, то мы его просто редактируем удаляя и добавляя снова.

            if (RoutesListToSave.Contains(TB_RouteName.Text))
            {
                //во первых считываем все строки из файла и добавляем их в список FileContents
                StreamReader f = new StreamReader(TextFileName);
                while (!f.EndOfStream)
                {
                    string stringFromFile = f.ReadLine();
                    if (stringFromFile == "") { continue; }
                    FileContents.Add(stringFromFile);
                }
                f.Dispose();
                //Теперь удаляем маршрут который редактируем из списка FileContents
                foreach (string item in FileContents)
                {
                    if (item.Contains(TB_RouteName.Text))
                    {
                        FileContents.Remove(item);
                        break;
                    }
                }
                FileContents.Sort();
                //Заполняем файл получившимся списком 
                string[] FileConverted = FileContents.ToArray();
                File.WriteAllText(TextFileName, String.Empty);
                File.WriteAllLines(TextFileName, FileConverted);
            }


            string Step = TB_RouteName.Text + ":";
            //Добавляем маршрут в файл 
            //Для того чтобы если случайно добавили повторяющиеся строки, они не попали в маршрут
            RouteEditorStorage Temp_Item = new RouteEditorStorage();
            foreach (RouteEditorStorage item in RoutesEditor)
            {
                if (item.Program != "" & item.Program != null & item.Step != "" & item.Step != null)
                {
                    if (item.Program == Temp_Item.Program & item.Step == Temp_Item.Step)
                    {
                        continue;
                    }
                    else
                    {
                        Step += $"p{item.Program}s{item.Step};";
                        Temp_Item = item;
                    }
                }
            }


            File.AppendAllText(TextFileName, Step + "\n");

            RoutesListUpdate(TextFileName, ref RoutesListToSave);
            MainWindow.mainWindowClient.CB_RoutesForButtons.ItemsSource = RoutesMainForButtonsList;
            LB_AddingQueue.ItemsSource = RoutesListToSave;
        }

        //ограничение ввода только цифр
        private void DG_RouteEditor_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            int val;
            if (!Int32.TryParse(e.Text, out val))
            {
                e.Handled = true; // отклоняем ввод
                return;
            }
        }

        //тут очищаем выделенный элемент из listbox очередей
        private void LB_CurrentQueue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                CurrentQueueList.Clear();
                if (LB_CurrentQueue.SelectedIndex > -1)
                {
                    LB_CurrentQueue.Items.RemoveAt(LB_CurrentQueue.SelectedIndex);
                    foreach (var item in LB_CurrentQueue.Items)
                    {
                        CurrentQueueList.Add(item.ToString());
                    }
                }
            }
            catch (Exception ex)
            { }
        }

        //Обновление Listbox очереди задач На главном окне
        public static void ShowCurrentQueue(List<string> Queue)
        {
            //Выводим информацию по текущей очереди в Listbox
            try
            {
                RoutesSelectorClient.Dispatcher.Invoke(() =>
                {
                    CurrentQueueList.Clear();
                    RoutesSelectorClient.LB_CurrentQueue.Items.Clear();
                    foreach (var item in Queue)
                    {
                        RoutesSelectorClient.LB_CurrentQueue.Items.Add(item);
                    }
                });
            }
            catch (Exception ex)
            {
                RoutesSelectorLog.Information(ex.Message + "Строка 206");
            }

        }

        private void Window_Closed(object sender, EventArgs e)
        {
            MainWindow.RouteSelectorOpened = false;
        }


        //При нажатии Backspace удаляется строка и датагрид.
        private void DG_RouteEditor_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == System.Windows.Input.Key.Back)
                {
                    foreach (var item in DG_RouteEditor.SelectedCells)
                    {
                        DataGridCellInfo cell = item;
                        
                        if (cell.Item != null)
                        {

                            RoutesEditor.Remove((RouteEditorStorage)cell.Item);
                        
                        }

                    }

                }
                
            }
            catch (Exception ex)
            {
 
            }
        }

        private void BT_QueueSave_MouseLeave(object sender, MouseEventArgs e)
        {
            Popup_QueueSaved.IsOpen = false;
        }

        //при переключении между табами, используется один и тот же listbox, но его заполнение зависит от вкладки. Так сделано из за удобства переключения между табами.
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                try
                {
                    Grid_HomeRoutes.Children.Remove(LB_AddingQueue);
                }
                catch (Exception)
                {

                   
                }
                try
                {
                    Grid_CatchersRoutes.Children.Remove(LB_AddingQueue);
                }
                catch (Exception)
                {


                }
                try
                {
                    Grid_MainRoutes.Children.Remove(LB_AddingQueue);
                }
                catch (Exception)
                {


                }

                if (RoutesPageMain.IsSelected)
                {
                    LB_AddingQueue.ItemsSource = RoutesMainForButtonsList;
                    Grid_MainRoutes.Children.Add(LB_AddingQueue);
                    routesMain = true;
                    routesCatchers = false;
                    routesForHomesAdding = false;
                }
                if (RoutesPageHome.IsSelected)
                {
                    LB_AddingQueue.ItemsSource = RoutesForHomesList;

                    Grid_HomeRoutes.Children.Add(LB_AddingQueue);
                    routesMain = false;
                    routesCatchers = false;
                    routesForHomesAdding = true;
                }
                if (RoutesPageCatchers.IsSelected)
                {
                    LB_AddingQueue.ItemsSource = RoutesList;

                    Grid_CatchersRoutes.Children.Add(LB_AddingQueue);
                    routesMain = false;
                    routesCatchers = true;
                    routesForHomesAdding = false;
                }
            }
            catch (Exception)
            {

            }
            
        }

        private void DG_RouteEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space) { e.Handled = true; }
        }
    }
}
