using System.Threading;

namespace OpenSim.Data.MySQL
{
    public class MySQLSuperManager
    {
        public bool Locked;
        private readonly Mutex m_lock = new Mutex(false);
        public MySQLManager Manager;

        public void GetLock()
        {
            Locked = true;
            m_lock.WaitOne();
        }

        public void Release()
        {
            m_lock.ReleaseMutex();
            Locked = false;
        }

    }
}
