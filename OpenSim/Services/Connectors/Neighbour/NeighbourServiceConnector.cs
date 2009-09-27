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
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

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
            uint x = 0, y = 0;
            Utils.LongToUInts(regionHandle, out x, out y);
            GridRegion regInfo = m_GridService.GetRegionByPosition(UUID.Zero, (int)x, (int)y);
            if ((regInfo != null) &&
                // Don't remote-call this instance; that's a startup hickup
                !((regInfo.ExternalHostName == thisRegion.ExternalHostName) && (regInfo.HttpPort == thisRegion.HttpPort)))
            {
                DoHelloNeighbourCall(regInfo, thisRegion);
            }
            //else
            //    m_log.Warn("[REST COMMS]: Region not found " + regionHandle);
            return regInfo;
        }

        public bool DoHelloNeighbourCall(GridRegion region, RegionInfo thisRegion)
        {
            string uri = "http://" + region.ExternalEndPoint.Address + ":" + region.HttpPort + "/region/" + thisRegion.RegionID + "/";
            //m_log.Debug("   >>> DoHelloNeighbourCall <<< " + uri);

            WebRequest HelloNeighbourRequest = WebRequest.Create(uri);
            HelloNeighbourRequest.Method = "POST";
            HelloNeighbourRequest.ContentType = "application/json";
            HelloNeighbourRequest.Timeout = 10000;

            // Fill it in
            OSDMap args = null;
            try
            {
                args = thisRegion.PackRegionInfoData();
            }
            catch (Exception e)
            {
                m_log.Debug("[REST COMMS]: PackRegionInfoData failed with exception: " + e.Message);
            }
            // Add the regionhandle of the destination region
            args["destination_handle"] = OSD.FromString(region.RegionHandle.ToString());

            string strBuffer = "";
            byte[] buffer = new byte[1];
            try
            {
                strBuffer = OSDParser.SerializeJsonString(args);
                UTF8Encoding str = new UTF8Encoding();
                buffer = str.GetBytes(strBuffer);

            }
            catch (Exception e)
            {
                m_log.WarnFormat("[REST COMMS]: Exception thrown on serialization of HelloNeighbour: {0}", e.Message);
                // ignore. buffer will be empty, caller should check.
            }

            Stream os = null;
            try
            { // send the Post
                HelloNeighbourRequest.ContentLength = buffer.Length;   //Count bytes to send
                os = HelloNeighbourRequest.GetRequestStream();
                os.Write(buffer, 0, strBuffer.Length);         //Send it
                os.Close();
                //m_log.InfoFormat("[REST COMMS]: Posted HelloNeighbour request to remote sim {0}", uri);
            }
            //catch (WebException ex)
            catch
            {
                //m_log.InfoFormat("[REST COMMS]: Bad send on HelloNeighbour {0}", ex.Message);

                return false;
            }

            // Let's wait for the response
            //m_log.Info("[REST COMMS]: Waiting for a reply after DoHelloNeighbourCall");

            try
            {
                WebResponse webResponse = HelloNeighbourRequest.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on DoHelloNeighbourCall post");
                }

                StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                sr.Close();
                //m_log.InfoFormat("[REST COMMS]: DoHelloNeighbourCall reply was {0} ", reply);

            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of DoHelloNeighbourCall {0}", ex.Message);
                // ignore, really
            }

            return true;

        }

    }
}
