using AGV_Mark6.Model;
using System.Collections.Generic;
using System.Linq;
using AGV_Mark6.MainTaskManager_Methods;
using Serilog;
using AGV_Mark6.SeriLog;
using AGV_Mark6.Modbus_Server_For_Buttons;

namespace AGV_Mark6.Catchers
{
    internal class CatcherWatcher
    {
        //Класс для отслеживания статуса ловителей и определения в какой ловитель АГВ совершать маршрут.

        Dictionary<string, int> CatchersStates = new Dictionary<string, int>();

        //логгер Ловителей c АГВ
        public static Serilog.Core.Logger CatcherLogger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.File(new MyOwnCompactJsonFormatter(), "Catchers_Log.txt").CreateLogger();
        public static bool SecondPartOfCycle = false;

        static string PreFirstRoute = "";
        static string PreSecondRoute = "";

        public static void CatchersWatchdogCheck(string CurrentTaskRoute, out string FirstRouteReady, out string SecondRouteReady)
        {

            FirstRouteReady = "";
            SecondRouteReady = "";

            //Склад -> Линия 1
            string[] PointToPoint = CurrentTaskRoute.Split("->");
            string firstPoint = PointToPoint[0].Trim();
            string secondPoint = PointToPoint[1].Trim();

            //Целевой ловитель(где забрать)
            string? TargetCatcherTake = "";
            //Целевой ловитель(Куда привезти)
            string? TargetCatcherGive = "";
            

            List<catchersCurrentStates> CatchersForFirstPoint = new List<catchersCurrentStates>();
            List<catchersCurrentStates> CatchersForSecondPoint = new List<catchersCurrentStates>();

            CatchersForFirstPoint = MainTaskManager.db.CatchersCurrentStates.Where(e => e.CatchersHome == firstPoint).ToList();
            CatchersForSecondPoint = MainTaskManager.db.CatchersCurrentStates.Where(e => e.CatchersHome == secondPoint && e.State==0).ToList();

            List<string[]> CatchersStatesForfirstPoint  = new List<string[]>();
            List<string[]> CatchersStatesForsecondPoint = new List<string[]>();

            for (int i = 0; i <= CatcherStateMemory.Count-1; i++)
            {
                if (CatcherStateMemory[i][1] == firstPoint)
                {

                    CatchersStatesForfirstPoint.Add(CatcherStateMemory[i]);

                }
            }

            if (CatchersStatesForfirstPoint.Count != 0)
            {
                TargetCatcherTake = CatchersStatesForfirstPoint[0][0];
            }

            if (CatchersForSecondPoint.Count != 0)
            {
                TargetCatcherGive = CatchersForSecondPoint[0].CatcherName;
            }
            

            //Проверяем что мы можем забрать и привезти 
            if(TargetCatcherTake=="" || TargetCatcherGive == "")
            {

                MainWindow.AddToTB_TB_Ping_Log("Относительно состояний ловителей, выполнить маршрут невозможно");
                CatcherLogger.Information($"TargetCatcherTake={TargetCatcherTake}, TargetCatcherGive={TargetCatcherGive} CatchersForFirstPoint[0]={CatchersForFirstPoint[0]}, CatchersForFirstPoint[1]={CatchersForFirstPoint[1]}");
                return;

            }

            //получаем два маршрута
            foreach(string route in RoutesEditClass.RoutesList)
            {

                string[] PtPRoute=route.Split("->");

                //Проверка и получение первого маршрута
                if (PtPRoute[0] == firstPoint)
                {
                    if (PtPRoute[1].Contains(TargetCatcherTake))
                    {
                        if (PtPRoute[1].Contains("Взять"))
                        {
                            if (FirstRouteReady == "")
                            {
                                FirstRouteReady = route;
                                if (SecondRouteReady != "")
                                {
                                    break;
                                }
                                continue;
                            }
                            continue;
                        }
                        continue;
                    }
                    continue;
                }

                //Проверка и получение второго маршрута
                if (PtPRoute[0] == secondPoint)
                {
                    if (PtPRoute[1].Contains(TargetCatcherGive))
                    {
                        if (PtPRoute[1].Contains("Дать"))
                        {
                            if (SecondRouteReady == "")
                            {
                                SecondRouteReady = route;
                                if (FirstRouteReady != "")
                                {
                                    break;
                                }
                                continue;
                            }
                            continue;
                        }
                        continue;
                    }
                    continue;
                }

            }

            //Проверка получили ли мы все маршруты
            if (FirstRouteReady=="" || SecondRouteReady == "")
            {

                MainWindow.AddToTB_TB_Ping_Log("Маршруты для данного задания не были найдены");
                FirstRouteReady = "";
                SecondRouteReady = "";
                return;

            }

            if (MainTaskManager.AGVQueue.Contains(FirstRouteReady) || MainTaskManager.AGVQueue.Contains(SecondRouteReady))
            {

                MainWindow.AddToTB_TB_Ping_Log("Команда уже присутствует в очереди");
                FirstRouteReady = "";
                SecondRouteReady = "";
                return;

            }

            if (MainTaskManager.AGV_Quest == FirstRouteReady || MainTaskManager.AGV_Quest == SecondRouteReady)
            {

                MainWindow.AddToTB_TB_Ping_Log("Команда выполняется");
                FirstRouteReady = "";
                SecondRouteReady = "";
                return;

            }
            if (FirstRouteReady == "" | SecondRouteReady == "")
            {
                return;
            }

            CatcherStateChange(FirstRouteReady);
            CatcherStateChange(SecondRouteReady);




            MainTaskManager.AGVQueue.Enqueue(FirstRouteReady);
            MainTaskManager.AGVQueue.Enqueue(SecondRouteReady);

           

            //ограничиваем вход, чтобы не было зацикливания.
            if (!SecondPartOfCycle)
            {
                foreach (var item1 in MainTaskManager.db.RegistersForButtons.Where(e => e.CommandForRegister.Contains("->" + firstPoint)))
                {
                    SecondPartOfCycle = true;

                    PreFirstRoute = FirstRouteReady;
                    PreSecondRoute= SecondRouteReady;
                    Class_ModbusServer_for_Buttons.registerWatchDog(item1.RegisterNumber, 1);



                }
            }

            SecondPartOfCycle = false;
            reservCatchersState.Clear();
        }

        public static List<string[]> CatcherStateMemory = new List<string[]>();

        //временное хранилище состояния ловителей, для того чтобы алгоритм проработал путь на склад и обратно. Иначе он может отработать путь только на склад и не вернуться. в работе
        private static List<catchersRoutes> reservCatchersState=new List<catchersRoutes>();
        //Изменяем состояние ловителя
        public static void CatcherStateChange(string CurrentTaskRoute)
        {

            List<catchersRoutes> CatchersList = new List<catchersRoutes>();
            
            CatchersList = MainTaskManager.db.CatchersRoutes.Where(e => e.CatcherRoute == CurrentTaskRoute).ToList();
            if (reservCatchersState.Count == 0)
            {
                reservCatchersState = CatchersList;
            }

            foreach (var catcherRoute in CatchersList)
            {
                //Получаем список заполненных ловителей для данного Base
                int CountOfFullCatchers = MainTaskManager.db.CatchersCurrentStates.Where(e => e.CatchersHome == catcherRoute.CatchersHome & e.State>0).Count();

                //Получаем Состояние Ловителя
                int? StateChange = MainTaskManager.db.CatchersRoutes.FirstOrDefault(e => e.CatcherName == catcherRoute.CatcherName & e.CatcherRoute == CurrentTaskRoute).StateChange;

                //Для того чтобы видеть какую телегу привезла АГВ последней
                if (StateChange == 1)
                {

                    CatcherStateMemory.Add( new string[] { $"{catcherRoute.CatcherName}", $"{catcherRoute.CatchersHome}" } );

                    MainTaskManager.db.CatchersCurrentStates.FirstOrDefault(e => e.CatcherName == catcherRoute.CatcherName & e.CatchersHome == catcherRoute.CatchersHome).CatcherMemoryState = CountOfFullCatchers+1;

                    MainWindow.DG_CatchersStatusesFill();
                    MainTaskManager.db.CatchersCurrentStates.FirstOrDefault(e => e.CatcherName == catcherRoute.CatcherName & e.CatchersHome == catcherRoute.CatchersHome).State = StateChange;

                    MainTaskManager.db.SaveChanges();
                    MainWindow.catcherMemoryOrder();

                }
                if (StateChange == 0)
                {

                    string[] item = CatcherStateMemory.FirstOrDefault(e => e[0] == catcherRoute.CatcherName & e[1] == catcherRoute.CatchersHome);
                    CatcherStateMemory.Remove( item );
                   

                    List<catchersCurrentStates> AllCatchersMemoryStates = MainTaskManager.db.CatchersCurrentStates.Where(e => e.CatchersHome == catcherRoute.CatchersHome & e.CatcherMemoryState>0).ToList();
                    
                    MainTaskManager.db.CatchersCurrentStates.FirstOrDefault(e => e.CatcherName == catcherRoute.CatcherName & e.CatchersHome == catcherRoute.CatchersHome).CatcherMemoryState = 0;

                    foreach (var catcherState in AllCatchersMemoryStates)
                    {
                        if (catcherState.CatcherName != catcherRoute.CatcherName)
                        {

                            catcherState.CatcherMemoryState = catcherState.CatcherMemoryState-1;

                        }
                    }
                    MainWindow.DG_CatchersStatusesFill();

                    MainTaskManager.db.CatchersCurrentStates.FirstOrDefault(e => e.CatcherName == catcherRoute.CatcherName & e.CatchersHome == catcherRoute.CatchersHome).State = StateChange;

                    MainTaskManager.db.SaveChanges();

                    MainWindow.catcherMemoryOrder();

                }

            }
        }



        public static bool CatchersCurrentStateIsEmpty(string CurrentTaskRoute)
        {

            string? CatcherName = MainTaskManager.db.CatchersRoutes.FirstOrDefault(e => e.CatcherRoute == CurrentTaskRoute & e.Direction==1).CatcherName;

            if (MainTaskManager.db.CatchersCurrentStates.FirstOrDefault(e=>e.CatcherName == CatcherName).State == 0)
            {

                return true;
                
            }
            else
            {

                return false;
                
            }
        }
    }
}
