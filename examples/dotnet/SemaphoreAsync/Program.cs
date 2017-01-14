#define USE_OUR_SEMAPHORE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace SemaphoreAsync
{
    internal class SemaphoreTests
    {

        // test semaphore as a mutual exclusion lock using synchronous acquires
        private static bool TestSemaphoreAsLock()
        {

            const int SETUP_TIME = 50;
            const int RUN_TIME = 10 * 1000;
            int THREADS = 20;
            const int MIN_TIMEOUT = 0;
            const int MAX_TIMEOUT = 10;
            const int MIN_CANCEL_INTERVAL = 10;
            const int MAX_CANCEL_INTERVAL = 50;

            Thread[] tthrs = new Thread[THREADS];
            int[] privateCounters = new int[THREADS];
            int[] timeouts = new int[THREADS];
            int[] cancellations = new int[THREADS];
            int[] issuedInterrupts = new int[THREADS];
            int[] sensedInterrupts = new int[THREADS];
            int sharedCounter = 0;
            bool exit = false;
            ManualResetEventSlim start = new ManualResetEventSlim();
            // the semaphore
#if USE_OUR_SEMAPHORE
		var _lock = new SemaphoreAsync(1, 1);	// our semaphore
#else
            SemaphoreSlim _lock = new SemaphoreSlim(1);         // BCL semaphore
#endif
            //
            // Create and start acquirer/releaser threads
            //

            for (int i = 0; i < THREADS; i++)
            {
                int tid = i;
                tthrs[i] = new Thread(() => {
                    Random rnd = new Random(Thread.CurrentThread.ManagedThreadId);
                    start.Wait();
                    CancellationTokenSource cts =
                         new CancellationTokenSource(rnd.Next(MIN_CANCEL_INTERVAL, MAX_CANCEL_INTERVAL));
                    do
                    {
                        do
                        {
                            try
                            {
                                if (_lock.Wait(rnd.Next(MIN_TIMEOUT, MAX_TIMEOUT), cts.Token))
                                    break;
                                timeouts[tid]++;
                            }
                            catch (OperationCanceledException)
                            {
                                cancellations[tid]++;
                                cts.Dispose();
                                cts = new CancellationTokenSource(rnd.Next(MIN_CANCEL_INTERVAL, MAX_CANCEL_INTERVAL));
                            }
                            catch (ThreadInterruptedException)
                            {
                                sensedInterrupts[tid]++;
                            }
                        } while (true);
                        sharedCounter++;
                        if (THREADS > 1)
                        {
                            if (rnd.Next(100) < 95)
                            {
                                Thread.Yield();
                            }
                            else
                            {
                                try
                                {
                                    Thread.Sleep(rnd.Next(MIN_TIMEOUT, MAX_TIMEOUT));
                                }
                                catch (ThreadInterruptedException)
                                {
                                    sensedInterrupts[tid]++;
                                }
                            }
                        }
                        // release the lock filtering interrupts
                        do
                        {
                            try
                            {
                                _lock.Release();
                                break;
                            }
                            catch (ThreadInterruptedException)
                            {
                                sensedInterrupts[tid]++;
                            }
                        } while (true);
                        privateCounters[tid]++;
                        if (THREADS > 1)
                        {
                            try
                            {
                                if ((privateCounters[tid] % 100) == 0)
                                    Console.Write("[#{0:D2}]", tid);
                            }
                            catch (ThreadInterruptedException)
                            {
                                sensedInterrupts[tid]++;
                            }
                        }
                    } while (!Volatile.Read(ref exit));
                    try
                    {
                        Thread.Sleep(100);
                    }
                    catch (ThreadInterruptedException)
                    {
                        sensedInterrupts[tid]++;
                    }
                });
                tthrs[i].Start();
            }
            Thread.Sleep(SETUP_TIME);
            Stopwatch sw = Stopwatch.StartNew();
            start.Set();
            Random grnd = new Random(Thread.CurrentThread.ManagedThreadId);
            int endTime = Environment.TickCount + RUN_TIME;
            //...
            do
            {
                Thread.Sleep(grnd.Next(100));
                if (THREADS > 1)
                {
                    var tid = grnd.Next(THREADS);
                    tthrs[tid].Interrupt();
                    issuedInterrupts[tid]++;
                }
                if (Console.KeyAvailable)
                {
                    Console.Read();
                    break;
                }
            } while (Environment.TickCount < endTime);
            Volatile.Write(ref exit, true);
            int sharedSnapshot = Volatile.Read(ref sharedCounter);
            sw.Stop();
            // Wait until all threads have been terminated.
            for (int i = 0; i < THREADS; i++)
                tthrs[i].Join();

            // Compute results

            Console.WriteLine("\nPrivate counters:");
            int sum = 0, sumInterrupts = 0, totalIssuedInterrupts = 0;
            for (int i = 0; i < THREADS; i++)
            {
                sum += privateCounters[i];
                sumInterrupts += sensedInterrupts[i];
                totalIssuedInterrupts += issuedInterrupts[i];
                if (i != 0 && (i % 3) == 0)
                {
                    Console.WriteLine();
                }
                else if (i != 0)
                {
                    Console.Write(' ');
                }
                Console.Write("[#{0:D2}: {1}/{2}/{3}/{4}]", i,
                     privateCounters[i], timeouts[i], cancellations[i], sensedInterrupts[i]);
            }
            Console.WriteLine("\n--shared/private: {0}/{1}", sharedCounter, sum);
            Console.WriteLine("--interrupts issuded/sensed: {0}/{1}", totalIssuedInterrupts, sumInterrupts);
            long unitCost = (sw.ElapsedMilliseconds * 1000000L) / sharedSnapshot;

            Console.Write("--time per acquisition/release: {0} {1}",
                         unitCost >= 1000 ? unitCost / 1000 : unitCost,
                         unitCost >= 1000 ? "us" : "ns");
            for(var i = 0; i<THREADS; ++i)
            {
                Console.WriteLine($"issued:{issuedInterrupts[i]}, sensed:{sensedInterrupts[i]}");
            }
            return sum == sharedCounter && totalIssuedInterrupts == sumInterrupts;
        }

        private static void Cancel(CancellationTokenSource cts)
        {
            cts.Cancel();
        }

        // test semaphore as a mutual exclusion lock using asynchronous acquires
        private static bool TestSemaphoreAsLockAsync()
        {

            const int SETUP_TIME = 50;
            const int RUN_TIME = 5 * 1000;
            int THREADS = 20;
            const int MIN_TIMEOUT = 1;
            const int MAX_TIMEOUT = 50;

            Thread[] tthrs = new Thread[THREADS];
            int[] privateCounters = new int[THREADS];
            int[] timeouts = new int[THREADS];
            int[] cancellations = new int[THREADS];
            int sharedCounter = 0;
            bool exit = false;
            ManualResetEventSlim start = new ManualResetEventSlim();

#if USE_OUR_SEMAPHORE
		var _lock = new SemaphoreAsync(1, 1);	// our semaphore
#else
            SemaphoreSlim _lock = new SemaphoreSlim(1);         // BCL semaphore
#endif

            //
            // Create and start acquirer/releaser threads
            //

            for (int i = 0; i < THREADS; i++)
            {
                int tid = i;
                tthrs[i] = new Thread(() => {
                    //Console.WriteLine("->#{0}", tid);
                    start.Wait();
                    Random rnd = new Random(tid);
                    do
                    {
                        do
                        {
                            using (CancellationTokenSource cts = new CancellationTokenSource())
                            {
                                try
                                {
                                    var resultTask = _lock.WaitAsync(rnd.Next(MAX_TIMEOUT), cts.Token);
                                    if (rnd.Next(100) < 10)
                                        cts.Cancel();
                                    if (resultTask.Result)
                                        break;
                                    timeouts[tid]++;
                                }
                                catch (AggregateException ae)
                                {
                                    ae.Handle((e) => {
                                        if (e is TaskCanceledException)
                                        {
                                            cancellations[tid]++;
                                            return true;
                                        }
                                        return false;
                                    });
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("*** Exception type: {0}", ex.GetType());
                                }
                            }
                        } while (true);
                        sharedCounter++;
                        if (THREADS > 1)
                        {
                            if (rnd.Next(100) < 95)
                            {
                                Thread.Yield();
                            }
                            else
                            {
                                Thread.Sleep(rnd.Next(MIN_TIMEOUT, MAX_TIMEOUT));
                            }
                        }
                        privateCounters[tid]++;
                        _lock.Release();
                        if (THREADS > 1 && privateCounters[tid] % 100 == 0)
                            Console.Write("[#{0:D2}]", tid);
                    } while (!Volatile.Read(ref exit));
                    //Console.Write("\n<-#{0}", tid);	
                });
                tthrs[i].Start();
            }

            Thread.Sleep(SETUP_TIME);
            Stopwatch sw = Stopwatch.StartNew();
            start.Set();
            int endTime = Environment.TickCount + RUN_TIME;
            //...
            do
            {
                Thread.Sleep(20);
                if (Console.KeyAvailable)
                {
                    Console.Read();
                    break;
                }
            } while (Environment.TickCount < endTime);
            Volatile.Write(ref exit, true);
            int sharedSnapshot = Volatile.Read(ref sharedCounter);
            sw.Stop();
            // Wait until all threads have been terminated.
            for (int i = 0; i < THREADS; i++)
                tthrs[i].Join();

            // Compute results

            Console.WriteLine("\n\nPrivate counters:");
            int sum = 0;
            for (int i = 0; i < THREADS; i++)
            {
                sum += privateCounters[i];
                if (i != 0 && i % 3 == 0)
                    Console.WriteLine();
                else if (i != 0)
                    Console.Write(' ');
                Console.Write("[#{0:D2}: {1}/{2}/{3}]", i, privateCounters[i], timeouts[i], cancellations[i]);
            }
            Console.WriteLine();
            long unitCost = (sw.ElapsedMilliseconds * 1000000L) / sharedSnapshot;
            Console.WriteLine("--unit cost of acquire/release: {0} {1}",
                                unitCost > 1000 ? unitCost / 1000 : unitCost,
                                unitCost > 1000 ? "us" : "ns");
            return sum == sharedCounter;
        }

        // test semaphore fairness synchronous
        private static bool TestSemaphoreFairness()
        {

            const int SETUP_TIME = 50;
            const int THREADS = 50;
            const int MIN_TIMEOUT = 5;
            const int MAX_TIMEOUT = 20;

            Thread[] tthrs = new Thread[THREADS];
            int[] privateCounters = new int[THREADS];
            ManualResetEventSlim startEvent = new ManualResetEventSlim(false);
            var sem = new SemaphoreAsync(THREADS, THREADS);
            int totalTimeouts = 0;
            bool exit = false;

            for (int i = 0; i < THREADS; i++)
            {
                int tid = i;
                tthrs[i] = new Thread(() => {
                    Random random = new Random(tid);

                    // Wait until start event is set
                    startEvent.Wait();

                    do
                    {
                        do
                        {
                            if (sem.WaitEx(random.Next(MIN_TIMEOUT, MAX_TIMEOUT), CancellationToken.None))
                                break;
                            Interlocked.Increment(ref totalTimeouts);
                        } while (true);
                        Thread.Yield();
                        sem.Release();
                        if ((++privateCounters[tid] % 100) == 0)
                        {
                            Console.Write("[#{0}]", tid);
                        }
                    } while (!Volatile.Read(ref exit));
                });
                tthrs[i].Start();
            }

            // Wait until all test threads have been started and then set the
            // start event.
            Thread.Sleep(SETUP_TIME);
            startEvent.Set();

            do
            {
                Thread.Sleep(50);
            } while (!Console.KeyAvailable);
            Console.Read();
            Volatile.Write(ref exit, true);

            // Wait until all threads have been terminated.
            for (int i = 0; i < THREADS; i++)
                tthrs[i].Join();

            // Show results
            int total = 0;
            Console.WriteLine("\nPrivate counters:");
            for (int i = 0; i < THREADS; i++)
            {
                if (i != 0 && (i % 5) == 0)
                    Console.WriteLine();
                Console.Write("[#{0:D2}:{1,4}]", i, privateCounters[i]);
                total += privateCounters[i];
            }
            Console.WriteLine("\n-- total acquisitions/releases: {0}, timeouts: {1}", total, totalTimeouts);
            return true;
        }

        // test semaphore fairness asynchronous
        private static bool TestSemaphoreFairnessAsync()
        {

            const int SETUP_TIME = 50;
            const int THREADS = 50;
            const int MIN_TIMEOUT = 5;
            const int MAX_TIMEOUT = 20;

            Thread[] tthrs = new Thread[THREADS];
            int[] privateCounters = new int[THREADS];
            ManualResetEventSlim startEvent = new ManualResetEventSlim(false);
            var sem = new SemaphoreAsync(THREADS, THREADS);
            int totalTimeouts = 0;
            bool exit = false;

            for (int i = 0; i < THREADS; i++)
            {
                int tid = i;
                tthrs[i] = new Thread(() => {
                    Random random = new Random(tid);

                    // Wait until start event is set
                    startEvent.Wait();

                    do
                    {
                        do
                        {
                            if (sem.WaitAsyncEx(random.Next(MIN_TIMEOUT, MAX_TIMEOUT),
                                                 CancellationToken.None).Result)
                                break;
                            Interlocked.Increment(ref totalTimeouts);
                        } while (true);
                        Thread.Yield();
                        sem.Release();
                        if ((++privateCounters[tid] % 100) == 0)
                        {
                            Console.Write("[#{0}]", tid);
                        }
                    } while (!Volatile.Read(ref exit));
                });
                tthrs[i].Start();
            }

            // Wait until all test threads have been started and then set the
            // start event.
            Thread.Sleep(SETUP_TIME);
            startEvent.Set();

            do
            {
                Thread.Sleep(50);
            } while (!Console.KeyAvailable);
            Console.Read();
            Volatile.Write(ref exit, true);

            // Wait until all threads have been terminated.
            for (int i = 0; i < THREADS; i++)
                tthrs[i].Join();

            // Show results
            int total = 0;
            Console.WriteLine("\nPrivate counters:");
            for (int i = 0; i < THREADS; i++)
            {
                if (i != 0 && (i % 5) == 0)
                    Console.WriteLine();
                Console.Write("[#{0:D2}:{1,4}]", i, privateCounters[i]);
                total += privateCounters[i];
            }
            Console.WriteLine("\n-- total acquisitions/releases: {0}, timeouts: {1}", total, totalTimeouts);
            return true;
        }

        //---
        // a blocking queue with synchronous and asynchronous TAP & APM interfaces
        //---

        internal class BlockingQueue_<T> where T : class
        {
            private readonly int queueSize;
            private readonly T[] room;
            private readonly SemaphoreAsync freeSlots, filledSlots;
            private int putIdx, takeIdx;

            // construct the blocking queue
            public BlockingQueue_(int size)
            {
                queueSize = size;
                room = new T[size];
                putIdx = takeIdx = 0;
                freeSlots = new SemaphoreAsync(size, size);
                filledSlots = new SemaphoreAsync(0, size);
            }

            //---
            // synchronous interface
            //---

            // put activating a timeout
            public bool Put(T item, int timeout)
            {
                if (!freeSlots.Wait(timeout))
                    return false;       // time out
                lock (room)
                {
                    room[putIdx++ % queueSize] = item;
                }
                filledSlots.Release();
                return true;
            }

            // unconditional put
            public void Put(T item)
            {
                Put(item, Timeout.Infinite);
            }

            // take activating a timeout
            public T Take(int timeout)
            {
                if (!filledSlots.Wait(timeout))
                    return null;        // time out
                T item;
                lock (room)
                {
                    item = room[takeIdx++ % queueSize];
                }
                freeSlots.Release();
                return item;
            }

            // unconditional take
            public T Take()
            {
                return Take(Timeout.Infinite);
            }

            //---
            // asynchronous TAP interface
            //--

            // put activating a timeout
            public async Task<bool> PutAsync(T item, int timeout)
            {
                if (!await freeSlots.WaitAsync(timeout))
                    return false;       // time out
                lock (room)
                {
                    room[putIdx++ % queueSize] = item;
                }
                filledSlots.Release();
                return true;
            }

            // unconditional put
            public async Task PutAsync(T item)
            {
                await PutAsync(item, Timeout.Infinite);
            }

            // take activating a timeout
            public async Task<T> TakeAsync(int timeout)
            {
                if (!await filledSlots.WaitAsync(timeout))
                    return null;        // time out
                T item;
                lock (room)
                {
                    item = room[takeIdx++ % queueSize];
                }
                freeSlots.Release();
                return item;
            }

            // unconditional take
            public async Task<T> TakeAsync()
            {
                return await TakeAsync(Timeout.Infinite);
            }

            //---
            // asynchronous APM interface
            //---

            // begin put activating a timeout
            public IAsyncResult BeginPut(T item, int timeout, CancellationToken ctk, AsyncCallback ucb, object ustate)
            {

                GenericAsyncResult<bool> gar = new GenericAsyncResult<bool>(ucb, ustate, false);
                freeSlots.BeginWaitEx(timeout, ctk, (ar) => {
                    try
                    {
                        if (!freeSlots.EndWaitEx(ar))
                        {
                            // complete put request with timeout
                            gar.OnComplete(false, null);
                            return;
                        }
                        // wait succeeded
                        lock (room)
                        {
                            room[putIdx++ % queueSize] = item;
                        }
                        filledSlots.Release();
                        // complete put request with success
                        gar.OnComplete(true, null);
                    }
                    catch (Exception ex)
                    {
                        // complete put request with exception
                        gar.OnComplete(false, ex);
                    }
                }, null);
                return gar;
            }

            // unconditional begin put
            public IAsyncResult BeginPut(T item, AsyncCallback ucb, object ustate)
            {
                return BeginPut(item, Timeout.Infinite, CancellationToken.None, ucb, ustate);
            }

            // end  put
            public bool EndPut(IAsyncResult asyncResult)
            {
                GenericAsyncResult<bool> gar = (GenericAsyncResult<bool>)asyncResult;
                return gar.Result;
            }

            // begin take activating a timeout
            public IAsyncResult BeginTake(int timeout, CancellationToken ctk, AsyncCallback ucb, object ustate)
            {
                GenericAsyncResult<T> gar = new GenericAsyncResult<T>(ucb, ustate, false);
                filledSlots.BeginWaitEx(timeout, ctk, (ar) => {
                    try
                    {
                        if (!filledSlots.EndWaitEx(ar))
                        {
                            // complete request due to timeout
                            gar.OnComplete(null, null);
                            return;
                        }
                        // complete request with success 
                        T item;
                        lock (room)
                        {
                            item = room[takeIdx++ % queueSize];
                        }
                        freeSlots.Release();
                        gar.OnComplete(item, null);
                    }
                    catch (Exception ex)
                    {
                        gar.OnComplete(null, ex);
                    }
                }, null);
                return gar;
            }

            // unconditional begin take
            public IAsyncResult BeginTake(AsyncCallback ucb, object ustate)
            {
                return BeginTake(Timeout.Infinite, CancellationToken.None, ucb, ustate);
            }

            // end  take
            public T EndTake(IAsyncResult asyncResult)
            {
                GenericAsyncResult<T> gar = (GenericAsyncResult<T>)asyncResult;
                return gar.Result;
            }

            // returns the number of filled slots
            public int Count
            {
                get
                {
                    lock (room)
                    {
                        return putIdx - takeIdx;
                    }
                }
            }

        }

        // use semaphore in a producer/consumer context using asynchronous TAP acquires
        private static bool TestSemaphoreInATapProducerConsumerContext()
        {

            const int RUN_TIME = 30 * 1000;
            const int EXIT_TIME = 50;
            const int PRODUCER_THREADS = 10;
            const int CONSUMER_THREADS = 20;
            const int QUEUE_SIZE = PRODUCER_THREADS * 4;
            const int MIN_PAUSE_INTERVAL = 10;
            const int MAX_PAUSE_INTERVAL = 100;
            const int PRODUCTION_ALIVE = 100;
            const int CONSUMER_ALIVE = 100;

            Thread[] pthrs = new Thread[PRODUCER_THREADS];
            Thread[] cthrs = new Thread[CONSUMER_THREADS];
            int[] productions = new int[PRODUCER_THREADS];
            int[] consumptions = new int[CONSUMER_THREADS];
            bool exit = false;
            BlockingQueue_<String> queue = new BlockingQueue_<String>(QUEUE_SIZE);

            // Create and start consumer threads.

            for (int i = 0; i < CONSUMER_THREADS; i++)
            {
                int ctid = i;
                cthrs[i] = new Thread(() => {
                    Random rnd = new Random(ctid);
                    do
                    {
                        try
                        {
                            var item = queue.TakeAsync().Result;
                            consumptions[ctid]++;
                        }
                        catch (ThreadInterruptedException)
                        {
                            break;
                        }
                        int sleepTime = 0;
                        if (consumptions[ctid] % CONSUMER_ALIVE == 0)
                        {
                            Console.Write("[#c{0}]", ctid);
                            sleepTime = rnd.Next(MIN_PAUSE_INTERVAL, MAX_PAUSE_INTERVAL);
                        }
                        try
                        {
                            Thread.Sleep(sleepTime);
                        }
                        catch (ThreadInterruptedException)
                        {
                            break;
                        }
                    } while (!Volatile.Read(ref exit));
                });
                cthrs[i].Priority = ThreadPriority.Highest;
                cthrs[i].Start();
            }

            // Create and start producer threads.
            for (int i = 0; i < PRODUCER_THREADS; i++)
            {
                int ptid = i;
                pthrs[i] = new Thread(() => {
                    Random rnd = new Random(ptid);
                    do
                    {
                        try
                        {
                            queue.PutAsync(rnd.Next().ToString()).Wait();
                            productions[ptid]++;
                        }
                        catch (ThreadInterruptedException)
                        {
                            break;
                        }
                        int sleepTime = 0;
                        if (consumptions[ptid] % PRODUCTION_ALIVE == 0)
                        {
                            Console.Write("[#p{0}]", ptid);
                            sleepTime = rnd.Next(MIN_PAUSE_INTERVAL, MAX_PAUSE_INTERVAL);
                        }
                        try
                        {
                            Thread.Sleep(sleepTime);
                        }
                        catch (ThreadInterruptedException)
                        {
                            break;
                        }
                    } while (!Volatile.Read(ref exit));
                });
                pthrs[i].Start();
            }

            // run the test for a while
            int endTime = Environment.TickCount + RUN_TIME;
            do
            {
                Thread.Sleep(50);
                if (Console.KeyAvailable)
                {
                    Console.Read();
                    break;
                }
            } while (Environment.TickCount < endTime);

            Volatile.Write(ref exit, true);
            Thread.Sleep(EXIT_TIME);

            // Wait until all producer have been terminated.
            int sumProductions = 0;
            for (int i = 0; i < PRODUCER_THREADS; i++)
            {
                if (pthrs[i].IsAlive)
                    pthrs[i].Interrupt();
                pthrs[i].Join();
                sumProductions += productions[i];
            }

            int sumConsumptions = 0;
            // Wait until all consumer have been terminated.
            for (int i = 0; i < CONSUMER_THREADS; i++)
            {
                if (cthrs[i].IsAlive)
                {
                    cthrs[i].Interrupt();
                }
                cthrs[i].Join();
                sumConsumptions += consumptions[i];
            }

            // Display consumer results
            Console.WriteLine("\nConsumer counters:");
            for (int i = 0; i < CONSUMER_THREADS; i++)
            {
                if (i != 0 && i % 4 == 0)
                {
                    Console.WriteLine();
                }
                else if (i != 0)
                {
                    Console.Write(' ');
                }
                Console.Write("[#c{0:D2}: {1,4}]", i, consumptions[i]);
            }

            // consider not consumed productions
            sumConsumptions += queue.Count;

            Console.WriteLine("\nProducer counters:");
            for (int i = 0; i < PRODUCER_THREADS; i++)
            {
                if (i != 0 && i % 4 == 0)
                {
                    Console.WriteLine();
                }
                else if (i != 0)
                {
                    Console.Write(' ');
                }
                Console.Write("[#p{0:D2}: {1,4}]", i, productions[i]);
            }
            Console.WriteLine("\n--productions: {0}, consumptions: {1}", sumProductions, sumConsumptions);
            return sumConsumptions == sumProductions;
        }

        // use semaphore in a producer/consumer context using asynchronous APM acquires
        private static bool TestSemaphoreInAApmProducerConsumerContext()
        {

            const int RUN_TIME = 30 * 1000;
            const int EXIT_TIME = 50;
            const int PRODUCER_THREADS = 10;
            const int CONSUMER_THREADS = 20;
            const int QUEUE_SIZE = PRODUCER_THREADS * 4;
            const int MIN_TIMEOUT = 0;
            const int MAX_TIMEOUT = 50;
            const int MIN_CANCEL_INTERVAL = 50;
            const int MAX_CANCEL_INTERVAL = 100;
            const int MIN_PAUSE_INTERVAL = 10;
            const int MAX_PAUSE_INTERVAL = 100;
            const int PRODUCTION_ALIVE = 100;
            const int CONSUMER_ALIVE = 100;

            Thread[] pthrs = new Thread[PRODUCER_THREADS];
            Thread[] cthrs = new Thread[CONSUMER_THREADS];
            int[] productions = new int[PRODUCER_THREADS];
            int[] productionCancellations = new int[PRODUCER_THREADS];
            int[] consumptions = new int[CONSUMER_THREADS];
            int[] consumptionTimeouts = new int[CONSUMER_THREADS];
            bool exit = false;
            BlockingQueue_<String> queue = new BlockingQueue_<String>(QUEUE_SIZE);

            // Create and start consumer threads.

            for (int i = 0; i < CONSUMER_THREADS; i++)
            {
                int ctid = i;
                cthrs[i] = new Thread(() => {
                    Random rnd = new Random(ctid);
                    do
                    {
                        var ar = queue.BeginTake(rnd.Next(MIN_TIMEOUT, MAX_TIMEOUT), CancellationToken.None,
                                                     null, null);
                        try
                        {
                            while (!ar.IsCompleted)
                                Thread.Sleep(10);
                        }
                        catch (ThreadInterruptedException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("***Exception: {0}: {1}", ex.GetType(), ex.Message);
                            break;
                        }
                        finally
                        {
                            if (ar.IsCompleted)
                            {
                                if (queue.EndTake(ar) != null)
                                    consumptions[ctid]++;
                                else
                                    consumptionTimeouts[ctid]++;
                            }
                        }
                        int sleepTime = 0;
                        if (consumptions[ctid] % CONSUMER_ALIVE == 0)
                        {
                            Console.Write("[#c{0}]", ctid);
                            sleepTime = rnd.Next(MIN_PAUSE_INTERVAL, MAX_PAUSE_INTERVAL);
                        }
                        try
                        {
                            Thread.Sleep(sleepTime);
                        }
                        catch (ThreadInterruptedException)
                        {
                            break;
                        }
                    } while (!Volatile.Read(ref exit));
                });
                cthrs[i].Priority = ThreadPriority.Highest;
                cthrs[i].Start();
            }

            // Create and start producer threads.
            for (int i = 0; i < PRODUCER_THREADS; i++)
            {
                int ptid = i;
                pthrs[i] = new Thread(() => {
                    Random rnd = new Random(ptid);
                    CancellationTokenSource cts = new CancellationTokenSource(rnd.Next(MIN_CANCEL_INTERVAL, MAX_CANCEL_INTERVAL));
                    do
                    {
                        using (var done = new ManualResetEventSlim(false))
                        {
                            queue.BeginPut(rnd.Next().ToString(), Timeout.Infinite, cts.Token, (ar) => {
                                try
                                {
                                    queue.EndPut(ar);
                                    productions[ptid]++;
                                }
                                catch (OperationCanceledException)
                                {
                                    productionCancellations[ptid]++;
                                    cts.Dispose();
                                    cts = new CancellationTokenSource(rnd.Next(MIN_CANCEL_INTERVAL, MAX_CANCEL_INTERVAL));
                                }
                                finally
                                {
                                    done.Set();
                                }
                            }, null);
                            try
                            {
                                done.Wait();
                            }
                            catch (ThreadInterruptedException)
                            {
                                break;
                            }
                        }

                        int sleepTime = 0;
                        if (consumptions[ptid] % PRODUCTION_ALIVE == 0)
                        {
                            Console.Write("[#p{0}]", ptid);
                            sleepTime = rnd.Next(MIN_PAUSE_INTERVAL, MAX_PAUSE_INTERVAL);
                        }
                        try
                        {
                            Thread.Sleep(sleepTime);
                        }
                        catch (ThreadInterruptedException)
                        {
                            break;
                        }
                    } while (!Volatile.Read(ref exit));
                });
                pthrs[i].Start();
            }

            // run the test for a while
            int endTime = Environment.TickCount + RUN_TIME;
            do
            {
                Thread.Sleep(50);
                if (Console.KeyAvailable)
                {
                    Console.Read();
                    break;
                }
            } while (Environment.TickCount < endTime);

            Volatile.Write(ref exit, true);
            Thread.Sleep(EXIT_TIME);

            // Wait until all producer have been terminated.
            int sumProductions = 0;
            for (int i = 0; i < PRODUCER_THREADS; i++)
            {
                if (pthrs[i].IsAlive)
                    pthrs[i].Interrupt();
                pthrs[i].Join();
                sumProductions += productions[i];
            }

            int sumConsumptions = 0;
            // Wait until all consumer have been terminated.
            for (int i = 0; i < CONSUMER_THREADS; i++)
            {
                if (cthrs[i].IsAlive)
                {
                    cthrs[i].Interrupt();
                }
                cthrs[i].Join();
                sumConsumptions += consumptions[i];
            }

            // Display consumer results
            Console.WriteLine("\nConsumer counters:");
            for (int i = 0; i < CONSUMER_THREADS; i++)
            {
                if (i != 0 && i % 4 == 0)
                {
                    Console.WriteLine();
                }
                else if (i != 0)
                {
                    Console.Write(' ');
                }
                Console.Write("[#c{0:D2}: {1,4}/{2, 3}]", i, consumptions[i], consumptionTimeouts[i]);
            }

            // consider not consumed productions
            sumConsumptions += queue.Count;

            Console.WriteLine("\nProducer counters:");
            for (int i = 0; i < PRODUCER_THREADS; i++)
            {
                if (i != 0 && i % 4 == 0)
                {
                    Console.WriteLine();
                }
                else if (i != 0)
                {
                    Console.Write(' ');
                }
                Console.Write("[#p{0:D2}: {1,4}/{2, 3}]", i, productions[i], productionCancellations[i]);
            }
            Console.WriteLine("\n--productions: {0}, consumptions: {1}", sumProductions, sumConsumptions);
            return sumConsumptions == sumProductions;
        }

        static void Main()
        {
            //Console.WriteLine("\n-->test semaphore as lock using synchronous acquires: {0}",
            //                      TestSemaphoreAsLock() ? "passed" : "failed");


            //Console.WriteLine("\n-->test semaphore as lock using asynchronous acquires: {0}",
            //                      TestSemaphoreAsLockAsync() ? "passed" : "failed");



            //Console.WriteLine("\n-->test semaphore fairness synchronous: {0}",
            //                  TestSemaphoreFairness() ? "passed" : "failed");


            //Console.WriteLine("\n-->test semaphore fairness asynchronous: {0}",
            //                  TestSemaphoreFairnessAsync() ? "passed" : "failed");

            //Console.WriteLine("\n-->test semaphore in a async TAP producer/consumer context: {0}",
            //                  TestSemaphoreInATapProducerConsumerContext() ? "passed" : "failed");

            //Console.WriteLine("\n-->test semaphore in a async APM producer/consumer context: {0}",
            //                  TestSemaphoreInAApmProducerConsumerContext() ? "passed" : "failed");

            TapQueueTest.Run();

            Console.ReadKey();
        }
    }

}
