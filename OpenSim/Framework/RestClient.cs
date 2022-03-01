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
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web;
using log4net;

using OpenSim.Framework.ServiceAuth;

namespace OpenSim.Framework
{
    /// <summary>
    /// Implementation of a generic REST client
    /// </summary>
    /// <remarks>
    /// This class is a generic implementation of a REST (Representational State Transfer) web service. This
    /// class is designed to execute both synchronously and asynchronously.
    ///
    /// Internally the implementation works as a two stage asynchronous web-client.
    /// When the request is initiated, RestClient will query asynchronously for for a web-response,
    /// sleeping until the initial response is returned by the server. Once the initial response is retrieved
    /// the second stage of asynchronous requests will be triggered, in an attempt to read of the response
    /// object into a memorystream as a sequence of asynchronous reads.
    ///
    /// The asynchronisity of RestClient is designed to move as much processing into the back-ground, allowing
    /// other threads to execute, while it waits for a response from the web-service. RestClient itself can be
    /// invoked by the caller in either synchronous mode or asynchronous modes.
    /// </remarks>
    public class RestClient : IDisposable
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // private string realuri;

        #region member variables

        /// <summary>
        /// The base Uri of the web-service e.g. http://www.google.com
        /// </summary>
        private string _url;

        /// <summary>
        /// Path elements of the query
        /// </summary>
        private List<string> _pathElements = new List<string>();

        /// <summary>
        /// Parameter elements of the query, e.g. min=34
        /// </summary>
        private Dictionary<string, string> _parameterElements = new Dictionary<string, string>();

        /// <summary>
        /// Request method. E.g. GET, POST, PUT or DELETE
        /// </summary>
        private string _method;

        /// <summary>
        /// Temporary buffer used to store bytes temporarily as they come in from the server
        /// </summary>
        private byte[] _readbuf;

        /// <summary>
        /// MemoryStream representing the resulting resource
        /// </summary>
        private MemoryStream _resource;

        /// <summary>
        /// WebRequest object, held as a member variable
        /// </summary>
        private HttpWebRequest _request;

        /// <summary>
        /// WebResponse object, held as a member variable, so we can close it
        /// </summary>
        private HttpWebResponse _response;

        /// <summary>
        /// This flag will help block the main synchroneous method, in case we run in synchroneous mode
        /// </summary>
        //public static ManualResetEvent _allDone = new ManualResetEvent(false);

        /// <summary>
        /// Default time out period
        /// </summary>
        //private const int DefaultTimeout = 10*1000; // 10 seconds timeout

        /// <summary>
        /// Default Buffer size of a block requested from the web-server
        /// </summary>
        private const int BufferSize = 4 * 4096; // Read blocks of 4 * 4 KB.

        /// <summary>
        /// if an exception occours during async processing, we need to save it, so it can be
        /// rethrown on the primary thread;
        /// </summary>
        private Exception _asyncException;

        #endregion member variables

        #region constructors

        /// <summary>
        /// Instantiate a new RestClient
        /// </summary>
        /// <param name="url">Web-service to query, e.g. http://osgrid.org:8003</param>
        public RestClient(string url)
        {
            _url = url;
            _readbuf = new byte[BufferSize];
            _resource = new MemoryStream();
            _request = null;
            _response = null;
            _lock = new object();
        }

        private object _lock;

        #endregion constructors


        #region Dispose

        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                _resource.Dispose();
            }

            disposed = true;
        }

        #endregion Dispose


        /// <summary>
        /// Add a path element to the query, e.g. assets
        /// </summary>
        /// <param name="element">path entry</param>
        public void AddResourcePath(string element)
        {
            _pathElements.Add(Util.TrimEndSlash(element));
        }

        /// <summary>
        /// Add a query parameter to the Url
        /// </summary>
        /// <param name="name">Name of the parameter, e.g. min</param>
        /// <param name="value">Value of the parameter, e.g. 42</param>
        public void AddQueryParameter(string name, string value)
        {
            try
            {
                _parameterElements.Add(HttpUtility.UrlEncode(name), HttpUtility.UrlEncode(value));
            }
            catch (ArgumentException)
            {
                m_log.Error("[REST]: Query parameter " + name + " is already added.");
            }
            catch (Exception e)
            {
                m_log.Error("[REST]: An exception was raised adding query parameter to dictionary. Exception: {0}",e);
            }
        }

        /// <summary>
        /// Add a query parameter to the Url
        /// </summary>
        /// <param name="name">Name of the parameter, e.g. min</param>
        public void AddQueryParameter(string name)
        {
            try
            {
                _parameterElements.Add(HttpUtility.UrlEncode(name), null);
            }
            catch (ArgumentException)
            {
                m_log.Error("[REST]: Query parameter " + name + " is already added.");
            }
            catch (Exception e)
            {
                m_log.Error("[REST]: An exception was raised adding query parameter to dictionary. Exception: {0}",e);
            }
        }

        /// <summary>
        /// Web-Request method, e.g. GET, PUT, POST, DELETE
        /// </summary>
        public string RequestMethod
        {
            get { return _method; }
            set { _method = value; }
        }

        /// <summary>
        /// Build a Uri based on the initial Url, path elements and parameters
        /// </summary>
        /// <returns>fully constructed Uri</returns>
        private Uri buildUri()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(_url);

            foreach (string e in _pathElements)
            {
                sb.Append("/");
                sb.Append(e);
            }

            bool firstElement = true;
            foreach (KeyValuePair<string, string> kv in _parameterElements)
            {
                if (firstElement)
                {
                    sb.Append("?");
                    firstElement = false;
                }
                else
                    sb.Append("&");

                sb.Append(kv.Key);
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    sb.Append("=");
                    sb.Append(kv.Value);
                }
            }
            // realuri = sb.ToString();
            //m_log.InfoFormat("[REST CLIENT]: RestURL: {0}", realuri);
            return new Uri(sb.ToString());
        }

        #region Async communications with server

        /// <summary>
        /// Async method, invoked when a block of data has been received from the service
        /// </summary>
        /// <param name="ar"></param>
        private void StreamIsReadyDelegate(IAsyncResult ar)
        {
            try
            {
                Stream s = (Stream) ar.AsyncState;
                int read = s.EndRead(ar);

                if (read > 0)
                {
                    _resource.Write(_readbuf, 0, read);
                    // IAsyncResult asynchronousResult =
                    //     s.BeginRead(_readbuf, 0, BufferSize, new AsyncCallback(StreamIsReadyDelegate), s);
                    s.BeginRead(_readbuf, 0, BufferSize, new AsyncCallback(StreamIsReadyDelegate), s);

                    // TODO! Implement timeout, without killing the server
                    //ThreadPool.RegisterWaitForSingleObject(asynchronousResult.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), _request, DefaultTimeout, true);
                }
                else
                {
                    s.Close();
                    //_allDone.Set();
                }
            }
            catch (Exception e)
            {
                //_allDone.Set();
                _asyncException = e;
            }
        }

        #endregion Async communications with server

        /// <summary>
        /// Perform a synchronous request
        /// </summary>
        public MemoryStream Request()
        {
            return Request(null);
        }

        /// <summary>
        /// Perform a synchronous request
        /// </summary>
        public MemoryStream Request(IServiceAuth auth)
        {
            lock (_lock)
            {
                try
                {
                    _request = (HttpWebRequest) WebRequest.Create(buildUri());
                    _request.ContentType = "application/xml";
                    _request.Timeout = 90000;
                    _request.Method = RequestMethod;
                    _asyncException = null;
                    if (auth != null)
                        auth.AddAuthorization(_request.Headers);
                    else
                        _request.AllowWriteStreamBuffering = false;

                    if (WebUtil.DebugLevel >= 3)
                        m_log.DebugFormat("[REST CLIENT] {0} to {1}",  _request.Method, _request.RequestUri);

                    using (_response = (HttpWebResponse) _request.GetResponse())
                    {
                        using (Stream src = _response.GetResponseStream())
                        {
                            int length = src.Read(_readbuf, 0, BufferSize);
                            while (length > 0)
                            {
                                _resource.Write(_readbuf, 0, length);
                                length = src.Read(_readbuf, 0, BufferSize);
                            }
                        }
                    }
                }
                catch (WebException e)
                {
                    using (HttpWebResponse errorResponse = e.Response as HttpWebResponse)
                    {
                        if (null != errorResponse && HttpStatusCode.NotFound == errorResponse.StatusCode)
                        {
                            // This is often benign. E.g., requesting a missing asset will return 404.
                            m_log.DebugFormat("[REST CLIENT] Resource not found (404): {0}", _request.Address.ToString());
                        }
                        else
                        {
                            m_log.Error(string.Format("[REST CLIENT] Error fetching resource from server: {0} ", _request.Address.ToString()), e);
                        }
                    }
                    return null;
                }

                if (_asyncException != null)
                    throw _asyncException;

                if (_resource != null)
                {
                    _resource.Flush();
                    _resource.Seek(0, SeekOrigin.Begin);
                }

                if (WebUtil.DebugLevel >= 5)
                    WebUtil.LogOutgoingDetail("[REST CLIENT]", _resource);

                return _resource;
            }
        }

        // just sync post data, ignoring result
        public void POSTRequest(byte[] src, IServiceAuth auth)
        {
            try
            {
                _request = (HttpWebRequest)WebRequest.Create(buildUri());
                _request.ContentType = "application/xml";
                _request.Timeout = 90000;
                _request.Method = "POST";
                _asyncException = null;
                _request.ContentLength = src.Length;
                if (auth != null)
                    auth.AddAuthorization(_request.Headers);
                else
                    _request.AllowWriteStreamBuffering = false;
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[REST]: POST {0} failed with exception {1} {2}",
                                _request.RequestUri, e.Message, e.StackTrace);
                return;
            }

            try
            {
                using (Stream dst = _request.GetRequestStream())
                {
                    dst.Write(src, 0, src.Length);
                }

                using(HttpWebResponse response = (HttpWebResponse)_request.GetResponse())
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        using (StreamReader reader = new StreamReader(responseStream))
                        {
                            string responseStr = reader.ReadToEnd();
                            if (WebUtil.DebugLevel >= 5)
                            {
                                int reqnum = WebUtil.RequestNumber++;
                                WebUtil.LogOutgoingDetail("REST POST", responseStr);
                            }
                        }
                    }
                }
            }
            catch (WebException e)
            {
                m_log.WarnFormat("[REST]: POST {0} failed with status {1} and message {2}",
                                  _request.RequestUri, e.Status, e.Message);
                return;
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[REST]: AsyncPOST {0} failed with exception {1} {2}",
                                _request.RequestUri, e.Message, e.StackTrace);
                return;
            }
        }

    }

    internal class SimpleAsyncResult : IAsyncResult
    {
        private readonly AsyncCallback m_callback;

        /// <summary>
        /// Is process completed?
        /// </summary>
        /// <remarks>Should really be boolean, but VolatileRead has no boolean method</remarks>
        private byte m_completed;

        /// <summary>
        /// Did process complete synchronously?
        /// </summary>
        /// <remarks>I have a hard time imagining a scenario where this is the case, again, same issue about
        /// booleans and VolatileRead as m_completed
        /// </remarks>
        private byte m_completedSynchronously;

        private readonly object m_asyncState;
        private ManualResetEvent m_waitHandle;
        private Exception m_exception;

        internal SimpleAsyncResult(AsyncCallback cb, object state)
        {
            m_callback = cb;
            m_asyncState = state;
            m_completed = 0;
            m_completedSynchronously = 1;
        }

        #region IAsyncResult Members

        public object AsyncState
        {
            get { return m_asyncState; }
        }

        public WaitHandle AsyncWaitHandle
        {
            get
            {
                if (m_waitHandle == null)
                {
                    bool done = IsCompleted;
                    ManualResetEvent mre = new ManualResetEvent(done);
                    if (Interlocked.CompareExchange(ref m_waitHandle, mre, null) != null)
                    {
                        mre.Close();
                    }
                    else
                    {
                        if (!done && IsCompleted)
                        {
                            m_waitHandle.Set();
                        }
                    }
                }

                return m_waitHandle;
            }
        }


        public bool CompletedSynchronously
        {
            get { return Thread.VolatileRead(ref m_completedSynchronously) == 1; }
        }


        public bool IsCompleted
        {
            get { return Thread.VolatileRead(ref m_completed) == 1; }
        }

        #endregion

        #region class Methods

        internal void SetAsCompleted(bool completedSynchronously)
        {
            m_completed = 1;
            if (completedSynchronously)
                m_completedSynchronously = 1;
            else
                m_completedSynchronously = 0;

            SignalCompletion();
        }

        internal void HandleException(Exception e, bool completedSynchronously)
        {
            m_completed = 1;
            if (completedSynchronously)
                m_completedSynchronously = 1;
            else
                m_completedSynchronously = 0;
            m_exception = e;

            SignalCompletion();
        }

        private void SignalCompletion()
        {
            if (m_waitHandle != null) m_waitHandle.Set();

            if (m_callback != null) m_callback(this);
        }

        public void EndInvoke()
        {
            // This method assumes that only 1 thread calls EndInvoke
            if (!IsCompleted)
            {
                // If the operation isn't done, wait for it
                AsyncWaitHandle.WaitOne();
                AsyncWaitHandle.Close();
                m_waitHandle.Close();
                m_waitHandle = null; // Allow early GC
            }

            // Operation is done: if an exception occured, throw it
            if (m_exception != null) throw m_exception;
        }

        #endregion
    }

    internal class AsyncResult<T> : SimpleAsyncResult
    {
        private T m_result = default(T);

        public AsyncResult(AsyncCallback asyncCallback, Object state) :
            base(asyncCallback, state)
        {
        }

        public void SetAsCompleted(T result, bool completedSynchronously)
        {
            // Save the asynchronous operation's result
            m_result = result;

            // Tell the base class that the operation completed
            // sucessfully (no exception)
            base.SetAsCompleted(completedSynchronously);
        }

        public new T EndInvoke()
        {
            base.EndInvoke();
            return m_result;
        }
    }

}
