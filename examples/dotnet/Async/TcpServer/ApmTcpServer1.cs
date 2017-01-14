using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpServer
{
    static class ApmTcpServer1
    {
        private static ThreadLocal<int> rcounter = new ThreadLocal<int>();

        public static void Run()
        {
            var listener = new TcpListener(
                IPAddress.Parse("127.0.0.1"),
                8888
                );

            AsyncCallback readCallback = null;
            readCallback = ar =>
            {
                var t = ar.AsyncState as Tuple<NetworkStream, byte[]>;
                var s = t.Item1;
                var buf = t.Item2;
                var len = s.EndRead(ar);
                var cmd = Encoding.ASCII.GetString(buf, 0, len);
                var last = cmd.Contains('\n');
                Console.WriteLine(cmd);
                var output = Encoding.ASCII.GetBytes(cmd.ToUpper());
                s.BeginWrite(output, 0, output.Length, _ =>
                {
                    if (last)
                    {
                        s.Dispose();
                    }
                    else
                    {
                        s.BeginRead(buf, 0, buf.Length, readCallback, ar.AsyncState);
                    }
                }, null);
            };

            AsyncCallback acceptCallback = null;
            acceptCallback = ar =>
            {
                var tcpClient = listener.EndAcceptTcpClient(ar);
                listener.BeginAcceptTcpClient(acceptCallback, null);
                // process socket
                var buf = new byte[4 * 1204];
                var s = tcpClient.GetStream();
                s.BeginRead(buf, 0, buf.Length, readCallback, Tuple.Create(s, buf));
            };

            AsyncCallback acceptCallbackEntry = ar =>
            {
                if (!ar.CompletedSynchronously)
                {
                    acceptCallback(ar);
                }
                else
                {
                    rcounter.Value += 1;
                    if (rcounter.Value > 10)
                    {
                        ThreadPool.QueueUserWorkItem(_ => { acceptCallback(ar); });
                    }
                    else
                    {
                        acceptCallback(ar);
                    }
                    rcounter.Value -= 1;
                }
            };
            listener.Start();
            listener.BeginAcceptTcpClient(acceptCallback, null);
            Console.ReadKey();
        }
    }
}
