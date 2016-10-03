// From C. Martins code (PC, S1516V)

//
// Comment the next line in order to create only one thread to compare
// the synchronization cost without contention.
//

#define TWO_INC_THREADS

using System;
using System.Threading;


class MySpinLock {

#pragma warning disable 420

	private const int FREE = 1;
	private const int BUSY = 0;
	
	private volatile int state = FREE;
	
	public void Enter() {
		SpinWait sw = new SpinWait();
		
		for (;;) {
			if (state == FREE && Interlocked.Exchange(ref state, BUSY) == FREE) {
				return;
			}
			do {
				sw.SpinOnce();
			} while (state == BUSY);
		}
	}
	
	public void Exit() {
		state = FREE;
	}
#pragma warning restore 420
}
	
static class Locks {

	//
	// The several flavours of locks available.
	//

	private static SpinLock spinLock = new SpinLock();
	private static MySpinLock mySpinLock = new MySpinLock();
	private static object intrinsicLock = new object();
	private static ReaderWriterLock readerWriterLock = new ReaderWriterLock();
	private static ReaderWriterLockSlim readerWriterSlimLock = new ReaderWriterLockSlim();
	private static Mutex mutexLock = new Mutex();
	private static Semaphore semaphoreLock = new Semaphore(1, 1);
	private static AutoResetEvent autoResetEventLock = new AutoResetEvent(true);

	//
	// The shared counter.
	//

	private static int sharedCounter;

	//
	// Increment the shared counter using several ways to synchronize.
	// 

	private static void UnprotectedIncrement() {
		sharedCounter++;
	}

	private static void AtomicIncrement() {
		Interlocked.Increment(ref sharedCounter);
	}

	private static void AtomicIncrementUsingCAS() {
		SpinWait sw = new SpinWait();
		int repeatCount = 0;
		for(;;) {
			int c = sharedCounter;
			if (Interlocked.CompareExchange(ref sharedCounter, c + 1, c) == c) {
				/*
				if (repeatCount >= 10)
					Console.Write('.');
				*/
				return;
			}
			repeatCount++;
			sw.SpinOnce();
		}
	}

	private static void SpinLockProtectedIncrement() {	
		bool lockTaken = false;
		try {
			spinLock.Enter(ref lockTaken);
			UnprotectedIncrement();
		} finally {
			if (lockTaken)
				spinLock.Exit();
		}
	}

	private static void MySpinLockProtectedIncrement() {	
		mySpinLock.Enter();
		UnprotectedIncrement();
		mySpinLock.Exit();
	}

	private static void  IntrinsicLockProtecetedIncrement() {
	
		lock(intrinsicLock) {
			UnprotectedIncrement();
		}
		
		// Which expands to:
		/*
		bool lockTaken = false;
		try {
			Monitor.Enter(intrinsicLock, ref lockTaken);
			sharedCounter++;
		} finally {
			if (lockTaken)
				Monitor.Exit(intrinsicLock);
		}
		*/
	}

	private static void  ReaderWriterLockProtecetedIncrement() {

		readerWriterLock.AcquireWriterLock(Timeout.Infinite);
		UnprotectedIncrement();
		readerWriterLock.ReleaseWriterLock();
	}

	private static void  ReaderWriterSlimLockProtecetedIncrement() {

		readerWriterSlimLock.EnterWriteLock();
		UnprotectedIncrement();
		readerWriterSlimLock.ExitWriteLock();
	}

	private static void  MutexProtecetedIncrement() {

		mutexLock.WaitOne();
		UnprotectedIncrement();
		mutexLock.ReleaseMutex();
	}

	private static void  SemaphoreProtecetedIncrement() {

		semaphoreLock.WaitOne();
		UnprotectedIncrement();
		semaphoreLock.Release();
	}

	private static void AutoResetEventProtecetedIncrement() {

		autoResetEventLock.WaitOne();
		UnprotectedIncrement();
		autoResetEventLock.Set();
	}

	//
	// Running test flag.
	//

	private static volatile bool running = true;

	//
	// The increment shared counter thread(s).
	//

	private static void IncrementThread(object incPrivateObj) {
		Action incPrivate = (Action)incPrivateObj;
		do {
		
			//
			// Choose a way to increment the shared counter.
			//
	
			//UnprotectedIncrement();
			//AtomicIncrement();
			//AtomicIncrementUsingCAS();
			SpinLockProtectedIncrement();
			//MySpinLockProtectedIncrement();
			//IntrinsicLockProtecetedIncrement();
			//ReaderWriterLockProtecetedIncrement();
			//ReaderWriterSlimLockProtecetedIncrement();
			//MutexProtecetedIncrement();
			//SemaphoreProtecetedIncrement();
			//AutoResetEventProtecetedIncrement();

			//
			// Increment the private counter using the action delegate.
			//

			incPrivate();		
		} while(running);
	}

	//
	// Array to store the private counters on the same cache line
	// (ARRAY_SIZE = 2) or in different cache lines (ARRAY_SIZE > 16).
	//
	
	private static int ARRAY_SIZE = 16;
	private static int FIRST_IDX = 0;
	private static int SECOND_IDX = ARRAY_SIZE - 1;
	private static int[] privateCounters = new int[ARRAY_SIZE];
	
		
	//
	// The primary thread.
	//
	
	private static void Main() {
	
		//
		// Sets the primary thread priority to highest to ensure that,
		// when ready, it preempts one of the other threads.
		//
	
		Thread.CurrentThread.Priority = ThreadPriority.Highest;

		//
		// Create the increment thread(s) and sleep for a while.
		//
	
		Thread thrinc1 = new Thread(IncrementThread);
		Action inc1 = delegate() { privateCounters[FIRST_IDX]++; };
		thrinc1.Start(inc1);

#if (TWO_INC_THREADS)
	
		Thread thrinc2 = new Thread(IncrementThread);
		Action inc2 = delegate() { privateCounters[SECOND_IDX]++; };
		thrinc2.Start(inc2);
#endif
	
		//
		// Run increment threads for 5 seconds.
		//
		
#if (TWO_INC_THREADS)		
		Console.WriteLine("--run the increment threads for 3 seconds");
#else
		Console.WriteLine("--run the increment thread for 3 seconds");
#endif
	
		int start = Environment.TickCount;
		
		Thread.Sleep(3000);
	
		//
		// Clear the running flag and synchronize with the termination
		// of the increment threads.
		//
	
		running = false;
		int elapsed = Environment.TickCount - start;
		
		thrinc1.Join();
		
#if (TWO_INC_THREADS)
		thrinc2.Join();
#endif
	
		//
		// Show the results.
		//
	
		Console.WriteLine("\n--shared counter: {0} K, private counters: {1} K, diff: {2}\n--increment cost: {3} ns",
		   sharedCounter >> 10,
		   (privateCounters[FIRST_IDX] + privateCounters[SECOND_IDX]) >> 10,
		   (privateCounters[FIRST_IDX] + privateCounters[SECOND_IDX]) - sharedCounter,
		   (int)((elapsed * 1000000.0) / sharedCounter));
	}
}
