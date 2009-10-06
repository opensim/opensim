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

namespace OpenSim.Framework.Communications
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
    public class RestClient
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
        /// MemoryStream representing the resultiong resource
        /// </summary>
        private Stream _resource;

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
        public static ManualResetEvent _allDone = new ManualResetEvent(false);

        /// <summary>
        /// Default time out period
        /// </summary>
        //private const int DefaultTimeout = 10*1000; // 10 seconds timeout

        /// <summary>
        /// Default Buffer size of a block requested from the web-server
        /// </summary>
        private const int BufferSize = 4096; // Read blocks of 4 KB.


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

        /// <summary>
        /// Add a path element to the query, e.g. assets
        /// </summary>
        /// <param name="element">path entry</param>
        public void AddResourcePath(string element)
        {
            if (isSlashed(element))
                _pathElements.Add(element.Substring(0, element.Length - 1));
            else
                _pathElements.Add(element);
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
        /// True if string contains a trailing slash '/'
        /// </summary>
        /// <param name="s">string to be examined</param>
        /// <returns>true if slash is present</returns>
        private static bool isSlashed(string s)
        {
            return s.Substring(s.Length - 1, 1) == "/";
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
                    _allDone.Set();
                }
            }
            catch (Exception e)
            {
                _allDone.Set();
                _asyncException = e;
            }
        }

        #endregion Async communications with server

        /// <summary>
        /// Perform a synchronous request
        /// </summary>
        public Stream Request()
        {
            lock (_lock)
            {
                _request = (HttpWebRequest) WebRequest.Create(buildUri());
                _request.KeepAlive = false;
                _request.ContentType = "application/xml";
                _request.Timeout = 200000;
                _request.Method = RequestMethod;
                _asyncException = null;

//                IAsyncResult responseAsyncResult = _request.BeginGetResponse(new AsyncCallback(ResponseIsReadyDelegate), _request);
                try
                {
                    _response = (HttpWebResponse) _request.GetResponse();
                }
                catch (WebException e)
                {
                    HttpWebResponse errorResponse = e.Response as HttpWebResponse;
                    if (null != errorResponse && HttpStatusCode.NotFound == errorResponse.StatusCode)
                    {
                        m_log.Warn("[REST CLIENT] Resource not found (404)");
                    }
                    else
                    {
                        m_log.Error("[REST CLIENT] Error fetching resource from server " + _request.Address.ToString());
                        m_log.Debug(e.ToString());
                    }

                    return null;
                }

                Stream src = _response.GetResponseStream();
                int length = src.Read(_readbuf, 0, BufferSize);
                while (length > 0)
                {
                    _resource.Write(_readbuf, 0, length);
                    length = src.Read(_readbuf, 0, BufferSize);
                }


                // TODO! Implement timeout, without killing the server
                // this line implements the timeout, if there is a timeout, the callback fires and the request becomes aborted
                //ThreadPool.RegisterWaitForSingleObject(responseAsyncResult.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), _request, DefaultTimeout, true);

//                _allDone.WaitOne();
                if (_response != null)
                    _response.Close();
                if (_asyncException != null)
                    throw _asyncException;

                if (_resource != null)
                {
                    _resource.Flush();
                    _resource.Seek(0, SeekOrigin.Begin);
                }

                return _resource;
            }
        }

        public Stream Request(Stream src)
        {
            _request = (HttpWebRequest) WebRequest.Create(buildUri());
            _request.KeepAlive = false;
            _request.ContentType = "application/xml";
            _request.Timeout = 900000;
            _request.Method = RequestMethod;
            _asyncException = null;
            _request.ContentLength = src.Length;

            m_log.InfoFormat("[REST]: Request Length {0}", _request.ContentLength);
            m_log.InfoFormat("[REST]: Sending Web Request {0}", buildUri());
            src.Seek(0, SeekOrigin.Begin);
            m_log.Info("[REST]: Seek is ok");
            Stream dst = _request.GetRequestStream();
            m_log.Info("[REST]: GetRequestStream is ok");

            byte[] buf = new byte[1024];
            int length = src.Read(buf, 0, 1024);
            m_log.Info("[REST]: First Read is ok");
            while (length > 0)
            {
                dst.Write(buf, 0, length);
                length = src.Read(buf, 0, 1024);
            }

            _response = (HttpWebResponse) _request.GetResponse();

//            IAsyncResult responseAsyncResult = _request.BeginGetResponse(new AsyncCallback(ResponseIsReadyDelegate), _request);

            // TODO! Implement timeout, without killing the server
            // this line implements the timeout, if there is a timeout, the callback fires and the request becomes aborted
            //ThreadPool.RegisterWaitForSingleObject(responseAsyncResult.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), _request, DefaultTimeout, true);

            return null;
        }

        #region Async Invocation

        public IAsyncResult BeginRequest(AsyncCallback callback, object state)
        {
            /// <summary>
            /// In case, we are invoked asynchroneously this object will keep track of the state
            /// </summary>
            AsyncResult<Stream> ar = new AsyncResult<Stream>(callback, state);
            ThreadPool.QueueUserWorkItem(RequestHelper, ar);
            return ar;
        }

        public Stream EndRequest(IAsyncResult asyncResult)
        {
            AsyncResult<Stream> ar = (AsyncResult<Stream>) asyncResult;

            // Wait for operation to complete, then return result or
            // throw exception
            return ar.EndInvoke();
        }

        private void RequestHelper(Object asyncResult)
        {
            // We know that it's really an AsyncResult<DateTime> object
            AsyncResult<Stream> ar = (AsyncResult<Stream>) asyncResult;
            try
            {
                // Perform the operation; if sucessful set the result
                Stream s = Request();
                ar.SetAsCompleted(s, false);
            }
            catch (Exception e)
            {
                // If operation fails, set the exception
                ar.HandleException(e, false);
            }
        }

        #endregion Async Invocation
    }
}
