// From C. Martins code (PC, S1516V)

/*
The action graph is as follows:

- Starts with A;
- After A finishes execution, can be executed B, C, D, and E;
- After D and E finish execution, can be executed F;
- After B, C and F finish execution, can be executed G.

In this design:
- Thread T1 executes A, B and G;
- Thread T2 executes C;
- Thread T3 executes D and F;
- Thread T4 executes E.
*/
		
//
// Uncomment the next define in order to user THREAD_JOIN
//

#define USE_THREAD_JOIN


using System;
using System.Threading;

static class Graph {

	//
	// Synchronizers
	//

	private static ManualResetEventSlim t1_2_t2t3t4 = new ManualResetEventSlim(false);

#if (!USE_THREAD_JOIN)

	// In this usage CountdownEvent is similar to ManualResetEventSlim!
	private static CountdownEvent t4_2_t3 = new CountdownEvent(1);
	
	private static SemaphoreSlim t2t3_2_t1 = new SemaphoreSlim(0, 2);

#endif

	//
	// Thread references
	//

	private static Thread[] ths = new Thread[4];

	//
	// Sleep constants
	//

	private static int MIN_TIMEOUT = 50;
	private static int MAX_TIMEOUT = 2000;

	//
	// Simulate action execution.
	//
	
	private static void DoAction(string actionName, int duration)
	{
		Console.Write("\n-->" + actionName);
		Thread.Sleep(duration);
		Console.Write("\n<--" + actionName);
	}
	
	//
	// Thread T1
	//

	private static void T1()
	{
		Random rand = new Random(Thread.CurrentThread.ManagedThreadId);
		
		// Do action A
		DoAction("A", rand.Next(MIN_TIMEOUT, MAX_TIMEOUT));
	
		// Allow T2, T3 and T4 to start.
	
		t1_2_t2t3t4.Set();
	
		// Do action B
		DoAction("B", rand.Next(MIN_TIMEOUT, MAX_TIMEOUT));

#if (USE_THREAD_JOIN)

		// Wait until threads T2 and T3 have been exited.
		ths[1].Join();
		ths[2].Join();
#else	

		// Acquire two permissions from the semaphore.

		t2t3_2_t1.Wait();
		t2t3_2_t1.Wait();
#endif
	
		// Do action G
		DoAction("G", rand.Next(MIN_TIMEOUT, MAX_TIMEOUT));
	}

	//
	// Thread T2
	//

	private static void T2()
	{
		Random rand = new Random(Thread.CurrentThread.ManagedThreadId);
	
		// Wait until T1 completes A
		t1_2_t2t3t4.Wait();	
	
		// Do action C
		DoAction("C", rand.Next(MIN_TIMEOUT, MAX_TIMEOUT));

#if (!USE_THREAD_JOIN)
	
		t2t3_2_t1.Release();
#endif
	}

	//
	// Thread T3
	//

	private static void T3()
	{
		Random rand = new Random(Thread.CurrentThread.ManagedThreadId);
	
		// Wait until t1 completes A
		t1_2_t2t3t4.Wait();	
	
		// Do action D
		DoAction("D", rand.Next(MIN_TIMEOUT, MIN_TIMEOUT));

		// Wait until T4 completes action E
	
#if (USE_THREAD_JOIN)

		ths[3].Join();	
#else

		// Wait until the t4_2_t3 event is signalled
		t4_2_t3.Wait();	
#endif
	
		// Do action F
		DoAction("F", rand.Next(MIN_TIMEOUT, MAX_TIMEOUT));

#if (!USE_THREAD_JOIN)
	
		t2t3_2_t1.Release();
#endif
	}

	//
	// Thread T4
	//

	private static void T4()
	{
		Random rand = new Random(Thread.CurrentThread.ManagedThreadId);
		
		// Wait until T1 completes action A
		t1_2_t2t3t4.Wait();	
	
		// Do action E
		DoAction("E", rand.Next(MIN_TIMEOUT, MAX_TIMEOUT));

#if (!USE_THREAD_JOIN)
	
		t4_2_t3.Signal();
#endif
	}


	private static void Main()
	{

		// Create the worker threads.
		ths[0] = Thread.CurrentThread;
		ths[1] = new Thread(T2);
		ths[2] = new Thread(T3);
		ths[3] = new Thread(T4);

		// Start the threads
		ths[1].Start();
		ths[2].Start();
		ths[3].Start();

		// Wait for a while
		Thread.Sleep(500);
		
		Console.Write("++executing actions...");

		
		// T1 is executed by the primary thread.
		T1();
		//Console.WriteLine();
		//
		// Since all thread are foreground threads, the process only exits
		// when all threads have been exited.
		// 	
	}
}
