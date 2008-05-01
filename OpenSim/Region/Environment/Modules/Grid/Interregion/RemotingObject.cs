using System;
using System.Collections.Generic;
using OpenSim.Framework;

namespace OpenSim.Region.Environment.Modules.Grid.Interregion
{
    public class RemotingObject : MarshalByRefObject
    {
        private readonly Location[] m_coords;
        private readonly Dictionary<Type, Object> m_interfaces = new Dictionary<Type, object>();

        public RemotingObject(Dictionary<Type, Object> myInterfaces, Location[] coords)
        {
            m_interfaces = myInterfaces;
            m_coords = coords;
        }

        public Location[] GetLocations()
        {
            return (Location[]) m_coords.Clone();
        }

        public string[] GetInterfaces()
        {
            string[] interfaces = new string[m_interfaces.Count];
            int i = 0;

            foreach (KeyValuePair<Type, object> pair in m_interfaces)
            {
                interfaces[i++] = pair.Key.FullName;
            }

            return interfaces;
        }

        /// <summary>
        /// Returns a registered interface availible to neighbouring regions.
        /// </summary>
        /// <typeparam name="T">The type of interface you wish to request</typeparam>
        /// <returns>A MarshalByRefObject inherited from this region inheriting the interface requested.</returns>
        /// <remarks>All registered interfaces <b>MUST</b> inherit from MarshalByRefObject and use only serialisable types.</remarks>
        public T RequestInterface<T>()
        {
            if (m_interfaces.ContainsKey(typeof (T)))
                return (T) m_interfaces[typeof (T)];

            throw new NotSupportedException("No such interface registered.");
        }
    }
}