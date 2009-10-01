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
using System.Text;
using System.Drawing;
using System.Net;
using System.Reflection;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenSim.Framework;

using OpenMetaverse;
using OpenMetaverse.Imaging;
using log4net;
using Nwc.XmlRpc;

namespace OpenSim.Services.Connectors.Grid
{
    public class HypergridServiceConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IAssetService m_AssetService;

        public HypergridServiceConnector(IAssetService assService)
        {
            m_AssetService = assService;
        }

        public UUID LinkRegion(GridRegion info, out ulong realHandle)
        {
            UUID uuid = UUID.Zero;
            realHandle = 0;

            Hashtable hash = new Hashtable();
            hash["region_name"] = info.RegionName;

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("link_region", paramList);
            string uri = "http://" + info.ExternalEndPoint.Address + ":" + info.HttpPort + "/";
            m_log.Debug("[HGrid]: Linking to " + uri);
            XmlRpcResponse response = null;
            try
            {
                response = request.Send(uri, 10000);
            }
            catch (Exception e)
            {
                m_log.Debug("[HGrid]: Exception " + e.Message);
                return uuid;
            }

            if (response.IsFault)
            {
                m_log.ErrorFormat("[HGrid]: remote call returned an error: {0}", response.FaultString);
            }
            else
            {
                hash = (Hashtable)response.Value;
                //foreach (Object o in hash)
                //    m_log.Debug(">> " + ((DictionaryEntry)o).Key + ":" + ((DictionaryEntry)o).Value);
                try
                {
                    UUID.TryParse((string)hash["uuid"], out uuid);
                    m_log.Debug(">> HERE, uuid: " + uuid);
                    info.RegionID = uuid;
                    if ((string)hash["handle"] != null)
                    {
                        realHandle = Convert.ToUInt64((string)hash["handle"]);
                        m_log.Debug(">> HERE, realHandle: " + realHandle);
                    }
                    //if (hash["region_image"] != null)
                    //{
                    //    UUID img = UUID.Zero;
                    //    UUID.TryParse((string)hash["region_image"], out img);
                    //    info.RegionSettings.TerrainImageID = img;
                    //}
                    if (hash["region_name"] != null)
                    {
                        info.RegionName = (string)hash["region_name"];
                        //m_log.Debug(">> " + info.RegionName);
                    }
                    if (hash["internal_port"] != null)
                    {
                        int port = Convert.ToInt32((string)hash["internal_port"]);
                        info.InternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), port);
                        //m_log.Debug(">> " + info.InternalEndPoint.ToString());
                    }

                }
                catch (Exception e)
                {
                    m_log.Error("[HGrid]: Got exception while parsing hyperlink response " + e.StackTrace);
                }
            }
            return uuid;
        }

        public void GetMapImage(GridRegion info)
        {
            try
            {
                string regionimage = "regionImage" + info.RegionID.ToString();
                regionimage = regionimage.Replace("-", "");

                WebClient c = new WebClient();
                string uri = "http://" + info.ExternalHostName + ":" + info.HttpPort + "/index.php?method=" + regionimage;
                //m_log.Debug("JPEG: " + uri);
                c.DownloadFile(uri, info.RegionID.ToString() + ".jpg");
                Bitmap m = new Bitmap(info.RegionID.ToString() + ".jpg");
                //m_log.Debug("Size: " + m.PhysicalDimension.Height + "-" + m.PhysicalDimension.Width);
                byte[] imageData = OpenJPEG.EncodeFromImage(m, true);
                AssetBase ass = new AssetBase(UUID.Random(), "region " + info.RegionID.ToString());
                
                // !!! for now
                //info.RegionSettings.TerrainImageID = ass.FullID;

                ass.Type = (int)AssetType.Texture;
                ass.Temporary = true;
                ass.Local = true;
                ass.Data = imageData;

                m_AssetService.Store(ass);

            }
            catch // LEGIT: Catching problems caused by OpenJPEG p/invoke
            {
                m_log.Warn("[HGrid]: Failed getting/storing map image, because it is probably already in the cache");
            }
        }

        public bool InformRegionOfUser(GridRegion regInfo, AgentCircuitData agentData, GridRegion home, string userServer, string assetServer, string inventoryServer)
        {
            string capsPath = agentData.CapsPath;
            Hashtable loginParams = new Hashtable();
            loginParams["session_id"] = agentData.SessionID.ToString();

            loginParams["firstname"] = agentData.firstname;
            loginParams["lastname"] = agentData.lastname;

            loginParams["agent_id"] = agentData.AgentID.ToString();
            loginParams["circuit_code"] = agentData.circuitcode.ToString();
            loginParams["startpos_x"] = agentData.startpos.X.ToString();
            loginParams["startpos_y"] = agentData.startpos.Y.ToString();
            loginParams["startpos_z"] = agentData.startpos.Z.ToString();
            loginParams["caps_path"] = capsPath;

            if (home != null)
            {
                loginParams["region_uuid"] = home.RegionID.ToString();
                loginParams["regionhandle"] = home.RegionHandle.ToString();
                loginParams["home_address"] = home.ExternalHostName;
                loginParams["home_port"] = home.HttpPort.ToString();
                loginParams["internal_port"] = home.InternalEndPoint.Port.ToString();

                m_log.Debug("  ---------     Home     -------");
                m_log.Debug("  >> " + loginParams["home_address"] + " <<");
                m_log.Debug("  >> " + loginParams["region_uuid"] + " <<");
                m_log.Debug("  >> " + loginParams["regionhandle"] + " <<");
                m_log.Debug("  >> " + loginParams["home_port"] + " <<");
                m_log.Debug("  --------- ------------ -------");
            }
            else
                m_log.WarnFormat("[HGrid]: Home region not found for {0} {1}", agentData.firstname, agentData.lastname);

            loginParams["userserver_id"] = userServer;
            loginParams["assetserver_id"] = assetServer;
            loginParams["inventoryserver_id"] = inventoryServer;


            ArrayList SendParams = new ArrayList();
            SendParams.Add(loginParams);

            // Send
            string uri = "http://" + regInfo.ExternalHostName + ":" + regInfo.HttpPort + "/";
            //m_log.Debug("XXX uri: " + uri);
            XmlRpcRequest request = new XmlRpcRequest("expect_hg_user", SendParams);
            XmlRpcResponse reply;
            try
            {
                reply = request.Send(uri, 6000);
            }
            catch (Exception e)
            {
                m_log.Warn("[HGrid]: Failed to notify region about user. Reason: " + e.Message);
                return false;
            }

            if (!reply.IsFault)
            {
                bool responseSuccess = true;
                if (reply.Value != null)
                {
                    Hashtable resp = (Hashtable)reply.Value;
                    if (resp.ContainsKey("success"))
                    {
                        if ((string)resp["success"] == "FALSE")
                        {
                            responseSuccess = false;
                        }
                    }
                }
                if (responseSuccess)
                {
                    m_log.Info("[HGrid]: Successfully informed remote region about user " + agentData.AgentID);
                    return true;
                }
                else
                {
                    m_log.ErrorFormat("[HGrid]: Region responded that it is not available to receive clients");
                    return false;
                }
            }
            else
            {
                m_log.ErrorFormat("[HGrid]: XmlRpc request to region failed with message {0}, code {1} ", reply.FaultString, reply.FaultCode);
                return false;
            }
        }

    }
}
