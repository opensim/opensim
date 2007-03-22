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
                GridReqParams["caller"] = "userserver";
                GridReqParams["authkey"] = SendKey;
                ArrayList SendParams = new ArrayList();
                SendParams.Add(GridReqParams);
                XmlRpcRequest GridReq = new XmlRpcRequest("get_sim_info", SendParams);

                XmlRpcResponse GridResp = GridReq.Send(GridURL, 3000);

                Hashtable RespData = (Hashtable)GridResp.Value;
                this.UUID = new LLUUID((string)RespData["UUID"]);
                this.regionhandle = (ulong)Convert.ToUInt64(RespData["regionhandle"]);
                this.regionname = (string)RespData["regionname"];
                this.sim_ip = (string)RespData["sim_ip"];
                this.sim_port = (uint)Convert.ToUInt16(RespData["sim_port"]);
                this.caps_url = (string)RespData["caps_url"];
                this.RegionLocX = (uint)Convert.ToUInt32(RespData["RegionLocX"]);
                this.RegionLocY = (uint)Convert.ToUInt32(RespData["RegionLocY"]);
                this.sendkey = (string)RespData["sendkey"];
                this.recvkey = (string)RespData["recvkey"];
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
