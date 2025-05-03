// Copyright 2024 Robert Adams (misterblue@misterblue.com)
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using System.Threading.Tasks;

using OpenMetaverse.StructuredData;

using log4net;
using log4net.Core;
using System.Reflection.Metadata;
using System.Threading;

namespace WebRtcVoice
{
    // Encapsulization of a Session to the Janus server
    public class JanusSession : IDisposable
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[JANUS SESSION]";

        // Set to 'true' to get the messages send and received from Janus
        private bool _MessageDetails = false;

        private string _JanusServerURI = String.Empty;
        private string _JanusAPIToken = String.Empty;
        private string _JanusAdminURI = String.Empty;
        private string _JanusAdminToken = String.Empty;

        public string JanusServerURI => _JanusServerURI;
        public string JanusAdminURI => _JanusAdminURI;

        public string SessionId { get; private set; }
        public string SessionUri { get ; private set ; }

        public string PluginId { get; set; }

        private CancellationTokenSource _CancelTokenSource = new CancellationTokenSource();
        private HttpClient _HttpClient = new HttpClient();

        public bool IsConnected { get; set; }

        // Wrapper around the session connection to Janus-gateway
        public JanusSession(string pServerURI, string pAPIToken, string pAdminURI, string pAdminToken, bool pDebugMessages = false)
        {
            m_log.DebugFormat("{0} JanusSession constructor", LogHeader);
            _JanusServerURI = pServerURI;
            _JanusAPIToken = pAPIToken;
            _JanusAdminURI = pAdminURI;
            _JanusAdminToken = pAdminToken;
            _MessageDetails = pDebugMessages;
        }

        public void Dispose()
        {
            ClearEventSubscriptions();
            if (IsConnected)
            {
                _ = DestroySession();
            }
            if (_HttpClient is not null)
            {
                _HttpClient.Dispose();
                _HttpClient = null;
            }
        }

        /// <summary>
        /// Make the create session request to the Janus server, get the
        /// sessionID and return TRUE if successful.
        /// </summary>
        /// <returns>TRUE if session was created successfully</returns>
        public async Task<bool> CreateSession()
        {
            bool ret = false;
            try
            {
                var resp = await SendToJanus(new CreateSessionReq());
                if (resp is not null && resp.isSuccess)
                {
                    var sessionResp = new CreateSessionResp(resp);
                    SessionId = sessionResp.returnedId;
                    IsConnected = true;
                    SessionUri = _JanusServerURI + "/" + SessionId;
                    m_log.DebugFormat("{0} CreateSession. Created. ID={1}, URL={2}", LogHeader, SessionId, SessionUri);
                    ret = true;
                    StartLongPoll();
                }
                else
                {
                    m_log.ErrorFormat("{0} CreateSession: failed", LogHeader);
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0} CreateSession: exception {1}", LogHeader, e);
            }

            return ret;
        }

        public async Task<bool> DestroySession()
        {
            bool ret = false;
            try
            {
                JanusMessageResp resp = await SendToSession(new DestroySessionReq());
                if (resp is not null && resp.isSuccess)
                {
                    // Note that setting IsConnected to false will cause the long poll to exit
                    m_log.DebugFormat("{0} DestroySession. Destroyed", LogHeader);
                }
                else
                {
                    if (resp.isError)
                    {
                        ErrorResp eResp = new ErrorResp(resp);
                        switch (eResp.errorCode)
                        {
                            case 458:
                                // This is the error code for a session that is already destroyed
                                m_log.DebugFormat("{0} DestroySession: session already destroyed", LogHeader);
                                break;
                            case 459:
                                // This is the error code for handle already destroyed
                                m_log.DebugFormat("{0} DestroySession: Handle not found", LogHeader);
                                break;
                            default:
                                m_log.ErrorFormat("{0} DestroySession: failed {1}", LogHeader, eResp.errorReason);
                                break;
                        }
                    }
                    else
                    {
                        m_log.ErrorFormat("{0} DestroySession: failed. Resp: {1}", LogHeader, resp.ToString());
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0} DestroySession: exception {1}", LogHeader, e);
            }
            IsConnected = false;
            _CancelTokenSource.Cancel();

            return ret;
        }

        // ====================================================================
        public async Task<JanusMessageResp> TrickleCandidates(JanusViewerSession pVSession, OSDArray pCandidates)
        {
            JanusMessageResp ret = null;
            // if the audiobridge is active, the trickle message is sent to it
            if (pVSession.AudioBridge is null)
            {
                ret = await SendToJanusNoWait(new TrickleReq(pVSession));
            }
            else
            {
                ret = await SendToJanusNoWait(new TrickleReq(pVSession), pVSession.AudioBridge.PluginUri);
            }
            return ret;
        }
        // ====================================================================
        public async Task<JanusMessageResp> TrickleCompleted(JanusViewerSession pVSession)
        {
            JanusMessageResp ret = null;
            // if the audiobridge is active, the trickle message is sent to it
            if (pVSession.AudioBridge is null)
            {
                ret = await SendToJanusNoWait(new TrickleReq(pVSession));
            }
            else
            {
                ret = await SendToJanusNoWait(new TrickleReq(pVSession), pVSession.AudioBridge.PluginUri);
            }
            return ret;
        }
        // ====================================================================
        public Dictionary<string, JanusPlugin> _Plugins = new Dictionary<string, JanusPlugin>();
        public void AddPlugin(JanusPlugin pPlugin)
        {
            _Plugins.Add(pPlugin.PluginName, pPlugin);
        }
        // ====================================================================
        // Post to the session
        public async Task<JanusMessageResp> SendToSession(JanusMessageReq pReq)
        {
            return await SendToJanus(pReq, SessionUri);
        }

        private class OutstandingRequest
        {
            public string TransactionId;
            public DateTime RequestTime;
            public TaskCompletionSource<JanusMessageResp> TaskCompletionSource;
        }
        private Dictionary<string, OutstandingRequest> _OutstandingRequests = new Dictionary<string, OutstandingRequest>();

        // Send a request directly to the Janus server.
        // NOTE: this is probably NOT what you want to do. This is a direct call that is outside the session.
        private async Task<JanusMessageResp> SendToJanus(JanusMessageReq pReq)
        {
            return await SendToJanus(pReq, _JanusServerURI);
        }

        /// <summary>
        /// Send a request to the Janus server. This is the basic call that sends a request to the server.
        /// The transaction ID is used to match the response to the request.
        /// If the request returns an 'ack' response, the code waits for the matching event
        /// before returning the response.
        /// </summary>
        /// <param name="pReq"></param>
        /// <param name="pURI"></param>
        /// <returns></returns>
        public async Task<JanusMessageResp> SendToJanus(JanusMessageReq pReq, string pURI)
        {
            AddJanusHeaders(pReq);
            // m_log.DebugFormat("{0} SendToJanus", LogHeader);
            if (_MessageDetails) m_log.DebugFormat("{0} SendToJanus. URI={1}, req={2}", LogHeader, pURI, pReq.ToJson());

            JanusMessageResp ret = null;
            try
            {
                OutstandingRequest outReq = new OutstandingRequest
                {
                    TransactionId = pReq.TransactionId,
                    RequestTime = DateTime.Now,
                    TaskCompletionSource = new TaskCompletionSource<JanusMessageResp>()
                };
                _OutstandingRequests.Add(pReq.TransactionId, outReq);

                string reqStr = pReq.ToJson();

                HttpRequestMessage reqMsg = new HttpRequestMessage(HttpMethod.Post, pURI);
                reqMsg.Content = new StringContent(reqStr, System.Text.Encoding.UTF8, MediaTypeNames.Application.Json);
                reqMsg.Headers.Add("Accept", "application/json");
                HttpResponseMessage response = await _HttpClient.SendAsync(reqMsg, _CancelTokenSource.Token);

                if (response.IsSuccessStatusCode)
                {
                    string respStr = await response.Content.ReadAsStringAsync();
                    ret = JanusMessageResp.FromJson(respStr);
                    if (ret.CheckReturnCode("ack"))
                    {
                        // Some messages are asynchronous and completed with an event
                        if (_MessageDetails) m_log.DebugFormat("{0} SendToJanus: ack response {1}", LogHeader, respStr);
                        if (_OutstandingRequests.TryGetValue(pReq.TransactionId, out OutstandingRequest outstandingRequest))
                        {
                            ret = await outstandingRequest.TaskCompletionSource.Task;
                            _OutstandingRequests.Remove(pReq.TransactionId);
                        }
                        // If there is no OutstandingRequest, the request was not waiting for an event or already processed
                    }
                    else 
                    {
                        // If the response is not an ack, that means a synchronous request/response so return the response
                        _OutstandingRequests.Remove(pReq.TransactionId);
                        if (_MessageDetails) m_log.DebugFormat("{0} SendToJanus: response {1}", LogHeader, respStr);
                    }
                }
                else
                {
                    m_log.ErrorFormat("{0} SendToJanus: response not successful {1}", LogHeader, response);
                    _OutstandingRequests.Remove(pReq.TransactionId);
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0} SendToJanus: exception {1}", LogHeader, e.Message);
            }

            return ret;
        }
        /// <summary>
        /// Send a request to the Janus server but we just return the response and don't wait for any
        /// event or anything.
        /// There are some requests that are just fire-and-forget.
        /// </summary>
        /// <param name="pReq"></param>
        /// <returns></returns>
        private async Task<JanusMessageResp> SendToJanusNoWait(JanusMessageReq pReq, string pURI)
        {
            JanusMessageResp ret = new JanusMessageResp();

            AddJanusHeaders(pReq);

            try {
                HttpRequestMessage reqMsg = new HttpRequestMessage(HttpMethod.Post, pURI);
                string reqStr = pReq.ToJson();
                reqMsg.Content = new StringContent(reqStr, System.Text.Encoding.UTF8, MediaTypeNames.Application.Json);
                reqMsg.Headers.Add("Accept", "application/json");
                HttpResponseMessage response = await _HttpClient.SendAsync(reqMsg);
                string respStr = await response.Content.ReadAsStringAsync();
                ret = JanusMessageResp.FromJson(respStr);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0} SendToJanusNoWait: exception {1}", LogHeader, e.Message);
            }
            return ret;

        }
        private async Task<JanusMessageResp> SendToJanusNoWait(JanusMessageReq pReq)
        {
            return await SendToJanusNoWait(pReq, SessionUri);
        }

        // There are various headers that are in most Janus requests. Add them here.
        private void AddJanusHeaders(JanusMessageReq pReq)
        {
            // Authentication token
            if (!String.IsNullOrEmpty(_JanusAPIToken))
            {
                pReq.AddAPIToken(_JanusAPIToken);
            }
            // Transaction ID that matches responses to requests
            if (String.IsNullOrEmpty(pReq.TransactionId))
            {
                pReq.TransactionId = Guid.NewGuid().ToString();
            }
            // The following two are required for the WebSocket interface. They are optional for the
            //     HTTP interface since the session and plugin handle are in the URL.
            // SessionId is added to the message if not already there
            if (!pReq.hasSessionId && !String.IsNullOrEmpty(SessionId))
            {
                pReq.AddSessionId(SessionId);
            }
            // HandleId connects to the plugin
            if (!pReq.hasHandleId && !String.IsNullOrEmpty(PluginId))
            {
                pReq.AddHandleId(PluginId);
            }
        }

        bool TryGetOutstandingRequest(string pTransactionId, out OutstandingRequest pOutstandingRequest)
        {
            if (String.IsNullOrEmpty(pTransactionId))
            {
                pOutstandingRequest = null;
                return false;
            }

            bool ret = false;
            lock (_OutstandingRequests)
            {
                if (_OutstandingRequests.TryGetValue(pTransactionId, out pOutstandingRequest))
                {
                    _OutstandingRequests.Remove(pTransactionId);
                    ret = true;
                }
            }
            return ret;
        }

        public Task<JanusMessageResp> SendToJanusAdmin(JanusMessageReq pReq)
        {
            return SendToJanus(pReq, _JanusAdminURI);
        }

        public Task<JanusMessageResp> GetFromJanus()
        {
            return GetFromJanus(_JanusServerURI);
        }
        /// <summary>
        /// Do a GET to the Janus server and return the response.
        /// If the response is an HTTP error, we return fake JanusMessageResp with the error.
        /// </summary>
        /// <param name="pURI"></param>
        /// <returns></returns>
        public async Task<JanusMessageResp> GetFromJanus(string pURI)
        {
            if (!String.IsNullOrEmpty(_JanusAPIToken))
            {
                pURI += "?apisecret=" + _JanusAPIToken;
            }

            JanusMessageResp ret = null;
            try
            {
                // m_log.DebugFormat("{0} GetFromJanus: URI = \"{1}\"", LogHeader, pURI);
                HttpRequestMessage reqMsg = new HttpRequestMessage(HttpMethod.Get, pURI);
                reqMsg.Headers.Add("Accept", "application/json");
                HttpResponseMessage response = null;
                try
                {
                    response = await _HttpClient.SendAsync(reqMsg, _CancelTokenSource.Token);
                    if (response is not null && response.IsSuccessStatusCode)
                    {
                        string respStr = await response.Content.ReadAsStringAsync();
                        ret = JanusMessageResp.FromJson(respStr);
                        // m_log.DebugFormat("{0} GetFromJanus: response {1}", LogHeader, respStr);
                    }
                    else
                    {
                        m_log.ErrorFormat("{0} GetFromJanus: response not successful {1}", LogHeader, response);
                        var eResp = new ErrorResp("GETERROR");
                        // Add the sessionId so the proper session can be shut down
                        eResp.AddSessionId(SessionId);
                        if (response is not null)
                        {
                            eResp.SetError((int)response.StatusCode, response.ReasonPhrase);
                        }
                        else
                        {
                            eResp.SetError(0, "Connection refused");
                        }
                        ret = eResp;
                    }
                }
                catch (TaskCanceledException e)
                {
                    m_log.DebugFormat("{0} GetFromJanus: task canceled: {1}", LogHeader, e.Message);
                    var eResp = new ErrorResp("GETERROR");
                    eResp.SetError(499, "Task canceled");
                    ret = eResp;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("{0} GetFromJanus: exception {1}", LogHeader, e.Message);
                    var eResp = new ErrorResp("GETERROR");
                    eResp.SetError(400, "Exception: " + e.Message);
                    ret = eResp;
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("{0} GetFromJanus: exception {1}", LogHeader, e);
                var eResp = new ErrorResp("GETERROR");
                eResp.SetError(400, "Exception: " + e.Message);
                ret = eResp;
            }

            return ret;
        }

        // ====================================================================
        public delegate void JanusEventHandler(EventResp pResp);

        // Not all the events are used. CS0067 is to suppress the warning that the event is not used.
        #pragma warning disable CS0067,CS0414
        public event JanusEventHandler OnKeepAlive;
        public event JanusEventHandler OnServerInfo;
        public event JanusEventHandler OnTrickle;
        public event JanusEventHandler OnHangup;
        public event JanusEventHandler OnDetached;
        public event JanusEventHandler OnError;
        public event JanusEventHandler OnEvent;
        public event JanusEventHandler OnMessage;
        public event JanusEventHandler OnJoined;
        public event JanusEventHandler OnLeaving;
        public event JanusEventHandler OnDisconnect;
        #pragma warning restore CS0067,CS0414
        public void ClearEventSubscriptions()
        {
            OnKeepAlive = null;
            OnServerInfo = null;
            OnTrickle = null;
            OnHangup = null;
            OnDetached = null;
            OnError = null;
            OnEvent = null;
            OnMessage = null;
            OnJoined = null;
            OnLeaving = null;
            OnDisconnect = null;
        }
        // ====================================================================
        /// <summary>
        /// In the REST API, events are returned by a long poll. This
        /// starts the poll and calls the registed event handler when
        /// an event is received.
        /// </summary>
        private void StartLongPoll()
        {
            bool running = true;

            m_log.DebugFormat("{0} EventLongPoll", LogHeader);
            Task.Run(async () => {
                while (running && IsConnected)
                {
                    try
                    {
                        var resp = await GetFromJanus(SessionUri);
                        if (resp is not null)
                        {
                            _ = Task.Run(() =>
                            {
                                EventResp eventResp = new EventResp(resp);
                                switch (resp.ReturnCode)
                                {
                                    case "keepalive":
                                        // These should happen every 30 seconds
                                        // m_log.DebugFormat("{0} EventLongPoll: keepalive {1}", LogHeader, resp.ToString());
                                        break;
                                    case "server_info":
                                        // Just info on the Janus instance
                                        m_log.DebugFormat("{0} EventLongPoll: server_info {1}", LogHeader, resp.ToString());
                                        break;
                                    case "ack":
                                        // 'ack' says the request was received and an event will follow
                                        if (_MessageDetails) m_log.DebugFormat("{0} EventLongPoll: ack {1}", LogHeader, resp.ToString());
                                        break;
                                    case "success":
                                        // success is a sync response that says the request was completed
                                        if (_MessageDetails) m_log.DebugFormat("{0} EventLongPoll: success {1}", LogHeader, resp.ToString());
                                        break;
                                    case "trickle":
                                        // got a trickle ICE candidate from Janus
                                        // this is for reverse communication from Janus to the client and we don't do that
                                        if (_MessageDetails) m_log.DebugFormat("{0} EventLongPoll: trickle {1}", LogHeader, resp.ToString());
                                        OnTrickle?.Invoke(eventResp);
                                        break;
                                    case "webrtcup":
                                        //  ICE and DTLS succeeded, and so Janus correctly established a PeerConnection with the user/application;
                                        m_log.DebugFormat("{0} EventLongPoll: webrtcup {1}", LogHeader, resp.ToString());
                                        break;
                                    case "hangup":
                                        // The PeerConnection was closed, either by the user/application or by Janus itself;
                                        // If one is in the room, when a "hangup" event happens, it means that the user left the room.
                                        m_log.DebugFormat("{0} EventLongPoll: hangup {1}", LogHeader, resp.ToString());
                                        OnHangup?.Invoke(eventResp);
                                        break;
                                    case "detached":
                                        // a plugin asked the core to detach one of our handles
                                        m_log.DebugFormat("{0} EventLongPoll: event {1}", LogHeader, resp.ToString());
                                        OnDetached?.Invoke(eventResp);
                                        break;
                                    case "media":
                                        // Janus is receiving (receiving: true/false) audio/video (type: "audio/video") on this PeerConnection;
                                        m_log.DebugFormat("{0} EventLongPoll: media {1}", LogHeader, resp.ToString());
                                        break;
                                    case "slowlink":
                                        // Janus detected a slowlink (uplink: true/false) on this PeerConnection;
                                        m_log.DebugFormat("{0} EventLongPoll: slowlink {1}", LogHeader, resp.ToString());
                                        break;
                                    case "error":
                                        m_log.DebugFormat("{0} EventLongPoll: error {1}", LogHeader, resp.ToString());
                                        if (TryGetOutstandingRequest(resp.TransactionId, out OutstandingRequest outstandingRequest))
                                        {
                                            outstandingRequest.TaskCompletionSource.SetResult(resp);
                                        }
                                        else
                                        {
                                            OnError?.Invoke(eventResp);
                                            m_log.ErrorFormat("{0} EventLongPoll: error with no transaction. {1}", LogHeader, resp.ToString());
                                        }
                                        break;
                                    case "event":
                                        if (_MessageDetails) m_log.DebugFormat("{0} EventLongPoll: event {1}", LogHeader, resp.ToString());
                                        if (TryGetOutstandingRequest(resp.TransactionId, out OutstandingRequest outstandingRequest2))
                                        {
                                            // Someone is waiting for this event
                                            outstandingRequest2.TaskCompletionSource.SetResult(resp);
                                        }
                                        else
                                        {
                                            m_log.ErrorFormat("{0} EventLongPoll: event no outstanding request {1}", LogHeader, resp.ToString());
                                            OnEvent?.Invoke(eventResp);
                                        }
                                        break;
                                    case "message":
                                        m_log.DebugFormat("{0} EventLongPoll: message {1}", LogHeader, resp.ToString());
                                        OnMessage?.Invoke(eventResp);
                                        break;
                                    case "timeout":
                                        // Events for the audio bridge
                                        m_log.DebugFormat("{0} EventLongPoll: timeout {1}", LogHeader, resp.ToString());
                                        break;
                                    case "joined":
                                        // Events for the audio bridge
                                        OnJoined?.Invoke(eventResp);
                                        m_log.DebugFormat("{0} EventLongPoll: joined {1}", LogHeader, resp.ToString());
                                        break;
                                    case "leaving":
                                        // Events for the audio bridge
                                        OnLeaving?.Invoke(eventResp);
                                        m_log.DebugFormat("{0} EventLongPoll: leaving {1}", LogHeader, resp.ToString());
                                        break;
                                    case "GETERROR":
                                        // Special error response from the GET
                                        var errorResp = new ErrorResp(resp);
                                        switch (errorResp.errorCode)
                                        {
                                            case 404:
                                                // "Not found" means there is a Janus server but the session is gone
                                                m_log.ErrorFormat("{0} EventLongPoll: GETERROR Not Found. URI={1}: {2}",
                                                            LogHeader, SessionUri, resp.ToString());
                                                break;
                                            case 400:
                                                // "Bad request" means the session is gone
                                                m_log.ErrorFormat("{0} EventLongPoll: Bad Request. URI={1}: {2}",
                                                            LogHeader, SessionUri, resp.ToString());
                                                break;
                                            case 499:
                                                // "Task canceled" means the long poll was canceled
                                                m_log.DebugFormat("{0} EventLongPoll: Task canceled. URI={1}", LogHeader, SessionUri);
                                                break;
                                            default:
                                                m_log.DebugFormat("{0} EventLongPoll: unknown response. URI={1}: {2}",
                                                            LogHeader, SessionUri, resp.ToString());
                                                break;
                                        }   
                                        // This will cause the long poll to exit
                                        running = false;
                                        OnDisconnect?.Invoke(eventResp);
                                        break;
                                    default:
                                        m_log.DebugFormat("{0} EventLongPoll: unknown response {1}", LogHeader, resp.ToString());
                                        break;
                                }
                            });
                        }
                        else
                        {
                            m_log.ErrorFormat("{0} EventLongPoll: failed. Response is null", LogHeader);
                        }
                    }
                    catch (Exception e)
                    {
                        // This will cause the long poll to exit
                        running = false;
                        m_log.ErrorFormat("{0} EventLongPoll: exception {1}", LogHeader, e);
                    }
                }
                m_log.InfoFormat("{0} EventLongPoll: Exiting long poll loop", LogHeader);
            });
        }
    }
}