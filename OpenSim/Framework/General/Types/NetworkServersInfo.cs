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
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Configuration;

namespace OpenSim.Framework.Types
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

        public uint DefaultHomeLocX = 0;
        public uint DefaultHomeLocY = 0;

        public int HttpListenerPort = 9000;
        public int RemotingListenerPort = 8895;

        private ConfigurationMember configMember;

        public NetworkServersInfo(string description, string filename)
        {
            configMember = new ConfigurationMember(filename, description, loadConfigurationOptions, handleConfigurationItem);
            configMember.performConfigurationRetrieve();
        }

        public NetworkServersInfo( )
        {

        }

        public void loadConfigurationOptions()
        {

            configMember.addConfigurationOption("HttpListenerPort", ConfigurationOption.ConfigurationTypes.TYPE_INT32, "HTTP Listener Port", "9000", false);
            configMember.addConfigurationOption("RemotingListenerPort", ConfigurationOption.ConfigurationTypes.TYPE_INT32, "Remoting Listener Port", "8895", false);
            configMember.addConfigurationOption("DefaultLocationX", ConfigurationOption.ConfigurationTypes.TYPE_UINT32, "Default Home Location (X Axis)", "1000", false);
            configMember.addConfigurationOption("DefaultLocationY", ConfigurationOption.ConfigurationTypes.TYPE_UINT32, "Default Home Location (Y Axis)", "1000", false);

            configMember.addConfigurationOption("GridServerURL", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "Grid Server URL", "http://127.0.0.1:8001", false);
            configMember.addConfigurationOption("GridSendKey", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "Key to send to grid server", "null", false);
            configMember.addConfigurationOption("GridRecvKey", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "Key to expect from grid server", "null", false);

            configMember.addConfigurationOption("UserServerURL", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "User Server URL", "http://127.0.0.1:8002", false);
            configMember.addConfigurationOption("UserSendKey", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "Key to send to user server", "null", false);
            configMember.addConfigurationOption("UserRecvKey", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "Key to expect from user server", "null", false);

            configMember.addConfigurationOption("AssetServerURL", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "Asset Server URL", "http://127.0.0.1:8003", false);
        }

        public bool handleConfigurationItem(string configuration_key, object configuration_object)
        {
            switch (configuration_key)
            {
                case "HttpListenerPort":
                    this.HttpListenerPort = (int)configuration_object;
                    break;
                case "RemotingListenerPort":
                    this.RemotingListenerPort = (int)configuration_object;
                    break;
                case "DefaultLocationX":
                    this.DefaultHomeLocX = (uint)configuration_object;
                    break;
                case "DefaultLocationY":
                    this.DefaultHomeLocY = (uint)configuration_object;
                    break;
                case "GridServerURL":
                    this.GridURL = (string)configuration_object;
                    break;
                case "GridSendKey":
                    this.GridSendKey = (string)configuration_object;
                    break;
                case "GridRecvKey":
                    this.GridRecvKey = (string)configuration_object;
                    break;
                case "UserServerURL":
                    this.UserURL = (string)configuration_object;
                    break;
                case "UserSendKey":
                    this.UserSendKey = (string)configuration_object;
                    break;
                case "UserRecvKey":
                    this.UserRecvKey = (string)configuration_object;
                    break;
                case "AssetServerURL":
                    this.AssetURL = (string)configuration_object;
                    break;
            }

            return true;
        }
    }
}
