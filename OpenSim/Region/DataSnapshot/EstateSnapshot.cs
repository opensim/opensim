using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Modules.LandManagement;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Communications;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim.Region.DataSnapshot
{
    public class EstateSnapshot : IDataSnapshotProvider
    {
        private Scene m_scene = null;
        private DataSnapshotManager m_parent = null;

        #region IDataSnapshotProvider Members

        public XmlNode RequestSnapshotData(XmlDocument factory)
        {
            //Estate data section - contains who owns a set of sims and the name of the set.
            //In Opensim all the estate names are the same as the Master Avatar (owner of the sim)
            //Now in DataSnapshotProvider module form!
            XmlNode estatedata = factory.CreateNode(XmlNodeType.Element, "estate", "");

            LLUUID ownerid = m_scene.RegionInfo.MasterAvatarAssignedUUID;

            //TODO: Change to query userserver about the master avatar UUID ?
            String firstname = m_scene.RegionInfo.MasterAvatarFirstName;
            String lastname = m_scene.RegionInfo.MasterAvatarLastName;

            //TODO: Fix the marshalling system to have less copypasta gruntwork
            XmlNode user = factory.CreateNode(XmlNodeType.Element, "user", "");
            XmlAttribute type = (XmlAttribute)factory.CreateNode(XmlNodeType.Attribute, "type", "");
            type.Value = "owner";
            user.Attributes.Append(type);

            //TODO: Create more TODOs
            XmlNode username = factory.CreateNode(XmlNodeType.Element, "name", "");
            username.InnerText = firstname + " " + lastname;
            user.AppendChild(username);

            XmlNode useruuid = factory.CreateNode(XmlNodeType.Element, "uuid", "");
            useruuid.InnerText = ownerid.ToString();
            user.AppendChild(useruuid);

            estatedata.AppendChild(user);

            return estatedata;
        }

        public void Initialize(Scene scene, DataSnapshotManager parent)
        {
            m_scene = scene;
            m_parent = parent;
        }

        public Scene GetParentScene
        {
            get { return m_scene; }
        }

        #endregion
    }
}
