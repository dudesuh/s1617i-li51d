package pt.isel.pc;

import java.util.concurrent.atomic.AtomicReference;

/**
 * Created by pedro on 14/11/16.
 */
public class LockFreeStack<T> {

    private static class Node<T>{
        public final T value;
        public Node<T> next;
        public Node(T v){
            value = v;
        }
    }

    private AtomicReference<Node<T>> tos =
            new AtomicReference(null);

    public void push(T t){
        Node<T> node = new Node<>(t);
        Node<T> observedTos;
        do {
            observedTos = tos.get();
            node.next = observedTos;
        }while(!tos.compareAndSet(observedTos, node));
    }

    public T pop() {
        Node<T> observedTos;
        Node<T> candidateTos;
        T candidateValue;
        do{
            observedTos = tos.get();
            if(observedTos == null) return null;
            candidateValue = observedTos.value;
            candidateTos = observedTos.next;
        }while(!tos.compareAndSet(observedTos, candidateTos));
        return candidateValue;
        //return observedTos.value;
    }
}
