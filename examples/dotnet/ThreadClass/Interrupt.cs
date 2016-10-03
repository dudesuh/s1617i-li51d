using System;
using System.Threading;

public class Interrupts{

  private static void ThreadMethod(){
    while(true){
      try{
        Thread.Sleep(0);
      }catch(ThreadInterruptedException e){
        Console.WriteLine(e);
      }
    }
  }

  public static void Main(string[] args){
    var th = new Thread(ThreadMethod);
    th.Start();
    while(true){
      Console.ReadKey();
      th.Interrupt();
    }    
  } 
}