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
            Util.RegionHandleToWorldLoc(regionHandle, out x, out y);
            GridRegion regInfo = m_GridService.GetRegionByPosition(thisRegion.ScopeID, (int)x, (int)y);
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
//            m_log.Debug("   >>> DoHelloNeighbourCall <<< " + uri);

            WebRequest helloNeighbourRequest;

            try
            {
                helloNeighbourRequest = WebRequest.Create(uri);
            }
            catch (Exception e)
            {
                m_log.Warn(string.Format(
                    "[NEIGHBOUR SERVICES CONNECTOR]: Unable to parse uri {0} to send HelloNeighbour from {1} to {2}.  Exception {3} ",
                    uri, thisRegion.RegionName, region.RegionName, e.Message), e);

                return false;
            }

            helloNeighbourRequest.Method = "POST";
            helloNeighbourRequest.ContentType = "application/json";
            helloNeighbourRequest.Timeout = 10000;

            // Fill it in
            OSDMap args = null;
            try
            {
                args = thisRegion.PackRegionInfoData();
            }
            catch (Exception e)
            {
                m_log.Warn(string.Format(
                    "[NEIGHBOUR SERVICES CONNECTOR]: PackRegionInfoData failed for HelloNeighbour from {0} to {1}.  Exception {2} ",
                    thisRegion.RegionName, region.RegionName, e.Message), e);

                return false;
            }

            // Add the regionhandle of the destination region
            args["destination_handle"] = OSD.FromString(region.RegionHandle.ToString());

            string strBuffer = "";
            byte[] buffer = new byte[1];

            try
            {
                strBuffer = OSDParser.SerializeJsonString(args);
                buffer = Util.UTF8NoBomEncoding.GetBytes(strBuffer);
            }
            catch (Exception e)
            {
                m_log.Warn(string.Format(
                    "[NEIGHBOUR SERVICES CONNECTOR]: Exception thrown on serialization of HelloNeighbour from {0} to {1}.  Exception {2} ",
                    thisRegion.RegionName, region.RegionName, e.Message), e);

                return false;
            }

            Stream os = null;
            try
            { // send the Post
                helloNeighbourRequest.ContentLength = buffer.Length;   //Count bytes to send
                os = helloNeighbourRequest.GetRequestStream();
                os.Write(buffer, 0, strBuffer.Length);         //Send it
                //m_log.InfoFormat("[REST COMMS]: Posted HelloNeighbour request to remote sim {0}", uri);
            }
            catch (Exception e)
            {
//                m_log.WarnFormat(
//                    "[NEIGHBOUR SERVICE CONNCTOR]: Unable to send HelloNeighbour from {0} to {1}.  Exception {2}{3}",
//                    thisRegion.RegionName, region.RegionName, e.Message, e.StackTrace);

                return false;
            }
            finally
            {
                if (os != null)
                    os.Dispose();
            }

            // Let's wait for the response
            //m_log.Info("[REST COMMS]: Waiting for a reply after DoHelloNeighbourCall");

            try
            {
                using (WebResponse webResponse = helloNeighbourRequest.GetResponse())
                {
                    if (webResponse == null)
                    {
                        m_log.DebugFormat(
                            "[NEIGHBOUR SERVICES CONNECTOR]: Null reply on DoHelloNeighbourCall post from {0} to {1}",
                            thisRegion.RegionName, region.RegionName);
                    }

                    using (Stream s = webResponse.GetResponseStream())
                    {
                        using (StreamReader sr = new StreamReader(s))
                        {
                            //reply = sr.ReadToEnd().Trim();
                            sr.ReadToEnd().Trim();
                            //m_log.InfoFormat("[REST COMMS]: DoHelloNeighbourCall reply was {0} ", reply);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Warn(string.Format(
                    "[NEIGHBOUR SERVICES CONNECTOR]: Exception on reply of DoHelloNeighbourCall from {0} back to {1}.  Exception {2} ",
                    region.RegionName, thisRegion.RegionName, e.Message), e);

                return false;
            }

            return true;
        }
    }
}
