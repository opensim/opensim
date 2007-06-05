/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
namespace Nwc.XmlRpc
{
    using System;
    using System.Collections;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Xml;

    /// <summary>A restricted HTTP server for use with XML-RPC.</summary>
    /// <remarks>It only handles POST requests, and only POSTs representing XML-RPC calls.
    /// In addition to dispatching requests it also provides a registry for request handlers. 
    /// </remarks>
    public class XmlRpcServer : IEnumerable
    {
#pragma warning disable 0414 // disable "private field assigned but not used"
        const int RESPONDER_COUNT = 10;
        private TcpListener _myListener;
        private int _port;
        private IPAddress _address;
        private IDictionary _handlers;
        private XmlRpcSystemObject _system;
        private WaitCallback _wc;
#pragma warning restore 0414

        ///<summary>Constructor with port and address.</summary>
        ///<remarks>This constructor sets up a TcpListener listening on the
        ///given port and address. It also calls a Thread on the method StartListen().</remarks>
        ///<param name="address"><c>IPAddress</c> value of the address to listen on.</param>
        ///<param name="port"><c>Int</c> value of the port to listen on.</param>
        public XmlRpcServer(IPAddress address, int port)
        {
            _port = port;
            _address = address;
            _handlers = new Hashtable();
            _system = new XmlRpcSystemObject(this);
            _wc = new WaitCallback(WaitCallback);
        }

        ///<summary>Basic constructor.</summary>
        ///<remarks>This constructor sets up a TcpListener listening on the
        ///given port. It also calls a Thread on the method StartListen(). IPAddress.Any
        ///is assumed as the address here.</remarks>
        ///<param name="port"><c>Int</c> value of the port to listen on.</param>
        public XmlRpcServer(int port) : this(IPAddress.Any, port) { }

        /// <summary>Start the server.</summary>
        public void Start()
        {
            try
            {
                Stop();
                //start listing on the given port
                //	    IPAddress addr = IPAddress.Parse("127.0.0.1");
                lock (this)
                {
                    _myListener = new TcpListener(IPAddress.Any, _port);
                    _myListener.Start();
                    //start the thread which calls the method 'StartListen'
                    Thread th = new Thread(new ThreadStart(StartListen));
                    th.Start();
                }
            }
            catch (Exception e)
            {
                Logger.WriteEntry("An Exception Occurred while Listening :" + e.ToString(), LogLevel.Error);
            }
        }

        /// <summary>Stop the server.</summary>
        public void Stop()
        {
            try
            {
                if (_myListener != null)
                {
                    lock (this)
                    {
                        _myListener.Stop();
                        _myListener = null;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.WriteEntry("An Exception Occurred while stopping :" +
                      e.ToString(), LogLevel.Error);
            }
        }

        /// <summary>Get an enumeration of my XML-RPC handlers.</summary>
        /// <returns><c>IEnumerable</c> the handler enumeration.</returns>
        public IEnumerator GetEnumerator()
        {
            return _handlers.GetEnumerator();
        }

        /// <summary>Retrieve a handler by name.</summary>
        /// <param name="name"><c>String</c> naming a handler</param>
        /// <returns><c>Object</c> that is the handler.</returns>
        public Object this[String name]
        {
            get { return _handlers[name]; }
        }

        ///<summary>
        ///This method Accepts new connections and dispatches them when appropriate.
        ///</summary>
        public void StartListen()
        {
            while (true && _myListener != null)
            {
                //Accept a new connection
                XmlRpcResponder responder = new XmlRpcResponder(this, _myListener.AcceptTcpClient());
                ThreadPool.QueueUserWorkItem(_wc, responder);
            }
        }


        ///<summary>
        ///Add an XML-RPC handler object by name.
        ///</summary>
        ///<param name="name"><c>String</c> XML-RPC dispatch name of this object.</param>
        ///<param name="obj"><c>Object</c> The object that is the XML-RPC handler.</param>
        public void Add(String name, Object obj)
        {
            _handlers.Add(name, obj);
        }

        ///<summary>Return a C# object.method name for and XML-RPC object.method name pair.</summary>
        ///<param name="methodName">The XML-RPC object.method.</param>
        ///<returns><c>String</c> of form object.method for the underlying C# method.</returns>
        public String MethodName(String methodName)
        {
            int dotAt = methodName.LastIndexOf('.');

            if (dotAt == -1)
            {
                throw new XmlRpcException(XmlRpcErrorCodes.SERVER_ERROR_METHOD,
                              XmlRpcErrorCodes.SERVER_ERROR_METHOD_MSG + ": Bad method name " + methodName);
            }

            String objectName = methodName.Substring(0, dotAt);
            Object target = _handlers[objectName];

            if (target == null)
            {
                throw new XmlRpcException(XmlRpcErrorCodes.SERVER_ERROR_METHOD,
                              XmlRpcErrorCodes.SERVER_ERROR_METHOD_MSG + ": Object " + objectName + " not found");
            }

            return target.GetType().FullName + "." + methodName.Substring(dotAt + 1);
        }

        ///<summary>Invoke a method described in a request.</summary>
        ///<param name="req"><c>XmlRpcRequest</c> containing a method descriptions.</param>
        /// <seealso cref="XmlRpcSystemObject.Invoke"/>
        /// <seealso cref="XmlRpcServer.Invoke(String,String,IList)"/>
        public Object Invoke(XmlRpcRequest req)
        {
            return Invoke(req.MethodNameObject, req.MethodNameMethod, req.Params);
        }

        ///<summary>Invoke a method on a named handler.</summary>
        ///<param name="objectName"><c>String</c> The name of the handler.</param>
        ///<param name="methodName"><c>String</c> The name of the method to invoke on the handler.</param>
        ///<param name="parameters"><c>IList</c> The parameters to invoke the method with.</param>
        /// <seealso cref="XmlRpcSystemObject.Invoke"/>
        public Object Invoke(String objectName, String methodName, IList parameters)
        {
            Object target = _handlers[objectName];

            if (target == null)
            {
                throw new XmlRpcException(XmlRpcErrorCodes.SERVER_ERROR_METHOD,
                              XmlRpcErrorCodes.SERVER_ERROR_METHOD_MSG + ": Object " + objectName + " not found");
            }

            return XmlRpcSystemObject.Invoke(target, methodName, parameters);
        }

        /// <summary>The method the thread pool invokes when a thread is available to handle an HTTP request.</summary>
        /// <param name="responder">TcpClient from the socket accept.</param>
        public void WaitCallback(object responder)
        {
            XmlRpcResponder resp = (XmlRpcResponder)responder;

            if (resp.HttpReq.HttpMethod == "POST")
            {
                try
                {
                    resp.Respond();
                }
                catch (Exception e)
                {
                    Logger.WriteEntry("Failed on post: " + e, LogLevel.Error);
                }
            }
            else
            {
                Logger.WriteEntry("Only POST methods are supported: " + resp.HttpReq.HttpMethod +
                          " ignored", LogLevel.Error);
            }

            resp.Close();
        }

        /// <summary>
        /// This function send the Header Information to the client (Browser)
        /// </summary>
        /// <param name="sHttpVersion">HTTP Version</param>
        /// <param name="sMIMEHeader">Mime Type</param>
        /// <param name="iTotBytes">Total Bytes to be sent in the body</param>
        /// <param name="sStatusCode"></param>
        /// <param name="output">Socket reference</param>
        static public void HttpHeader(string sHttpVersion, string sMIMEHeader, long iTotBytes, string sStatusCode, TextWriter output)
        {
            String sBuffer = "";

            // if Mime type is not provided set default to text/html
            if (sMIMEHeader.Length == 0)
            {
                sMIMEHeader = "text/html";  // Default Mime Type is text/html
            }

            sBuffer += sHttpVersion + sStatusCode + "\r\n";
            sBuffer += "Connection: close\r\n";
            if (iTotBytes > 0)
                sBuffer += "Content-Length: " + iTotBytes + "\r\n";
            sBuffer += "Server: XmlRpcServer \r\n";
            sBuffer += "Content-Type: " + sMIMEHeader + "\r\n";
            sBuffer += "\r\n";

            output.Write(sBuffer);
        }
    }
}
