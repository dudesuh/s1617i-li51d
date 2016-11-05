package pt.isel.pc;

import org.junit.Test;

import java.util.Random;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

/**
 * Created by pedro on 31/10/16.
 */
public class SemaphoreUsingLocksAndConditionsTests {

    private final long lotsOfTime = 1L << 62;

    @Test
    public void invariant_is_preserved_under_load() throws InterruptedException {

        final int nOfThreads = 100;
        final int nOfReps = 20;
        final int initialUnits = nOfThreads;
        final Random rg = new Random();
        final Thread[] ths = new Thread[nOfThreads];
        final SemaphoreUsingLocksAndConditions s =
                new SemaphoreUsingLocksAndConditions(initialUnits);
        final AtomicInteger units = new AtomicInteger(initialUnits);
        final ConcurrentLinkedQueue<Throwable> eQueue = new ConcurrentLinkedQueue<>();

        for(int i = 0 ; i<nOfThreads; ++i){
            final int requestUnits = i+1;
            final int thIx = i;
            ths[i] = new Thread(() -> {
                try{
                    for(int j = 0 ; j<nOfReps; ++j){
                        s.acquire(requestUnits, lotsOfTime);
                        int curr = units.addAndGet(-requestUnits);
                        assertTrue(curr >= 0);
                        System.out.println(
                            String.format("[%d] acquired %d on rep %d",
                                    thIx, requestUnits, j));

                        Thread.sleep(rg.nextInt(10));
                        units.addAndGet(requestUnits);
                        s.release(requestUnits);
                    }
                }catch(Throwable t){
                    eQueue.add(t);
                }
            });
            ths[i].start();
        }
        for(int i = 0; i<nOfThreads; ++i){
            ths[i].join();
        }
        assertEquals(initialUnits, units.get());
        assertEquals(0, eQueue.size());
    }

    @Test
    public void release_notifies_multiple_threads() throws InterruptedException {
        final int[] units = {100, 50, 1};
        final Thread[] ths = new Thread[units.length];
        final SemaphoreUsingLocksAndConditions s =
                new SemaphoreUsingLocksAndConditions(0);
        final ConcurrentLinkedQueue<Throwable> eQueue =
                new ConcurrentLinkedQueue<>();
        for(int i = 0; i<units.length; ++i){
            final int ix = i;
            ths[i] = new Thread(()->{
                try {
                    s.acquire(units[ix], lotsOfTime);
                    System.out.println("Acquired "+units[ix]);
                }catch(Throwable e){
                    eQueue.add(e);
                }
            });
            ths[i].start();
        }
        int acc = 0;
        for(int i = 0 ; i<units.length; ++i){
            acc += units[i];
        }
        System.out.println("Releasing "+acc);
        s.release(acc);
        for(int i = 0 ; i<units.length; ++i){
            ths[i].join();
        }
        assertEquals(0, eQueue.size());
    }

    @Test
    public void scheduling_is_fifo() throws InterruptedException {
        final int[] units = {100, 50, 1};
        final Thread[] ths = new Thread[units.length];
        final SemaphoreUsingLocksAndConditions s =
                new SemaphoreUsingLocksAndConditions(0);
        final ConcurrentLinkedQueue<Throwable> eQueue =
                new ConcurrentLinkedQueue<>();
        final int deltaInMs = 100;
        AtomicInteger counter = new AtomicInteger(0);
        s.release(1);
        for(int i = 0; i<units.length; ++i){
            final int ix = i;
            ths[i] = new Thread(()->{
                try {
                    System.out.println("acquiring "+units[ix]);
                    s.acquire(units[ix], lotsOfTime);
                    System.out.println("acquired " + units[ix]);
                    assertEquals(ix, counter.getAndAdd(1));
                }catch(Throwable e){
                    e.printStackTrace();
                    eQueue.add(e);
                }
            });
            ths[i].start();
            Thread.sleep(deltaInMs);
        }
        for(int i = 0 ; i<units.length; ++i){
            s.release(units[i]);
            Thread.sleep(deltaInMs);
        }
        for(int i = 0 ; i<units.length; ++i){
            ths[i].join();
        }
        assertEquals(0, eQueue.size());
    }

    @Test
    public void interrupt_give_up_release_other_threads() throws InterruptedException {
        final int[] units = {100, 50, 1};
        final Thread[] ths = new Thread[units.length];
        final SemaphoreUsingLocksAndConditions s =
                new SemaphoreUsingLocksAndConditions(0);
        final ConcurrentLinkedQueue<Throwable> eQueue =
                new ConcurrentLinkedQueue<>();
        s.release(51);
        for(int i = 0; i<units.length; ++i){
            final int ix = i;
            ths[i] = new Thread(()->{
                try {
                    System.out.println("acquiring "+units[ix]);
                    s.acquire(units[ix], lotsOfTime);
                    System.out.println("acquired " + units[ix]);
                }catch(Throwable e){
                    e.printStackTrace();
                    eQueue.add(e);
                }
            });
            ths[i].start();
        }
        ths[0].interrupt();
        for(int i = 0 ; i<units.length; ++i){
            ths[i].join();
        }
        assertEquals(1, eQueue.size());
        assertTrue(eQueue.peek() instanceof InterruptedException);
    }

    @Test
    public void timeout_releases_other_threads() throws InterruptedException {
        final int[] units = {100, 50, 1};
        final Thread[] ths = new Thread[units.length];
        final SemaphoreUsingLocksAndConditions s =
                new SemaphoreUsingLocksAndConditions(0);
        final ConcurrentLinkedQueue<Throwable> eQueue =
                new ConcurrentLinkedQueue<>();
        final int deltaInMs = 100;
        s.release(51);
        for(int i = 0; i<units.length; ++i){
            final int ix = i;
            ths[i] = new Thread(()->{
                try {
                    long timeout = ix == 0 ?
                            TimeUnit.SECONDS.toNanos(1)
                            : lotsOfTime;
                    System.out.println("acquiring "+units[ix]);
                    boolean res = s.acquire(units[ix], timeout);
                    assertEquals(ix == 0 ? false : true, res);
                    System.out.println("acquired " + units[ix]);
                }catch(Throwable e){
                    e.printStackTrace();
                    eQueue.add(e);
                }
            });
            ths[i].start();
            Thread.sleep(deltaInMs);
        }

        for(int i = 0 ; i<units.length; ++i){
            ths[i].join();
        }
        assertEquals(0, eQueue.size());
    }
}
