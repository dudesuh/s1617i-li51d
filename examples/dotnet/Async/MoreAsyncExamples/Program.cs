using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoreAsyncExamples
{
    class Program
    {

        public static async Task<int> Method1Async()
        {
            if (true) throw new Exception();
            return 2;
        }

        public static Task<int> Method2Async()
        {
            if (true)
            {
                var tcs = new TaskCompletionSource<int>();
                tcs.SetException(new Exception());
                return tcs.Task;
             }   
            return Task.FromResult(2);
            // NO return Task.Factory.StartNew(() => 2);
        }


        static void Main(string[] args)
        {
            var t = Method2Async();
            Console.WriteLine($"Task status is {t.Status}");
            Console.ReadKey();
        }
    }
}
