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
*     * Neither the name of the OpenSim Project nor the
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
* 
*/

using System;
using System.Collections;
using System.Net;
using System.Timers;
using libsecondlife;
using Mono.Addins;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment.Scenes;

[assembly : Addin]
[assembly : AddinDependency("OpenSim", "0.4")]

namespace OpenSim.ApplicationPlugins.LoadRegions
{
    [Extension("/OpenSim/Startup")]
    public class RemoteAdminPlugin : IApplicationPlugin
    {
        private OpenSimMain m_app;
        private BaseHttpServer m_httpd;
        private string requiredPassword = "";

        public string ModuleName()
        {
            return "OpenSim.ApplicationPlugins.LoadRegions.RemoteAdminPlugin";
        }

        public void Initialise(OpenSimMain openSim)
        {
            try
            {
                if (openSim.ConfigSource.Configs["RemoteAdmin"].GetBoolean("enabled", false))
                {
                    MainLog.Instance.Verbose("RADMIN", "Remote Admin Plugin Enabled");
                    requiredPassword = openSim.ConfigSource.Configs["RemoteAdmin"].GetString("access_password", "");

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
            Hashtable requestData = (Hashtable) request.Params[0];

            LLUUID regionID = new LLUUID((string) requestData["regionID"]);

            Hashtable responseData = new Hashtable();
            if (requiredPassword != "" &&
                (!requestData.Contains("password") || (string) requestData["password"] != requiredPassword))
            {
                responseData["accepted"] = "false";
                response.Value = responseData;
            }
            else
            {
                responseData["accepted"] = "true";
                response.Value = responseData;

                Scene RebootedScene;

                if (m_app.SceneManager.TryGetScene(regionID, out RebootedScene))
                {
                    responseData["rebooting"] = "true";
                    RebootedScene.Restart(30);
                }
                else
                {
                    responseData["rebooting"] = "false";
                }
            }

            return response;
        }

        public XmlRpcResponse XmlRpcAlertMethod(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable) request.Params[0];

            Hashtable responseData = new Hashtable();
            if (requiredPassword != "" &&
                (!requestData.Contains("password") || (string) requestData["password"] != requiredPassword))
            {
                responseData["accepted"] = "false";
                response.Value = responseData;
            }
            else
            {
                string message = (string) requestData["message"];
                MainLog.Instance.Verbose("RADMIN", "Broadcasting: " + message);

                responseData["accepted"] = "true";
                response.Value = responseData;

                m_app.SceneManager.SendGeneralMessage(message);
            }

            return response;
        }

        public XmlRpcResponse XmlRpcShutdownMethod(XmlRpcRequest request)
        {
            MainLog.Instance.Verbose("RADMIN", "Received Shutdown Administrator Request");
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable) request.Params[0];
            Hashtable responseData = new Hashtable();
            if (requiredPassword != "" &&
                (!requestData.Contains("password") || (string) requestData["password"] != requiredPassword))
            {
                responseData["accepted"] = "false";
                response.Value = responseData;
            }
            else
            {
                if ((string) requestData["shutdown"] == "delayed")
                {
                    int timeout = (Int32) requestData["milliseconds"];

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
            return response;
        }

        private void shutdownTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            m_app.Shutdown();
        }

        public XmlRpcResponse XmlRpcCreateRegionMethod(XmlRpcRequest request)
        {
            MainLog.Instance.Verbose("RADMIN", "Received Create Region Administrator Request");
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable) request.Params[0];
            Hashtable responseData = new Hashtable();
            if (requiredPassword != "" &&
                (!requestData.Contains("password") || (string) requestData["password"] != requiredPassword))
            {
                responseData["created"] = "false";
                response.Value = responseData;
            }
            else
            {
                RegionInfo newRegionData = new RegionInfo();

                try
                {
                    newRegionData.RegionID = (string) requestData["region_id"];
                    newRegionData.RegionName = (string) requestData["region_name"];
                    newRegionData.RegionLocX = Convert.ToUInt32((Int32) requestData["region_x"]);
                    newRegionData.RegionLocY = Convert.ToUInt32((Int32) requestData["region_y"]);

                    // Security risk
                    newRegionData.DataStore = (string) requestData["datastore"];

                    newRegionData.InternalEndPoint = new IPEndPoint(
                        IPAddress.Parse((string) requestData["listen_ip"]), 0);

                    newRegionData.InternalEndPoint.Port = (Int32) requestData["listen_port"];
                    newRegionData.ExternalHostName = (string) requestData["external_address"];

                    newRegionData.MasterAvatarFirstName = (string) requestData["region_master_first"];
                    newRegionData.MasterAvatarLastName = (string) requestData["region_master_last"];

                    m_app.CreateRegion(newRegionData);

                    responseData["created"] = "true";
                    response.Value = responseData;
                }
                catch (Exception e)
                {
                    responseData["created"] = "false";
                    responseData["error"] = e.ToString();
                    response.Value = responseData;
                }
            }

            return response;
        }

        public void Close()
        {
        }

    }
}