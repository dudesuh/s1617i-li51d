using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileHash1
{

    public class Barrier
    {
        private readonly int _needed;
        private volatile int _current;
        public Barrier(int needed)
        {
            _needed = needed;
            _current = 0;
        }

        public bool Signal()
        {
            return Interlocked.Increment(ref _current) == _needed;
        }

        public void Reset()
        {
            _current = 0;
        }
    }

    public class Program
    {
        const int BufLen = 4 * 1024 * 1024;
        const int MaxReentry = 10;
        const string Path = @"C:\home\toremove.log";
        private static readonly Random random = new Random();

        class State : IDisposable
        {
            public readonly SHA1 HashFunction;
            public readonly byte[][] ByteBuffers;
            public readonly FileStream InputStream;
            public readonly ManualResetEventSlim Done;
            public static ThreadLocal<int> ReentryCounter = new ThreadLocal<int>();
            public Barrier Barrier = new Barrier(2);
            private int ReadBufferIx = 0;

            public byte[] ReadBuffer { get { return ByteBuffers[ReadBufferIx]; } }
            public byte[] HashBuffer { get { return ByteBuffers[(ReadBufferIx + 1) % 2]; } }

            public State(string path)
            {
                HashFunction = SHA1.Create();
                ByteBuffers = new byte[2][];
                ByteBuffers[0] = new byte[BufLen];
                ByteBuffers[1] = new byte[BufLen];

                InputStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 4096, true);
                Done = new ManualResetEventSlim(false);
            }

            public void ToggleBuffers()
            {
                ReadBufferIx = (ReadBufferIx + 1) % 2;
            }

            public void Dispose()
            {
                HashFunction.Dispose();
                InputStream.Dispose();
            }
        }

        public static void Main(string[] args)
        {
            using (var state = new State(Path))
            {
                var sw = new Stopwatch();
                sw.Start();
                state.Barrier.Signal();
                state.InputStream.BeginRead(state.ReadBuffer, 0, BufLen, Callback, state);
                state.Done.Wait();
                state.HashFunction.TransformFinalBlock(state.HashBuffer, 0, 0);
                sw.Stop();
                Console.WriteLine();
                Console.WriteLine($"Hash is {BitConverter.ToString(state.HashFunction.Hash)}, elapsed: {sw.Elapsed.TotalMilliseconds}");
                Console.ReadKey();
            }
        }

        public static void Callback(IAsyncResult ar)
        {
            var state = ar.AsyncState as State;
            if (!state.Barrier.Signal())
            {
                return;
            }
            if (ar.CompletedSynchronously)
            {
                if (State.ReentryCounter.Value == MaxReentry)
                {
                    ThreadPool.QueueUserWorkItem(_ => HandleCompletion(state, ar));
                }
                else
                {
                    State.ReentryCounter.Value += 1;
                    HandleCompletion(state, ar);
                    State.ReentryCounter.Value -= 1;
                }
            }
            else
            {
                HandleCompletion(state, ar);
            }
        }

        private static void HandleCompletion(State state, IAsyncResult ar)
        {
            while (true)
            {
                var len = state.InputStream.EndRead(ar);
                if (len == 0)
                {
                    state.Done.Set();
                    return;
                }
                else
                {
                    state.ToggleBuffers();
                    state.Barrier.Reset();

                    //ar = state.InputStream.BeginRead(state.ReadBuffer, 0, BufLen, Callback, state);
                    state.HashFunction.TransformBlock(state.HashBuffer, 0, len, state.HashBuffer, 0);
                    ar = state.InputStream.BeginRead(state.ReadBuffer, 0, BufLen, Callback, state);
                    var r = state.Barrier.Signal();
                    Console.Write(r ? '+' : '.');
                    if (!r) break;
                }
            }
        }
    }
}
