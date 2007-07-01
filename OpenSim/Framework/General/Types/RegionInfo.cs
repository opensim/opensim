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
using System.Net;

namespace OpenSim.Framework.Types
{
    public class RegionInfo
    {
        public LLUUID SimUUID = new LLUUID();
        public string RegionName = "";

        private IPEndPoint m_internalEndPoint;
        public IPEndPoint InternalEndPoint
        {
            get
            {
                return m_internalEndPoint;
            }
        }

        public IPEndPoint ExternalEndPoint
        {
            get
            {
                return new IPEndPoint( Dns.GetHostAddresses( m_externalHostName )[0], m_internalEndPoint.Port );
            }
        }
        
        private string m_externalHostName;
        public string ExternalHostName
        {
            get
            {
                return m_externalHostName;
            }
        }

        private uint? m_regionLocX;
        public uint RegionLocX
        {
            get
            {
                return m_regionLocX.Value;
            }
        }

        private uint? m_regionLocY;
        public uint RegionLocY
        {
            get
            {
                return m_regionLocY.Value;
            }
        }

        private ulong? m_regionHandle;
        public ulong RegionHandle
        {
            get
            {
                if (!m_regionHandle.HasValue)
                {
                    m_regionHandle = Util.UIntsToLong((RegionLocX * 256), (RegionLocY * 256));
                }

                return m_regionHandle.Value;
            }
        }

        public string DataStore = "";
        public bool isSandbox = false;

        public LLUUID MasterAvatarAssignedUUID = new LLUUID();
        public string MasterAvatarFirstName = "";
        public string MasterAvatarLastName = "";
        public string MasterAvatarSandboxPassword = "";

        //private int? m_commsIPListenPort;

        ///// <summary>
        ///// Port used for listening (TCP and UDP)
        ///// </summary>
        ///// <remarks>Seperate TCP and UDP</remarks>
        //public int CommsIPListenPort
        //{
        //    get
        //    {
        //        return m_commsIPListenPort.Value;
        //    }
        //}

        //private string m_commsIPListenAddr;
        ///// <summary>
        ///// Address used for internal listening (default: 0.0.0.0?)
        ///// </summary>
        //public string CommsIPListenAddr        
        //{
        //    get
        //    {
        //        return m_commsIPListenAddr;
        //    }            
        //}

        //private string m_commsExternalAddress;
        ///// <summary>
        ///// Address used for external addressing (DNS or IP)
        ///// </summary>
        //public string CommsExternalAddress
        //{
        //    get
        //    {
        //        return m_commsExternalAddress;
        //    }
        //}


        public EstateSettings estateSettings;

        public RegionInfo()
        {
            estateSettings = new EstateSettings();
        }

        public RegionInfo(uint regionLocX, uint regionLocY, IPEndPoint internalEndPoint, string externalUri)
            : this()
        {
            m_regionLocX = regionLocX;
            m_regionLocY = regionLocY;

            //m_commsIPListenAddr = simIp;
            //m_commsIPListenPort = simPort;
            //m_commsExternalAddress = simUri;

            m_internalEndPoint = internalEndPoint;
            m_externalHostName = externalUri;
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
                    m_regionLocX = (uint)Convert.ToUInt32(location);
                }
                else
                {
                    m_regionLocX = (uint)Convert.ToUInt32(attri);
                }
                // Sim/Grid location Y
                attri = "";
                attri = configData.GetAttribute("SimLocationY");
                if (attri == "")
                {
                    string location = OpenSim.Framework.Console.MainLog.Instance.CmdPrompt("Grid Location Y", "1000");
                    configData.SetAttribute("SimLocationY", location);
                    m_regionLocY = (uint)Convert.ToUInt32(location);
                }
                else
                {
                    m_regionLocY = (uint)Convert.ToUInt32(attri);
                }

                m_regionHandle = null;

                this.DataStore = GetString(configData, "Datastore", "localworld.yap", "Filename for local storage");
                
                IPAddress internalAddress = GetIPAddress(configData, "InternalIPAddress", "0.0.0.0", "Internal IP Address for UDP client connections");
                int internalPort = GetIPPort(configData, "InternalIPPort", "9000", "Internal IP Port for UDP client connections");
                m_internalEndPoint = new IPEndPoint(internalAddress, internalPort);

                m_externalHostName = GetString(configData, "ExternalHostName", "localhost", "External Host Name");

                estateSettings.terrainFile =
                    GetString(configData, "TerrainFile", "default.r32", "GENERAL SETTING: Default Terrain File");                
                
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

                configData.Commit();
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainLog.Instance.Warn("Config.cs:InitConfig() - Exception occured");
                OpenSim.Framework.Console.MainLog.Instance.Warn(e.ToString());
            }

            OpenSim.Framework.Console.MainLog.Instance.Verbose("Sim settings loaded:");
            OpenSim.Framework.Console.MainLog.Instance.Verbose("UUID: " + this.SimUUID.ToStringHyphenated());
            OpenSim.Framework.Console.MainLog.Instance.Verbose("Name: " + this.RegionName);
            OpenSim.Framework.Console.MainLog.Instance.Verbose("Region Location: [" + this.RegionLocX.ToString() + "," + this.RegionLocY + "]");
            OpenSim.Framework.Console.MainLog.Instance.Verbose("Region Handle: " + this.RegionHandle.ToString());
            OpenSim.Framework.Console.MainLog.Instance.Verbose("Listening on IP end point: " + m_internalEndPoint.ToString() );
            OpenSim.Framework.Console.MainLog.Instance.Verbose("Sandbox Mode? " + isSandbox.ToString());

        }

        private string GetString(IGenericConfig configData, string attrName, string defaultvalue, string prompt)
        {
            string s = configData.GetAttribute(attrName);

            if (String.IsNullOrEmpty( s ))
            {
                s = MainLog.Instance.CmdPrompt(prompt, defaultvalue);
                configData.SetAttribute(attrName, s );
            }
            return s;
        }

        private IPAddress GetIPAddress(IGenericConfig configData, string attrName, string defaultvalue, string prompt)
        {
            string addressStr = configData.GetAttribute(attrName);

            IPAddress address;

            if (!IPAddress.TryParse(addressStr, out address))
            {
                address =  MainLog.Instance.CmdPromptIPAddress(prompt, defaultvalue);
                configData.SetAttribute(attrName, address.ToString());
            }
            return address;
        }
        
        private int GetIPPort(IGenericConfig configData, string attrName, string defaultvalue, string prompt)
        {
            string portStr = configData.GetAttribute(attrName);

            int port;

            if (!int.TryParse(portStr, out port))
            {
                port = MainLog.Instance.CmdPromptIPPort(prompt, defaultvalue);
                configData.SetAttribute(attrName, port.ToString());
            }
            
            return port;
        }
    }
}
