using System;
using System.Threading;

public static class MonitorEx {
	
	// Acquire an object's lock ignoring interrupts.
	// Through its out parameter this method informs the caller if the current
	// thread was interrupted while it was trying to acquire the lock.
	
	public static void EnterUninterruptibly(object mlock, out bool interrupted) {
		interrupted = false;
		do {
			try {
				Monitor.Enter(mlock);
				break;
			} catch (ThreadInterruptedException) {
				interrupted = true;
			}
		} while (true);
	}
	
	// This method waits on a specific condition of a multi-condition monitor.
	//
	// This method is called with "mlock" locked and the condition's lock unlocked.
	// On return, the same conditions are meet: "mlock" locked and the condition's lock unlocked.
	
	public static void Wait(object mlock, object condition, int timeout) {
		// if the mlock and condition are the same object, we just call Monitor.Wait on "mlock"
		if (mlock == condition) {
			Monitor.Wait(mlock, timeout);
			return;
		}
		
		// if the "mlock" and "condition" are different objects, we need to release the "mlock"'s
		// lock before wait on condition's monitor.
		//
		// first, we acquire lock on "condition" object before release the lock on mlock,
		// to prevent the loss of notifications.
		// if a ThreadInterruptException is thrown, we return the exception with the "mlock"
		// locked. We considerer this case as the exception was thrown by the
		// the method Monitor.Wait(condition).
		
		Monitor.Enter(condition);
		
		// release the mlock's lock and wait on condition’s monitor condition
		Monitor.Exit(mlock);
		try {
			// wait on the condition monitor
			Monitor.Wait(condition, timeout);
		} finally {
			// release the condition’s lock
			Monitor.Exit(condition);
			
			// re-acquire the mlock's lock uninterruptibly
			bool interrupted;
			EnterUninterruptibly(mlock, out interrupted);
			// if the thread was interrupted while trying to acquire the mlock, we consider that
			// it was interrupted when in the wait state, so, we throw  ThreadInterruptedException.
			if (interrupted)
				throw new ThreadInterruptedException();
		}
	}
	
	// This method wait on a specific condition of a monitor without timeout.
	public static void Wait(object mlock, object condition) {
		Wait(mlock, condition, Timeout.Infinite);
	}
	
	// This method notifies one thread that called Wait using the same mlock and condition objects.
	//
	// This method is called with the mlock's lock held, and returns under the same conditions
	public static void Pulse(object mlock, object condition) {
		// if mlock and condition refers to the same object, we just call Monitor.Pulse on mlock.
		if (mlock == condition) {
			Monitor.Pulse(mlock);
			return;
		}
		// If mlock and condition refer to different objects, in order to call Monitor.Pulse on
		// condition we need to acquire condition's lock. We must acquire this lock ignoring the
		// ThreadInterruptedExceptions, because this method in not used for wait purposes,
		// so it must not throw ThreadInterruptedException.
		bool interrupted;
		EnterUninterruptibly(condition, out interrupted);
		// Notify the condition object and leave the corresponding monitor.
		Monitor.Pulse(condition);
		Monitor.Exit(condition);
		// if the current thread was interrupted, we re-assert the interrupt, so the exception
		// will be raised on the next call to a wait method.
		if (interrupted)
			Thread.CurrentThread.Interrupt();
	}
	
	// This method notifies all threads that called Wait using the same mlock and condition objects.
	//
	// This method is called with the mlock's lock held, and returns under the same conditions
	
	public static void PulseAll(object mlock, object condition) {
		// if mlock and condition refer to the same object, we just call Monitor.PulseAll on "mlock".
		if (mlock == condition) {
			Monitor.PulseAll(mlock);
			return;
		}
		// If mlock and condition refer to different objects, in order to call Monitor.PulseAll
		// on condition, we need to hold the condition's lock.
		// We must acquire the condition's lock ignoring the ThreadInterruptedExceptions, because
		// this method is not used for wait purposes, so it must not throw
		// ThreadInterruptedException.
		bool interrupted;
		EnterUninterruptibly(condition, out interrupted);
		
		// notify all threads waiting on the condition and leave the condition object's monitor
		Monitor.PulseAll(condition);
		Monitor.Exit(condition);
		// In case of interrupt, we re-assert the interrupt, so the exception can be raised on
		// the next wait.
		if (interrupted)
			Thread.CurrentThread.Interrupt();
	}
}
