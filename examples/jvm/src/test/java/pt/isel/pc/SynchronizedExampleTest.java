package pt.isel.pc;

import org.junit.Test;

import java.util.Random;
import java.util.concurrent.ConcurrentLinkedQueue;

import static org.junit.Assert.assertEquals;

/**
 * Created by pedro on 31/10/16.
 */
public class SynchronizedExampleTest {
    private static Random rg = new Random();
    public static class Counter{
        private int counter;
        private Object mlock = new Object();

        public void inc()
                throws InterruptedException {
            synchronized (mlock) {
                int c = counter;
                Thread.sleep(rg.nextInt(100));
                counter = c + 1;
            }
        }

        public int get(){
            synchronized (mlock) {
                return counter;
            }
        }
    }

    @Test
    public void example1() throws InterruptedException {
        final int nOfThreads = 10;
        final int nOfReps = 10;
        final Counter counter = new Counter();
        Thread[] ths = new Thread[nOfThreads];
        ConcurrentLinkedQueue<Throwable> eQueue =
                new ConcurrentLinkedQueue<>();

        for(int i = 0 ; i<nOfThreads ; ++i){
            ths[i] = new Thread(()->{
                try {
                    for (int j = 0; j < nOfReps; ++j) {
                        counter.inc();
                    }
                }catch(Throwable e){
                    eQueue.add(e);
                }
            });
            ths[i].start();
        }
        for(int i = 0; i<nOfThreads; ++i){
            ths[i].join();
        }
        assertEquals(nOfThreads*nOfReps, counter.get());
        assertEquals(0, eQueue.size());
    }
}
