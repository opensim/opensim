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
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
using OSHttpServer;
using log4net;

namespace OpenSim.Framework.Servers.HttpServer
{
    public class OSHttpRequest : IOSHttpRequest
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected IHttpRequest m_request = null;
        protected IHttpClientContext m_context = null;

        public string[] AcceptTypes
        {
            get { return m_request.AcceptTypes; }
        }

        public Encoding ContentEncoding
        {
            get { return m_contentEncoding; }
        }
        private Encoding m_contentEncoding;

        public long ContentLength
        {
            get { return m_request.ContentLength; }
        }

        public long ContentLength64
        {
            get { return ContentLength; }
        }

        public string ContentType
        {
            get { return m_contentType; }
        }
        private string m_contentType;

        public bool HasEntityBody
        {
            get { return m_request.ContentLength != 0; }
        }

        public NameValueCollection Headers
        {
            get { return m_request.Headers; }
        }

        public string HttpMethod
        {
            get { return m_request.Method; }
        }

        public Stream InputStream
        {
            get { return m_request.Body; }
        }

        public bool IsSecured
        {
            get { return m_context.IsSecured; }
        }

        public bool KeepAlive
        {
            get { return ConnectionType.KeepAlive == m_request.Connection; }
        }

        public NameValueCollection QueryString
        {
            get { return m_request.QueryString;}
        }

        private Hashtable m_queryAsHashtable = null;
        public Hashtable Query
        {
            get
            {
                if (m_queryAsHashtable == null)
                    BuildQueryHashtable();
                return m_queryAsHashtable;
            }
        }

        //faster than Query
        private Dictionary<string, string> _queryAsDictionay = null;
        public Dictionary<string,string> QueryAsDictionary
        {
            get
            {
                if (_queryAsDictionay == null)
                    BuildQueryDictionary();
                return _queryAsDictionay;
            }
        }

        private HashSet<string> m_queryFlags = null;
        public HashSet<string> QueryFlags
        {
            get
            {
                if (m_queryFlags == null)
                    BuildQueryDictionary();
                return m_queryFlags;
            }
        }
    /// <value>
    /// POST request values, if applicable
    /// </value>
    //        public Hashtable Form { get; private set; }

        public string RawUrl
        {
            get { return m_request.Uri.AbsolutePath; }
        }

        public IPEndPoint RemoteIPEndPoint
        {
            get { return m_request.RemoteIPEndPoint; }
        }

        public IPEndPoint LocalIPEndPoint
        {
            get { return m_request.LocalIPEndPoint; }
        }

        public Uri Url
        {
            get { return m_request.Uri; }
        }

        public string UriPath
        {
            get { return m_request.UriPath; }
        }

        public string UserAgent
        {
            get { return m_userAgent; }
        }
        private string m_userAgent;

        public double ArrivalTS
        {
            get { return m_request.ArrivalTS;}
        }

        internal IHttpRequest IHttpRequest
        {
            get { return m_request; }
        }

        internal IHttpClientContext IHttpClientContext
        {
            get { return m_context; }
        }

        /// <summary>
        /// Internal whiteboard for handlers to store temporary stuff
        /// into.
        /// </summary>
        internal Dictionary<string, object> Whiteboard
        {
            get { return _whiteboard; }
        }
        private Dictionary<string, object> _whiteboard = new Dictionary<string, object>();

        public OSHttpRequest() {}

        public OSHttpRequest(IHttpRequest req)
        {
            m_request = req;
            m_context = req.Context;

            if (null != req.Headers["content-encoding"])
            {
                try
                {
                    m_contentEncoding = Encoding.GetEncoding(m_request.Headers["content-encoding"]);
                }
                catch
                {
                    // ignore
                }
            }

            if (null != req.Headers["content-type"])
                m_contentType = m_request.Headers["content-type"];
            if (null != req.Headers["user-agent"])
                m_userAgent = req.Headers["user-agent"];

//            Form = new Hashtable();
//            foreach (HttpInputItem item in req.Form)
//            {
//                _log.DebugFormat("[OSHttpRequest]: Got form item {0}={1}", item.Name, item.Value);
//                Form.Add(item.Name, item.Value);
//            }
        }

        private void BuildQueryDictionary()
        {
            NameValueCollection q = m_request.QueryString;
            _queryAsDictionay = new Dictionary<string, string>();
            m_queryFlags = new HashSet<string>();
            for(int i = 0; i <q.Count; ++i)
            {
                try
                {
                    var name = q.GetKey(i);
                    if(!string.IsNullOrEmpty(name))
                        _queryAsDictionay[name] = q[i];
                    else
                        m_queryFlags.Add(q[i]);
                }
                catch {}
            }
        }

        private void BuildQueryHashtable()
        {
            NameValueCollection q = m_request.QueryString;
            m_queryAsHashtable = new Hashtable();
            m_queryFlags = new HashSet<string>();
            for (int i = 0; i < q.Count; ++i)
            {
                try
                {
                    var name = q.GetKey(i);
                    if (!string.IsNullOrEmpty(name))
                        m_queryAsHashtable[name] = q[i];
                    else
                        m_queryFlags.Add(q[i]);
                }
                catch { }
            }
        }

        public override string ToString()
        {
            StringBuilder me = new StringBuilder();
            me.Append(String.Format("OSHttpRequest: {0} {1}\n", HttpMethod, RawUrl));
            foreach (string k in Headers.AllKeys)
            {
                me.Append(String.Format("    {0}: {1}\n", k, Headers[k]));
            }
            if (null != RemoteIPEndPoint)
            {
                me.Append(String.Format("    IP: {0}\n", RemoteIPEndPoint));
            }

            return me.ToString();
        }
    }
}
