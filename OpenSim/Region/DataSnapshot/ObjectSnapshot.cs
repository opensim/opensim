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

using System.Reflection;
using System.Xml;
using log4net;
using OpenSim.Region.DataSnapshot.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.DataSnapshot
{
    public class ObjectSnapshot : IDataSnapshotProvider
    {
        private Scene m_scene = null;
        private DataSnapshotManager m_parent = null;
        private ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public void Initialize(Scene scene, DataSnapshotManager parent)
        {
            m_scene = scene;
            m_parent = parent;
        }

        public Scene GetParentScene
        {
            get { return m_scene; }
        }

        public XmlNode RequestSnapshotData(XmlDocument nodeFactory)
        {
            XmlNode parent = nodeFactory.CreateNode(XmlNodeType.Element, "objectdata", "");
            XmlNode node;
#if LIBSL_IS_FIXED
            foreach (EntityBase entity in m_scene.Entities.Values)
            {
                // only objects, not avatars
                if (entity is SceneObjectGroup) 
                {
                    SceneObjectGroup obj = (SceneObjectGroup)entity;

                    XmlNode xmlobject = nodeFactory.CreateNode(XmlNodeType.Element, "object", "");

                    node = nodeFactory.CreateNode(XmlNodeType.Element, "uuid", "");
                    node.InnerText = obj.UUID.ToString();
                    xmlobject.AppendChild(node);

                    SceneObjectPart m_rootPart = null;
                    try
                    {
                        Type sog = typeof(SceneObjectGroup);
                        FieldInfo rootField = sog.GetField("m_rootPart", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (rootField != null)
                        {
                            m_rootPart = (SceneObjectPart)rootField.GetValue(obj);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("[DATASNAPSHOT] couldn't access field reflectively\n" + e.ToString());
                    }
                    if (m_rootPart != null)
                    {
                        node = nodeFactory.CreateNode(XmlNodeType.Element, "title", "");
                        node.InnerText = m_rootPart.Name;
                        xmlobject.AppendChild(node);

                        node = nodeFactory.CreateNode(XmlNodeType.Element, "description", "");
                        node.InnerText = m_rootPart.Description;
                        xmlobject.AppendChild(node);

                        node = nodeFactory.CreateNode(XmlNodeType.Element, "flags", "");
                        node.InnerText = String.Format("{0:x}", m_rootPart.ObjectFlags);
                        xmlobject.AppendChild(node);
                    }
                    parent.AppendChild(xmlobject);
                }
            }
#endif
            return parent;

        }
    }
}
