using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace OpenSim.Data.MySQL
{
    class MySQLSuperManager
    {
        public bool Locked;
        private Mutex m_lock;
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
