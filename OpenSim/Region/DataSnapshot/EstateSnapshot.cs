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
using System.Xml;
using OpenMetaverse;
using OpenSim.Framework;

using OpenSim.Region.DataSnapshot.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.DataSnapshot.Providers
{
    public class EstateSnapshot : IDataSnapshotProvider
    {
        /* This module doesn't check for changes, since it's *assumed* there are none.
         * Nevertheless, it's possible to have changes, since all the fields are public.
         * There's no event to subscribe to. :/
         *
         * I don't think anything changes the fields beyond RegionModule PostInit, however.
         */
        private Scene m_scene = null;
        // private DataSnapshotManager m_parent = null;
        private bool m_stale = true;

        #region IDataSnapshotProvider Members

        public XmlNode RequestSnapshotData(XmlDocument factory)
        {
            //Estate data section - contains who owns a set of sims and the name of the set.
            //Now in DataSnapshotProvider module form!
            XmlNode estatedata = factory.CreateNode(XmlNodeType.Element, "estate", "");

            UUID ownerid = m_scene.RegionInfo.EstateSettings.EstateOwner;

            UserAccount userInfo = m_scene.UserAccountService.GetUserAccount(m_scene.RegionInfo.ScopeID, ownerid);
            //TODO: Change to query userserver about the master avatar UUID ?
            String firstname;
            String lastname;

            if (userInfo != null)
            {
                firstname = userInfo.FirstName;
                lastname = userInfo.LastName;

                //TODO: Fix the marshalling system to have less copypasta gruntwork
                XmlNode user = factory.CreateNode(XmlNodeType.Element, "user", "");
//                XmlAttribute type = (XmlAttribute)factory.CreateNode(XmlNodeType.Attribute, "type", "");
//                type.Value = "owner";
//                user.Attributes.Append(type);

                //TODO: Create more TODOs
                XmlNode username = factory.CreateNode(XmlNodeType.Element, "name", "");
                username.InnerText = firstname + " " + lastname;
                user.AppendChild(username);

                XmlNode useruuid = factory.CreateNode(XmlNodeType.Element, "uuid", "");
                useruuid.InnerText = ownerid.ToString();
                user.AppendChild(useruuid);

                estatedata.AppendChild(user);
            }

            XmlNode estatename = factory.CreateNode(XmlNodeType.Element, "name", "");
            estatename.InnerText = m_scene.RegionInfo.EstateSettings.EstateName.ToString();
            estatedata.AppendChild(estatename);

            XmlNode estateid = factory.CreateNode(XmlNodeType.Element, "id", "");
            estateid.InnerText = m_scene.RegionInfo.EstateSettings.EstateID.ToString();
            estatedata.AppendChild(estateid);

            XmlNode parentid = factory.CreateNode(XmlNodeType.Element, "parentid", "");
            parentid.InnerText = m_scene.RegionInfo.EstateSettings.ParentEstateID.ToString();
            estatedata.AppendChild(parentid);

            XmlNode flags = factory.CreateNode(XmlNodeType.Element, "flags", "");

            XmlAttribute teleport = (XmlAttribute)factory.CreateNode(XmlNodeType.Attribute, "teleport", "");
            teleport.Value = m_scene.RegionInfo.EstateSettings.AllowDirectTeleport.ToString();
            flags.Attributes.Append(teleport);

            XmlAttribute publicaccess = (XmlAttribute)factory.CreateNode(XmlNodeType.Attribute, "public", "");
            publicaccess.Value = m_scene.RegionInfo.EstateSettings.PublicAccess.ToString();
            flags.Attributes.Append(publicaccess);

            estatedata.AppendChild(flags);

            this.Stale = false;
            return estatedata;
        }

        public void Initialize(Scene scene, DataSnapshotManager parent)
        {
            m_scene = scene;
            // m_parent = parent;
        }

        public Scene GetParentScene
        {
            get { return m_scene; }
        }

        public String Name {
            get { return "EstateSnapshot"; }
        }

        public bool Stale
        {
            get {
                return m_stale;
            }
            set {
                m_stale = value;

                if (m_stale)
                    OnStale(this);
            }
        }

        public event ProviderStale OnStale;

        #endregion
    }
}
