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
using System.Net;
using System.Web;
using System.IO;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;
using libsecondlife;

namespace OpenSim.RegionServer
{
    public class RegionInfo : RegionInfoBase
    {
        //following should be removed and the GenericConfig object passed around,
        //so each class (AssetServer, GridServer etc) can access what config data they want 
        public string AssetURL = "http://127.0.0.1:8003/";
        public string AssetSendKey = "";

        public string GridURL = "";
        public string GridSendKey = "";
        public string GridRecvKey = "";
        public string UserURL = "";
        public string UserSendKey = "";
        public string UserRecvKey = "";
        private bool isSandbox;

        public string MasterAvatarFirstName = "";
        public string MasterAvatarLastName = "";
        public string MasterAvatarSandboxPassword = "";
        public LLUUID MasterAvatarAssignedUUID = LLUUID.Zero;

        public string DataStore;

        public RegionInfo()
        {

        }

        public void SaveToGrid()
        {
            //we really want to keep any server connection code out of here and out of the code code
            // and put it in the server connection classes (those inheriting from IGridServer etc)
            string reqtext;
            reqtext = "<Root>";
            reqtext += "<authkey>" + this.GridSendKey + "</authkey>";
            reqtext += "<sim>";
            reqtext += "<uuid>" + this.SimUUID.ToString() + "</uuid>";
            reqtext += "<regionname>" + this.RegionName + "</regionname>";
            reqtext += "<sim_ip>" + this.IPListenAddr + "</sim_ip>";
            reqtext += "<sim_port>" + this.IPListenPort.ToString() + "</sim_port>";
            reqtext += "<region_locx>" + this.RegionLocX.ToString() + "</region_locx>";
            reqtext += "<region_locy>" + this.RegionLocY.ToString() + "</region_locy>";
            reqtext += "<estate_id>1</estate_id>";
            reqtext += "</sim>";
            reqtext += "</Root>";

            byte[] reqdata = (new System.Text.ASCIIEncoding()).GetBytes(reqtext);
            string newpath = "";
            if (this.GridURL.EndsWith("/"))
            {
                newpath = this.GridURL + "sims/";
            }
            else
            {
                newpath = this.GridURL + "/sims/";
            }

            WebRequest GridSaveReq = WebRequest.Create(newpath + this.SimUUID.ToString());
            GridSaveReq.Method = "POST";
            GridSaveReq.ContentType = "application/x-www-form-urlencoded";
            GridSaveReq.ContentLength = reqdata.Length;

            Stream stOut = GridSaveReq.GetRequestStream();
            stOut.Write(reqdata, 0, reqdata.Length);
            stOut.Close();

            WebResponse gridresp = GridSaveReq.GetResponse();
            StreamReader stIn = new StreamReader(gridresp.GetResponseStream(), Encoding.ASCII);
            string GridResponse = stIn.ReadToEnd();
            stIn.Close();
            gridresp.Close();

            OpenSim.Framework.Console.MainConsole.Instance.Verbose("RegionInfo.CS:SaveToGrid() - Grid said: " + GridResponse);
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
                    this.RegionName = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("GENERAL SETTING: Simulator Name", "OpenSim Island");
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
                    string location = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("GENERAL SETTING: Grid Location X", "997");
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
                    string location = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("GENERAL SETTING: Grid Location Y", "996");
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
                    string datastore = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("GENERAL SETTING: Filename for local world storage", "localworld.yap");
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
                    string port = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("GENERAL SETTING: UDP port for client connections", "9000");
                    configData.SetAttribute("SimListenPort", port);
                    this.IPListenPort = Convert.ToInt32(port);
                }
                else
                {
                    this.IPListenPort = Convert.ToInt32(attri);
                }
                //Sim Listen Address
                attri = "";
                attri = configData.GetAttribute("SimListenAddress");
                if (attri == "")
                {
                    this.IPListenAddr = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("GENERAL SETTING: IP Address to listen on for client connections", "127.0.0.1");
                    configData.SetAttribute("SimListenAddress", this.IPListenAddr);
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
                            this.IPListenAddr = ips[0].ToString();
                        }
                        catch (Exception e)
                        {
                            e.ToString();
                            this.IPListenAddr = "127.0.0.1"; // Use the default if we fail
                        }
                    }
                    else
                    {
                        this.IPListenAddr = attri;
                    }
                }
                
                // Terrain Default File
                attri = "";
                attri = configData.GetAttribute("TerrainFile");
                if (attri == "")
                {
                    this.estateSettings.terrainFile = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("GENERAL SETTING: Default Terrain File", "default.r32");
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
                    string re = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("GENERAL SETTING: Terrain Height Multiplier", "60.0");
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
                    this.MasterAvatarFirstName = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("GENERAL SETTING: First name of Master Avatar", "Test");

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
                    this.MasterAvatarLastName = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("GENERAL SETTING: Last name of Master Avatar", "User");

                    configData.SetAttribute("MasterAvatarLastName", this.MasterAvatarLastName);
                }
                else
                {
                    this.MasterAvatarLastName = attri;
                }

                if (isSandbox) //Sandbox Mode Settings
                {
                    attri = "";
                    attri = configData.GetAttribute("MasterAvatarSandboxPassword");
                    if (attri == "")
                    {
                        this.MasterAvatarSandboxPassword = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("SANDBOX MODE SETTING: Password of Master Avatar", "test");

                        configData.SetAttribute("MasterAvatarSandboxPassword", this.MasterAvatarSandboxPassword);
                    }
                    else
                    {
                        this.MasterAvatarSandboxPassword = attri;
                    }
                }
                else //Grid Mode Settings
                {
                    //shouldn't be reading this data in here, it should be up to the classes implementing the server interfaces to read what they need from the config object

                    //Grid Server URL
                    attri = "";
                    attri = configData.GetAttribute("GridServerURL");
                    if (attri == "")
                    {
                        this.GridURL = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("GRID MODE SETTING: Grid server URL", "http://127.0.0.1:8001/");
                        configData.SetAttribute("GridServerURL", this.GridURL);
                    }
                    else
                    {
                        this.GridURL = attri;
                    }

                    //Grid Send Key
                    attri = "";
                    attri = configData.GetAttribute("GridSendKey");
                    if (attri == "")
                    {
                        this.GridSendKey = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("GRID MODE SETTING: Key to send to grid server", "null");
                        configData.SetAttribute("GridSendKey", this.GridSendKey);
                    }
                    else
                    {
                        this.GridSendKey = attri;
                    }

                    //Grid Receive Key
                    attri = "";
                    attri = configData.GetAttribute("GridRecvKey");
                    if (attri == "")
                    {
                        this.GridRecvKey = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("GRID MODE SETTING: Key to expect from grid server", "null");
                        configData.SetAttribute("GridRecvKey", this.GridRecvKey);
                    }
                    else
                    {
                        this.GridRecvKey = attri;
                    }

                    attri = "";
                    attri = configData.GetAttribute("AssetServerURL");
                    if (attri == "")
                    {
                        this.AssetURL = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("GRID MODE SETTING: Asset server URL", "http://127.0.0.1:8003/");
                        configData.SetAttribute("AssetServerURL", this.AssetURL);
                    }
                    else
                    {
                        this.AssetURL = attri;
                    }

                }
                 
                this.RegionHandle = Util.UIntsToLong((RegionLocX * 256), (RegionLocY * 256));
                if (!this.isSandbox)
                {
                    this.SaveToGrid();
                }
                configData.Commit();
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.Warn("Config.cs:InitConfig() - Exception occured");
                OpenSim.Framework.Console.MainConsole.Instance.Warn(e.ToString());
            }

            OpenSim.Framework.Console.MainConsole.Instance.Verbose("Simulator Settings Loaded");
            /* MainConsole.Instance.Notice("UUID: " + this.SimUUID.ToStringHyphenated());
            MainConsole.Instance.Notice("Name: " + this.RegionName);
            MainConsole.Instance.Notice("Region Location: [" + this.RegionLocX.ToString() + "," + this.RegionLocY + "]");
            MainConsole.Instance.Notice("Region Handle: " + this.RegionHandle.ToString());
            MainConsole.Instance.Notice("Listening on IP: " + this.IPListenAddr + ":" + this.IPListenPort);
            MainConsole.Instance.Notice("Sandbox Mode? " + isSandbox.ToString());
            MainConsole.Instance.Notice("Asset URL: " + this.AssetURL);
            MainConsole.Instance.Notice("Asset key: " + this.AssetSendKey);
            MainConsole.Instance.Notice("Grid URL: " + this.GridURL);
            MainConsole.Instance.Notice("Grid key: " + this.GridSendKey); */
        }
    }
}
