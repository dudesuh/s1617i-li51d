package pt.isel.pc;

import java.util.LinkedList;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ConcurrentMap;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;

/**
 * Created by pedro on 19/11/16.
 */
public class ThrottledRegion2 {

    private final int maxInside;
    private final int maxWaiting;
    private final int waitTimeout;

    private static class Request
    {
        public final Condition condition;
        public boolean done;
        public Request(Condition condition){
            this.condition = condition;
            done = false;
        }
    }

    private class ThrottledRegionForKey{

        private final AtomicInteger insideCount = new AtomicInteger(0);

        // changed inside the lock but observed outside of it
        private volatile int waitingCount = 0;

        private final Lock lock = new ReentrantLock();

        // changed and observed inside the lock
        private final LinkedList<Request> q = new LinkedList<>();

        private boolean TryAcquire(){
            do{
                int observed = insideCount.get();
                if(observed >= maxInside){
                    return false;
                }
                if(insideCount.compareAndSet(observed, observed + 1)){
                    return true;
                }
            }while(true);
        }

        public boolean TryEnter() throws InterruptedException {
            if(waitingCount == 0 && TryAcquire()){
                return true;
            }
            // slow path
            lock.lock();
            try{
                // if full, quit immediately
                if(waitingCount == maxWaiting){
                    return false;
                }

                // mark as waiting
                // until this point, this thread can be overtaken by another one in the fast path
                waitingCount +=1;

                // If first in waiting line, check again if region is not full
                if(waitingCount == 1 && TryAcquire()) {
                    waitingCount -= 1;
                    return true;
                }

                // let's wait ...
                Request r = new Request(lock.newCondition());
                q.addLast(r);
                long timeout = waitTimeout;
                do{
                    try{
                        timeout = r.condition.awaitNanos(timeout);
                    }catch(InterruptedException e){
                        if(r.done){
                            Thread.currentThread().interrupt();
                            return true;
                        }
                        waitingCount -= 1;
                        q.remove(r);
                        throw e;
                    }
                    if(r.done){
                        return true;
                    }
                    if(timeout <= 0){
                        waitingCount -= 1;
                        q.remove(r);
                        return false;
                    }
                }while(true);
            }finally{
                lock.unlock();
            }
        }

        public void Leave(){
            boolean alreadyDecremented = false;
            if(waitingCount == 0){
                insideCount.decrementAndGet();
                if(waitingCount == 0) {
                    return;
                }
                alreadyDecremented = true;
            }
            lock.lock();
            try{
                Request first = q.peekFirst();
                if(first == null) {
                    insideCount.decrementAndGet();
                    return;
                }
                if(alreadyDecremented && !TryAcquire()){
                    return;
                }
                first.done = true;
                waitingCount -= 1;
                q.remove(first);
                first.condition.signal();
            }finally{
                lock.unlock();
            }
        }
    }

    private final ConcurrentMap<Integer, ThrottledRegionForKey> map =
            new ConcurrentHashMap<>();

    public ThrottledRegion2 (int maxInside, int maxWaiting, int waitTimeout){
        this.maxInside = maxInside;
        this.maxWaiting = maxWaiting;
        this.waitTimeout = waitTimeout;
    }
    public boolean TryEnter(int key) throws InterruptedException {
        return map.computeIfAbsent(key, k -> new ThrottledRegionForKey()).TryEnter();
    }
    public void Leave(int key){
        map.get(key).Leave();
    }

}
