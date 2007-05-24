using System;
using System.Threading;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Utilities
{
    public class BlockingQueue<T>
    {
        private Queue<T> _queue = new Queue<T>();
        private object _queueSync = new object();

        public void Enqueue(T value)
        {
            lock (_queueSync)
            {
                _queue.Enqueue(value);
                Monitor.Pulse(_queueSync);
            }
        }

        public T Dequeue()
        {
            lock (_queueSync)
            {
                if (_queue.Count < 1)
                    Monitor.Wait(_queueSync);

                return _queue.Dequeue();
            }
        }
    }
}
