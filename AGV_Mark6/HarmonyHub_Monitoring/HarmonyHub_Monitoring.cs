using EasyModbus;
using Serilog;
using System;
using System.Threading;
using AGV_Mark6.Modbus_Server_For_Buttons;
using System.Collections.Generic;
using AGV_Mark6.Model;
using System.Linq;


namespace AGV_Mark6.HarmonyHub_Monitoring
{

    internal class HarmonyHub_Monitoring
    {

        //Задел под работу через кнопки от HarmonyHub
        private static ModbusClient ButtonClient = new ModbusClient();
        private static bool HarmonyConnected=false;

        public static void HarmonyHub_Client()
        {

            try
            {
                HarmonyHub_reconnect();
                ButtonMonitoring();
            }
            catch(Exception e)
            {
                HarmonyConnected = false;
                Log.Logger.Information("Подключение отсутствует");
            }


        }

        public static void HarmonyHub_reconnect()
        {
            while (!HarmonyConnected)
            {
                try
                {
                    ButtonClient = new ModbusClient();
                    ButtonClient.UnitIdentifier = 255;
                    ButtonClient.Connect("192.168.51.31", 502);
                    HarmonyConnected = true;
                    Log.Logger.Information("Harmony нашлась");
                }
                catch (Exception)
                {
                    Log.Logger.Information("Harmony не найдена в сети");
                    Thread.Sleep(1000);
                }
            }
            
            
        }

        public static List<registersForButtons> RegistersData = new List<registersForButtons>();
        private static void ButtonMonitoring()
        {
            try
            {

                AGV_Storage_Context db = new AGV_Storage_Context();
                RegistersData = db.RegistersForButtons.Where(e => e.MechanicalButtonRegister >= 0).ToList();

            }
            catch (Exception ex)
            {
                Log.Logger.Information("Harmony"+ ex);
            }
           

            while (true)
            {
                try
                {
                    foreach(var item in RegistersData)
                    {
                        if (item.MechanicalButtonRegister > 3) { continue; }
                        int Button1 = ButtonClient.ReadHoldingRegisters(item.MechanicalButtonRegister, 1)[0];
                        if (Button1 == 1)
                        {

                            Class_ModbusServer_for_Buttons.registerWatchDog(item.RegisterNumber, 1);
                            Thread.Sleep(1000 * 60);
                        }

                    }

                  
                }
                catch(Exception ex) 
                {
                    HarmonyConnected = false;
                    HarmonyHub_reconnect();
                    Log.Logger.Information("Harmony теряет связь. ButtonMonitoring "+ ex);
                }
               
                
            }
        }

    }

}
