/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * A generic IAsyncResult implementation
 *
 * To generate a library with this class execute the following command:
 *	csc /target:library /out:generic-async-result.dll generic-async-result.cs
 * or, it can be built with file-copy.cs execute:
 *  nmake
 * to clean all generated file, execute:
 *	nmake clean
 *
 * Carlos Martins, May 2016
 *
 **/

using System;
using System.Threading;

// Generic IAsyncResult implementation 

public class GenericAsyncResult<R> : IAsyncResult {

	private volatile int resultCalled;
	private volatile bool isCompleted;
	private volatile EventWaitHandle waitEvent;
	private readonly AsyncCallback userCallback;
	private readonly object userState;
	private bool completedSynchronously;
	private R result;
	private Exception error;

		
	public GenericAsyncResult(AsyncCallback ucallback, object ustate, bool synchCompletion) {
		userCallback = ucallback;
		userState = ustate;
		completedSynchronously = synchCompletion;
	}

	public GenericAsyncResult() : this(null, null, false) {}
	
	//
	// Returns a completed instance of GenericAsyncResult<R> with the specified result.
	//
	
	public static IAsyncResult FromResult(AsyncCallback ucallback, object ustate, R result, Exception error,
										  bool synchCompletion) {
		GenericAsyncResult<R> gar = new GenericAsyncResult<R>(ucallback, ustate, synchCompletion);
		gar.OnComplete(result, error);
		return gar;
	}
		
	//
	// Complete the underlying asynchronous operation.
	//
		
	public void OnComplete(R result, Exception error) {	
		this.result = result;
		this.error = error;
		isCompleted = true;
			
		Thread.MemoryBarrier();		// Prevent the release followed by acquire hazard! 
			
		if (waitEvent != null) {
			try {
				waitEvent.Set();
				// We can get ObjectDisposedExcption due a benign race, so ignore it!
			} catch (ObjectDisposedException) {}
		}
		if (userCallback != null)
			userCallback(this);				
	}
	
	//
	// Return the asynchronous operation's result (called once by EndXxx())
	//

#pragma warning disable 420
	
	public R Result {
		get {
			// EndXxx can only be called once!
			if (Interlocked.Exchange(ref resultCalled, 1) != 0)
				throw new InvalidOperationException("EndXxx already called");
			if (!isCompleted)
				AsyncWaitHandle.WaitOne();
			if (waitEvent != null)
				waitEvent.Close();
			if (error != null)
				throw error;
			return result;
		}
	}
		
	//
	// The IAsyncResult interface's implementation.
	//
		
	public bool IsCompleted { get { return isCompleted; } }
		
	public bool CompletedSynchronously { get { return completedSynchronously; } }
		
	public Object AsyncState { get { return userState; } }
		
	public WaitHandle AsyncWaitHandle {
		get {
			if (waitEvent == null) {
				bool completed = isCompleted;
				EventWaitHandle done = new ManualResetEvent(completed);
				if (Interlocked.CompareExchange(ref waitEvent, done, null) == null) {
					if (completed != isCompleted)
						done.Set();
				} else {
					done.Close();		// someone else already the event; so dispose this one!
				}
			}
			return waitEvent;
		}
	}
#pragma warning restore 420
}
