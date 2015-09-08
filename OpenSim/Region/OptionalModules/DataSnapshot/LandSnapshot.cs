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
using System.Reflection;
using System.Xml;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;

using OpenSim.Region.CoreModules.World.Land;
using OpenSim.Region.DataSnapshot.Interfaces;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.DataSnapshot.Providers
{
    public class LandSnapshot : IDataSnapshotProvider
    {
        private Scene m_scene = null;
        private DataSnapshotManager m_parent = null;
        //private Dictionary<int, Land> m_landIndexed = new Dictionary<int, Land>();
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private bool m_stale = true;

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
                //if ((curLand.Value.LandData.landFlags & (uint)ParcelFlags.ShowDirectory) == (uint)ParcelFlags.ShowDirectory)
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

            //Brought back from the dead for staleness checks.
            m_scene.EventManager.OnNewClient += OnNewClient;
        }

        public Scene GetParentScene
        {
            get { return m_scene; }
        }

        public XmlNode RequestSnapshotData(XmlDocument nodeFactory)
        {
            ILandChannel landChannel = m_scene.LandChannel;
            List<ILandObject> parcels = landChannel.AllParcels();

            IDwellModule dwellModule = m_scene.RequestModuleInterface<IDwellModule>();

            XmlNode parent = nodeFactory.CreateNode(XmlNodeType.Element, "parceldata", "");
            if (parcels != null)
            {

                //foreach (KeyValuePair<int, Land> curParcel in m_landIndexed)
                foreach (ILandObject parcel_interface in parcels)
                {
                    // Play it safe
                    if (!(parcel_interface is LandObject))
                        continue;

                    LandObject land = (LandObject)parcel_interface;

                    LandData parcel = land.LandData;
                    if (m_parent.ExposureLevel.Equals("all") ||
                        (m_parent.ExposureLevel.Equals("minimum") && 
                        (parcel.Flags & (uint)ParcelFlags.ShowDirectory) == (uint)ParcelFlags.ShowDirectory))
                    {

                        //TODO: make better method of marshalling data from LandData to XmlNode
                        XmlNode xmlparcel = nodeFactory.CreateNode(XmlNodeType.Element, "parcel", "");

                        // Attributes of the parcel node
                        XmlAttribute scripts_attr = nodeFactory.CreateAttribute("scripts");
                        scripts_attr.Value = GetScriptsPermissions(parcel);
                        XmlAttribute build_attr = nodeFactory.CreateAttribute("build");
                        build_attr.Value = GetBuildPermissions(parcel);
                        XmlAttribute public_attr = nodeFactory.CreateAttribute("public");
                        public_attr.Value = GetPublicPermissions(parcel);
                        // Check the category of the Parcel
                        XmlAttribute category_attr = nodeFactory.CreateAttribute("category");
                        category_attr.Value = ((int)parcel.Category).ToString();
                        // Check if the parcel is for sale
                        XmlAttribute forsale_attr = nodeFactory.CreateAttribute("forsale");
                        forsale_attr.Value = CheckForSale(parcel);
                        XmlAttribute sales_attr = nodeFactory.CreateAttribute("salesprice");
                        sales_attr.Value = parcel.SalePrice.ToString();

                        XmlAttribute directory_attr = nodeFactory.CreateAttribute("showinsearch");
                        directory_attr.Value = GetShowInSearch(parcel);
                        //XmlAttribute entities_attr = nodeFactory.CreateAttribute("entities");
                        //entities_attr.Value = land.primsOverMe.Count.ToString();
                        xmlparcel.Attributes.Append(directory_attr);
                        xmlparcel.Attributes.Append(scripts_attr);
                        xmlparcel.Attributes.Append(build_attr);
                        xmlparcel.Attributes.Append(public_attr);
                        xmlparcel.Attributes.Append(category_attr);
                        xmlparcel.Attributes.Append(forsale_attr);
                        xmlparcel.Attributes.Append(sales_attr);
                        //xmlparcel.Attributes.Append(entities_attr);


                        //name, description, area, and UUID
                        XmlNode name = nodeFactory.CreateNode(XmlNodeType.Element, "name", "");
                        name.InnerText = parcel.Name;
                        xmlparcel.AppendChild(name);

                        XmlNode desc = nodeFactory.CreateNode(XmlNodeType.Element, "description", "");
                        desc.InnerText = parcel.Description;
                        xmlparcel.AppendChild(desc);

                        XmlNode uuid = nodeFactory.CreateNode(XmlNodeType.Element, "uuid", "");
                        uuid.InnerText = parcel.GlobalID.ToString();
                        xmlparcel.AppendChild(uuid);

                        XmlNode area = nodeFactory.CreateNode(XmlNodeType.Element, "area", "");
                        area.InnerText = parcel.Area.ToString();
                        xmlparcel.AppendChild(area);

                        //default location
                        XmlNode tpLocation = nodeFactory.CreateNode(XmlNodeType.Element, "location", "");
                        Vector3 loc = parcel.UserLocation;
                        if (loc.Equals(Vector3.Zero)) // This test is moot at this point: the location is wrong by default
                            loc = new Vector3((parcel.AABBMax.X + parcel.AABBMin.X) / 2, (parcel.AABBMax.Y + parcel.AABBMin.Y) / 2, (parcel.AABBMax.Z + parcel.AABBMin.Z) / 2);
                        tpLocation.InnerText = loc.X.ToString() + "/" + loc.Y.ToString() + "/" + loc.Z.ToString();
                        xmlparcel.AppendChild(tpLocation);

                        XmlNode infouuid = nodeFactory.CreateNode(XmlNodeType.Element, "infouuid", "");
                        uint x = (uint)loc.X, y = (uint)loc.Y;
                        findPointInParcel(land, ref x, ref y); // find a suitable spot
                        infouuid.InnerText = Util.BuildFakeParcelID(
                                m_scene.RegionInfo.RegionHandle, x, y).ToString();
                        xmlparcel.AppendChild(infouuid);

                        XmlNode dwell = nodeFactory.CreateNode(XmlNodeType.Element, "dwell", "");
                        if (dwellModule != null)
                            dwell.InnerText = dwellModule.GetDwell(parcel.GlobalID).ToString();
                        else
                            dwell.InnerText = "0";
                        xmlparcel.AppendChild(dwell);

                        //TODO: figure how to figure out teleport system landData.landingType

                        //land texture snapshot uuid
                        if (parcel.SnapshotID != UUID.Zero)
                        {
                            XmlNode textureuuid = nodeFactory.CreateNode(XmlNodeType.Element, "image", "");
                            textureuuid.InnerText = parcel.SnapshotID.ToString();
                            xmlparcel.AppendChild(textureuuid);
                        }

                        string groupName = String.Empty;

                        //attached user and group
                        if (parcel.GroupID != UUID.Zero)
                        {
                            XmlNode groupblock = nodeFactory.CreateNode(XmlNodeType.Element, "group", "");
                            XmlNode groupuuid = nodeFactory.CreateNode(XmlNodeType.Element, "groupuuid", "");
                            groupuuid.InnerText = parcel.GroupID.ToString();
                            groupblock.AppendChild(groupuuid);

                            IGroupsModule gm = m_scene.RequestModuleInterface<IGroupsModule>();
                            if (gm != null)
                            {
                                GroupRecord g = gm.GetGroupRecord(parcel.GroupID);
                                if (g != null)
                                    groupName = g.GroupName;
                            }

                            XmlNode groupname = nodeFactory.CreateNode(XmlNodeType.Element, "groupname", "");
                            groupname.InnerText = groupName;
                            groupblock.AppendChild(groupname);

                            xmlparcel.AppendChild(groupblock);
                        }

                        XmlNode userblock = nodeFactory.CreateNode(XmlNodeType.Element, "owner", "");

                        UUID userOwnerUUID = parcel.OwnerID;

                        XmlNode useruuid = nodeFactory.CreateNode(XmlNodeType.Element, "uuid", "");
                        useruuid.InnerText = userOwnerUUID.ToString();
                        userblock.AppendChild(useruuid);

                        if (!parcel.IsGroupOwned)
                        {
                            try
                            {
                                XmlNode username = nodeFactory.CreateNode(XmlNodeType.Element, "name", "");
                                UserAccount account = m_scene.UserAccountService.GetUserAccount(m_scene.RegionInfo.ScopeID, userOwnerUUID);
                                username.InnerText = account.FirstName + " " + account.LastName;
                                userblock.AppendChild(username);
                            }
                            catch (Exception)
                            {
                                //m_log.Info("[DATASNAPSHOT]: Cannot find owner name; ignoring this parcel");
                            }

                        }
                        else
                        {
                            XmlNode username = nodeFactory.CreateNode(XmlNodeType.Element, "name", "");
                            username.InnerText = groupName;
                            userblock.AppendChild(username);
                        }

                        xmlparcel.AppendChild(userblock);

                        parent.AppendChild(xmlparcel);
                    }
                }
                //snap.AppendChild(parent);
            }

            this.Stale = false;
            return parent;
        }

        public String Name
        {
            get { return "LandSnapshot"; }
        }

        public bool Stale
        {
            get
            {
                return m_stale;
            }
            set
            {
                m_stale = value;

                if (m_stale)
                    OnStale(this);
            }
        }

        public event ProviderStale OnStale;

        #endregion

        #region Helper functions

        private string GetScriptsPermissions(LandData parcel)
        {
            if ((parcel.Flags & (uint)ParcelFlags.AllowOtherScripts) == (uint)ParcelFlags.AllowOtherScripts)
                return "true";
            else
                return "false";

        }

        private string GetPublicPermissions(LandData parcel)
        {
            if ((parcel.Flags & (uint)ParcelFlags.UseAccessList) == (uint)ParcelFlags.UseAccessList)
                return "false";
            else
                return "true";

        }

        private string GetBuildPermissions(LandData parcel)
        {
            if ((parcel.Flags & (uint)ParcelFlags.CreateObjects) == (uint)ParcelFlags.CreateObjects)
                return "true";
            else
                return "false";

        }

        private string CheckForSale(LandData parcel)
        {
            if ((parcel.Flags & (uint)ParcelFlags.ForSale) == (uint)ParcelFlags.ForSale)
                return "true";
            else
                return "false";
        }

        private string GetShowInSearch(LandData parcel)
        {
            if ((parcel.Flags & (uint)ParcelFlags.ShowDirectory) == (uint)ParcelFlags.ShowDirectory)
                return "true";
            else
                return "false";

        }

        #endregion

        #region Change detection hooks

        public void OnNewClient(IClientAPI client)
        {
            //Land hooks
            client.OnParcelDivideRequest += delegate(int west, int south, int east, int north,
                IClientAPI remote_client) { this.Stale = true; };
            client.OnParcelJoinRequest += delegate(int west, int south, int east, int north,
                IClientAPI remote_client) { this.Stale = true; };
            client.OnParcelPropertiesUpdateRequest += delegate(LandUpdateArgs args, int local_id,
                IClientAPI remote_client) { this.Stale = true; };
            client.OnParcelBuy += delegate(UUID agentId, UUID groupId, bool final, bool groupOwned,
                bool removeContribution, int parcelLocalID, int parcelArea, int parcelPrice, bool authenticated)
            { this.Stale = true; };
        }

        public void ParcelSplitHook(int west, int south, int east, int north, IClientAPI remote_client)
        {
            this.Stale = true;
        }

        public void ParcelPropsHook(LandUpdateArgs args, int local_id, IClientAPI remote_client)
        {
            this.Stale = true;
        }

        #endregion

        // this is needed for non-convex parcels (example: rectangular parcel, and in the exact center
        // another, smaller rectangular parcel). Both will have the same initial coordinates.
        private void findPointInParcel(ILandObject land, ref uint refX, ref uint refY)
        {
            m_log.DebugFormat("[DATASNAPSHOT] trying {0}, {1}", refX, refY);
            // the point we started with already is in the parcel
            if (land.ContainsPoint((int)refX, (int)refY)) return;

            // ... otherwise, we have to search for a point within the parcel
            uint startX = (uint)land.LandData.AABBMin.X;
            uint startY = (uint)land.LandData.AABBMin.Y;
            uint endX = (uint)land.LandData.AABBMax.X;
            uint endY = (uint)land.LandData.AABBMax.Y;

            // default: center of the parcel
            refX = (startX + endX) / 2;
            refY = (startY + endY) / 2;
            // If the center point is within the parcel, take that one
            if (land.ContainsPoint((int)refX, (int)refY)) return;

            // otherwise, go the long way.
            for (uint y = startY; y <= endY; ++y)
            {
                for (uint x = startX; x <= endX; ++x)
                {
                    if (land.ContainsPoint((int)x, (int)y))
                    {
                        // found a point
                        refX = x;
                        refY = y;
                        return;
                    }
                }
            }
        }
    }
}

