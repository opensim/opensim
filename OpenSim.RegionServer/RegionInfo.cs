using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;
using libsecondlife;

namespace OpenSim
{
    public class RegionInfo  // could inherit from SimProfileBase
    {
        public LLUUID SimUUID;
        public string RegionName;
        public uint RegionLocX;
        public uint RegionLocY;
        public ulong RegionHandle;

        public int IPListenPort;
        public string IPListenAddr;

        //following should be removed and the GenericConfig object passed around,
        //so each class (AssetServer, GridServer etc) can access what config data they want 
        public string AssetURL = "";
        public string AssetSendKey = "";

        public string GridURL = "";
        public string GridSendKey = "";
        public string GridRecvKey = "";
        public string UserURL = "";
        public string UserSendKey = "";
        public string UserRecvKey = "";
        private bool isSandbox;

        public RegionInfo()
        {

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
                    this.RegionName = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Name [OpenSim test]: ", "OpenSim test");
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
                    string location = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Grid Location X [997]: ", "997");
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
                    string location = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Grid Location Y [996]: ", "996");
                    configData.SetAttribute("SimLocationY", location);
                    this.RegionLocY = (uint)Convert.ToUInt32(location);
                }
                else
                {
                    this.RegionLocY = (uint)Convert.ToUInt32(attri);
                }
                //Sim Listen Port
                attri = "";
                attri = configData.GetAttribute("SimListenPort");
                if (attri == "")
                {
                    string port = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("UDP port for client connections [9000]: ", "9000");
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
                    this.IPListenAddr = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("IP Address to listen on for client connections [127.0.0.1]: ", "127.0.0.1");
                    configData.SetAttribute("SimListenAddress", this.IPListenAddr);
                }
                else
                {
                    this.IPListenAddr = attri;
                }

                if (!isSandbox)
                {
                    //shouldn't be reading this data in here, it should be up to the classes implementing the server interfaces to read what they need from the config object

                    // Asset Server URL
                    attri = "";
                    attri = configData.GetAttribute("AssetServerURL");
                    if (attri == "")
                    {
                        this.AssetURL = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Asset server URL: ");
                        configData.SetAttribute("AssetServerURL", this.AssetURL);
                    }
                    else
                    {
                        this.AssetURL = attri;
                    }
                    //Asset Server key
                    attri = "";
                    attri = configData.GetAttribute("AssetServerKey");
                    if (attri == "")
                    {
                        this.AssetSendKey = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Asset server key: ");
                        configData.SetAttribute("AssetServerKey", this.AssetSendKey);
                    }
                    else
                    {
                        this.AssetSendKey = attri;
                    }
                    //Grid Sever URL
                    attri = "";
                    attri = configData.GetAttribute("GridServerURL");
                    if (attri == "")
                    {
                        this.GridURL = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Grid server URL: ");
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
                        this.GridSendKey = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Key to send to grid server: ");
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
                        this.GridRecvKey = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Key to expect from grid server: ");
                        configData.SetAttribute("GridRecvKey", this.GridRecvKey);
                    }
                    else
                    {
                        this.GridRecvKey = attri;
                    }
                    //User Server URL
                    attri = "";
                    attri = configData.GetAttribute("UserServerURL");
                    if (attri == "")
                    {
                        this.UserURL = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("User server URL: ");
                        configData.SetAttribute("UserServerURL", this.UserURL);
                    }
                    else
                    {
                        this.UserURL = attri;
                    }
                    //User Send Key
                    attri = "";
                    attri = configData.GetAttribute("UserSendKey");
                    if (attri == "")
                    {
                        this.UserSendKey = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Key to send to user server: ");
                        configData.SetAttribute("UserSendKey", this.UserSendKey);
                    }
                    else
                    {
                        this.UserSendKey = attri;
                    }
                    //User Receive Key
                    attri = "";
                    attri = configData.GetAttribute("UserRecvKey");
                    if (attri == "")
                    {
                        this.UserRecvKey = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Key to expect from user server: ");
                        configData.SetAttribute("UserRecvKey", this.UserRecvKey);
                    }
                    else
                    {
                        this.UserRecvKey = attri;
                    }
                }
                this.RegionHandle = Util.UIntsToLong((RegionLocX * 256), (RegionLocY * 256));
                configData.Commit();
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Config.cs:InitConfig() - Exception occured");
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(e.ToString());
            }

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Sim settings loaded:");
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("UUID: " + this.SimUUID.ToStringHyphenated());
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Name: " + this.RegionName);
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Region Location: [" + this.RegionLocX.ToString() + "," + this.RegionLocY + "]");
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Region Handle: " + this.RegionHandle.ToString());
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Listening on IP: " + this.IPListenAddr + ":" + this.IPListenPort);
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Sandbox Mode? " + isSandbox.ToString());
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Asset URL: " + this.AssetURL);
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Asset key: " + this.AssetSendKey);
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Grid URL: " + this.GridURL);
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Grid key: " + this.GridSendKey);
        }
    }
}
