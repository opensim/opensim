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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework
{
    /// <summary>
    /// This class stores and retrieves dynamic attributes.
    /// </summary>
    /// <remarks>
    /// Modules that want to use dynamic attributes need to do so in a private data store
    /// which is accessed using a unique name. DAMap provides access to the data stores,
    /// each of which is an OSDMap. Modules are free to store any type of data they want
    /// within their data store. However, avoid storing large amounts of data because that
    /// would slow down database access.
    /// </remarks>
    public class DAMap : IXmlSerializable
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly int MIN_NAMESPACE_LENGTH = 4;

        private OSDMap m_map = new OSDMap();

        // WARNING: this is temporary for experimentation only, it will be removed!!!!
        public OSDMap TopLevelMap
        {
            get { return m_map; }
            set { m_map = value; }
        }

        public XmlSchema GetSchema() { return null; }

        public static DAMap FromXml(string rawXml)
        {
            DAMap map = new DAMap();
            map.ReadXml(rawXml);
            return map;
        }

        public void ReadXml(XmlReader reader)
        {
            ReadXml(reader.ReadInnerXml());
        }

        public void ReadXml(string rawXml)
        {
            // System.Console.WriteLine("Trying to deserialize [{0}]", rawXml);

            lock (this)
            {
                m_map = (OSDMap)OSDParser.DeserializeLLSDXml(rawXml);
                SanitiseMap(this);
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteRaw(ToXml());
        }

        public string ToXml()
        {
            lock (this)
                return OSDParser.SerializeLLSDXmlString(m_map);
        }

        public void CopyFrom(DAMap other)
        {
            // Deep copy

            string data = null;
            lock (other)
            {
                if (other.CountNamespaces > 0)
                {
                    data = OSDParser.SerializeLLSDXmlString(other.m_map);
                }
            }

            lock (this)
            {
                if (data == null)
                    Clear();
                else
                    m_map = (OSDMap)OSDParser.DeserializeLLSDXml(data);
            }
        }

        /// <summary>
        /// Sanitise the map to remove any namespaces or stores that are not OSDMap.
        /// </summary>
        /// <param name='map'>
        /// </param>
        public static void SanitiseMap(DAMap daMap)
        {
            List<string> keysToRemove = null;

            OSDMap namespacesMap = daMap.m_map;

            foreach (string key in namespacesMap.Keys)
            {
//                Console.WriteLine("Processing ns {0}", key);
                if (!(namespacesMap[key] is OSDMap))
                {
                    if (keysToRemove == null)
                        keysToRemove = new List<string>();

                    keysToRemove.Add(key);
                }
            }

            if (keysToRemove != null)
            {
                foreach (string key in keysToRemove)
                {
//                    Console.WriteLine ("Removing bad ns {0}", key);
                    namespacesMap.Remove(key);
                }
            }

            foreach (OSD nsOsd in namespacesMap.Values)
            {
                OSDMap nsOsdMap = (OSDMap)nsOsd;
                keysToRemove = null;

                foreach (string key in nsOsdMap.Keys)
                {
                    if (!(nsOsdMap[key] is OSDMap))
                    {
                        if (keysToRemove == null)
                            keysToRemove = new List<string>();

                        keysToRemove.Add(key);
                    }
                }

                if (keysToRemove != null)
                    foreach (string key in keysToRemove)
                        nsOsdMap.Remove(key);
            }
        }

        /// <summary>
        /// Get the number of namespaces
        /// </summary>
        public int CountNamespaces { get { lock (this) { return m_map.Count; } } }

        /// <summary>
        /// Get the number of stores.
        /// </summary>
        public int CountStores
        {
            get
            {
                int count = 0;

                lock (this)
                {
                    foreach (OSD osdNamespace in m_map)
                    {
                        count += ((OSDMap)osdNamespace).Count;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Retrieve a Dynamic Attribute store
        /// </summary>
        /// <param name="ns">namespace for the store - use "OpenSim" for in-core modules</param>
        /// <param name="storeName">name of the store within the namespace</param>
        /// <returns>an OSDMap representing the stored data, or null if not found</returns>
        public OSDMap GetStore(string ns, string storeName)
        {
            OSD namespaceOsd;

            lock (this)
            {
                if (m_map.TryGetValue(ns, out namespaceOsd))
                {
                    OSD store;

                    if (((OSDMap)namespaceOsd).TryGetValue(storeName, out store))
                        return (OSDMap)store;
                }
            }

            return null;
        }

        /// <summary>
        /// Saves a Dynamic attribute store
        /// </summary>
        /// <param name="ns">namespace for the store - use "OpenSim" for in-core modules</param>
        /// <param name="storeName">name of the store within the namespace</param>
        /// <param name="store">an OSDMap representing the data to store</param>
        public void SetStore(string ns, string storeName, OSDMap store)
        {
            ValidateNamespace(ns);
            OSDMap nsMap;

            lock (this)
            {
                if (!m_map.ContainsKey(ns))
                {
                    nsMap = new OSDMap();
                    m_map[ns] = nsMap;
                }

                nsMap = (OSDMap)m_map[ns];

//                m_log.DebugFormat("[DA MAP]: Setting store to {0}:{1}", ns, storeName);
                nsMap[storeName] = store;
            }
        }

        /// <summary>
        /// Validate the key used for storing separate data stores.
        /// </summary>
        /// <param name='key'></param>
        public static void ValidateNamespace(string ns)
        {
            if (ns.Length < MIN_NAMESPACE_LENGTH)
                throw new Exception("Minimum namespace length is " + MIN_NAMESPACE_LENGTH);
        }

        public bool ContainsStore(string ns, string storeName)
        {
            OSD namespaceOsd;

            lock (this)
            {
                if (m_map.TryGetValue(ns, out namespaceOsd))
                {
                    return ((OSDMap)namespaceOsd).ContainsKey(storeName);
                }
            }

            return false;
        }

        public bool TryGetStore(string ns, string storeName, out OSDMap store)
        {
            OSD namespaceOsd;

            lock (this)
            {
                if (m_map.TryGetValue(ns, out namespaceOsd))
                {
                    OSD storeOsd;

                    bool result = ((OSDMap)namespaceOsd).TryGetValue(storeName, out storeOsd);
                    store = (OSDMap)storeOsd;

                    return result;
                }
            }

            store = null;
            return false;
        }

        public void Clear()
        {
            lock (this)
                m_map.Clear();
        }

        public bool RemoveStore(string ns, string storeName)
        {
            OSD namespaceOsd;

            lock (this)
            {
                if (m_map.TryGetValue(ns, out namespaceOsd))
                {
                    OSDMap namespaceOsdMap = (OSDMap)namespaceOsd;
                    namespaceOsdMap.Remove(storeName);

                    // Don't keep empty namespaces around
                    if (namespaceOsdMap.Count <= 0)
                        m_map.Remove(ns);
                }
            }

            return false;
        }
    }
}