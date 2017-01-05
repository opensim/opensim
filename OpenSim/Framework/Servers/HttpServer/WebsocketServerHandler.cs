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
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using HttpServer;

namespace OpenSim.Framework.Servers.HttpServer
{
    // Sealed class.  If you're going to unseal it, implement IDisposable.
    /// <summary>
    /// This class implements websockets.    It grabs the network context from C#Webserver and utilizes it directly as a tcp streaming service
    /// </summary>
    public sealed class WebSocketHttpServerHandler : BaseRequestHandler
    {

        private class WebSocketState
        {
            public List<byte> ReceivedBytes;
            public int ExpectedBytes;
            public WebsocketFrameHeader Header;
            public bool FrameComplete;
            public WebSocketFrame ContinuationFrame;
        }

        /// <summary>
        /// Binary Data will trigger this event
        /// </summary>
        public event DataDelegate OnData;

        /// <summary>
        /// Textual Data will trigger this event
        /// </summary>
        public event TextDelegate OnText;

        /// <summary>
        /// A ping request form the other side will trigger this event.
        /// This class responds to the ping automatically.  You shouldn't send a pong.
        /// it's informational.
        /// </summary>
        public event PingDelegate OnPing;

        /// <summary>
        /// This is a response to a ping you sent.
        /// </summary>
        public event PongDelegate OnPong;

        /// <summary>
        /// This is a regular HTTP Request...    This may be removed in the future.
        /// </summary>
//        public event RegularHttpRequestDelegate OnRegularHttpRequest;

        /// <summary>
        /// When the upgrade from a HTTP request to a Websocket is completed, this will be fired
        /// </summary>
        public event UpgradeCompletedDelegate OnUpgradeCompleted;

        /// <summary>
        /// If the upgrade failed, this will be fired
        /// </summary>
        public event UpgradeFailedDelegate OnUpgradeFailed;

        /// <summary>
        /// When the websocket is closed, this will be fired.
        /// </summary>
        public event CloseDelegate OnClose;

        /// <summary>
        /// Set this delegate to allow your module to validate the origin of the
        /// Websocket request.  Primary line of defense against cross site scripting
        /// </summary>
        public ValidateHandshake HandshakeValidateMethodOverride = null;

        private ManualResetEvent _receiveDone = new ManualResetEvent(false);

        private OSHttpRequest _request;
        private HTTPNetworkContext _networkContext;
        private IHttpClientContext _clientContext;

        private int _pingtime = 0;
        private byte[] _buffer;
        private int _bufferPosition;
        private int _bufferLength;
        private bool _closing;
        private bool _upgraded;
        private int _maxPayloadBytes = 41943040;
        private int _initialMsgTimeout = 0;
        private int _defaultReadTimeout = 10000;

        private const string HandshakeAcceptText =
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            "sec-websocket-accept: {0}\r\n\r\n";// +
           //"{1}";

        private const string HandshakeDeclineText =
            "HTTP/1.1 {0} {1}\r\n" +
            "Connection: close\r\n\r\n";

        /// <summary>
        /// Mysterious constant defined in RFC6455 to append to the client provided security key
        /// </summary>
        private const string WebsocketHandshakeAcceptHashConstant = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        public WebSocketHttpServerHandler(OSHttpRequest preq, IHttpClientContext pContext, int bufferlen)
            : base(preq.HttpMethod, preq.Url.OriginalString)
        {
            _request = preq;
            _networkContext = pContext.GiveMeTheNetworkStreamIKnowWhatImDoing();
            _networkContext.Stream.ReadTimeout = _defaultReadTimeout;
            _clientContext = pContext;
            _bufferLength = bufferlen;
            _buffer = new byte[_bufferLength];
        }

        // Sealed class implments destructor and an internal dispose method. complies with C# unmanaged resource best practices.
        ~WebSocketHttpServerHandler()
        {
            Dispose();

        }

        /// <summary>
        /// Sets the length of the stream buffer
        /// </summary>
        /// <param name="pChunk">Byte length.</param>
        public void SetChunksize(int pChunk)
        {
            if (!_upgraded)
            {
                _buffer = new byte[pChunk];
            }
            else
            {
                throw new InvalidOperationException("You must set the chunksize before the connection is upgraded");
            }
        }

        /// <summary>
        /// This is the famous nagle.
        /// </summary>
        public bool NoDelay_TCP_Nagle
        {
            get
            {
                if (_networkContext != null && _networkContext.Socket != null)
                {
                    return _networkContext.Socket.NoDelay;
                }
                else
                {
                    throw new InvalidOperationException("The socket has been shutdown");
                }
            }
            set
            {
                if (_networkContext != null && _networkContext.Socket != null)
                    _networkContext.Socket.NoDelay = value;
                else
                {
                    throw new InvalidOperationException("The socket has been shutdown");
                }
            }
        }

        /// <summary>
        /// This triggers the websocket to start the upgrade process...
        /// This is a Generalized Networking 'common sense' helper method.  Some people expect to call Start() instead
        /// of the more context appropriate HandshakeAndUpgrade()
        /// </summary>
        public void Start()
        {
            HandshakeAndUpgrade();
        }

        /// <summary>
        /// Max Payload Size in bytes.  Defaults to 40MB, but could be set upon connection before calling handshake and upgrade.
        /// </summary>
        public int MaxPayloadSize
        {
            get { return _maxPayloadBytes; }
            set { _maxPayloadBytes = value; }
        }

        /// <summary>
        /// Set this to the maximum amount of milliseconds to wait for the first complete message to be sent or received on the websocket after upgrading
        /// Default, it waits forever.  Use this when your Websocket consuming code opens a connection and waits for a message from the other side to avoid a Denial of Service vector.
        /// </summary>
        public int InitialMsgTimeout
        {
            get { return _initialMsgTimeout; }
            set { _initialMsgTimeout = value; }
        }

        /// <summary>
        /// This triggers the websocket start the upgrade process
        /// </summary>
        public void HandshakeAndUpgrade()
        {
            string webOrigin = string.Empty;
            string websocketKey = string.Empty;
            string acceptKey = string.Empty;
            string accepthost = string.Empty;
            if (!string.IsNullOrEmpty(_request.Headers["origin"]))
                webOrigin = _request.Headers["origin"];

            if (!string.IsNullOrEmpty(_request.Headers["sec-websocket-key"]))
                websocketKey = _request.Headers["sec-websocket-key"];

            if (!string.IsNullOrEmpty(_request.Headers["host"]))
                accepthost = _request.Headers["host"];

            if (string.IsNullOrEmpty(_request.Headers["upgrade"]))
            {
                FailUpgrade(OSHttpStatusCode.ClientErrorBadRequest, "no upgrade request submitted");
            }

            string connectionheader = _request.Headers["upgrade"];
            if (connectionheader.ToLower() != "websocket")
            {
                FailUpgrade(OSHttpStatusCode.ClientErrorBadRequest, "no connection upgrade request submitted");
            }

            // If the object consumer provided a method to validate the origin, we should call it and give the client a success or fail.
            // If not..  we should accept any.   The assumption here is that there would be no Websocket handlers registered in baseHTTPServer unless
            // Something asked for it...
            if (HandshakeValidateMethodOverride != null)
            {
                if (HandshakeValidateMethodOverride(webOrigin, websocketKey, accepthost))
                {
                    acceptKey = GenerateAcceptKey(websocketKey);
                    string rawaccept = string.Format(HandshakeAcceptText, acceptKey);
                    SendUpgradeSuccess(rawaccept);


                }
                else
                {
                    FailUpgrade(OSHttpStatusCode.ClientErrorForbidden, "Origin Validation Failed");
                }
            }
            else
            {
                acceptKey = GenerateAcceptKey(websocketKey);
                string rawaccept = string.Format(HandshakeAcceptText, acceptKey);
                SendUpgradeSuccess(rawaccept);
            }
        }
        public IPEndPoint GetRemoteIPEndpoint()
        {
            return _request.RemoteIPEndPoint;
        }

        /// <summary>
        /// Generates a handshake response key string based on the client's
        /// provided key to prove to the client that we're allowing the Websocket
        /// upgrade of our own free will and we were not coerced into doing it.
        /// </summary>
        /// <param name="key">Client provided security key</param>
        /// <returns></returns>
        private static string GenerateAcceptKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            string acceptkey = key + WebsocketHandshakeAcceptHashConstant;

            SHA1 hashobj = SHA1.Create();
            string ret = Convert.ToBase64String(hashobj.ComputeHash(Encoding.UTF8.GetBytes(acceptkey)));
            hashobj.Clear();

            return ret;
        }

        /// <summary>
        /// Informs the otherside that we accepted their upgrade request
        /// </summary>
        /// <param name="pHandshakeResponse">The HTTP 1.1 101 response that says Yay \o/ </param>
        private void SendUpgradeSuccess(string pHandshakeResponse)
        {
            // Create a new websocket state so we can keep track of data in between network reads.
            WebSocketState socketState = new WebSocketState() { ReceivedBytes = new List<byte>(), Header = WebsocketFrameHeader.HeaderDefault(), FrameComplete = true};

            byte[] bhandshakeResponse = Encoding.UTF8.GetBytes(pHandshakeResponse);




            try
            {
                if (_initialMsgTimeout > 0)
                {
                    _receiveDone.Reset();
                }
                // Begin reading the TCP stream before writing the Upgrade success message to the other side of the stream.
                _networkContext.Stream.BeginRead(_buffer, 0, _bufferLength, OnReceive, socketState);

                // Write the upgrade handshake success message
                _networkContext.Stream.Write(bhandshakeResponse, 0, bhandshakeResponse.Length);
                _networkContext.Stream.Flush();
                _upgraded = true;
                UpgradeCompletedDelegate d = OnUpgradeCompleted;
                if (d != null)
                    d(this, new UpgradeCompletedEventArgs());
                if (_initialMsgTimeout > 0)
                {
                    if (!_receiveDone.WaitOne(TimeSpan.FromMilliseconds(_initialMsgTimeout)))
                        Close(string.Empty);
                }
            }
            catch (IOException)
            {
                Close(string.Empty);
            }
            catch (ObjectDisposedException)
            {
                Close(string.Empty);
            }
        }

        /// <summary>
        /// The server has decided not to allow the upgrade to a websocket for some reason.  The Http 1.1 response that says Nay >:(
        /// </summary>
        /// <param name="pCode">HTTP Status reflecting the reason why</param>
        /// <param name="pMessage">Textual reason for the upgrade fail</param>
        private void FailUpgrade(OSHttpStatusCode pCode, string pMessage )
        {
            string handshakeResponse = string.Format(HandshakeDeclineText, (int)pCode, pMessage.Replace("\n", string.Empty).Replace("\r", string.Empty));
            byte[] bhandshakeResponse = Encoding.UTF8.GetBytes(handshakeResponse);
            _networkContext.Stream.Write(bhandshakeResponse, 0, bhandshakeResponse.Length);
            _networkContext.Stream.Flush();
            _networkContext.Stream.Dispose();

            UpgradeFailedDelegate d = OnUpgradeFailed;
            if (d != null)
                d(this,new UpgradeFailedEventArgs());
        }


        /// <summary>
        /// This is our ugly Async OnReceive event handler.
        /// This chunks the input stream based on the length of the provided buffer and processes out
        /// as many frames as it can.   It then moves the unprocessed data to the beginning of the buffer.
        /// </summary>
        /// <param name="ar">Our Async State from beginread</param>
        private void OnReceive(IAsyncResult ar)
        {
            WebSocketState _socketState = ar.AsyncState as WebSocketState;
            try
            {
                int bytesRead = _networkContext.Stream.EndRead(ar);
                if (bytesRead == 0)
                {
                    // Do Disconnect
                    _networkContext.Stream.Dispose();
                    _networkContext = null;
                    return;
                }
                _bufferPosition += bytesRead;

                if (_bufferPosition > _bufferLength)
                {
                    // Message too big for chunksize..   not sure how this happened...
                    //Close(string.Empty);
                }

                int offset = 0;
                bool headerread = true;
                int headerforwardposition = 0;
                while (headerread && offset < bytesRead)
                {
                    if (_socketState.FrameComplete)
                    {
                        WebsocketFrameHeader pheader = WebsocketFrameHeader.ZeroHeader;

                        headerread = WebSocketReader.TryReadHeader(_buffer, offset, _bufferPosition - offset, out pheader,
                                                                   out headerforwardposition);
                        offset += headerforwardposition;

                        if (headerread)
                        {
                            _socketState.FrameComplete = false;
                            if (pheader.PayloadLen > (ulong) _maxPayloadBytes)
                            {
                                Close("Invalid Payload size");

                                return;
                            }
                            if (pheader.PayloadLen > 0)
                            {
                                if ((int) pheader.PayloadLen > _bufferPosition - offset)
                                {
                                    byte[] writebytes = new byte[_bufferPosition - offset];

                                    Buffer.BlockCopy(_buffer, offset, writebytes, 0, (int) _bufferPosition - offset);
                                    _socketState.ExpectedBytes = (int) pheader.PayloadLen;
                                    _socketState.ReceivedBytes.AddRange(writebytes);
                                    _socketState.Header = pheader; // We need to add the header so that we can unmask it
                                    offset += (int) _bufferPosition - offset;
                                }
                                else
                                {
                                    byte[] writebytes = new byte[pheader.PayloadLen];
                                    Buffer.BlockCopy(_buffer, offset, writebytes, 0, (int) pheader.PayloadLen);
                                    WebSocketReader.Mask(pheader.Mask, writebytes);
                                    pheader.IsMasked = false;
                                    _socketState.FrameComplete = true;
                                    _socketState.ReceivedBytes.AddRange(writebytes);
                                    _socketState.Header = pheader;
                                    offset += (int) pheader.PayloadLen;
                                }
                            }
                            else
                            {
                                pheader.Mask = 0;
                                _socketState.FrameComplete = true;
                                _socketState.Header = pheader;
                            }

                            if (_socketState.FrameComplete)
                            {
                                ProcessFrame(_socketState);
                                _socketState.Header.SetDefault();
                                _socketState.ReceivedBytes.Clear();
                                _socketState.ExpectedBytes = 0;

                            }
                        }
                    }
                    else
                    {
                        WebsocketFrameHeader frameHeader = _socketState.Header;
                        int bytesleft = _socketState.ExpectedBytes - _socketState.ReceivedBytes.Count;

                        if (bytesleft > _bufferPosition)
                        {
                            byte[] writebytes = new byte[_bufferPosition];

                            Buffer.BlockCopy(_buffer, offset, writebytes, 0, (int) _bufferPosition);
                            _socketState.ReceivedBytes.AddRange(writebytes);
                            _socketState.Header = frameHeader; // We need to add the header so that we can unmask it
                            offset += (int) _bufferPosition;
                        }
                        else
                        {
                            byte[] writebytes = new byte[_bufferPosition];
                            Buffer.BlockCopy(_buffer, offset, writebytes, 0, (int) _bufferPosition);
                            _socketState.FrameComplete = true;
                            _socketState.ReceivedBytes.AddRange(writebytes);
                            _socketState.Header = frameHeader;
                            offset += (int) _bufferPosition;
                        }
                        if (_socketState.FrameComplete)
                        {
                            ProcessFrame(_socketState);
                            _socketState.Header.SetDefault();
                            _socketState.ReceivedBytes.Clear();
                            _socketState.ExpectedBytes = 0;
                            // do some processing
                        }
                    }
                }
                if (offset > 0)
                {
                    // If the buffer is maxed out..  we can just move the cursor.   Nothing to move to the beginning.
                    if (offset <_buffer.Length)
                        Buffer.BlockCopy(_buffer, offset, _buffer, 0, _bufferPosition - offset);
                    _bufferPosition -= offset;
                }
                if (_networkContext.Stream != null && _networkContext.Stream.CanRead && !_closing)
                {
                    _networkContext.Stream.BeginRead(_buffer, _bufferPosition, _bufferLength - _bufferPosition, OnReceive,
                                                    _socketState);
                }
                else
                {
                    // We can't read the stream anymore...
                }
            }
            catch (IOException)
            {
                Close(string.Empty);
            }
            catch (ObjectDisposedException)
            {
                Close(string.Empty);
            }
        }

        /// <summary>
        /// Sends a string to the other side
        /// </summary>
        /// <param name="message">the string message that is to be sent</param>
        public void SendMessage(string message)
        {
            if (_initialMsgTimeout > 0)
            {
                _receiveDone.Set();
                _initialMsgTimeout = 0;
            }
            byte[] messagedata = Encoding.UTF8.GetBytes(message);
            WebSocketFrame textMessageFrame = new WebSocketFrame() { Header = WebsocketFrameHeader.HeaderDefault(), WebSocketPayload = messagedata };
            textMessageFrame.Header.Opcode = WebSocketReader.OpCode.Text;
            textMessageFrame.Header.IsEnd = true;
            SendSocket(textMessageFrame.ToBytes());

        }

        public void SendData(byte[] data)
        {
            if (_initialMsgTimeout > 0)
            {
                _receiveDone.Set();
                _initialMsgTimeout = 0;
            }
            WebSocketFrame dataMessageFrame = new WebSocketFrame() { Header = WebsocketFrameHeader.HeaderDefault(), WebSocketPayload = data};
            dataMessageFrame.Header.IsEnd = true;
            dataMessageFrame.Header.Opcode = WebSocketReader.OpCode.Binary;
            SendSocket(dataMessageFrame.ToBytes());

        }

        /// <summary>
        /// Writes raw bytes to the websocket.   Unframed data will cause disconnection
        /// </summary>
        /// <param name="data"></param>
        private void SendSocket(byte[] data)
        {
            if (!_closing)
            {
                try
                {

                    _networkContext.Stream.Write(data, 0, data.Length);
                }
                catch (IOException)
                {

                }
            }
        }

        /// <summary>
        /// Sends a Ping check to the other side.  The other side SHOULD respond as soon as possible with a pong frame.   This interleaves with incoming fragmented frames.
        /// </summary>
        public void SendPingCheck()
        {
            if (_initialMsgTimeout > 0)
            {
                _receiveDone.Set();
                _initialMsgTimeout = 0;
            }
            WebSocketFrame pingFrame = new WebSocketFrame() { Header = WebsocketFrameHeader.HeaderDefault(), WebSocketPayload = new byte[0] };
            pingFrame.Header.Opcode = WebSocketReader.OpCode.Ping;
            pingFrame.Header.IsEnd = true;
            _pingtime = Util.EnvironmentTickCount();
            SendSocket(pingFrame.ToBytes());
        }

        /// <summary>
        /// Closes the websocket connection.   Sends a close message to the other side if it hasn't already done so.
        /// </summary>
        /// <param name="message"></param>
        public void Close(string message)
        {
            if (_initialMsgTimeout > 0)
            {
                _receiveDone.Set();
                _initialMsgTimeout = 0;
            }
            if (_networkContext == null)
                return;
            if (_networkContext.Stream != null)
            {
                if (_networkContext.Stream.CanWrite)
            {
                byte[] messagedata = Encoding.UTF8.GetBytes(message);
                WebSocketFrame closeResponseFrame = new WebSocketFrame()
                                                        {
                                                            Header = WebsocketFrameHeader.HeaderDefault(),
                                                            WebSocketPayload = messagedata
                                                        };
                closeResponseFrame.Header.Opcode = WebSocketReader.OpCode.Close;
                closeResponseFrame.Header.PayloadLen = (ulong) messagedata.Length;
                closeResponseFrame.Header.IsEnd = true;
                SendSocket(closeResponseFrame.ToBytes());
            }
                }
            CloseDelegate closeD = OnClose;
            if (closeD != null)
            {
                closeD(this, new CloseEventArgs());
            }

            _closing = true;
        }

        /// <summary>
        /// Processes a websocket frame and triggers consumer events
        /// </summary>
        /// <param name="psocketState">We need to modify the websocket state here depending on the frame</param>
        private void ProcessFrame(WebSocketState psocketState)
        {
            if (psocketState.Header.IsMasked)
            {
                byte[] unmask = psocketState.ReceivedBytes.ToArray();
                WebSocketReader.Mask(psocketState.Header.Mask, unmask);
                psocketState.ReceivedBytes = new List<byte>(unmask);
            }
            if (psocketState.Header.Opcode != WebSocketReader.OpCode.Continue  && _initialMsgTimeout > 0)
            {
                _receiveDone.Set();
                _initialMsgTimeout = 0;
            }
            switch (psocketState.Header.Opcode)
            {
                case WebSocketReader.OpCode.Ping:
                    PingDelegate pingD = OnPing;
                    if (pingD != null)
                    {
                        pingD(this, new PingEventArgs());
                    }

                    WebSocketFrame pongFrame = new WebSocketFrame(){Header = WebsocketFrameHeader.HeaderDefault(),WebSocketPayload = new byte[0]};
                    pongFrame.Header.Opcode = WebSocketReader.OpCode.Pong;
                    pongFrame.Header.IsEnd = true;
                    SendSocket(pongFrame.ToBytes());
                    break;
                case WebSocketReader.OpCode.Pong:

                    PongDelegate pongD = OnPong;
                    if (pongD != null)
                    {
                        pongD(this, new PongEventArgs(){PingResponseMS = Util.EnvironmentTickCountSubtract(Util.EnvironmentTickCount(),_pingtime)});
                    }
                    break;
                case WebSocketReader.OpCode.Binary:
                    if (!psocketState.Header.IsEnd) // Not done, so we need to store this and wait for the end frame.
                    {
                        psocketState.ContinuationFrame = new WebSocketFrame
                                                             {
                                                                 Header = psocketState.Header,
                                                                 WebSocketPayload =
                                                                     psocketState.ReceivedBytes.ToArray()
                                                             };
                    }
                    else
                    {
                        // Send Done Event!
                        DataDelegate dataD = OnData;
                        if (dataD != null)
                        {
                            dataD(this,new WebsocketDataEventArgs(){Data = psocketState.ReceivedBytes.ToArray()});
                        }
                    }
                    break;
                case WebSocketReader.OpCode.Text:
                    if (!psocketState.Header.IsEnd) // Not done, so we need to store this and wait for the end frame.
                    {
                        psocketState.ContinuationFrame = new WebSocketFrame
                                                             {
                                                                 Header = psocketState.Header,
                                                                 WebSocketPayload =
                                                                     psocketState.ReceivedBytes.ToArray()
                                                             };
                    }
                    else
                    {
                        TextDelegate textD = OnText;
                        if (textD != null)
                        {
                            textD(this, new WebsocketTextEventArgs() { Data = Encoding.UTF8.GetString(psocketState.ReceivedBytes.ToArray()) });
                        }

                        // Send Done Event!
                    }
                    break;
                case WebSocketReader.OpCode.Continue:  // Continuation.  Multiple frames worth of data for one message.   Only valid when not using Control Opcodes
                    //Console.WriteLine("currhead " + psocketState.Header.IsEnd);
                    //Console.WriteLine("Continuation! " + psocketState.ContinuationFrame.Header.IsEnd);
                    byte[] combineddata = new byte[psocketState.ReceivedBytes.Count+psocketState.ContinuationFrame.WebSocketPayload.Length];
                    byte[] newdata = psocketState.ReceivedBytes.ToArray();
                    Buffer.BlockCopy(psocketState.ContinuationFrame.WebSocketPayload, 0, combineddata, 0, psocketState.ContinuationFrame.WebSocketPayload.Length);
                    Buffer.BlockCopy(newdata, 0, combineddata,
                                        psocketState.ContinuationFrame.WebSocketPayload.Length, newdata.Length);
                    psocketState.ContinuationFrame.WebSocketPayload = combineddata;
                    psocketState.Header.PayloadLen = (ulong)combineddata.Length;
                    if (psocketState.Header.IsEnd)
                    {
                        if (psocketState.ContinuationFrame.Header.Opcode == WebSocketReader.OpCode.Text)
                        {
                            // Send Done event
                            TextDelegate textD = OnText;
                            if (textD != null)
                            {
                                textD(this, new WebsocketTextEventArgs() { Data = Encoding.UTF8.GetString(combineddata) });
                            }
                        }
                        else if (psocketState.ContinuationFrame.Header.Opcode == WebSocketReader.OpCode.Binary)
                        {
                            // Send Done event
                            DataDelegate dataD = OnData;
                            if (dataD != null)
                            {
                                dataD(this, new WebsocketDataEventArgs() { Data = combineddata });
                            }
                        }
                        else
                        {
                            // protocol violation
                        }
                        psocketState.ContinuationFrame = null;
                    }
                    break;
                case WebSocketReader.OpCode.Close:
                    Close(string.Empty);

                    break;

            }
            psocketState.Header.SetDefault();
            psocketState.ReceivedBytes.Clear();
            psocketState.ExpectedBytes = 0;
        }
        public void Dispose()
        {
            if (_initialMsgTimeout > 0)
            {
                _receiveDone.Set();
                _initialMsgTimeout = 0;
            }
            if (_networkContext != null && _networkContext.Stream != null)
            {
                if (_networkContext.Stream.CanWrite)
                    _networkContext.Stream.Flush();
                _networkContext.Stream.Close();
                _networkContext.Stream.Dispose();
                _networkContext.Stream = null;
            }

            if (_request != null && _request.InputStream != null)
            {
                _request.InputStream.Close();
                _request.InputStream.Dispose();
                _request = null;
            }

            if (_clientContext != null)
            {
                _clientContext.Close();
                _clientContext = null;
            }
        }
    }

    /// <summary>
    /// Reads a byte stream and returns Websocket frames.
    /// </summary>
    public class WebSocketReader
    {
        /// <summary>
        /// Bit to determine if the frame read on the stream is the last frame in a sequence of fragmented frames
        /// </summary>
        private const byte EndBit = 0x80;

        /// <summary>
        /// These are the Frame Opcodes
        /// </summary>
        public enum OpCode
        {
            // Data Opcodes
            Continue = 0x0,
            Text = 0x1,
            Binary = 0x2,

            // Control flow Opcodes
            Close = 0x8,
            Ping = 0x9,
            Pong = 0xA
        }

        /// <summary>
        /// Masks and Unmasks data using the frame mask.  Mask is applied per octal
        /// Note: Frames from clients MUST be masked
        /// Note: Frames from servers MUST NOT be masked
        /// </summary>
        /// <param name="pMask">Int representing 32 bytes of mask data.  Mask is applied per octal</param>
        /// <param name="pBuffer"></param>
        public static void Mask(int pMask, byte[] pBuffer)
        {
            byte[] maskKey = BitConverter.GetBytes(pMask);
            int currentMaskIndex = 0;
            for (int i = 0; i < pBuffer.Length; i++)
            {
                pBuffer[i] = (byte)(pBuffer[i] ^ maskKey[currentMaskIndex]);
                if (currentMaskIndex == 3)
                {
                    currentMaskIndex = 0;
                }
                else
                {
                    currentMaskIndex++;

                }

            }
        }

        /// <summary>
        /// Attempts to read a header off the provided buffer.  Returns true, exports a WebSocketFrameheader,
        /// and an int to move the buffer forward when it reads a header.  False when it can't read a header
        /// </summary>
        /// <param name="pBuffer">Bytes read from the stream</param>
        /// <param name="pOffset">Starting place in the stream to begin trying to read from</param>
        /// <param name="length">Lenth in the stream to try and read from.  Provided for cases where the
        /// buffer's length is larger then the data in it</param>
        /// <param name="oHeader">Outputs the read WebSocket frame header</param>
        /// <param name="moveBuffer">Informs the calling stream to move the buffer forward</param>
        /// <returns>True if it got a header, False if it didn't get a header</returns>
        public static bool TryReadHeader(byte[] pBuffer, int pOffset, int length, out WebsocketFrameHeader oHeader,
                                         out int moveBuffer)
        {
            oHeader = WebsocketFrameHeader.ZeroHeader;
            int minumheadersize = 2;
            if (length > pBuffer.Length - pOffset)
                throw new ArgumentOutOfRangeException("The Length specified was larger the byte array supplied");
            if (length < minumheadersize)
            {
                moveBuffer = 0;
                return false;
            }

            byte nibble1 = (byte)(pBuffer[pOffset] & 0xF0);  //FIN/RSV1/RSV2/RSV3
            byte nibble2 = (byte)(pBuffer[pOffset] & 0x0F); // Opcode block

            oHeader = new WebsocketFrameHeader();
            oHeader.SetDefault();

            if ((nibble1 & WebSocketReader.EndBit) == WebSocketReader.EndBit)
            {
                oHeader.IsEnd = true;
            }
            else
            {
                oHeader.IsEnd = false;
            }
            //Opcode
            oHeader.Opcode = (WebSocketReader.OpCode)nibble2;
            //Mask
            oHeader.IsMasked = Convert.ToBoolean((pBuffer[pOffset + 1] & 0x80) >> 7);

            // Payload length
            oHeader.PayloadLen = (byte)(pBuffer[pOffset + 1] & 0x7F);

            int index = 2; // LargerPayload length starts at byte 3

            switch (oHeader.PayloadLen)
            {
                case 126:
                    minumheadersize += 2;
                    if (length < minumheadersize)
                    {
                        moveBuffer = 0;
                        return false;
                    }
                    Array.Reverse(pBuffer, pOffset + index, 2);  // two bytes
                    oHeader.PayloadLen = BitConverter.ToUInt16(pBuffer, pOffset + index);
                    index += 2;
                    break;
                case 127:   // we got more this is a bigger frame
                    // 8 bytes - uint64 - most significant bit 0  network byte order
                    minumheadersize += 8;
                    if (length < minumheadersize)
                    {
                        moveBuffer = 0;
                        return false;
                    }
                    Array.Reverse(pBuffer, pOffset + index, 8);
                    oHeader.PayloadLen = BitConverter.ToUInt64(pBuffer, pOffset + index);
                    index += 8;
                    break;

            }
            //oHeader.PayloadLeft = oHeader.PayloadLen; // Start the count in case it's chunked over the network.  This is different then frame fragmentation
            if (oHeader.IsMasked)
            {
                minumheadersize += 4;
                if (length < minumheadersize)
                {
                    moveBuffer = 0;
                    return false;
                }
                oHeader.Mask = BitConverter.ToInt32(pBuffer, pOffset + index);
                index += 4;
            }
            moveBuffer = index;
            return true;

        }
    }

    /// <summary>
    /// RFC6455 Websocket Frame
    /// </summary>
    public class WebSocketFrame
    {
        /*
         * RFC6455
nib   0       1       2       3       4       5       6       7
byt   0               1               2               3
dec   0                   1                   2                   3
      0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
     +-+-+-+-+-------+-+-------------+-------------------------------+
     |F|R|R|R| opcode|M| Payload len |    Extended payload length    |
     |I|S|S|S|  (4)  |A|     (7)     |             (16/64)           +
     |N|V|V|V|       |S|             |   (if payload len==126/127)   |
     | |1|2|3|       |K|             |                               +
     +-+-+-+-+-------+-+-------------+ - - - - - - - - - - - - - - - +
     |     Extended payload length continued, if payload len == 127  |
     + - - - - - - - - - - - - - - - +-------------------------------+
     |                               |Masking-key, if MASK set to 1  |
     +-------------------------------+-------------------------------+
     | Masking-key (continued)       |          Payload Data         |
     +-------------------------------- - - - - - - - - - - - - - - - +
     :                     Payload Data continued ...                :
     + - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - +
     |                     Payload Data continued ...                |
     +---------------------------------------------------------------+

         *   When reading these, the frames are possibly fragmented and interleaved with control frames
         *   the fragmented frames are not interleaved with data frames.  Just control frames
         */
        public static readonly WebSocketFrame DefaultFrame = new WebSocketFrame(){Header = new WebsocketFrameHeader(),WebSocketPayload = new byte[0]};
        public WebsocketFrameHeader Header;
        public byte[] WebSocketPayload;

        public byte[] ToBytes()
        {
            Header.PayloadLen = (ulong)WebSocketPayload.Length;
            return Header.ToBytes(WebSocketPayload);
        }

    }

    public struct WebsocketFrameHeader
    {
        //public byte CurrentMaskIndex;
        /// <summary>
        /// The last frame in a sequence of fragmented frames or the one and only frame for this message.
        /// </summary>
        public bool IsEnd;

        /// <summary>
        /// Returns whether the payload data is masked or not.  Data from Clients MUST be masked, Data from Servers MUST NOT be masked
        /// </summary>
        public bool IsMasked;

        /// <summary>
        /// A set of cryptologically sound random bytes XoR-ed against the payload octally.  Looped
        /// </summary>
        public int Mask;
        /*
byt   0               1               2               3
      0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
     +---------------+---------------+---------------+---------------+
     |    Octal 1    |    Octal 2    |    Octal 3    |    Octal 4    |
     +---------------+---------------+---------------+---------------+
*/


        public WebSocketReader.OpCode Opcode;

        public UInt64 PayloadLen;
        //public UInt64 PayloadLeft;
        // Payload is X + Y
        //public UInt64 ExtensionDataLength;
        //public UInt64 ApplicationDataLength;
        public static readonly WebsocketFrameHeader ZeroHeader = WebsocketFrameHeader.HeaderDefault();

        public void SetDefault()
        {

            //CurrentMaskIndex = 0;
            IsEnd = true;
            IsMasked = true;
            Mask = 0;
            Opcode = WebSocketReader.OpCode.Close;
  //        PayloadLeft = 0;
            PayloadLen = 0;
  //        ExtensionDataLength = 0;
  //        ApplicationDataLength = 0;

        }

        /// <summary>
        /// Returns a byte array representing the Frame header
        /// </summary>
        /// <param name="payload">This is the frame data payload.  The header describes the size of the payload.
        /// If payload is null, a Zero sized payload is assumed</param>
        /// <returns>Returns a byte array representing the frame header</returns>
        public byte[] ToBytes(byte[] payload)
        {
            List<byte> result = new List<byte>();

            // Squeeze in our opcode and our ending bit.
            result.Add((byte)((byte)Opcode | (IsEnd?0x80:0x00) ));

            // Again with the three different byte interpretations of size..

            //bytesize
            if (PayloadLen <= 125)
            {
                result.Add((byte) PayloadLen);
            } //Uint16
            else if (PayloadLen <= ushort.MaxValue)
            {
                result.Add(126);
                byte[] payloadLengthByte = BitConverter.GetBytes(Convert.ToUInt16(PayloadLen));
                Array.Reverse(payloadLengthByte);
                result.AddRange(payloadLengthByte);
            } //UInt64
            else
            {
                result.Add(127);
                byte[] payloadLengthByte = BitConverter.GetBytes(PayloadLen);
                Array.Reverse(payloadLengthByte);
                result.AddRange(payloadLengthByte);
            }

            // Only add a payload if it's not null
            if (payload != null)
            {
                result.AddRange(payload);
            }
            return result.ToArray();
        }

        /// <summary>
        /// A Helper method to define the defaults
        /// </summary>
        /// <returns></returns>

        public static WebsocketFrameHeader HeaderDefault()
        {
            return new WebsocketFrameHeader
                       {
                           //CurrentMaskIndex = 0,
                           IsEnd = false,
                           IsMasked = true,
                           Mask = 0,
                           Opcode = WebSocketReader.OpCode.Close,
                           //PayloadLeft = 0,
                           PayloadLen = 0,
 //                          ExtensionDataLength = 0,
 //                          ApplicationDataLength = 0
                       };
        }
    }

    public delegate void DataDelegate(object sender, WebsocketDataEventArgs data);

    public delegate void TextDelegate(object sender, WebsocketTextEventArgs text);

    public delegate void PingDelegate(object sender, PingEventArgs pingdata);

    public delegate void PongDelegate(object sender, PongEventArgs pongdata);

    public delegate void RegularHttpRequestDelegate(object sender, RegularHttpRequestEvnetArgs request);

    public delegate void UpgradeCompletedDelegate(object sender, UpgradeCompletedEventArgs completeddata);

    public delegate void UpgradeFailedDelegate(object sender, UpgradeFailedEventArgs faileddata);

    public delegate void CloseDelegate(object sender, CloseEventArgs closedata);

    public delegate bool ValidateHandshake(string pWebOrigin, string pWebSocketKey, string pHost);


    public class WebsocketDataEventArgs : EventArgs
    {
        public byte[] Data;
    }

    public class WebsocketTextEventArgs : EventArgs
    {
        public string Data;
    }

    public class PingEventArgs : EventArgs
    {
        /// <summary>
        /// The ping event can arbitrarily contain data
        /// </summary>
        public byte[] Data;
    }

    public class PongEventArgs : EventArgs
    {
        /// <summary>
        /// The pong event can arbitrarily contain data
        /// </summary>
        public byte[] Data;

        public int PingResponseMS;

    }

    public class RegularHttpRequestEvnetArgs : EventArgs
    {

    }

    public class UpgradeCompletedEventArgs : EventArgs
    {

    }

    public class UpgradeFailedEventArgs : EventArgs
    {

    }

    public class CloseEventArgs : EventArgs
    {

    }


}
