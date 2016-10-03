// From C. Martins code (PC, S1516V)

// Run it with as many threads as processors
// Use process explorer to view the CPU occupancy
// Run it with one less thread than processors
// Discuss thread abort semantics
// Discuss thread interruption semantics

using System;
using System.Threading;


class FullLoop {
	public static void Main() {

		// Create as many busy loop threads as processors.
		Thread[] threads = new Thread[Environment.ProcessorCount];
    Console.WriteLine("Creating {0} threads", Environment.ProcessorCount);
		for (int i = 0; i < threads.Length; ++i) {
      var ix = i;
			threads[i] = new Thread(() => {
        while(true){
          try {		
            while(true){
              Thread.Sleep(0);
            }			
          } catch (Exception e){
            Console.WriteLine("[{0}] {1}", ix, e);
            break;
          }
        }
        //Console.WriteLine("[{0}] exiting",ix);
      });
			threads[i].Start();
      Console.WriteLine("Thread {0} is {1}", ix, threads[ix].ThreadState);
		}
    Console.WriteLine("Threads created.Press any key to stop...");
		Console.ReadLine();
		
		// Abort each created threads and wait it terminates. 
		for (int i = 0; i < threads.Length; ++i) {
			//threads[i].Abort();
      threads[i].Interrupt();
			threads[i].Join();
		}
	}
}