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
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Xml;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Framework.Scenes.Serialization
{
    /// <summary>
    /// Serialize and deserialize coalesced scene objects.
    /// </summary>
    /// <remarks>
    /// Deserialization not yet here.
    /// </remarks>
    public class CoalescedSceneObjectsSerializer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);        

        /// <summary>
        /// Serialize coalesced objects to Xml
        /// </summary>
        /// <param name="coa"></param>
        /// <returns></returns>
        public static string ToXml(CoalescedSceneObjects coa)
        {
            // TODO: Should probably return an empty xml serialization rather than a blank string
            if (!coa.HasObjects)
                return "";
            
            using (StringWriter sw = new StringWriter())
            {
                using (XmlTextWriter writer = new XmlTextWriter(sw))
                {                                        
                    Vector3 size;
                    
                    List<SceneObjectGroup> coaObjects = coa.Objects;
                    
//                    m_log.DebugFormat(
//                        "[COALESCED SCENE OBJECTS SERIALIZER]: Writing {0} objects for coalesced object", 
//                        coaObjects.Count);
                    
                    // This is weak - we're relying on the set of coalesced objects still being identical
                    Vector3[] offsets = coa.GetSizeAndOffsets(out size);

                    writer.WriteStartElement("CoalescedObject");
                    
                    writer.WriteAttributeString("x", size.X.ToString());
                    writer.WriteAttributeString("y", size.Y.ToString());
                    writer.WriteAttributeString("z", size.Z.ToString());                    
                    
                    // Embed the offsets into the group XML
                    for (int i = 0; i < coaObjects.Count; i++)
                    {
                        SceneObjectGroup obj = coaObjects[i];
                        
//                        m_log.DebugFormat(
//                            "[COALESCED SCENE OBJECTS SERIALIZER]: Writing offset for object {0}, {1}", 
//                            i, obj.Name);                        
                        
                        writer.WriteStartElement("SceneObjectGroup");
                        writer.WriteAttributeString("offsetx", offsets[i].X.ToString());
                        writer.WriteAttributeString("offsety", offsets[i].Y.ToString());
                        writer.WriteAttributeString("offsetz", offsets[i].Z.ToString());
                        
                        SceneObjectSerializer.ToOriginalXmlFormat(obj, writer, true);
                        
                        writer.WriteEndElement(); // SceneObjectGroup
                    }  
                    
                    writer.WriteEndElement(); // CoalescedObject
                }

                string output = sw.ToString();
                
//                m_log.Debug(output);
                
                return output;
            }
        }
    }
}