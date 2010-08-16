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
    /// This is the map for storing and retrieving dynamic attributes.
    /// </summary>
    public class DAMap : IDictionary<string, OSD>, IXmlSerializable
    {    
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
            //System.Console.WriteLine("Trying to deserialize [{0}]", rawXml);
            
            m_map = (OSDMap)OSDParser.DeserializeLLSDXml(rawXml);         
        }
        
        public void ReadXml(XmlReader reader)
        { 
            ReadXml(reader.ReadInnerXml());            
        }
        
        public string ToXml()
        {
            lock (m_map)
                return OSDParser.SerializeLLSDXmlString(m_map);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteRaw(ToXml());
        }                             
        
        public int Count { get { lock (m_map) { return m_map.Count; } } }
        public bool IsReadOnly { get { return false; } }
        public ICollection<string> Keys { get { lock (m_map) { return m_map.Keys; } } }
        public ICollection<OSD> Values { get { lock (m_map) { return m_map.Values; } } }
        public OSD this[string key] 
        {    
            get  
            {                    
                OSD llsd;
                
                lock (m_map)
                {
                    if (m_map.TryGetValue(key, out llsd))
                        return llsd;
                    else 
                        return null;
                }
            }    
            set { lock (m_map) { m_map[key] = value; } }
        }    

        public bool ContainsKey(string key) 
        {    
            lock (m_map)
                return m_map.ContainsKey(key);
        }    

        public void Add(string key, OSD llsd)
        {    
            lock (m_map)
                m_map.Add(key, llsd);
        }    

        public void Add(KeyValuePair<string, OSD> kvp) 
        {    
            lock (m_map)
                m_map.Add(kvp.Key, kvp.Value);
        }    

        public bool Remove(string key) 
        {    
            lock (m_map)
                return m_map.Remove(key);
        }    

        public bool TryGetValue(string key, out OSD llsd)
        {    
            lock (m_map)
                return m_map.TryGetValue(key, out llsd);
        }    

        public void Clear()
        {
            lock (m_map)
                m_map.Clear();
        }  
        
        public bool Contains(KeyValuePair<string, OSD> kvp)
        {
            lock (m_map)
                return m_map.ContainsKey(kvp.Key);
        }

        public void CopyTo(KeyValuePair<string, OSD>[] array, int index)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<string, OSD> kvp)
        {
            lock (m_map)
                return m_map.Remove(kvp.Key);
        }

        public System.Collections.IDictionaryEnumerator GetEnumerator()
        {
            lock (m_map)
                return m_map.GetEnumerator();
        }

        IEnumerator<KeyValuePair<string, OSD>> IEnumerable<KeyValuePair<string, OSD>>.GetEnumerator()
        {
            return null;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (m_map)
                return m_map.GetEnumerator();
        }        
    }
}