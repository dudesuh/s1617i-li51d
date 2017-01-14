using System;
using System.Threading;
using System.Threading.Tasks;

namespace SemaphoreAsync
{
    public class SemaphoreAsync
    {
        private struct Data
        {
            // nothing to hold, since the semaphore is not n-ary
        }

        private readonly object _lock = new object();
        private int currentPermits;
        private readonly int maxPermits;
        private readonly DListNode waiters = new DListNode();

        public SemaphoreAsync(int initial, int maximum)
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

        public SemaphoreAsync(int initial) : this(initial, Int32.MaxValue) { }

        //---------------------------------------------------------------------
        // Synchronous
        //--
        public bool WaitEx(int timeout, CancellationToken ctk)
        {
            bool interrupted = false;
            Waiter<Data> waiter = null;
            bool ownsTheWaiter = false;
            lock (_lock)
            {
                if (currentPermits > 0)
                {
                    currentPermits -= 1;
                    return true;
                }
                // if the current thread must block, check immediate cancelers
                if (timeout == 0)
                {
                    return false;
                }
                ctk.ThrowIfCancellationRequested();

                // create a synchronous waiter node and insert it in the wait queue
                waiter = new SyncWaiter<Data>(new Data(), ctk, SyncUnlink);
                DListNode.AddLast(waiters, waiter);
                waiter.Enable();

                // wrap timeout value with timeout holder, in order to support timeout adjust			
                TimeoutHolder th = new TimeoutHolder(timeout);

                // wait until the request is staisfied, timeout expires or cancellation
                do
                {
                    try
                    {
                        if ((timeout = th.Value) == 0)
                        {                            
                            ownsTheWaiter = TrySetStateAndRemoveWaiter(waiter, WaiterState.TIMED_OUT);
                            break;
                        }
                        // wait on the monitor's condition variable
                        MonitorEx.Wait(_lock, waiter, timeout);
                    }
                    catch (ThreadInterruptedException)
                    {
                        interrupted = true;
                        ownsTheWaiter = TrySetStateAndRemoveWaiter(waiter, WaiterState.INTERRUPTED);
                        break;
                    }
                } while (waiter.State == WaiterState.WAITING);
            }

            // this processing is private of the current thread, so we can do it without
            // owning the lock.
            if (ownsTheWaiter)
            {
                waiter.CancelCancellation();
            }
            if (waiter.State == WaiterState.INTERRUPTED)
            {
                throw new ThreadInterruptedException();
            }
            if (interrupted)
            {
                Thread.CurrentThread.Interrupt();
            }
            if (waiter.State == WaiterState.CANCELED)
            {
                throw new OperationCanceledException(ctk);
            }
            return waiter.State == WaiterState.SUCCESS;
        }

        private bool TrySetStateAndRemoveWaiter(Waiter<Data> waiter, int state)
        {
            bool res = waiter.TrySetState(state);
            if (res)
            {
                DListNode.RemoveIfInserted(waiter);
            }
            return res;
        }

        public void Wait()
        {
            WaitEx(Timeout.Infinite, CancellationToken.None);
        }

        public bool Wait(int timeout)
        {
            return WaitEx(timeout, CancellationToken.None);
        }

        public void Wait(CancellationToken ctk)
        {
            WaitEx(Timeout.Infinite, ctk);
        }

        // synchronous acquire activating a timeout and cancellation
        public bool Wait(int timeout, CancellationToken ctk)
        {
            return WaitEx(timeout, ctk);
        }

        //---------------------------------------------------------------------
        // TAP
        //--

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

                var waiter = new TapWaiter<Data>(new Data(), ctk, timeout, AsyncUnlink);
                DListNode.AddLast(waiters, waiter);
                waiter.Enable();
                return waiter.Task;
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

        //---------------------------------------------------------------------
        // APM
        //--
        public IAsyncResult BeginWaitEx(int timeout, CancellationToken ctk,
                                   AsyncCallback ucb, object ustate)
        {
            bool result = true;
            lock (_lock)
            {
                if (currentPermits > 0)
                {
                    currentPermits -= 1;
                }
                else if (timeout == 0 || ctk.IsCancellationRequested)
                {
                    result = false;
                }
                else
                {
                    var waiter = new ApmWaiter<Data>(new Data(), ucb, ustate, ctk, timeout, AsyncUnlink);
                    DListNode.AddLast(waiters, waiter);
                    waiter.Enable();
                    return waiter.AsyncResult;
                }
            }
            // FromResult will invoke the async callbacks, so do it outside of the lock
            if (result)
            {
                return GenericAsyncResult<bool>.FromResult(ucb, ustate, true, null, true);
            }
            // synchronous completion due to timeout or cancellation
            return ctk.IsCancellationRequested ?
                GenericAsyncResult<bool>.FromResult(ucb, ustate, false, new OperationCanceledException(ctk), true) :
                GenericAsyncResult<bool>.FromResult(ucb, ustate, false, null, true);
        }

        public bool EndWaitEx(IAsyncResult asyncResult)
        {
            GenericAsyncResult<bool> gbar = (GenericAsyncResult<bool>)asyncResult;
            return gbar.Result;
        }

        public void Release()
        {
            Waiter<Data> toFinish = null;
            lock (_lock)
            {
                if (currentPermits + 1 > maxPermits)
                {
                    throw new ArgumentOutOfRangeException("releases");
                }
                currentPermits += 1;
                Waiter<Data> waiter;
                while ((waiter = (Waiter<Data>)DListNode.FirstEntry(waiters)) != null)
                {
                    if (Volatile.Read(ref waiter.state) != WaiterState.WAITING)
                    {
                        // Shouldn't already be on the queue, ensure it is removed
                        DListNode.RemoveIfInserted(waiter);
                        continue;
                    }
                    if (waiter.TrySetStateAndUnlink(WaiterState.SUCCESS))
                    {
                        currentPermits -= 1;
                        // queue node for further processing outside the lock
                        waiter.prev = toFinish;
                        toFinish = waiter;
                        break;
                    }
                }
            }
            // finish the stack of satisfied requests
            while (toFinish != null)
            {
                toFinish.OnSuccess();
                toFinish = (Waiter<Data>)toFinish.prev;
            }
        }

        private void SyncUnlink(DListNode waiter)
        {            
            lock (_lock)
            {
                DListNode.RemoveIfInserted(waiter);
                MonitorEx.PulseAll(_lock, waiter);
            }            
        }
        private void AsyncUnlink(DListNode waiter)
        {            
            lock (_lock)
            {
                DListNode.RemoveIfInserted(waiter);
            }            
        }

        // A pre-completed task with Result == false
        private readonly static Task<bool> FalseTask = Task<bool>.FromResult(false);
        // A pre-completed task with Result == true
        private readonly static Task<bool> TrueTask = Task<bool>.FromResult(true);
    }

    public static class WaiterState
    {
        public const int WAITING = 0;
        public const int CANCELED = 1;
        public const int TIMED_OUT = 2;
        public const int INTERRUPTED = 3;
        public const int SUCCESS = 4;
    }

    public abstract class Waiter<TData> : DListNode
    {
        public int state;

        private readonly TData data;
        private readonly CancellationToken ctk;
        private CancellationTokenRegistration ctkReg;
        private Timer timer;
        private readonly Action<Waiter<TData>> unlink;
        private readonly int timeout;

        public int State { get { return state; } }
        public TData Data { get { return data; } }        

        public Waiter(TData data, CancellationToken ctk, int timeout, Action<Waiter<TData>> unlink)
        {
            this.data = data;
            this.ctk = ctk;
            this.timeout = timeout;
            this.unlink = unlink;            
        }

        public void Enable()
        {
            if (ctk.CanBeCanceled)
            {
                ctkReg = ctk.Register(this.HandleCancellation);
            }
            if (timeout != Timeout.Infinite)
            {
                timer = new Timer(this.HandleTimeout, null, timeout, Timeout.Infinite);
            }
        }

        private void HandleTimeout(object _)
        {
            if (TrySetState(WaiterState.TIMED_OUT))
            {
                unlink(this);
                timer.Dispose();
                if (ctkReg != null)
                {
                    ctkReg.Dispose();
                }
                this.DoCompleteWithTimeout();
            }
        }

        private void HandleCancellation()
        {
            if (TrySetState(WaiterState.CANCELED))
            {
                unlink(this);
                if (timer != null)
                {
                    timer.Dispose();
                }
                this.DoCompleteWithCancellation(ctk);
            }
        }

        public void OnSuccess()
        {
            if (ctkReg != null)
            {
                ctkReg.Dispose();
            }
            if (timer != null)
            {
                timer.Dispose();
            }
            DoCompleteWithSuccess();
        }

        public bool TrySetState(int newState)
        {
            int s;
            do
            {
                if ((s = Volatile.Read(ref state)) != WaiterState.WAITING)
                {
                    return false;
                }
            } while (Interlocked.CompareExchange(ref state, newState, WaiterState.WAITING) != WaiterState.WAITING);
            return true;
        }

        public bool TrySetStateAndUnlink(int newState)
        {
            var res = TrySetState(newState);
            if (res)
            {
                unlink(this);
            }
            return res;
        }

        public void CancelCancellation()
        {
            if (ctk.CanBeCanceled)
            {
                ctkReg.Dispose();
            }
        }

        protected abstract void DoCompleteWithCancellation(CancellationToken ctk);
        protected abstract void DoCompleteWithTimeout();
        protected abstract void DoCompleteWithSuccess();
    }

    public class SyncWaiter<TData> : Waiter<TData>
    {
        public SyncWaiter(TData data, CancellationToken ctk, Action<Waiter<TData>> unlink)
            : base(data, ctk, Timeout.Infinite, unlink)
        {

        }

        protected override void DoCompleteWithCancellation(CancellationToken ctk)
        {
            // nothing to do, monitor pulse was done on unlink
        }

        protected override void DoCompleteWithTimeout()
        {
            // nothing to do, monitor pulse was done on unlink
        }

        protected override void DoCompleteWithSuccess()
        {
            // nothing to do, monitor pulse was done on unlink
        }
    }

    public class ApmWaiter<TData> : Waiter<TData>
    {
        public GenericAsyncResult<bool> AsyncResult { get; private set; }

        public ApmWaiter(TData data, AsyncCallback ucb, object ustate, CancellationToken ctk, int timeout, Action<Waiter<TData>> unlink)
            : base(data, ctk, timeout, unlink)
        {
            AsyncResult = new GenericAsyncResult<bool>(ucb, ustate, synchCompletion: false);
        }

        protected override void DoCompleteWithCancellation(CancellationToken ctk)
        {
            AsyncResult.OnComplete(false, new OperationCanceledException(ctk));
        }

        protected override void DoCompleteWithTimeout()
        {
            AsyncResult.OnComplete(false, null);
        }

        protected override void DoCompleteWithSuccess()
        {
            AsyncResult.OnComplete(true, null);
        }
    }

    public class TapWaiter<TData> : Waiter<TData>
    {
        private readonly TaskCompletionSource<bool> tcs;

        public Task<bool> Task { get { return tcs.Task; } }

        public TapWaiter(TData data, CancellationToken ctk, int timeout, Action<Waiter<TData>> unlink)
            : base(data, ctk, timeout, unlink)
        {
            tcs = new TaskCompletionSource<bool>();
        }

        protected override void DoCompleteWithCancellation(CancellationToken ctk)
        {
            tcs.SetCanceled();
        }

        protected override void DoCompleteWithTimeout()
        {
            tcs.TrySetResult(false);
        }

        protected override void DoCompleteWithSuccess()
        {
            tcs.SetResult(true);
        }
    }
}
