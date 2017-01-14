/**
 *  ISEL, LEIC, Concurrent Programming
 *
 *  An auxiliary type to support timeout adjustments.
 *
 *  Carlos Martins, November 2016
 *
 **/


using System;
using System.Threading;

public struct TimeoutHolder {
	private int timeout;
	private int refTime;
	
	public TimeoutHolder(int timeout) {
		this.timeout = timeout;
		this.refTime = timeout != Timeout.Infinite ? Environment.TickCount : 0;
	}
	
	// returns the remaining timeout
	public int Value {
		get {
			if (timeout != Timeout.Infinite && timeout != 0) {
				int now = Environment.TickCount;
				if (now != refTime) {
					int elapsed = now - refTime;
					refTime = now;
					timeout = elapsed < timeout ? timeout - elapsed : 0;
				}
			}
			return timeout;
		}
	}
}
