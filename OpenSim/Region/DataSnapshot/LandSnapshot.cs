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
* 
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Reflection;

using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Modules.LandManagement;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Communications;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim.Region.DataSnapshot
{
    public class LandSnapshot : IDataSnapshotProvider
    {
        private Scene m_scene = null;
        private DataSnapshotManager m_parent = null;
        //private Dictionary<int, Land> m_landIndexed = new Dictionary<int, Land>();
        private log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        #region Dead code

        /*
         * David, I don't think we need this at all. When we do the snapshot, we can
         * simply look into the parcels that are marked for ShowDirectory -- see 
         * conditional in RequestSnapshotData
         * 
        //Revise this, look for more direct way of checking for change in land
        #region Client hooks

        public void OnNewClient(IClientAPI client)
        {
            //Land hooks
            client.OnParcelDivideRequest += ParcelSplitHook;
            client.OnParcelJoinRequest += ParcelSplitHook;
            client.OnParcelPropertiesUpdateRequest += ParcelPropsHook;
        }

        public void ParcelSplitHook(int west, int south, int east, int north, IClientAPI remote_client)
        {
            PrepareData();
        }

        public void ParcelPropsHook(ParcelPropertiesUpdatePacket packet, IClientAPI remote_client)
        {
            PrepareData();
        }

        #endregion

        public void PrepareData()
        {
            m_log.Info("[EXTERNALDATA]: Generating land data.");

            m_landIndexed.Clear();

            //Index sim land
            foreach (KeyValuePair<int, Land> curLand in m_scene.LandManager.landList)
            {
                //if ((curLand.Value.landData.landFlags & (uint)Parcel.ParcelFlags.ShowDirectory) == (uint)Parcel.ParcelFlags.ShowDirectory)
                //{
                    m_landIndexed.Add(curLand.Key, curLand.Value.Copy());
                //}
            }
        }

        public Dictionary<int,Land> IndexedLand {
            get { return m_landIndexed; }
        }
        */

        #endregion

        #region IDataSnapshotProvider members

        public void Initialize(Scene scene, DataSnapshotManager parent)
        {
            m_scene = scene;
            m_parent = parent;
            //m_scene.EventManager.OnNewClient += OnNewClient;
        }

        public Scene GetParentScene
        {
            get { return m_scene; }
        }

        public XmlNode RequestSnapshotData(XmlDocument nodeFactory)
        {   
            ILandChannel landChannel = (LandChannel)m_scene.LandChannel;
            Dictionary<int, ILandObject> landList = null;
            try
            {
                Type landChannelType = typeof(LandChannel);
                FieldInfo landListField = landChannelType.GetField("landList", BindingFlags.NonPublic | BindingFlags.Instance);
                if (landListField != null)
                {
                    landList = (Dictionary<int, ILandObject>)landListField.GetValue(landChannel);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("[DATASNAPSHOT] couldn't access field reflectively\n" + e.ToString());
            }
            XmlNode parent = nodeFactory.CreateNode(XmlNodeType.Element, "parceldata", "");
            if (landList != null)
            {

                //foreach (KeyValuePair<int, Land> curParcel in m_landIndexed)
                foreach (LandObject land in landList.Values)
                {
                    LandData parcel = land.landData;
                    if ((parcel.landFlags & (uint)Parcel.ParcelFlags.ShowDirectory) == (uint)Parcel.ParcelFlags.ShowDirectory)
                    {

                        //TODO: make better method of marshalling data from LandData to XmlNode
                        XmlNode xmlparcel = nodeFactory.CreateNode(XmlNodeType.Element, "parcel", "");

                        // Attributes of the parcel node
                        XmlAttribute scripts_attr = nodeFactory.CreateAttribute("scripts");
                        scripts_attr.Value = GetScriptsPermissions(parcel);
                        XmlAttribute category_attr = nodeFactory.CreateAttribute("category");
                        category_attr.Value = parcel.category.ToString();
                        //XmlAttribute entities_attr = nodeFactory.CreateAttribute("entities");
                        //entities_attr.Value = land.primsOverMe.Count.ToString();
                        xmlparcel.Attributes.Append(scripts_attr);
                        xmlparcel.Attributes.Append(category_attr);
                        //xmlparcel.Attributes.Append(entities_attr);


                        //name, description, area, and UUID
                        XmlNode name = nodeFactory.CreateNode(XmlNodeType.Element, "name", "");
                        name.InnerText = parcel.landName;
                        xmlparcel.AppendChild(name);

                        XmlNode desc = nodeFactory.CreateNode(XmlNodeType.Element, "description", "");
                        desc.InnerText = parcel.landDesc;
                        xmlparcel.AppendChild(desc);

                        XmlNode uuid = nodeFactory.CreateNode(XmlNodeType.Element, "uuid", "");
                        uuid.InnerText = parcel.globalID.ToString();
                        xmlparcel.AppendChild(uuid);

                        XmlNode area = nodeFactory.CreateNode(XmlNodeType.Element, "area", "");
                        area.InnerText = parcel.area.ToString();
                        xmlparcel.AppendChild(area);

                        //default location
                        XmlNode tpLocation = nodeFactory.CreateNode(XmlNodeType.Element, "location", "");
                        LLVector3 loc = parcel.userLocation;
                        if (loc.Equals(LLVector3.Zero)) // This test is mute at this point: the location is wrong by default
                            loc = new LLVector3((parcel.AABBMax.X - parcel.AABBMin.X) / 2, (parcel.AABBMax.Y - parcel.AABBMin.Y) / 2, (parcel.AABBMax.Y - parcel.AABBMin.Y) / 2);
                        tpLocation.InnerText = loc.X.ToString() + "/" + loc.Y.ToString() + "/" + loc.Z.ToString();
                        xmlparcel.AppendChild(tpLocation);

                        //TODO: figure how to figure out teleport system landData.landingType

                        //land texture snapshot uuid
                        if (parcel.snapshotID != LLUUID.Zero)
                        {
                            XmlNode textureuuid = nodeFactory.CreateNode(XmlNodeType.Element, "image", "");
                            textureuuid.InnerText = parcel.snapshotID.ToString();
                            xmlparcel.AppendChild(textureuuid);
                        }

                        //attached user and group
                        if (parcel.groupID != LLUUID.Zero)
                        {
                            XmlNode groupblock = nodeFactory.CreateNode(XmlNodeType.Element, "group", "");
                            XmlNode groupuuid = nodeFactory.CreateNode(XmlNodeType.Element, "uuid", "");
                            groupuuid.InnerText = parcel.groupID.ToString();
                            groupblock.AppendChild(groupuuid);

                            //No name yet, there's no way to get a group name since they don't exist yet.
                            //TODO: When groups are supported, add the group handling code.

                            xmlparcel.AppendChild(groupblock);
                        }

                        if (!parcel.isGroupOwned)
                        {
                            XmlNode userblock = nodeFactory.CreateNode(XmlNodeType.Element, "owner", "");

                            LLUUID userOwnerUUID = parcel.ownerID;

                            XmlNode useruuid = nodeFactory.CreateNode(XmlNodeType.Element, "uuid", "");
                            useruuid.InnerText = userOwnerUUID.ToString();
                            userblock.AppendChild(useruuid);

                            try
                            {
                                XmlNode username = nodeFactory.CreateNode(XmlNodeType.Element, "name", "");
                                UserProfileData userProfile = m_scene.CommsManager.UserService.GetUserProfile(userOwnerUUID);
                                username.InnerText = userProfile.username + " " + userProfile.surname;
                                userblock.AppendChild(username);
                            }
                            catch (Exception)
                            {
                                m_log.Info("[DATASNAPSHOT]: Cannot find owner name; ignoring this parcel");
                            }

                            xmlparcel.AppendChild(userblock);
                        }
                        //else
                        //{
                        //    XmlAttribute type = (XmlAttribute)nodeFactory.CreateNode(XmlNodeType.Attribute, "type", "");
                        //    type.InnerText = "owner";
                        //    groupblock.Attributes.Append(type);
                        //}

                        parent.AppendChild(xmlparcel);
                    }
                }
                //snap.AppendChild(parent);
            }

            return parent;
        }

        #endregion

        #region Helper functions

        private string GetScriptsPermissions(LandData parcel)
        {
            if ((parcel.landFlags & (uint)Parcel.ParcelFlags.AllowOtherScripts) == (uint)Parcel.ParcelFlags.AllowOtherScripts)
                return "yes";
            else
                return "no";

        }

        #endregion
    }
}
