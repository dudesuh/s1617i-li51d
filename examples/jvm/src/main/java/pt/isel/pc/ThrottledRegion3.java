package pt.isel.pc;

import java.util.LinkedList;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ConcurrentMap;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;
import java.util.function.Function;
import java.util.function.Predicate;

/**
 * Created by pedro on 19/11/16.
 */
public class ThrottledRegion3 {

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

        // used to store a waitingCounter flag on bit_31 and a counter on the remaining bits
        // the waitingCounter flag is observed outside the lock however it is only changed inside the lock
        // by having both the flag and the counter on the same integer we have atomic updates of both
        private final AtomicInteger count = new AtomicInteger(0);
        // waitingCounter mask
        private static final int MASK = 1 << 31;

        private final Lock lock = new ReentrantLock();
        // changed and observed inside the lock
        private int waitingCounter = 0;
        private final LinkedList<Request> q = new LinkedList<>();

        private boolean conditionalUpdate(AtomicInteger i, Predicate<Integer> pred, Function<Integer, Integer> next){
            do{
                int observed = i.get();
                if(!pred.test(observed)){
                    return false;
                }
                int newValue = next.apply(observed);
                if(i.compareAndSet(observed, newValue)){
                    return true;
                }
            }while(true);
        }

        private int update(AtomicInteger i, Function<Integer, Integer> next){
            do{
                int observed = i.get();
                int newValue = next.apply(observed);
                if(i.compareAndSet(observed, newValue)){
                    return newValue;
                }
            }while(true);
        }

        private boolean waiting(int counter){
            return counter < 0;
        }

        public boolean TryEnter() throws InterruptedException {
            if(conditionalUpdate(count,
                    // if not waiting and below maxInside...
                    observed -> !waiting(observed) && observed < maxInside,
                    // ... just increment it
                    observed -> observed+1)){
                return true;
            }

            // slow path
            lock.lock();
            try{
                if(waitingCounter == maxWaiting){
                    return false;
                }
                // because the counter could be decremented in the meanwhile
                int res = update(count,
                        // if not waiting and below maxInside...
                        observed -> !waiting(observed) && observed < maxInside
                            // ... just increment it
                            ? observed + 1
                            // ... otherwise set waitingCounter
                            : observed | MASK
                        );
                if(!waiting(res)){
                    return true;
                }
                // if we reached here, then the waiting bit is set,
                // meaning that a leaver will always try to signal a waitingCounter thread
                waitingCounter += 1;
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
                        giveUp(r);
                        throw e;
                    }
                    if(r.done){
                        return true;
                    }
                    if(timeout <= 0){
                        giveUp(r);
                        return false;
                    }
                }while(true);
            }finally{
                lock.unlock();
            }
        }

        private void giveUp(Request r){
            waitingCounter -= 1;
            q.remove(r);
            if(q.peekFirst() == null){
                // no one is waiting, update the bit
                // no concurrency on this update since we are inside the lock
                update(count, observed -> observed & ~MASK);
            }
        }

        public void Leave(){
            if(conditionalUpdate(count,
                    // if no one is waiting...
                    observed -> !waiting(observed),
                    // ... just decrement the counter
                    observed -> observed - 1)){
                return;
            }
            lock.lock();
            try{
                Request first = q.peekFirst();
                // recheck if someone is waiting ...
                if(first == null){
                    // if not, just decrement the counter
                    // since we are inside the lock a waiter
                    // - did not yet entered and so will see the decremented counter
                    // - always add the request to the queue
                    update(count, observed -> observed-1);
                    return;
                }
                first.done = true;
                q.remove(first);
                first.condition.signal();
                waitingCounter -= 1;
                if(q.peekFirst() == null){
                    // no one is waiting
                    update(count, observed -> observed & ~MASK);
                }
            }finally{
                lock.unlock();
            }
        }
    }

    private final ConcurrentMap<Integer, ThrottledRegionForKey> map =
            new ConcurrentHashMap<>();

    public ThrottledRegion3(int maxInside, int maxWaiting, int waitTimeout){
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
