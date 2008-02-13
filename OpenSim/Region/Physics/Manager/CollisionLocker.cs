using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.Physics.Manager
{
    public class CollisionLocker
    {
        
        private bool locked = false;
        private List<IntPtr> worldlock = new List<IntPtr>();
        public CollisionLocker()
        {

        }
        public void dlock(IntPtr world)
        {
            lock (worldlock)
            {
                worldlock.Add(world);
            }

        }
        public void dunlock(IntPtr world)
        {
            lock (worldlock)
            {
                worldlock.Remove(world);
            }
        }
        public bool lockquery()
        {
            return (worldlock.Count > 0);
        }
        public void drelease(IntPtr world)
        {
            lock (worldlock)
            {
                if (worldlock.Contains(world))
                    worldlock.Remove(world);
            }
        }

    }

}
