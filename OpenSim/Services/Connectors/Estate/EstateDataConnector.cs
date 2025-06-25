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
using System.Collections.Generic;
using System.Net;
using System.Reflection;

using log4net;

using OpenMetaverse;
using Nini.Config;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Server.Base;
using System.Net.Http;

namespace OpenSim.Services.Connectors
{
    public class EstateDataRemoteConnector : BaseServiceConnector, IEstateDataService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = string.Empty;
        private ExpiringCache<string, List<EstateSettings>> m_EstateCache = new ExpiringCache<string, List<EstateSettings>>();
        private const int EXPIRATION = 5 * 60; // 5 minutes in secs

        public EstateDataRemoteConnector(IConfigSource source)
        {
            Initialise(source);
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["EstateService"];
            if (gridConfig is null)
            {
                m_log.Error("[ESTATE CONNECTOR]: EstateService missing from OpenSim.ini");
                throw new Exception("Estate connector init error");
            }

            string serviceURI = gridConfig.GetString("EstateServerURI", string.Empty);
            if (serviceURI.Length == 0)
            {
                m_log.Error("[ESTATE CONNECTOR]: No Server URI named in section EstateService");
                throw new Exception("Estate connector init error");
            }
            m_ServerURI = serviceURI;

            base.Initialise(source, "EstateService");
        }

        #region IEstateDataService

        public List<EstateSettings> LoadEstateSettingsAll()
        {
            string uri = m_ServerURI + "/estates";
            string reply = MakeRequest("GET", uri, string.Empty);
            if (string.IsNullOrEmpty(reply))
                return [];

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
            if (replyData != null && replyData.Count > 0)
            {
                m_log.Debug($"[ESTATE CONNECTOR]: LoadEstateSettingsAll returned {replyData.Count} elements");
                Dictionary<string, object>.ValueCollection estateData = replyData.Values;
                List<EstateSettings> estates = [];
                foreach (object r in estateData)
                {
                    if (r is Dictionary<string, object> dr )
                    {
                        EstateSettings es = new EstateSettings(dr);
                        estates.Add(es);
                    }
                }
                m_EstateCache.AddOrUpdate("estates", estates, EXPIRATION);
                return estates;
            }
            else
                m_log.Debug($"[ESTATE CONNECTOR]: LoadEstateSettingsAll from {uri} received empty response");

            return [];
        }

        public List<int> GetEstatesAll()
        {
            // If we don't have them, load them from the server
            if (!m_EstateCache.TryGetValue("estates", out List<EstateSettings> estates))
                estates = LoadEstateSettingsAll();

            List<int> eids = [];
            foreach (EstateSettings es in estates)
                eids.Add((int)es.EstateID);

            return eids;
        }

        public List<int> GetEstates(string search)
        {
            // If we don't have them, load them from the server
            if (!m_EstateCache.TryGetValue("estates", out List<EstateSettings> estates))
                estates = LoadEstateSettingsAll();

            List<int> eids = [];
            foreach (EstateSettings es in estates)
                if (es.EstateName == search)
                    eids.Add((int)es.EstateID);

            return eids;
        }

        public List<int> GetEstatesByOwner(UUID ownerID)
        {
            // If we don't have them, load them from the server
            if (!m_EstateCache.TryGetValue("estates", out List<EstateSettings> estates))
                estates = LoadEstateSettingsAll();

            List<int> eids = [];
            foreach (EstateSettings es in estates)
                if (es.EstateOwner.Equals(ownerID))
                    eids.Add((int)es.EstateID);

            return eids;
        }

        public List<UUID> GetRegions(int estateID)
        {
            // /estates/regions/?eid=int
            string uri = m_ServerURI + "/estates/regions/?eid=" + estateID.ToString();

            string reply = MakeRequest("GET", uri, string.Empty);
            if (string.IsNullOrEmpty(reply))
                return [];

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
            if (replyData != null && replyData.Count > 0)
            {
                m_log.Debug($"[ESTATE CONNECTOR]: GetRegions for estate {estateID} returned {replyData.Count} elements");
                List<UUID> regions = [];
                Dictionary<string, object>.ValueCollection data = replyData.Values;
                foreach (object r in data)
                {
                    if (UUID.TryParse(r.ToString(), out UUID uuid))
                        regions.Add(uuid);
                }
                return regions;
            }
            else
                m_log.Debug($"[ESTATE CONNECTOR]: GetRegions from {uri} received null or zero response");
            return [];
        }

        public EstateSettings LoadEstateSettings(UUID regionID, bool create)
        {
            // /estates/estate/?region=uuid&create=[t|f]
            string uri = m_ServerURI + $"/estates/estate/?region={regionID}&create={create}";
            string reply = MakeRequest("GET", uri, string.Empty);
            if (string.IsNullOrEmpty(reply))
                return null;

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            if (replyData != null && replyData.Count > 0)
            {
                m_log.DebugFormat("[ESTATE CONNECTOR]: LoadEstateSettings({0}) returned {1} elements", regionID, replyData.Count);
                EstateSettings es = new EstateSettings(replyData);
                return es;
            }
            else
                m_log.DebugFormat("[ESTATE CONNECTOR]: LoadEstateSettings(regionID) from {0} received null or zero response", uri);

            return null;
        }

        public EstateSettings LoadEstateSettings(int estateID)
        {
            // /estates/estate/?eid=int
            string uri = m_ServerURI + $"/estates/estate/?eid={estateID}";

            string reply = MakeRequest("GET", uri, string.Empty);
            if (string.IsNullOrEmpty(reply))
                return null;

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            if (replyData != null && replyData.Count > 0)
            {
                m_log.Debug($"[ESTATE CONNECTOR]: LoadEstateSettings({estateID}) returned {replyData.Count} elements");
                EstateSettings es = new EstateSettings(replyData);
                return es;
            }
            else
                m_log.DebugFormat("[ESTATE CONNECTOR]: LoadEstateSettings(estateID) from {0} received null or zero response", uri);

            return null;
        }

        /// <summary>
        /// Forbidden operation
        /// </summary>
        /// <returns></returns>
        public EstateSettings CreateNewEstate(int estateID)
        {
            // No can do
            return null;
        }

        public void StoreEstateSettings(EstateSettings es)
        {
            // /estates/estate/
            string uri = m_ServerURI + "/estates/estate";

            Dictionary<string, object> formdata = es.ToMap();
            formdata["OP"] = "STORE";

            PostRequest(uri, formdata);
        }

        public bool LinkRegion(UUID regionID, int estateID)
        {
            // /estates/estate/?eid=int&region=uuid
            string uri = m_ServerURI + $"/estates/estate/?eid={estateID}&region={regionID}";

            Dictionary<string, object> formdata = new()
            {
                ["OP"] = "LINK"
            };
            return PostRequest(uri, formdata);
        }

        private bool PostRequest(string uri, Dictionary<string, object> sendData)
        {
            string reqString = ServerUtils.BuildQueryString(sendData);

            string reply = MakeRequest("POST", uri, reqString);
            if (string.IsNullOrEmpty(reply))
                return false;

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
            if (replyData != null && replyData.Count > 0)
            {
                if (replyData.TryGetValue("Result", out object ortmp) && ortmp is string srtmp)
                {
                    if (bool.TryParse(srtmp, out  bool result))
                    {
                        m_log.Debug($"[ESTATE CONNECTOR]: PostRequest {uri} returned {result}");
                        return result;
                    }
                }
            }
            else
                m_log.Debug($"[ESTATE CONNECTOR]: PostRequest {uri} received empty response");

            return false;
        }

        /// <summary>
        /// Forbidden operation
        /// </summary>
        /// <returns></returns>
        public bool DeleteEstate(int estateID)
        {
            return false;
        }

        #endregion

        private string MakeRequest(string verb, string uri, string formdata)
        {
            string reply = string.Empty;
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest(verb, uri, formdata, 30, m_Auth);
                return reply;
            }
            catch (HttpRequestException e)
            {
                if (e.StatusCode is HttpStatusCode status)
                {
                    if (status == HttpStatusCode.Unauthorized)
                    {
                        m_log.Error($"[ESTATE CONNECTOR]: Web request {uri} requires authentication ");
                    }
                    else if (status != HttpStatusCode.NotFound)
                    {
                        m_log.Error($"[ESTATE CONNECTOR]: Resource {uri} not found ");
                        return reply;
                    }
                }
                else
                    m_log.Error($"[ESTATE CONNECTOR]: WebException for {verb} {uri} {formdata} {e.Message}");
            }
            catch (Exception e)
            {
                m_log.DebugFormat($"[ESTATE CONNECTOR]: Exception when contacting estate server at {uri}: {e.Message}");
            }

            return null;
        }
    }
}
