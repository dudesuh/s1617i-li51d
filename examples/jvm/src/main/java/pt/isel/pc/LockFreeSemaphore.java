package pt.isel.pc;

import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;

/**
 * Created by pedro on 21/11/16.
 */
public class LockFreeSemaphore {

    // NOT protected by the lock
    private final AtomicInteger permits;


    private final Lock lock;
    private final Condition cond;
    // mutated inside the lock but observed outside the lock
    private volatile int waiting;

    public LockFreeSemaphore(int initialPermits){
        permits = new AtomicInteger(initialPermits);
        lock = new ReentrantLock();
        cond = lock.newCondition();
        waiting = 0;
    }

    private boolean tryAcquire(){
        do{
            int observed = permits.get();
            if(observed == 0){
                return false;
            }
            if(permits.compareAndSet(observed, observed-1)){
                return true;
            }
        }while(true);
    }

    public void acquire() throws InterruptedException {
        // let's be optimistic
        if(tryAcquire()){
            return;
        }

        // slow path, we must wait
        lock.lock();
        waiting += 1;
        try{
            do {
                if(tryAcquire()){
                    return;
                }
                try {
                    cond.await();
                } catch (InterruptedException e) {
                    cond.signal();
                    throw e;
                }
            }while(true);
        } finally{
            waiting -= 1;
            lock.unlock();
        }
    }

    public void release(){
        permits.incrementAndGet();
        if(waiting == 0){
            return;
        }
        lock.lock();
        try{
            cond.signal();
        }finally{
            lock.unlock();
        }
    }

}
