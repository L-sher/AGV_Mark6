using AGV_Mark6.SeriLog;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace AGV_Mark6.Web_Connections
{
    internal class Ping_Agv
    {
        //В этом Классе постоянно проверяется связь с АГВ.
        //Отрабатывается в отдельном потоке

        //Объявление переменных и объектов классов
        #region //переменные и объекты классов

        public static Serilog.Core.Logger PingLog = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.File(new MyOwnCompactJsonFormatter(), "AGV-Log_Ping.txt").CreateLogger();

        //LogHelper Для того чтоб логи не лопнули
        static string LogHelper = "";

        //далее экземпляры пинга для каждого элемента АГВ
        static Ping Microtic_WIFI_Ping = new Ping();
        static Ping AGV_PLC_Ping = new Ping();
        static Ping AGV_SickNanoScan_Ping = new Ping();
        static Ping AGV_Lidar_Ping = new Ping();

        //Для подсчета времени, когда АГВ не в сети
        static Stopwatch AGV_Ping_WatchDog;

        //для хранения статусов каждого компонента АГВ
        static string statuses;

        //Переменная определяющая в сети агв или нет
        public static bool AGVIsOnline=false;
        #endregion

        //Пинг АГВ и монитор его отсутствия в сетке. Лог в Файл AGV-Log_Ping.txt
        public static void Method_Ping_Agv()
        {
            try
            {
                PingLog.Information("Мониторинг АГВ Запущен");

                AGV_Ping_WatchDog = new Stopwatch();

                PingReply Pinging = null;

                //Список компонентов АГВ к которым есть доступ в сети
                Dictionary<Ping, string[]> PingReplies = new Dictionary<Ping, string[]>()
            {
                { Microtic_WIFI_Ping,       new string[]{   "Микротик",    "10.1.101.1" } },//.SendPingAsync("10.1.101.1"),
                { AGV_PLC_Ping,             new string[]{   "ПЛК",         "10.1.101.2" } },//.SendPingAsync("10.1.101.2"),//ПЛК
                { AGV_SickNanoScan_Ping,    new string[]{   "Камера",      "10.1.101.3" } },//.SendPingAsync("10.1.101.3"),//Камера
                { AGV_Lidar_Ping,           new string[]{   "Лидар",       "10.1.101.4" } }//.SendPingAsync("10.1.101.4")// Лидар
            };

                //Основной цикл, постоянно проверяющий АГВ в сети или нет
                while (true)
                {
                    foreach (var item in PingReplies)
                    {
                        try
                        {
                            Pinging = item.Key.Send(item.Value[1]);
                            if (Pinging.Status.ToString() == "Success")
                            {

                                if (LogHelper != "ОК")
                                {
                                    LogHelper = "ОК";
                                    Ping_AGV_OK();

                                }
                            }
                            else
                            {
                                if (LogHelper != "NОК")
                                {
                                    Pinging = item.Key.Send(item.Value[1]);

                                    LogHelper = "NОК";
                                    statuses = item.Value[0] + " = " + Pinging.Status.ToString();

                                    Ping_AGV_NOK();
                                    PingLog.Error(item.Value[0] + " = " + "Connection failed");
                                }
                            }
                        }
                        catch (PingException ex)
                        {
                            if (LogHelper != "NОК")
                            {
                                LogHelper = "NОК";
                                statuses = item.Value[0] + " = " + "Connection failed";
                                Ping_AGV_NOK();
                                PingLog.Error(item.Value[0] + " = " + "Connection failed");
                            }
                        }
                    }
                }
            }

            catch (Exception ex)
            {
                PingLog.Error(ex +" 110 ");

            }

            void Ping_AGV_OK()
            {

                //Красим Индикатор в зеленый     
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    MainWindow.mainWindowClient.Ell_Status_Watcher.Fill = Brushes.Green;

                }));

                //АГВ Онлайн.
                AGVIsOnline = true;

                if (AGV_Ping_WatchDog.IsRunning)
                {
                    AGV_Ping_WatchDog.Stop();
                    PingLog.Information("\nОнлайн" + DateTime.Now.ToString("dd.mm.yyyy ; HH:mm:ss:ff") + " время Офлайн:" + AGV_Ping_WatchDog.ElapsedMilliseconds);
                    if (AGV_Ping_WatchDog.ElapsedMilliseconds > 1000)
                    {
                        MainWindow.AddToTB_TB_Ping_Log($"\nАГВ не было в сети {AGV_Ping_WatchDog.ElapsedMilliseconds} миллисекунд");
                    }
                }
                else
                {
                    LogHelper = "ОК";
                    PingLog.Information("Микротик ок");
                }

            }


            //Обработка отсутствия АГВ В сети
            void Ping_AGV_NOK()
            {

                //Красим Индикатор в красный
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    MainWindow.mainWindowClient.Ell_Status_Watcher.Fill = Brushes.Red;
                }));

                //АГВ Офлайн.
                AGVIsOnline = false;

                AGV_Ping_WatchDog.Restart();
                PingLog.Error(DateTime.Now.ToString("dd.mm.yyyy ; HH:mm:ss:ff") + " _ " + "АГВ_NOK Запущен секундомер отсутствия АГВ в сети" + " " + statuses);

            }


            //Обработка если АГВ увиделся в сетке

        }
    }
}
