using EasyModbus;
using System;
using System.Collections.Generic;
using Serilog;
using AGV_Mark6.SeriLog;
using System.Threading;
using System.Diagnostics;
using AGV_Mark6.MainTaskManager_Methods;




namespace AGV_Mark6.Web_Connections
{
    public class AGV_Connect
    {
        //класс отвечает за подключение к АГВ и прочтение регистров.
        
        #region Переменные
        //Класс для подключения к агв
        public static ModbusClient AGV_Client = new ModbusClient();
        public static FluentModbus.ModbusTcpClient FluentClient = new FluentModbus.ModbusTcpClient();
       
        //логгер Соединений c АГВ
        public static Serilog.Core.Logger AGVConnectLog = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.File(new MyOwnCompactJsonFormatter(), "AGV-Log_ModbusConnection.txt").CreateLogger();

        //Логгер критических ошибок
        public static Serilog.Core.Logger CriticalAlertsLog = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.File(new MyOwnCompactJsonFormatter(), "AGV-Log_ModbusConnection.txt").CreateLogger();

        public static Stopwatch StepsStopwatch = new Stopwatch();

      

        //Словарь через который отображаются регистры в таблице на MainWindow
        public static Dictionary<string, int> registerMap = new Dictionary<string, int>()
            {
                    {"Текущая программа"                            ,0    },
                    {"Текущий шаг"                                  ,0    },
                    {"Следующая очередь"                            ,0    },
                    {"Последняя прочитанная метка"                  ,0    },
                    {"Слово жизни ( случайное число )"              ,0    },
                    {"Метка по текущей программе"                   ,0    },
                    {"Авария: Объект в зоне предупреждения 0"       ,0    },
                    {"Критичная авария"                             ,0    }
    };

        //Словарь для проверки корректности регистров, после проверки того что шаги не отрицательные(Агв Может прислать отрицательный шаг если слишком маленькие тайминги)
        public static Dictionary<string, int> Temp_registerMap = new Dictionary<string, int>();


        //Переменная, которая определяет, запущена ли наша программа для АГВ или нет
        public static bool ProgramIsRunning = false;

        //Булева для того чтобы просто отслеживать регистры агв, а не управлять и не запускать программу
        public static bool OnlyReadRegistersAGV = false;

        //Локер для ограничения доступа потоков
        static object block = new object();


        //Задел под отработку ошибки отсутствия линии. Будет работать только в том случае если АГВ отправлять по координатам куда-то.
        public static bool LineCannotBeFound = false;

        public enum AGVregisters : int
        {
            Controll = 62207,               // Сигналы управления AGV
            Current_Queue = 60208,          // Текущая очередь
            Current_Programm = 60209,       // Текущая программа
            Current_Step = 60210            // Текущий шаг
        }
        #endregion

        //Подключение к AGV
        public static void StartListeningAGV()
        {
            try
            {
                AGVConnectLog.Error("Выполняется подключение к АГВ");
                //подключаемся
                AGVReconnect();
            }
            catch (Exception ex)
            {
                AGVConnectLog.Error("ошибка переподключения к АГВ"+ ex.Message);
            }
            

            while (true)
            {

                try
                {
                
                    if (OnlyReadRegistersAGV == false) { Thread.Sleep(500); continue; }
                    ReadAGVRegisters();
                    Thread.Sleep(150);
                   
                    //обработка критических аварий
                    //CriticalAlertsWatchDog();

                }
                catch (Exception ex)
                {

                    //Пробуем переподключиться при ошибках
                    while (true)
                    {
                        try
                        {

                            AGVReconnect();
                            Thread.Sleep(1000);
                            break;

                        }
                        catch (Exception exe)
                        {
                          
                            Thread.Sleep(1000);
                            continue;

                        }
                    }

                }

            }

        }

        //Основной метод, через который получаем параметры с АГВ
        public static void ReadAGVRegisters()
        {

            //ID Потока
            int hash = Thread.CurrentThread.GetHashCode();

            lock (block)
            {
                try
                {
                    //Получаем регистры с АГВ. Больше регистров записаны в файле 'Карта IP Адресов и протокол обмена' Excel.
                    //Если потеряны, спросить у разработчика Николая.
                    Temp_registerMap = new Dictionary<string, int>()
                    {
                    {"Текущая программа"                          , AGV_Client.ReadHoldingRegisters(60209, 1)[0]    },
                    {"Текущий шаг"                                , AGV_Client.ReadHoldingRegisters(60210, 1)[0]    },
                    {"Последняя прочитанная метка"                , AGV_Client.ReadHoldingRegisters(60218, 1)[0]    },
                    {"Метка по текущей программе"                 , AGV_Client.ReadHoldingRegisters(60220, 1)[0]    },
                    {"Авария: Объект в зоне предупреждения 0"     , AGV_Client.ReadHoldingRegisters(60201, 1)[0]    },
                    {"Критичная авария"                           , AGV_Client.ReadHoldingRegisters(60200, 11)[0]    }
                    };

                    //АГВ может прислать отрицательный шаг, Это негативно влияет на менеджер, поэтому сделал следующие проверки
                    if (Temp_registerMap["Текущий шаг"] < 1 || Temp_registerMap["Текущий шаг"] > 100 || Temp_registerMap["Текущая программа"] > 10 || Temp_registerMap["Текущая программа"] < 1)
                    {
                        MainWindow.AddToTB_TB_Ping_Log("Неверные данные от АГВ, Вынужденная остановка");
                        AGVReconnect();
                        if (ProgramIsRunning)
                        {

                            MainTaskManager.StopEventMethod();
                            Thread.Sleep(300);
                            if (MainTaskManager.AwaitingTransition == true)
                            {
                                AGV_Connect.ProgramIsRunning = true;
                                return;
                            }
                            if (MainTaskManager.AGV_Quest != "")
                            {
                                AGV_Connect.ProgramIsRunning = true;
                                return;
                            }
                            AGV_Connect.ProgramIsRunning = true;

                            MainTaskManager.StartEventMethod();
                            AGVConnectLog.Information("Старт отправлен 177 ReadAGVRegisters ");
                        }
                        Thread.Sleep(1000);
                        return;
                    }

                    //Если повторяются показания
                    if (Temp_registerMap["Текущий шаг"] == registerMap["Текущий шаг"] & Temp_registerMap["Текущая программа"] == registerMap["Текущая программа"])
                    {
                        //Задел под обработку отсутствия линии движения
                        //if (Temp_registerMap["Критичная авария"] == 2048)
                        //{
                        //    LineCannotBeFound = true;
                        //    MainTaskManager.whichProgramToChoose(Temp_registerMap["Текущая программа"], registerMap["Текущий шаг"]);
                        //}
                        Thread.Sleep(800);
                        return;
                    }
                    //Если выполняется переход, то пока не прочитаем шаги на которые должны были перейти в мониторинг они не попадают
                    if (MainTaskManager.AwaitingTransition == true)
                    {
                        //Если выполнен переход и мы считываем шаг и программу, которые были идентификаторами перехода и не нужны нам для мониторинга
                        if (Temp_registerMap["Текущий шаг"] == MainTaskManager.RightStepToPass & Temp_registerMap["Текущая программа"] == MainTaskManager.RightProgramToPass)
                        {

                            //Прочитываем Home текуще точки
                            MainTaskManager.OnlyReadHome = true;
                            MainTaskManager.whichProgramToChoose(Temp_registerMap["Текущая программа"], Temp_registerMap["Текущий шаг"]);

                            
                            registerMap = Temp_registerMap;

                            MainTaskManager.RightStepToPass = 0;
                            MainTaskManager.RightProgramToPass = 0;

                            //Если переход на другой шаг не выполняется
                            //MainWindow.AGVRegistersShow();
                            MainWindow.DG_Registers_ShowActual();
                            MainTaskManager.AwaitingTransition = false;
                            AGVConnectLog.Debug($"Переход на Программу{Temp_registerMap["Текущая программа"]} шаг {Temp_registerMap["Текущий шаг"]}  Выполнен AwaitingTransition= {MainTaskManager.AwaitingTransition} ") ;
                        }
                        else
                        {

                            registerMap = Temp_registerMap;

                        }

                    }
                    else
                    {
                        //Прочитываем Home текуще точки
                        MainTaskManager.OnlyReadHome = true;
                        MainTaskManager.whichProgramToChoose(Temp_registerMap["Текущая программа"], Temp_registerMap["Текущий шаг"]);

                        registerMap = Temp_registerMap;

                        MainTaskManager.RightStepToPass = 0;
                        MainTaskManager.RightProgramToPass = 0;

                        //Если переход на другой шаг не выполняется
                        //MainWindow.AGVRegistersShow();
                        MainWindow.DG_Registers_ShowActual();
                    }

                    //Здесь Тайминг установил такой, так как АГВ если получает много запросов, начинает отправлять хрень
                    if (MainWindow.Emulator)
                    {
                       // Thread.Sleep(800);
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                    
                    AGVConnectLog.Debug( "Регистры прочитаны" +"_S"+Temp_registerMap["Текущий шаг"].ToString()+" P"+ Temp_registerMap["Текущая программа"].ToString() + "Метка:  "+ Temp_registerMap["Последняя прочитанная метка"]);
                   
                }
                catch (Exception ex)
                {
                    AGVReconnect();
                    AGVConnectLog.Error(ex.Message + "строка 294");
                }

            }

        }

        //Обработка Аварий
        //Здесь разные числа с регистра указывают на разные ошибки, я все вроде не нашел, так как не было необходимости
        //переменная запоминает последнюю ошибку, чтобы не повторяться
        int AlertmarkerCrit = 0;
        void CriticalAlertsWatchDog()
        {
            try
            {
                if (registerMap["Критичная авария"] != 0 & registerMap["Критичная авария"] != 10240)
                {
                    if (AlertmarkerCrit != registerMap["Критичная авария"])
                    {
                        if (registerMap["Критичная авария"] == 2048)
                        {

                        }
                        if (registerMap["Критичная авария"] == 1024)
                        {
                            CriticalAlertsLog.Error("Критичная авария" + " ошибка связи с Лидаром " + registerMap["Критичная авария"]);
                            return;
                        }
                        if (registerMap["Критичная авария"] == 10240)
                        {

                        }
                        if (registerMap["Критичная авария"] == 3)
                        {
                            CriticalAlertsLog.Error("Критичная авария" + " нажата кнопка СТОП " + registerMap["Критичная авария"]);
                            return;
                        }
                        AlertmarkerCrit = registerMap["Критичная авария"];

                        CriticalAlertsLog.Error("Критичная авария " + registerMap["Критичная авария"]);

                        MainTaskManager.NotificationTelegram("Критичная авария "  + registerMap["Критичная авария"]);

                    }
                }
            }
            catch ( Exception ex)
            {
                AGVConnectLog.Error("Ошибка на этапе критических исключений" + ex.Message);
            }
            
        }

        //переподключение к АГВ
        public static void AGVReconnect()
        {
            while (true)
            {
                try
                {
                    AGV_Client.Disconnect();
                    Thread.Sleep(500);
                    if (MainWindow.Emulator)
                    {

                        //TODO: Эмулятор
                        AGV_Client.UnitIdentifier = 1;
                        AGV_Client.Connect("192.168.96.139", 501);

                    }
                    else
                    {
                        //TODO:Рабочая
                        AGV_Client.ConnectionTimeout = 500;
                        AGV_Client.UnitIdentifier = 255;
                        AGV_Client.Connect("10.1.101.2", 502);
                    }
                    AGVConnectLog.Error("Выполнено переподключение к АГВ");
                    MainTaskManager.ProgramIsOnConnect = false;
                    break;
                }
                catch (Exception ex)
                {
                    AGVConnectLog.Error("ошибка переподключения к АГВ");
                    MainTaskManager.ProgramIsOnConnect = true;
                }
            }
        }
    }
}

