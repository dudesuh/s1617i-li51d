using System;
using System.Threading;
using System.Linq;

namespace Monitors.First
{
  public class Parameters
  {
    public long result; 
  }

  public class Program
  {
    private static object theLock = new object();
    private static long shared = 0;

    private static void ThreadMethod(object prms){
      long local = 0;
      try{
        while(true){
          lock(theLock){
            shared += 1;
          }
          local += 1;
        }
      }catch(ThreadInterruptedException){
        Console.WriteLine("interrupting with local = {0}",local);
        (prms as Parameters).result = local;
      }
    }

    public static void Main(string[] args)
    {
      const int nOfThreads = 2;
      var ths = Enumerable.Range(0,nOfThreads).Select(i => new Thread(ThreadMethod)).ToArray();
      var prms = new Parameters[nOfThreads];
      for(var i = 0 ; i < nOfThreads ; ++i)
      {
        prms[i] = new Parameters();
        ths[i].Start(prms[i]);
      }
      Thread.Sleep(3000);
      long acc = 0;
      for(var i = 0 ; i < nOfThreads ; ++i)
      {
        ths[i].Interrupt();
        ths[i].Join();
        acc += prms[i].result;
      }
      Console.WriteLine(acc);
      Console.WriteLine(shared);
    }
  }
}