using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileHash3
{
    class Sha1HashProcessor
    {
        public IAsyncResult BeginComputeHash(Stream s, AsyncCallback callback, object state)
        {
            var h = SHA1.Create();
            var buffer = new byte[4 * 1024 * 1024];
            var result = new GenericAsyncResult<byte[]>(callback, state, synchCompletion: false);
            AsyncCallback cb = null;
            cb = ar =>
            {
                Console.Write('.');
                try
                {
                    var readBytes = s.EndRead(ar);
                    if (readBytes != 0)
                    {
                        h.TransformBlock(buffer, 0, readBytes, buffer, 0);
                        s.BeginRead(buffer, 0, buffer.Length, cb, null);
                    }
                    else
                    {
                        h.TransformFinalBlock(buffer, 0, 0);                        
                        result.OnComplete(h.Hash, null);
                        h.Dispose();
                    }
                }
                catch (Exception e)
                {
                    result.OnComplete(null, e);
                    h.Dispose();
                    return;
                }
            };
            s.BeginRead(buffer, 0, buffer.Length, cb, null);
            return result;
        }

        public byte[] EndComputeHash(IAsyncResult ar)
        {
            var genericAsyncResult = ar as GenericAsyncResult<byte[]>;
            return genericAsyncResult.Result;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            using (var s = new FileStream(@"c:\home\toremove.log",
                FileMode.Open, FileAccess.Read,
                FileShare.None, 4 * 1024, useAsync: true))
            {

                var hp = new Sha1HashProcessor();
                var ar = hp.BeginComputeHash(s, null, null);
                //while (!ar.IsCompleted)
                //{
                //    Console.Write('w');
                //    Thread.Sleep(2000);
                //}
                //ar.AsyncWaitHandle.WaitOne();
                Console.WriteLine("\nBefore EndComputeHash");
                var hash = hp.EndComputeHash(ar);
                Console.WriteLine();
                Console.WriteLine($"Hash is {BitConverter.ToString(hash)}");
                Console.ReadKey();
            }

        }
    }
}
