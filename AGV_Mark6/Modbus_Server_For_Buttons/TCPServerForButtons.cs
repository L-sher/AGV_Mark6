using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AGV_Mark6.MainTaskManager_Methods;
using System;
using AGV_Mark6.SeriLog;
using Serilog;

namespace AGV_Mark6.Modbus_Server_For_Buttons
{

    internal class TCPServerForButtons
    {
        //В этом классе мы работаем с кнопками.
        //Через modbus получаем задание через tcp передаем данные по текущей очереди и заданию 
        static IPEndPoint ipPoint = new IPEndPoint(IPAddress.Any, 8888);
        static Socket TCPListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        //логгер Соединений c АГВ
        private static Serilog.Core.Logger ButtonsLog = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.File(new MyOwnCompactJsonFormatter(), "ButtonsLog.txt").CreateLogger();

        //Запускаем сервер TCP
        public static void HTTPServer()
        {
            try
            {

                // начинаем прослушивать входящие подключения
                TCPListener.Bind(ipPoint);
                TCPListener.Listen();
               

                //Запускаем "общение" с приложением
                HttpResponse();
              
            }
            catch (Exception ex)
            {

                ButtonsLog.Information(ex + " 42");

            }
        }

        //Реализация Входящих запросов и ответов для кнопок
        static async Task HttpResponse()
        {

            while (true)
            {
                
                try
                {
                    using var tcpClient = await TCPListener.AcceptAsync();

                    Thread.Sleep(200);
                    List<string> TCPList = new List<string>();
                    if (MainTaskManager.AGV_Quest == "")
                    {

                        TCPList.Add("");

                    }
                    else
                    {
                        //ограничиваю задания которые отображаются. Должны отображаться только маршруты в ловители
                        if (RoutesEditClass.RoutesList.Contains(MainTaskManager.AGV_Quest))
                        {
                            TCPList.Add(MainTaskManager.AGV_Quest);
                        }
                        else
                        {
                            TCPList.Add("Еду на следующее задание");
                        }
                    }

                    foreach (string item in MainTaskManager.AGVQueue)
                    {
                        TCPList.Add(item);
                    }

                    byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(TCPList));

                    // отправляем данные
                    SocketFlags flags = new SocketFlags();
                    await tcpClient.SendAsync(data, flags);

                }
                catch(Exception ex)
                {
                    ButtonsLog.Information(ex + " 94");
                }
            }
        }
    }
}
