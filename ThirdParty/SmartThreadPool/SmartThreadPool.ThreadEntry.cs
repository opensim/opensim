
using Amib.Threading.Internal;
using System;
using System.Threading;

namespace Amib.Threading
{
    public partial class SmartThreadPool
    {
        #region ThreadEntry class

        internal class ThreadEntry
        {
            /// <summary>
            /// The thread creation time
            /// The value is stored as UTC value.
            /// </summary>
            private readonly DateTime _creationTime;

            /// <summary>
            /// The last time this thread has been running
            /// It is updated by IAmAlive() method
            /// The value is stored as UTC value.
            /// </summary>
            private DateTime _lastAliveTime;

            /// <summary>
            /// A reference from each thread in the thread pool to its SmartThreadPool
            /// object container.
            /// With this variable a thread can know whatever it belongs to a 
            /// SmartThreadPool.
            /// </summary>
            private SmartThreadPool _associatedSmartThreadPool;

            /// <summary>
            /// A reference to the current work item a thread from the thread pool 
            /// is executing.
            /// </summary>            
            public WorkItem CurrentWorkItem { get; set; }
            public Thread WorkThread;

            public ThreadEntry(SmartThreadPool stp, Thread th)
            {
                _associatedSmartThreadPool = stp;
                _creationTime = DateTime.UtcNow;
                _lastAliveTime = DateTime.MinValue;
                WorkThread = th;
            }

            public SmartThreadPool AssociatedSmartThreadPool
            {
                get { return _associatedSmartThreadPool; }
            }

            public void IAmAlive()
            {
                _lastAliveTime = DateTime.UtcNow;
            }

            public void Clean()
            {
                WorkThread = null;
                _associatedSmartThreadPool = null;
            }
        }

        #endregion
    }
}
