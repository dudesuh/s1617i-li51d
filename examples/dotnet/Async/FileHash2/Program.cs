using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileHash2
{
    

    class State : IDisposable
    {
        private readonly ManualResetEvent manualResetEvent;

        public Stream Stream { get; }
        public SHA1 Hash { get; }
        public byte[] Buffer { get; }

        public WaitHandle WaitHandle { get { return manualResetEvent; } }

        public State(Stream s, SHA1 hash, byte[] buffer)
        {
            Stream = s;
            Hash = hash;
            Buffer = buffer;           
            manualResetEvent = new ManualResetEvent(false);
;       }

        public void Done()
        {
            manualResetEvent.Set();
        }

        public void Dispose()
        {
            Stream.Dispose();
            Hash.Dispose();
            WaitHandle.Dispose();
        }
    }

    class Program
    {
        const int BlockSize = 4 * 1024 * 1024;
        static void Main(string[] args)
        {
            var s = new FileStream(@"c:\home\toremove.log",
                FileMode.Open, FileAccess.Read, 
                FileShare.None, 4 * 1024, useAsync: true);

            var h = SHA1.Create();
            var buf = new byte[BlockSize];
            using (var state = new State(s, h, buf))
            {
                s.BeginRead(buf, 0, BlockSize, Callback, state);
                state.WaitHandle.WaitOne();
                state.Hash.TransformFinalBlock(state.Buffer, 0, 0);
                Console.WriteLine();
                Console.WriteLine($"Hash is {BitConverter.ToString(state.Hash.Hash)}");
                Console.ReadKey();
            }

        }

        

        private static void Callback(IAsyncResult ar)
        {
            Console.Write('.');
            var state = ar.AsyncState as State;
            var readBytes = state.Stream.EndRead(ar);
            if(readBytes != 0)
            {
                state.Hash.TransformBlock(state.Buffer, 0, readBytes, state.Buffer, 0);
                state.Stream.BeginRead(state.Buffer, 0, BlockSize, Callback, state);
            }else
            {
                state.Done();
            }
        }
    }
}
