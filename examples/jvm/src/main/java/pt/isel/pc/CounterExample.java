package pt.isel.pc;

import java.util.concurrent.atomic.AtomicInteger;

/**
 * Created by pedro on 14/11/16.
 */
public class CounterExample {

    static class Counter1 {
        private long counter;

        public void inc() { counter +=1; }
        public long get() { return counter; }
    }

    static class Counter2 {
        private long counter;

        public synchronized void inc() { counter +=1; }
        public synchronized long get() { return counter; }
    }

    static class Counter3 {
        private volatile long counter;

        public void inc() { counter +=1; }
        public long get() { return counter; }
    }

    static class Counter4 {
        private AtomicInteger counter;

        public Counter4() {
            counter = new AtomicInteger();
        }

        public void inc() {
            while(true) {
                int oldValue = counter.get();
                int newValue = oldValue+1;
                // ...
                if (counter.compareAndSet(oldValue, newValue)) {
                    return;
                }
            }
        }
        public long get() { return counter.get(); }
    }

    static class Counter5 {
        private AtomicInteger counter = new AtomicInteger();

        public void inc() {
            counter.incrementAndGet();
        }
        public long get() { return counter.get(); }
    }


    static class IncrThread extends Thread{
        public void run(){
            while(true){
                counter.inc();
            }
        }
    }

    public static Counter1 counter = new Counter1();

    public static void main(String[] args){
        new IncrThread().start();
        while(true){
            System.out.println(counter.get());
        }
    }
}
