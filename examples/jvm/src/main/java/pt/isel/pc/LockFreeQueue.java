package pt.isel.pc;

import java.util.concurrent.atomic.AtomicReference;

/**
 * Created by pedro on 21/11/16.
 */
public class LockFreeQueue<T> {

    private static class Node<T>{
        public AtomicReference<Node<T>> next;
        public final T value;
        public Node(T value){
            this.value = value;
        }
    }

    private AtomicReference<Node<T>> head;
    private AtomicReference<Node<T>> tail;

    public LockFreeQueue(){
        Node<T> dummyNode = new Node<T>(null);
        head.set(dummyNode);
        tail.set(dummyNode);
    }

    public void enqueue(T value){
        Node<T> mynode = new Node<>(value);
        while(true) {
            Node<T> currTail = tail.get();
            Node<T> tailNext = currTail.next.get();
            if (tailNext == null) {
                // (1)
                if (currTail.next.compareAndSet(null, mynode)) {
                    // (2)
                    tail.compareAndSet(currTail, mynode);
                    return;
                }
            }else{
                // try to finish another Thread's enqueue
                // (2)
                tail.compareAndSet(currTail, tailNext);
            }
        }
    }


}
