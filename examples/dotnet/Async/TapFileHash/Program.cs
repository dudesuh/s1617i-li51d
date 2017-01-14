using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TapFileHash
{
    class Sha1HashProcessor
    {
        public Task<byte[]> ComputeHashAsync(Stream s)
        {
            var h = SHA1.Create();
            var buffer = new byte[4 * 1024 * 1024];
            var result = new TaskCompletionSource<byte[]>();
            Action<Task<int>> cb = null;
            cb = task =>
            {
                Console.Write('.');
                try
                {
                    // TODO try-catch
                    var readBytes = task.Result;
                    if (readBytes != 0)
                    {
                        h.TransformBlock(buffer, 0, readBytes, buffer, 0);
                        var t2 = s.ReadAsync(buffer, 0, buffer.Length);
                        t2.ContinueWith(cb);

                    }
                    else
                    {
                        h.TransformFinalBlock(buffer, 0, 0);
                        result.SetResult(h.Hash);
                        h.Dispose();
                    }
                }
                catch (Exception e)
                {
                    result.SetException(e);
                    h.Dispose();
                    return;
                }
            };
            var t = s.ReadAsync(buffer, 0, buffer.Length);            
            t.ContinueWith(cb);
            return result.Task;
        }

        public byte[] ComputeHash(Stream s)
        {
            var buffer = new byte[4 * 1024 * 1024];
            using (var h = SHA1.Create())
            {
                while (true)
                {
                    var readBytes = s.Read(buffer, 0, buffer.Length);
                    if (readBytes == 0)
                    {
                        h.TransformFinalBlock(buffer, 0, 0);
                        return h.Hash;
                    }
                    else
                    {
                        h.TransformBlock(buffer, 0, readBytes, buffer, 0);
                    }
                }
            }
        }


        public async Task<byte[]> ComputeHashAsync3(Stream s)
        {
            var buffer = new byte[4 * 1024 * 1024];
            using (var h = SHA1.Create())
            {
                while (true)
                {
                    var readBytes = await s.ReadAsync(buffer, 0, buffer.Length);
                    if (readBytes == 0)
                    {
                        h.TransformFinalBlock(buffer, 0, 0);
                        return h.Hash;
                    }
                    else
                    {
                        h.TransformBlock(buffer, 0, readBytes, buffer, 0);
                    }
                }
            }
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
                Console.WriteLine("Before ComputeHashAsync3");
                var t = hp.ComputeHashAsync3(s);
                Console.WriteLine("After ComputeHashAsync3");

                while (!t.IsCompleted)
                {
                    Console.Write('w');
                    Thread.Sleep(2000);
                }               
                var hash = t.Result;
                Console.WriteLine();
                Console.WriteLine($"Hash is {BitConverter.ToString(hash)}");
                Console.ReadKey();
            }
    }
    }
}
