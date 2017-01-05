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
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Web;
using DotNetOpenId;
using DotNetOpenId.Provider;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;
using OpenSim.Services.Interfaces;
using Nini.Config;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Authentication
{
    /// <summary>
    /// Temporary, in-memory store for OpenID associations
    /// </summary>
    public class ProviderMemoryStore : IAssociationStore<AssociationRelyingPartyType>
    {
        private class AssociationItem
        {
            public AssociationRelyingPartyType DistinguishingFactor;
            public string Handle;
            public DateTime Expires;
            public byte[] PrivateData;
        }

        Dictionary<string, AssociationItem> m_store = new Dictionary<string, AssociationItem>();
        SortedList<DateTime, AssociationItem> m_sortedStore = new SortedList<DateTime, AssociationItem>();
        object m_syncRoot = new object();

        #region IAssociationStore<AssociationRelyingPartyType> Members

        public void StoreAssociation(AssociationRelyingPartyType distinguishingFactor, Association assoc)
        {
            AssociationItem item = new AssociationItem();
            item.DistinguishingFactor = distinguishingFactor;
            item.Handle = assoc.Handle;
            item.Expires = assoc.Expires.ToLocalTime();
            item.PrivateData = assoc.SerializePrivateData();

            lock (m_syncRoot)
            {
                m_store[item.Handle] = item;
                m_sortedStore[item.Expires] = item;
            }
        }

        public Association GetAssociation(AssociationRelyingPartyType distinguishingFactor)
        {
            lock (m_syncRoot)
            {
                if (m_sortedStore.Count > 0)
                {
                    AssociationItem item = m_sortedStore.Values[m_sortedStore.Count - 1];
                    return Association.Deserialize(item.Handle, item.Expires.ToUniversalTime(), item.PrivateData);
                }
                else
                {
                    return null;
                }
            }
        }

        public Association GetAssociation(AssociationRelyingPartyType distinguishingFactor, string handle)
        {
            AssociationItem item;
            bool success = false;
            lock (m_syncRoot)
                success = m_store.TryGetValue(handle, out item);

            if (success)
                return Association.Deserialize(item.Handle, item.Expires.ToUniversalTime(), item.PrivateData);
            else
                return null;
        }

        public bool RemoveAssociation(AssociationRelyingPartyType distinguishingFactor, string handle)
        {
            lock (m_syncRoot)
            {
                for (int i = 0; i < m_sortedStore.Values.Count; i++)
                {
                    AssociationItem item = m_sortedStore.Values[i];
                    if (item.Handle == handle)
                    {
                        m_sortedStore.RemoveAt(i);
                        break;
                    }
                }

                return m_store.Remove(handle);
            }
        }

        public void ClearExpiredAssociations()
        {
            lock (m_syncRoot)
            {
                List<AssociationItem> itemsCopy = new List<AssociationItem>(m_sortedStore.Values);
                DateTime now = DateTime.Now;

                for (int i = 0; i < itemsCopy.Count; i++)
                {
                    AssociationItem item = itemsCopy[i];

                    if (item.Expires <= now)
                    {
                        m_sortedStore.RemoveAt(i);
                        m_store.Remove(item.Handle);
                    }
                }
            }
        }

        #endregion
    }

    public class OpenIdStreamHandler : BaseOutputStreamHandler, IStreamHandler
    {
        #region HTML

        /// <summary>Login form used to authenticate OpenID requests</summary>
        const string LOGIN_PAGE =
@"<html>
<head><title>OpenSim OpenID Login</title></head>
<body>
<h3>OpenSim Login</h3>
<form method=""post"">
<label for=""first"">First Name:</label> <input readonly type=""text"" name=""first"" id=""first"" value=""{0}""/>
<label for=""last"">Last Name:</label> <input readonly type=""text"" name=""last"" id=""last"" value=""{1}""/>
<label for=""pass"">Password:</label> <input type=""password"" name=""pass"" id=""pass""/>
<input type=""submit"" value=""Login"">
</form>
</body>
</html>";

        /// <summary>Page shown for a valid OpenID identity</summary>
        const string OPENID_PAGE =
@"<html>
<head>
<title>{2} {3}</title>
<link rel=""openid2.provider openid.server"" href=""{0}://{1}/openid/server/""/>
</head>
<body>OpenID identifier for {2} {3}</body>
</html>
";

        /// <summary>Page shown for an invalid OpenID identity</summary>
        const string INVALID_OPENID_PAGE =
@"<html><head><title>Identity not found</title></head>
<body>Invalid OpenID identity</body></html>";

        /// <summary>Page shown if the OpenID endpoint is requested directly</summary>
        const string ENDPOINT_PAGE =
@"<html><head><title>OpenID Endpoint</title></head><body>
This is an OpenID server endpoint, not a human-readable resource.
For more information, see <a href='http://openid.net/'>http://openid.net/</a>.
</body></html>";

        #endregion HTML

        IAuthenticationService m_authenticationService;
        IUserAccountService m_userAccountService;
        ProviderMemoryStore m_openidStore = new ProviderMemoryStore();

        public override string ContentType { get { return "text/html"; } }

        /// <summary>
        /// Constructor
        /// </summary>
        public OpenIdStreamHandler(
            string httpMethod, string path, IUserAccountService userService, IAuthenticationService authService)
            : base(httpMethod, path, "OpenId", "OpenID stream handler")
        {
            m_authenticationService = authService;
            m_userAccountService = userService;
        }

        /// <summary>
        /// Handles all GET and POST requests for OpenID identifier pages and endpoint
        /// server communication
        /// </summary>
        protected override void ProcessRequest(
            string path, Stream request, Stream response, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            Uri providerEndpoint = new Uri(String.Format("{0}://{1}{2}", httpRequest.Url.Scheme, httpRequest.Url.Authority, httpRequest.Url.AbsolutePath));

            // Defult to returning HTML content
            httpResponse.ContentType = ContentType;

            try
            {
                NameValueCollection postQuery = HttpUtility.ParseQueryString(new StreamReader(httpRequest.InputStream).ReadToEnd());
                NameValueCollection getQuery = HttpUtility.ParseQueryString(httpRequest.Url.Query);
                NameValueCollection openIdQuery = (postQuery.GetValues("openid.mode") != null ? postQuery : getQuery);

                OpenIdProvider provider = new OpenIdProvider(m_openidStore, providerEndpoint, httpRequest.Url, openIdQuery);

                if (provider.Request != null)
                {
                    if (!provider.Request.IsResponseReady && provider.Request is IAuthenticationRequest)
                    {
                        IAuthenticationRequest authRequest = (IAuthenticationRequest)provider.Request;
                        string[] passwordValues = postQuery.GetValues("pass");

                        UserAccount account;
                        if (TryGetAccount(new Uri(authRequest.ClaimedIdentifier.ToString()), out account))
                        {
                            // Check for form POST data
                            if (passwordValues != null && passwordValues.Length == 1)
                            {
                                if (account != null &&
                                    (m_authenticationService.Authenticate(account.PrincipalID,Util.Md5Hash(passwordValues[0]), 30) != string.Empty))
                                    authRequest.IsAuthenticated = true;
                                else
                                    authRequest.IsAuthenticated = false;
                            }
                            else
                            {
                                // Authentication was requested, send the client a login form
                                using (StreamWriter writer = new StreamWriter(response))
                                    writer.Write(String.Format(LOGIN_PAGE, account.FirstName, account.LastName));
                                return;
                            }
                        }
                        else
                        {
                            // Cannot find an avatar matching the claimed identifier
                            authRequest.IsAuthenticated = false;
                        }
                    }

                    // Add OpenID headers to the response
                    foreach (string key in provider.Request.Response.Headers.Keys)
                        httpResponse.AddHeader(key, provider.Request.Response.Headers[key]);

                    string[] contentTypeValues = provider.Request.Response.Headers.GetValues("Content-Type");
                    if (contentTypeValues != null && contentTypeValues.Length == 1)
                        httpResponse.ContentType = contentTypeValues[0];

                    // Set the response code and document body based on the OpenID result
                    httpResponse.StatusCode = (int)provider.Request.Response.Code;
                    response.Write(provider.Request.Response.Body, 0, provider.Request.Response.Body.Length);
                    response.Close();
                }
                else if (httpRequest.Url.AbsolutePath.Contains("/openid/server"))
                {
                    // Standard HTTP GET was made on the OpenID endpoint, send the client the default error page
                    using (StreamWriter writer = new StreamWriter(response))
                        writer.Write(ENDPOINT_PAGE);
                }
                else
                {
                    // Try and lookup this avatar
                    UserAccount account;
                    if (TryGetAccount(httpRequest.Url, out account))
                    {
                        using (StreamWriter writer = new StreamWriter(response))
                        {
                            // TODO: Print out a full profile page for this avatar
                            writer.Write(String.Format(OPENID_PAGE, httpRequest.Url.Scheme,
                                httpRequest.Url.Authority, account.FirstName, account.LastName));
                        }
                    }
                    else
                    {
                        // Couldn't parse an avatar name, or couldn't find the avatar in the user server
                        using (StreamWriter writer = new StreamWriter(response))
                            writer.Write(INVALID_OPENID_PAGE);
                    }
                }
            }
            catch (Exception ex)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                using (StreamWriter writer = new StreamWriter(response))
                    writer.Write(ex.Message);
            }
        }

        /// <summary>
        /// Parse a URL with a relative path of the form /users/First_Last and try to
        /// retrieve the profile matching that avatar name
        /// </summary>
        /// <param name="requestUrl">URL to parse for an avatar name</param>
        /// <param name="profile">Profile data for the avatar</param>
        /// <returns>True if the parse and lookup were successful, otherwise false</returns>
        bool TryGetAccount(Uri requestUrl, out UserAccount account)
        {
            if (requestUrl.Segments.Length == 3 && requestUrl.Segments[1] == "users/")
            {
                // Parse the avatar name from the path
                string username = requestUrl.Segments[requestUrl.Segments.Length - 1];
                string[] name = username.Split('_');

                if (name.Length == 2)
                {
                    account = m_userAccountService.GetUserAccount(UUID.Zero, name[0], name[1]);
                    return (account != null);
                }
            }

            account = null;
            return false;
        }
    }
}