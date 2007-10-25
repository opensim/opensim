using System;
using System.IO;
using System.Net;
using System.Web;
using System.Text;
using System.Collections.Generic;
using System.Threading;

namespace OpenSim.Framework.RestClient
{
    /// <summary>
    /// Implementation of a generic REST client
    /// </summary>
    /// <remarks>
    /// This class is a generic implementation of a REST (Representational State Transfer) web service. This
    /// class is designed to execute both synchroneously and asynchroneously.
    /// 
    /// Internally the implementation works as a two stage asynchroneous web-client.
    /// When the request is initiated, RestClient will query asynchroneously for for a web-response,
    /// sleeping until the initial response is returned by the server. Once the initial response is retrieved
    /// the second stage of asynchroneous requests will be triggered, in an attempt to read of the response
    /// object into a memorystream as a sequence of asynchroneous reads.
    /// 
    /// The asynchronisity of RestClient is designed to move as much processing into the back-ground, allowing 
    /// other threads to execute, while it waits for a response from the web-service. RestClient it self, can be
    /// invoked by the caller in either synchroneous mode or asynchroneous mode.
    /// </remarks>
    public class RestClient 
    {
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
        MemoryStream _resource;

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
        const int DefaultTimeout = 10 * 1000; // 10 seconds timeout

        /// <summary>
        /// Default Buffer size of a block requested from the web-server
        /// </summary>
        const int BufferSize = 4096; // Read blocks of 4 KB.


        /// <summary>
        /// if an exception occours during async processing, we need to save it, so it can be 
        /// rethrown on the primary thread;
        /// </summary>
        private Exception _asyncException;

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
        }

        /// <summary>
        /// Add a path element to the query, e.g. assets
        /// </summary>
        /// <param name="element">path entry</param>
        public void AddResourcePath(string element)
        {
            if(isSlashed(element))
                _pathElements.Add(element.Substring(0, element.Length-1));
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
            _parameterElements.Add(HttpUtility.UrlEncode(name), HttpUtility.UrlEncode(value));
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
        private bool isSlashed(string s)
        {
            return s.Substring(s.Length - 1, 1) == "/";
        }

        /// <summary>
        /// return a slash or blank. A slash will be returned if the string does not contain one
        /// </summary>
        /// <param name="s">stromg to be examined</param>
        /// <returns>slash '/' if not already present</returns>
        private string slash(string s)
        {
            return isSlashed(s) ? "" : "/";
        }

        /// <summary>
        /// Build a Uri based on the intial Url, path elements and parameters
        /// </summary>
        /// <returns>fully constructed Uri</returns>
        Uri buildUri()
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
                } else
                    sb.Append("&");

                sb.Append(kv.Key);
                if (kv.Value != null && kv.Value.Length != 0)
                {
                    sb.Append("=");
                    sb.Append(kv.Value);
                }
            }
            return new Uri(sb.ToString());
        }

        /// <summary>
        /// Async method, invoked when a block of data has been received from the service
        /// </summary>
        /// <param name="ar"></param>
        private void StreamIsReadyDelegate(IAsyncResult ar)
        {
            try
            {
                Stream s = (Stream)ar.AsyncState;
                int read = s.EndRead(ar);

                // Read the HTML page and then print it to the console.
                if (read > 0)
                {
                    _resource.Write(_readbuf, 0, read);
                    IAsyncResult asynchronousResult = s.BeginRead(_readbuf, 0, BufferSize, new AsyncCallback(StreamIsReadyDelegate), s);

                    // TODO! Implement timeout, without killing the server
                    //ThreadPool.RegisterWaitForSingleObject(asynchronousResult.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), _request, DefaultTimeout, true);
                    return;
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

        /// <summary>
        /// Async method, invoked when the intial response if received from the server
        /// </summary>
        /// <param name="ar"></param>
        private void ResponseIsReadyDelegate(IAsyncResult ar)
        {
            try
            {
                // grab response
                WebRequest wr = (WebRequest)ar.AsyncState;
                _response = (HttpWebResponse)wr.EndGetResponse(ar);

                // get response stream, and setup async reading
                Stream s = _response.GetResponseStream();
                IAsyncResult asynchronousResult = s.BeginRead(_readbuf, 0, BufferSize, new AsyncCallback(StreamIsReadyDelegate), s);

                // TODO! Implement timeout, without killing the server
                // wait until completed, or we timed out
                // ThreadPool.RegisterWaitForSingleObject(asynchronousResult.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), _request, DefaultTimeout, true);
            }
            catch (Exception e)
            {
                _allDone.Set();
                _asyncException = e;
            }
        }

        // Abort the request if the timer fires.
        private static void TimeoutCallback(object state, bool timedOut)
        {
            if (timedOut)
            {
                HttpWebRequest request = state as HttpWebRequest;
                if (request != null)
                {
                    request.Abort();
                }
            }
        }

        /// <summary>
        /// Perform synchroneous request
        /// </summary>
        public Stream Request()
        {
            _request = (HttpWebRequest)WebRequest.Create(buildUri());
            _request.KeepAlive = false;
            _request.ContentType = "text/html";
            _request.Timeout = 200;
            _asyncException = null;

            IAsyncResult responseAsyncResult = _request.BeginGetResponse(new AsyncCallback(ResponseIsReadyDelegate), _request);

            // TODO! Implement timeout, without killing the server
            // this line implements the timeout, if there is a timeout, the callback fires and the request becomes aborted
            //ThreadPool.RegisterWaitForSingleObject(responseAsyncResult.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), _request, DefaultTimeout, true);

            _allDone.WaitOne();
            if(_response != null)
                _response.Close();
            if (_asyncException != null)
                throw _asyncException;
            return _resource;
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
            AsyncResult<Stream> ar = (AsyncResult<Stream>)asyncResult;

            // Wait for operation to complete, then return result or 
            // throw exception
            return ar.EndInvoke();
        }

        private void RequestHelper(Object asyncResult)
        {
            // We know that it's really an AsyncResult<DateTime> object
            AsyncResult<Stream> ar = (AsyncResult<Stream>)asyncResult;
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
