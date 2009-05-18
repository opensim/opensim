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
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Hypergrid;

namespace OpenSim
{
    public class HGCommands
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public static IHyperlink HGServices = null;

        private static uint m_autoMappingX = 0;
        private static uint m_autoMappingY = 0;
        private static bool m_enableAutoMapping = false;

        public static Scene CreateScene(RegionInfo regionInfo, AgentCircuitManager circuitManager, CommunicationsManager m_commsManager,
            StorageManager storageManager, ModuleLoader m_moduleLoader, ConfigSettings m_configSettings, OpenSimConfigSource m_config, string m_version)
        {
            HGSceneCommunicationService sceneGridService = new HGSceneCommunicationService(m_commsManager, HGServices);

            return
                new HGScene(
                    regionInfo, circuitManager, m_commsManager, sceneGridService, storageManager,
                    m_moduleLoader, false, m_configSettings.PhysicalPrim,
                    m_configSettings.See_into_region_from_neighbor, m_config.Source, m_version);
        }

        public static void RunHGCommand(string command, string[] cmdparams, Scene scene)
        {
            if (command.Equals("link-mapping"))
            {
                if (cmdparams.Length == 2)
                {
                    try
                    {
                        m_autoMappingX = Convert.ToUInt32(cmdparams[0]);
                        m_autoMappingY = Convert.ToUInt32(cmdparams[1]);
                        m_enableAutoMapping = true;
                    }
                    catch (Exception)
                    {
                        m_autoMappingX = 0;
                        m_autoMappingY = 0;
                        m_enableAutoMapping = false;
                    }
                }
            }
            else if (command.Equals("link-region"))
            {
                if (cmdparams.Length < 3)
                {
                    if ((cmdparams.Length == 1) || (cmdparams.Length == 2))
                    {
                        LoadXmlLinkFile(cmdparams, scene);
                    }
                    else
                    {
                        LinkRegionCmdUsage();
                    }
                    return;
                }

                if (cmdparams[2].Contains(":"))
                {
                    // New format
                    uint xloc, yloc;
                    string mapName;
                    try
                    {
                        xloc = Convert.ToUInt32(cmdparams[0]);
                        yloc = Convert.ToUInt32(cmdparams[1]);
                        mapName = cmdparams[2];
                        if (cmdparams.Length > 3)
                            for (int i = 3; i < cmdparams.Length; i++)
                                mapName += " " + cmdparams[i];

                        m_log.Info(">> MapName: " + mapName);
                        //internalPort = Convert.ToUInt32(cmdparams[4]);
                        //remotingPort = Convert.ToUInt32(cmdparams[5]);
                    }
                    catch (Exception e)
                    {
                        m_log.Warn("[HGrid] Wrong format for link-region command: " + e.Message);
                        LinkRegionCmdUsage();
                        return;
                    }

                    HGHyperlink.TryLinkRegionToCoords(scene, null, mapName, xloc, yloc);
                }
                else
                {
                    // old format
                    RegionInfo regInfo;
                    uint xloc, yloc;
                    uint externalPort;
                    string externalHostName;
                    try
                    {
                        xloc = Convert.ToUInt32(cmdparams[0]);
                        yloc = Convert.ToUInt32(cmdparams[1]);
                        externalPort = Convert.ToUInt32(cmdparams[3]);
                        externalHostName = cmdparams[2];
                        //internalPort = Convert.ToUInt32(cmdparams[4]);
                        //remotingPort = Convert.ToUInt32(cmdparams[5]);
                    }
                    catch (Exception e)
                    {
                        m_log.Warn("[HGrid] Wrong format for link-region command: " + e.Message);
                        LinkRegionCmdUsage();
                        return;
                    }

                    //if (TryCreateLink(xloc, yloc, externalPort, externalHostName, out regInfo))
                    if (HGHyperlink.TryCreateLink(scene, null, xloc, yloc, "", externalPort, externalHostName, out regInfo))
                    {
                        if (cmdparams.Length >= 5)
                        {
                            regInfo.RegionName = "";
                            for (int i = 4; i < cmdparams.Length; i++)
                                regInfo.RegionName += cmdparams[i] + " ";
                        }
                    }
                }
                return;
            }
            else if (command.Equals("unlink-region"))
            {
                if (cmdparams.Length < 1)
                {
                    UnlinkRegionCmdUsage();
                    return;
                }
                if (HGHyperlink.TryUnlinkRegion(scene, cmdparams[0]))
                    m_log.InfoFormat("[HGrid]: Successfully unlinked {0}", cmdparams[0]);
                else
                    m_log.InfoFormat("[HGrid]: Unable to unlink {0}, region not found", cmdparams[0]);
            }
        }

        private static void LoadXmlLinkFile(string[] cmdparams, Scene scene)
        {
            //use http://www.hgurl.com/hypergrid.xml for test
            try
            {
                XmlReader r = XmlReader.Create(cmdparams[0]);
                XmlConfigSource cs = new XmlConfigSource(r);
                string[] excludeSections = null;

                if (cmdparams.Length == 2)
                {
                    if (cmdparams[1].ToLower().StartsWith("excludelist:"))
                    {
                        string excludeString = cmdparams[1].ToLower();
                        excludeString = excludeString.Remove(0, 12);
                        char[] splitter = {';'};

                        excludeSections = excludeString.Split(splitter);
                    }
                }

                for (int i = 0; i < cs.Configs.Count; i++)
                {
                    bool skip = false;
                    if ((excludeSections != null) && (excludeSections.Length > 0))
                    {
                        for (int n = 0; n < excludeSections.Length; n++)
                        {
                            if (excludeSections[n] == cs.Configs[i].Name.ToLower())
                            {
                                skip = true;
                                break;
                            }
                        }
                    }
                    if (!skip)
                    {
                        ReadLinkFromConfig(cs.Configs[i], scene);
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
        }


        private static void ReadLinkFromConfig(IConfig config, Scene scene)
        {
            RegionInfo regInfo;
            uint xloc, yloc;
            uint externalPort;
            string externalHostName;
            uint realXLoc, realYLoc;

            xloc = Convert.ToUInt32(config.GetString("xloc", "0"));
            yloc = Convert.ToUInt32(config.GetString("yloc", "0"));
            externalPort = Convert.ToUInt32(config.GetString("externalPort", "0"));
            externalHostName = config.GetString("externalHostName", "");
            realXLoc = Convert.ToUInt32(config.GetString("real-xloc", "0"));
            realYLoc = Convert.ToUInt32(config.GetString("real-yloc", "0"));

            if (m_enableAutoMapping)
            {
                xloc = (uint) ((xloc%100) + m_autoMappingX);
                yloc = (uint) ((yloc%100) + m_autoMappingY);
            }

            if (((realXLoc == 0) && (realYLoc == 0)) ||
                (((realXLoc - xloc < 3896) || (xloc - realXLoc < 3896)) &&
                 ((realYLoc - yloc < 3896) || (yloc - realYLoc < 3896))))
            {
                if (
                    HGHyperlink.TryCreateLink(scene, null, xloc, yloc, "", externalPort,
                                              externalHostName, out regInfo))
                {
                    regInfo.RegionName = config.GetString("localName", "");
                }
            }
        }


        private static void LinkRegionCmdUsage()
        {
            m_log.Info("Usage: link-region <Xloc> <Yloc> <HostName>:<HttpPort>[:<RemoteRegionName>]");
            m_log.Info("Usage: link-region <Xloc> <Yloc> <HostName> <HttpPort> [<LocalName>]");
            m_log.Info("Usage: link-region <URI_of_xml> [<exclude>]");
        }

        private static void UnlinkRegionCmdUsage()
        {
            m_log.Info("Usage: unlink-region <HostName>:<HttpPort>");
            m_log.Info("Usage: unlink-region <LocalName>");
        }

    }
}
