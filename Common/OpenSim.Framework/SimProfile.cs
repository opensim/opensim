/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using System.Collections;
using System.Xml;
using System.Text;
using libsecondlife;
using Nwc.XmlRpc;

namespace OpenSim.Framework.Sims
{
    public class SimProfile : SimProfileBase
    {
        public SimProfile LoadFromGrid(ulong region_handle, string GridURL, string SendKey, string RecvKey)
        {
            try
            {
                Hashtable GridReqParams = new Hashtable();
                GridReqParams["region_handle"] = region_handle.ToString();
                GridReqParams["authkey"] = SendKey;
                ArrayList SendParams = new ArrayList();
                SendParams.Add(GridReqParams);
                XmlRpcRequest GridReq = new XmlRpcRequest("simulator_login", SendParams);

                XmlRpcResponse GridResp = GridReq.Send(GridURL, 3000);

                Hashtable RespData = (Hashtable)GridResp.Value;
                this.UUID = new LLUUID((string)RespData["UUID"]);
                this.regionhandle = Helpers.UIntsToLong(((uint)Convert.ToUInt32(RespData["region_locx"]) * 256), ((uint)Convert.ToUInt32(RespData["region_locy"]) * 256));
                this.regionname = (string)RespData["regionname"];
                this.sim_ip = (string)RespData["sim_ip"];
                this.sim_port = (uint)Convert.ToUInt16(RespData["sim_port"]);
                this.caps_url = "http://" + ((string)RespData["sim_ip"]) + ":" + (string)RespData["sim_port"] + "/";
                this.RegionLocX = (uint)Convert.ToUInt32(RespData["region_locx"]);
                this.RegionLocY = (uint)Convert.ToUInt32(RespData["region_locy"]);
                this.sendkey = SendKey;
                this.recvkey = RecvKey;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return this;
        }

        public SimProfile LoadFromGrid(LLUUID UUID, string GridURL, string SendKey, string RecvKey)
        {
            try
            {
                Hashtable GridReqParams = new Hashtable();
                GridReqParams["UUID"] = UUID.ToString();
                GridReqParams["authkey"] = SendKey;
                ArrayList SendParams = new ArrayList();
                SendParams.Add(GridReqParams);
                XmlRpcRequest GridReq = new XmlRpcRequest("simulator_login", SendParams);

                XmlRpcResponse GridResp = GridReq.Send(GridURL, 3000);

                Hashtable RespData = (Hashtable)GridResp.Value;
                this.UUID = new LLUUID((string)RespData["UUID"]);
                this.regionhandle = Helpers.UIntsToLong(((uint)Convert.ToUInt32(RespData["region_locx"]) * 256), ((uint)Convert.ToUInt32(RespData["region_locy"]) * 256));
                this.regionname = (string)RespData["regionname"];
                this.sim_ip = (string)RespData["sim_ip"];
                this.sim_port = (uint)Convert.ToUInt16(RespData["sim_port"]);
                this.caps_url = "http://" + ((string)RespData["sim_ip"]) + ":" + (string)RespData["sim_port"] + "/";
                this.RegionLocX = (uint)Convert.ToUInt32(RespData["region_locx"]);
                this.RegionLocY = (uint)Convert.ToUInt32(RespData["region_locy"]);
                this.sendkey = SendKey;
                this.recvkey = RecvKey;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return this;
        }


        public SimProfile()
        {
        }
    }

}
