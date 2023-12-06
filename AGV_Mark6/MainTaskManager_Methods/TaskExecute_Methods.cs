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
using AGV_Mark6;

namespace AGV_Mark6.MainTaskManager_Methods
{
    public partial class MainTaskManager
    {
        //В этом классе Методы для извлечения задач и работы с очередью

        //Извлечение пути домой
        public static void GoToHome()
        {

            if (LastBase == "") { return; }
            Regex GoToHomeRoute;
            if (LoadStatus == "Загружена" || LoadStatus == "С тележкой")
            {
                GoToHomeRoute = new Regex($"^{LastBase}.*CrossPoint.*");
            }
            else
            {
                GoToHomeRoute = new Regex($"^{LastBase}.*home.*");
            }

            if (LastBase == "home" || LastBase == "CrossPoint") { return; }

            foreach (var item in RoutesEditClass.RoutesForHomesList)
            {
                if (GoToHomeRoute.IsMatch(item))
                {

                    AGVQueue.Enqueue(item);
                    break;

                }
            }
        }

        //получаем Задание
        private static void GettingTask()
        {

            //Прочитаем следующую задачу
            string ReadNextTask = AGVQueue.ElementAt(0);

            string[] NextPoint = ReadNextTask.Split("->");

            MakeATransitionBetweenBases(LastBase, NextPoint[0].Trim());

            //Если не нашли маршрута то останавливаем программу
            if (ProgramIsRunning == false)
            {
                StopEventMethod();


                return;
            }

            //Если следующая задача в другом home  то ждем переезда в нужный нам home
            if (AGV_Quest != "")
            {
                ExtractRouteForHomesFromCurrentTask();
                if (MainTaskManager.AGV_Task_Route.Count == 0)
                {

                    MainWindow.AddToTB_TB_Ping_Log("Критическая ошибка при чтении шагов маршрута. Проверьте Маршрут");
                    return;

                }
                bool TaskFinished = false;
                ExecuteNextTaskStep(out TaskFinished);
                if (TaskFinished == false)
                {

                    return;

                }
                MainWindow.AGVRegistersShow();
                //выводим список текущих задач и полученную задачу на главное окно\
                ShowCurrentTaskAndCurrentQueueInMainWindow();


                return;

            }

            //Получаем следующую задачу
            AGV_Quest = DequeueTask();
            MainWindow.SavedAGVStatuses.OnAppStart_AGV_Quest = AGV_Quest;
            MainWindow.SavedAGVStatuses.Save();
            MainWindow.AddToTB_TB_Ping_Log($"АГВ получило задачу: {AGV_Quest}");
            AGVConnectLog.Information($"АГВ получило задачу: {AGV_Quest}");
            NextPoint = AGV_Quest.Split("->");


            if (LastBase != NextPoint[0])
            {
                MainWindow.AddToTB_TB_Ping_Log($"Последий Home {LastBase} Едем в {NextPoint[0]}");
            }

            //выводим список текущих задач и полученную задачу на главное окно
            ShowCurrentTaskAndCurrentQueueInMainWindow();

            //извлекаем маршрут по заданию
            ExtractRouteFromCurrentTask();
            //Проверка что маршрут не пустой
            if (MainTaskManager.AGV_Task_Route.Count == 0)
            {

                MainWindow.AddToTB_TB_Ping_Log("Критическая ошибка при чтении шагов маршрута. Проверьте Маршрут");


                return;

            }

            MainWindow.AddToTB_TB_Ping_Log($"Начало в точке P{int.Parse(AGV_Task_Route.FirstOrDefault(e => e.id == 0).Program)}S{int.Parse(AGV_Task_Route.FirstOrDefault(e => e.id == 0).Step)}");
            //Переходим к началу маршрута
            TransitionMethod(int.Parse(AGV_Task_Route.FirstOrDefault(e => e.id == 0).Program), int.Parse(AGV_Task_Route.FirstOrDefault(e => e.id == 0).Step));
            CurrentTaskStepNumber++;
            CurrentStepsBackUpper();
            if (MainWindow.Emulator)
            {
                //TODO:Эмулятор
                AGV_Connect.AGV_Client.WriteMultipleRegisters(90, new int[] { 1 });
            }
        }

        //Выполнение следующего шага по текущему маршруту(заданию)
        private static void ExecuteNextTaskStep(out bool TaskFinished)
        {
            TaskFinished = false;
            int CurrentTaskStep = 0;
            int CurrentTaskProgram = 0;
            if (AGV_Task_Route.Count < 1) { return; }
            try
            {
                 CurrentTaskStep = int.Parse(AGV_Task_Route.FirstOrDefault(e => e.id == CurrentTaskStepNumber).Step);
                 CurrentTaskProgram = int.Parse(AGV_Task_Route.FirstOrDefault(e => e.id == CurrentTaskStepNumber).Program);
            }
            catch (Exception)
            {

                FinishTask(ref TaskFinished);
                return;

            }
            


            if (AGV_Task_Route.FirstOrDefault(e => e.id == CurrentTaskStepNumber).LastOne != true)
            {
                //если при переходе разрыв между шагами а не программой
                if (CurrentTaskStepNumber == 0)
                {
                    if (CurrentTaskStep == CurrentStep & CurrentTaskProgram == CurrentProgram)
                    {

                        CurrentTaskStepNumber++;
                        CurrentStepsBackUpper();
                        return;

                    }

                    //Если первый шаг программы не по порядку идет
                    if (CurrentTaskStep != CurrentStep + 1)
                    {

                        AwaitingTransition = true;
                        //Отображаем данные следующего шага по заданию
                        MainWindow.AddToTB_TB_Ping_Log($"Двигаемся по маршруту задания (перескакиваем на неочередной шаг): \nP{CurrentTaskProgram};S{CurrentTaskStep}" + "\n");
                        //выполняем переход

                        TransitionMethod(int.Parse(AGV_Task_Route.FirstOrDefault(e => e.id == 0).Program), int.Parse(AGV_Task_Route.FirstOrDefault(e => e.id == 0).Step));

                        CurrentTaskStepNumber++;
                        CurrentStepsBackUpper();
                        return;

                    }
                    //Если программа на первом шаге меняется
                    if (CurrentTaskProgram != CurrentProgram)
                    {
                        AwaitingTransition = true;
                        //Отображаем данные следующего шага по заданию
                        MainWindow.AddToTB_TB_Ping_Log($"Двигаемся по маршруту задания (перескакиваем на программу): \nP{CurrentTaskProgram};S{CurrentTaskStep}" + "\n");

                        //выполняем переход
                        TransitionMethod(CurrentTaskProgram, CurrentTaskStep);

                        CurrentTaskStepNumber++;
                        CurrentStepsBackUpper();
                        return;

                    }

                }

                //Если программа на следующем шаге меняется
                if (CurrentTaskProgram != CurrentProgram)
                {
                    AwaitingTransition = true;
                    //Отображаем данные следующего шага по заданию
                    MainWindow.AddToTB_TB_Ping_Log($"Двигаемся по маршруту задания (перескакиваем на программу): \nP{CurrentTaskProgram};S{CurrentTaskStep}" + "\n");

                    //выполняем переход
                    TransitionMethod(CurrentTaskProgram, CurrentTaskStep);

                    CurrentTaskStepNumber++;
                    CurrentStepsBackUpper();
                    return;

                }

                //Если следующий шаг не по порядку идет
                if (CurrentStep + 1 != CurrentTaskStep)
                {

                    AwaitingTransition = true;
                    //Отображаем данные следующего шага по заданию
                    MainWindow.AddToTB_TB_Ping_Log($"Двигаемся по маршруту задания (перескакиваем на неочередной шаг): \nP{CurrentTaskProgram};S{CurrentTaskStep}" + "\n");
                    //выполняем переход
                    TransitionMethod(CurrentTaskProgram, CurrentTaskStep);

                    CurrentTaskStepNumber++;
                    CurrentStepsBackUpper();
                    return;

                }

                MainWindow.AddToTB_TB_Ping_Log($"Двигаемся по маршруту задания (двигаемся ровно по шагам): \nP{CurrentTaskProgram};S{CurrentTaskStep}" + "\n");

                CurrentTaskStepNumber++;
                CurrentStepsBackUpper();
                return;
            }
            else
            {

                if (AGV_Task_Route.Count == 1)
                {
                    //Переходим к точке маршрута и завершаем задачу, так как маршрут состоит из одной точки
                    MainWindow.AddToTB_TB_Ping_Log($"Маршрут состоит из одной точки: \nP{AGV_Task_Route.FirstOrDefault(e => e.id == 0).Program};S{AGV_Task_Route.FirstOrDefault(e => e.id == 0).Step}" + "\n");

                    TransitionMethod(int.Parse(AGV_Task_Route.FirstOrDefault(e => e.id == 0).Program), int.Parse(AGV_Task_Route.FirstOrDefault(e => e.id == 0).Step));

                    FinishTask(ref TaskFinished);
                    return;
                }

                //Если программа на последнем шаге другая
                if (CurrentTaskProgram != CurrentProgram)
                {
                    AwaitingTransition = true;
                    //Отображаем данные следующего шага по заданию
                    MainWindow.AddToTB_TB_Ping_Log($"Последняя точка маршрута :(перескакиваем на программу): \nP{CurrentTaskProgram};S{CurrentTaskStep}" + "\n");

                    //выполняем переход
                    TransitionMethod(CurrentTaskProgram, CurrentTaskStep);

                    FinishTask(ref TaskFinished);
                    return;

                }

                if (CurrentTaskStep != CurrentStep)
                {

                    AwaitingTransition = true;
                    //Отображаем данные следующего шага по заданию
                    MainWindow.AddToTB_TB_Ping_Log($"Последняя точка маршрута : (перескакиваем на неочередной шаг): \nP{CurrentTaskProgram};S{CurrentTaskStep}" + "\n");
                    //выполняем переход
                    TransitionMethod(CurrentTaskProgram, CurrentTaskStep);

                    FinishTask(ref TaskFinished);
                    return;

                }


                MainWindow.AddToTB_TB_Ping_Log($"Последняя точка маршрута : \nP{CurrentTaskProgram};S{CurrentTaskStep}" + "\n");

                FinishTask(ref TaskFinished);

            }
        }

        //Едем на Зарядку
        public static void GoToCharge()
        {
            if (LastBase == "") { return; }

            Regex GoToChargeRoute = new Regex($"^Зарядка.*home.*");


            foreach (var item in RoutesEditClass.RoutesForHomesList)
            {
                if (GoToChargeRoute.IsMatch(item))
                {

                    if (AGVQueue.Contains(item)) { MainWindow.AddToTB_TB_Ping_Log("Команда уже присутствует в очереди"); return; }

                    List<string> TempQueue = new List<string>();
                    TempQueue = AGVQueue.ToList();

                    AGVQueue.Clear();

                    AGVQueue.Enqueue(item);

                    foreach (string str in TempQueue)
                    {
                        AGVQueue.Enqueue(str);
                    }
                    ShowCurrentTaskAndCurrentQueueInMainWindow();

                    break;

                }
            }
            AGVOnCharge = true;
        }

        //метод для получения текущего элемента очереди,и очистки файла !CurrentQueue.txt от него 
        public static string DequeueTask()
        {
            //во первых считываем все строки из файла и добавляем их в список FileContents
            List<string> FileContents = new List<string>();
            string CurrentTask = AGVQueue.Dequeue();
            StreamReader f = new StreamReader("!CurrentQueue.txt");

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
                if (item.Contains(CurrentTask))
                {
                    FileContents.Remove(item);
                    break;
                }
            }

            //Заполняем файл получившимся списком 
            string[] FileConverted = FileContents.ToArray();
            File.WriteAllText("!CurrentQueue.txt", string.Empty);
            File.WriteAllLines("!CurrentQueue.txt", FileConverted);
            //возвращаем текущий элемент очереди
            return CurrentTask;

        }

        //После отмены задачи АГВ, не известно где она находится. Нужно вручную отправить в место назначения.
        public static bool Task_Canceled = false;

        //очищаем текущую задачу АГВ
        public static void ClearCurrentTask()
        {

            MainWindow.AddToTB_TB_Ping_Log($"Задание {AGV_Quest} отменено Принудительно. АГВ нужно отправить в home в ручную");
            ProgramIsRunning = false;

            Task_Canceled = true;
            MainWindow.SavedAGVStatuses.OnAppStart_TaskCanceled = true;
            MainWindow.SavedAGVStatuses.Save();


            Control_AGV.AGV_ChargedUp = 0;
            AGV_Quest = "";
            MainWindow.SavedAGVStatuses.OnAppStart_AGV_Quest = "";
            MainWindow.SavedAGVStatuses.Save();

            CurrentTaskStepNumber = 0;
            AGV_Task_Route.Clear();

        }

        private static bool KeyToHome = false;

        private static additionCoordinates Home;
        //для проверки того находимся мы сейчас в Home или нет
        public static void ReadCurrentHome(Prog Current_Status_AGV)
        {
            try
            {

                Home = new additionCoordinates();
                Home = db.AdditionCoordinates.Where(e => e.PointName == Current_Status_AGV.AdditionCommand).FirstOrDefault();
                if (Home != null)
                {
                    if (Home.PointName != "")
                    {
                        LastBase = Home.PointName;
                        MainWindow.SavedAGVStatuses.OnAppStart_LastBase = Home.PointName;
                        MainWindow.SavedAGVStatuses.Save();
                        KeyToHome = true;

                    }

                }
                else
                {
                    KeyToHome = false;
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
                db.ChangeTracker.Clear();
            }
            catch (Exception)
            {


            }

        }

        //Метод для отражения в txt шагов текущего задания 
        public static List<string> Queue_CurrentSteps = new List<string>();

        //Счетчик шагов
        private static int EnterCounter = 1;
        public static void CurrentStepsBackUpper()
        {
            List<RouteEditorStorage>? NewStorageRoute = new List<RouteEditorStorage>();

            NewStorageRoute = JsonConvert.DeserializeObject<List<RouteEditorStorage>>(MainWindow.SavedAGVStatuses.OnAppStart_CurrentTaskSteps);

            //ошибка что коллекция модифицируется
            int i = 0;
            if (NewStorageRoute == null) { return; }
            while (i < EnterCounter)
            {
                if (NewStorageRoute.Count > 0)
                {
                    NewStorageRoute.RemoveAt(0);
                    i++;
                }
                else
                {
                    break;
                }

            }
            int IdOffset = i;
            foreach (RouteEditorStorage step in NewStorageRoute)
            {
                NewStorageRoute.FirstOrDefault(e => e.id == step.id).id -= i;

                // NewStorageRoute.FirstOrDefault(e=>e.id==step.id).id=step.id-1;
            }



            //AGV_Task_Route = NewStorageRoute;
            string txt = JsonConvert.SerializeObject(NewStorageRoute);

            MainWindow.SavedAGVStatuses.OnAppStart_CurrentTaskSteps = txt;
            MainWindow.SavedAGVStatuses.Save();

            return;

        }

        //завершение задания и обнуление привязанных параметров
        private static void FinishTask(ref bool TaskFinished)
        {
            Thread.Sleep(50);

            MainWindow.AddToTB_TB_Ping_Log($"Задача {AGV_Quest} выполнена\n");
            AGVConnectLog.Information($"Задача {AGV_Quest} выполнена\n");
            //Меняем состояние регистров после выполнения задачи.

            //Catchers.CatcherWatcher.CatcherStateChange(AGV_Quest);
            AGV_Quest = "";
            MainWindow.SavedAGVStatuses.OnAppStart_AGV_Quest = "";
            MainWindow.SavedAGVStatuses.Save();

            AGV_Task_Route.Clear();

            Control_AGV.AGV_ChargedUp = 0;
            CurrentTaskStepNumber = 0;
            //обновление окна с заданиями
            CurrentStepsBackUpper();
            ShowCurrentTaskAndCurrentQueueInMainWindow();

            EnterCounter = 1;
            ProgrammsLog.Information($"Задача выполнена");
            TaskFinished = true;
        }

        //Метод для отображения на главном окне текущей задачи и текущей очереди
        public static void ShowCurrentTaskAndCurrentQueueInMainWindow()
        {
            try
            {
                //Выводим задачу на главный экран
                MainWindow.showCurrentTaskInLabel(AGV_Quest);

                MainWindow.ShowCurrentQueue(AGVQueue.ToList());
                if (MainWindow.RouteSelectorOpened == true)
                {
                    RoutesEditClass.ShowCurrentQueue(AGVQueue.ToList());
                }
            }
            catch (Exception ex)
            {
                ProgrammsLog.Information($"ошибка при отображении очереди в ListBox" + ex.Message);
            }
        }

        //Метод для извлечения точек маршрута для передвижения между Homes
        public static void ExtractRouteForHomesFromCurrentTask()
        {
            //Переменная для сравнения того что из файла получили с тем что хранится в AGV_Quest
            string[] splittedString = new string[3];

            //Регулярное выражение для вычленения нужного маршрута из файла !Route.txt
            Regex RouteName = new Regex("^*:");
            //Находим строку в файле с нужным маршрутом
            StreamReader f = new StreamReader("!RoutesBetweenBases.txt");
            while (!f.EndOfStream)
            {
                string stringFromFile = f.ReadLine();
                if (RouteName.IsMatch(stringFromFile))
                {
                    splittedString = stringFromFile.Split(":");
                    if (splittedString[0] == AGV_Quest)
                    {
                        break;

                    }
                }
            }
            f.Dispose();

            //Получаем Список всех координат маршрута
            string[] RouteCoordinates = splittedString[1].Split(";");

            int i = 0;
            int step = 0;
            int program = 0;
            try
            {
                foreach (var str in RouteCoordinates)
                {
                    if (str == "")
                    {
                        continue;
                    }
                    RouteConverterFromTxtFile(str, out program, out step);
                    AGV_Task_Route.Add(new RouteEditorStorage() { id = i, Program = program.ToString(), Step = step.ToString(), LastOne = false });
                    i++;

                }
                if (AGV_Task_Route.Count == 0)
                {

                    return;
                }
                AGV_Task_Route.Where(e => e.id == i - 1).FirstOrDefault().LastOne = true;
                MainWindow.SavedAGVStatuses.OnAppStart_CurrentTaskSteps = JsonConvert.SerializeObject(AGV_Task_Route);
                MainWindow.SavedAGVStatuses.Save();


            }
            catch (Exception ex)
            {


            }

        }

        //Метод для извлечения точек маршрута по заданию
        public static void ExtractRouteFromCurrentTask()
        {
            //Переменная для сравнения того что из файла получили с тем что хранится в AGV_Quest
            string[] splittedString = new string[3];

            //Регулярное выражение для вычленения нужного маршрута из файла !Route.txt
            Regex RouteName = new Regex("^*:");
            //Находим строку в файле с нужным маршрутом
            StreamReader f = new StreamReader("!Routes.txt");
            while (!f.EndOfStream)
            {
                string stringFromFile = f.ReadLine();
                if (RouteName.IsMatch(stringFromFile))
                {
                    splittedString = stringFromFile.Split(":");
                    if (splittedString[0] == AGV_Quest)
                    {
                        break;

                    }
                }
            }
            f.Dispose();

            //Получаем Список всех координат маршрута
            string[] RouteCoordinates = splittedString[1].Split(";");

            int i = 0;
            int step = 0;
            int program = 0;
            try
            {
                foreach (var str in RouteCoordinates)
                {
                    if (str == "")
                    {
                        continue;
                    }
                    RouteConverterFromTxtFile(str, out program, out step);
                    AGV_Task_Route.Add(new RouteEditorStorage() { id = i, Program = program.ToString(), Step = step.ToString(), LastOne = false });
                    i++;
                }
                if (AGV_Task_Route.Count == 0)
                {

                    return;

                }
                AGV_Task_Route.Where(e => e.id == i - 1).FirstOrDefault().LastOne = true;

                MainWindow.SavedAGVStatuses.OnAppStart_CurrentTaskSteps = JsonConvert.SerializeObject(AGV_Task_Route);
                MainWindow.SavedAGVStatuses.Save();

            }
            catch (Exception ex)
            {


            }

        }

        //метод для получения координат из первой и конечной точки маршрута из txt файла
        public static void RouteConverterFromTxtFile(string RouteCoordinatesNotConverted, out int routeprogram, out int routestep)
        {
            string programstring = "";
            string stepstring = "";
            routeprogram = 0;
            routestep = 0;
            int i = 1;
            while (RouteCoordinatesNotConverted[i] != 's')
            {
                programstring += RouteCoordinatesNotConverted[i];
                i++;
            }
            routeprogram = int.Parse(programstring);
            while (i < RouteCoordinatesNotConverted.Length)
            {
                if (RouteCoordinatesNotConverted[i] != 's')
                {
                    stepstring += RouteCoordinatesNotConverted[i];
                    i++;
                    continue;
                }
                i++;
            }
            routestep = int.Parse(stepstring);
        }
    }
}
