using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SemaphoreAsync
{
    class TapQueueTest
    {
        class Holder
        {
            public readonly int Value;
            public Holder(int v)
            {
                Value = v;
            }
        }

        static volatile bool end = false;
        static int sent = 0;
        static int received = 0;

        public static void Run()
        {
            const int NOfProducers = 16;
            const int NOfConsumers = NOfProducers;
            
            var q = new TapQueue<Holder>(NOfProducers / 4);
            var producers = new Thread[NOfProducers];
            var consumers = new Thread[NOfConsumers];

            for(var i = 0; i<NOfProducers; ++i)
            {
                var ix = i;
                producers[i] = new Thread(() =>
                {
                    var rnd = new Random(ix);
                    while (!end)
                    {
                        var v = rnd.Next(10);
                        if (q.PutAsync(new Holder(v), rnd.Next(50)).Result)
                        {
                            Interlocked.Add(ref sent, v);
                        }
                        else
                        {
                            Console.WriteLine("producer timeout");
                        }

                        Thread.Sleep(rnd.Next(50));
                    }
                    Console.WriteLine($"exit producer {ix}");
                });
                producers[i].Start();
            }
            for (var i = 0; i < NOfConsumers; ++i)
            {
                var ix = i;
                consumers[i] = new Thread(() =>
                {
                    var rnd = new Random(ix);
                    while (!end)
                    {
                        var h = q.TakeAsync(rnd.Next(50)).Result;
                        if (h != null)
                        {
                            Interlocked.Add(ref received, h.Value);
                        }
                        else
                        {
                            Console.WriteLine("consumer timeout");
                        }
                        Thread.Sleep(rnd.Next(50));
                    }
                    Console.WriteLine($"exit consumer {ix}");
                });
                consumers[i].Start();
            }
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    break;
                }
                Thread.Sleep(1000);
                Console.WriteLine($"sent={Volatile.Read(ref sent)}, received={Volatile.Read(ref received)}");
            }
            Console.ReadKey();
            end = true;
            for(var i = 0; i<NOfConsumers; ++i)
            {
                consumers[i].Join();
            }
            for (var i = 0; i < NOfProducers; ++i)
            {
                producers[i].Join();
            }
            while (true)
            {
                var h = q.TakeAsync(0).Result;
                if (h == null) break;
                Interlocked.Add(ref received, h.Value);
            }
            var s = Volatile.Read(ref sent);
            var r = Volatile.Read(ref received);
            Console.WriteLine($"sent={s}, received={r}, equal={s==r}");
        }
    }
}
