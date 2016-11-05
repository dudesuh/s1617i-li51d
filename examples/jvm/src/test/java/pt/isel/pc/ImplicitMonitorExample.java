package pt.isel.pc;

import org.junit.Test;

import java.util.Random;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.atomic.AtomicInteger;

import static org.junit.Assert.assertEquals;

/**
 * Created by pedro on 31/10/16.
 */
public class ImplicitMonitorExample {

    static class ReallyBasicSemaphore {
        private int available;
        public ReallyBasicSemaphore(int initial){
            available = initial;
        }

        public synchronized void acquire() throws InterruptedException {
            while(available == 0){
                try {
                    this.wait();
                } catch (InterruptedException e) {
                    if(available > 0){
                        this.notify();
                    }
                    throw e;
                }
            }
            available -= 1;
        }

        public synchronized void release() {
            available += 1;
            this.notify();
        }
    }

    @Test
    public void someTest() throws InterruptedException {
        final Random rg = new Random();
        final int nOfThreads = 10;
        final AtomicInteger counter = new AtomicInteger(1);
        final ConcurrentLinkedQueue<Throwable> eQueue = new ConcurrentLinkedQueue<>();
        final Thread[] ths = new Thread[nOfThreads];
        final ReallyBasicSemaphore s = new ReallyBasicSemaphore(1);
        for(int i = 0; i<nOfThreads; ++i){
            ths[i] = new Thread(()->{
                try {
                    s.acquire();
                    assertEquals(0, counter.decrementAndGet());
                    Thread.sleep(rg.nextInt(100));
                    assertEquals(1, counter.incrementAndGet());
                    s.release();
                }catch(Throwable e){
                    e.printStackTrace();
                    eQueue.add(e);
                }
            });
            ths[i].start();
        }
        for(int i = 0 ; i<nOfThreads; ++i){
            ths[i].join();
        }
        assertEquals(1, counter.get());
        assertEquals(0, eQueue.size());
    }
}
