using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using OpenSim;
using OpenSim.Framework.Console;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using Mono.Addins;
using Mono.Addins.Description;
using Nini;
using Nini.Config;
using Nwc.XmlRpc;
using System.Collections;
using System.Timers;
using libsecondlife;

[assembly: Addin]
[assembly: AddinDependency("OpenSim", "0.4")]

namespace OpenSim.ApplicationPlugins.LoadRegions
{
    [Extension("/OpenSim/Startup")]
    public class RemoteAdminPlugin : IApplicationPlugin
    {
        private OpenSimMain m_app;
        private BaseHttpServer m_httpd;

        public void Initialise(OpenSimMain openSim)
        {
            try
            {
                if (openSim.ConfigSource.Configs["RemoteAdmin"].GetBoolean("enabled", false))
                {
                    MainLog.Instance.Verbose("RADMIN", "Remote Admin Plugin Enabled");

                    m_app = openSim;
                    m_httpd = openSim.HttpServer;

                    m_httpd.AddXmlRPCHandler("admin_create_region", XmlRpcCreateRegionMethod);
                    m_httpd.AddXmlRPCHandler("admin_shutdown", XmlRpcShutdownMethod);
                    m_httpd.AddXmlRPCHandler("admin_broadcast", XmlRpcAlertMethod);
                    m_httpd.AddXmlRPCHandler("admin_restart", XmlRpcRestartMethod);
                }
            }
            catch (NullReferenceException)
            {
                // Ignore.
            }
        }

        public XmlRpcResponse XmlRpcRestartMethod(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];

            LLUUID regionID = new LLUUID((string)requestData["regionID"]);

            Hashtable responseData = new Hashtable();
            responseData["accepted"] = "true";
            response.Value = responseData;

            OpenSim.Region.Environment.Scenes.Scene RebootedScene;

            if (m_app.SceneManager.TryGetScene(regionID, out RebootedScene))
            {
                responseData["rebooting"] = "true";
                RebootedScene.Restart(30);
            }
            else
            {
                responseData["rebooting"] = "false";
            }

            return response;
        }

        public XmlRpcResponse XmlRpcAlertMethod(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];

            string message = (string)requestData["message"];
            MainLog.Instance.Verbose("RADMIN", "Broadcasting: " + message);

            Hashtable responseData = new Hashtable();
            responseData["accepted"] = "true";
            response.Value = responseData;

            m_app.SceneManager.SendGeneralMessage(message);

            return response;
        }

        public XmlRpcResponse XmlRpcShutdownMethod(XmlRpcRequest request)
        {
            MainLog.Instance.Verbose("RADMIN", "Recieved Shutdown Administrator Request");
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];

            if ((string)requestData["shutdown"] == "delayed")
            {
                int timeout = (Int32)requestData["milliseconds"];

                Hashtable responseData = new Hashtable();
                responseData["accepted"] = "true";
                response.Value = responseData;

                m_app.SceneManager.SendGeneralMessage("Region is going down in " + ((int)(timeout / 1000)).ToString() +
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
            MainLog.Instance.Verbose("RADMIN", "Recieved Create Region Administrator Request");
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];

            RegionInfo newRegionData = new RegionInfo();

            try
            {
                newRegionData.RegionID = (string)requestData["region_id"];
                newRegionData.RegionName = (string)requestData["region_name"];
                newRegionData.RegionLocX = Convert.ToUInt32((Int32)requestData["region_x"]);
                newRegionData.RegionLocY = Convert.ToUInt32((Int32)requestData["region_y"]);

                // Security risk
                newRegionData.DataStore = (string)requestData["datastore"];

                newRegionData.InternalEndPoint = new IPEndPoint(
                    IPAddress.Parse((string)requestData["listen_ip"]), 0);

                newRegionData.InternalEndPoint.Port = (Int32)requestData["listen_port"];
                newRegionData.ExternalHostName = (string)requestData["external_address"];

                newRegionData.MasterAvatarFirstName = (string)requestData["region_master_first"];
                newRegionData.MasterAvatarLastName = (string)requestData["region_master_last"];

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

        public void Close()
        {

        }
    }
}