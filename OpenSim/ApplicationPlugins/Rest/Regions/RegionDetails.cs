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
using System.Xml.Serialization;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.ApplicationPlugins.Rest.Regions
{
    [XmlRoot(ElementName="region", IsNullable = false)]
    public class RegionDetails
    {
        public string region_name;
        public string region_id;
        public uint region_x;
        public uint region_y;
        public string region_owner;
        public string region_owner_id;
        public uint region_http_port;
        public uint region_port;
        public string region_server_uri;
        public string region_external_hostname;

        public RegionDetails()
        {
        }

        public RegionDetails(RegionInfo regInfo)
        {
            region_name = regInfo.RegionName;
            region_id = regInfo.RegionID.ToString();
            region_x = regInfo.RegionLocX;
            region_y = regInfo.RegionLocY;
            region_owner_id = regInfo.EstateSettings.EstateOwner.ToString();
            region_http_port = regInfo.HttpPort;
            region_server_uri = regInfo.ServerURI;
            region_external_hostname = regInfo.ExternalHostName;

            Uri uri = new Uri(region_server_uri);
            region_port = (uint)uri.Port;
        }

        public string this[string idx]
        {
            get
            {
                switch (idx.ToLower())
                {
                case "name":
                    return region_name;
                case "id":
                    return region_id;
                case "location":
                    return String.Format("<x>{0}</x><y>{1}</y>", region_x, region_y);
                case "owner":
                    return region_owner;
                case "owner_id":
                    return region_owner_id;
                case "http_port":
                    return region_http_port.ToString();
                case "server_uri":
                    return region_server_uri;
                case "external_hostname":
                case "hostname":
                    return region_external_hostname;

                default:
                    return null;
                }
            }
        }
    }
}
