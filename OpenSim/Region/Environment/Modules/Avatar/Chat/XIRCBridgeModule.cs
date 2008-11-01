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
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Avatar.Chat
{

    public class XIRCBridgeModule : IRegionModule
    {

        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        internal static bool   configured = false;
        internal static bool   enabled    = false;
        internal static IConfig m_config  = null;

        internal static List<RegionState>  m_regions  = new List<RegionState>();
        internal static List<ChannelState> m_channels = new List<ChannelState>();

        internal static string password = String.Empty;

        #region IRegionModule Members

        public string Name
        {
            get { return "XIRCBridgeModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        public void Initialise(Scene scene, IConfigSource config)
        {

            // Do a once-only scan of the configuration file to make
            // sure it's basically intact.

            if (!configured)
            {

                configured = true;

                try
                {
                    if ((m_config = config.Configs["XIRC"]) == null)
                    {
                        m_log.InfoFormat("[XIRC-Bridge] module not configured");
                        return;
                    }

                    if (!m_config.GetBoolean("enabled", false))
                    {
                        m_log.InfoFormat("[XIRC-Bridge] module disabled in configuration");
                        return;
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[XIRC-Bridge] configuration failed : {0}", e.Message);
                    return;
                }

                enabled = true;

                if (config.Configs["RemoteAdmin"] != null)
                {
                    password = config.Configs["RemoteAdmin"].GetString("access_password", password);
                    scene.CommsManager.HttpServer.AddXmlRPCHandler("xirc_admin", XmlRpcAdminMethod, false);
                }

            }

            // Iff the IRC bridge is enabled, then each new region may be 
            // connected to IRC. But it should NOT be obligatory (and it 
            // is not).
             
            if (enabled)
            {
                try
                {
                    m_log.InfoFormat("[XIRC-Bridge] Connecting region {0}", scene.RegionInfo.RegionName);
                    m_regions.Add(new RegionState(scene, m_config));
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("[XIRC-Bridge] Region {0} not connected to IRC : {1}", scene.RegionInfo.RegionName, e.Message);
                    m_log.Debug(e);
                }
            }
            else
            {
                m_log.WarnFormat("[XIRC-Bridge] Not enabled. Connect for region {0} ignored", scene.RegionInfo.RegionName);
            }

        }

        // Called after all region modules have been loaded.
        // Iff the IRC bridge is enabled, then start all of the
        // configured channels. The set of channels is a side
        // effect of RegionState creation.

        public void PostInitialise()
        {

            if (!enabled)
                return;

            foreach (RegionState region in m_regions)
            {
                m_log.InfoFormat("[XIRC-Bridge] Opening connection for {0}:{1} on IRC server {2}:{3}",
                            region.Region, region.cs.BaseNickname, region.cs.Server, region.cs.IrcChannel);
                try
                {
                    region.Open();
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[XIRC-Bridge] Open failed for {0}:{1} on IRC server {2}:{3} : {4}",
                            region.Region, region.cs.BaseNickname, region.cs.Server, region.cs.IrcChannel,
                            e.Message);
                }
            }

        }

        // Called immediately before the region module is unloaded. Close all
        // associated channels.

        public void Close()
        {

            if (!enabled)
                return;

            // Stop each of the region sessions

            foreach (RegionState region in m_regions)
            {
                m_log.InfoFormat("[XIRC-Bridge] Closing connection for {0}:{1} on IRC server {2}:{3}",
                            region.Region, region.cs.BaseNickname, region.cs.Server, region.cs.IrcChannel);
                try
                {
                    region.Close();
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[XIRC-Bridge] Close failed for {0}:{1} on IRC server {2}:{3} : {4}",
                            region.Region, region.cs.BaseNickname, region.cs.Server, region.cs.IrcChannel,
                            e.Message);
                }
            }

            // Perform final cleanup of the channels (they now have no active clients)

            foreach (ChannelState channel in m_channels)
            {
                m_log.InfoFormat("[XIRC-Bridge] Closing connection for {0} on IRC server {1}:{2}",
                            channel.BaseNickname, channel.Server, channel.IrcChannel);
                try
                {
                    channel.Close();
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[XIRC-Bridge] Close failed for {0} on IRC server {1}:{2} : {3}",
                            channel.BaseNickname, channel.Server, channel.IrcChannel,
                            e.Message);
                }
            }

        }

        #endregion

        public XmlRpcResponse XmlRpcAdminMethod(XmlRpcRequest request)
        {

            m_log.Info("[XIRC-Bridge]: XML RPC Admin Entry");

            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable  responseData = new Hashtable();

            try
            {

                Hashtable requestData = (Hashtable)request.Params[0];
                bool    found = false;
                string region = String.Empty;

                if (password != String.Empty)
                {
                    if (!requestData.ContainsKey("password"))
                        throw new Exception("Invalid request");
                    if (requestData["password"] != password)
                        throw new Exception("Invalid request");
                }

                if (!requestData.ContainsKey("region"))
                    throw new Exception("No region name specified");
                
                foreach (RegionState rs in m_regions)
                {
                    if (rs.Region == region)
                    {
                        responseData["server"]    = rs.cs.Server;
                        responseData["port"]      = rs.cs.Port;
                        responseData["user"]      = rs.cs.User;
                        responseData["channel"]   = rs.cs.IrcChannel;
                        responseData["enabled"]   = rs.cs.irc.Enabled;
                        responseData["connected"] = rs.cs.irc.Connected;
                        responseData["nickname"]  = rs.cs.irc.Nick;
                        found = true;
                        break;
                    }
                }

                if (!found) throw new Exception(String.Format("Region <{0}> not found", region));

                responseData["success"] = true;

            }
            catch (Exception e)
            {
                m_log.InfoFormat("[XIRC-Bridge] XML RPC Admin request failed : {0}", e.Message);

                responseData["success"] = "false";
                responseData["error"]   = e.Message;

            }
            finally
            {
                response.Value = responseData;
            }

            m_log.Debug("[XIRC-Bridge]: XML RPC Admin Exit");

            return response;

        }

    }

}
