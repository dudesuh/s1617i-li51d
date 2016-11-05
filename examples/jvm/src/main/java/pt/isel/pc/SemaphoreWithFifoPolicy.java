package pt.isel.pc;

import java.util.LinkedList;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;

/**
 * Created by pedro on 29/10/16.
 */
public class SemaphoreWithFifoPolicy {

    private static class Request {
        public final int units;
        public final Condition condition;
        public Request(int units, Lock lock){
            this.units = units;
            condition = lock.newCondition();
        }
    }

    private final LinkedList<Request> queue = new LinkedList<>();
    private final Lock lock = new ReentrantLock();
    private int available;

    public SemaphoreWithFifoPolicy(int initial){
        available = initial;
    }

    private void conditionalNotify(){
        Request r = queue.peek();
        if(r != null && available >= r.units){
            r.condition.signal();
        }
    }

    public boolean acquire(int units, long nanos) throws InterruptedException {
        lock.lock();
        try{
            if(queue.size() == 0 && available > units){
                available -= units;
                return true;
            }
            Request request = new Request(units, lock);
            queue.addLast(request);
            while(true){
                try{
                    nanos = request.condition.awaitNanos(nanos);
                }catch(InterruptedException e){
                    queue.remove(request);
                    conditionalNotify();
                    throw e;
                }
                if(request == queue.peek() && available >= units){
                    queue.removeFirst();
                    available -= units;
                    conditionalNotify();
                    return true;
                }
                if(nanos <= 0){
                    queue.remove(request);
                    conditionalNotify();
                    return false;
                }
            }
        }finally{
            lock.unlock();
        }
    }

    public void release(int units){
        lock.lock();
        try{
            available += units;
            conditionalNotify();
        }finally{
            lock.unlock();
        }
    }
}
