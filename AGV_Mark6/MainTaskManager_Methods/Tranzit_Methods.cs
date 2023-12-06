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
        //Сюда я вывел методы для непосредственного движения АГВ. 
        //Работа с Задачами и очередью в Другой части класса

        //Эти переменные нужны для того чтобы в окно мониторинга выводить корректные шаги.
        //Так как при передвижении, агв сначала заканчивает предыдущий шаг, переходит на следующий и в этот момент получает тот который нам необходим.
        //то есть программа должна завершить свой шаг, а понять то что она его завершила мы можем только по тому что она перешла на следующий. Вот такая логика
        public static int RightStepToPass = 0;
        public static int RightProgramToPass = 0;     


        //Отдельный файл класса где храняться дополнительные методы.

        //Метод для переходов между базами
        private static void MakeATransitionBetweenBases(string currentHome, string nextTaskHome)
        {

            if (currentHome.ToLower() == nextTaskHome.ToLower()) { return; }

            //Переменая хранящая в какой home ехать АГВ(если загружена то в Crosspoint)
            string HomeForAGV = "";
            if (LoadStatus == "Загружена" || LoadStatus == "С тележкой")
            {
                HomeForAGV = "crosspoint";
            }
            else
            {
                HomeForAGV = "home";
            }

            if (currentHome.ToLower() == HomeForAGV)
            {
                Regex NextPointRoute = new Regex($"^{currentHome}->.*{nextTaskHome}$");
                foreach (string item in RoutesEditClass.RoutesForHomesList)
                {
                    if (NextPointRoute.IsMatch(item))
                    {

                        MainWindow.AddToTB_TB_Ping_Log($"Для следующей задачи выполняется переход от {currentHome} в {nextTaskHome}");

                        AGV_Quest = item;
                        MainWindow.SavedAGVStatuses.OnAppStart_AGV_Quest = AGV_Quest;
                        MainWindow.SavedAGVStatuses.Save();
                        break;

                    }
                }
                if (AGV_Quest == "")
                {
                    ProgramIsRunning = false;
                    MainWindow.AddToTB_TB_Ping_Log($"Маршрут до {nextTaskHome} не найден");
                }
            }
            else
            {
                Regex NextPointRoute = new Regex($"^{currentHome}->.*{HomeForAGV}$");
                foreach (string item in RoutesEditClass.RoutesForHomesList)
                {
                    if (NextPointRoute.IsMatch(item))
                    {

                        MainWindow.AddToTB_TB_Ping_Log($"Для следующей задачи выполняется переход в домашнюю позицию от {currentHome} в {HomeForAGV}");
                        AGV_Quest = item;
                        MainWindow.SavedAGVStatuses.OnAppStart_AGV_Quest = AGV_Quest;
                        MainWindow.SavedAGVStatuses.Save();
                        break;

                    }
                }
                if (AGV_Quest == "")
                {

                    ProgramIsRunning = false;

                    MainWindow.AddToTB_TB_Ping_Log($"Маршрут до {nextTaskHome} не найден");

                }
            }

        }

        public static void MoveStraightTheCycle(Prog Current_Status_AGV)
        {

            //Проверяем есть ли данном шаге переход
            if (Current_Status_AGV.TransitionToProgram != null & Current_Status_AGV.TransitionToProgram != "" & Current_Status_AGV.TransitionToStep != null & Current_Status_AGV.TransitionToStep != "")
            {
                ProgrammsLog.Information($"  3 {TransitionMissCount}  {WithoutTransition}");
                //Если есть переход, то проверяем есть ли у нас задание пропускать шаги и сколько 
                if (TransitionMissCount > 0)
                {

                    TransitionMissCount--;
                    MainWindow.SavedAGVStatuses.OnAppStart_TransitionMissCount = TransitionMissCount.ToString();
                    MainWindow.SavedAGVStatuses.Save();

                }
                else
                {
                    ProgrammsLog.Information($"  4");
                    //Если мы не пропускаем шаги, то проверяем на текущем шаге есть ли задача пропускать шаги после данного перехода
                    if (Current_Status_AGV.TransitionMissCount != null & Current_Status_AGV.TransitionMissCount != "")
                    {
                        ProgrammsLog.Information($"  5");
                        MainWindow.SavedAGVStatuses.OnAppStart_TransitionMissCount = Current_Status_AGV.TransitionMissCount.ToString();
                        MainWindow.SavedAGVStatuses.Save();
                        TransitionMissCount = int.Parse(Current_Status_AGV.TransitionMissCount);
                        ProgrammsLog.Information($"  6");
                    }
                    else
                    {
                        ProgrammsLog.Information($"  7");

                    }
                    ProgrammsLog.Information($" Начат переход на {Current_Status_AGV.TransitionToProgram}, {Current_Status_AGV.TransitionToStep}");

                    //Выполняем переход после проверки всех остальных параметров.
                    if (LineCannotBeFound)
                    {

                        SimpleTranzition(int.Parse(Current_Status_AGV.TransitionToProgram), int.Parse(Current_Status_AGV.TransitionToStep));
                        MainWindow.AddToTB_TB_Ping_Log("Ошибка отсутствия линии движения отработана");
                        step = CurrentStep;
                        programm = CurrentProgram;
                        MethodEnded = true;
                        return;

                    }
                    TransitionMethod(int.Parse(Current_Status_AGV.TransitionToProgram), int.Parse(Current_Status_AGV.TransitionToStep));
                    ProgrammsLog.Information($" Закончен переход на {Current_Status_AGV.TransitionToProgram}, {Current_Status_AGV.TransitionToStep}");
                    //Запоминаем какой шаг мы прошли успешно, чтобы не повторять выполнение этого шага
                    step = CurrentStep;
                    programm = CurrentProgram;

                    ProgrammsLog.Information($" Выполнен Шаг {CurrentStep} Программа {CurrentProgram}");
                    //StartEventMethod();
                    MethodEnded = true;
                    return;
                }

                //Если ошибка отсутствия линии то переключаем вручную следующий шаг
                //Запоминаем какой шаг мы прошли успешно, чтобы не повторять выполнение этого шага
                step = CurrentStep;
                programm = CurrentProgram;
            }
            if (LineCannotBeFound)
            {

                SimpleTranzition(CurrentProgram, CurrentStep + 1);
                MainWindow.AddToTB_TB_Ping_Log("Ошибка отсутствия линии движения отработана");

            }
        }


        //Переменная для отслеживания отображения программы и шага в окне мониторинга, так как часто не успевает отрисовываться программа и шаг и уже идет другой шаг и отрисовывается он
        //false если отобразилось и не ожидаем отображения. true если ожидаем отображения
        public static bool awaitingMonitoringShow = false;
        //Если выполняется переход то выводить в мониторинг не нужно шаги и программы лишние.
        public static bool AwaitingTransition = false;
        //Метод для команды перехода на другие шаг и программу
        public static void TransitionMethod(int NextProgram, int NextStep)
        {
            if (AGVOnCharge) { return; }

            //Если во время перехода прога крашится, то агв может поехать совсем не туда после включения и восстановления шагов


            AwaitingTransition = true;
            int CurrentStepWatchDog = CurrentStep;
            int CurrentProgrammWatchDog = CurrentProgram;

            RightStepToPass = NextStep;
            RightProgramToPass = NextProgram;
            ReadAGVRegisters();
            while (registerMap["Текущий шаг"] == CurrentStepWatchDog & registerMap["Текущая программа"] == CurrentProgrammWatchDog)
            {
                if (ProgramIsRunning)
                {
                    if (AGVOnCharge) { Thread.Sleep(100); return; }

                    Thread.Sleep(300);
                    ReadAGVRegisters();
                    if (registerMap["Текущий шаг"] != CurrentStepWatchDog)
                    {
                        break;
                    }
                    if (registerMap["Текущая программа"] != CurrentProgrammWatchDog)
                    {
                        break;
                    }
                    MainWindow.SavedAGVStatuses.OnAppStart_TransitionExit = true;
                    MainWindow.SavedAGVStatuses.Save();

                    StartEventMethod();
                    AGVConnectLog.Information("Старт отправлен 214 TransitionMethod");
                    Thread.Sleep(100);
                    ReadAGVRegisters();
                    continue;

                }
                else
                {
                    if (AGVOnCharge) { return; }
                    Thread.Sleep(300);
                }

            }
            if (registerMap["Текущий шаг"] == NextStep & registerMap["Текущая программа"] == NextProgram)
            {
                //Поток MaintaskWindow также стартует!!!

                return;
            }
            if (MainWindow.Emulator)
            {
                //TODO: эмулятор
                AGV_Client.WriteMultipleRegisters(62218, new int[] { NextProgram });
                AGV_Client.WriteMultipleRegisters(62219, new int[] { NextStep });

                awaitingMonitoringShow = true;
                MainWindow.SavedAGVStatuses.OnAppStart_TransitionExit = false;
                MainWindow.SavedAGVStatuses.Save();

                //while (AwaitingTransition)
                //{

                //    //MainWindow.AGVRegistersShow();
                //    Thread.Sleep(100);

                //}
            }
            else
            {
                //отслеживание полного выполнения всех команд на АГВ
                bool TransitionEndedSuccessfully = false;
                while (TransitionEndedSuccessfully == false)
                {
                    try
                    {
                        //TODO: рабочая
                        ProgrammsLog.Information("Выполняется переход");
                        AGV_Client.WriteMultipleRegisters((int)AGVregisters.Controll, new int[] { 0 });
                        Thread.Sleep(100);

                        AGV_Client.WriteMultipleRegisters((int)AGVregisters.Controll, new int[] { 2 });//Остановка
                        Thread.Sleep(300);

                        AGV_Client.WriteMultipleRegisters(62218, new int[] { NextProgram });
                        Thread.Sleep(100);

                        AGV_Client.WriteMultipleRegisters((int)AGVregisters.Controll, new int[] { 16 });
                        Thread.Sleep(300);

                        AGV_Client.WriteMultipleRegisters(62219, new int[] { NextStep });
                        Thread.Sleep(100);

                        AGV_Client.WriteMultipleRegisters((int)AGVregisters.Controll, new int[] { 32 });
                        Thread.Sleep(600);

                        AGV_Client.WriteMultipleRegisters((int)AGVregisters.Controll, new int[] { 1 });//Запуск
                       
                        ReadAGVRegisters();

                        if (registerMap["Текущий шаг"] == NextStep & registerMap["Текущая программа"] == NextProgram)
                        {
                            TransitionEndedSuccessfully = true;
                            MainWindow.SavedAGVStatuses.OnAppStart_TransitionExit = false;
                            MainWindow.SavedAGVStatuses.Save();

                            awaitingMonitoringShow = true;

                            ProgrammsLog.Information("Переход успешно выполнен");
                            AGVConnectLog.Information($"Переход на программу {NextProgram} и шаг {NextStep} успешно выполнен");

                        }
                        else
                        {
                            AGVConnectLog.Information($"Проверка: Команда успешно отправлена, но ничего не поменялось. Программа {registerMap["Текущая программа"]}, шаг{registerMap["Текущий шаг"]}");
                            MainWindow.AddToTB_TB_Ping_Log("E244");
                            continue;
                        }

                    }
                    catch (Exception ex)
                    {
                        ProgrammsLog.Information("Команда в процессе выполнения, возможно отсутствует связь");
                        AGVReconnect();
                        Thread.Sleep(200);

                    }
                }
            }
            ProgrammsLog.Information($"Перешли на программу {NextProgram} Шаг {NextStep}");

        }



        //Посылаем команду Старт
        public static void StartEventMethod()
        {
            try
            {
                if (Control_AGV.CommandDoneSuccessfully == false) { MainWindow.AddToTB_TB_Ping_Log("Дождитесь завершения предыдущей команды"); return; }

                if (ProgramIsRunning == false)
                {
                    return;
                }
                if (Task_Canceled)
                {
                    ProgramIsRunning = false;
                    MainWindow.AddToTB_TB_Ping_Log("Задание было отменено. Старт невозможен");
                    ProgrammsLog.Information("Отмена старта так как задание было отменено StartEventMethod 1469");
                    return;

                }

                //не отправляем старты если АГВ на Зарядке. Только по Специальной Кнопке
                if (AGVOnCharge) { return; }

                //AGV_Connect.OnlyReadRegistersAGV = true;

                if (MainWindow.Emulator)
                {
                    //TODO: Эмулятор
                    AGV_Connect.AGV_Client.WriteMultipleRegisters(60, new int[] { 1 });
                }
                else
                {

                    int i = 0;
                    Control_AGV.CommandDoneSuccessfully = false;
                    while (Control_AGV.CommandDoneSuccessfully == false)
                    {
                        try
                        {

                            //TODO:Рабочая
                            Thread.Sleep(500);
                            AGV_Client.WriteMultipleRegisters((int)AGVregisters.Controll, new int[] { 0 });
                            Thread.Sleep(300);
                            AGV_Client.WriteMultipleRegisters((int)AGVregisters.Controll, new int[] { 1 });//Запуск
                            Control_AGV.CommandDoneSuccessfully = true;
                            ProgrammsLog.Information("Старт АГВ Успешно выполнен StartEventMethod 1579");
                            
                        }
                        catch (Exception ex)
                        {
                            ProgrammsLog.Information("Команда в процессе выполнения, возможно отсутствует связь StartEventMethod");

                            MainWindow.AddToTB_TB_Ping_Log("\nАГВ не в сети не могу отправить Старт! StartEventMethod");

                            AGVReconnect();
                            Control_AGV.CommandDoneSuccessfully = true;

                            return;// Это чтобы она не ехала в 2 10 после 2 9. Ибо она получает старт и едет в 2 10


                        }
                    }

                }

            }
            catch (Exception ex)
            {
                ProgrammsLog.Error("StartEventMethod" + ex.Message.ToString());
            }
        }

        //Посылаем команду Стоп
        public static void StopEventMethod()
        {
            try
            {
                if (Control_AGV.CommandDoneSuccessfully == false) { MainWindow.AddToTB_TB_Ping_Log("Дождитесь завершения предыдущей команды"); return; }
                if (MainWindow.Emulator)
                {
                    //TODO:Эмулятор
                    AGV_Connect.ProgramIsRunning = false;
                    AGV_Connect.AGV_Client.WriteMultipleRegisters(60, new int[] { 0 });
                }
                else
                {

                    AGV_Connect.ProgramIsRunning = false;
                    Control_AGV.CommandDoneSuccessfully = false;
                    int i = 0;
                    while (Control_AGV.CommandDoneSuccessfully == false)
                    {
                        try
                        {

                            //TODO: Рабочая
                            AGV_Client.WriteMultipleRegisters((int)AGVregisters.Controll, new int[] { 0 });
                            Thread.Sleep(300);
                            AGV_Client.WriteMultipleRegisters((int)AGVregisters.Controll, new int[] { 2 });
                            Control_AGV.CommandDoneSuccessfully = true;
                            ProgrammsLog.Information("Стоп АГВ Успешно выполнено StopEventMethod 1579");

                        }
                        catch (Exception)
                        {

                            ProgrammsLog.Information("Команда в процессе выполнения, возможно отсутствует связь StopEventMethod");

                            MainWindow.AddToTB_TB_Ping_Log("\nАГВ не в сети не могу отправить Стоп! StopEventMethod");

                            AGVReconnect();

                            i++;
                            if (i >= 2)
                            {
                                continue;
                            }

                            Thread.Sleep(3000);

                        }
                    }

                }

            }
            catch (Exception ex)
            {
                ProgrammsLog.Error("StopEventMethod" + ex.Message.ToString());
            }
        }

        //Выполнение перехода
        public static void SimpleTranzition(int program, int step)
        {
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
        }
    }
}
