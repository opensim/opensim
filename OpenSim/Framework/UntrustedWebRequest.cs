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
using System.Net;
using System.Net.Security;
using System.Text;
using log4net;

namespace OpenSim.Framework
{
    /// <summary>
    /// Used for requests to untrusted endpoints that may potentially be
    /// malicious
    /// </summary>
    public static class UntrustedHttpWebRequest
    {
        /// <summary>Setting this to true will allow HTTP connections to localhost</summary>
        private const bool DEBUG = true;

        private static readonly ILog m_log =
                LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly ICollection<string> allowableSchemes = new List<string> { "http", "https" };

        /// <summary>
        /// Creates an HttpWebRequest that is hardened against malicious
        /// endpoints after ensuring the given Uri is safe to retrieve
        /// </summary>
        /// <param name="uri">Web location to request</param>
        /// <returns>A hardened HttpWebRequest if the uri was determined to be safe</returns>
        /// <exception cref="ArgumentNullException">If uri is null</exception>
        /// <exception cref="ArgumentException">If uri is unsafe</exception>
        public static HttpWebRequest Create(Uri uri)
        {
            return Create(uri, DEBUG, 1000 * 5, 1000 * 20, 10);
        }

        /// <summary>
        /// Creates an HttpWebRequest that is hardened against malicious
        /// endpoints after ensuring the given Uri is safe to retrieve
        /// </summary>
        /// <param name="uri">Web location to request</param>
        /// <param name="allowLoopback">True to allow connections to localhost, otherwise false</param>
        /// <param name="readWriteTimeoutMS">Read write timeout, in milliseconds</param>
        /// <param name="timeoutMS">Connection timeout, in milliseconds</param>
        /// <param name="maximumRedirects">Maximum number of allowed redirects</param>
        /// <returns>A hardened HttpWebRequest if the uri was determined to be safe</returns>
        /// <exception cref="ArgumentNullException">If uri is null</exception>
        /// <exception cref="ArgumentException">If uri is unsafe</exception>
        public static HttpWebRequest Create(Uri uri, bool allowLoopback, int readWriteTimeoutMS, int timeoutMS, int maximumRedirects)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            if (!IsUriAllowable(uri, allowLoopback))
                throw new ArgumentException("Uri " + uri + " was rejected");

            HttpWebRequest httpWebRequest = (HttpWebRequest)HttpWebRequest.Create(uri);
            httpWebRequest.MaximumAutomaticRedirections = maximumRedirects;
            httpWebRequest.ReadWriteTimeout = readWriteTimeoutMS;
            httpWebRequest.Timeout = timeoutMS;
            httpWebRequest.KeepAlive = false;

            return httpWebRequest;
        }

        public static string PostToUntrustedUrl(Uri url, string data)
        {
            try
            {
                byte[] requestData = System.Text.Encoding.UTF8.GetBytes(data);

                HttpWebRequest request = Create(url);
                request.Method = "POST";
                request.ContentLength = requestData.Length;
                request.ContentType = "application/x-www-form-urlencoded";

                using (Stream requestStream = request.GetRequestStream())
                    requestStream.Write(requestData, 0, requestData.Length);

                using (WebResponse response = request.GetResponse())
                {
                    using (Stream responseStream = response.GetResponseStream())
                        return responseStream.GetStreamString();
                }
            }
            catch (Exception ex)
            {
                m_log.Warn("POST to untrusted URL " + url + " failed: " + ex.Message);
                return null;
            }
        }

        public static string GetUntrustedUrl(Uri url)
        {
            try
            {
                HttpWebRequest request = Create(url);

                using (WebResponse response = request.GetResponse())
                {
                    using (Stream responseStream = response.GetResponseStream())
                        return responseStream.GetStreamString();
                }
            }
            catch (Exception ex)
            {
                m_log.Warn("GET from untrusted URL " + url + " failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Determines whether a URI is allowed based on scheme and host name.
        /// No requireSSL check is done here
        /// </summary>
        /// <param name="allowLoopback">True to allow loopback addresses to be used</param>
        /// <param name="uri">The URI to test for whether it should be allowed.</param>
        /// <returns>
        ///     <c>true</c> if [is URI allowable] [the specified URI]; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsUriAllowable(Uri uri, bool allowLoopback)
        {
            if (!allowableSchemes.Contains(uri.Scheme))
            {
                m_log.WarnFormat("Rejecting URL {0} because it uses a disallowed scheme.", uri);
                return false;
            }

            // Try to interpret the hostname as an IP address so we can test for internal
            // IP address ranges.  Note that IP addresses can appear in many forms 
            // (e.g. http://127.0.0.1, http://2130706433, http://0x0100007f, http://::1
            // So we convert them to a canonical IPAddress instance, and test for all
            // non-routable IP ranges: 10.*.*.*, 127.*.*.*, ::1
            // Note that Uri.IsLoopback is very unreliable, not catching many of these variants.
            IPAddress hostIPAddress;
            if (IPAddress.TryParse(uri.DnsSafeHost, out hostIPAddress))
            {
                byte[] addressBytes = hostIPAddress.GetAddressBytes();

                // The host is actually an IP address.
                switch (hostIPAddress.AddressFamily)
                {
                    case System.Net.Sockets.AddressFamily.InterNetwork:
                        if (!allowLoopback && (addressBytes[0] == 127 || addressBytes[0] == 10))
                        {
                            m_log.WarnFormat("Rejecting URL {0} because it is a loopback address.", uri);
                            return false;
                        }
                        break;
                    case System.Net.Sockets.AddressFamily.InterNetworkV6:
                        if (!allowLoopback && IsIPv6Loopback(hostIPAddress))
                        {
                            m_log.WarnFormat("Rejecting URL {0} because it is a loopback address.", uri);
                            return false;
                        }
                        break;
                    default:
                        m_log.WarnFormat("Rejecting URL {0} because it does not use an IPv4 or IPv6 address.", uri);
                        return false;
                }
            }
            else
            {
                // The host is given by name.  We require names to contain periods to
                // help make sure it's not an internal address.
                if (!allowLoopback && !uri.Host.Contains("."))
                {
                    m_log.WarnFormat("Rejecting URL {0} because it does not contain a period in the host name.", uri);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether an IP address is the IPv6 equivalent of "localhost/127.0.0.1".
        /// </summary>
        /// <param name="ip">The ip address to check.</param>
        /// <returns>
        ///     <c>true</c> if this is a loopback IP address; <c>false</c> otherwise.
        /// </returns>
        private static bool IsIPv6Loopback(IPAddress ip)
        {
            if (ip == null)
                throw new ArgumentNullException("ip");

            byte[] addressBytes = ip.GetAddressBytes();
            for (int i = 0; i < addressBytes.Length - 1; i++)
            {
                if (addressBytes[i] != 0)
                    return false;
            }

            if (addressBytes[addressBytes.Length - 1] != 1)
                return false;

            return true;
        }
    }
}
