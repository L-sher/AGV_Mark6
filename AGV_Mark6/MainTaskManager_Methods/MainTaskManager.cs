using AGV_Mark6.Model;
using System;
using AGV_Mark6.Web_Connections;
using static AGV_Mark6.Web_Connections.AGV_Connect;
using System.Threading;
using System.Net;
using System.Linq;
using System.IO;
using Serilog;
using AGV_Mark6.SeriLog;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using AGV_Mark6.ChargingState;




namespace AGV_Mark6.MainTaskManager_Methods
{
    public partial class MainTaskManager
    {
        //Основной класс, где вычисляются маршруты АГВ, вычисляются как АГВ едет, логика и алгоритм передвижения АГВ. А также получение задач
        #region Переменные 

        public static bool ProgramIsEditing = false;

        //Параметры состояния АГВ
        public static bool WithoutTransition = false;
        public static string LoadStatus = "Пустая";
        public static int TransitionMissCount = 0;
        public static bool AGVOnCharge = false;
        public static int AGV_Step_OnCharge = 0;
        public static int AGV_Program_OnCharge = 0;

        //Очередь Задач для АГВ
        public static Queue<string> AGVQueue = new Queue<string>();
        //переменная хранящая состояние текущего задания для агв, если не пустая то АГВ выполняет задачу из списка текущих задач.
        public static string AGV_Quest = "";
        
        public enum AGVLoadStatusList { WIthProducts, WithoutProducts, Empty };

        public static AGVLoadStatusList AGVLoadStatus = AGVLoadStatusList.Empty;

        public static Serilog.Core.Logger ProgrammsLog = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.File(new MyOwnCompactJsonFormatter(), "AGV-Log_Programms.txt").CreateLogger();

        public static AGV_Storage_Context db = new AGV_Storage_Context();
        
        static int CurrentStep = 0;
        static int CurrentProgram = 0;

        public static int step = 0;
        public static int programm = 0;

        //переменная для чтения текущего Home, которая также ограничивает выполнение основного алгоритма
        public static bool OnlyReadHome = false;

        //Определяем какие действия нужно выполнить на данном шаге и выполняем их.


        public static bool ProgramIsOnConnect = false;


        public static List<RouteEditorStorage> AGV_Task_Route = new List<RouteEditorStorage>();
        public static int CurrentTaskStepNumber = 0;

        //Переменная для отслеживания последнего Base
        public static string LastBase = "";
        //Переменная для ограничения старта агв, после остановки в в ловителе в ожидании старта именно от кнопки на складе! не после того как она получила задачу, так как после получения ProgramIsRunning=true;
        public static bool WaitingForButtonPermission = false;
        #endregion



        //Основной Метод в котором запущен поток.
        public static void StartAGVManager()
        {
            CurrentStep = 0;
            CurrentProgram = 0;

            //Пока редактирование программ Не происходит, АГВ работает
            while (true)
            {
                try
                {
                    //Если на зарядке
                    if (AGVOnCharge)
                    {
                        Thread.Sleep(3000); continue;
                    }

                    //Если АГВ на Зарядке
                    if (Control_AGV.AGV_ChargedUp==1) 
                    { 
                        Thread.Sleep(3000); continue; 
                    }
                    else
                    {
                        if (ProgramIsRunning & !WaitingForButtonPermission)
                        {
                            //отключаем мониторинг регистров АГВ, так как у нас в этом потоке будет тоже самое
                            OnlyReadRegistersAGV = false;

                            ReadAGVRegisters();
                        }
                        else { Thread.Sleep(1000); continue; }
                    }
                    //Если сетка отвалилась
                    if (ProgramIsOnConnect)
                    {
                        Thread.Sleep(500);
                        continue;
                    }

                    //Если программа редактируется
                    if (ProgramIsEditing == true)
                    {
                        StopEventMethod();
                        Thread.Sleep(500);
                        ProgramIsRunning = false;
                        continue;
                    }

                    //Если таблица registerMap не пуста.
                    if (registerMap != null)
                    {

                        CurrentStep = registerMap["Текущий шаг"];
                        CurrentProgram = registerMap["Текущая программа"];

                    }
                }
                catch (NullReferenceException e) {  }

                try
                {
                    //Если включен режим работы от кнопок
                    if (MainWindow.Mode_ButtonsOnly)
                    {
                        if (AGVQueue.Count == 0 & AGV_Quest == "")
                        {

                            ProgramIsRunning = false;
                            MainWindow.AGVRegistersShow();
                            MainWindow.AddToTB_TB_Ping_Log("В этом режиме при пустой очереди работа невозможна.");
                            continue;

                        }
                        
                        if (AGVQueue.Count > 0)
                        {
                            if (AGV_Quest != "")
                            {
                                if (step == CurrentStep & programm == CurrentProgram)
                                {
                                    StartEventMethod();
                                    AGVConnectLog.Information("Старт отправлен 147");
                                    continue;
                                }

                            }
                            else
                            {

                                OnlyReadHome = true;
                                whichProgramToChoose(CurrentProgram, CurrentStep);
                                if (!KeyToHome)
                                {

                                    ProgramIsRunning = false;
                                    MainWindow.AddToTB_TB_Ping_Log("За пределы заданий в этом режиме выходить нельзя. Поправьте маршруты");
                                    continue;

                                }

                            }
                        }
                        if (AGVQueue.Count == 0 && AGV_Quest != "")
                        {

                        }

                        //Проверяем выполняли ли мы действия для данного шага, если выполняли то возврат
                        if (step == CurrentStep & programm == CurrentProgram & AGV_Quest!="")
                        {
                            StartEventMethod();
                            AGVConnectLog.Information("Старт отправлен 175");
                            continue;
                        }
                        //Вывел хвост, который не знал как назвать в отдельный метод
                        StartAGVManagerPart2();
                        continue;

                    }
                    else
                    {

                        //Проверяем выполняли ли мы действия для данного шага, если выполняли то возврат
                        if (step == CurrentStep & programm == CurrentProgram)
                        {

                            //Это проверка на то что мы получили задачу от кнопок
                            if (!Modbus_Server_For_Buttons.Class_ModbusServer_for_Buttons.AGV_Button_Clicked)
                            {

                                StartEventMethod();
                                AGVConnectLog.Information("Старт отправлен 194");
                                continue;

                            }
                            else
                            {
                                Modbus_Server_For_Buttons.Class_ModbusServer_for_Buttons.AGV_Button_Clicked = false;
                            }

                        }
                        //Вывел хвост, который не знал как назвать в отдельный метод
                        StartAGVManagerPart2();

                    }
                }
                catch (Exception e)
                {

                    ProgrammsLog.Information($"{e.Message} 193");
                }
                
            }

        }

        //Вывод в окно мониторинга и запуск алгоритма передвижения АГВ по маршрутам
        private static void StartAGVManagerPart2()
        {
            //Вывод корректных данных в окно мониторинга
            if (AGV_Quest != "")
            {
                if (CurrentTaskStepNumber > 0)
                {

                    if (int.Parse(AGV_Task_Route.FirstOrDefault(e => e.id == CurrentTaskStepNumber - 1).Step) == CurrentStep & int.Parse(AGV_Task_Route.FirstOrDefault(e => e.id == CurrentTaskStepNumber - 1).Program) == CurrentProgram)
                    {

                        MainWindow.AGVRegistersShow();

                    }

                }

            }
            else
            {

                MainWindow.AGVRegistersShow();

            }


            ProgrammsLog.Information($"Начат Шаг {CurrentStep} Программа {CurrentProgram}");
            ProgrammsLog.Information($"\n __ P{CurrentProgram}  S{CurrentStep} статус загрузки:{LoadStatus} очередь:{AGVQueue.Count} Последняя Home:{LastBase}  AwaitingTransition:{AwaitingTransition} Перед проверкой Home");

            //переход для основного алгоритма передвижения АГВ.
            whichProgramToChoose(CurrentProgram, CurrentStep);

            //Запоминаем какой шаг мы прошли успешно, чтобы не повторять выполнение этого шага
            step = CurrentStep;
            programm = CurrentProgram;
        }

        //В зависимости от программы выбираем таблицу из базы данных с которой будем работать. Всего программ 10.
        public static void whichProgramToChoose(int CurrentProgramThis, int CurrentStepThis)
        {
            //Отличие в том, что в SQLite таблицы для разных программ разные. пришлось вот так сделать. Не смог по-другому

            switch (CurrentProgramThis)
            {
                case 1:
                    try
                    {

                        Prog1? Current_Status_AGV = db.Prog1.Where(e => e.Step == CurrentStepThis.ToString()).FirstOrDefault();

                        //только прочитываем Home в точке
                        if (OnlyReadHome)
                        {
                            ReadCurrentHome(Current_Status_AGV);
                            OnlyReadHome = false;
                            break;
                        }

                        //Задел под ошибку отсутствия линии движения
                        if (LineCannotBeFound)
                        {
                            CurrentStep = CurrentStepThis;
                            CurrentProgram = CurrentProgramThis;
                            MoveStraightTheCycle(Current_Status_AGV);
                            LineCannotBeFound = false;
                            break;
                        }
                        //Передаем необходимую таблицу в основной алгоритм и работаем с ней.
                        ExecuteProgram(Current_Status_AGV);

                    }
                    catch (Exception ex)
                    {
                        ProgrammsLog.Information(ex.Message.ToString() + $"на программе {CurrentProgramThis} и шаге {CurrentStepThis}");
                    }
                    break;
                case 2:
                    try
                    {
                        Prog2? Current_Status_AGV = db.Prog2.Where(e => e.Step == CurrentStepThis.ToString()).FirstOrDefault();
                        if (OnlyReadHome)
                        {
                            ReadCurrentHome(Current_Status_AGV);
                            OnlyReadHome = false;
                            break;
                        }
                        if (LineCannotBeFound)
                        {
                            CurrentStep = CurrentStepThis;
                            CurrentProgram = CurrentProgramThis;
                            MoveStraightTheCycle(Current_Status_AGV);
                            LineCannotBeFound = false;
                            break;

                        }
                        //Передаем необходимую таблицу в основной алгоритм и работаем с ней.
                        ExecuteProgram(Current_Status_AGV);

                    }
                    catch (Exception ex)
                    {
                        ProgrammsLog.Information(ex.Message.ToString() + $"на программе {CurrentProgramThis} и шаге {CurrentStepThis}");
                    }
                    break;
                case 3:
                    try
                    {
                        Prog3? Current_Status_AGV = db.Prog3.Where(e => e.Step == CurrentStepThis.ToString()).FirstOrDefault();
                        if (OnlyReadHome)
                        {
                            ReadCurrentHome(Current_Status_AGV);
                            OnlyReadHome = false;
                            break;
                        }
                        if (LineCannotBeFound)
                        {
                            CurrentStep = CurrentStepThis;
                            CurrentProgram = CurrentProgramThis;
                            MoveStraightTheCycle(Current_Status_AGV);
                            LineCannotBeFound = false;
                            break;

                        }
                        //Передаем необходимую таблицу в основной алгоритм и работаем с ней.
                        ExecuteProgram(Current_Status_AGV);

                    }
                    catch (Exception ex)
                    {
                        ProgrammsLog.Information(ex.Message.ToString() + $"на программе {CurrentProgramThis} и шаге {CurrentStepThis}");
                    }
                    break;
                case 4:
                    try
                    {
                        Prog4? Current_Status_AGV = db.Prog4.Where(e => e.Step == CurrentStepThis.ToString()).FirstOrDefault();
                        if (OnlyReadHome)
                        {
                            ReadCurrentHome(Current_Status_AGV);
                            OnlyReadHome = false;
                            break;
                        }
                        if (LineCannotBeFound)
                        {
                            CurrentStep = CurrentStepThis;
                            CurrentProgram = CurrentProgramThis;
                            MoveStraightTheCycle(Current_Status_AGV);
                            LineCannotBeFound = false;
                            break;

                        }
                        //Передаем необходимую таблицу в основной алгоритм и работаем с ней.
                        ExecuteProgram(Current_Status_AGV);
                    }
                    catch (Exception ex)
                    {
                        ProgrammsLog.Information(ex.Message.ToString() + $"на программе {CurrentProgramThis} и шаге {CurrentStepThis}");
                    }
                    break;
                case 5:
                    try
                    {
                        Prog5? Current_Status_AGV = db.Prog5.Where(e => e.Step == CurrentStepThis.ToString()).FirstOrDefault();
                        if (OnlyReadHome)
                        {
                            ReadCurrentHome(Current_Status_AGV);
                            OnlyReadHome = false;
                            break;
                        }
                        if (LineCannotBeFound)
                        {
                            CurrentStep = CurrentStepThis;
                            CurrentProgram = CurrentProgramThis;
                            MoveStraightTheCycle(Current_Status_AGV);
                            LineCannotBeFound = false;
                            break;

                        }
                        //Передаем необходимую таблицу в основной алгоритм и работаем с ней.
                        ExecuteProgram(Current_Status_AGV);
                    }
                    catch (Exception ex)
                    {
                        ProgrammsLog.Information(ex.Message.ToString() + $"на программе {CurrentProgramThis} и шаге {CurrentStepThis}");
                    }
                    break;
                case 6:
                    try
                    {
                        Prog6? Current_Status_AGV = db.Prog6.Where(e => e.Step == CurrentStepThis.ToString()).FirstOrDefault();
                        if (OnlyReadHome)
                        {
                            ReadCurrentHome(Current_Status_AGV);
                            OnlyReadHome = false;
                            break;
                        }
                        if (LineCannotBeFound)
                        {
                            CurrentStep = CurrentStepThis;
                            CurrentProgram = CurrentProgramThis;
                            MoveStraightTheCycle(Current_Status_AGV);
                            LineCannotBeFound = false;
                            break;

                        }
                        //Передаем необходимую таблицу в основной алгоритм и работаем с ней.
                        ExecuteProgram(Current_Status_AGV);
                    }
                    catch (Exception ex)
                    {
                        ProgrammsLog.Information(ex.Message.ToString() + $"на программе {CurrentProgramThis} и шаге {CurrentStepThis}");
                    }
                    break;
                case 7:
                    try
                    {
                        Prog7? Current_Status_AGV = db.Prog7.Where(e => e.Step == CurrentStepThis.ToString()).FirstOrDefault();
                        if (OnlyReadHome)
                        {
                            ReadCurrentHome(Current_Status_AGV);
                            OnlyReadHome = false;
                            break;
                        }
                        if (LineCannotBeFound)
                        {
                            CurrentStep = CurrentStepThis;
                            CurrentProgram = CurrentProgramThis;
                            MoveStraightTheCycle(Current_Status_AGV);
                            LineCannotBeFound = false;
                            break;

                        }
                        //Передаем необходимую таблицу в основной алгоритм и работаем с ней.
                        ExecuteProgram(Current_Status_AGV);
                    }
                    catch (Exception ex)
                    {
                        ProgrammsLog.Information(ex.Message.ToString() + $"на программе {CurrentProgramThis} и шаге {CurrentStepThis}");
                    }
                    break;
                case 8:
                    try
                    {
                        Prog8? Current_Status_AGV = db.Prog8.Where(e => e.Step == CurrentStepThis.ToString()).FirstOrDefault();
                        if (OnlyReadHome)
                        {
                            ReadCurrentHome(Current_Status_AGV);
                            OnlyReadHome = false;
                            break;
                        }
                        if (LineCannotBeFound)
                        {
                            CurrentStep = CurrentStepThis;
                            CurrentProgram = CurrentProgramThis;
                            MoveStraightTheCycle(Current_Status_AGV);
                            LineCannotBeFound = false;
                            break;

                        }
                        //Передаем необходимую таблицу в основной алгоритм и работаем с ней.
                        ExecuteProgram(Current_Status_AGV);
                    }
                    catch (Exception ex)
                    {
                        ProgrammsLog.Information(ex.Message.ToString() + $"на программе {CurrentProgramThis} и шаге {CurrentStepThis}");
                    }
                    break;
                case 9:
                    try
                    {
                        Prog9? Current_Status_AGV = db.Prog9.Where(e => e.Step == CurrentStepThis.ToString()).FirstOrDefault();
                        if (OnlyReadHome)
                        {
                            ReadCurrentHome(Current_Status_AGV);
                            OnlyReadHome = false;
                            break;
                        }
                        if (LineCannotBeFound)
                        {
                            CurrentStep = CurrentStepThis;
                            CurrentProgram = CurrentProgramThis;
                            MoveStraightTheCycle(Current_Status_AGV);
                            LineCannotBeFound = false;
                            break;

                        }
                        //Передаем необходимую таблицу в основной алгоритм и работаем с ней.
                        ExecuteProgram(Current_Status_AGV);
                    }
                    catch (Exception ex)
                    {
                        ProgrammsLog.Information(ex.Message.ToString() + $"на программе {CurrentProgramThis} и шаге {CurrentStepThis}");
                    }
                    break;
                case 10:
                    try
                    {
                        Prog10? Current_Status_AGV = db.Prog10.Where(e => e.Step == CurrentStepThis.ToString()).FirstOrDefault();
                        if (OnlyReadHome)
                        {
                            ReadCurrentHome(Current_Status_AGV);
                            OnlyReadHome = false;
                            break;
                        }
                        if (LineCannotBeFound)
                        {
                            CurrentStep = CurrentStepThis;
                            CurrentProgram = CurrentProgramThis;
                            MoveStraightTheCycle(Current_Status_AGV);
                            LineCannotBeFound = false;
                            break;

                        }
                        //Передаем необходимую таблицу в основной алгоритм и работаем с ней.
                        ExecuteProgram(Current_Status_AGV);
                    }
                    catch (Exception ex)
                    {
                        ProgrammsLog.Information(ex.Message.ToString() + $"на программе {CurrentProgramThis} и шаге {CurrentStepThis}");
                    }
                    break;
                default:
                    break;
            }

        }

        //основной алгоритм передвижения и получения задач
        public static void ExecuteProgram(Prog Current_Status_AGV)
        {
            //Если это конец программы, то обнуляем параметры маршрута.
            if (AGV_Quest!="" && Current_Status_AGV.Stop == "+")
            {

                //StopEventMethod();
                WaitingForButtonPermission = true;
                MainWindow.SavedAGVStatuses.OnAppStart_TransitionMissCount = "";
                MainWindow.SavedAGVStatuses.Save();
                ProgramIsRunning = false;
                while (WaitingForButtonPermission)
                {
                    Thread.Sleep(300);
                }

            }
            //Проверяем статус загруженности АГВ и Присваиваем статус загруженности
            if (Current_Status_AGV.LoadStatus != null)
            {
                if (Current_Status_AGV.LoadStatus != "" & Current_Status_AGV.LoadStatus != " ")
                {
                    MainWindow.SavedAGVStatuses.OnAppStart_loadStatus = "Загруженность:" + Current_Status_AGV.LoadStatus;
                    MainWindow.SavedAGVStatuses.Save();
                    LoadStatus = Current_Status_AGV.LoadStatus;

                    //TODO:Эмулятор
                    if (MainWindow.Emulator)
                    {
                        
                        if (LoadStatus == "Загружена" | LoadStatus == "С тележкой")
                        {
                            AGV_Connect.AGV_Client.WriteMultipleRegisters(80, new int[] { 1 });
                        }
                        else
                        {
                            AGV_Connect.AGV_Client.WriteMultipleRegisters(70, new int[] { 1 });
                        }

                    }

                }
            }

            //Проверяем Становится ли АгВ на зарядку на текущем шаге. если становится, то останавливаем программу и АГВ
            if (Current_Status_AGV.AdditionCommand == "Зарядка" & Control_AGV.AGV_ChargedUp==2)
            {
                try
                {
                    //останавливаем текущую программу так как АГВ на зарядке
                    StopEventMethod();
                    AGVOnCharge = true;

                    //Запускаем Прослеживание того, заряжается ли АГВ
                    new Thread(ChargingUp.ChargingWatchDog).Start(); 
                    additionCoordinates prog = db.AdditionCoordinates.FirstOrDefault(e => e.Id == 1);

                    if (prog != null)
                    {
                        prog.Step = CurrentStep.ToString();
                        prog.Program = CurrentProgram.ToString();
                        db.SaveChanges();
                    }

                    string charged = AGVQueue.ElementAt(0);
                    Regex GoToChargeRoute = new Regex($"^Зарядка.*home.*");
                    MainWindow.CancelCurrentAGVTask();

                    if (GoToChargeRoute.IsMatch(charged))
                    {
                        DequeueTask();
                    }

                    MainWindow.CancelCurrentAGVTask();
                    ShowCurrentTaskAndCurrentQueueInMainWindow();

                    MainWindow.AddToTB_TB_Ping_Log("АГВ приехало на зарядку. Просьба зарядить АГВ \nКнопки Старт не работают!");
                  
                    return;
                }
                catch (Exception)
                {
                    ProgrammsLog.Information("Координаты зарядки потеряны");
                }
            }

            //Отправка в телеграм текущего шага и программы
            if (Current_Status_AGV.Notification == "+")
            {
                NotificationTelegram($"Шаг {CurrentStep.ToString()} Программа {CurrentProgram.ToString()}");
            }
           
            //Проверка выполняет ли АГВ какую-либо задачу.
            if (AGV_Quest != "")
            {
                //Выполняем задачу по маршруту
                bool TaskFinished = false;
                ExecuteNextTaskStep(out TaskFinished);
                if (TaskFinished==false)
                {
                    return;
                }
                //ReadAGVRegisters();
                return;
            }

            //проверка Home
            ReadCurrentHome(Current_Status_AGV);

            //Проверяем можем ли мы сейчас дать задание АГВ
            //Если мы в точке Home и АГВ Пустая и Очередь задач не пустая, то выдаем задание
            if (Home != null)
            {
                ProgrammsLog.Information($"\n __ P{CurrentProgram}  S{CurrentStep} статус загрузки:{LoadStatus} очередь:{AGVQueue.Count} Имя точки:{Home.PointName}  AwaitingTransition:{AwaitingTransition} Перед проверкой Home" );

                if(Home.PointName != "" & LoadStatus == "Пустая" & AGVQueue.Count==0 & MainWindow.Mode_ButtonsOnly)
                {

                    MainWindow.AddToTB_TB_Ping_Log("Очередь Пуста, Агв ждет команд в этом режиме");
                    StopEventMethod();
                    return;

                }

                //Получаем задачу
                if (Home.PointName != "" & LoadStatus == "Пустая" & AGVQueue.Count > 0)
                {

                    //Получаем задачу
                    GettingTask();
                    return;

                }

                //В режиме от кнопок АГВ получает задачи даже если она нагружена. просто она в любом случае выполняет полный цикл загрузки разгрузки,
                //Для того чтобы не прописывать маршруты между базами, агв сама переходит между ними.
                if(MainWindow.Mode_ButtonsOnly & Home.PointName != "" & AGVQueue.Count > 0)
                {

                    //Получаем задачу
                    GettingTask();
                    return;

                }

            }
            if (!MainWindow.Mode_ButtonsOnly)
            {
                //Двигаемся по главной программе АГВ(по циклу)
                MoveStraightTheCycle(Current_Status_AGV);
            }
            
            if(MethodEnded)
            {

                MethodEnded = false;
                return;

            }

            //Если это конец программы, то обнуляем параметры маршрута.
            if (Current_Status_AGV.Stop == "+")
            {

                StopEventMethod();
                MainWindow.SavedAGVStatuses.OnAppStart_TransitionMissCount = "";
                MainWindow.SavedAGVStatuses.Save();

            }
            ProgrammsLog.Information($" Выполнен Шаг {CurrentStep} Программа {CurrentProgram}");
           // StartEventMethod();
        }

        static bool MethodEnded = false;

        //Отправляем сообщение в телеграм
        public static void NotificationTelegram(string MessageText)
        {
            try
            {
                string apilToken = "5673439398:AAExNfKSTMMKE0VaMzvJMcAYCkFz3dfvqBA";

                string destID = "-1001562959605";

                string urlString = $"https://api.telegram.org/bot{apilToken}/sendMessage?chat_id={destID}&text={MessageText}";

                WebClient webclient = new WebClient();
                webclient.DownloadString(urlString);
            }
            catch (Exception ex)
            {
                ProgrammsLog.Error("NotificationTelegram Невозможно отправить сообщение в Телеграм");
            }
        }
    }
}
