using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Reflection;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework;
using libsecondlife;

namespace OpenSim.Region.DataSnapshot
{
    public class ObjectSnapshot : IDataSnapshotProvider
    {
        private Scene m_scene = null;
        private DataSnapshotManager m_parent = null;
        private log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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
