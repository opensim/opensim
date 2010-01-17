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
//using OpenSim.Framework.Communications;
using OpenSim.Framework.Console;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Services.HypergridService
{
    public class HGCommands
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private HypergridService m_HypergridService;

        private static uint m_autoMappingX = 0;
        private static uint m_autoMappingY = 0;
        private static bool m_enableAutoMapping = false;

        public HGCommands(HypergridService service)
        {
            m_HypergridService = service;
        }

        public void HandleShow(string module, string[] cmd)
        {
            if (cmd.Length != 2)
            {
                MainConsole.Instance.Output("Syntax: show hyperlinks");
                return;
            }
            List<GridRegion> regions = new List<GridRegion>(m_HypergridService.m_HyperlinkRegions.Values);
            if (regions == null || regions.Count < 1)
            {
                MainConsole.Instance.Output("No hyperlinks");
                return;
            }

            MainConsole.Instance.Output("Region Name          Region UUID");
            MainConsole.Instance.Output("Location             URI");
            MainConsole.Instance.Output("Owner ID  ");
            MainConsole.Instance.Output("-------------------------------------------------------------------------------");
            foreach (GridRegion r in regions)
            {
                MainConsole.Instance.Output(String.Format("{0,-20} {1}\n{2,-20} {3}\n{4,-39} \n\n",
                        r.RegionName, r.RegionID,
                        String.Format("{0},{1}", r.RegionLocX, r.RegionLocY), "http://" + r.ExternalHostName + ":" + r.HttpPort.ToString(),
                        r.EstateOwner.ToString()));
            }
            return;
        }
        public void RunCommand(string module, string[] cmdparams)
        {
            List<string> args = new List<string>(cmdparams);
            if (args.Count < 1)
                return;

            string command = args[0];
            args.RemoveAt(0);

            cmdparams = args.ToArray();

            RunHGCommand(command, cmdparams);
            
        }

        private void RunHGCommand(string command, string[] cmdparams)
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
                        LoadXmlLinkFile(cmdparams);
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
                    int xloc, yloc;
                    string mapName;
                    try
                    {
                        xloc = Convert.ToInt32(cmdparams[0]);
                        yloc = Convert.ToInt32(cmdparams[1]);
                        mapName = cmdparams[2];
                        if (cmdparams.Length > 3)
                            for (int i = 3; i < cmdparams.Length; i++)
                                mapName += " " + cmdparams[i];

                        //m_log.Info(">> MapName: " + mapName);
                    }
                    catch (Exception e)
                    {
                        MainConsole.Instance.Output("[HGrid] Wrong format for link-region command: " + e.Message);
                        LinkRegionCmdUsage();
                        return;
                    }

                    // Convert cell coordinates given by the user to meters
                    xloc = xloc * (int)Constants.RegionSize;
                    yloc = yloc * (int)Constants.RegionSize;
                    string reason = string.Empty;
                    if (m_HypergridService.TryLinkRegionToCoords(mapName, xloc, yloc, out reason) == null)
                        MainConsole.Instance.Output("Failed to link region: " + reason);
                    MainConsole.Instance.Output("Hyperlink estalished");
                }
                else
                {
                    // old format
                    GridRegion regInfo;
                    int xloc, yloc;
                    uint externalPort;
                    string externalHostName;
                    try
                    {
                        xloc = Convert.ToInt32(cmdparams[0]);
                        yloc = Convert.ToInt32(cmdparams[1]);
                        externalPort = Convert.ToUInt32(cmdparams[3]);
                        externalHostName = cmdparams[2];
                        //internalPort = Convert.ToUInt32(cmdparams[4]);
                        //remotingPort = Convert.ToUInt32(cmdparams[5]);
                    }
                    catch (Exception e)
                    {
                        MainConsole.Instance.Output("[HGrid] Wrong format for link-region command: " + e.Message);
                        LinkRegionCmdUsage();
                        return;
                    }

                    // Convert cell coordinates given by the user to meters
                    xloc = xloc * (int)Constants.RegionSize;
                    yloc = yloc * (int)Constants.RegionSize;
                    string reason = string.Empty;
                    if (m_HypergridService.TryCreateLink(xloc, yloc, "", externalPort, externalHostName, out regInfo, out reason))
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
                if (m_HypergridService.TryUnlinkRegion(cmdparams[0]))
                    MainConsole.Instance.Output("Successfully unlinked " + cmdparams[0]);
                else
                    MainConsole.Instance.Output("Unable to unlink " + cmdparams[0] + ", region not found.");
            }
        }

        private void LoadXmlLinkFile(string[] cmdparams)
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
                        char[] splitter = { ';' };

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
                        ReadLinkFromConfig(cs.Configs[i]);
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
        }


        private void ReadLinkFromConfig(IConfig config)
        {
            GridRegion regInfo;
            int xloc, yloc;
            uint externalPort;
            string externalHostName;
            uint realXLoc, realYLoc;

            xloc = Convert.ToInt32(config.GetString("xloc", "0"));
            yloc = Convert.ToInt32(config.GetString("yloc", "0"));
            externalPort = Convert.ToUInt32(config.GetString("externalPort", "0"));
            externalHostName = config.GetString("externalHostName", "");
            realXLoc = Convert.ToUInt32(config.GetString("real-xloc", "0"));
            realYLoc = Convert.ToUInt32(config.GetString("real-yloc", "0"));

            if (m_enableAutoMapping)
            {
                xloc = (int)((xloc % 100) + m_autoMappingX);
                yloc = (int)((yloc % 100) + m_autoMappingY);
            }

            if (((realXLoc == 0) && (realYLoc == 0)) ||
                (((realXLoc - xloc < 3896) || (xloc - realXLoc < 3896)) &&
                 ((realYLoc - yloc < 3896) || (yloc - realYLoc < 3896))))
            {
                xloc = xloc * (int)Constants.RegionSize;
                yloc = yloc * (int)Constants.RegionSize;
                string reason = string.Empty;
                if (m_HypergridService.TryCreateLink(xloc, yloc, "", externalPort,
                                                     externalHostName, out regInfo, out reason))
                {
                    regInfo.RegionName = config.GetString("localName", "");
                }
                else
                    MainConsole.Instance.Output("Unable to link " + externalHostName + ": " + reason);
            }
        }


        private void LinkRegionCmdUsage()
        {
            MainConsole.Instance.Output("Usage: link-region <Xloc> <Yloc> <HostName>:<HttpPort>[:<RemoteRegionName>]");
            MainConsole.Instance.Output("Usage: link-region <Xloc> <Yloc> <HostName> <HttpPort> [<LocalName>]");
            MainConsole.Instance.Output("Usage: link-region <URI_of_xml> [<exclude>]");
        }

        private void UnlinkRegionCmdUsage()
        {
            MainConsole.Instance.Output("Usage: unlink-region <HostName>:<HttpPort>");
            MainConsole.Instance.Output("Usage: unlink-region <LocalName>");
        }

    }
}
