package pt.isel.pc;

import org.junit.*;

import java.util.Random;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;

import static org.junit.Assert.*;

/**
 * Created by pedro on 29/10/16.
 */
public class SemaphoreWithFifoPolicyTests {

    final int nOfThreads = 100;
    final int nOfReps = 10;
    final long LotsOfTime = (1L << 62);

    @Test
    public void maximum_no_of_units_is_not_exceeded() throws InterruptedException {
        Random rg = new Random();
        Thread[] ths = new Thread[nOfThreads];
        int initialUnits = nOfThreads;
        SemaphoreWithFifoPolicy s = new SemaphoreWithFifoPolicy(initialUnits);
        AtomicInteger counter = new AtomicInteger(initialUnits);
        ConcurrentLinkedQueue<Throwable> eQueue = new ConcurrentLinkedQueue<>();

        for(int i = 0; i<ths.length; ++i){
            final int units = i;
            ths[i] = new Thread(() -> {
                try {
                    for (int j = 0; j < nOfReps; ++j) {
                        boolean b = s.acquire(units, LotsOfTime);
                        assertTrue(b);
                        assertTrue(counter.addAndGet(-units) >= 0);
                        Thread.sleep(rg.nextInt(10));
                        System.out.println(String.format("%d/%d - %d",units, j, counter.get()));
                        assertTrue(counter.addAndGet(units) <= initialUnits);
                        s.release(units);
                    }
                }catch(Throwable e){
                    eQueue.add(e);
                }
            });
            ths[i].start();

        }
        for(int i = 0; i<ths.length; ++i){
            ths[i].join();
        }
        assertEquals(0, eQueue.size());
        assertEquals(initialUnits, counter.get());
    }

    @Test
    public void uses_fifo_policy() throws InterruptedException {
        int[] units = new int[]{100, 50, 10, 1};
        Thread[] ths = new Thread[units.length];
        ConcurrentLinkedQueue<Throwable> eQueue = new ConcurrentLinkedQueue<>();
        AtomicInteger counter = new AtomicInteger();
        SemaphoreWithFifoPolicy s = new SemaphoreWithFifoPolicy(0);
        for(int i = 0 ; i<units.length; ++i){
            final int ix = i;
            ths[i] = new Thread(() -> {
                try{
                    s.acquire(units[ix], LotsOfTime);
                    int c = counter.getAndIncrement();
                    assertEquals(ix, c);
                }catch(Exception e){
                    eQueue.add(e);
                }
            });
            ths[i].start();
            Thread.sleep(100);
        }
        s.release(units.length);
        for(int i = 0 ; i<units.length; ++i){
            s.release(units[i]-1);
            Thread.sleep(100);
        }
        for(int i = 0; i<units.length; ++i){
            ths[i].join();
        }
        assertEquals(0, eQueue.size());
    }

    @Test
    public void timeout_releases_waiting_threads() throws InterruptedException {
        int[] units = new int[]{1000, 50, 10, 1};
        Thread[] ths = new Thread[units.length];
        ConcurrentLinkedQueue<Throwable> eQueue = new ConcurrentLinkedQueue<>();
        SemaphoreWithFifoPolicy s = new SemaphoreWithFifoPolicy(0);
        for(int i = 0 ; i<units.length; ++i){
            final int ix = i;
            ths[i] = new Thread(() -> {
                try{
                    boolean res = s.acquire(units[ix], ix == 0 ? TimeUnit.SECONDS.toNanos(1) : LotsOfTime);
                    if(ix == 0){
                        assertFalse(res);
                    }else {
                        assertTrue(res);
                    }
                }catch(Throwable e){
                    eQueue.add(e);
                }
            });
            ths[i].start();
            Thread.sleep(100);
        }
        for(int i = 1 ; i<units.length; ++i){
            s.release(units[i]);
            Thread.sleep(100);
        }
        for(int i = 0; i<units.length; ++i){
            ths[i].join();
        }
        assertEquals(0, eQueue.size());
    }
}
