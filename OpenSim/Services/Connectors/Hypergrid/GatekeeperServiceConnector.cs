using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Reflection;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using OpenMetaverse.Imaging;
using Nwc.XmlRpc;
using log4net;

using OpenSim.Services.Connectors.Simulation;

namespace OpenSim.Services.Connectors.Hypergrid
{
    public class GatekeeperServiceConnector : SimulationServiceConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static UUID m_HGMapImage = new UUID("00000000-0000-1111-9999-000000000013");

        private IAssetService m_AssetService;

        public GatekeeperServiceConnector() : base()
        {
        }

        public GatekeeperServiceConnector(IAssetService assService)
        {
            m_AssetService = assService;
        }

        protected override string AgentPath()
        {
            return "/foreignagent/";
        }

        protected override string ObjectPath()
        {
            return "/foreignobject/";
        }

        public bool LinkRegion(GridRegion info, out UUID regionID, out ulong realHandle, out string imageURL, out string reason)
        {
            regionID = UUID.Zero;
            imageURL = string.Empty;
            realHandle = 0;
            reason = string.Empty;

            Hashtable hash = new Hashtable();
            hash["region_name"] = info.RegionName;

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("link_region", paramList);
            string uri = "http://" + info.ExternalEndPoint.Address + ":" + info.HttpPort + "/";
            //m_log.Debug("[GATEKEEPER SERVICE CONNECTOR]: Linking to " + uri);
            XmlRpcResponse response = null;
            try
            {
                response = request.Send(uri, 10000);
            }
            catch (Exception e)
            {
                m_log.Debug("[GATEKEEPER SERVICE CONNECTOR]: Exception " + e.Message);
                reason = "Error contacting remote server";
                return false;
            }

            if (response.IsFault)
            {
                reason = response.FaultString;
                m_log.ErrorFormat("[GATEKEEPER SERVICE CONNECTOR]: remote call returned an error: {0}", response.FaultString);
                return false;
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
                    UUID.TryParse((string)hash["uuid"], out regionID);
                    //m_log.Debug(">> HERE, uuid: " + uuid);
                    if ((string)hash["handle"] != null)
                    {
                        realHandle = Convert.ToUInt64((string)hash["handle"]);
                        //m_log.Debug(">> HERE, realHandle: " + realHandle);
                    }
                    if (hash["region_image"] != null)
                    {
                        imageURL = (string)hash["region_image"];
                    }
                }

            }
            catch (Exception e)
            {
                reason = "Error parsing return arguments";
                m_log.Error("[GATEKEEPER SERVICE CONNECTOR]: Got exception while parsing hyperlink response " + e.StackTrace);
                return false;
            }

            return true;
        }

        UUID m_MissingTexture = new UUID("5748decc-f629-461c-9a36-a35a221fe21f");

        public UUID GetMapImage(UUID regionID, string imageURL)
        {
            if (m_AssetService == null)
                return m_MissingTexture;

            try
            {

                WebClient c = new WebClient();
                //m_log.Debug("JPEG: " + imageURL);
                string filename = regionID.ToString();
                c.DownloadFile(imageURL, filename + ".jpg");
                Bitmap m = new Bitmap(filename + ".jpg");
                //m_log.Debug("Size: " + m.PhysicalDimension.Height + "-" + m.PhysicalDimension.Width);
                byte[] imageData = OpenJPEG.EncodeFromImage(m, true);
                AssetBase ass = new AssetBase(UUID.Random(), "region " + filename, (sbyte)AssetType.Texture);

                // !!! for now
                //info.RegionSettings.TerrainImageID = ass.FullID;

                ass.Temporary = true;
                ass.Local = true;
                ass.Data = imageData;

                m_AssetService.Store(ass);

                // finally
                return ass.FullID;

            }
            catch // LEGIT: Catching problems caused by OpenJPEG p/invoke
            {
                m_log.Warn("[GATEKEEPER SERVICE CONNECTOR]: Failed getting/storing map image, because it is probably already in the cache");
            }
            return UUID.Zero;
        }

        public GridRegion GetHyperlinkRegion(GridRegion gatekeeper, UUID regionID)
        {
            Hashtable hash = new Hashtable();
            hash["region_uuid"] = regionID.ToString();

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("get_region", paramList);
            string uri = "http://" + gatekeeper.ExternalEndPoint.Address + ":" + gatekeeper.HttpPort + "/";
            m_log.Debug("[GATEKEEPER SERVICE CONNECTOR]: contacting " + uri);
            XmlRpcResponse response = null;
            try
            {
                response = request.Send(uri, 10000);
            }
            catch (Exception e)
            {
                m_log.Debug("[GATEKEEPER SERVICE CONNECTOR]: Exception " + e.Message);
                return null;
            }

            if (response.IsFault)
            {
                m_log.ErrorFormat("[GATEKEEPER SERVICE CONNECTOR]: remote call returned an error: {0}", response.FaultString);
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

                    // Successful return
                    return region;
                }

            }
            catch (Exception e)
            {
                m_log.Error("[GATEKEEPER SERVICE CONNECTOR]: Got exception while parsing hyperlink response " + e.StackTrace);
                return null;
            }

            return null;
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
