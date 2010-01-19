using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;

using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using Nwc.XmlRpc;

using OpenSim.Services.Connectors.Simulation;

namespace OpenSim.Services.Connectors.Hypergrid
{
    public class GatekeeperServiceConnector : SimulationServiceConnector
    {
        protected override string AgentPath()
        {
            return "/foreignagent/";
        }

        protected override string ObjectPath()
        {
            return "/foreignobject/";
        }

        public GridRegion GetHomeRegion(GridRegion gatekeeper, UUID userID, out Vector3 position, out Vector3 lookAt)
        {
            position = Vector3.UnitY; lookAt = Vector3.UnitY;

            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("get_home_region", paramList);
            string uri = "http://" + gatekeeper.ExternalHostName + ":" + gatekeeper.HttpPort + "/";
            XmlRpcResponse response = null;
            try
            {
                response = request.Send(uri, 10000);
            }
            catch (Exception e)
            {
                return null;
            }

            if (response.IsFault)
            {
                return null;
            }

            hash = (Hashtable)response.Value;
            //foreach (Object o in hash)
            //    m_log.Debug(">> " + ((DictionaryEntry)o).Key + ":" + ((DictionaryEntry)o).Value);
            try
            {
                bool success = false;
                Boolean.TryParse((string)hash["result"], out success);
                if (success)
                {
                    GridRegion region = new GridRegion();

                    UUID.TryParse((string)hash["uuid"], out region.RegionID);
                    //m_log.Debug(">> HERE, uuid: " + region.RegionID);
                    int n = 0;
                    if (hash["x"] != null)
                    {
                        Int32.TryParse((string)hash["x"], out n);
                        region.RegionLocX = n;
                        //m_log.Debug(">> HERE, x: " + region.RegionLocX);
                    }
                    if (hash["y"] != null)
                    {
                        Int32.TryParse((string)hash["y"], out n);
                        region.RegionLocY = n;
                        //m_log.Debug(">> HERE, y: " + region.RegionLocY);
                    }
                    if (hash["region_name"] != null)
                    {
                        region.RegionName = (string)hash["region_name"];
                        //m_log.Debug(">> HERE, name: " + region.RegionName);
                    }
                    if (hash["hostname"] != null)
                        region.ExternalHostName = (string)hash["hostname"];
                    if (hash["http_port"] != null)
                    {
                        uint p = 0;
                        UInt32.TryParse((string)hash["http_port"], out p);
                        region.HttpPort = p;
                    }
                    if (hash["internal_port"] != null)
                    {
                        int p = 0;
                        Int32.TryParse((string)hash["internal_port"], out p);
                        region.InternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), p);
                    }
                    if (hash["position"] != null)
                        Vector3.TryParse((string)hash["position"], out position);
                    if (hash["lookAt"] != null)
                        Vector3.TryParse((string)hash["lookAt"], out lookAt);

                    // Successful return
                    return region;
                }

            }
            catch (Exception e)
            {
                return null;
            }

            return null;

        }
    }
}
