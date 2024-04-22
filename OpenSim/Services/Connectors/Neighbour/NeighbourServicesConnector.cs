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

using log4net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using Nini.Config;
using OpenSim.Framework;

using OpenSim.Services.Interfaces;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using System.Net.Http;
using System.Threading;

namespace OpenSim.Services.Connectors
{
    public class NeighbourServicesConnector : INeighbourService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        protected IGridService m_GridService = null;

        public NeighbourServicesConnector()
        {
        }

        public NeighbourServicesConnector(IGridService gridServices)
        {
            Initialise(gridServices);
        }

        public virtual void Initialise(IGridService gridServices)
        {
            m_GridService = gridServices;
        }

        public virtual GridRegion HelloNeighbour(ulong regionHandle, RegionInfo thisRegion)
        {
            GridRegion regInfo = m_GridService.GetRegionByHandle(thisRegion.ScopeID, regionHandle);
            if ((regInfo != null) &&
                // Don't remote-call this instance; that's a startup hickup
                !((regInfo.ExternalHostName == thisRegion.ExternalHostName) && (regInfo.HttpPort == thisRegion.HttpPort)))
            {
                if (!DoHelloNeighbourCall(regInfo, thisRegion))
                    return null;
            }
            else
                return null;

            return regInfo;
        }

        public bool DoHelloNeighbourCall(GridRegion region, RegionInfo thisRegion)
        {
            string uri = region.ServerURI + "region/" + thisRegion.RegionID + "/";
            //m_log.Debug("   >>> DoHelloNeighbourCall <<< " + uri);

            byte[] buffer;
            try
            {
                OSDMap args = thisRegion.PackRegionInfoData();
                args["destination_handle"] = OSD.FromString(region.RegionHandle.ToString());
                buffer = OSDParser.SerializeJsonToBytes(args);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[NEIGHBOUR SERVICES CONNECTOR]: PackRegionInfoData failed for HelloNeighbour from {0} to {1}.  Exception: {2} ",
                    thisRegion.RegionName, region.RegionName, e.Message);
                return false;
            }

            if(buffer is null || buffer.Length == 0)
                return false;

            HttpResponseMessage responseMessage = null;
            HttpRequestMessage request = null;
            HttpClient client = null;
            try
            {
                client = WebUtil.GetNewGlobalHttpClient(10000);
                request = new(HttpMethod.Post, uri);
                request.Headers.ExpectContinue = false;
                request.Headers.TransferEncodingChunked = false;
                //if (keepalive)
                {
                    request.Headers.TryAddWithoutValidation("Keep-Alive", "timeout=30, max=10");
                    request.Headers.TryAddWithoutValidation("Connection", "Keep-Alive");
                }
                //else
                //    request.Headers.TryAddWithoutValidation("Connection", "close");

                request.Content = new ByteArrayContent(buffer);
                request.Content.Headers.TryAddWithoutValidation("Content-Type", "application/json");
                request.Content.Headers.TryAddWithoutValidation("Content-Length", buffer.Length.ToString());

                //m_log.InfoFormat("[REST COMMS]: Posted HelloNeighbour request to remote sim {0}", uri);

                responseMessage = client.Send(request, HttpCompletionOption.ResponseContentRead);
                responseMessage.EnsureSuccessStatusCode();

                //using StreamReader sr = new(responseMessage.Content.ReadAsStream());
                //sr.ReadToEnd(); // just try to read
                //string reply = sr.ReadToEnd();
                //m_log.InfoFormat("[REST COMMS]: DoHelloNeighbourCall reply was {0} ", reply);
                return true;
            }
            catch (Exception e)
            {
                m_log.WarnFormat(
                    "[NEIGHBOUR SERVICES CONNECTOR]: Exception on DoHelloNeighbourCall from {0} back to {1}.  Exception: {2} ",
                    region.RegionName, thisRegion.RegionName, e.Message);
            }
            finally
            {
                request?.Dispose();
                responseMessage?.Dispose();
                client?.Dispose();
            }
            return false;
        }
    }
}
