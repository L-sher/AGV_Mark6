using AGV_Mark6.FluentClientForChargeMonitoring;
using System.Threading;
using AGV_Mark6.MainTaskManager_Methods;


namespace AGV_Mark6.ChargingState
{
    //Класс для отслеживания того, заряжается ли АГВ. 
    internal class ChargingUp
    {
        
        public static void ChargingWatchDog()
        {
            float CurrentChargePercentage = FluentClientForChargeMonitoring_Class.ChargingPercentage;
            float ChargePercentageIndicator = CurrentChargePercentage;
            int DisChargeIndicator = 0;

            //Отслеживаем время нахождения АГВ на Зарядке
            int ChargeTimer = 0;

            while (MainTaskManager.AGVOnCharge)
            {
                if (ChargeTimer > 60)
                {

                    MainTaskManager.NotificationTelegram("АГВ на зарядке уже больше часа");
                    ChargeTimer -= 60*16;
                
                }
                if (!Web_Connections.Ping_Agv.AGVIsOnline)
                {
                    Thread.Sleep(1000 * 60 * 2);
                    ChargeTimer += 2;
                    continue;
                }

                CurrentChargePercentage = FluentClientForChargeMonitoring_Class.ChargingPercentage;

                //Отслеживаем если АГВ на зарядке РАЗРЯЖАЕТСЯ
                if (CurrentChargePercentage < ChargePercentageIndicator)
                {

                    ChargePercentageIndicator = CurrentChargePercentage;
                    DisChargeIndicator++;

                    if (DisChargeIndicator > 5)
                    {

                        //Отправляем сообщение, что АГВ не заряжается.
                        MainTaskManager.NotificationTelegram("АГВ на зарядке, но все ещё не заряжается.");

                    }

                }

                //Если процент заряда растет, значит АГВ успешно заряжается.
                if(CurrentChargePercentage > ChargePercentageIndicator)
                {

                    ChargePercentageIndicator = CurrentChargePercentage;
                    DisChargeIndicator = 0;

                }
                else
                {

                }
                Thread.Sleep(1000 * 60 * 2);
                ChargeTimer += 2;
            }
        }

    }
}
