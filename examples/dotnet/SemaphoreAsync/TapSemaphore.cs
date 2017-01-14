using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SemaphoreAsync
{
    public class TapSemaphore
    {
        private readonly object _lock = new object();
        private int currentPermits;
        private readonly int maxPermits;
        private readonly DListNode waiters = new DListNode();

        public TapSemaphore(int initial, int maximum)
        {
            if (initial < 0 || initial > maximum)
            {
                throw new ArgumentOutOfRangeException("initial");
            }
            if (maximum <= 0)
            {
                throw new ArgumentOutOfRangeException("maximum");
            }
            maxPermits = maximum;
            currentPermits = initial;
            DListNode.InitializeList(waiters);
        }

        public TapSemaphore(int initial) : this(initial, Int32.MaxValue) { }

        public Task<bool> WaitAsyncEx(int timeout, CancellationToken ctk)
        {
            lock (_lock)
            {
                if (currentPermits > 0)
                {
                    currentPermits -= 1;
                    return TrueTask;
                }
                // the current thread must block, so check immediate cancelers
                if (timeout == 0)
                {
                    return FalseTask;
                }
                ctk.ThrowIfCancellationRequested();

                var waiter = new Waiter(ctk);
                DListNode.AddLast(waiters, waiter);
                // enable timeout or cancellation only after the node is in the list
                if (ctk.CanBeCanceled)
                {
                    waiter.ctkReg = ctk.Register(OnCancellation, waiter);
                }
                if(timeout != Timeout.Infinite)
                {
                    waiter.timer = new Timer(OnTimeout, waiter, timeout, Timeout.Infinite);
                }                
                return waiter.tcs.Task;
            }
        }

        public void OnCancellation(object state)
        {
            var waiter = (Waiter)state;
            if (waiter.TrySetState(CANCELED))
            {
                if (DListNode.IsInList(waiter))
                {
                    lock (_lock)
                    {
                        DListNode.RemoveIfInserted(waiter);
                    }
                }
                if(waiter.timer != null)
                {
                    waiter.timer.Dispose();
                }
                waiter.tcs.SetCanceled();
            }
        }

        public void OnTimeout(object state)
        {
            var waiter = (Waiter)state;
            if (waiter.TrySetState(CANCELED))
            {
                if (DListNode.IsInList(waiter))
                {
                    lock (_lock)
                    {
                        DListNode.RemoveIfInserted(waiter);
                    }
                }
                if (waiter.ctkReg != null)
                {
                    waiter.ctkReg.Dispose();
                }
                waiter.timer.Dispose();
                waiter.tcs.SetResult(false);
            }
        }

        public void Release()
        {
            Waiter toFinish = null;
            lock (_lock)
            {
                if (currentPermits + 1 > maxPermits)
                {
                    throw new ArgumentOutOfRangeException("releases");
                }
                currentPermits += 1;
                Waiter waiter;
                while ((waiter = (Waiter)DListNode.FirstEntry(waiters)) != null)
                {
                    if (Volatile.Read(ref waiter.state) != WAITING)
                    {
                        DListNode.RemoveIfInserted(waiter);
                        continue;
                    }                    
                    if (waiter.TrySetState(SUCCESS))
                    {
                        DListNode.RemoveIfInserted(waiter);
                        currentPermits -= 1;
                        toFinish = waiter;
                        break;                        
                    }
                }
            }
            // finish it outside the lock
            if (toFinish != null)
            {
                // cancel the active cancelers
                if (toFinish.ctkReg != null)
                {
                    toFinish.ctkReg.Dispose();
                }
                if (toFinish.timer != null)
                {
                    toFinish.timer.Dispose();
                }
                toFinish.tcs.SetResult(true);                                
            }
        }

        // asynchronous unconditional acquire
        public Task<bool> WaitAsync()
        {
            return WaitAsyncEx(Timeout.Infinite, CancellationToken.None);
        }

        // asynchronous acquire, activating a timeout
        public Task<bool> WaitAsync(int timeout)
        {
            return WaitAsyncEx(timeout, CancellationToken.None);
        }

        // asynchronous acquire, activating cancellation
        public Task<bool> WaitAsync(CancellationToken ctk)
        {
            return WaitAsyncEx(Timeout.Infinite, ctk);
        }

        // asynchronous acquire, activating timeout and cancellation
        public Task<bool> WaitAsync(int timeout, CancellationToken ctk)
        {
            return WaitAsyncEx(timeout, ctk);
        }

        // A pre-completed task with Result == false
        private readonly static Task<bool> FalseTask = Task<bool>.FromResult(false);
        // A pre-completed task with Result == true
        private readonly static Task<bool> TrueTask = Task<bool>.FromResult(true);

        // the states of a acquire request
        private const int WAITING = 0;
        private const int CANCELED = 1;
        private const int TIMED_OUT = 2;
        private const int INTERRUPTED = 3;
        private const int SUCCESS = 4;

        public class Waiter : DListNode
        {
            public int state;

            public readonly CancellationToken ctk;
            public readonly TaskCompletionSource<bool> tcs;
            public CancellationTokenRegistration ctkReg;
            public Timer timer;

            public Waiter(CancellationToken ctk)
            {
                state = WAITING;
                this.ctk = ctk;
                tcs = new TaskCompletionSource<bool>();
            }

            public bool TrySetState(int newState)
            {
                return Interlocked.CompareExchange(ref state, newState, WaiterState.WAITING) == WaiterState.WAITING; 
            }
        }
    }   
}
