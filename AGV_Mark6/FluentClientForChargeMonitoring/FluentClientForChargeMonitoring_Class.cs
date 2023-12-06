using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FluentModbus;
using AGV_Mark6.MainTaskManager_Methods;


namespace AGV_Mark6.FluentClientForChargeMonitoring
{

    internal class FluentClientForChargeMonitoring_Class
    {

        public static float ChargingPercentage = 0;

        //Читаем с АГВ регистры которые отвечают за процент заряда.
        public static void ChargeMonitoring()
        {

            while (true)
            {
                if (!Web_Connections.Ping_Agv.AGVIsOnline) { Thread.Sleep(1000); continue; }
                ModbusTcpClient FluentClient = new ModbusTcpClient();
                try
                {

                   
                    FluentClient.ConnectTimeout = 500;
                    FluentClient.Connect("10.1.101.2", ModbusEndianness.BigEndian);

                    Span<short> Registers = FluentClient.ReadHoldingRegisters<short>(255, 60204, 2);//Возможно 0x00 или 255 unitidentifier

                    FluentClient.Dispose();

                    ChargingPercentage = Registers.GetLittleEndian<float>(0);

                    if (MainTaskManager.AGVOnCharge)
                    {
                        if (ChargingPercentage > 98)
                        {

                            Control_AGV.AGVChargedUp();

                        }
                    }

                    if (ChargingPercentage < 30)
                    {
                        if (!MainTaskManager.AGVOnCharge)
                        {
                            //Запускаем цикл по которому мы проверяем условия по которым АГВ может стать на заряд
                            while (!MainTaskManager.AGVOnCharge) 
                            {

                                if (MainTaskManager.LoadStatus == "Пустая")
                                {

                                    if (!MainTaskManager.AGV_Quest.Contains("Взять"))
                                    {

                                        MainTaskManager.GoToCharge();
                                        Thread.Sleep(1500);
                                        continue;

                                    }

                                }
                                Thread.Sleep(500);

                            }
                        }
                        Thread.Sleep(1000 * 60 * 5);
                    }

                   
                    
                    MainWindow.mainWindowClient.Dispatcher.Invoke(() =>
                    {

                        MainWindow.mainWindowClient.PB_AGV_Charge.Value = ChargingPercentage;
                        MainWindow.mainWindowClient.PB_Textblock.Text = ChargingPercentage.ToString()+ "%";

                    });
                    Thread.Sleep(1000*60*2);
                }
                catch (Exception ex)
                {

                    FluentClient.Dispose();
                    MainWindow.AddToTB_TB_Ping_Log("Ошибка прочтения заряда АГВ \n"+ex.Message);
                    Thread.Sleep(1000 * 60);

                }
            }
        }

      



    }
}
