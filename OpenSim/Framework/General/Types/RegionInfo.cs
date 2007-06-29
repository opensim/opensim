/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Console;
using libsecondlife;

namespace OpenSim.Framework.Types
{
    public class RegionInfo
    {
        public LLUUID SimUUID = new LLUUID();
        public string RegionName = "";
        public uint RegionLocX = 0;
        public uint RegionLocY = 0;
        public ulong RegionHandle = 0;

        public string DataStore = "";
        public bool isSandbox = false;

        public LLUUID MasterAvatarAssignedUUID = new LLUUID();
        public string MasterAvatarFirstName = "";
        public string MasterAvatarLastName = "";
        public string MasterAvatarSandboxPassword = "";

        /// <summary>
        /// Port used for listening (TCP and UDP)
        /// </summary>
        /// <remarks>Seperate TCP and UDP</remarks>
        public int CommsIPListenPort = 0;
        /// <summary>
        /// Address used for internal listening (default: 0.0.0.0?)
        /// </summary>
        public string CommsIPListenAddr = "";
        /// <summary>
        /// Address used for external addressing (DNS or IP)
        /// </summary>
        public string CommsExternalAddress = "";


        public EstateSettings estateSettings;

        public RegionInfo()
        {
            estateSettings = new EstateSettings();
        }


        public void InitConfig(bool sandboxMode, IGenericConfig configData)
        {
            this.isSandbox = sandboxMode;
            try
            {
                // Sim UUID
                string attri = "";
                attri = configData.GetAttribute("SimUUID");
                if (attri == "")
                {
                    this.SimUUID = LLUUID.Random();
                    configData.SetAttribute("SimUUID", this.SimUUID.ToString());
                }
                else
                {
                    this.SimUUID = new LLUUID(attri);
                }

                // Sim name
                attri = "";
                attri = configData.GetAttribute("SimName");
                if (attri == "")
                {
                    this.RegionName = OpenSim.Framework.Console.MainLog.Instance.CmdPrompt("Name", "OpenSim test");
                    configData.SetAttribute("SimName", this.RegionName);
                }
                else
                {
                    this.RegionName = attri;
                }
                // Sim/Grid location X
                attri = "";
                attri = configData.GetAttribute("SimLocationX");
                if (attri == "")
                {
                    string location = OpenSim.Framework.Console.MainLog.Instance.CmdPrompt("Grid Location X", "1000");
                    configData.SetAttribute("SimLocationX", location);
                    this.RegionLocX = (uint)Convert.ToUInt32(location);
                }
                else
                {
                    this.RegionLocX = (uint)Convert.ToUInt32(attri);
                }
                // Sim/Grid location Y
                attri = "";
                attri = configData.GetAttribute("SimLocationY");
                if (attri == "")
                {
                    string location = OpenSim.Framework.Console.MainLog.Instance.CmdPrompt("Grid Location Y", "1000");
                    configData.SetAttribute("SimLocationY", location);
                    this.RegionLocY = (uint)Convert.ToUInt32(location);
                }
                else
                {
                    this.RegionLocY = (uint)Convert.ToUInt32(attri);
                }

                // Local storage datastore
                attri = "";
                attri = configData.GetAttribute("Datastore");
                if (attri == "")
                {
                    string datastore = OpenSim.Framework.Console.MainLog.Instance.CmdPrompt("Filename for local storage", "localworld.yap");
                    configData.SetAttribute("Datastore", datastore);
                    this.DataStore = datastore;
                }
                else
                {
                    this.DataStore = attri;
                }

                //Sim Listen Port
                attri = "";
                attri = configData.GetAttribute("SimListenPort");
                if (attri == "")
                {
                    string port = OpenSim.Framework.Console.MainLog.Instance.CmdPrompt("UDP port for client connections", "9000");
                    configData.SetAttribute("SimListenPort", port);
                    this.CommsIPListenPort = Convert.ToInt32(port);
                }
                else
                {
                    this.CommsIPListenPort = Convert.ToInt32(attri);
                }

                //Sim Listen Address
                attri = "";
                attri = configData.GetAttribute("SimListenAddress");
                if (attri == "")
                {
                    this.CommsIPListenAddr = OpenSim.Framework.Console.MainLog.Instance.CmdPrompt("IP Address to listen on for client connections", "0.0.0.0");
                    configData.SetAttribute("SimListenAddress", this.CommsIPListenAddr);
                }
                else
                {
                    // Probably belongs elsewhere, but oh well.
                    if (attri.Trim().StartsWith("SYSTEMIP"))
                    {
                        string localhostname = System.Net.Dns.GetHostName();
                        System.Net.IPAddress[] ips = System.Net.Dns.GetHostAddresses(localhostname);
                        try
                        {
                            this.CommsIPListenAddr = "0.0.0.0"; // Incase a IPv4 address isnt found

                            foreach (System.Net.IPAddress ip in ips)
                            {
                                if (ip.AddressFamily.ToString() == System.Net.Sockets.ProtocolFamily.InterNetwork.ToString())
                                {
                                    this.CommsIPListenAddr = ip.ToString();
                                    break;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            e.ToString();
                            this.CommsIPListenAddr = "0.0.0.0"; // Use the default if we fail
                        }
                    }
                    else
                    {
                        this.CommsIPListenAddr = attri;
                    }
                }

                // Sim External Address
                attri = "";
                attri = configData.GetAttribute("SimExternalAddress");
                if (attri == "")
                {
                    this.CommsExternalAddress = OpenSim.Framework.Console.MainLog.Instance.CmdPrompt("IP or DNS address to send external clients to", "localhost");
                    configData.SetAttribute("SimExternalAddress", this.CommsExternalAddress);
                }
                else
                {
                    this.CommsExternalAddress = attri;
                }

                attri = "";
                attri = configData.GetAttribute("TerrainFile");
                if (attri == "")
                {
                    this.estateSettings.terrainFile = OpenSim.Framework.Console.MainLog.Instance.CmdPrompt("GENERAL SETTING: Default Terrain File", "default.r32");
                    configData.SetAttribute("TerrainFile", this.estateSettings.terrainFile);
                }
                else
                {
                    this.estateSettings.terrainFile = attri;
                }

                attri = "";
                attri = configData.GetAttribute("TerrainMultiplier");
                if (attri == "")
                {
                    string re = OpenSim.Framework.Console.MainLog.Instance.CmdPrompt("GENERAL SETTING: Terrain Height Multiplier", "60.0");
                    this.estateSettings.terrainMultiplier = Convert.ToDouble(re, CultureInfo.InvariantCulture);
                    configData.SetAttribute("TerrainMultiplier", this.estateSettings.terrainMultiplier.ToString());
                }
                else
                {
                    this.estateSettings.terrainMultiplier = Convert.ToDouble(attri);
                }

                attri = "";
                attri = configData.GetAttribute("MasterAvatarFirstName");
                if (attri == "")
                {
                    this.MasterAvatarFirstName = OpenSim.Framework.Console.MainLog.Instance.CmdPrompt("First name of Master Avatar (Land and Region Owner)", "Test");

                    configData.SetAttribute("MasterAvatarFirstName", this.MasterAvatarFirstName);
                }
                else
                {
                    this.MasterAvatarFirstName = attri;
                }

                attri = "";
                attri = configData.GetAttribute("MasterAvatarLastName");
                if (attri == "")
                {
                    this.MasterAvatarLastName = OpenSim.Framework.Console.MainLog.Instance.CmdPrompt("Last name of Master Avatar (Land and Region Owner)", "User");

                    configData.SetAttribute("MasterAvatarLastName", this.MasterAvatarLastName);
                }
                else
                {
                    this.MasterAvatarLastName = attri;
                }

                if (isSandbox) //Sandbox Mode Specific Settings
                {
                    attri = "";
                    attri = configData.GetAttribute("MasterAvatarSandboxPassword");
                    if (attri == "")
                    {
                        this.MasterAvatarSandboxPassword = OpenSim.Framework.Console.MainLog.Instance.CmdPrompt("Password of Master Avatar (Needed for sandbox mode account creation only)", "test");

                        //Should I store this?
                        configData.SetAttribute("MasterAvatarSandboxPassword", this.MasterAvatarSandboxPassword);
                    }
                    else
                    {
                        this.MasterAvatarSandboxPassword = attri;
                    }
                }

                this.RegionHandle = Util.UIntsToLong((RegionLocX * 256), (RegionLocY * 256));
               
                configData.Commit();
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainLog.Instance.Warn("Config.cs:InitConfig() - Exception occured");
                OpenSim.Framework.Console.MainLog.Instance.Warn(e.ToString());
            }

            OpenSim.Framework.Console.MainLog.Instance.Verbose("Sim settings loaded:");
            OpenSim.Framework.Console.MainLog.Instance.Verbose( "UUID: " + this.SimUUID.ToStringHyphenated());
            OpenSim.Framework.Console.MainLog.Instance.Verbose( "Name: " + this.RegionName);
            OpenSim.Framework.Console.MainLog.Instance.Verbose( "Region Location: [" + this.RegionLocX.ToString() + "," + this.RegionLocY + "]");
            OpenSim.Framework.Console.MainLog.Instance.Verbose( "Region Handle: " + this.RegionHandle.ToString());
            OpenSim.Framework.Console.MainLog.Instance.Verbose( "Listening on IP: " + this.CommsIPListenAddr + ":" + this.CommsIPListenPort);
            OpenSim.Framework.Console.MainLog.Instance.Verbose( "Sandbox Mode? " + isSandbox.ToString());
  
        }
    }
}
