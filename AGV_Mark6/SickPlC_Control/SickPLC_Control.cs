using Sres.Net.EEIP;
using System;
using Serilog;


namespace AGV_Mark6.SickPlC_Control
{
    internal class SickPLC_Control
    {
        //Класс для работы с РТК Упаковка.

        EEIPClient PLC_client = new EEIPClient(); 
        //подключаемся к ПЛК РТК "Упаковка"
        public void Connect_to_PLC()
        {
            try
            {
                PLC_client.RegisterSession("192.168.51.21");
            }
            catch(Exception ex)
            {

            }
        }

        //Метод открытия шторок безопасности на ПЛК РТК "Упаковка"
        public void PLC_OpenGates()
        {
            try
            {
                PLC_client.RegisterSession("192.168.51.21");
                PLC_client.SetAttributeSingle(0x72, 1, 5, new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                Log.Logger.Information("Шторка открыта");
            }
            catch(Exception ex)
            {
                Log.Logger.Error(ex.Message);
            }    
        }

        //Метод Закрытия шторок безопасности на ПЛК РТК "Упаковка"
        public void PLC_CloseGates()
        {
            try
            {
                PLC_client.RegisterSession("192.168.51.21");
                PLC_client.SetAttributeSingle(0x72, 1, 5, new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                Log.Logger.Information("Шторка закрыта");
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
            }

        }
    }
}
