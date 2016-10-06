using System;
using System.Threading;

namespace Monitors
{
  class Semaphore
  {
    private int units;
    private readonly object lockObj = new object();

    public Semaphore(int initialUnits)
    {
      units = initialUnits;
    }

    public void Acquire(TimeSpan timeout){
      lock(lockObj)
      {
        while(units <= 0)
        {
          try
          {
            Monitor.Wait(lockObj, timeout);
          }
          catch(ThreadInterruptedException e)
          {
            if(units > 0)
            {
              Monitor.Pulse(lockObj);
            }
            throw;
          }
        }
        units -= 1;
      }
    }

    public void Release(){
      lock(lockObj)
      {
        units += 1;
        Monitor.Pulse(lockObj);
      }
    }

  }
}