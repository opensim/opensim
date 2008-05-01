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
using System.Xml;
using libsecondlife;
using OpenSim.Region.DataSnapshot.Interfaces;
using OpenSim.Region.Environment.Scenes;

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
