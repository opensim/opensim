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
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

using OpenMetaverse;

namespace OpenSim.ApplicationPlugins.Rest.Inventory
{

    /// <summary>
    /// This class represents the current REST request. It
    /// encapsulates the request/response state and takes care
    /// of response generation without exposing the REST handler
    /// to the actual mechanisms involved.
    ///
    /// This structure is created on entry to the Handler
    /// method and is disposed of upon return. It is part of
    /// the plug-in infrastructure, rather than the functionally
    /// specific REST handler, and fundamental changes to
    /// this should be reflected in the Rest HandlerVersion. The
    /// object is instantiated, and may be extended by, any
    /// given handler. See the inventory handler for an example
    /// of this.
    ///
    /// If possible, the underlying request/response state is not
    /// changed until the handler explicitly issues a Respond call.
    /// This ensures that the request/response pair can be safely
    /// processed by subsequent, unrelated, handlers even id the
    /// agent handler had completed much of its processing. Think
    /// of it as a transactional req/resp capability.
    /// </summary>

    public class RequestData
    {

        // HTTP Server interface data (Received values)

        internal OSHttpRequest        request = null;
        internal OSHttpResponse       response = null;
        internal string               qprefix = null;

        // Request lifetime values
        // buffer is global because it is referenced by the handler
        // in supported of streamed requests.
        // If a service provider wants to construct the message
        // body explicitly it can use body to do this. The value
        // in body is used if the buffer is still null when a response
        // is generated.
        // Storing information in body will suppress the return of
        // statusBody which is only intended to report status on
        // requests which do not themselves ordinarily generate
        // an informational response. All of this is handled in
        // Respond().

        internal byte[]               buffer = null;
        internal string               body   = null;
        internal string               bodyType = "text/html";

        // The encoding in effect is set to a server default. It may
        // subsequently be overridden by a Content header. This
        // value is established during construction and is used
        // wherever encoding services are needed.

        internal Encoding             encoding = Rest.Encoding;

        // These values are derived from the supplied URL. They
        // are initialized during construction.

        internal string               path = null;
        internal string               method = null;
        internal Uri                  uri = null;
        internal string               query = null;
        internal string               hostname = "localhost";
        internal int                  port = 80;

        // The path part of the URI is decomposed. pathNodes
        // is an array of every element in the URI. Parameters
        // is an array that contains only those nodes that
        // are not a part of the authority prefix

        private string[]              pathNodes = null;
        private string[]              parameters = null;
        private static readonly string[] EmptyPath = { String.Empty };

        // The status code gets set during the course of processing
        // and is the HTTP completion code. The status body is
        // initialized during construction, is appended to during the
        // course of execution, and is finalized during Respond
        // processing.
        //
        // Fail processing marks the request as failed and this is
        // then used to inhibit processing during Response processing.

        internal int                  statusCode = 0;
        internal string               statusBody = String.Empty;
        internal bool                 fail = false;

        // This carries the URL to which the client should be redirected.
        // It is set by the service provider using the Redirect call.

        internal string               redirectLocation = null;

        // These values influence response processing. They can be set by
        // service providers according to need. The defaults are generally
        // good.

        internal bool                 keepAlive = false;
        internal bool                 chunked = false;

        // XML related state

        internal XmlWriter            writer = null;
        internal XmlReader            reader = null;

        // Internal working state

        private StringBuilder         sbuilder = new StringBuilder(1024);
        private MemoryStream          xmldata = null;

        // This is used to make the response mechanism idempotent.

        internal bool                 handled = false;

        // Authentication related state
        //
        // Two supported authentication mechanisms are:
        // scheme = Rest.AS_BASIC;
        // scheme = Rest.AS_DIGEST;
        // Presented in that order (as required by spec)
        // A service provider can set the scheme variable to
        // force selection of a particular authentication model
        // (choosing from amongst those supported of course)
        //

        internal bool                 authenticated = false;
        internal string               scheme = Rest.Scheme;
        internal string               realm = Rest.Realm;
        internal string               domain = null;
        internal string               nonce = null;
        internal string               cnonce = null;
        internal string               qop = Rest.Qop_Auth;
        internal string               opaque = null;
        internal string               stale = null;
        internal string               algorithm = Rest.Digest_MD5;
        internal string               authParms  = null;
        internal string               authPrefix = null;
        internal string               userName  = String.Empty;
        internal string               userPass  = String.Empty;

        // Session related tables. These are only needed if QOP is set to "auth-sess"
        // and for now at least, it is not. Session related authentication is of
        // questionable merit in the context of REST anyway, but it is, arguably, more
        // secure.

        private static Dictionary<string,string>  cntable   = new Dictionary<string,string>();
        private static Dictionary<string,string>  sktable   = new Dictionary<string,string>();

        // This dictionary is used to keep track fo all of the parameters discovered
        // when the authorisation header is anaylsed.

        private        Dictionary<string,string>  authparms = new Dictionary<string,string>();

        // These regular expressions are used to decipher the various header entries.

        private static Regex schema      = new Regex("^\\s*(?<scheme>\\w+)\\s*.*",
                                                     RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static Regex basicParms  = new Regex("^\\s*(?:\\w+)\\s+(?<pval>\\S+)\\s*",
                                                     RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static Regex digestParm1 = new Regex("\\s*(?<parm>\\w+)\\s*=\\s*\"(?<pval>[^\"]+)\"",
                                                     RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static Regex digestParm2 = new Regex("\\s*(?<parm>\\w+)\\s*=\\s*(?<pval>[^\\p{P}\\s]+)",
                                                     RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static Regex reuserPass  = new Regex("(?<user>[^:]+):(?<pass>[\\S\\s]*)",
                                                     RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // For efficiency, we create static instances of these objects

        private static MD5   md5hash     = MD5.Create();

        private static StringComparer sc = StringComparer.OrdinalIgnoreCase;

#region properties

        // Just for convenience...

        internal string MsgId
        {
            get { return Rest.MsgId; }
        }

        /// <summary>
        /// Return a boolean indication of whether or no an authenticated user is
        /// associated with this request. This could be wholly integrated, but
        /// that would make authentication mandatory.
        /// </summary>

        internal bool IsAuthenticated
        {
            get
            {
                if (Rest.Authenticate)
                {
                    if (!authenticated)
                    {
                        authenticate();
                    }

                    return authenticated;
                }
                else return true;
            }
        }

        /// <summary>
        /// Access to all 'nodes' in the supplied URI as an
        /// array of strings.
        /// </summary>

        internal string[] PathNodes
        {
            get
            {
                return pathNodes;
            }
        }

        /// <summary>
        /// Access to all non-prefix 'nodes' in the supplied URI as an
        /// array of strings. These identify a specific resource that
        /// is managed by the authority (the prefix).
        /// </summary>

        internal string[] Parameters
        {
            get
            {
                return parameters;
            }
        }

#endregion properties

#region constructors

        // Constructor

        internal RequestData(OSHttpRequest p_request, OSHttpResponse p_response, string p_qprefix)
        {

            request  = p_request;
            response = p_response;
            qprefix  = p_qprefix;

            sbuilder.Length = 0;

            encoding = request.ContentEncoding;

            if (encoding == null)
            {
                encoding = Rest.Encoding;
            }

            method = request.HttpMethod.ToLower();
            initUrl();

            initParameters(p_qprefix.Length);

        }

#endregion constructors

#region authentication_common

        /// <summary>
        /// The REST handler has requested authentication. Authentication
        /// is considered to be with respect to the current values for
        /// Realm, domain, etc.
        ///
        /// This method checks to see if the current request is already
        /// authenticated for this domain. If it is, then it returns
        /// true. If it is not, then it issues a challenge to the client
        /// and responds negatively to the request.
        ///
        /// As soon as authentication failure is detected the method calls
        /// DoChallenge() which terminates the request with REST exception
        /// for unauthroized access.
        /// </summary>

        private void authenticate()
        {

            string authdata  = request.Headers.Get("Authorization");
            string reqscheme = String.Empty;

            // If we don't have an authorization header, then this
            // user is certainly not authorized. This is the typical
            // pivot for the 1st request by a client.

            if (authdata == null)
            {
                Rest.Log.DebugFormat("{0} Challenge reason: No authorization data", MsgId);
                DoChallenge();
            }

            // So, we have authentication data, now we have to check to
            // see what we got and whether or not it is valid for the
            // current domain. To do this we need to interpret the data
            // provided in the Authorization header. First we need to
            // identify the scheme being used and route accordingly.

            MatchCollection matches = schema.Matches(authdata);

            foreach (Match m in matches)
            {
                Rest.Log.DebugFormat("{0} Scheme matched : {1}", MsgId, m.Groups["scheme"].Value);
                reqscheme = m.Groups["scheme"].Value.ToLower();
            }

            // If we want a specific authentication mechanism, make sure
            // we get it. null indicates we don't care. non-null indicates
            // a specific scheme requirement.

            if (scheme != null && scheme.ToLower() != reqscheme)
            {
                Rest.Log.DebugFormat("{0} Challenge reason: Requested scheme not acceptable", MsgId);
                DoChallenge();
            }

            // In the future, these could be made into plug-ins...
            // But for now at least we have no reason to use anything other
            // then MD5. TLS/SSL are taken care of elsewhere.

            switch (reqscheme)
            {
                case "digest" :
                    Rest.Log.DebugFormat("{0} Digest authentication offered", MsgId);
                    DoDigest(authdata);
                    break;

                case "basic" :
                    Rest.Log.DebugFormat("{0} Basic authentication offered", MsgId);
                    DoBasic(authdata);
                    break;
            }

            // If the current header is invalid, then a challenge is still needed.

            if (!authenticated)
            {
                Rest.Log.DebugFormat("{0} Challenge reason: Authentication failed", MsgId);
                DoChallenge();
            }

        }

        /// <summary>
        /// Construct the necessary WWW-Authenticate headers and fail the request
        /// with a NOT AUTHORIZED response. The parameters are the union of values
        /// required by the supported schemes.
        /// </summary>

        private void DoChallenge()
        {
            Flush();
            nonce = Rest.NonceGenerator(); // should be unique per 401 (and it is)
            Challenge(scheme, realm, domain, nonce, opaque, stale, algorithm, qop, authParms);
            Fail(Rest.HttpStatusCodeNotAuthorized);
        }

        /// <summary>
        /// The Flush() call is here to support a problem encountered with the
        /// client where an authentication rejection was lost because the rejection
        /// may flow before the clienthas finished sending us the inbound data stream,
        /// in which case the client responds to the socket error on out put, and
        /// never sees the authentication challenge. The client should be fixed,
        /// because this solution leaves the server prone to DOS attacks. A message
        /// will be issued whenever flushing occurs. It can be enabled/disabled from
        /// the configuration file.
        /// </summary>

        private void Flush()
        {
            if (Rest.FlushEnabled)
            {
                byte[] dbuffer = new byte[8192];
                Rest.Log.WarnFormat("{0} REST server is flushing the inbound data stream", MsgId);
                while (request.InputStream.Read(dbuffer,0,dbuffer.Length) != 0);
            }
            return;
        }

        // Indicate that authentication is required

        private void Challenge(string scheme, string realm, string domain, string nonce,
                                string opaque, string stale, string alg,
                                string qop, string auth)
        {

            sbuilder.Length = 0;

            // The service provider can force a particular scheme by
            // assigning a value to scheme.

            // Basic authentication is pretty simple.
            // Just specify the realm in question.

            if (scheme == null || scheme == Rest.AS_BASIC)
            {

                sbuilder.Append(Rest.AS_BASIC);

                if (realm != null)
                {
                    sbuilder.Append(" realm=");
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(realm);
                    sbuilder.Append(Rest.CS_DQUOTE);
                }
                AddHeader(Rest.HttpHeaderWWWAuthenticate,sbuilder.ToString());
            }

            sbuilder.Length = 0;

            // Digest authentication takes somewhat more
            // to express.

            if (scheme == null || scheme == Rest.AS_DIGEST)
            {

                sbuilder.Append(Rest.AS_DIGEST);
                sbuilder.Append(" ");

                // Specify the effective realm. This should
                // never be null if we are uthenticating, as it is required for all
                // authentication schemes. It defines, in conjunction with the
                // absolute URI information, the domain to which the authentication
                // applies. It is an arbitrary string. I *believe* this allows an
                // authentication to apply to disjoint resources within the same
                // server.

                if (realm != null)
                {
                    sbuilder.Append("realm=");
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(realm);
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(Rest.CS_COMMA);
                }

                // Share our nonce. This is *uniquely* generated each time a 401 is
                // returned. We do not generate a very sophisticated nonce at the
                // moment (it's simply a base64 encoded UUID).

                if (nonce != null)
                {
                    sbuilder.Append("nonce=");
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(nonce);
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(Rest.CS_COMMA);
                }

                // The opaque string should be returned by the client unchanged in all
                // subsequent requests.

                if (opaque != null)
                {
                    sbuilder.Append("opaque=");
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(opaque);
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(Rest.CS_COMMA);
                }

                // This flag indicates that the authentication was rejected because the
                // included nonce was stale. The server might use timestamp information
                // in the nonce to determine this. We do not.

                if (stale != null)
                {
                    sbuilder.Append("stale=");
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(stale);
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(Rest.CS_COMMA);
                }

                // Identifies the algorithm used to produce the digest and checksum.
                // The default is MD5.

                if (alg != null)
                {
                    sbuilder.Append("algorithm=");
                    sbuilder.Append(alg);
                    sbuilder.Append(Rest.CS_COMMA);
                }

                // Theoretically QOP is optional, but it is required by a compliant
                // with current versions of the scheme. In fact IE requires that QOP
                // be specified and will refuse to authenticate otherwise.

                if (qop != String.Empty)
                {
                    sbuilder.Append("qop=");
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(qop);
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(Rest.CS_COMMA);
                }

                // This parameter allows for arbitrary extensions to the protocol.
                // Unrecognized values should be simply ignored.

                if (auth != null)
                {
                    sbuilder.Append(auth);
                    sbuilder.Append(Rest.CS_COMMA);
                }

                // We don't know the userid that will be used
                // so we cannot make any authentication domain
                // assumptions. So the prefix will determine
                // this.

                sbuilder.Append("domain=");
                sbuilder.Append(Rest.CS_DQUOTE);
                sbuilder.Append(qprefix);
                sbuilder.Append(Rest.CS_DQUOTE);

                // Generate the authenticate header and we're basically
                // done.

                AddHeader(Rest.HttpHeaderWWWAuthenticate,sbuilder.ToString());

            }

        }

#endregion authentication_common

#region authentication_basic

        /// <summary>
        /// Interpret a BASIC authorization claim. Some clients can only
        /// understand this and also expect it to be the first one
        /// offered. So we do.
        /// OpenSim also needs this, as it is the only scheme that allows
        /// authentication using the hashed passwords stored in the
        /// user database.
        /// </summary>

        private void DoBasic(string authdata)
        {

            string response = null;

            MatchCollection matches = basicParms.Matches(authdata);

            // In the case of basic authentication there is
            // only expected to be a single argument.

            foreach (Match m in matches)
            {
                authparms.Add("response",m.Groups["pval"].Value);
                Rest.Log.DebugFormat("{0} Parameter matched : {1} = {2}",
                                     MsgId, "response", m.Groups["pval"].Value);
            }

            // Did we get a valid response?

            if (authparms.TryGetValue("response", out response))
            {
                // Decode
                response = Rest.Base64ToString(response);
                Rest.Log.DebugFormat("{0} Auth response is: <{1}>", MsgId, response);

                // Extract user & password
                Match m = reuserPass.Match(response);
                userName = m.Groups["user"].Value;
                userPass = m.Groups["pass"].Value;

                // Validate against user database
                authenticated = Validate(userName,userPass);
            }

        }

        /// <summary>
        /// This method provides validation in support of the BASIC
        /// authentication method. This is not normaly expected to be
        /// used, but is included for completeness (and because I tried
        /// it first).
        /// </summary>

        private bool Validate(string user, string pass)
        {

            Rest.Log.DebugFormat("{0} Simple User Validation", MsgId);

            // Both values are required

            if (user == null || pass == null)
                return false;

            // Eliminate any leading or trailing spaces
            user = user.Trim();

            return vetPassword(user, pass);

        }

        /// <summary>
        /// This is used by the BASIC authentication scheme to calculate
        /// the double hash used by OpenSim to encode user's passwords.
        /// It returns true, if the supplied password is actually correct.
        /// If the specified user-id is not recognized, but the password
        /// matches the God password, then this is accepted as an admin
        /// session.
        /// </summary>

        private bool vetPassword(string user, string pass)
        {

            int x;
            string first;
            string last;

            // Distinguish the parts, if necessary

            if ((x=user.IndexOf(Rest.C_SPACE)) != -1)
            {
                first = user.Substring(0,x);
                last  = user.Substring(x+1);
            }
            else
            {
                first = user;
                last  = String.Empty;
            }

            UserAccount account = Rest.UserServices.GetUserAccount(UUID.Zero, first, last);

            // If we don't recognize the user id, perhaps it is god?
            if (account == null)
                return pass == Rest.GodKey;

            return (Rest.AuthServices.Authenticate(account.PrincipalID, pass, 1) != string.Empty);

        }

#endregion authentication_basic

#region authentication_digest

        /// <summary>
        /// This is an RFC2617 compliant HTTP MD5 Digest authentication
        /// implementation. It has been tested with Firefox, Java HTTP client,
        /// and Microsoft's Internet Explorer V7.
        /// </summary>

        private void DoDigest(string authdata)
        {

            string response = null;

            // Find all of the values of the for x = "y"

            MatchCollection matches = digestParm1.Matches(authdata);

            foreach (Match m in matches)
            {
                authparms.Add(m.Groups["parm"].Value,m.Groups["pval"].Value);
                Rest.Log.DebugFormat("{0} String Parameter matched : {1} = {2}",
                                     MsgId, m.Groups["parm"].Value,m.Groups["pval"].Value);
            }

            // Find all of the values of the for x = y

            matches = digestParm2.Matches(authdata);

            foreach (Match m in matches)
            {
                authparms.Add(m.Groups["parm"].Value,m.Groups["pval"].Value);
                Rest.Log.DebugFormat("{0} Tokenized Parameter matched : {1} = {2}",
                                     MsgId, m.Groups["parm"].Value,m.Groups["pval"].Value);
            }

            // A response string MUST be returned, otherwise we are
            // NOT authenticated.

            Rest.Log.DebugFormat("{0} Validating authorization parameters", MsgId);

            if (authparms.TryGetValue("response", out response))
            {

                string temp   = null;

                do
                {

                    string nck = null;
                    string ncl = null;

                    // The userid is sent in clear text. Needed for the
                    // verification.

                    authparms.TryGetValue("username", out userName);

                    // All URI's of which this is a prefix are
                    // optimistically considered to be authenticated by the
                    // client. This is also needed to verify the response.

                    authparms.TryGetValue("uri", out authPrefix);

                    // There MUST be a nonce string present. We're not preserving any server
                    // side state and we can't validate the MD5 unless the client returns it
                    // to us, as it should.

                    if (!authparms.TryGetValue("nonce", out nonce) || nonce == null)
                    {
                        Rest.Log.WarnFormat("{0} Authentication failed: nonce missing", MsgId);
                        break;
                    }

                    // If there is an opaque string present, it had better
                    // match what we sent.

                    if (authparms.TryGetValue("opaque", out temp))
                    {
                        if (temp != opaque)
                        {
                            Rest.Log.WarnFormat("{0} Authentication failed: bad opaque value", MsgId);
                            break;
                        }
                    }

                    // If an algorithm string is present, it had better
                    // match what we sent.

                    if (authparms.TryGetValue("algorithm", out temp))
                    {
                        if (temp != algorithm)
                        {
                            Rest.Log.WarnFormat("{0} Authentication failed: bad algorithm value", MsgId);
                            break;
                        }
                    }

                    // Quality of protection considerations...

                    if (authparms.TryGetValue("qop", out temp))
                    {

                        qop = temp.ToLower(); // replace with actual value used

                        // if QOP was specified then
                        // these MUST be present.

                        if (!authparms.ContainsKey("cnonce"))
                        {
                            Rest.Log.WarnFormat("{0} Authentication failed: cnonce missing", MsgId);
                            Fail(Rest.HttpStatusCodeBadRequest);
                            break;
                        }

                        cnonce = authparms["cnonce"];

                        if (!authparms.TryGetValue("nc", out nck) || nck == null)
                        {
                            Rest.Log.WarnFormat("{0} Authentication failed: cnonce counter missing", MsgId);
                            Fail(Rest.HttpStatusCodeBadRequest);
                            break;
                        }

                        Rest.Log.DebugFormat("{0} Comparing nonce indices", MsgId);

                        if (cntable.TryGetValue(nonce, out ncl))
                        {
                            Rest.Log.DebugFormat("{0} nonce values: Verify that request({1}) > Reference({2})", MsgId, nck, ncl);

                            if (Rest.Hex2Int(ncl) >= Rest.Hex2Int(nck))
                            {
                                Rest.Log.WarnFormat("{0} Authentication failed: bad cnonce counter", MsgId);
                                Fail(Rest.HttpStatusCodeBadRequest);
                                break;
                            }
                            cntable[nonce] = nck;
                        }
                        else
                        {
                            lock (cntable) cntable.Add(nonce, nck);
                        }

                    }
                    else
                    {

                        qop = String.Empty;

                        // if QOP was not specified then
                        // these MUST NOT be present.
                        if (authparms.ContainsKey("cnonce"))
                        {
                            Rest.Log.WarnFormat("{0} Authentication failed: invalid cnonce", MsgId);
                            Fail(Rest.HttpStatusCodeBadRequest);
                            break;
                        }
                        if (authparms.ContainsKey("nc"))
                        {
                            Rest.Log.WarnFormat("{0} Authentication failed: invalid cnonce counter[2]", MsgId);
                            Fail(Rest.HttpStatusCodeBadRequest);
                            break;
                        }
                    }

                    // Validate the supplied userid/password info

                    authenticated = ValidateDigest(userName, nonce, cnonce, nck, authPrefix, response);

                }
                while (false);

            }
            else
                Fail(Rest.HttpStatusCodeBadRequest);

        }

        /// <summary>
        /// This mechanism is used by the digest authentication mechanism
        /// to return the user's password. In fact, because the OpenSim
        /// user's passwords are already hashed, and the HTTP mechanism
        /// does not supply an open password, the hashed passwords cannot
        /// be used unless the client has used the same salting mechanism
        /// to has the password before using it in the authentication
        /// algorithn. This is not inconceivable...
        /// </summary>

        private string getPassword(string user)
        {

            int x;
            string first;
            string last;

            // Distinguish the parts, if necessary

            if ((x=user.IndexOf(Rest.C_SPACE)) != -1)
            {
                first = user.Substring(0,x);
                last  = user.Substring(x+1);
            }
            else
            {
                first = user;
                last  = String.Empty;
            }

            UserAccount account = Rest.UserServices.GetUserAccount(UUID.Zero, first, last);
            // If we don;t recognize the user id, perhaps it is god?

            if (account == null)
            {
                Rest.Log.DebugFormat("{0} Administrator", MsgId);
                return Rest.GodKey;
            }
            else
            {
                Rest.Log.DebugFormat("{0} Normal User {1}", MsgId, user);

                // !!! REFACTORING PROBLEM
                // This is what it was. It doesn't work in 0.7
                // Nothing retrieves the password from the authentication service, there's only authentication.
                //return udata.PasswordHash;
                return string.Empty;
            }

        }

        // Validate the request-digest

        private bool ValidateDigest(string user, string nonce, string cnonce, string nck, string uri, string response)
        {

            string patt = null;
            string payl = String.Empty;
            string KDS  = null;
            string HA1  = null;
            string HA2  = null;
            string pass = getPassword(user);

            // Generate H(A1)

            if (algorithm == Rest.Digest_MD5Sess)
            {
                if (!sktable.ContainsKey(cnonce))
                {
                    patt = String.Format("{0}:{1}:{2}:{3}:{4}", user, realm, pass, nonce, cnonce);
                    HA1  = HashToString(patt);
                    sktable.Add(cnonce, HA1);
                }
                else
                {
                    HA1  = sktable[cnonce];
                }
            }
            else
            {
                patt = String.Format("{0}:{1}:{2}", user, realm, pass);
                HA1  = HashToString(patt);
            }

            // Generate H(A2)

            if (qop == "auth-int")
            {
                patt = String.Format("{0}:{1}:{2}", request.HttpMethod, uri, HashToString(payl));
            }
            else
            {
                patt = String.Format("{0}:{1}", request.HttpMethod, uri);
            }

            HA2  = HashToString(patt);

            // Generate Digest

            if (qop != String.Empty)
            {
                patt = String.Format("{0}:{1}:{2}:{3}:{4}:{5}", HA1, nonce, nck, cnonce, qop, HA2);
            }
            else
            {
                patt = String.Format("{0}:{1}:{2}", HA1, nonce, HA2);
            }

            KDS = HashToString(patt);

            // Compare the generated sequence with the original

            return (0 == sc.Compare(KDS, response));

        }

        private string HashToString(string pattern)
        {

            Rest.Log.DebugFormat("{0} Generate <{1}>", MsgId, pattern);

            byte[] hash = md5hash.ComputeHash(encoding.GetBytes(pattern));

            sbuilder.Length = 0;

            for (int i = 0; i < hash.Length; i++)
            {
                sbuilder.Append(hash[i].ToString("x2"));
            }

            Rest.Log.DebugFormat("{0} Hash = <{1}>", MsgId, sbuilder.ToString());

            return sbuilder.ToString();

        }

#endregion authentication_digest

#region service_interface

        /// <summary>
        /// Conditionally set a normal completion code. This allows a normal
        /// execution path to default.
        /// </summary>

        internal void Complete()
        {
            if (statusCode == 0)
            {
                statusCode = Rest.HttpStatusCodeOK;
            }
        }

        /// <summary>
        /// Indicate a functionally-dependent conclusion to the
        /// request. See Rest.cs for a list of possible values.
        /// </summary>

        internal void Complete(int code)
        {
            statusCode = code;
        }

        /// <summary>
        /// Indicate that a request should be redirected, using
        /// the HTTP completion codes. Permanent and temporary
        /// redirections may be indicated. The supplied URL is
        /// the new location of the resource.
        /// </summary>

        internal void Redirect(string Url, bool temp)
        {

            redirectLocation = Url;

            if (temp)
            {
                statusCode = Rest.HttpStatusCodeTemporaryRedirect;
            }
            else
            {
                statusCode = Rest.HttpStatusCodePermanentRedirect;
            }

            Fail(statusCode, String.Empty, true);

        }

        /// <summary>
        /// Fail for an arbitrary reason. Just a failure with
        /// headers. The supplied message will be returned in the
        /// message body.
        /// </summary>

        internal void Fail(int code)
        {
            Fail(code, String.Empty, false);
        }

        /// <summary>
        /// For the more adventurous. This failure also includes a
        /// specified entity to be appended to the code-related
        /// status string.
        /// </summary>

        internal void Fail(int code, string addendum)
        {
            Fail(code, addendum, false);
        }

        internal void Fail(int code, string addendum, bool reset)
        {

            statusCode        = code;
            appendStatus(String.Format("({0}) : {1}", code, Rest.HttpStatusDesc[code]));

            // Add any final addendum to the status information

            if (addendum != String.Empty)
            {
                appendStatus(String.Format(addendum));
            }

            // Help us understand why the request is being rejected

            if (Rest.DEBUG)
            {
                Rest.Log.DebugFormat("{0} Request Failure State Dump", MsgId);
                Rest.Log.DebugFormat("{0}     Scheme = {1}", MsgId, scheme);
                Rest.Log.DebugFormat("{0}      Realm = {1}", MsgId, realm);
                Rest.Log.DebugFormat("{0}     Domain = {1}", MsgId, domain);
                Rest.Log.DebugFormat("{0}      Nonce = {1}", MsgId, nonce);
                Rest.Log.DebugFormat("{0}     CNonce = {1}", MsgId, cnonce);
                Rest.Log.DebugFormat("{0}     Opaque = {1}", MsgId, opaque);
                Rest.Log.DebugFormat("{0}      Stale = {1}", MsgId, stale);
                Rest.Log.DebugFormat("{0}  Algorithm = {1}", MsgId, algorithm);
                Rest.Log.DebugFormat("{0}        QOP = {1}", MsgId, qop);
                Rest.Log.DebugFormat("{0} AuthPrefix = {1}", MsgId, authPrefix);
                Rest.Log.DebugFormat("{0}   UserName = {1}", MsgId, userName);
                Rest.Log.DebugFormat("{0}   UserPass = {1}", MsgId, userPass);
            }

            fail = true;

            // Respond to the client's request, tag the response (for the
            // benefit of trace) to indicate the reason.

            Respond(String.Format("Failure response: ({0}) : {1} ",
                            code, Rest.HttpStatusDesc[code]));

            // Finally initialize and the throw a RestException. All of the
            // handler's infrastructure knows that this is a "normal"
            // completion from a code point-of-view.

            RestException re = new RestException(Rest.HttpStatusDesc[code]+" <"+code+">");

            re.statusCode = code;
            re.statusDesc = Rest.HttpStatusDesc[code];
            re.httpmethod = method;
            re.httppath   = path;

            throw re;

        }

        // Reject this request

        internal void Reject()
        {
            Fail(Rest.HttpStatusCodeNotImplemented, "request rejected (not implemented)");
        }

        // This MUST be called by an agent handler before it returns
        // control to Handle, otherwise the request will be ignored.
        // This is called implciitly for the REST stream handlers and
        // is harmless if it is called twice.

        internal virtual bool Respond(string reason)
        {


            Rest.Log.DebugFormat("{0} Respond ENTRY, handled = {1}, reason = {2}", MsgId, handled, reason);

            // We do this to try and make multiple Respond requests harmless,
            // as it is sometimes convenient to isse a response without
            // certain knowledge that it has not previously been done.

            if (!handled)
            {

                Rest.Log.DebugFormat("{0} Generating Response", MsgId);
                Rest.Log.DebugFormat("{0} Method is {1}", MsgId, method);

                // A Head request can NOT have a body! So don't waste time on
                // formatting if we're going to reject it anyway!

                if (method != Rest.HEAD)
                {

                    Rest.Log.DebugFormat("{0} Response is not abbreviated", MsgId);

                    // If the writer is non-null then we know that an XML
                    // data component exists. Flush and close the writer and
                    // then convert the result to the expected buffer format
                    // unless the request has already been failed for some
                    // reason.

                    if (writer != null)
                    {
                        Rest.Log.DebugFormat("{0} XML Response handler extension ENTRY", MsgId);
                        Rest.Log.DebugFormat("{0} XML Response exists", MsgId);
                        writer.Flush();
                        writer.Close();
                        if (!fail)
                        {
                            buffer = xmldata.ToArray();
                            AddHeader("Content-Type","application/xml");
                        }
                        xmldata.Close();
                        Rest.Log.DebugFormat("{0} XML Response encoded", MsgId);
                        Rest.Log.DebugFormat("{0} XML Response handler extension EXIT", MsgId);
                    }

                    if (buffer == null && body != null)
                    {
                        buffer = encoding.GetBytes(body);
                        AddHeader("Content-Type",bodyType);
                    }

                    // OK, if the buffer contains something, regardless of how
                    // it got there, set various response headers accordingly.

                    if (buffer != null)
                    {
                        Rest.Log.DebugFormat("{0} Buffer-based entity", MsgId);
                    }
                    else
                    {
                        if (statusBody != String.Empty)
                        {
                            statusBody += Rest.statusTail;
                            buffer = encoding.GetBytes(statusBody);
                            AddHeader("Content-Type","text/html");
                        }
                        else
                        {
                            statusBody = Rest.statusHead;
                            appendStatus(String.Format(": ({0}) {1}",
                                 statusCode, Rest.HttpStatusDesc[statusCode]));
                            statusBody += Rest.statusTail;
                            buffer = encoding.GetBytes(statusBody);
                            AddHeader("Content-Type","text/html");
                        }
                    }

                    response.ContentLength64 = buffer.Length;

                    if (response.ContentEncoding == null)
                        response.ContentEncoding = encoding;

                    response.SendChunked     = chunked;
                    response.KeepAlive       = keepAlive;

                }

                // Set the status code & description. If nothing has been stored,
                // we consider that a success.

                if (statusCode == 0)
                {
                    Complete();
                }

                // Set the response code in the actual carrier

                response.StatusCode = statusCode;

                // For a redirect we need to set the relocation header accordingly

                if (response.StatusCode == (int) Rest.HttpStatusCodeTemporaryRedirect ||
                    response.StatusCode == (int) Rest.HttpStatusCodePermanentRedirect)
                {
                    Rest.Log.DebugFormat("{0} Re-direct location is {1}", MsgId, redirectLocation);
                    response.RedirectLocation = redirectLocation;
                }

                // And include the status description if provided.

                response.StatusDescription = Rest.HttpStatusDesc[response.StatusCode];

                // Finally we send back our response.

                // We've left the setting of handled' until the
                // last minute because the header settings included
                // above are pretty harmless. But everything from
                // here on down probably leaves the response
                // element unusable by anyone else.

                handled = true;

                // DumpHeaders();

                // if (request.InputStream != null)
                // {
                //     Rest.Log.DebugFormat("{0} Closing input stream", MsgId);
                //     request.InputStream.Close();
                // }

                if (buffer != null && buffer.Length != 0)
                {
                    Rest.Log.DebugFormat("{0} Entity buffer, length = {1}", MsgId, buffer.Length);
                    // Rest.Log.DebugFormat("{0} Entity buffer, length = {1} : <{2}>",
                    //                     MsgId, buffer.Length, encoding.GetString(buffer));
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }

                // Closing the outputstream should complete the transmission process

                Rest.Log.DebugFormat("{0} Sending response", MsgId);
                // response.OutputStream.Close();
                response.Send();

            }

            Rest.Log.DebugFormat("{0} Respond EXIT, handled = {1}, reason = {2}", MsgId, handled, reason);

            return handled;

        }

        /// <summary>
        /// These methods allow a service provider to manipulate the
        /// request/response headers. The DumpHeaders method is intended
        /// for problem diagnosis.
        /// </summary>

        internal void AddHeader(string hdr, string data)
        {
            if (Rest.DEBUG) Rest.Log.DebugFormat("{0}   Adding header: <{1}: {2}>", MsgId, hdr, data);
            response.AddHeader(hdr, data);
        }

        // internal void RemoveHeader(string hdr)
        // {
        //     if (Rest.DEBUG)
        //     {
        //         Rest.Log.DebugFormat("{0} Removing header: <{1}>", MsgId, hdr);
        //         if (response.Headers.Get(hdr) == null)
        //         {
        //             Rest.Log.DebugFormat("{0} No such header existed",
        //                      MsgId, hdr);
        //         }
        //     }
        //     response.Headers.Remove(hdr);
        // }

        // internal void DumpHeaders()
        // {
        //     if (Rest.DEBUG)
        //     {
        //         for (int i=0;i<response.Headers.Count;i++)
        //         {
        //             Rest.Log.DebugFormat("{0} Header[{1}] : {2}", MsgId, i,
        //                      response.Headers.Get(i));
        //         }
        //     }
        // }

        // Setup the XML writer for output

        internal void initXmlWriter()
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            xmldata = new MemoryStream();
            settings.Indent = true;
            settings.IndentChars = "    ";
            settings.Encoding = encoding;
            settings.CloseOutput = false;
            settings.OmitXmlDeclaration = true;
            settings.ConformanceLevel = ConformanceLevel.Fragment;
            writer = XmlWriter.Create(xmldata, settings);
        }

        internal void initXmlReader()
        {

            XmlReaderSettings        settings = new XmlReaderSettings();

            settings.ConformanceLevel             = ConformanceLevel.Fragment;
            settings.IgnoreComments               = true;
            settings.IgnoreWhitespace             = true;
            settings.IgnoreProcessingInstructions = true;
            settings.ValidationType               = ValidationType.None;

            reader = XmlReader.Create(request.InputStream,settings);

        }

        internal void appendStatus(string msg)
        {
            if (statusBody == String.Empty)
            {
                statusBody = String.Format(Rest.statusHead, request.HttpMethod);
            }

            statusBody = String.Format("{0} {1}", statusBody, msg);
        }

#endregion service_interface

#region internal_methods

        /// <summary>
        /// Helper methods for deconstructing and reconstructing
        /// URI path data.
        /// </summary>

        private void initUrl()
        {

            uri = request.Url;

            if (query == null)
            {
                query = uri.Query;
            }

            // If the path has not been previously initialized,
            // do so now.

            if (path == null)
            {
                path = uri.AbsolutePath;
                if (path.EndsWith(Rest.UrlPathSeparator))
                    path = path.Substring(0,path.Length-1);
            }

            // If we succeeded in getting a path, perform any
            // additional pre-processing required.

            if (path != null)
            {
                if (Rest.ExtendedEscape)
                {
                    // Handle "+". Not a standard substitution, but
                    // common enough...
                    path      = path.Replace(Rest.C_PLUS,Rest.C_SPACE);
                }
                pathNodes = path.Split(Rest.CA_PATHSEP);
            }
            else
            {
                pathNodes = EmptyPath;
            }

            // Elimiate any %-escaped values. This is left until here
            // so that escaped "+' are not mistakenly replaced.

            path = Uri.UnescapeDataString(path);

            // Request server context info

            hostname = uri.Host;
            port     = uri.Port;

        }

        private int initParameters(int prfxlen)
        {

            if (prfxlen < path.Length-1)
            {
                parameters = path.Substring(prfxlen+1).Split(Rest.CA_PATHSEP);
            }
            else
            {
                parameters = new string[0];
            }

            // Generate a debug list of the decoded parameters

            if (Rest.DEBUG && prfxlen < path.Length-1)
            {
                Rest.Log.DebugFormat("{0} URI: Parameters: {1}", MsgId, path.Substring(prfxlen));
                for (int i = 0; i < parameters.Length; i++)
                {
                    Rest.Log.DebugFormat("{0} Parameter[{1}]: {2}", MsgId, i, parameters[i]);
                }
            }

            return parameters.Length;

        }

#endregion internal_methods

    }
}
