package pt.isel.pc;

import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;

/**
 * Created by pedro on 24/11/16.
 */
public class LockFreeManualResetEvent {

    private volatile boolean isSet = false;

    private final Lock lock = new ReentrantLock();
    private final Condition cond = lock.newCondition();
    private volatile int waiting = 0;
    private int version = 0;

    public LockFreeManualResetEvent(){

    }

    public void set(){
        isSet = true;
        if(waiting == 0){
            return;
        }
        lock.lock();
        try{
            version += 1;
            cond.signalAll();
        }finally{
            lock.unlock();
        }
    }

    public void reset(){
        isSet = false;
    }

    public void waitForSet() throws InterruptedException{
        if(isSet){
            return;
        }
        lock.lock();
        try{
            int myversion = version;
            waiting += 1;
            if(isSet == true){
                return;
            }

            do {
                cond.await();
                if(myversion != version){
                    return;
                }
            }while(true);

        }finally{
            waiting -= 1;
            lock.unlock();
        }
    }
}
