/*
Copyright (c) OpenGrid project, http://osgrid.org/


* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Console;
using OpenSim.Framework.Sims;
using Db4objects.Db4o;
using Nwc.XmlRpc;
using System.Xml;

namespace OpenGridServices.GridServer
{
    /// <summary>
    /// </summary>
    public class SimProfileManager
    {

        public Dictionary<LLUUID, SimProfileBase> SimProfiles = new Dictionary<LLUUID, SimProfileBase>();
        private OpenGrid_Main m_gridManager;

        public SimProfileManager(OpenGrid_Main gridManager)
        {
            m_gridManager = gridManager;
        }

        public void LoadProfiles()
        {		// should abstract this out
            IObjectContainer db;
            db = Db4oFactory.OpenFile("simprofiles.yap");
            IObjectSet result = db.Get(typeof(SimProfileBase));
            foreach (SimProfileBase simprof in result)
            {
                SimProfiles.Add(simprof.UUID, simprof);
            }
            MainConsole.Instance.WriteLine("SimProfiles.Cs:LoadProfiles() - Successfully loaded " + result.Count.ToString() + " from database");
            db.Close();
        }

        public SimProfileBase GetProfileByHandle(ulong reqhandle)
        {
            foreach (libsecondlife.LLUUID UUID in SimProfiles.Keys)
            {
                if (SimProfiles[UUID].regionhandle == reqhandle) return SimProfiles[UUID];
            }
            return null;
        }

        public SimProfileBase GetProfileByLLUUID(LLUUID ProfileLLUUID)
        {
            foreach (libsecondlife.LLUUID UUID in SimProfiles.Keys)
            {
                if (SimProfiles[UUID].UUID == ProfileLLUUID) return SimProfiles[UUID];
            }
            return null;
        }

        public bool AuthenticateSim(LLUUID RegionUUID, uint regionhandle, string simrecvkey)
        {
            SimProfileBase TheSim = GetProfileByHandle(regionhandle);
            if (TheSim != null)
                if (TheSim.recvkey == simrecvkey)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            else return false;

        }

        public string GetXMLNeighbours(ulong reqhandle)
        {
            string response = "";
            SimProfileBase central_region = GetProfileByHandle(reqhandle);
            SimProfileBase neighbour;
            for (int x = -1; x < 2; x++) for (int y = -1; y < 2; y++)
                {
                    if (GetProfileByHandle(Util.UIntsToLong((uint)((central_region.RegionLocX + x) * 256), (uint)(central_region.RegionLocY + y) * 256)) != null)
                    {
                        neighbour = GetProfileByHandle(Util.UIntsToLong((uint)((central_region.RegionLocX + x) * 256), (uint)(central_region.RegionLocY + y) * 256));
                        response += "<neighbour>";
                        response += "<sim_ip>" + neighbour.sim_ip + "</sim_ip>";
                        response += "<sim_port>" + neighbour.sim_port.ToString() + "</sim_port>";
                        response += "<locx>" + neighbour.RegionLocX.ToString() + "</locx>";
                        response += "<locy>" + neighbour.RegionLocY.ToString() + "</locy>";
                        response += "<regionhandle>" + neighbour.regionhandle.ToString() + "</regionhandle>";
                        response += "</neighbour>";

                    }
                }
            return response;
        }

        public SimProfileBase CreateNewProfile(string regionname, string caps_url, string sim_ip, uint sim_port, uint RegionLocX, uint RegionLocY, string sendkey, string recvkey)
        {
            SimProfileBase newprofile = new SimProfileBase();
            newprofile.regionname = regionname;
            newprofile.sim_ip = sim_ip;
            newprofile.sim_port = sim_port;
            newprofile.RegionLocX = RegionLocX;
            newprofile.RegionLocY = RegionLocY;
            newprofile.caps_url = "http://" + sim_ip + ":9000/";
            newprofile.sendkey = sendkey;
            newprofile.recvkey = recvkey;
            newprofile.regionhandle = Util.UIntsToLong((RegionLocX * 256), (RegionLocY * 256));
            newprofile.UUID = LLUUID.Random();
            this.SimProfiles.Add(newprofile.UUID, newprofile);
            return newprofile;
        }

        public XmlRpcResponse XmlRpcLoginToSimulatorMethod(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            SimProfileBase TheSim = null;
            Hashtable requestData = (Hashtable)request.Params[0];

            if (requestData.ContainsKey("UUID"))
            {
                TheSim = GetProfileByLLUUID(new LLUUID((string)requestData["UUID"]));
            }
            else if (requestData.ContainsKey("region_handle"))
            {
                TheSim = GetProfileByHandle((ulong)Convert.ToUInt64(requestData["region_handle"]));
            }

            if (TheSim == null)
            {
                responseData["error"] = "sim not found";
            }
            else
            {

                ArrayList SimNeighboursData = new ArrayList();

                SimProfileBase neighbour;
                Hashtable NeighbourBlock;
                for (int x = -1; x < 2; x++) for (int y = -1; y < 2; y++)
                    {
                        if (GetProfileByHandle(Helpers.UIntsToLong((uint)((TheSim.RegionLocX + x) * 256), (uint)(TheSim.RegionLocY + y) * 256)) != null)
                        {
                            neighbour = GetProfileByHandle(Helpers.UIntsToLong((uint)((TheSim.RegionLocX + x) * 256), (uint)(TheSim.RegionLocY + y) * 256));

                            NeighbourBlock = new Hashtable();
                            NeighbourBlock["sim_ip"] = neighbour.sim_ip;
                            NeighbourBlock["sim_port"] = neighbour.sim_port.ToString();
                            NeighbourBlock["region_locx"] = neighbour.RegionLocX.ToString();
                            NeighbourBlock["region_locy"] = neighbour.RegionLocY.ToString();
                            NeighbourBlock["UUID"] = neighbour.UUID.ToString();

                            if (neighbour.UUID != TheSim.UUID) SimNeighboursData.Add(NeighbourBlock);
                        }
                    }

                responseData["UUID"] = TheSim.UUID.ToString();
                responseData["region_locx"] = TheSim.RegionLocX.ToString();
                responseData["region_locy"] = TheSim.RegionLocY.ToString();
                responseData["regionname"] = TheSim.regionname;
                responseData["estate_id"] = "1";
                responseData["neighbours"] = SimNeighboursData;

                responseData["sim_ip"] = TheSim.sim_ip;
                responseData["sim_port"] = TheSim.sim_port.ToString();
                responseData["asset_url"] = m_gridManager.Cfg.DefaultAssetServer;
                responseData["asset_sendkey"] = m_gridManager.Cfg.AssetSendKey;
                responseData["asset_recvkey"] = m_gridManager.Cfg.AssetRecvKey;
                responseData["user_url"] = m_gridManager.Cfg.DefaultUserServer;
                responseData["user_sendkey"] = m_gridManager.Cfg.UserSendKey;
                responseData["user_recvkey"] = m_gridManager.Cfg.UserRecvKey;
                responseData["authkey"] = m_gridManager.Cfg.SimSendKey;
            }

            return response;
        }

        public string RestSetSimMethod(string request, string path, string param)
        {
            Console.WriteLine("SimProfiles.cs:RestSetSimMethod() - processing request......");
            SimProfileBase TheSim;
            TheSim = GetProfileByLLUUID(new LLUUID(param));
            if ((TheSim) == null)
            {
                TheSim = new SimProfileBase();
                LLUUID UUID = new LLUUID(param);
                TheSim.UUID = UUID;
            }

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(request);
            XmlNode rootnode = doc.FirstChild;
            XmlNode authkeynode = rootnode.ChildNodes[0];
            if (authkeynode.Name != "authkey")
            {
                return "ERROR! bad XML - expected authkey tag";
            }

            XmlNode simnode = rootnode.ChildNodes[1];
            if (simnode.Name != "sim")
            {
                return "ERROR! bad XML - expected sim tag";
            }

            if (authkeynode.InnerText != m_gridManager.Cfg.SimRecvKey)
            {
                return "ERROR! invalid key";
            }
            for (int i = 0; i < simnode.ChildNodes.Count; i++)
            {
                switch (simnode.ChildNodes[i].Name)
                {
                    case "regionname":
                        TheSim.regionname = simnode.ChildNodes[i].InnerText;
                        break;

                    case "sim_ip":
                        TheSim.sim_ip = simnode.ChildNodes[i].InnerText;
                        break;

                    case "sim_port":
                        TheSim.sim_port = Convert.ToUInt32(simnode.ChildNodes[i].InnerText);
                        break;

                    case "region_locx":
                        TheSim.RegionLocX = Convert.ToUInt32((string)simnode.ChildNodes[i].InnerText);
                        TheSim.regionhandle = Helpers.UIntsToLong((TheSim.RegionLocX * 256), (TheSim.RegionLocY * 256));
                        break;

                    case "region_locy":
                        TheSim.RegionLocY = Convert.ToUInt32((string)simnode.ChildNodes[i].InnerText);
                        TheSim.regionhandle = Helpers.UIntsToLong((TheSim.RegionLocX * 256), (TheSim.RegionLocY * 256));
                        break;
                }
            }

            try
            {
                SimProfiles.Add(TheSim.UUID, TheSim);
                IObjectContainer db;
                db = Db4oFactory.OpenFile("simprofiles.yap");
                db.Set(TheSim);
                db.Close();
                return "OK";
            }
            catch (Exception e)
            {
                return "ERROR! could not save to database!";
            }

        }

        public string RestGetRegionMethod(string request, string path, string param)
        {
            SimProfileBase TheSim = GetProfileByHandle((ulong)Convert.ToUInt64(param));
            return RestGetSimMethod("", "/sims/", param);
        }

        public string RestSetRegionMethod(string request, string path, string param)
        {
            SimProfileBase TheSim = GetProfileByHandle((ulong)Convert.ToUInt64(param));
            return RestSetSimMethod("", "/sims/", param);
        }

        public string RestGetSimMethod(string request, string path, string param)
        {
            string respstring = String.Empty;

            SimProfileBase TheSim;
            LLUUID UUID = new LLUUID(param);
            TheSim = GetProfileByLLUUID(UUID);

            if (!(TheSim == null))
            {
                respstring = "<Root>";
                respstring += "<authkey>" + m_gridManager.Cfg.SimSendKey + "</authkey>";
                respstring += "<sim>";
                respstring += "<uuid>" + TheSim.UUID.ToString() + "</uuid>";
                respstring += "<regionname>" + TheSim.regionname + "</regionname>";
                respstring += "<sim_ip>" + TheSim.sim_ip + "</sim_ip>";
                respstring += "<sim_port>" + TheSim.sim_port.ToString() + "</sim_port>";
                respstring += "<region_locx>" + TheSim.RegionLocX.ToString() + "</region_locx>";
                respstring += "<region_locy>" + TheSim.RegionLocY.ToString() + "</region_locy>";
                respstring += "<estate_id>1</estate_id>";
                respstring += "</sim>";
                respstring += "</Root>";
            }

            return respstring;
        }

    }


}
