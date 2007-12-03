using System;
using System.Collections;
using System.Net;
using System.Timers;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;

namespace OpenSim
{
    internal class OpenSimController
    {
        private OpenSimMain m_app;
        private BaseHttpServer m_httpServer;
        private const bool m_enablexmlrpc = true;

        public OpenSimController(OpenSimMain core, BaseHttpServer httpd)
        {
            m_app = core;
            m_httpServer = httpd;

            if (m_enablexmlrpc)
            {
                m_httpServer.AddXmlRPCHandler("admin_create_region", XmlRpcCreateRegionMethod);
                m_httpServer.AddXmlRPCHandler("admin_shutdown", XmlRpcShutdownMethod);
            }
        }

        public XmlRpcResponse XmlRpcShutdownMethod(XmlRpcRequest request)
        {
            MainLog.Instance.Verbose("CONTROLLER", "Recieved Shutdown Administrator Request");
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable) request.Params[0];

            if ((string) requestData["shutdown"] == "delayed")
            {
                int timeout = (Int32)requestData["milliseconds"];

                Hashtable responseData = new Hashtable();
                responseData["accepted"] = "true";
                response.Value = responseData;

                m_app.SceneManager.SendGeneralMessage("Region is going down in " + ((int) (timeout/1000)).ToString() +
                                                      " second(s). Please save what you are doing and log out.");

                // Perform shutdown
                Timer shutdownTimer = new Timer(timeout); // Wait before firing
                shutdownTimer.AutoReset = false;
                shutdownTimer.Elapsed += new ElapsedEventHandler(shutdownTimer_Elapsed);
                shutdownTimer.Start();

                return response;
            }
            else
            {
                Hashtable responseData = new Hashtable();
                responseData["accepted"] = "true";
                response.Value = responseData;

                m_app.SceneManager.SendGeneralMessage("Region is going down now.");

                // Perform shutdown
                Timer shutdownTimer = new Timer(2000); // Wait 2 seconds before firing
                shutdownTimer.AutoReset = false;
                shutdownTimer.Elapsed += new ElapsedEventHandler(shutdownTimer_Elapsed);
                shutdownTimer.Start();

                return response;
            }
        }

        private void shutdownTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            m_app.Shutdown();
        }

        public XmlRpcResponse XmlRpcCreateRegionMethod(XmlRpcRequest request)
        {
            MainLog.Instance.Verbose("CONTROLLER", "Recieved Create Region Administrator Request");
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable) request.Params[0];

            RegionInfo newRegionData = new RegionInfo();

            try
            {
                newRegionData.RegionID = (string)requestData["region_id"];
                newRegionData.RegionName = (string)requestData["region_name"];
                newRegionData.RegionLocX = Convert.ToUInt32((Int32)requestData["region_x"]);
                newRegionData.RegionLocY = Convert.ToUInt32((Int32)requestData["region_y"]);

                // Security risk
                newRegionData.DataStore = (string) requestData["datastore"];

                newRegionData.InternalEndPoint = new IPEndPoint(
                    IPAddress.Parse((string) requestData["listen_ip"]), 0);

                newRegionData.InternalEndPoint.Port = (Int32)requestData["listen_port"];
                newRegionData.ExternalHostName = (string)requestData["external_address"];

                newRegionData.MasterAvatarFirstName = (string) requestData["region_master_first"];
                newRegionData.MasterAvatarLastName = (string) requestData["region_master_last"];

                m_app.CreateRegion(newRegionData);

                Hashtable responseData = new Hashtable();
                responseData["created"] = "true";
                response.Value = responseData;
            }
            catch (Exception e)
            {
                Hashtable responseData = new Hashtable();
                responseData["created"] = "false";
                responseData["error"] = e.ToString();
                response.Value = responseData;
            }

            return response;
        }
    }
}
