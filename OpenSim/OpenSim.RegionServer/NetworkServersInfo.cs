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
                        this.GridURL = OpenSim.Framework.Console.MainLog.Instance.CmdPrompt("Grid server URL", "http://127.0.0.1:8001/");
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
                        this.GridSendKey = OpenSim.Framework.Console.MainLog.Instance.CmdPrompt("Key to send to grid server", "null");
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
                        this.GridRecvKey = OpenSim.Framework.Console.MainLog.Instance.CmdPrompt("Key to expect from grid server", "null");
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
                        this.AssetURL = OpenSim.Framework.Console.MainLog.Instance.CmdPrompt("Asset server URL", "http://127.0.0.1:8003/");
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
                OpenSim.Framework.Console.MainLog.Instance.Warn("Config.cs:InitConfig() - Exception occured");
                OpenSim.Framework.Console.MainLog.Instance.Warn(e.ToString());
            }
        }
    }
    
}
