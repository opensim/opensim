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
                System.Console.WriteLine(e.ToString());
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
                System.Console.WriteLine(e.ToString());
            }
            return this;
        }


        public SimProfile()
        {
        }
    }

}
