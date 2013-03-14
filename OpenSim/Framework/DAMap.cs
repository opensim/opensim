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
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
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
    public class DAMap : IDictionary<string, OSDMap>, IXmlSerializable
    {
        private static readonly int MIN_STORE_NAME_LENGTH = 4;

        protected OSDMap m_map;
        
        public DAMap() { m_map = new OSDMap(); }
        
        public XmlSchema GetSchema() { return null; } 

        public static DAMap FromXml(string rawXml)
        {
            DAMap map = new DAMap();
            map.ReadXml(rawXml);
            return map;
        }
        
        public void ReadXml(string rawXml)
        {            
            // System.Console.WriteLine("Trying to deserialize [{0}]", rawXml);
            
            lock (this)
                m_map = (OSDMap)OSDParser.DeserializeLLSDXml(rawXml);         
        }
        
        // WARNING: this is temporary for experimentation only, it will be removed!!!!
        public OSDMap TopLevelMap
        {
            get { return m_map; }
            set { m_map = value; }
        }
        

        public void ReadXml(XmlReader reader)
        { 
            ReadXml(reader.ReadInnerXml());            
        }
        
        public string ToXml()
        {
            lock (this)
                return OSDParser.SerializeLLSDXmlString(m_map);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteRaw(ToXml());
        }
        
        public void CopyFrom(DAMap other)
        {
            // Deep copy
            
            string data = null;
            lock (other)
            {
                if (other.Count > 0)
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
        /// Returns the number of data stores.
        /// </summary>
        public int Count { get { lock (this) { return m_map.Count; } } }
        
        public bool IsReadOnly { get { return false; } }
        
        /// <summary>
        /// Returns the names of the data stores.
        /// </summary>
        public ICollection<string> Keys { get { lock (this) { return m_map.Keys; } } }

        /// <summary>
        /// Returns all the data stores.
        /// </summary>
        public ICollection<OSDMap> Values
        {
            get
            {
                lock (this)
                {
                    List<OSDMap> stores = new List<OSDMap>(m_map.Count);
                    foreach (OSD llsd in m_map.Values)
                        stores.Add((OSDMap)llsd);
                    return stores;
                }
            }
        }
        
        /// <summary>
        /// Gets or sets one data store.
        /// </summary>
        /// <param name="key">Store name</param>
        /// <returns></returns>
        public OSDMap this[string key] 
        {    
            get  
            {                    
                OSD llsd;
                
                lock (this)
                {
                    if (m_map.TryGetValue(key, out llsd))
                        return (OSDMap)llsd;
                    else 
                        return null;
                }
            }    
            
            set
            {
                ValidateKey(key);
                lock (this)
                    m_map[key] = value;
            }
        }

        /// <summary>
        /// Validate the key used for storing separate data stores.
        /// </summary>
        /// <param name='key'></param>
        public static void ValidateKey(string key)
        {
            if (key.Length < MIN_STORE_NAME_LENGTH)
                throw new Exception("Minimum store name length is " + MIN_STORE_NAME_LENGTH);
        }

        public bool ContainsKey(string key) 
        {    
            lock (this)
                return m_map.ContainsKey(key);
        }    

        public void Add(string key, OSDMap store)
        {
            ValidateKey(key);
            lock (this)
                m_map.Add(key, store);
        }    

        public void Add(KeyValuePair<string, OSDMap> kvp) 
        {   
            ValidateKey(kvp.Key);
            lock (this)
                m_map.Add(kvp.Key, kvp.Value);
        }    

        public bool Remove(string key) 
        {    
            lock (this)
                return m_map.Remove(key);
        }    

        public bool TryGetValue(string key, out OSDMap store)
        {
            lock (this)
            {
                OSD llsd;
                if (m_map.TryGetValue(key, out llsd))
                {
                    store = (OSDMap)llsd;
                    return true;
                }
                else
                {
                    store = null;
                    return false;
                }
            }
        }    

        public void Clear()
        {
            lock (this)
                m_map.Clear();
        }  
        
        public bool Contains(KeyValuePair<string, OSDMap> kvp)
        {
            lock (this)
                return m_map.ContainsKey(kvp.Key);
        }

        public void CopyTo(KeyValuePair<string, OSDMap>[] array, int index)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<string, OSDMap> kvp)
        {
            lock (this)
                return m_map.Remove(kvp.Key);
        }

        public System.Collections.IDictionaryEnumerator GetEnumerator()
        {
            lock (this)
                return m_map.GetEnumerator();
        }

        IEnumerator<KeyValuePair<string, OSDMap>> IEnumerable<KeyValuePair<string, OSDMap>>.GetEnumerator()
        {
            return null;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (this)
                return m_map.GetEnumerator();
        }        
    }
}