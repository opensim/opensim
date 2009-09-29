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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;

using log4net;
using Nwc.XmlRpc;

namespace OpenSim.Server.Handlers.Grid
{
    public class HypergridServiceInConnector : ServiceConnector
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private List<SimpleRegionInfo> m_RegionsOnSim = new List<SimpleRegionInfo>();

        public HypergridServiceInConnector(IConfigSource config, IHttpServer server) :
                base(config, server, String.Empty)
        {
            server.AddXmlRPCHandler("linkk_region", LinkRegionRequest, false);
        }

        /// <summary>
        /// Someone wants to link to us
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse LinkRegionRequest(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            string name = (string)requestData["region_name"];

            m_log.DebugFormat("[HGrid]: Hyperlink request");

            SimpleRegionInfo regInfo = null;
            foreach (SimpleRegionInfo r in m_RegionsOnSim)
            {
                if ((r.RegionName != null) && (name != null) && (r.RegionName.ToLower() == name.ToLower()))
                {
                    regInfo = r;
                    break;
                }
            }

            if (regInfo == null)
                regInfo = m_RegionsOnSim[0]; // Send out the first region

            Hashtable hash = new Hashtable();
            hash["uuid"] = regInfo.RegionID.ToString();
            hash["handle"] = regInfo.RegionHandle.ToString();
            //m_log.Debug(">> Here " + regInfo.RegionHandle);
            //hash["region_image"] = regInfo.RegionSettings.TerrainImageID.ToString();
            hash["region_name"] = regInfo.RegionName;
            hash["internal_port"] = regInfo.InternalEndPoint.Port.ToString();
            //m_log.Debug(">> Here: " + regInfo.InternalEndPoint.Port);


            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;
        }

        public void AddRegion(SimpleRegionInfo rinfo)
        {
            m_RegionsOnSim.Add(rinfo);
        }

        public void RemoveRegion(SimpleRegionInfo rinfo)
        {
            if (m_RegionsOnSim.Contains(rinfo))
                m_RegionsOnSim.Remove(rinfo);
        }
    }
}
