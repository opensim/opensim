// Ami Bar
// amibar@gmail.com

using System.Threading;

namespace Amib.Threading
{
    /// <summary>
    /// Summary description for STPStartInfo.
    /// </summary>
    public class STPStartInfo : WIGStartInfo
    {
        /// <summary>
        /// Idle timeout in milliseconds.
        /// If a thread is idle for _idleTimeout milliseconds then 
        /// it may quit.
        /// </summary>
        private int _idleTimeout;

        /// <summary>
        /// The lower limit of threads in the pool.
        /// </summary>
        private int _minWorkerThreads;

        /// <summary>
        /// The upper limit of threads in the pool.
        /// </summary>
        private int _maxWorkerThreads;

        /// <summary>
        /// The priority of the threads in the pool
        /// </summary>
        private ThreadPriority _threadPriority;

        /// <summary>
        /// The thread pool name. Threads will get names depending on this.
        /// </summary>
        private string _threadPoolName;

        /// <summary>
        /// If this field is not null then the performance counters are enabled
        /// and use the string as the name of the instance.
        /// </summary>
        private string _pcInstanceName;

        private int _stackSize;

        public STPStartInfo() : base()
        {
            _idleTimeout = SmartThreadPool.DefaultIdleTimeout;
            _minWorkerThreads = SmartThreadPool.DefaultMinWorkerThreads;
            _maxWorkerThreads = SmartThreadPool.DefaultMaxWorkerThreads;
            _threadPriority = SmartThreadPool.DefaultThreadPriority;
            _threadPoolName = SmartThreadPool.DefaultThreadPoolName;
            _pcInstanceName = SmartThreadPool.DefaultPerformanceCounterInstanceName;
            _stackSize = SmartThreadPool.DefaultStackSize;
        }

        public STPStartInfo(STPStartInfo stpStartInfo) : base(stpStartInfo)
        {
            _idleTimeout = stpStartInfo._idleTimeout;
            _minWorkerThreads = stpStartInfo._minWorkerThreads;
            _maxWorkerThreads = stpStartInfo._maxWorkerThreads;
            _threadPriority = stpStartInfo._threadPriority;
            _threadPoolName = stpStartInfo._threadPoolName;
            _pcInstanceName = stpStartInfo._pcInstanceName;
            _stackSize = stpStartInfo._stackSize;
        }

        public int IdleTimeout
        {
            get { return _idleTimeout; }
            set { _idleTimeout = value; }
        }

        public int MinWorkerThreads
        {
            get { return _minWorkerThreads; }
            set { _minWorkerThreads = value; }
        }

        public int MaxWorkerThreads
        {
            get { return _maxWorkerThreads; }
            set { _maxWorkerThreads = value; }
        }

        public ThreadPriority ThreadPriority
        {
            get { return _threadPriority; }
            set { _threadPriority = value; }
        }

        public virtual string ThreadPoolName
        {
            get { return _threadPoolName; }
            set { _threadPoolName = value; }
        }


        public string PerformanceCounterInstanceName
        {
            get { return _pcInstanceName; }
            set { _pcInstanceName = value; }
        }

        public int StackSize
        {
            get { return _stackSize; }
            set { _stackSize = value; }
        }
    }
}
