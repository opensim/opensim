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
using OpenSim.Framework.ServiceAuth;
using OpenSim.Services.Connectors;
using OpenSim.Services.Interfaces;
using OpenSim.Server.Base;

namespace OpenSim.Services.Connectors
{
    public class EstateDataRemoteConnector : BaseServiceConnector, IEstateDataService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;
        private ExpiringCache<string, List<EstateSettings>> m_EstateCache = new ExpiringCache<string, List<EstateSettings>>();
        private const int EXPIRATION = 5 * 60; // 5 minutes in secs

        public EstateDataRemoteConnector(IConfigSource source)
        {
            Initialise(source);
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["EstateService"];
            if (gridConfig == null)
            {
                m_log.Error("[ESTATE CONNECTOR]: EstateService missing from OpenSim.ini");
                throw new Exception("Estate connector init error");
            }

            string serviceURI = gridConfig.GetString("EstateServerURI",
                    String.Empty);

            if (serviceURI == String.Empty)
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
            string reply = string.Empty;
            string uri = m_ServerURI + "/estates";

            reply = MakeRequest("GET", uri, string.Empty);
            if (String.IsNullOrEmpty(reply))
                return new List<EstateSettings>();

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            List<EstateSettings> estates = new List<EstateSettings>();
            if (replyData != null && replyData.Count > 0)
            {
                m_log.DebugFormat("[ESTATE CONNECTOR]: LoadEstateSettingsAll returned {0} elements", replyData.Count);
                Dictionary<string, object>.ValueCollection estateData = replyData.Values;
                foreach (object r in estateData)
                {
                    if (r is Dictionary<string, object>)
                    {
                        EstateSettings es = new EstateSettings((Dictionary<string, object>)r);
                        estates.Add(es);
                    }
                }
                m_EstateCache.AddOrUpdate("estates", estates, EXPIRATION);
            }
            else
                m_log.DebugFormat("[ESTATE CONNECTOR]: LoadEstateSettingsAll from {0} received null or zero response", uri);

            return estates;

        }

        public List<int> GetEstatesAll()
        {
            List<int> eids = new List<int>();
            // If we don't have them, load them from the server
            List<EstateSettings> estates = null;
            if (!m_EstateCache.TryGetValue("estates", out estates))
                estates = LoadEstateSettingsAll();

            foreach (EstateSettings es in estates)
                eids.Add((int)es.EstateID);

            return eids;
        }

        public List<int> GetEstates(string search)
        {
            // If we don't have them, load them from the server
            List<EstateSettings> estates = null;
            if (!m_EstateCache.TryGetValue("estates", out estates))
                estates = LoadEstateSettingsAll();

            List<int> eids = new List<int>();
            foreach (EstateSettings es in estates)
                if (es.EstateName == search)
                    eids.Add((int)es.EstateID);

            return eids;
        }

        public List<int> GetEstatesByOwner(UUID ownerID)
        {
            // If we don't have them, load them from the server
            List<EstateSettings> estates = null;
            if (!m_EstateCache.TryGetValue("estates", out estates))
                estates = LoadEstateSettingsAll();

            List<int> eids = new List<int>();
            foreach (EstateSettings es in estates)
                if (es.EstateOwner == ownerID)
                    eids.Add((int)es.EstateID);

            return eids;
        }

        public List<UUID> GetRegions(int estateID)
        {
            string reply = string.Empty;
            // /estates/regions/?eid=int
            string uri = m_ServerURI + "/estates/regions/?eid=" + estateID.ToString();

            reply = MakeRequest("GET", uri, string.Empty);
            if (String.IsNullOrEmpty(reply))
                return new List<UUID>();

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            List<UUID> regions = new List<UUID>();
            if (replyData != null && replyData.Count > 0)
            {
                m_log.DebugFormat("[ESTATE CONNECTOR]: GetRegions for estate {0} returned {1} elements", estateID, replyData.Count);
                Dictionary<string, object>.ValueCollection data = replyData.Values;
                foreach (object r in data)
                {
                    UUID uuid = UUID.Zero;
                    if (UUID.TryParse(r.ToString(), out uuid))
                        regions.Add(uuid);
                }
            }
            else
                m_log.DebugFormat("[ESTATE CONNECTOR]: GetRegions from {0} received null or zero response", uri);

            return regions;
        }

        public EstateSettings LoadEstateSettings(UUID regionID, bool create)
        {
            string reply = string.Empty;
            // /estates/estate/?region=uuid&create=[t|f]
            string uri = m_ServerURI + string.Format("/estates/estate/?region={0}&create={1}", regionID, create);

            reply = MakeRequest("GET", uri, string.Empty);
            if (String.IsNullOrEmpty(reply))
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
            string reply = string.Empty;
            // /estates/estate/?eid=int
            string uri = m_ServerURI + string.Format("/estates/estate/?eid={0}", estateID);

            reply = MakeRequest("GET", uri, string.Empty);
            if (String.IsNullOrEmpty(reply))
                return null;

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            if (replyData != null && replyData.Count > 0)
            {
                m_log.DebugFormat("[ESTATE CONNECTOR]: LoadEstateSettings({0}) returned {1} elements", estateID, replyData.Count);
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
        public EstateSettings CreateNewEstate()
        {
            // No can do
            return null;
        }

        public void StoreEstateSettings(EstateSettings es)
        {
            // /estates/estate/
            string uri = m_ServerURI + ("/estates/estate");

            Dictionary<string, object> formdata = es.ToMap();
            formdata["OP"] = "STORE";

            PostRequest(uri, formdata);
        }

        public bool LinkRegion(UUID regionID, int estateID)
        {
            // /estates/estate/?eid=int&region=uuid
            string uri = m_ServerURI + String.Format("/estates/estate/?eid={0}&region={1}", estateID, regionID);

            Dictionary<string, object> formdata = new Dictionary<string, object>();
            formdata["OP"] = "LINK";
            return PostRequest(uri, formdata);
        }

        private bool PostRequest(string uri, Dictionary<string, object> sendData)
        {
            string reqString = ServerUtils.BuildQueryString(sendData);

            string reply = MakeRequest("POST", uri, reqString);
            if (String.IsNullOrEmpty(reply))
                return false;

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            bool result = false;
            if (replyData != null && replyData.Count > 0)
            {
                if (replyData.ContainsKey("Result"))
                {
                    if (Boolean.TryParse(replyData["Result"].ToString(), out result))
                        m_log.DebugFormat("[ESTATE CONNECTOR]: PostRequest {0} returned {1}", uri, result);
                }
            }
            else
                m_log.DebugFormat("[ESTATE CONNECTOR]: PostRequest {0} received null or zero response", uri);

            return result;
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
                reply = SynchronousRestFormsRequester.MakeRequest(verb, uri, formdata, m_Auth);
            }
            catch (WebException e)
            {
                using (HttpWebResponse hwr = (HttpWebResponse)e.Response)
                {
                    if (hwr != null)
                    {
                        if (hwr.StatusCode == HttpStatusCode.NotFound)
                            m_log.Error(string.Format("[ESTATE CONNECTOR]: Resource {0} not found ", uri));
                        if (hwr.StatusCode == HttpStatusCode.Unauthorized)
                            m_log.Error(string.Format("[ESTATE CONNECTOR]: Web request {0} requires authentication ", uri));
                    }
                    else
                        m_log.Error(string.Format(
                            "[ESTATE CONNECTOR]: WebException for {0} {1} {2} {3}",
                            verb, uri, formdata, e));
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[ESTATE CONNECTOR]: Exception when contacting estate server at {0}: {1}", uri, e.Message);
            }

            return reply;
        }
    }
}
