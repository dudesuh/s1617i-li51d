using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SemaphoreAsync
{
    internal class TapQueue<T> where T : class
    {
        private readonly int queueSize;
        private readonly T[] room;
        private readonly TapSemaphore freeSlots, filledSlots;
        private int putIdx, takeIdx;

        // construct the blocking queue
        public TapQueue(int size)
        {
            queueSize = size;
            room = new T[size];
            putIdx = takeIdx = 0;
            freeSlots = new TapSemaphore(size, size);
            filledSlots = new TapSemaphore(0, size);
        }        
        
        public async Task<bool> PutAsync(T item, int timeout)
        {
            if (!await freeSlots.WaitAsync(timeout))
            {
                return false;
            }
            lock (room)
            {
                room[putIdx++ % queueSize] = item;
            }
            filledSlots.Release();
            return true;
        }
        
        public async Task PutAsync(T item)
        {
            await PutAsync(item, Timeout.Infinite);
        }
        
        public async Task<T> TakeAsync(int timeout)
        {
            if (!await filledSlots.WaitAsync(timeout))
            {
                return null;
            }
            T item;
            lock (room)
            {
                item = room[takeIdx++ % queueSize];
            }
            freeSlots.Release();
            return item;
        }
        
        public async Task<T> TakeAsync()
        {
            return await TakeAsync(Timeout.Infinite);
        }
    }
}
