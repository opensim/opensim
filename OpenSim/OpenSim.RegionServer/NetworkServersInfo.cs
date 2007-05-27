using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Interfaces;

namespace OpenSim
{
    public class NetworkServersInfo
    {
        public string AssetURL = "http://127.0.0.1:8003/";
        public string AssetSendKey = "";

        public string GridURL = "";
        public string GridSendKey = "";
        public string GridRecvKey = "";
        public string UserURL = "";
        public string UserSendKey = "";
        public string UserRecvKey = "";
        public bool isSandbox;

        public void InitConfig(bool sandboxMode, IGenericConfig configData)
        {
            this.isSandbox = sandboxMode;

            try
            {
                if (!isSandbox)
                {
                    string attri = "";
                    //Grid Server URL
                    attri = "";
                    attri = configData.GetAttribute("GridServerURL");
                    if (attri == "")
                    {
                        this.GridURL = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Grid server URL", "http://127.0.0.1:8001/");
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
                        this.GridSendKey = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Key to send to grid server", "null");
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
                        this.GridRecvKey = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Key to expect from grid server", "null");
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
                        this.AssetURL = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Asset server URL", "http://127.0.0.1:8003/");
                        configData.SetAttribute("AssetServerURL", this.GridURL);
                    }
                    else
                    {
                        this.AssetURL = attri;
                    }

                }
                configData.Commit();
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "Config.cs:InitConfig() - Exception occured");
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, e.ToString());
            }
        }
    }
}
