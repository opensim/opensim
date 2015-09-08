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
using System.Globalization;
using System.IO;
using System.Xml;
using System.Net;
using System.Reflection;
using System.Timers;
using System.Threading;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using Mono.Addins;
using OpenSim;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using PresenceInfo = OpenSim.Services.Interfaces.PresenceInfo;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using PermissionMask = OpenSim.Framework.PermissionMask;
using RegionInfo = OpenSim.Framework.RegionInfo;

namespace OpenSim.ApplicationPlugins.RemoteController
{
    [Extension(Path = "/OpenSim/Startup", Id = "LoadRegions", NodeName = "Plugin")]
    public class RemoteAdminPlugin : IApplicationPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static bool m_defaultAvatarsLoaded = false;
        private static Object   m_requestLock = new Object();
        private static Object   m_saveOarLock = new Object();

        private OpenSimBase m_application;
        private IHttpServer m_httpServer;
        private IConfig m_config;
        private IConfigSource m_configSource;
        private string m_requiredPassword = String.Empty;
        private HashSet<string> m_accessIP;

        private string m_name = "RemoteAdminPlugin";
        private string m_version = "0.0";
        private string m_openSimVersion;

        public string Version
        {
            get { return m_version; }
        }

        public string Name
        {
            get { return m_name; }
        }

        public void Initialise()
        {
            m_log.Error("[RADMIN]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException(Name);
        }

        public void Initialise(OpenSimBase openSim)
        {
            m_openSimVersion = openSim.GetVersionText();

            m_configSource = openSim.ConfigSource.Source;
            try
            {
                if (m_configSource.Configs["RemoteAdmin"] == null ||
                    !m_configSource.Configs["RemoteAdmin"].GetBoolean("enabled", false))
                {
                    // No config or disabled
                }
                else
                {
                    m_config = m_configSource.Configs["RemoteAdmin"];
                    m_log.Debug("[RADMIN]: Remote Admin Plugin Enabled");
                    m_requiredPassword = m_config.GetString("access_password", String.Empty);
                    int port = m_config.GetInt("port", 0);

                    string accessIP = m_config.GetString("access_ip_addresses", String.Empty);
                    m_accessIP = new HashSet<string>();
                    if (accessIP != String.Empty)
                    {
                        string[] ips = accessIP.Split(new char[] { ',' });
                        foreach (string ip in ips)
                        {
                            string current = ip.Trim();

                            if (current != String.Empty)
                                m_accessIP.Add(current);
                        }
                    }

                    m_application = openSim;
                    string bind_ip_address = m_config.GetString("bind_ip_address", "0.0.0.0");
                    IPAddress ipaddr = IPAddress.Parse(bind_ip_address);
                    m_httpServer = MainServer.GetHttpServer((uint)port,ipaddr);

                    Dictionary<string, XmlRpcMethod> availableMethods = new Dictionary<string, XmlRpcMethod>();
                    availableMethods["admin_create_region"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcCreateRegionMethod);
                    availableMethods["admin_delete_region"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcDeleteRegionMethod);
                    availableMethods["admin_close_region"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcCloseRegionMethod);
                    availableMethods["admin_modify_region"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcModifyRegionMethod);
                    availableMethods["admin_region_query"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcRegionQueryMethod);
                    availableMethods["admin_shutdown"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcShutdownMethod);
                    availableMethods["admin_broadcast"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcAlertMethod);
                    availableMethods["admin_dialog"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcDialogMethod);
                    availableMethods["admin_restart"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcRestartMethod);
                    availableMethods["admin_load_heightmap"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcLoadHeightmapMethod);
                    availableMethods["admin_save_heightmap"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcSaveHeightmapMethod);

                    availableMethods["admin_reset_land"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcResetLand);

                    // Agent management
                    availableMethods["admin_get_agents"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcGetAgentsMethod);
                    availableMethods["admin_teleport_agent"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcTeleportAgentMethod);

                    // User management
                    availableMethods["admin_create_user"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcCreateUserMethod);
                    availableMethods["admin_create_user_email"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcCreateUserMethod);
                    availableMethods["admin_exists_user"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcUserExistsMethod);
                    availableMethods["admin_update_user"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcUpdateUserAccountMethod);
                    availableMethods["admin_authenticate_user"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcAuthenticateUserMethod);

                    // Region state management
                    availableMethods["admin_load_xml"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcLoadXMLMethod);
                    availableMethods["admin_save_xml"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcSaveXMLMethod);
                    availableMethods["admin_load_oar"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcLoadOARMethod);
                    availableMethods["admin_save_oar"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcSaveOARMethod);

                    // Estate access list management
                    availableMethods["admin_acl_clear"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcAccessListClear);
                    availableMethods["admin_acl_add"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcAccessListAdd);
                    availableMethods["admin_acl_remove"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcAccessListRemove);
                    availableMethods["admin_acl_list"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcAccessListList);
                    availableMethods["admin_estate_reload"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcEstateReload);

                    // Misc
                    availableMethods["admin_refresh_search"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcRefreshSearch);
                    availableMethods["admin_refresh_map"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcRefreshMap);
                    availableMethods["admin_get_opensim_version"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcGetOpenSimVersion);
                    availableMethods["admin_get_agent_count"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcGetAgentCount);

                    // Either enable full remote functionality or just selected features
                    string enabledMethods = m_config.GetString("enabled_methods", "all");

                    // To get this, you must explicitly specify "all" or
                    // mention it in a whitelist. It won't be available
                    // If you just leave the option out!
                    //
                    if (!String.IsNullOrEmpty(enabledMethods))
                        availableMethods["admin_console_command"] = (req, ep) => InvokeXmlRpcMethod(req, ep, XmlRpcConsoleCommandMethod);

                    // The assumption here is that simply enabling Remote Admin as before will produce the same
                    // behavior - enable all methods unless the whitelist is in place for backward-compatibility.
                    if (enabledMethods.ToLower() == "all" || String.IsNullOrEmpty(enabledMethods))
                    {
                        foreach (string method in availableMethods.Keys)
                        {
                            m_httpServer.AddXmlRPCHandler(method, availableMethods[method], false);
                        }
                    }
                    else
                    {
                        foreach (string enabledMethod in enabledMethods.Split('|'))
                        {
                            m_httpServer.AddXmlRPCHandler(enabledMethod, availableMethods[enabledMethod], false);
                        }
                    }
                }
            }
            catch (NullReferenceException)
            {
                // Ignore.
            }
        }

        public void PostInitialise()
        {
            if (!CreateDefaultAvatars())
            {
                m_log.Info("[RADMIN]: Default avatars not loaded");
            }
        }

        /// <summary>
        /// Invoke an XmlRpc method with the standard actions (password check, etc.)
        /// </summary>
        /// <param name="method"></param>
        private XmlRpcResponse InvokeXmlRpcMethod(
            XmlRpcRequest request, IPEndPoint remoteClient, Action<XmlRpcRequest, XmlRpcResponse, IPEndPoint> method)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            try
            {
                Hashtable requestData = (Hashtable) request.Params[0];

                CheckStringParameters(requestData, responseData, new string[] {"password"});

                FailIfRemoteAdminNotAllowed((string)requestData["password"], responseData, remoteClient.Address.ToString());

                method(request, response, remoteClient);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[RADMIN]: Method {0} failed.  Exception {1}{2}", request.MethodName, e.Message, e.StackTrace);

                responseData["success"] = false;
                responseData["error"] = e.Message;
            }

            return response;
        }

        private void FailIfRemoteAdminNotAllowed(string password, Hashtable responseData, string check_ip_address)
        {
            if (m_accessIP.Count > 0 && !m_accessIP.Contains(check_ip_address))
            {
                m_log.WarnFormat("[RADMIN]: Unauthorized access blocked from IP {0}", check_ip_address);
                responseData["accepted"] = false;
                throw new Exception("not authorized");
            }

            if (m_requiredPassword != String.Empty && password != m_requiredPassword)
            {
                m_log.WarnFormat("[RADMIN]: Wrong password, blocked access from IP {0}", check_ip_address);
                responseData["accepted"] = false;
                throw new Exception("wrong password");
            }
        }

        private void XmlRpcRestartMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            try
            {
                Scene rebootedScene = null;
                bool restartAll = false;

                IConfig startupConfig = m_configSource.Configs["Startup"];
                if (startupConfig != null)
                {
                    if (startupConfig.GetBoolean("InworldRestartShutsDown", false))
                    {
                        rebootedScene = m_application.SceneManager.CurrentOrFirstScene;
                        restartAll = true;
                    }
                }

                if (rebootedScene == null)
                {
                    CheckRegionParams(requestData, responseData);

                    GetSceneFromRegionParams(requestData, responseData, out rebootedScene);
                }

                IRestartModule restartModule = rebootedScene.RequestModuleInterface<IRestartModule>();

                responseData["success"] = false;
                responseData["accepted"] = true;
                responseData["rebooting"] = true;

                string message;
                List<int> times = new List<int>();

                if (requestData.ContainsKey("alerts"))
                {
                    string[] alertTimes = requestData["alerts"].ToString().Split( new char[] {','});
                    if (alertTimes.Length == 1 && Convert.ToInt32(alertTimes[0]) == -1)
                    {
                        m_log.Info("[RADMIN]: Request to cancel restart.");

                        if (restartModule != null)
                        {
                            message = "Restart has been cancelled";

                            if (requestData.ContainsKey("message"))
                                message = requestData["message"].ToString();

                            restartModule.AbortRestart(message);

                            responseData["success"] = true;
                            responseData["rebooting"] = false;

                            return;
                        }
                    }
                    foreach (string a in alertTimes)
                        times.Add(Convert.ToInt32(a));
                }
                else
                {
                    int timeout = 30;
                    if (requestData.ContainsKey("milliseconds"))
                        timeout = Int32.Parse(requestData["milliseconds"].ToString()) / 1000;
                    while (timeout > 0)
                    {
                        times.Add(timeout);
                        if (timeout > 300)
                            timeout -= 120;
                        else if (timeout > 30)
                            timeout -= 30;
                        else
                            timeout -= 15;
                    }
                }

                m_log.Info("[RADMIN]: Request to restart Region.");

                message = "Region is restarting in {0}. Please save what you are doing and log out.";

                if (requestData.ContainsKey("message"))
                    message = requestData["message"].ToString();

                bool notice = true;
                if (requestData.ContainsKey("noticetype")
                    && ((string)requestData["noticetype"] == "dialog"))
                {
                    notice = false;
                }

                List<Scene> restartList;

                if (restartAll)
                    restartList = m_application.SceneManager.Scenes;
                else
                    restartList = new List<Scene>() { rebootedScene };

                foreach (Scene s in m_application.SceneManager.Scenes)
                {
                    restartModule = s.RequestModuleInterface<IRestartModule>();
                    if (restartModule != null)
                        restartModule.ScheduleRestart(UUID.Zero, message, times.ToArray(), notice);
                }
                responseData["success"] = true;
            }
            catch (Exception e)
            {
//                m_log.ErrorFormat("[RADMIN]: Restart region: failed: {0} {1}", e.Message, e.StackTrace);
                responseData["rebooting"] = false;

                throw e;
            }

            m_log.Info("[RADMIN]: Restart Region request complete");
        }

        private void XmlRpcAlertMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Alert request started");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            string message = (string) requestData["message"];
            m_log.InfoFormat("[RADMIN]: Broadcasting: {0}", message);

            responseData["accepted"] = true;
            responseData["success"] = true;

            m_application.SceneManager.ForEachScene(
                delegate(Scene scene)
                    {
                        IDialogModule dialogModule = scene.RequestModuleInterface<IDialogModule>();
                        if (dialogModule != null)
                            dialogModule.SendGeneralAlert(message);
                    });

            m_log.Info("[RADMIN]: Alert request complete");
        }

        public void XmlRpcDialogMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            Hashtable responseData = (Hashtable)response.Value;

            m_log.Info("[RADMIN]: Dialog request started");

            Hashtable requestData = (Hashtable)request.Params[0];

            string message = (string)requestData["message"];
            string fromuuid = (string)requestData["from"];
            m_log.InfoFormat("[RADMIN]: Broadcasting: {0}", message);

            responseData["accepted"] = true;
            responseData["success"] = true;

            m_application.SceneManager.ForEachScene(
                delegate(Scene scene)
                {
                    IDialogModule dialogModule = scene.RequestModuleInterface<IDialogModule>();
                    if (dialogModule != null)
                        dialogModule.SendNotificationToUsersInRegion(UUID.Zero, fromuuid, message);
                });

            m_log.Info("[RADMIN]: Dialog request complete");
        }

        private void XmlRpcLoadHeightmapMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Load height maps request started");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

//                m_log.DebugFormat("[RADMIN]: Load Terrain: XmlRpc {0}", request);
            // foreach (string k in requestData.Keys)
            // {
            //     m_log.DebugFormat("[RADMIN]: Load Terrain: XmlRpc {0}: >{1}< {2}",
            //                       k, (string)requestData[k], ((string)requestData[k]).Length);
            // }

            CheckStringParameters(requestData, responseData, new string[] { "filename" });
            CheckRegionParams(requestData, responseData);

            Scene scene = null;
            GetSceneFromRegionParams(requestData, responseData, out scene);

            if (scene != null)
            {
                string file = (string)requestData["filename"];

                responseData["accepted"] = true;

                LoadHeightmap(file, scene.RegionInfo.RegionID);

                responseData["success"] = true;
            }
            else
            {
                responseData["success"] = false;
            }

            m_log.Info("[RADMIN]: Load height maps request complete");
        }

        private void XmlRpcSaveHeightmapMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Save height maps request started");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

//                m_log.DebugFormat("[RADMIN]: Save Terrain: XmlRpc {0}", request.ToString());

            CheckStringParameters(requestData, responseData, new string[] { "filename" });
            CheckRegionParams(requestData, responseData);

            Scene scene = null;
            GetSceneFromRegionParams(requestData, responseData, out scene);

            if (scene != null)
            {
                string file = (string)requestData["filename"];
                m_log.InfoFormat("[RADMIN]: Terrain Saving: {0}", file);

                responseData["accepted"] = true;

                ITerrainModule terrainModule = scene.RequestModuleInterface<ITerrainModule>();
                if (null == terrainModule) throw new Exception("terrain module not available");

                terrainModule.SaveToFile(file);

                responseData["success"] = true;
            }
            else
            {
                responseData["success"] = false;
            }

            m_log.Info("[RADMIN]: Save height maps request complete");
        }

        private void XmlRpcShutdownMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Received Shutdown Administrator Request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            responseData["accepted"] = true;
            response.Value = responseData;

            int timeout = 2000;
            string message;

            if (requestData.ContainsKey("shutdown")
                && ((string) requestData["shutdown"] == "delayed")
                && requestData.ContainsKey("milliseconds"))
            {
                timeout = Int32.Parse(requestData["milliseconds"].ToString());

                message
                    = "Region is going down in " + ((int) (timeout/1000)).ToString()
                      + " second(s). Please save what you are doing and log out.";
            }
            else
            {
                message = "Region is going down now.";
            }

            if (requestData.ContainsKey("noticetype")
                && ((string) requestData["noticetype"] == "dialog"))
            {
                m_application.SceneManager.ForEachScene(

                delegate(Scene scene)
                {
                    IDialogModule dialogModule = scene.RequestModuleInterface<IDialogModule>();
                    if (dialogModule != null)
                            dialogModule.SendNotificationToUsersInRegion(UUID.Zero, "System", message);
                });
            }
            else
            {
                if (!requestData.ContainsKey("noticetype")
                    || ((string)requestData["noticetype"] != "none"))
                {
                    m_application.SceneManager.ForEachScene(
                    delegate(Scene scene)
                    {
                        IDialogModule dialogModule = scene.RequestModuleInterface<IDialogModule>();
                        if (dialogModule != null)
                            dialogModule.SendGeneralAlert(message);
                    });
                }
            }

            // Perform shutdown
            System.Timers.Timer shutdownTimer = new System.Timers.Timer(timeout); // Wait before firing
            shutdownTimer.AutoReset = false;
            shutdownTimer.Elapsed += new ElapsedEventHandler(shutdownTimer_Elapsed);
            lock (shutdownTimer)
            {
                shutdownTimer.Start();
            }

            responseData["success"] = true;
            
            m_log.Info("[RADMIN]: Shutdown Administrator Request complete");
        }

        private void shutdownTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            m_application.Shutdown();
        }

        /// <summary>
        /// Create a new region.
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcCreateRegionMethod takes the following XMLRPC
        /// parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>password</term>
        ///       <description>admin password as set in OpenSim.ini</description></item>
        /// <item><term>region_name</term>
        ///       <description>desired region name</description></item>
        /// <item><term>region_id</term>
        ///       <description>(optional) desired region UUID</description></item>
        /// <item><term>region_x</term>
        ///       <description>desired region X coordinate (integer)</description></item>
        /// <item><term>region_y</term>
        ///       <description>desired region Y coordinate (integer)</description></item>
        /// <item><term>estate_owner_first</term>
        ///       <description>firstname of estate owner (formerly region master)
        ///       (required if new estate is being created, optional otherwise)</description></item>
        /// <item><term>estate_owner_last</term>
        ///       <description>lastname of estate owner (formerly region master)
        ///       (required if new estate is being created, optional otherwise)</description></item>
        /// <item><term>estate_owner_uuid</term>
        ///       <description>explicit UUID to use for estate owner (optional)</description></item>
        /// <item><term>listen_ip</term>
        ///       <description>internal IP address (dotted quad)</description></item>
        /// <item><term>listen_port</term>
        ///       <description>internal port (integer)</description></item>
        /// <item><term>external_address</term>
        ///       <description>external IP address</description></item>
        /// <item><term>persist</term>
        ///       <description>if true, persist the region info
        ///       ('true' or 'false')</description></item>
        /// <item><term>public</term>
        ///       <description>if true, the region is public
        ///       ('true' or 'false') (optional, default: true)</description></item>
        /// <item><term>enable_voice</term>
        ///       <description>if true, enable voice on all parcels,
        ///       ('true' or 'false') (optional, default: false)</description></item>
        /// <item><term>estate_name</term>
        ///       <description>the name of the estate to join (or to create if it doesn't
        ///       already exist)</description></item>
        /// <item><term>region_file</term>
        ///       <description>The name of the file to persist the region specifications to.
        /// If omitted, the region_file_template setting from OpenSim.ini will be used. (optional)</description></item>
        /// </list>
        ///
        /// XmlRpcCreateRegionMethod returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// <item><term>error</term>
        ///       <description>error message if success is false</description></item>
        /// <item><term>region_uuid</term>
        ///       <description>UUID of the newly created region</description></item>
        /// <item><term>region_name</term>
        ///       <description>name of the newly created region</description></item>
        /// </list>
        /// </remarks>
        private void XmlRpcCreateRegionMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: CreateRegion: new request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            lock (m_requestLock)
            {
                int  m_regionLimit = m_config.GetInt("region_limit", 0);
                bool m_enableVoiceForNewRegions = m_config.GetBoolean("create_region_enable_voice", false);
                bool m_publicAccess = m_config.GetBoolean("create_region_public", true);

                CheckStringParameters(requestData, responseData, new string[]
                                                   {
                                                       "region_name",
                                                       "listen_ip", "external_address",
                                                       "estate_name"
                                                   });
                CheckIntegerParams(requestData, responseData, new string[] {"region_x", "region_y", "listen_port"});

                // check whether we still have space left (iff we are using limits)
                if (m_regionLimit != 0 && m_application.SceneManager.Scenes.Count >= m_regionLimit)
                    throw new Exception(String.Format("cannot instantiate new region, server capacity {0} already reached; delete regions first",
                                                      m_regionLimit));
                // extract or generate region ID now
                Scene scene = null;
                UUID regionID = UUID.Zero;
                if (requestData.ContainsKey("region_id") &&
                    !String.IsNullOrEmpty((string) requestData["region_id"]))
                {
                    regionID = (UUID) (string) requestData["region_id"];
                    if (m_application.SceneManager.TryGetScene(regionID, out scene))
                        throw new Exception(
                            String.Format("region UUID already in use by region {0}, UUID {1}, <{2},{3}>",
                                          scene.RegionInfo.RegionName, scene.RegionInfo.RegionID,
                                          scene.RegionInfo.RegionLocX, scene.RegionInfo.RegionLocY));
                }
                else
                {
                    regionID = UUID.Random();
                    m_log.DebugFormat("[RADMIN] CreateRegion: new region UUID {0}", regionID);
                }

                // create volatile or persistent region info
                RegionInfo region = new RegionInfo();

                region.RegionID = regionID;
                region.originRegionID = regionID;
                region.RegionName = (string) requestData["region_name"];
                region.RegionLocX = Convert.ToUInt32(requestData["region_x"]);
                region.RegionLocY = Convert.ToUInt32(requestData["region_y"]);

                // check for collisions: region name, region UUID,
                // region location
                if (m_application.SceneManager.TryGetScene(region.RegionName, out scene))
                    throw new Exception(
                        String.Format("region name already in use by region {0}, UUID {1}, <{2},{3}>",
                                      scene.RegionInfo.RegionName, scene.RegionInfo.RegionID,
                                      scene.RegionInfo.RegionLocX, scene.RegionInfo.RegionLocY));

                if (m_application.SceneManager.TryGetScene(region.RegionLocX, region.RegionLocY, out scene))
                    throw new Exception(
                        String.Format("region location <{0},{1}> already in use by region {2}, UUID {3}, <{4},{5}>",
                                      region.RegionLocX, region.RegionLocY,
                                      scene.RegionInfo.RegionName, scene.RegionInfo.RegionID,
                                      scene.RegionInfo.RegionLocX, scene.RegionInfo.RegionLocY));

                region.InternalEndPoint =
                    new IPEndPoint(IPAddress.Parse((string) requestData["listen_ip"]), 0);

                region.InternalEndPoint.Port = Convert.ToInt32(requestData["listen_port"]);
                if (0 == region.InternalEndPoint.Port) throw new Exception("listen_port is 0");
                if (m_application.SceneManager.TryGetScene(region.InternalEndPoint, out scene))
                    throw new Exception(
                        String.Format(
                            "region internal IP {0} and port {1} already in use by region {2}, UUID {3}, <{4},{5}>",
                            region.InternalEndPoint.Address,
                            region.InternalEndPoint.Port,
                            scene.RegionInfo.RegionName, scene.RegionInfo.RegionID,
                            scene.RegionInfo.RegionLocX, scene.RegionInfo.RegionLocY));

                region.ExternalHostName = (string) requestData["external_address"];

                bool persist = Convert.ToBoolean(requestData["persist"]);
                if (persist)
                {
                    // default place for region configuration files is in the
                    // Regions directory of the config dir (aka /bin)
                    string regionConfigPath = Path.Combine(Util.configDir(), "Regions");
                    try
                    {
                        // OpenSim.ini can specify a different regions dir
                        IConfig startupConfig = (IConfig) m_configSource.Configs["Startup"];
                        regionConfigPath = startupConfig.GetString("regionload_regionsdir", regionConfigPath).Trim();
                    }
                    catch (Exception)
                    {
                        // No INI setting recorded.
                    }
                    
                    string regionIniPath;
                    
                    if (requestData.Contains("region_file"))
                    {
                        // Make sure that the file to be created is in a subdirectory of the region storage directory.
                        string requestedFilePath = Path.Combine(regionConfigPath, (string) requestData["region_file"]);
                        string requestedDirectory = Path.GetDirectoryName(Path.GetFullPath(requestedFilePath));
                        if (requestedDirectory.StartsWith(Path.GetFullPath(regionConfigPath)))
                            regionIniPath = requestedFilePath;
                        else
                            throw new Exception("Invalid location for region file.");
                    }
                    else
                    {
                        regionIniPath = Path.Combine(regionConfigPath,
                                                        String.Format(
                                                            m_config.GetString("region_file_template",
                                                                               "{0}x{1}-{2}.ini"),
                                                            region.RegionLocX.ToString(),
                                                            region.RegionLocY.ToString(),
                                                            regionID.ToString(),
                                                            region.InternalEndPoint.Port.ToString(),
                                                            region.RegionName.Replace(" ", "_").Replace(":", "_").
                                                                Replace("/", "_")));
                    }
                    
                    m_log.DebugFormat("[RADMIN] CreateRegion: persisting region {0} to {1}",
                                      region.RegionID, regionIniPath);
                    region.SaveRegionToFile("dynamic region", regionIniPath);
                }
                else
                {
                    region.Persistent = false;
                }
                    
                // Set the estate
                
                // Check for an existing estate
                List<int> estateIDs = m_application.EstateDataService.GetEstates((string) requestData["estate_name"]);
                if (estateIDs.Count < 1)
                {
                    UUID userID = UUID.Zero;
                    if (requestData.ContainsKey("estate_owner_uuid"))
                    {
                        // ok, client wants us to use an explicit UUID
                        // regardless of what the avatar name provided
                        userID = new UUID((string) requestData["estate_owner_uuid"]);
                        
                        // Check that the specified user exists
                        Scene currentOrFirst = m_application.SceneManager.CurrentOrFirstScene;
                        IUserAccountService accountService = currentOrFirst.UserAccountService;
                        UserAccount user = accountService.GetUserAccount(currentOrFirst.RegionInfo.ScopeID, userID);
                        
                        if (user == null)
                            throw new Exception("Specified user was not found.");
                    }
                    else if (requestData.ContainsKey("estate_owner_first") & requestData.ContainsKey("estate_owner_last"))
                    {
                        // We need to look up the UUID for the avatar with the provided name.
                        string ownerFirst = (string) requestData["estate_owner_first"];
                        string ownerLast = (string) requestData["estate_owner_last"];
                        
                        Scene currentOrFirst = m_application.SceneManager.CurrentOrFirstScene;
                        IUserAccountService accountService = currentOrFirst.UserAccountService;
                        UserAccount user = accountService.GetUserAccount(currentOrFirst.RegionInfo.ScopeID,
                                                                           ownerFirst, ownerLast);
                        
                        // Check that the specified user exists
                        if (user == null)
                            throw new Exception("Specified user was not found.");
                        
                        userID = user.PrincipalID;
                    }
                    else
                    {
                        throw new Exception("Estate owner details not provided.");
                    }
                    
                    // Create a new estate with the name provided
                    region.EstateSettings = m_application.EstateDataService.CreateNewEstate();

                    region.EstateSettings.EstateName = (string) requestData["estate_name"];
                    region.EstateSettings.EstateOwner = userID;
                    // Persistence does not seem to effect the need to save a new estate
                    m_application.EstateDataService.StoreEstateSettings(region.EstateSettings);

                    if (!m_application.EstateDataService.LinkRegion(region.RegionID, (int) region.EstateSettings.EstateID))
                        throw new Exception("Failed to join estate.");
                }
                else
                {
                    int estateID = estateIDs[0];

                    region.EstateSettings = m_application.EstateDataService.LoadEstateSettings(region.RegionID, false);

                    if (region.EstateSettings.EstateID != estateID)
                    {
                        // The region is already part of an estate, but not the one we want.
                        region.EstateSettings = m_application.EstateDataService.LoadEstateSettings(estateID);

                        if (!m_application.EstateDataService.LinkRegion(region.RegionID, estateID))
                            throw new Exception("Failed to join estate.");
                    }
                }
                
                // Create the region and perform any initial initialization

                IScene newScene;
                m_application.CreateRegion(region, out newScene);
                newScene.Start();

                // If an access specification was provided, use it.
                // Otherwise accept the default.
                newScene.RegionInfo.EstateSettings.PublicAccess = GetBoolean(requestData, "public", m_publicAccess);
                m_application.EstateDataService.StoreEstateSettings(newScene.RegionInfo.EstateSettings);

                // enable voice on newly created region if
                // requested by either the XmlRpc request or the
                // configuration
                if (GetBoolean(requestData, "enable_voice", m_enableVoiceForNewRegions))
                {
                    List<ILandObject> parcels = ((Scene)newScene).LandChannel.AllParcels();

                    foreach (ILandObject parcel in parcels)
                    {
                        parcel.LandData.Flags |= (uint) ParcelFlags.AllowVoiceChat;
                        parcel.LandData.Flags |= (uint) ParcelFlags.UseEstateVoiceChan;
                        ((Scene)newScene).LandChannel.UpdateLandObject(parcel.LandData.LocalID, parcel.LandData);
                    }
                }

                //Load Heightmap if specified to new region
                if (requestData.Contains("heightmap_file"))
                {
                    LoadHeightmap((string)requestData["heightmap_file"], region.RegionID);
                }

                responseData["success"] = true;
                responseData["region_name"] = region.RegionName;
                responseData["region_id"] = region.RegionID.ToString();

                m_log.Info("[RADMIN]: CreateRegion: request complete");
            }
        }

        /// <summary>
        /// Delete a new region.
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcDeleteRegionMethod takes the following XMLRPC
        /// parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>password</term>
        ///       <description>admin password as set in OpenSim.ini</description></item>
        /// <item><term>region_name</term>
        ///       <description>desired region name</description></item>
        /// <item><term>region_id</term>
        ///       <description>(optional) desired region UUID</description></item>
        /// </list>
        ///
        /// XmlRpcDeleteRegionMethod returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// <item><term>error</term>
        ///       <description>error message if success is false</description></item>
        /// </list>
        /// </remarks>
        private void XmlRpcDeleteRegionMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: DeleteRegion: new request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            lock (m_requestLock)
            {
                CheckStringParameters(requestData, responseData, new string[] {"region_name"});
                CheckRegionParams(requestData, responseData);

                Scene scene = null;
                GetSceneFromRegionParams(requestData, responseData, out scene);

                m_application.RemoveRegion(scene, true);

                responseData["success"] = true;
                responseData["region_name"] = scene.RegionInfo.RegionName;
                responseData["region_id"] = scene.RegionInfo.RegionID;

                m_log.Info("[RADMIN]: DeleteRegion: request complete");
            }
        }

        /// <summary>
        /// Close a region.
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcCloseRegionMethod takes the following XMLRPC
        /// parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>password</term>
        ///       <description>admin password as set in OpenSim.ini</description></item>
        /// <item><term>region_name</term>
        ///       <description>desired region name</description></item>
        /// <item><term>region_id</term>
        ///       <description>(optional) desired region UUID</description></item>
        /// </list>
        ///
        /// XmlRpcShutdownRegionMethod returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// <item><term>region_name</term>
        ///       <description>the region name if success is true</description></item>
        /// <item><term>error</term>
        ///       <description>error message if success is false</description></item>
        /// </list>
        /// </remarks>
        private void XmlRpcCloseRegionMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: CloseRegion: new request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            lock (m_requestLock)
            {
                CheckRegionParams(requestData, responseData);

                Scene scene = null;
                GetSceneFromRegionParams(requestData, responseData, out scene);

                m_application.CloseRegion(scene);

                responseData["success"] = true;
                responseData["region_name"] = scene.RegionInfo.RegionName;
                responseData["region_id"] = scene.RegionInfo.RegionID;

                response.Value = responseData;

                m_log.Info("[RADMIN]: CloseRegion: request complete");
            }
        }

        /// <summary>
        /// Change characteristics of an existing region.
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcModifyRegionMethod takes the following XMLRPC
        /// parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>password</term>
        ///       <description>admin password as set in OpenSim.ini</description></item>
        /// <item><term>region_name</term>
        ///       <description>desired region name</description></item>
        /// <item><term>region_id</term>
        ///       <description>(optional) desired region UUID</description></item>
        /// <item><term>public</term>
        ///       <description>if true, set the region to public
        ///       ('true' or 'false'), else to private</description></item>
        /// <item><term>enable_voice</term>
        ///       <description>if true, enable voice on all parcels of
        ///       the region, else disable</description></item>
        /// </list>
        ///
        /// XmlRpcModifyRegionMethod returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// <item><term>error</term>
        ///       <description>error message if success is false</description></item>
        /// </list>
        /// </remarks>
        private void XmlRpcModifyRegionMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: ModifyRegion: new request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            lock (m_requestLock)
            {
                CheckRegionParams(requestData, responseData);

                Scene scene = null;
                GetSceneFromRegionParams(requestData, responseData, out scene);

                // Modify access
                scene.RegionInfo.EstateSettings.PublicAccess =
                    GetBoolean(requestData,"public", scene.RegionInfo.EstateSettings.PublicAccess);
                if (scene.RegionInfo.Persistent)
                    m_application.EstateDataService.StoreEstateSettings(scene.RegionInfo.EstateSettings);

                if (requestData.ContainsKey("enable_voice"))
                {
                    bool enableVoice = GetBoolean(requestData, "enable_voice", true);
                    List<ILandObject> parcels = ((Scene)scene).LandChannel.AllParcels();

                    foreach (ILandObject parcel in parcels)
                    {
                        if (enableVoice)
                        {
                            parcel.LandData.Flags |= (uint)ParcelFlags.AllowVoiceChat;
                            parcel.LandData.Flags |= (uint)ParcelFlags.UseEstateVoiceChan;
                        }
                        else
                        {
                            parcel.LandData.Flags &= ~(uint)ParcelFlags.AllowVoiceChat;
                            parcel.LandData.Flags &= ~(uint)ParcelFlags.UseEstateVoiceChan;
                        }
                        scene.LandChannel.UpdateLandObject(parcel.LandData.LocalID, parcel.LandData);
                    }
                }

                responseData["success"] = true;
                responseData["region_name"] = scene.RegionInfo.RegionName;
                responseData["region_id"] = scene.RegionInfo.RegionID;

                m_log.Info("[RADMIN]: ModifyRegion: request complete");
            }
        }

        /// <summary>
        /// Create a new user account.
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcCreateUserMethod takes the following XMLRPC
        /// parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>password</term>
        ///       <description>admin password as set in OpenSim.ini</description></item>
        /// <item><term>user_firstname</term>
        ///       <description>avatar's first name</description></item>
        /// <item><term>user_lastname</term>
        ///       <description>avatar's last name</description></item>
        /// <item><term>user_password</term>
        ///       <description>avatar's password</description></item>
        /// <item><term>user_email</term>
        ///       <description>email of the avatar's owner (optional)</description></item>
        /// <item><term>start_region_x</term>
        ///       <description>avatar's start region coordinates, X value</description></item>
        /// <item><term>start_region_y</term>
        ///       <description>avatar's start region coordinates, Y value</description></item>
        /// </list>
        ///
        /// XmlRpcCreateUserMethod returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// <item><term>error</term>
        ///       <description>error message if success is false</description></item>
        /// <item><term>avatar_uuid</term>
        ///       <description>UUID of the newly created avatar
        ///                    account; UUID.Zero if failed.
        ///       </description></item>
        /// </list>
        /// </remarks>
        private void XmlRpcCreateUserMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: CreateUser: new request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            lock (m_requestLock)
            {
                try
                {
                    // check completeness
                    CheckStringParameters(requestData, responseData, new string[]
                                                       {
                                                           "user_firstname",
                                                           "user_lastname", "user_password",
                                                       });
                    CheckIntegerParams(requestData, responseData, new string[] {"start_region_x", "start_region_y"});

                    // do the job
                    string firstName = (string) requestData["user_firstname"];
                    string lastName = (string) requestData["user_lastname"];
                    string password = (string) requestData["user_password"];

                    uint regionXLocation = Convert.ToUInt32(requestData["start_region_x"]);
                    uint regionYLocation = Convert.ToUInt32(requestData["start_region_y"]);

                    string email = ""; // empty string for email
                    if (requestData.Contains("user_email"))
                        email = (string)requestData["user_email"];

                    Scene scene = m_application.SceneManager.CurrentOrFirstScene;
                    UUID scopeID = scene.RegionInfo.ScopeID;

                    UserAccount account = CreateUser(scopeID, firstName, lastName, password, email);

                    if (null == account)
                        throw new Exception(String.Format("failed to create new user {0} {1}",
                                                          firstName, lastName));

                    // Set home position

                    GridRegion home = scene.GridService.GetRegionByPosition(scopeID, 
                                        (int)Util.RegionToWorldLoc(regionXLocation), (int)Util.RegionToWorldLoc(regionYLocation));
                    if (null == home)
                    {
                        m_log.WarnFormat("[RADMIN]: Unable to set home region for newly created user account {0} {1}", firstName, lastName);
                    }
                    else
                    {
                        scene.GridUserService.SetHome(account.PrincipalID.ToString(), home.RegionID, new Vector3(128, 128, 0), new Vector3(0, 1, 0));
                        m_log.DebugFormat("[RADMIN]: Set home region {0} for updated user account {1} {2}", home.RegionID, firstName, lastName);
                    }

                    // Establish the avatar's initial appearance

                    UpdateUserAppearance(responseData, requestData, account.PrincipalID);

                    responseData["success"] = true;
                    responseData["avatar_uuid"] = account.PrincipalID.ToString();

                    m_log.InfoFormat("[RADMIN]: CreateUser: User {0} {1} created, UUID {2}", firstName, lastName, account.PrincipalID);
                }
                catch (Exception e)
                {
                    responseData["avatar_uuid"] = UUID.Zero.ToString();

                    throw e;
                }

                m_log.Info("[RADMIN]: CreateUser: request complete");
            }
        }

        /// <summary>
        /// Check whether a certain user account exists.
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcUserExistsMethod takes the following XMLRPC
        /// parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>password</term>
        ///       <description>admin password as set in OpenSim.ini</description></item>
        /// <item><term>user_firstname</term>
        ///       <description>avatar's first name</description></item>
        /// <item><term>user_lastname</term>
        ///       <description>avatar's last name</description></item>
        /// </list>
        ///
        /// XmlRpcCreateUserMethod returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>user_firstname</term>
        ///       <description>avatar's first name</description></item>
        /// <item><term>user_lastname</term>
        ///       <description>avatar's last name</description></item>
        /// <item><term>user_lastlogin</term>
        ///       <description>avatar's last login time (secs since UNIX epoch)</description></item>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// <item><term>error</term>
        ///       <description>error message if success is false</description></item>
        /// </list>
        /// </remarks>
        private void XmlRpcUserExistsMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: UserExists: new request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            // check completeness
            CheckStringParameters(requestData, responseData, new string[] {"user_firstname", "user_lastname"});

            string firstName = (string) requestData["user_firstname"];
            string lastName = (string) requestData["user_lastname"];

            responseData["user_firstname"] = firstName;
            responseData["user_lastname"] = lastName;

            UUID scopeID = m_application.SceneManager.CurrentOrFirstScene.RegionInfo.ScopeID;

            UserAccount account = m_application.SceneManager.CurrentOrFirstScene.UserAccountService.GetUserAccount(scopeID, firstName, lastName);

            if (null == account)
            {
                responseData["success"] = false;
                responseData["lastlogin"] = 0;
            }
            else
            {
                GridUserInfo userInfo = m_application.SceneManager.CurrentOrFirstScene.GridUserService.GetGridUserInfo(account.PrincipalID.ToString());
                if (userInfo != null)
                    responseData["lastlogin"] = Util.ToUnixTime(userInfo.Login);
                else
                    responseData["lastlogin"] = 0;

                responseData["success"] = true;
            }

            m_log.Info("[RADMIN]: UserExists: request complete");
        }

        /// <summary>
        /// Update a user account.
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcUpdateUserAccountMethod takes the following XMLRPC
        /// parameters (changeable ones are optional)
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>password</term>
        ///       <description>admin password as set in OpenSim.ini</description></item>
        /// <item><term>user_firstname</term>
        ///       <description>avatar's first name (cannot be changed)</description></item>
        /// <item><term>user_lastname</term>
        ///       <description>avatar's last name (cannot be changed)</description></item>
        /// <item><term>user_password</term>
        ///       <description>avatar's password (changeable)</description></item>
        /// <item><term>start_region_x</term>
        ///       <description>avatar's start region coordinates, X
        ///                    value (changeable)</description></item>
        /// <item><term>start_region_y</term>
        ///       <description>avatar's start region coordinates, Y
        ///                    value (changeable)</description></item>
        /// <item><term>about_real_world (not implemented yet)</term>
        ///       <description>"about" text of avatar owner (changeable)</description></item>
        /// <item><term>about_virtual_world (not implemented yet)</term>
        ///       <description>"about" text of avatar (changeable)</description></item>
        /// </list>
        ///
        /// XmlRpcCreateUserMethod returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// <item><term>error</term>
        ///       <description>error message if success is false</description></item>
        /// <item><term>avatar_uuid</term>
        ///       <description>UUID of the updated avatar
        ///                    account; UUID.Zero if failed.
        ///       </description></item>
        /// </list>
        /// </remarks>
        private void XmlRpcUpdateUserAccountMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: UpdateUserAccount: new request");
            m_log.Warn("[RADMIN]: This method needs update for 0.7");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            lock (m_requestLock)
            {
                try
                {
                    // check completeness
                    CheckStringParameters(requestData, responseData, new string[] {
                            "user_firstname",
                            "user_lastname"});

                    // do the job
                    string firstName = (string) requestData["user_firstname"];
                    string lastName = (string) requestData["user_lastname"];

                    string password = String.Empty;
                    uint? regionXLocation = null;
                    uint? regionYLocation = null;
            //        uint? ulaX = null;
            //        uint? ulaY = null;
            //        uint? ulaZ = null;
            //        uint? usaX = null;
            //        uint? usaY = null;
            //        uint? usaZ = null;
            //        string aboutFirstLive = String.Empty;
            //        string aboutAvatar = String.Empty;

                    if (requestData.ContainsKey("user_password")) password = (string) requestData["user_password"];
                    if (requestData.ContainsKey("start_region_x"))
                        regionXLocation = Convert.ToUInt32(requestData["start_region_x"]);
                    if (requestData.ContainsKey("start_region_y"))
                        regionYLocation = Convert.ToUInt32(requestData["start_region_y"]);

            //        if (requestData.ContainsKey("start_lookat_x"))
            //            ulaX = Convert.ToUInt32((Int32) requestData["start_lookat_x"]);
            //        if (requestData.ContainsKey("start_lookat_y"))
            //            ulaY = Convert.ToUInt32((Int32) requestData["start_lookat_y"]);
            //        if (requestData.ContainsKey("start_lookat_z"))
            //            ulaZ = Convert.ToUInt32((Int32) requestData["start_lookat_z"]);

            //        if (requestData.ContainsKey("start_standat_x"))
            //            usaX = Convert.ToUInt32((Int32) requestData["start_standat_x"]);
            //        if (requestData.ContainsKey("start_standat_y"))
            //            usaY = Convert.ToUInt32((Int32) requestData["start_standat_y"]);
            //        if (requestData.ContainsKey("start_standat_z"))
            //            usaZ = Convert.ToUInt32((Int32) requestData["start_standat_z"]);
            //        if (requestData.ContainsKey("about_real_world"))
            //            aboutFirstLive = (string)requestData["about_real_world"];
            //        if (requestData.ContainsKey("about_virtual_world"))
            //            aboutAvatar = (string)requestData["about_virtual_world"];

                    Scene scene = m_application.SceneManager.CurrentOrFirstScene;
                    UUID scopeID = scene.RegionInfo.ScopeID;
                    UserAccount account = scene.UserAccountService.GetUserAccount(scopeID, firstName, lastName);

                    if (null == account)
                        throw new Exception(String.Format("avatar {0} {1} does not exist", firstName, lastName));

                    if (!String.IsNullOrEmpty(password))
                    {
                        m_log.DebugFormat("[RADMIN]: UpdateUserAccount: updating password for avatar {0} {1}", firstName, lastName);
                        ChangeUserPassword(firstName, lastName, password);
                    }

            //        if (null != usaX) userProfile.HomeLocationX = (uint) usaX;
            //        if (null != usaY) userProfile.HomeLocationY = (uint) usaY;
            //        if (null != usaZ) userProfile.HomeLocationZ = (uint) usaZ;

            //        if (null != ulaX) userProfile.HomeLookAtX = (uint) ulaX;
            //        if (null != ulaY) userProfile.HomeLookAtY = (uint) ulaY;
            //        if (null != ulaZ) userProfile.HomeLookAtZ = (uint) ulaZ;

            //        if (String.Empty != aboutFirstLive) userProfile.FirstLifeAboutText = aboutFirstLive;
            //        if (String.Empty != aboutAvatar) userProfile.AboutText = aboutAvatar;

                    // Set home position

                    if ((null != regionXLocation) && (null != regionYLocation))
                    {
                        GridRegion home = scene.GridService.GetRegionByPosition(scopeID, 
                                        (int)Util.RegionToWorldLoc((uint)regionXLocation), (int)Util.RegionToWorldLoc((uint)regionYLocation));
                        if (null == home) {
                            m_log.WarnFormat("[RADMIN]: Unable to set home region for updated user account {0} {1}", firstName, lastName);
                        } else {
                            scene.GridUserService.SetHome(account.PrincipalID.ToString(), home.RegionID, new Vector3(128, 128, 0), new Vector3(0, 1, 0));
                            m_log.DebugFormat("[RADMIN]: Set home region {0} for updated user account {1} {2}", home.RegionID, firstName, lastName);
                        }
                    }

                    // User has been created. Now establish gender and appearance.

                    UpdateUserAppearance(responseData, requestData, account.PrincipalID);

                    responseData["success"] = true;
                    responseData["avatar_uuid"] = account.PrincipalID.ToString();

                    m_log.InfoFormat("[RADMIN]: UpdateUserAccount: account for user {0} {1} updated, UUID {2}",
                                     firstName, lastName,
                                     account.PrincipalID);
                }
                catch (Exception e)
                {
                    responseData["avatar_uuid"] = UUID.Zero.ToString();

                    throw e;
                }
                
                m_log.Info("[RADMIN]: UpdateUserAccount: request complete");
            }
        }

        /// <summary>
        /// Authenticate an user.
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcAuthenticateUserMethod takes the following XMLRPC
        /// parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>password</term>
        ///       <description>admin password as set in OpenSim.ini</description></item>
        /// <item><term>user_firstname</term>
        ///       <description>avatar's first name</description></item>
        /// <item><term>user_lastname</term>
        ///       <description>avatar's last name</description></item>
        /// <item><term>user_password</term>
        ///       <description>MD5 hash of avatar's password</description></item>
        /// <item><term>token_lifetime</term>
        ///       <description>the lifetime of the returned token (upper bounded to 30s)</description></item>
        /// </list>
        ///
        /// XmlRpcAuthenticateUserMethod returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// <item><term>token</term>
        ///       <description>the authentication token sent by OpenSim</description></item>
        /// <item><term>error</term>
        ///       <description>error message if success is false</description></item>
        /// </list>
        /// </remarks>
        private void XmlRpcAuthenticateUserMethod(XmlRpcRequest request, XmlRpcResponse response,
                                                   IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: AuthenticateUser: new request");

            var responseData = (Hashtable)response.Value;
            var requestData = (Hashtable)request.Params[0];

            lock (m_requestLock)
            {
                try
                {
                    CheckStringParameters(requestData, responseData, new[]
                                                                         {
                                                                             "user_firstname",
                                                                             "user_lastname",
                                                                             "user_password",
                                                                             "token_lifetime"
                                                                         });

                    var firstName = (string)requestData["user_firstname"];
                    var lastName = (string)requestData["user_lastname"];
                    var password = (string)requestData["user_password"];

                    var scene = m_application.SceneManager.CurrentOrFirstScene;

                    if (scene.Equals(null))
                    {
                        m_log.Debug("scene does not exist");
                        throw new Exception("Scene does not exist.");
                    }

                    var scopeID = scene.RegionInfo.ScopeID;
                    var account = scene.UserAccountService.GetUserAccount(scopeID, firstName, lastName);

                    if (account.Equals(null) || account.PrincipalID.Equals(UUID.Zero))
                    {
                        m_log.DebugFormat("avatar {0} {1} does not exist", firstName, lastName);
                        throw new Exception(String.Format("avatar {0} {1} does not exist", firstName, lastName));
                    }

                    if (String.IsNullOrEmpty(password))
                    {
                        m_log.DebugFormat("[RADMIN]: AuthenticateUser: no password provided for {0} {1}", firstName,
                                          lastName);
                        throw new Exception(String.Format("no password provided for {0} {1}", firstName,
                                          lastName));
                    }

                    int lifetime;
                    if (int.TryParse((string)requestData["token_lifetime"], NumberStyles.Integer, CultureInfo.InvariantCulture, out lifetime) == false)
                    {
                        m_log.DebugFormat("[RADMIN]: AuthenticateUser: no token lifetime provided for {0} {1}", firstName,
                                          lastName);
                        throw new Exception(String.Format("no token lifetime provided for {0} {1}", firstName,
                                          lastName));
                    }

                    // Upper bound on lifetime set to 30s.
                    if (lifetime > 30)
                    {
                        m_log.DebugFormat("[RADMIN]: AuthenticateUser: token lifetime longer than 30s for {0} {1}", firstName,
                                          lastName);
                        throw new Exception(String.Format("token lifetime longer than 30s for {0} {1}", firstName,
                                          lastName));
                    }

                    var authModule = scene.RequestModuleInterface<IAuthenticationService>();
                    if (authModule == null)
                    {
                        m_log.Debug("[RADMIN]: AuthenticateUser: no authentication module loded");
                        throw new Exception("no authentication module loaded");
                    }

                    var token = authModule.Authenticate(account.PrincipalID, password, lifetime);
                    if (String.IsNullOrEmpty(token))
                    {
                        m_log.DebugFormat("[RADMIN]: AuthenticateUser: authentication failed for {0} {1}", firstName,
                            lastName);
                        throw new Exception(String.Format("authentication failed for {0} {1}", firstName,
                            lastName));
                    }

                    m_log.DebugFormat("[RADMIN]: AuthenticateUser: account for user {0} {1} identified with token {2}",
                        firstName, lastName, token);

                    responseData["token"] = token;
                    responseData["success"] = true;

                }
                catch (Exception e)
                {
                    responseData["success"] = false;
                    responseData["error"] = e.Message;
                    throw e;
                }

                m_log.Info("[RADMIN]: AuthenticateUser: request complete");
            }
        }

        /// <summary>
        /// Load an OAR file into a region..
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcLoadOARMethod takes the following XMLRPC
        /// parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>password</term>
        ///       <description>admin password as set in OpenSim.ini</description></item>
        /// <item><term>filename</term>
        ///       <description>file name of the OAR file</description></item>
        /// <item><term>region_uuid</term>
        ///       <description>UUID of the region</description></item>
        /// <item><term>region_name</term>
        ///       <description>region name</description></item>
        /// <item><term>merge</term>
        ///       <description>true if oar should be merged</description></item>
        /// <item><term>skip-assets</term>
        ///       <description>true if assets should be skiped</description></item>
        /// </list>
        ///
        /// <code>region_uuid</code> takes precedence over
        /// <code>region_name</code> if both are present; one of both
        /// must be present.
        ///
        /// XmlRpcLoadOARMethod returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// <item><term>error</term>
        ///       <description>error message if success is false</description></item>
        /// </list>
        /// </remarks>
        private void XmlRpcLoadOARMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Received Load OAR Administrator Request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            lock (m_requestLock)
            {
                try
                {
                    CheckStringParameters(requestData, responseData, new string[] {"filename"});
                    CheckRegionParams(requestData, responseData);

                    Scene scene = null;
                    GetSceneFromRegionParams(requestData, responseData, out scene);

                    string filename = (string) requestData["filename"];
                    
                    bool mergeOar = false;
                    bool skipAssets = false;

                    if ((string)requestData["merge"] == "true")
                    {
                        mergeOar = true;
                    }
                    if ((string)requestData["skip-assets"] == "true")
                    {
                        skipAssets = true;
                    }

                    IRegionArchiverModule archiver = scene.RequestModuleInterface<IRegionArchiverModule>();
                    Dictionary<string, object> archiveOptions = new Dictionary<string, object>();
                    if (mergeOar) archiveOptions.Add("merge", null);
                    if (skipAssets) archiveOptions.Add("skipAssets", null);
                    if (archiver != null)
                        archiver.DearchiveRegion(filename, Guid.Empty, archiveOptions);
                    else
                        throw new Exception("Archiver module not present for scene");

                    responseData["loaded"] = true;
                }
                catch (Exception e)
                {
                    responseData["loaded"] = false;

                    throw e;
                }

                m_log.Info("[RADMIN]: Load OAR Administrator Request complete");
            }
        }

        /// <summary>
        /// Save a region to an OAR file
        /// <summary>
        /// <param name="request">incoming XML RPC request</param>
        /// <remarks>
        /// XmlRpcSaveOARMethod takes the following XMLRPC
        /// parameters
        /// <list type="table">
        /// <listheader><term>parameter name</term><description>description</description></listheader>
        /// <item><term>password</term>
        ///       <description>admin password as set in OpenSim.ini</description></item>
        /// <item><term>filename</term>
        ///       <description>file name for the OAR file</description></item>
        /// <item><term>region_uuid</term>
        ///       <description>UUID of the region</description></item>
        /// <item><term>region_name</term>
        ///       <description>region name</description></item>
        /// <item><term>profile</term>
        ///       <description>profile url</description></item>
        /// <item><term>noassets</term>
        ///       <description>true if no assets should be saved</description></item>
        /// <item><term>all</term>
        ///       <description>true to save all the regions in the simulator</description></item>
        /// <item><term>perm</term>
        ///       <description>C and/or T</description></item>
        /// </list>
        ///
        /// <code>region_uuid</code> takes precedence over
        /// <code>region_name</code> if both are present; one of both
        /// must be present.
        ///
        /// XmlRpcLoadOARMethod returns
        /// <list type="table">
        /// <listheader><term>name</term><description>description</description></listheader>
        /// <item><term>success</term>
        ///       <description>true or false</description></item>
        /// <item><term>error</term>
        ///       <description>error message if success is false</description></item>
        /// </list>
        /// </remarks>
        private void XmlRpcSaveOARMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Received Save OAR Request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            try
            {
                CheckStringParameters(requestData, responseData, new string[] {"filename"});
                CheckRegionParams(requestData, responseData);

                Scene scene = null;
                GetSceneFromRegionParams(requestData, responseData, out scene);

                string filename = (string)requestData["filename"];

                Dictionary<string, object> options = new Dictionary<string, object>();

                //if (requestData.Contains("version"))
                //{
                //    options["version"] = (string)requestData["version"];
                //}

                if (requestData.Contains("home"))
                {
                    options["home"] = (string)requestData["home"];
                }

                if ((string)requestData["noassets"] == "true")
                {
                    options["noassets"] = (string)requestData["noassets"] ;
                }

                if (requestData.Contains("perm"))
                {
                    options["checkPermissions"] = (string)requestData["perm"];
                }

                if ((string)requestData["all"] == "true")
                {
                    options["all"] = (string)requestData["all"];
                }

                IRegionArchiverModule archiver = scene.RequestModuleInterface<IRegionArchiverModule>();

                if (archiver != null)
                {
                    Guid requestId = Guid.NewGuid();
                    scene.EventManager.OnOarFileSaved += RemoteAdminOarSaveCompleted;

                    m_log.InfoFormat(
                        "[RADMIN]: Submitting save OAR request for {0} to file {1}, request ID {2}", 
                        scene.Name, filename, requestId);

                    archiver.ArchiveRegion(filename, requestId, options);

                    lock (m_saveOarLock)
                        Monitor.Wait(m_saveOarLock,5000);

                    scene.EventManager.OnOarFileSaved -= RemoteAdminOarSaveCompleted;
                }
                else
                {
                    throw new Exception("Archiver module not present for scene");
                }

                responseData["saved"] = true;
            }
            catch (Exception e)
            {
                responseData["saved"] = false;

                throw e;
            }

            m_log.Info("[RADMIN]: Save OAR Request complete");
        }

        private void RemoteAdminOarSaveCompleted(Guid uuid, string name)
        {
            if (name != "")
                m_log.ErrorFormat("[RADMIN]: Saving of OAR file with request ID {0} failed with message {1}", uuid, name);
            else
                m_log.DebugFormat("[RADMIN]: Saved OAR file for request {0}", uuid);

            lock (m_saveOarLock)
                Monitor.Pulse(m_saveOarLock);
        }

        private void XmlRpcLoadXMLMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Received Load XML Administrator Request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            lock (m_requestLock)
            {
                try
                {
                    CheckStringParameters(requestData, responseData, new string[] {"filename"});
                    CheckRegionParams(requestData, responseData);

                    Scene scene = null;
                    GetSceneFromRegionParams(requestData, responseData, out scene);

                    string filename = (string) requestData["filename"];

                    responseData["switched"] = true;

                    string xml_version = "1";
                    if (requestData.Contains("xml_version"))
                    {
                        xml_version = (string) requestData["xml_version"];
                    }

                    switch (xml_version)
                    {
                        case "1":
                            m_application.SceneManager.LoadCurrentSceneFromXml(filename, true, new Vector3(0, 0, 0));
                            break;

                        case "2":
                            m_application.SceneManager.LoadCurrentSceneFromXml2(filename);
                            break;

                        default:
                            throw new Exception(String.Format("unknown Xml{0} format", xml_version));
                    }

                    responseData["loaded"] = true;
                }
                catch (Exception e)
                {
                    responseData["loaded"] = false;
                    responseData["switched"] = false;

                    throw e;
                }

                m_log.Info("[RADMIN]: Load XML Administrator Request complete");
            }
        }

        private void XmlRpcSaveXMLMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Received Save XML Administrator Request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            try
            {
                CheckStringParameters(requestData, responseData, new string[] {"filename"});
                CheckRegionParams(requestData, responseData);

                Scene scene = null;
                GetSceneFromRegionParams(requestData, responseData, out scene);

                string filename = (string) requestData["filename"];

                responseData["switched"] = true;

                string xml_version = "1";
                if (requestData.Contains("xml_version"))
                {
                    xml_version = (string) requestData["xml_version"];
                }

                switch (xml_version)
                {
                    case "1":
                        m_application.SceneManager.SaveCurrentSceneToXml(filename);
                        break;

                    case "2":
                        m_application.SceneManager.SaveCurrentSceneToXml2(filename);
                        break;

                    default:
                        throw new Exception(String.Format("unknown Xml{0} format", xml_version));
                }

                responseData["saved"] = true;
            }
            catch (Exception e)
            {
                responseData["saved"] = false;
                responseData["switched"] = false;

                throw e;
            }

            m_log.Info("[RADMIN]: Save XML Administrator Request complete");
        }

        private void XmlRpcRegionQueryMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            int flags = 0;
            string text = String.Empty;
            int health = 0;
            responseData["success"] = true;

            CheckRegionParams(requestData, responseData);

            Scene scene = null;
            try
            {
                GetSceneFromRegionParams(requestData, responseData, out scene);
                health = scene.GetHealth(out flags, out text);
            }
            catch (Exception e)
            {
                responseData["error"] = null;
            }

            responseData["success"] = true;
            responseData["health"] = health;
            responseData["flags"] = flags;
            responseData["message"] = text;
        }

        private void XmlRpcConsoleCommandMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Received Command XML Administrator Request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            CheckStringParameters(requestData, responseData, new string[] {"command"});

            MainConsole.Instance.RunCommand(requestData["command"].ToString());

            m_log.Info("[RADMIN]: Command XML Administrator Request complete");
        }

        private void XmlRpcAccessListClear(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Received Access List Clear Request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            responseData["success"] = true;

            CheckRegionParams(requestData, responseData);

            Scene scene = null;
            GetSceneFromRegionParams(requestData, responseData, out scene);

            scene.RegionInfo.EstateSettings.EstateAccess = new UUID[]{};

            if (scene.RegionInfo.Persistent)
                m_application.EstateDataService.StoreEstateSettings(scene.RegionInfo.EstateSettings);

            m_log.Info("[RADMIN]: Access List Clear Request complete");
        }

        private void XmlRpcAccessListAdd(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Received Access List Add Request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            CheckRegionParams(requestData, responseData);

            Scene scene = null;
            GetSceneFromRegionParams(requestData, responseData, out scene);

            int addedUsers = 0;

            if (requestData.Contains("users"))
            {
                UUID scopeID = scene.RegionInfo.ScopeID;
                IUserAccountService userService = scene.UserAccountService;
                Hashtable users = (Hashtable) requestData["users"];
                List<UUID> uuids = new List<UUID>();
                foreach (string name in users.Values)
                {
                    string[] parts = name.Split();
                    UserAccount account = userService.GetUserAccount(scopeID, parts[0], parts[1]);
                    if (account != null)
                    {
                        uuids.Add(account.PrincipalID);
                        m_log.DebugFormat("[RADMIN]: adding \"{0}\" to ACL for \"{1}\"", name, scene.RegionInfo.RegionName);
                    }
                }
                List<UUID> accessControlList = new List<UUID>(scene.RegionInfo.EstateSettings.EstateAccess);
                foreach (UUID uuid in uuids)
                {
                   if (!accessControlList.Contains(uuid))
                    {
                        accessControlList.Add(uuid);
                        addedUsers++;
                    }
                }
                scene.RegionInfo.EstateSettings.EstateAccess = accessControlList.ToArray();
                if (scene.RegionInfo.Persistent)
                    m_application.EstateDataService.StoreEstateSettings(scene.RegionInfo.EstateSettings);
            }

            responseData["added"] = addedUsers;

            m_log.Info("[RADMIN]: Access List Add Request complete");
        }

        private void XmlRpcAccessListRemove(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Received Access List Remove Request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            CheckRegionParams(requestData, responseData);

            Scene scene = null;
            GetSceneFromRegionParams(requestData, responseData, out scene);

            int removedUsers = 0;

            if (requestData.Contains("users"))
            {
                UUID scopeID = scene.RegionInfo.ScopeID;
                IUserAccountService userService = scene.UserAccountService;
                //UserProfileCacheService ups = m_application.CommunicationsManager.UserProfileCacheService;
                Hashtable users = (Hashtable) requestData["users"];
                List<UUID> uuids = new List<UUID>();
                foreach (string name in users.Values)
                {
                    string[] parts = name.Split();
                    UserAccount account = userService.GetUserAccount(scopeID, parts[0], parts[1]);
                    if (account != null)
                    {
                        uuids.Add(account.PrincipalID);
                    }
                }
                List<UUID> accessControlList = new List<UUID>(scene.RegionInfo.EstateSettings.EstateAccess);
                foreach (UUID uuid in uuids)
                {
                   if (accessControlList.Contains(uuid))
                    {
                        accessControlList.Remove(uuid);
                        removedUsers++;
                    }
                }
                scene.RegionInfo.EstateSettings.EstateAccess = accessControlList.ToArray();
                if (scene.RegionInfo.Persistent)
                    m_application.EstateDataService.StoreEstateSettings(scene.RegionInfo.EstateSettings);
            }

            responseData["removed"] = removedUsers;
            responseData["success"] = true;

            m_log.Info("[RADMIN]: Access List Remove Request complete");
        }

        private void XmlRpcAccessListList(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Received Access List List Request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            CheckRegionParams(requestData, responseData);

            Scene scene = null;
            GetSceneFromRegionParams(requestData, responseData, out scene);

            UUID[] accessControlList = scene.RegionInfo.EstateSettings.EstateAccess;
            Hashtable users = new Hashtable();

            foreach (UUID user in accessControlList)
            {
                UUID scopeID = scene.RegionInfo.ScopeID;
                UserAccount account = scene.UserAccountService.GetUserAccount(scopeID, user);
                if (account != null)
                {
                    users[user.ToString()] = account.FirstName + " " + account.LastName;
                }
            }

            responseData["users"] = users;
            responseData["success"] = true;

            m_log.Info("[RADMIN]: Access List List Request complete");
        }

        private void XmlRpcEstateReload(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Received Estate Reload Request");

            Hashtable responseData = (Hashtable)response.Value;
//            Hashtable requestData = (Hashtable)request.Params[0];

            m_application.SceneManager.ForEachScene(s => 
                s.RegionInfo.EstateSettings = m_application.EstateDataService.LoadEstateSettings(s.RegionInfo.RegionID, false)                
            );

            responseData["success"] = true;

            m_log.Info("[RADMIN]: Estate Reload Request complete");
        }

        private void XmlRpcGetAgentsMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            bool includeChildren = false;

            if (requestData.Contains("include_children"))
                bool.TryParse((string)requestData["include_children"], out includeChildren);

            Scene scene;
            GetSceneFromRegionParams(requestData, responseData, out scene);

            ArrayList xmlRpcRegions = new ArrayList();
            responseData["regions"] = xmlRpcRegions;

            Hashtable xmlRpcRegion = new Hashtable();
            xmlRpcRegions.Add(xmlRpcRegion);

            xmlRpcRegion["name"] = scene.Name;
            xmlRpcRegion["id"] = scene.RegionInfo.RegionID.ToString();

            List<ScenePresence> agents = scene.GetScenePresences();
            ArrayList xmlrpcAgents = new ArrayList();

            foreach (ScenePresence agent in agents)
            {
                if (agent.IsChildAgent && !includeChildren)
                    continue;

                Hashtable xmlRpcAgent = new Hashtable();
                xmlRpcAgent.Add("name", agent.Name);
                xmlRpcAgent.Add("id", agent.UUID.ToString());
                xmlRpcAgent.Add("type", agent.PresenceType.ToString());
                xmlRpcAgent.Add("current_parcel_id", agent.currentParcelUUID.ToString());

                Vector3 pos = agent.AbsolutePosition;
                xmlRpcAgent.Add("pos_x", pos.X.ToString());
                xmlRpcAgent.Add("pos_y", pos.Y.ToString());
                xmlRpcAgent.Add("pos_z", pos.Z.ToString());

                Vector3 lookAt = agent.Lookat;
                xmlRpcAgent.Add("lookat_x", lookAt.X.ToString());
                xmlRpcAgent.Add("lookat_y", lookAt.Y.ToString());
                xmlRpcAgent.Add("lookat_z", lookAt.Z.ToString());

                Vector3 vel = agent.Velocity;
                xmlRpcAgent.Add("vel_x", vel.X.ToString());
                xmlRpcAgent.Add("vel_y", vel.Y.ToString());
                xmlRpcAgent.Add("vel_z", vel.Z.ToString());

                xmlRpcAgent.Add("is_flying", agent.Flying.ToString());
                xmlRpcAgent.Add("is_sat_on_ground", agent.SitGround.ToString());
                xmlRpcAgent.Add("is_sat_on_object", agent.IsSatOnObject.ToString());

                xmlrpcAgents.Add(xmlRpcAgent);
            }

            m_log.DebugFormat(
                "[REMOTE ADMIN]: XmlRpcGetAgents found {0} agents in {1}", xmlrpcAgents.Count, scene.Name);

            xmlRpcRegion["agents"] = xmlrpcAgents;
            responseData["success"] = true;
        }

        private void XmlRpcTeleportAgentMethod(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            UUID agentId;
            string regionName = null;
            Vector3 pos, lookAt;
            ScenePresence sp = null;

            if (requestData.Contains("agent_first_name") && requestData.Contains("agent_last_name"))
            {
                string firstName = requestData["agent_first_name"].ToString();
                string lastName = requestData["agent_last_name"].ToString();
                m_application.SceneManager.TryGetRootScenePresenceByName(firstName, lastName, out sp);

                if (sp == null)
                    throw new Exception(
                        string.Format(
                            "No agent found with agent_first_name {0} and agent_last_name {1}", firstName, lastName));
            }
            else if (requestData.Contains("agent_id"))
            {
                string rawAgentId = (string)requestData["agent_id"];

                if (!UUID.TryParse(rawAgentId, out agentId))
                    throw new Exception(string.Format("agent_id {0} does not have the correct id format", rawAgentId));

                m_application.SceneManager.TryGetRootScenePresence(agentId, out sp);

                if (sp == null)
                    throw new Exception(string.Format("No agent with agent_id {0} found in this simulator", agentId));
            }
            else
            {
                throw new Exception("No agent_id or agent_first_name and agent_last_name parameters specified");
            }

            if (requestData.Contains("region_name"))
                regionName = (string)requestData["region_name"];

            pos.X = ParseFloat(requestData, "pos_x", sp.AbsolutePosition.X);
            pos.Y = ParseFloat(requestData, "pos_y", sp.AbsolutePosition.Y);
            pos.Z = ParseFloat(requestData, "pos_z", sp.AbsolutePosition.Z);
            lookAt.X = ParseFloat(requestData, "lookat_x", sp.Lookat.X);
            lookAt.Y = ParseFloat(requestData, "lookat_y", sp.Lookat.Y);
            lookAt.Z = ParseFloat(requestData, "lookat_z", sp.Lookat.Z);

            sp.Scene.RequestTeleportLocation(
                sp.ControllingClient, regionName, pos, lookAt, (uint)Constants.TeleportFlags.ViaLocation);

            // We have no way of telling the failure of the actual teleport
            responseData["success"] = true;
        }

        private void XmlRpcResetLand(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = (Hashtable)response.Value;
            string musicURL = string.Empty;
            UUID groupID = UUID.Zero;
            uint flags = 0;
            bool set_group = false, set_music = false, set_flags = false;

            if (requestData.Contains("group") && requestData["group"] != null)
                set_group = UUID.TryParse(requestData["group"].ToString(), out groupID);
            if (requestData.Contains("music") && requestData["music"] != null)
            {

                musicURL = requestData["music"].ToString();
                set_music = true;
            }

            if (requestData.Contains("flags") && requestData["flags"] != null)
                set_flags = UInt32.TryParse(requestData["flags"].ToString(), out flags);

            m_log.InfoFormat("[RADMIN]: Received Reset Land Request group={0} musicURL={1} flags={2}",
                (set_group ? groupID.ToString() : "unchanged"),
                (set_music ? musicURL : "unchanged"),
                (set_flags ? flags.ToString() : "unchanged"));

            m_application.SceneManager.ForEachScene(delegate (Scene s)
            {
                List<ILandObject> parcels = s.LandChannel.AllParcels();
                foreach (ILandObject p in parcels)
                {
                    if (set_music)
                        p.LandData.MusicURL = musicURL;
                    if (set_group)
                        p.LandData.GroupID = groupID;
                    if (set_flags)
                        p.LandData.Flags = flags;
                    s.LandChannel.UpdateLandObject(p.LandData.LocalID, p.LandData);
                }
            }
            );
            responseData["success"] = true;
            m_log.Info("[RADMIN]: Reset Land Request complete");
        }

        private void XmlRpcRefreshSearch(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Received Refresh Search Request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            CheckRegionParams(requestData, responseData);

            Scene scene = null;
            GetSceneFromRegionParams(requestData, responseData, out scene);

            ISearchModule searchModule = scene.RequestModuleInterface<ISearchModule>();
            if (searchModule != null)
            {
                searchModule.Refresh();
                responseData["success"] = true;
            }
            else
            {
                responseData["success"] = false;
            }

            m_log.Info("[RADMIN]: Refresh Search Request complete");
        }

        private void XmlRpcRefreshMap(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Received Refresh Map Request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            CheckRegionParams(requestData, responseData);

            Scene scene = null;
            GetSceneFromRegionParams(requestData, responseData, out scene);

            IMapImageUploadModule mapTileModule = scene.RequestModuleInterface<IMapImageUploadModule>();
            if (mapTileModule != null)
            {
                Util.FireAndForget((x) =>
                {
                    mapTileModule.UploadMapTile(scene);
                });
                responseData["success"] = true;
            }
            else
            {
                responseData["success"] = false;
            }

            m_log.Info("[RADMIN]: Refresh Map Request complete");
        }

        private void XmlRpcGetOpenSimVersion(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Received Get OpenSim Version Request");

            Hashtable responseData = (Hashtable)response.Value;

            responseData["version"] = m_openSimVersion;
            responseData["success"] = true;

            m_log.Info("[RADMIN]: Get OpenSim Version Request complete");
        }

        private void XmlRpcGetAgentCount(XmlRpcRequest request, XmlRpcResponse response, IPEndPoint remoteClient)
        {
            m_log.Info("[RADMIN]: Received Get Agent Count Request");

            Hashtable responseData = (Hashtable)response.Value;
            Hashtable requestData = (Hashtable)request.Params[0];

            CheckRegionParams(requestData, responseData);

            Scene scene = null;
            GetSceneFromRegionParams(requestData, responseData, out scene);

            if (scene == null)
            {
                responseData["success"] = false;
            }
            else
            {
                responseData["count"] = scene.GetRootAgentCount();
                responseData["success"] = true;
            }

            m_log.Info("[RADMIN]: Get Agent Count Request complete");
        }

        /// <summary>
        /// Parse a float with the given parameter name from a request data hash table.
        /// </summary>
        /// <remarks>
        /// Will throw an exception if parameter is not a float.
        /// Will not throw if parameter is not found, passes back default value instead.
        /// </remarks>
        /// <param name="requestData"></param>
        /// <param name="paramName"></param>
        /// <param name="defaultVal"></param>
        /// <returns></returns>
        private static float ParseFloat(Hashtable requestData, string paramName, float defaultVal)
        {
            if (requestData.Contains(paramName))
            {
                string rawVal = (string)requestData[paramName];
                float val;

                if (!float.TryParse(rawVal, out val))
                    throw new Exception(string.Format("{0} {1} is not a valid float", paramName, rawVal));
                else
                    return val;
            }
            else
            {
                return defaultVal;
            }
        }

        private static void CheckStringParameters(Hashtable requestData, Hashtable responseData, string[] param)
        {
            foreach (string parameter in param)
            {
                if (!requestData.Contains(parameter))
                {
                    responseData["accepted"] = false;
                    throw new Exception(String.Format("missing string parameter {0}", parameter));
                }
                if (String.IsNullOrEmpty((string) requestData[parameter]))
                {
                    responseData["accepted"] = false;
                    throw new Exception(String.Format("parameter {0} is empty", parameter));
                }
            }
        }

        private static void CheckIntegerParams(Hashtable requestData, Hashtable responseData, string[] param)
        {
            foreach (string parameter in param)
            {
                if (!requestData.Contains(parameter))
                {
                    responseData["accepted"] = false;
                    throw new Exception(String.Format("missing integer parameter {0}", parameter));
                }
            }
        }

        private void CheckRegionParams(Hashtable requestData, Hashtable responseData)
        {
            //Checks if region parameters exist and gives exeption if no parameters are given
            if ((requestData.ContainsKey("region_id") && !String.IsNullOrEmpty((string)requestData["region_id"])) ||
                (requestData.ContainsKey("region_name") && !String.IsNullOrEmpty((string)requestData["region_name"])))
            {
                return;
            }
            else
            {
                responseData["accepted"] = false;
                throw new Exception("no region_name or region_id given");
            }
        }

        private void GetSceneFromRegionParams(Hashtable requestData, Hashtable responseData, out Scene scene)
        {
            scene = null;

            if (requestData.ContainsKey("region_id") &&
                !String.IsNullOrEmpty((string)requestData["region_id"]))
            {
                UUID regionID = (UUID)(string)requestData["region_id"];
                if (!m_application.SceneManager.TryGetScene(regionID, out scene))
                {
                    responseData["error"] = String.Format("Region ID {0} not found", regionID);
                    throw new Exception(String.Format("Region ID {0} not found", regionID));
                }
            }
            else if (requestData.ContainsKey("region_name") &&
                !String.IsNullOrEmpty((string)requestData["region_name"]))
            {
                string regionName = (string)requestData["region_name"];
                if (!m_application.SceneManager.TryGetScene(regionName, out scene))
                {
                    responseData["error"] = String.Format("Region {0} not found", regionName);
                    throw new Exception(String.Format("Region {0} not found", regionName));
                }
            }
            else
            {
                responseData["error"] = "no region_name or region_id given";
                throw new Exception("no region_name or region_id given");
            }
            return;
        }

        private bool GetBoolean(Hashtable requestData, string tag, bool defaultValue)
        {
            // If an access value has been provided, apply it.
            if (requestData.Contains(tag))
            {
                switch (((string)requestData[tag]).ToLower())
                {
                    case "true" :
                    case "t" :
                    case "1" :
                        return true;
                    case "false" :
                    case "f" :
                    case "0" :
                        return false;
                    default :
                        return defaultValue;
                }
            }
            else
                return defaultValue;
        }

        private int GetIntegerAttribute(XmlNode node, string attribute, int defaultValue)
        {
            try { return Convert.ToInt32(node.Attributes[attribute].Value); } catch{}
            return defaultValue;
        }

        private uint GetUnsignedAttribute(XmlNode node, string attribute, uint defaultValue)
        {
            try { return Convert.ToUInt32(node.Attributes[attribute].Value); } catch{}
            return defaultValue;
        }

        private string GetStringAttribute(XmlNode node, string attribute, string defaultValue)
        {
            try { return node.Attributes[attribute].Value; } catch{}
            return defaultValue;
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// Create a user
        /// </summary>
        /// <param name="scopeID"></param>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="password"></param>
        /// <param name="email"></param>
        private UserAccount CreateUser(UUID scopeID, string firstName, string lastName, string password, string email)
        {
            Scene scene = m_application.SceneManager.CurrentOrFirstScene;
            IUserAccountService userAccountService = scene.UserAccountService;
            IGridService gridService = scene.GridService;
            IAuthenticationService authenticationService = scene.AuthenticationService;
            IGridUserService gridUserService = scene.GridUserService;
            IInventoryService inventoryService = scene.InventoryService;

            UserAccount account = userAccountService.GetUserAccount(scopeID, firstName, lastName);
            if (null == account)
            {
                account = new UserAccount(scopeID, UUID.Random(), firstName, lastName, email);
                if (account.ServiceURLs == null || (account.ServiceURLs != null && account.ServiceURLs.Count == 0))
                {
                    account.ServiceURLs = new Dictionary<string, object>();
                    account.ServiceURLs["HomeURI"] = string.Empty;
                    account.ServiceURLs["InventoryServerURI"] = string.Empty;
                    account.ServiceURLs["AssetServerURI"] = string.Empty;
                }

                if (userAccountService.StoreUserAccount(account))
                {
                    bool success;
                    if (authenticationService != null)
                    {
                        success = authenticationService.SetPassword(account.PrincipalID, password);
                        if (!success)
                            m_log.WarnFormat("[RADMIN]: Unable to set password for account {0} {1}.",
                                firstName, lastName);
                    }

                    GridRegion home = null;
                    if (gridService != null)
                    {
                        List<GridRegion> defaultRegions = gridService.GetDefaultRegions(UUID.Zero);
                        if (defaultRegions != null && defaultRegions.Count >= 1)
                            home = defaultRegions[0];

                        if (gridUserService != null && home != null)
                            gridUserService.SetHome(account.PrincipalID.ToString(), home.RegionID, new Vector3(128, 128, 0), new Vector3(0, 1, 0));
                        else
                            m_log.WarnFormat("[RADMIN]: Unable to set home for account {0} {1}.",
                               firstName, lastName);
                    }
                    else
                        m_log.WarnFormat("[RADMIN]: Unable to retrieve home region for account {0} {1}.",
                           firstName, lastName);

                    if (inventoryService != null)
                    {
                        success = inventoryService.CreateUserInventory(account.PrincipalID);
                        if (!success)
                            m_log.WarnFormat("[RADMIN]: Unable to create inventory for account {0} {1}.",
                                firstName, lastName);
                    }

                    m_log.InfoFormat("[RADMIN]: Account {0} {1} created successfully", firstName, lastName);
                    return account;
                 } else {
                    m_log.ErrorFormat("[RADMIN]: Account creation failed for account {0} {1}", firstName, lastName);
                }
            }
            else
            {
                m_log.ErrorFormat("[RADMIN]: A user with the name {0} {1} already exists!", firstName, lastName);
            }
            return null;
        }

        /// <summary>
        /// Change password
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="password"></param>
        private bool ChangeUserPassword(string firstName, string lastName, string password)
        {
            Scene scene = m_application.SceneManager.CurrentOrFirstScene;
            IUserAccountService userAccountService = scene.UserAccountService;
            IAuthenticationService authenticationService = scene.AuthenticationService;

            UserAccount account = userAccountService.GetUserAccount(UUID.Zero, firstName, lastName);
            if (null != account)
            {
                bool success = false;
                if (authenticationService != null)
                    success = authenticationService.SetPassword(account.PrincipalID, password);

                if (!success)
                {
                    m_log.WarnFormat("[RADMIN]: Unable to set password for account {0} {1}.",
                       firstName, lastName);
                    return false;
                }
                return true;
            }
            else
            {
                m_log.ErrorFormat("[RADMIN]: No such user");
                return false;
            }
        }

        private bool LoadHeightmap(string file, UUID regionID)
        {
            m_log.InfoFormat("[RADMIN]: Terrain Loading: {0}", file);

            Scene region = null;

            if (!m_application.SceneManager.TryGetScene(regionID, out region))
            {
                m_log.InfoFormat("[RADMIN]: unable to get a scene with that name: {0}", regionID.ToString());
                return false;
            }

            ITerrainModule terrainModule = region.RequestModuleInterface<ITerrainModule>();
            if (null == terrainModule) throw new Exception("terrain module not available");
            if (Uri.IsWellFormedUriString(file, UriKind.Absolute))
            {
                m_log.Info("[RADMIN]: Terrain path is URL");
                Uri result;
                if (Uri.TryCreate(file, UriKind.RelativeOrAbsolute, out result))
                {
                    // the url is valid
                    string fileType = file.Substring(file.LastIndexOf('/') + 1);
                    terrainModule.LoadFromStream(fileType, result);
                }
            }
            else
            {
                terrainModule.LoadFromFile(file);
            }

            m_log.Info("[RADMIN]: Load height maps request complete");

            return true;
        }


        /// <summary>
        /// This method is called by the user-create and user-modify methods to establish
        /// or change, the user's appearance. Default avatar names can be specified via
        /// the config file, but must correspond to avatars in the default appearance
        /// file, or pre-existing in the user database.
        /// This should probably get moved into somewhere more core eventually.
        /// </summary>
        private void UpdateUserAppearance(Hashtable responseData, Hashtable requestData, UUID userid)
        {
            m_log.DebugFormat("[RADMIN]: updateUserAppearance");

            string defaultMale   = m_config.GetString("default_male", "Default Male");
            string defaultFemale = m_config.GetString("default_female", "Default Female");
            string defaultNeutral   = m_config.GetString("default_female", "Default Default");
            string model   = String.Empty;

            // Has a gender preference been supplied?

            if (requestData.Contains("gender"))
            {
                switch ((string)requestData["gender"])
                {
                    case "m" :
                    case "male" :
                        model = defaultMale;
                        break;
                    case "f" :
                    case "female" :
                        model = defaultFemale;
                        break;
                    case "n" :
                    case "neutral" :
                    default :
                        model = defaultNeutral;
                        break;
                }
            }

            // Has an explicit model been specified?

            if (requestData.Contains("model") && (String.IsNullOrEmpty((string)requestData["gender"])))
            {
                model = (string)requestData["model"];
            }

            // No appearance attributes were set

            if (String.IsNullOrEmpty(model))
            {
                m_log.DebugFormat("[RADMIN]: Appearance update not requested");
                return;
            }

            m_log.DebugFormat("[RADMIN]: Setting appearance for avatar {0}, using model <{1}>", userid, model);

            string[] modelSpecifiers = model.Split();
            if (modelSpecifiers.Length != 2)
            {
                m_log.WarnFormat("[RADMIN]: User appearance not set for {0}. Invalid model name : <{1}>", userid, model);
                // modelSpecifiers = dmodel.Split();
                return;
            }

            Scene scene = m_application.SceneManager.CurrentOrFirstScene;
            UUID scopeID = scene.RegionInfo.ScopeID;
            UserAccount modelProfile = scene.UserAccountService.GetUserAccount(scopeID, modelSpecifiers[0], modelSpecifiers[1]);

            if (modelProfile == null)
            {
                m_log.WarnFormat("[RADMIN]: Requested model ({0}) not found. Appearance unchanged", model);
                return;
            }

            // Set current user's appearance. This bit is easy. The appearance structure is populated with
            // actual asset ids, however to complete the magic we need to populate the inventory with the
            // assets in question.

            EstablishAppearance(userid, modelProfile.PrincipalID);

            m_log.DebugFormat("[RADMIN]: Finished setting appearance for avatar {0}, using model {1}",
                              userid, model);
        }

        /// <summary>
        /// This method is called by updateAvatarAppearance once any specified model has been
        /// ratified, or an appropriate default value has been adopted. The intended prototype
        /// is known to exist, as is the target avatar.
        /// </summary>
        private void EstablishAppearance(UUID destination, UUID source)
        {
            m_log.DebugFormat("[RADMIN]: Initializing inventory for {0} from {1}", destination, source);
            Scene scene = m_application.SceneManager.CurrentOrFirstScene;

            // If the model has no associated appearance we're done.
            AvatarAppearance avatarAppearance = scene.AvatarService.GetAppearance(source);
            if (avatarAppearance == null)
                return;

            // Simple appearance copy or copy Clothing and Bodyparts folders?
            bool copyFolders = m_config.GetBoolean("copy_folders", false);

            if (!copyFolders)
            {
                // Simple copy of wearables and appearance update
                try
                {
                    CopyWearablesAndAttachments(destination, source, avatarAppearance);

                    scene.AvatarService.SetAppearance(destination, avatarAppearance);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("[RADMIN]: Error transferring appearance for {0} : {1}",
                                      destination, e.Message);
                }

                return;
            }

            // Copy Clothing and Bodypart folders and appearance update
            try
            {
                Dictionary<UUID,UUID> inventoryMap = new Dictionary<UUID,UUID>();
                CopyInventoryFolders(destination, source, FolderType.Clothing, inventoryMap, avatarAppearance);
                CopyInventoryFolders(destination, source, FolderType.BodyPart, inventoryMap, avatarAppearance);

                AvatarWearable[] wearables = avatarAppearance.Wearables;

                for (int i=0; i<wearables.Length; i++)
                {
                    if (inventoryMap.ContainsKey(wearables[i][0].ItemID))
                    {
                        AvatarWearable wearable = new AvatarWearable();
                        wearable.Wear(inventoryMap[wearables[i][0].ItemID],
                                wearables[i][0].AssetID);
                        avatarAppearance.SetWearable(i, wearable);
                    }
                }

                scene.AvatarService.SetAppearance(destination, avatarAppearance);
            }
            catch (Exception e)
            {
               m_log.WarnFormat("[RADMIN]: Error transferring appearance for {0} : {1}",
                                  destination, e.Message);
            }

            return;
        }

        /// <summary>
        /// This method is called by establishAppearance to do a copy all inventory items
        /// worn or attached to the Clothing inventory folder of the receiving avatar.
        /// In parallel the avatar wearables and attachments are updated.
        /// </summary>
        private void CopyWearablesAndAttachments(UUID destination, UUID source, AvatarAppearance avatarAppearance)
        {
            IInventoryService inventoryService = m_application.SceneManager.CurrentOrFirstScene.InventoryService;

            // Get Clothing folder of receiver
            InventoryFolderBase destinationFolder = inventoryService.GetFolderForType(destination, FolderType.Clothing);

            if (destinationFolder == null)
                throw new Exception("Cannot locate folder(s)");

            // Missing destination folder? This should *never* be the case
            if (destinationFolder.Type != (short)FolderType.Clothing)
            {
                destinationFolder = new InventoryFolderBase();
                
                destinationFolder.ID       = UUID.Random();
                destinationFolder.Name     = "Clothing";
                destinationFolder.Owner    = destination;
                destinationFolder.Type = (short)FolderType.Clothing;
                destinationFolder.ParentID = inventoryService.GetRootFolder(destination).ID;
                destinationFolder.Version  = 1;
                inventoryService.AddFolder(destinationFolder);     // store base record
                m_log.ErrorFormat("[RADMIN]: Created folder for destination {0}", source);
            }

            // Wearables
            AvatarWearable[] wearables = avatarAppearance.Wearables;
            AvatarWearable wearable;

            for (int i = 0; i<wearables.Length; i++)
            {
                wearable = wearables[i];
                if (wearable[0].ItemID != UUID.Zero)
                {
                    // Get inventory item and copy it
                    InventoryItemBase item = new InventoryItemBase(wearable[0].ItemID, source);
                    item = inventoryService.GetItem(item);

                    if (item != null)
                    {
                        InventoryItemBase destinationItem = new InventoryItemBase(UUID.Random(), destination);
                        destinationItem.Name = item.Name;
                        destinationItem.Owner = destination;
                        destinationItem.Description = item.Description;
                        destinationItem.InvType = item.InvType;
                        destinationItem.CreatorId = item.CreatorId;
                        destinationItem.CreatorData = item.CreatorData;
                        destinationItem.NextPermissions = item.NextPermissions;
                        destinationItem.CurrentPermissions = item.CurrentPermissions;
                        destinationItem.BasePermissions = item.BasePermissions;
                        destinationItem.EveryOnePermissions = item.EveryOnePermissions;
                        destinationItem.GroupPermissions = item.GroupPermissions;
                        destinationItem.AssetType = item.AssetType;
                        destinationItem.AssetID = item.AssetID;
                        destinationItem.GroupID = item.GroupID;
                        destinationItem.GroupOwned = item.GroupOwned;
                        destinationItem.SalePrice = item.SalePrice;
                        destinationItem.SaleType = item.SaleType;
                        destinationItem.Flags = item.Flags;
                        destinationItem.CreationDate = item.CreationDate;
                        destinationItem.Folder = destinationFolder.ID;
                        ApplyNextOwnerPermissions(destinationItem);

                        m_application.SceneManager.CurrentOrFirstScene.AddInventoryItem(destinationItem);
                        m_log.DebugFormat("[RADMIN]: Added item {0} to folder {1}", destinationItem.ID, destinationFolder.ID);

                        // Wear item
                        AvatarWearable newWearable = new AvatarWearable();
                        newWearable.Wear(destinationItem.ID, wearable[0].AssetID);
                        avatarAppearance.SetWearable(i, newWearable);
                    }
                    else
                    {
                        m_log.WarnFormat("[RADMIN]: Error transferring {0} to folder {1}", wearable[0].ItemID, destinationFolder.ID);
                    }
                }
            }

            // Attachments
            List<AvatarAttachment> attachments = avatarAppearance.GetAttachments();

            foreach (AvatarAttachment attachment in attachments)
            {
                int attachpoint = attachment.AttachPoint;
                UUID itemID = attachment.ItemID;

                if (itemID != UUID.Zero)
                {
                    // Get inventory item and copy it
                    InventoryItemBase item = new InventoryItemBase(itemID, source);
                    item = inventoryService.GetItem(item);

                    if (item != null)
                    {
                        InventoryItemBase destinationItem = new InventoryItemBase(UUID.Random(), destination);
                        destinationItem.Name = item.Name;
                        destinationItem.Owner = destination;
                        destinationItem.Description = item.Description;
                        destinationItem.InvType = item.InvType;
                        destinationItem.CreatorId = item.CreatorId;
                        destinationItem.CreatorData = item.CreatorData;
                        destinationItem.NextPermissions = item.NextPermissions;
                        destinationItem.CurrentPermissions = item.CurrentPermissions;
                        destinationItem.BasePermissions = item.BasePermissions;
                        destinationItem.EveryOnePermissions = item.EveryOnePermissions;
                        destinationItem.GroupPermissions = item.GroupPermissions;
                        destinationItem.AssetType = item.AssetType;
                        destinationItem.AssetID = item.AssetID;
                        destinationItem.GroupID = item.GroupID;
                        destinationItem.GroupOwned = item.GroupOwned;
                        destinationItem.SalePrice = item.SalePrice;
                        destinationItem.SaleType = item.SaleType;
                        destinationItem.Flags = item.Flags;
                        destinationItem.CreationDate = item.CreationDate;
                        destinationItem.Folder = destinationFolder.ID;
                        ApplyNextOwnerPermissions(destinationItem);

                        m_application.SceneManager.CurrentOrFirstScene.AddInventoryItem(destinationItem);
                        m_log.DebugFormat("[RADMIN]: Added item {0} to folder {1}", destinationItem.ID, destinationFolder.ID);

                        // Attach item
                        avatarAppearance.SetAttachment(attachpoint, destinationItem.ID, destinationItem.AssetID);
                        m_log.DebugFormat("[RADMIN]: Attached {0}", destinationItem.ID);
                    }
                    else
                    {
                        m_log.WarnFormat("[RADMIN]: Error transferring {0} to folder {1}", itemID, destinationFolder.ID);
                    }
                }
            }
        }

        /// <summary>
        /// This method is called by establishAppearance to copy inventory folders to make
        /// copies of Clothing and Bodyparts inventory folders and attaches worn attachments
        /// </summary>
        private void CopyInventoryFolders(UUID destination, UUID source, FolderType assetType, Dictionary<UUID, UUID> inventoryMap,
                                          AvatarAppearance avatarAppearance)
        {
            IInventoryService inventoryService = m_application.SceneManager.CurrentOrFirstScene.InventoryService;

            InventoryFolderBase sourceFolder = inventoryService.GetFolderForType(source, assetType);
            InventoryFolderBase destinationFolder = inventoryService.GetFolderForType(destination, assetType);

            if (sourceFolder == null || destinationFolder == null)
                throw new Exception("Cannot locate folder(s)");

            // Missing source folder? This should *never* be the case
            if (sourceFolder.Type != (short)assetType)
            {
                sourceFolder = new InventoryFolderBase();
                sourceFolder.ID       = UUID.Random();
                if (assetType == FolderType.Clothing) 
                {
                    sourceFolder.Name     = "Clothing";
                } 
                else 
                {
                    sourceFolder.Name     = "Body Parts";
                }
                sourceFolder.Owner    = source;
                sourceFolder.Type     = (short)assetType;
                sourceFolder.ParentID = inventoryService.GetRootFolder(source).ID;
                sourceFolder.Version  = 1;
                inventoryService.AddFolder(sourceFolder);     // store base record
                m_log.ErrorFormat("[RADMIN] Created folder for source {0}", source);
            }

            // Missing destination folder? This should *never* be the case
            if (destinationFolder.Type != (short)assetType)
            {
                destinationFolder = new InventoryFolderBase();
                destinationFolder.ID       = UUID.Random();
                if (assetType == FolderType.Clothing)
                {
                    destinationFolder.Name  = "Clothing";
                }
                else
                {
                    destinationFolder.Name  = "Body Parts";
                }
                destinationFolder.Owner    = destination;
                destinationFolder.Type     = (short)assetType;
                destinationFolder.ParentID = inventoryService.GetRootFolder(destination).ID;
                destinationFolder.Version  = 1;
                inventoryService.AddFolder(destinationFolder);     // store base record
                m_log.ErrorFormat("[RADMIN]: Created folder for destination {0}", source);
            }

            InventoryFolderBase extraFolder;
            List<InventoryFolderBase> folders = inventoryService.GetFolderContent(source, sourceFolder.ID).Folders;

            foreach (InventoryFolderBase folder in folders)
            {
                extraFolder = new InventoryFolderBase();
                extraFolder.ID = UUID.Random();
                extraFolder.Name = folder.Name;
                extraFolder.Owner = destination;
                extraFolder.Type = folder.Type;
                extraFolder.Version = folder.Version;
                extraFolder.ParentID = destinationFolder.ID;
                inventoryService.AddFolder(extraFolder);

                m_log.DebugFormat("[RADMIN]: Added folder {0} to folder {1}", extraFolder.ID, sourceFolder.ID);

                List<InventoryItemBase> items = inventoryService.GetFolderContent(source, folder.ID).Items;

                foreach (InventoryItemBase item in items)
                {
                    InventoryItemBase destinationItem = new InventoryItemBase(UUID.Random(), destination);
                    destinationItem.Name = item.Name;
                    destinationItem.Owner = destination;
                    destinationItem.Description = item.Description;
                    destinationItem.InvType = item.InvType;
                    destinationItem.CreatorId = item.CreatorId;
                    destinationItem.CreatorData = item.CreatorData;
                    destinationItem.NextPermissions = item.NextPermissions;
                    destinationItem.CurrentPermissions = item.CurrentPermissions;
                    destinationItem.BasePermissions = item.BasePermissions;
                    destinationItem.EveryOnePermissions = item.EveryOnePermissions;
                    destinationItem.GroupPermissions = item.GroupPermissions;
                    destinationItem.AssetType = item.AssetType;
                    destinationItem.AssetID = item.AssetID;
                    destinationItem.GroupID = item.GroupID;
                    destinationItem.GroupOwned = item.GroupOwned;
                    destinationItem.SalePrice = item.SalePrice;
                    destinationItem.SaleType = item.SaleType;
                    destinationItem.Flags = item.Flags;
                    destinationItem.CreationDate = item.CreationDate;
                    destinationItem.Folder = extraFolder.ID;
                    ApplyNextOwnerPermissions(destinationItem);

                    m_application.SceneManager.CurrentOrFirstScene.AddInventoryItem(destinationItem);
                    inventoryMap.Add(item.ID, destinationItem.ID);
                    m_log.DebugFormat("[RADMIN]: Added item {0} to folder {1}", destinationItem.ID, extraFolder.ID);

                    // Attach item, if original is attached
                    int attachpoint = avatarAppearance.GetAttachpoint(item.ID);
                    if (attachpoint != 0)
                    {
                        avatarAppearance.SetAttachment(attachpoint, destinationItem.ID, destinationItem.AssetID);
                        m_log.DebugFormat("[RADMIN]: Attached {0}", destinationItem.ID);
                    }
                }
            }
        }

        /// <summary>
        /// Apply next owner permissions.
        /// </summary>
        private void ApplyNextOwnerPermissions(InventoryItemBase item)
        {
            if (item.InvType == (int)InventoryType.Object && (item.CurrentPermissions & 7) != 0)
            {
                uint perms = item.CurrentPermissions;
                PermissionsUtil.ApplyFoldedPermissions(item.CurrentPermissions, ref perms);
                item.CurrentPermissions = perms;
            }

            item.CurrentPermissions &= item.NextPermissions;
            item.BasePermissions &= item.NextPermissions;
            item.EveryOnePermissions &= item.NextPermissions;
            // item.OwnerChanged = true;
            // item.PermsMask = 0;
            // item.PermsGranter = UUID.Zero;
        }

        /// <summary>
        /// This method is called if a given model avatar name can not be found. If the external
        /// file has already been loaded once, then control returns immediately. If not, then it
        /// looks for a default appearance file. This file contains XML definitions of zero or more named
        /// avatars, each avatar can specify zero or more "outfits". Each outfit is a collection
        /// of items that together, define a particular ensemble for the avatar. Each avatar should
        /// indicate which outfit is the default, and this outfit will be automatically worn. The
        /// other outfits are provided to allow "real" avatars a way to easily change their outfits.
        /// </summary>
        private bool CreateDefaultAvatars()
        {
            // Only load once
            if (m_defaultAvatarsLoaded)
            {
                return false;
            }

            m_log.DebugFormat("[RADMIN]: Creating default avatar entries");

            m_defaultAvatarsLoaded = true;

            // Load processing starts here...

            try
            {
                string defaultAppearanceFileName = null;

                //m_config may be null if RemoteAdmin configuration secition is missing or disabled in OpenSim.ini
                if (m_config != null)
                {
                    defaultAppearanceFileName = m_config.GetString("default_appearance", "default_appearance.xml");
                }

                if (File.Exists(defaultAppearanceFileName))
                {
                    XmlDocument doc = new XmlDocument();
                    string name     = "*unknown*";
                    string email    = "anon@anon";
                    uint   regionXLocation     = 1000;
                    uint   regionYLocation     = 1000;
                    string password   = UUID.Random().ToString(); // No requirement to sign-in.
                    UUID ID = UUID.Zero;
                    AvatarAppearance avatarAppearance;
                    XmlNodeList avatars;
                    XmlNodeList assets;
                    XmlNode perms = null;
                    bool include = false;
                    bool select  = false;

                    Scene scene = m_application.SceneManager.CurrentOrFirstScene;
                    IInventoryService inventoryService = scene.InventoryService;
                    IAssetService assetService = scene.AssetService;

                    doc.LoadXml(File.ReadAllText(defaultAppearanceFileName));

                    // Load up any included assets. Duplicates will be ignored
                    assets = doc.GetElementsByTagName("RequiredAsset");
                    foreach (XmlNode assetNode in assets)
                    {
                        AssetBase asset = new AssetBase(UUID.Random(), GetStringAttribute(assetNode, "name", ""), SByte.Parse(GetStringAttribute(assetNode, "type", "")), UUID.Zero.ToString());
                        asset.Description = GetStringAttribute(assetNode,"desc","");
                        asset.Local       = Boolean.Parse(GetStringAttribute(assetNode,"local",""));
                        asset.Temporary   = Boolean.Parse(GetStringAttribute(assetNode,"temporary",""));
                        asset.Data        = Convert.FromBase64String(assetNode.InnerText);
                        assetService.Store(asset);
                    }

                    avatars = doc.GetElementsByTagName("Avatar");

                    // The document may contain multiple avatars

                    foreach (XmlElement avatar in avatars)
                    {
                        m_log.DebugFormat("[RADMIN]: Loading appearance for {0}, gender = {1}",
                            GetStringAttribute(avatar,"name","?"), GetStringAttribute(avatar,"gender","?"));

                        // Create the user identified by the avatar entry

                        try
                        {
                            // Only the name value is mandatory
                            name   = GetStringAttribute(avatar,"name",name);
                            email  = GetStringAttribute(avatar,"email",email);
                            regionXLocation   = GetUnsignedAttribute(avatar,"regx",regionXLocation);
                            regionYLocation   = GetUnsignedAttribute(avatar,"regy",regionYLocation);
                            password = GetStringAttribute(avatar,"password",password);

                            string[] names = name.Split();
                            UUID scopeID = scene.RegionInfo.ScopeID;
                            UserAccount account = scene.UserAccountService.GetUserAccount(scopeID, names[0], names[1]);
                            if (null == account)
                            {
                                account = CreateUser(scopeID, names[0], names[1], password, email);
                                if (null == account)
                                {
                                    m_log.ErrorFormat("[RADMIN]: Avatar {0} {1} was not created", names[0], names[1]);
                                    return false;
                                }
                            }

                            // Set home position

                            GridRegion home = scene.GridService.GetRegionByPosition(scopeID, 
                                        (int)Util.RegionToWorldLoc(regionXLocation), (int)Util.RegionToWorldLoc(regionYLocation));
                            if (null == home) {
                                m_log.WarnFormat("[RADMIN]: Unable to set home region for newly created user account {0} {1}", names[0], names[1]);
                            } else {
                                scene.GridUserService.SetHome(account.PrincipalID.ToString(), home.RegionID, new Vector3(128, 128, 0), new Vector3(0, 1, 0));
                                m_log.DebugFormat("[RADMIN]: Set home region {0} for updated user account {1} {2}", home.RegionID, names[0], names[1]);
                            }

                            ID = account.PrincipalID;

                            m_log.DebugFormat("[RADMIN]: User {0}[{1}] created or retrieved", name, ID);
                            include = true;
                        }
                        catch (Exception e)
                        {
                            m_log.DebugFormat("[RADMIN]: Error creating user {0} : {1}", name, e.Message);
                            include = false;
                        }

                        // OK, User has been created OK, now we can install the inventory.
                        // First retrieve the current inventory (the user may already exist)
                        // Note that althought he inventory is retrieved, the hierarchy has
                        // not been interpreted at all.

                        if (include)
                        {
                            // Setup for appearance processing
                            avatarAppearance = scene.AvatarService.GetAppearance(ID);
                            if (avatarAppearance == null)
                                avatarAppearance = new AvatarAppearance();

                            AvatarWearable[] wearables = avatarAppearance.Wearables;
                            for (int i=0; i<wearables.Length; i++)
                            {
                                wearables[i] = new AvatarWearable();
                            }

                            try
                            {
                                // m_log.DebugFormat("[RADMIN] {0} folders, {1} items in inventory",
                                //   uic.folders.Count, uic.items.Count);

                                InventoryFolderBase clothingFolder = inventoryService.GetFolderForType(ID, FolderType.Clothing);

                                // This should *never* be the case
                                if (clothingFolder == null || clothingFolder.Type != (short)FolderType.Clothing)
                                {
                                    clothingFolder = new InventoryFolderBase();
                                    clothingFolder.ID       = UUID.Random();
                                    clothingFolder.Name     = "Clothing";
                                    clothingFolder.Owner    = ID;
                                    clothingFolder.Type     = (short)FolderType.Clothing;
                                    clothingFolder.ParentID = inventoryService.GetRootFolder(ID).ID;
                                    clothingFolder.Version  = 1;
                                    inventoryService.AddFolder(clothingFolder);     // store base record
                                    m_log.ErrorFormat("[RADMIN]: Created clothing folder for {0}/{1}", name, ID);
                                }

                                // OK, now we have an inventory for the user, read in the outfits from the
                                // default appearance XMl file.

                                XmlNodeList outfits = avatar.GetElementsByTagName("Ensemble");
                                InventoryFolderBase extraFolder;
                                string outfitName;
                                UUID assetid;

                                foreach (XmlElement outfit in outfits)
                                {
                                    m_log.DebugFormat("[RADMIN]: Loading outfit {0} for {1}",
                                        GetStringAttribute(outfit,"name","?"), GetStringAttribute(avatar,"name","?"));

                                    outfitName   = GetStringAttribute(outfit,"name","");
                                    select  = (GetStringAttribute(outfit,"default","no") == "yes");

                                    // If the folder already exists, re-use it. The defaults may
                                    // change over time. Augment only.

                                    List<InventoryFolderBase> folders = inventoryService.GetFolderContent(ID, clothingFolder.ID).Folders;
                                    extraFolder = null;

                                    foreach (InventoryFolderBase folder in folders)
                                    {
                                    if (folder.Name == outfitName)
                                        {
                                            extraFolder = folder;
                                            break;
                                        }
                                    }

                                    // Otherwise, we must create the folder.
                                    if (extraFolder == null)
                                    {
                                        m_log.DebugFormat("[RADMIN]: Creating outfit folder {0} for {1}", outfitName, name);
                                        extraFolder          = new InventoryFolderBase();
                                        extraFolder.ID       = UUID.Random();
                                        extraFolder.Name     = outfitName;
                                        extraFolder.Owner    = ID;
                                        extraFolder.Type     = (short)FolderType.Clothing;
                                        extraFolder.Version  = 1;
                                        extraFolder.ParentID = clothingFolder.ID;
                                        inventoryService.AddFolder(extraFolder);
                                        m_log.DebugFormat("[RADMIN]: Adding outfile folder {0} to folder {1}", extraFolder.ID, clothingFolder.ID);
                                    }

                                    // Now get the pieces that make up the outfit
                                    XmlNodeList items = outfit.GetElementsByTagName("Item");

                                    foreach (XmlElement item in items)
                                    {
                                        assetid = UUID.Zero;
                                        XmlNodeList children = item.ChildNodes;
                                        foreach (XmlNode child in children)
                                        {
                                            switch (child.Name)
                                            {
                                                case "Permissions" :
                                                    m_log.DebugFormat("[RADMIN]: Permissions specified");
                                                    perms = child;
                                                    break;
                                                case "Asset" :
                                                    assetid = new UUID(child.InnerText);
                                                    break;
                                            }
                                        }

                                        InventoryItemBase inventoryItem = null;

                                        // Check if asset is in inventory already
                                        inventoryItem = null;
                                        List<InventoryItemBase> inventoryItems = inventoryService.GetFolderContent(ID, extraFolder.ID).Items;

                                        foreach (InventoryItemBase listItem in inventoryItems)
                                        {
                                            if (listItem.AssetID == assetid)
                                            {
                                                inventoryItem = listItem;
                                                break;
                                            }
                                        }

                                        // Create inventory item
                                        if (inventoryItem == null)
                                        {
                                            inventoryItem = new InventoryItemBase(UUID.Random(), ID);
                                            inventoryItem.Name = GetStringAttribute(item,"name","");
                                            inventoryItem.Description = GetStringAttribute(item,"desc","");
                                            inventoryItem.InvType = GetIntegerAttribute(item,"invtype",-1);
                                            inventoryItem.CreatorId = GetStringAttribute(item,"creatorid","");
                                            inventoryItem.CreatorData = GetStringAttribute(item, "creatordata", "");
                                            inventoryItem.NextPermissions = GetUnsignedAttribute(perms, "next", 0x7fffffff);
                                            inventoryItem.CurrentPermissions = GetUnsignedAttribute(perms,"current",0x7fffffff);
                                            inventoryItem.BasePermissions = GetUnsignedAttribute(perms,"base",0x7fffffff);
                                            inventoryItem.EveryOnePermissions = GetUnsignedAttribute(perms,"everyone",0x7fffffff);
                                            inventoryItem.GroupPermissions = GetUnsignedAttribute(perms,"group",0x7fffffff);
                                            inventoryItem.AssetType = GetIntegerAttribute(item,"assettype",-1);
                                            inventoryItem.AssetID = assetid; // associated asset
                                            inventoryItem.GroupID = (UUID)GetStringAttribute(item,"groupid","");
                                            inventoryItem.GroupOwned = (GetStringAttribute(item,"groupowned","false") == "true");
                                            inventoryItem.SalePrice = GetIntegerAttribute(item,"saleprice",0);
                                            inventoryItem.SaleType = (byte)GetIntegerAttribute(item,"saletype",0);
                                            inventoryItem.Flags = GetUnsignedAttribute(item,"flags",0);
                                            inventoryItem.CreationDate = GetIntegerAttribute(item,"creationdate",Util.UnixTimeSinceEpoch());
                                            inventoryItem.Folder = extraFolder.ID; // Parent folder

                                            m_application.SceneManager.CurrentOrFirstScene.AddInventoryItem(inventoryItem);
                                            m_log.DebugFormat("[RADMIN]: Added item {0} to folder {1}", inventoryItem.ID, extraFolder.ID);
                                        }

                                        // Attach item, if attachpoint is specified
                                        int attachpoint = GetIntegerAttribute(item,"attachpoint",0);
                                        if (attachpoint != 0)
                                        {
                                            avatarAppearance.SetAttachment(attachpoint, inventoryItem.ID, inventoryItem.AssetID);
                                            m_log.DebugFormat("[RADMIN]: Attached {0}", inventoryItem.ID);
                                        }

                                        // Record whether or not the item is to be initially worn
                                        try
                                        {
                                        if (select && (GetStringAttribute(item, "wear", "false") == "true"))
                                            {
                                                avatarAppearance.Wearables[inventoryItem.Flags].Wear(inventoryItem.ID, inventoryItem.AssetID);
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            m_log.WarnFormat("[RADMIN]: Error wearing item {0} : {1}", inventoryItem.ID, e.Message);
                                        }
                                    } // foreach item in outfit
                                    m_log.DebugFormat("[RADMIN]: Outfit {0} load completed", outfitName);
                                } // foreach outfit
                                m_log.DebugFormat("[RADMIN]: Inventory update complete for {0}", name);
                                scene.AvatarService.SetAppearance(ID, avatarAppearance);
                            }
                            catch (Exception e)
                            {
                                m_log.WarnFormat("[RADMIN]: Inventory processing incomplete for user {0} : {1}",
                                    name, e.Message);
                            }
                        } // End of include
                    }
                    m_log.DebugFormat("[RADMIN]: Default avatar loading complete");
                }
                else
                {
                    m_log.DebugFormat("[RADMIN]: No default avatar information available");
                    return false;
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[RADMIN]: Exception whilst loading default avatars ; {0}", e.Message);
                return false;
            }

            return true;
        }
    }
}
