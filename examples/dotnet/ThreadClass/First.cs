using System;
using System.Threading;
using System.Collections.Concurrent;

public class Parameters
{
  public int TheInt {get;set;}
  //public ManualResetEventSlim TheEvent {get;set;}
  //public int Result {get;set;}
  public BlockingCollection<int> Queue {get; private set;}
  public Parameters(){
    Queue = new BlockingCollection<int>();
  }
}

public class First
{
  private static void ThreadMethod(){
    Console.WriteLine("thread started {0}", 
      Thread.CurrentThread.ManagedThreadId);
    while(true);
  }

  private static int SomethingVeryCostly(int i)
  {
    Thread.Sleep(5000);
    // while(true){
    //   try{
    //     while(true);
    //   }catch(ThreadAbortException e){
    //     Console.WriteLine(e);
    //   }
    // }
    return i + 1;
  }

  private static void Wrapper(object prms){
    var p = prms as Parameters;
    var res = SomethingVeryCostly(p.TheInt);
    p.Queue.Add(res);
  }

  public static void Main(string[] args)
  {
    var ev = new ManualResetEventSlim();
    Console.WriteLine("before call");
    //var result = SomethingVeryCostly(3);
    var th = new Thread(Wrapper);
    var prms = new Parameters
    {
      TheInt = 3,
      //TheEvent = ev,
    };
    th.Start(prms);
    //Console.WriteLine("Press a key to abort...");
    //Console.ReadKey();
    //th.Abort();
    //th.Join();
    //Console.WriteLine("After join");
    var result = prms.Queue.Take();
    Console.WriteLine("after call, result = {0}", result);
    //Console.WriteLine("after call, result = {0}", prms.Result);
  }
}