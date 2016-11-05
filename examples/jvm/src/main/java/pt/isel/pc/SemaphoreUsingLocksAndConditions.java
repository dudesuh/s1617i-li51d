package pt.isel.pc;

import java.util.LinkedList;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;

/**
 * Created by pedro on 31/10/16.
 * - FIFO acquire policy
 * - Minimizes context switches
 */
public class SemaphoreUsingLocksAndConditions {

    private static class Request{
        public final int units;
        public final Condition condition;

        public Request(int units, Lock lock){
            this.units = units;
            condition = lock.newCondition();
        }
    }

    private int available;
    private final Lock mlock = new ReentrantLock();
    private final LinkedList<Request> queue = new LinkedList<>();

    public SemaphoreUsingLocksAndConditions(int initial){
        available = initial;
    }

    private void conditionalSignal(){
        Request r = queue.peek();
        if(r != null && available >= r.units){
            r.condition.signal();
        }
    }

    private void giveUp(Request myReq){
        queue.remove(myReq);
        conditionalSignal();
    }

    public boolean acquire(int units, long nanos) throws InterruptedException{
        mlock.lock();
        try{
            if(queue.size() == 0 && available >= units){
                available -= units;
                return true;
            }
            Request myReq = new Request(units, mlock);
            queue.addLast(myReq);
            while(true){
                try {
                    nanos = myReq.condition.awaitNanos(nanos);
                }catch(InterruptedException e){
                    giveUp(myReq);
                    throw e;
                }
                if(queue.peek() == myReq && available >= units){
                    queue.removeFirst();
                    available -= units;
                    conditionalSignal();
                    return true;
                }
                if(nanos <= 0){
                    giveUp(myReq);
                    return false;
                }
            }
        }finally{
            mlock.unlock();
        }
    }

    public void release(int units){
        mlock.lock();
        try{
            available += units;
            conditionalSignal();
        }finally{
            mlock.unlock();
        }
    }
}
