using AGV_Mark6.Web_Connections;
using System;
using System.Collections.Generic;
using System.Linq;
using AGV_Mark6.Catchers;
using AGV_Mark6.MainTaskManager_Methods;
using System.Threading;
using System.Diagnostics;

namespace AGV_Mark6.Modbus_Server_For_Buttons
{

    class Class_ModbusServer_for_Buttons
    {
        //В этом классе мы работаем с кнопками.
        //Через modbus получаем задание через tcp передаем данные по текущей очереди и заданию 

        //Сервер Модбас
        static EasyModbus.ModbusServer Server_For_Buttons;
        //Словарь для соответствия регистров с коммандами(маршрутами) от кнопок
        public static Dictionary<int, string?> RegistersAndRoutes=new Dictionary<int, string?>();
        public static void ButtonServer_Start()
        {
            try
            {
                Server_For_Buttons = new EasyModbus.ModbusServer();

                Server_For_Buttons.Listen();

                TimeBetweenButtonPushes.Start();

                Server_For_Buttons.HoldingRegistersChanged += registerWatchDog;

                //Заполнение Маршрутов с соответствующими им регистрами для работы кнопок
                FillRegistersForButtons();
            }
            catch (Exception ex)
            {

                
            }
           

        }
        public static void FillRegistersForButtons()
        {
            using (Model.AGV_Storage_Context db = new Model.AGV_Storage_Context())
            {
                //перебор всех строк таблицы RegistersForButtons
                List<Model.registersForButtons> TableRegistersForButtons = new List<Model.registersForButtons>();
                //Настраиваем соответствие регистров и маршрутов
                TableRegistersForButtons = db.RegistersForButtons.ToList();

                for (int i = 1; i <= TableRegistersForButtons.Count; i++)
                {
                    if (db.RegistersForButtons.Where(e => e.RegisterNumber == i) != null)
                    {
                        try
                        {
                            Server_For_Buttons.holdingRegisters[i] = 0;
                            RegistersAndRoutes.Add(i, TableRegistersForButtons.FirstOrDefault(e => e.RegisterNumber == i).CommandForRegister);
                        }
                        catch
                        {

                        }

                    }
                }

            }


        }

        static object locker=new object();
        public static bool AGV_Button_Clicked = false;

        static Stopwatch TimeBetweenButtonPushes=new Stopwatch();

        //Обработка комманд от кнопок
        public static void registerWatchDog(int register, int NumberOfRegisters)
        {

            lock (locker)
            {
                try
                {
                    if (register == 64322)
                    {

                        MainTaskManager.WaitingForButtonPermission = false;
                        AGV_Connect.ProgramIsRunning = true;

                        return;

                    }

                    if (!CatcherWatcher.SecondPartOfCycle)
                    {
                        if (TimeBetweenButtonPushes.ElapsedMilliseconds / 1000 < 10) { return; }
                        TimeBetweenButtonPushes.Restart();
                    }

                    string firstRoute = "";
                    string secondRoute = "";

                    if (MainTaskManager.AGVOnCharge) { MainWindow.AddToTB_TB_Ping_Log("АГВ На зарядке, полученная команда не учтена"); return; }

                    if (RoutesEditClass.RoutesMainForButtonsList.Contains(RegistersAndRoutes[register]))
                    {

                        //Убрать
                        CatcherWatcher.CatchersWatchdogCheck(RegistersAndRoutes[register], out firstRoute, out secondRoute);
                        

                        MainTaskManager.ShowCurrentTaskAndCurrentQueueInMainWindow();
                        Control_AGV.SaveCurrentQueue();
                        Server_For_Buttons.holdingRegisters[register] = 0;

                        //Если Агв ждет заданий от кнопок и режим работы только от кнопок то:
                        if (MainWindow.Mode_ButtonsOnly & AGV_Connect.ProgramIsRunning == false & MainTaskManager.AGV_Quest=="")
                        {

                            AGV_Button_Clicked = true;

                            AGV_Connect.ProgramIsRunning = true;
                            //MainTaskManager.StartEventMethod();

                        }
                       
                        return;

                    }

                    MainWindow.AddToTB_TB_Ping_Log("Была получена неизвестная команда, требуется поправить маршрут");

                }
                catch (Exception ex)
                {
                    MainWindow.AddToTB_TB_Ping_Log("Оператор баловник. Его нужно уволить. Теперь кнопку переделывать ");
                }
            }

        }
    }
}
