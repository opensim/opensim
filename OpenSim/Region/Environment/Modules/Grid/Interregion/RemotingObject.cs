/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

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
