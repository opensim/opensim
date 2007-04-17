using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Web;
using System.IO;
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

            WebRequest GridSaveReq = WebRequest.Create(this.GridURL + "sims/" + this.SimUUID.ToString());
            GridSaveReq.Method = "POST";
            GridSaveReq.ContentType = "application/x-www-form-urlencoded";
            GridSaveReq.ContentLength = reqdata.Length;

            Stream stOut = GridSaveReq.GetRequestStream();
            stOut.Write(reqdata,0,reqdata.Length);
            stOut.Close();

            WebResponse gridresp = GridSaveReq.GetResponse();
	    StreamReader stIn = new StreamReader(gridresp.GetResponseStream(), Encoding.ASCII);
            string GridResponse = stIn.ReadToEnd();
            stIn.Close();
	    gridresp.Close();

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("RegionInfo.CS:SaveToGrid() - Grid said: " + GridResponse);
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
                    this.RegionName = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Name", "OpenSim test");
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
                    string location = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Grid Location X", "997");
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
                    string location = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Grid Location Y", "996");
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
                    string port = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("UDP port for client connections", "9000");
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
                    this.IPListenAddr = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("IP Address to listen on for client connections", "127.0.0.1");
                    configData.SetAttribute("SimListenAddress", this.IPListenAddr);
                }
                else
                {
                    this.IPListenAddr = attri;
                }

                if (!isSandbox)
                {
                    //shouldn't be reading this data in here, it should be up to the classes implementing the server interfaces to read what they need from the config object

                    //Grid Server URL
                    attri = "";
                    attri = configData.GetAttribute("GridServerURL");
                    if (attri == "")
                    {
                        this.GridURL = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Grid server URL");
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
                        this.GridSendKey = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Key to send to grid server");
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
                        this.GridRecvKey = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Key to expect from grid server");
                        configData.SetAttribute("GridRecvKey", this.GridRecvKey);
                    }
                    else
                    {
                        this.GridRecvKey = attri;
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
