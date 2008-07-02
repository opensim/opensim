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
 *     * Neither the name of the OpenSim Project nor the
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
 * 
 */

using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections.Specialized;
using OpenSim.Framework.Servers;
using libsecondlife;
using System.Xml;

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
    /// specifici REST handler, and fundamental changes to 
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

    internal class RequestData
    {

        // HTTP Server interface data

        internal OSHttpRequest        request = null;
        internal OSHttpResponse       response = null;

        // Request lifetime values

        internal NameValueCollection  headers = null;
        internal List<string>         removed_headers = null;
        internal byte[]               buffer = null;
        internal string               body = null;
        internal string               html = null;
        internal string               entity = null;
        internal string               path = null;
        internal string               method = null;
        internal string               statusDescription = null;
        internal string               redirectLocation = null;
        internal string[]             pathNodes = null;
        internal string[]             parameters = null;
        internal int                  statusCode = 0;
        internal bool                 handled = false;
        internal LLUUID               uuid = LLUUID.Zero;
        internal Encoding             encoding = Rest.Encoding;
        internal Uri                  uri = null;
        internal string               query = null;
        internal bool                 fail = false;
        internal string               hostname = "localhost";
        internal int                  port = 80;
        internal string               prefix = Rest.UrlPathSeparator;

        // Authentication related state
 
        internal bool                 authenticated = false;
        internal string               scheme = Rest.AS_DIGEST;
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
        internal LLUUID               client = LLUUID.Zero;

        // XML related state

        internal XmlWriter            writer = null;
        internal XmlReader            reader = null;

        // Internal working state

        private StringBuilder         sbuilder = new StringBuilder(1024);
        private MemoryStream          xmldata = null;

        private static readonly string[] EmptyPath = { String.Empty };

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
        
        private static Regex digestParm1 = new Regex("\\s*(?<parm>\\w+)\\s*=\\s*\"(?<pval>\\S+)\"",
                                                     RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        private static Regex digestParm2 = new Regex("\\s*(?<parm>\\w+)\\s*=\\s*(?<pval>[^\\p{P}\\s]+)",
                                                     RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        private static Regex reuserPass  = new Regex("\\s*(?<user>\\w+)\\s*:\\s*(?<pass>\\S*)",
                                                     RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        // For efficiency, we create static instances of these objects

        private static MD5   md5hash     = MD5.Create();
        
        private static StringComparer sc = StringComparer.OrdinalIgnoreCase;

        // Constructor
        
        internal RequestData(OSHttpRequest p_request, OSHttpResponse p_response, string qprefix)
        {

            request  = p_request;
            response = p_response;

            sbuilder.Length = 0;

            encoding = request.ContentEncoding;
            if (encoding == null)
            {
                encoding = Rest.Encoding;
            }

            method = request.HttpMethod.ToLower();
            initUrl();

            initParameters(qprefix.Length);

        }

        // Just for convenience...

        internal string MsgId
        {
            get { return Rest.MsgId; }
        }

        // Defer authentication check until requested

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
        /// The REST handler has requested authentication. Authentication
        /// is considered to be with respect to the current values for
        /// Realm, domain, etc.
        ///
        /// This method checks to see if the current request is already
        /// authenticated for this domain. If it is, then it returns 
        /// true. If it is not, then it issues a challenge to the client
        /// and responds negatively to the request.
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
            // we get it.

            if (scheme != null && scheme.ToLower() != reqscheme)
            {
                Rest.Log.DebugFormat("{0} Challenge reason: Required scheme not accepted", MsgId);
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
            Fail(Rest.HttpStatusCodeNotAuthorized, Rest.HttpStatusDescNotAuthorized);
        }

        /// <summary>
        /// Interpret a BASIC authorization claim
        /// This is here for completeness, it is not used.
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
        /// This is an RFC2617 compliant HTTP MD5 Digest authentication
        /// implementation. It has been tested with Firefox, Java HTTP client,
        /// and Miscrosoft's Internet Explorer V7.
        /// </summary>

        private void DoDigest(string authdata)
        {

            string response = null;

            MatchCollection matches = digestParm1.Matches(authdata);

            // Collect all of the supplied parameters and store them
            // in a dictionary (for ease of access)

            foreach (Match m in matches)
            {
                authparms.Add(m.Groups["parm"].Value,m.Groups["pval"].Value);
                Rest.Log.DebugFormat("{0} String Parameter matched : {1} = {2}", 
                                     MsgId, m.Groups["parm"].Value,m.Groups["pval"].Value);
            }

            // And pick up any tokens too

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
                    // side state and we can;t validate the MD5 unless the lcient returns it
                    // to us, as it should.

                    if (!authparms.TryGetValue("nonce", out nonce))
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
                            break;
                        }

                        cnonce = authparms["cnonce"];

                        if (!authparms.ContainsKey("nc"))
                        {
                            Rest.Log.WarnFormat("{0} Authentication failed: cnonce counter missing", MsgId); 
                            break;
                        }

                        nck = authparms["nc"];

                        if (cntable.TryGetValue(cnonce, out ncl))
                        {
                            if (Rest.Hex2Int(ncl) <= Rest.Hex2Int(nck))
                            {
                                Rest.Log.WarnFormat("{0} Authentication failed: bad cnonce counter", MsgId); 
                                break;
                            }
                            cntable[cnonce] = nck;
                        }
                        else
                        {
                            lock(cntable) cntable.Add(cnonce, nck);
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
                            break;
                        }
                        if (authparms.ContainsKey("nc"))
                        {
                            Rest.Log.WarnFormat("{0} Authentication failed: invalid cnonce counter[2]", MsgId); 
                            break;
                        }
                    }

                    // Validate the supplied userid/password info

                    authenticated = ValidateDigest(userName, nonce, cnonce, nck, authPrefix, response);

                } 
                while (false);

            }

        }

        // Indicate that authentication is required

        internal void Challenge(string scheme, string realm, string domain, string nonce,
                                string opaque, string stale, string alg,
                                string qop, string auth)
        {

            sbuilder.Length = 0;

            if (scheme == null || scheme == Rest.AS_DIGEST)
            {

                sbuilder.Append(Rest.AS_DIGEST);
                sbuilder.Append(" ");

                if (realm != null)
                {
                    sbuilder.Append("realm=");
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(realm);
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(Rest.CS_COMMA);
                }

                if (nonce != null)
                {
                    sbuilder.Append("nonce=");
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(nonce);
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(Rest.CS_COMMA);
                }

                if (opaque != null)
                {
                    sbuilder.Append("opaque=");
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(opaque);
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(Rest.CS_COMMA);
                }

                if (stale != null)
                {
                    sbuilder.Append("stale=");
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(stale);
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(Rest.CS_COMMA);
                }

                if (alg != null)
                {
                    sbuilder.Append("algorithm=");
                    sbuilder.Append(alg);
                    sbuilder.Append(Rest.CS_COMMA);
                }

                if (qop != String.Empty)
                {
                    sbuilder.Append("qop=");
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(qop);
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(Rest.CS_COMMA);
                }

                if (auth != null)
                {
                    sbuilder.Append(auth);
                    sbuilder.Append(Rest.CS_COMMA);
                }

                if (Rest.Domains.Count != 0)
                {
                    sbuilder.Append("domain=");
                    sbuilder.Append(Rest.CS_DQUOTE);
                    foreach (string dom in Rest.Domains.Values)
                    {
                        sbuilder.Append(dom);
                        sbuilder.Append(Rest.CS_SPACE);
                    }
                    if (sbuilder[sbuilder.Length-1] == Rest.C_SPACE)
                    {
                        sbuilder.Length = sbuilder.Length-1;
                    }
                    sbuilder.Append(Rest.CS_DQUOTE);
                    sbuilder.Append(Rest.CS_COMMA);
                }

                if (sbuilder[sbuilder.Length-1] == Rest.C_COMMA)
                {
                    sbuilder.Length = sbuilder.Length-1;
                }

                AddHeader(Rest.HttpHeaderWWWAuthenticate,sbuilder.ToString());

            }

            if (scheme == null || scheme == Rest.AS_BASIC)
            {

                sbuilder.Append(Rest.AS_BASIC);

                if (realm != null)
                {
                    sbuilder.Append(" realm=\"");
                    sbuilder.Append(realm);
                    sbuilder.Append("\"");
                }
                AddHeader(Rest.HttpHeaderWWWAuthenticate,sbuilder.ToString());
            }

        }

        private bool Validate(string user, string pass)
        {
            Rest.Log.DebugFormat("{0} Validating {1}:{2}", MsgId, user, pass);
            return user == "awebb" && pass == getPassword(user);
        }

        private string getPassword(string user)
        {
            return Rest.GodKey;
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

        internal void Complete()
        {
            statusCode = Rest.HttpStatusCodeOK;
            statusDescription = Rest.HttpStatusDescOK;
        }

        internal void Redirect(string Url, bool temp)
        {

            redirectLocation = Url;

            if (temp)
            {
                statusCode = Rest.HttpStatusCodeTemporaryRedirect;
                statusDescription = Rest.HttpStatusDescTemporaryRedirect;
            }
            else
            {
                statusCode = Rest.HttpStatusCodePermanentRedirect;
                statusDescription = Rest.HttpStatusDescPermanentRedirect;
            }

            Fail(statusCode, statusDescription, true);

        }

        // Fail for an arbitrary reason. Just a failure with
        // headers.

        internal void Fail(int code, string message)
        {
            Fail(code, message, true);
        }

        // More adventurous. This failure also includes a 
        // specified entity.

        internal void Fail(int code, string message, string data)
        {
            buffer = null;
            body   = data;
            Fail(code, message, false);
        }

        internal void Fail(int code, string message, bool reset)
        {

            statusCode        = code;
            statusDescription = message;

            if (reset)
            {
                buffer            = null;
                body              = null;
            }

            if (Rest.DEBUG)
            {
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

            Respond("Failure response");
           
            RestException re = new RestException(message+" <"+code+">");

            re.statusCode = code;
            re.statusDesc = message;
            re.httpmethod = method;
            re.httppath   = path;

            throw re;

        }

        // Reject this request

        internal void Reject()
        {
            Fail(Rest.HttpStatusCodeNotImplemented, Rest.HttpStatusDescNotImplemented);
        }

        // This MUST be called by an agent handler before it returns 
        // control to Handle, otherwise the request will be ignored.
        // This is called implciitly for the REST stream handlers and
        // is harmless if it is called twice.

        internal virtual bool Respond(string reason)
        {

            Rest.Log.DebugFormat("{0} Respond ENTRY, handled = {1}, reason = {2}", MsgId, handled, reason);

            if (!handled)
            {

                Rest.Log.DebugFormat("{0} Generating Response", MsgId);

                // Process any arbitrary headers collected

                BuildHeaders();

                // A Head request can NOT have a body!
                if (method != Rest.HEAD)
                {

                    Rest.Log.DebugFormat("{0} Response is not abbreviated", MsgId);

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

                    // If buffer != null, then we assume that 
                    // this has already been done some other
                    // way. For example, transfer encoding might
                    // have been done.

                    if (buffer == null)
                    {
                        if (body != null && body.Length > 0)
                        {
                            Rest.Log.DebugFormat("{0} String-based entity", MsgId);
                            buffer = encoding.GetBytes(body);
                        }
                    }

                    if (buffer != null)
                    {
                        Rest.Log.DebugFormat("{0} Buffer-based entity", MsgId);
                        if (response.Headers.Get("Content-Encoding") == null)
                            response.ContentEncoding = encoding;
                        response.ContentLength64 = buffer.Length;
                        response.SendChunked     = false;
                        response.KeepAlive       = false;
                    }

                }

                // Set the status code & description. If nothing
                // has been stored, we consider that a success

                if (statusCode == 0)
                {
                    Complete();
                }

                response.StatusCode = statusCode;

                if (response.StatusCode == (int)OSHttpStatusCode.RedirectMovedTemporarily || 
                    response.StatusCode == (int)OSHttpStatusCode.RedirectMovedPermanently)
                {
                    response.RedirectLocation = redirectLocation;
                }

                if (statusDescription != null)
                {
                    response.StatusDescription = statusDescription;
                }

                // Finally we send back our response, consuming
                // any exceptions that doing so might produce.

                // We've left the setting of handled' until the
                // last minute because the header settings included
                // above are pretty harmless. But everything from
                // here on down probably leaves the response 
                // element unusable by anyone else.

                handled = true;

                if (buffer != null && buffer.Length != 0)
                {
                    Rest.Log.DebugFormat("{0} Entity buffer, length = {1} : <{2}>", 
                                         MsgId, buffer.Length, encoding.GetString(buffer));
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }

                response.OutputStream.Close();

                if (request.InputStream != null)
                {
                    request.InputStream.Close();
                }

            }

            Rest.Log.DebugFormat("{0} Respond EXIT, handled = {1}, reason = {2}", MsgId, handled, reason);

            return handled;

        }

        // Add a header to the table. If the header 
        // already exists, it is replaced.

        internal void AddHeader(string hdr, string data)
        {

            if (headers == null)
            {
                headers = new NameValueCollection();
            }

            headers[hdr] = data;

        }

        // Keep explicit track of any headers which
        // are to be removed.

        internal void RemoveHeader(string hdr)
        {

            if (removed_headers == null)
            {
                removed_headers = new List<string>();
            }

            removed_headers.Add(hdr);

            if (headers != null)
            {
                headers.Remove(hdr);
            }

        }

        // Should it prove necessary, we could always
        // restore the header collection from a cloned
        // copy, but for now we'll assume that that is
        // not necessary.

        private void BuildHeaders()
        {
            if (removed_headers != null)
            {
                foreach (string h in removed_headers)
                {
                    Rest.Log.DebugFormat("{0} Removing header: <{1}>", MsgId, h);
                    response.Headers.Remove(h);
                }
            }
            if (headers!= null)
            {
                for (int i = 0; i < headers.Count; i++)
                {
                    Rest.Log.DebugFormat("{0}   Adding header: <{1}: {2}>", 
                                         MsgId, headers.GetKey(i), headers.Get(i));
                    response.Headers.Add(headers.GetKey(i), headers.Get(i));
                }
            }
        }

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
                path = Uri.UnescapeDataString(path);
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

            // Request server context info

            hostname = uri.Host;
            port     = uri.Port;

        }

        internal int initParameters(int prfxlen)
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
                
        internal string[] PathNodes
        {
            get
            { 
                if (pathNodes == null)
                {
                    initUrl();
                }
                return pathNodes;
            }
        }
        
        internal string BuildUrl(int first, int last)
        {
           
            if (pathNodes == null)
            {
                initUrl();
            }

            if (first < 0)
            {
                first = first + pathNodes.Length;
            }

            if (last < 0)
            {
                last = last + pathNodes.Length;
                if (last < 0)
                {
                    return Rest.UrlPathSeparator;
                }
            }

            sbuilder.Length = 0;
            sbuilder.Append(Rest.UrlPathSeparator);

            if (first <= last)
            {
                for (int i = first; i <= last; i++)
                {
                    sbuilder.Append(pathNodes[i]);
                    sbuilder.Append(Rest.UrlPathSeparator);
                }
            }
            else
            {
                for (int i = last; i >= first; i--)
                {
                    sbuilder.Append(pathNodes[i]);
                    sbuilder.Append(Rest.UrlPathSeparator);
                }
            }

            return sbuilder.ToString();

        }

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
            // reader = XmlReader.Create(new StringReader(entity),settings);
            reader = XmlReader.Create(request.InputStream,settings);
        }

        private void Flush()
        {
            byte[] dbuffer = new byte[8192];
            while (request.InputStream.Read(dbuffer,0,dbuffer.Length) != 0);
            return;
        }

        // This allows us to make errors a bit more apparent in REST

        internal void SendHtml(string text)
        {
            SendHtml("OpenSim REST Interface 1.0", text);
        }

        internal void SendHtml(string title, string text)
        {

            AddHeader(Rest.HttpHeaderContentType, "text/html");
            sbuilder.Length = 0;

            sbuilder.Append("<html>");
            sbuilder.Append("<head>");
            sbuilder.Append("<title>");
            sbuilder.Append(title);
            sbuilder.Append("</title>");
            sbuilder.Append("</head>");

            sbuilder.Append("<body>");
            sbuilder.Append("<br />");
            sbuilder.Append("<p>");
            sbuilder.Append(text);
            sbuilder.Append("</p>");
            sbuilder.Append("</body>");
            sbuilder.Append("</html>");

            html = sbuilder.ToString();

        }
    }
}
