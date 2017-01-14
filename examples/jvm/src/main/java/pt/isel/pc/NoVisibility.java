package pt.isel.pc;

/**
 * Created by pedro on 14/11/16.
 */
public class NoVisibility {

    private volatile static boolean ready;
    private static int number;
    private static Object lock = new Object();


    private static class ReaderThread extends Thread {
        public void run() {

            while (true) {
                //synchronized (lock) {
                    if (ready) {
                        System.out.println(number);
                        return;
                    }
                //}
            }
        }
    }

    public static void main(String[] args) throws InterruptedException {
        new ReaderThread().start();
        Thread.sleep(100);
        //synchronized (lock) {
            number = 42;
            ready = true;
        //}
        System.out.println("main is done");
    }
}
