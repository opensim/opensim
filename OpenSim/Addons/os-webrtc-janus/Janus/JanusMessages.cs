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
using System.Reflection;

using OpenMetaverse.StructuredData;
using OpenMetaverse;

using log4net;

namespace osWebRtcVoice
{

    /// <summary>
    /// Wrappers around the Janus requests and responses.
    /// Since the messages are JSON and, because of the libraries we are using,
    /// the internal structure is an OSDMap, these routines hold all the logic
    /// to getting and setting the values in the JSON.
    /// </summary>
    public class JanusMessage
    {
        protected static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        protected static readonly string LogHeader = "[JANUS MESSAGE]";

        protected OSDMap m_message = new();

        public JanusMessage()
        {
        }

        // A basic Janus message is:
        // {
        //    "janus": "operation",
        //    "transaction": "baefcec8-70c5-4e79-b2c1-d653b9617dea",
        //    "session_id": 5645225333294848,   // optional, gives the session ID
        //    "handle_id": 6969906757968657    // optional, gives the plugin handle ID
        //    "sender": 6969906757968657        // optional, gives the ID of the sending subsystem
        //    "jsep": { "type": "offer", "sdp": "..." }  // optional, gives the SDP
        //  }
        public JanusMessage(string pType) : this()
        {
            m_message["janus"] = pType;
            m_message["transaction"] = UUID.Random().ToString();
        }

        public OSDMap RawBody => m_message;

        public string TransactionId
        { 
            get { return m_message.TryGetString("transaction", out string tid) ? tid : null; }
            set { m_message["transaction"] = value; }
        }

        public string Sender
        { 
            get { return m_message.TryGetString("sender", out string tid) ? tid : null; }
            set { m_message["sender"] = value; }
        }

        public OSDMap Jsep
        {
            get { return m_message.TryGetOSDMap("jsep", out OSDMap jsep) ? jsep : null; }
            set { m_message["jsep"] = value; }
        }

        public void SetJsep(string pOffer, string pSdp)
        {
            m_message["jsep"] = new OSDMap()
            {
                { "type", pOffer },
                { "sdp", pSdp }
            };
        }

        public void AddAPIToken(string pToken)
        {
            m_message["apisecret"] = pToken;
        }

        public void AddAdminToken(string pToken)
        {
            m_message["admin_secret"] = pToken;
        }

        // Note that the session_id is a long number in the JSON so we convert the string.
        public string sessionId
        { 
            get
            {
                return m_message.TryGetValue("session_id", out OSD sessionId) ?
                    sessionId.AsLong().ToString() : string.Empty;
            }
            set
            {
                m_message["session_id"] = long.Parse(value);
            }
        }

        public bool hasSessionId { get { return m_message.ContainsKey("session_id"); } }

        public void AddSessionId(string pToken)
        {
            AddSessionId(long.Parse(pToken));
        }

        public void AddSessionId(long pToken)
        {
            m_message["session_id"] = pToken;
        }

        public bool hasHandleId { get { return m_message.ContainsKey("handle_id"); } }

        public void AddHandleId(string pToken)
        {
            m_message["handle_id"] = long.Parse(pToken);
        }
        public string sender
        {
            get { return m_message is not null && m_message.TryGetString("sender", out string sender) ? sender : string.Empty; }
        }

        public virtual string ToJson()
        {
            return m_message is null ? "'null'": m_message.ToString();
        }
        
        public override string ToString()
        {
            return m_message is null ? "'null'": m_message.ToString();
        }
    }

    // ==============================================================
    // A Janus request message is a basic Janus message with an API token
    public class JanusMessageReq : JanusMessage
    {
        public JanusMessageReq(string pType) : base(pType)
        {
        }
    }

    // ==============================================================
    // Janus message response is a basic Janus message with the response data
    // {
    //    "janus": "success",
    //    "transaction": "baefcec8-70c5-4e79-b2c1-d653b9617dea", // ID of the requesting message
    //    "data": { ... }  // the response data
    //    "error": { "code": 123, "reason": "..." }  // if there was an error
    // }
    // The "janus" return code changes depending on the request. The above is for
    //    a successful response. Could be
    //      "event": for an event message (See JanusEventResp)
    //      "keepalive": for a keepalive event
    public class JanusMessageResp : JanusMessage
    {
        public JanusMessageResp() : base()
        {
        }

        public JanusMessageResp(string pType) : base(pType)
        {
        }

        public JanusMessageResp(OSDMap pMap) : base()
        {
            m_message = pMap;
        }

        public static JanusMessageResp FromJson(string pJson)
        {
            OSDMap newBody = OSDParser.DeserializeJson(pJson) as OSDMap;
            return new JanusMessageResp(newBody);
        }

        // Return the "data" portion of the response as an OSDMap or null if there is none
        public OSDMap dataSection { get { return m_message.TryGetOSDMap("data", out OSDMap data) ? data : null; } }

        // Check if a successful response code is in the response
        public virtual bool isSuccess { get { return CheckReturnCode("success"); } }

        public virtual bool isEvent { get { return CheckReturnCode("event"); } }

        public virtual bool isError { get { return CheckReturnCode("error"); } }

        public virtual bool CheckReturnCode(string pCode)
        {
            return ReturnCode == pCode;
        }
        public virtual string ReturnCode
        {
            get
            { 
                return m_message is not null && m_message.TryGetString("janus", out string sjanus) ?
                    sjanus : string.Empty;
            }
        }
    }

    // ==============================================================
    // An error response is a Janus response with an error code and reason.
    // {
    //    "janus": "error",
    //    "transaction": "baefcec8-70c5-4e79-b2c1-d653b9617dea", // ID of the requesting message
    //    "error": { "code": 123, "reason": "..." }  // if there was an error
    // }
    public class ErrorResp : JanusMessageResp
    {
        public ErrorResp() : base("error")
        {
        }

        public ErrorResp(string pType) : base(pType)
        {
        }

        public ErrorResp(JanusMessageResp pResp) : base(pResp.RawBody)
        {
        }

        public void SetError(int pCode, string pReason)
        {
            m_message["error"] = new OSDMap()
            {
                { "code", pCode },
                { "reason", pReason }
            };
        }

        // Dig through the response to get the error code or 0 if there is none
        public int errorCode
        {
            get
            {
                return m_message.TryGetOSDMap("error", out OSDMap errMap) ?
                    (int)errMap["code"].AsLong() : 0;
            }
        }

        // Dig through the response to get the error reason or empty string if there is none
        public string errorReason
        {
            get
            {
                if(m_message.TryGetOSDMap("error", out OSDMap errMap))
                    return errMap.TryGetString("reason", out string reason) ? reason : string.Empty;
                return string.Empty;
            }
        }
    }

    // ==============================================================
    // Create session request and response
    public class CreateSessionReq : JanusMessageReq
    {
        public CreateSessionReq() : base("create")
        {
        }
    }

    public class CreateSessionResp : JanusMessageResp
    {
        public CreateSessionResp(JanusMessageResp pResp) : base(pResp.RawBody)
        { }
        public string returnedId
        {
            get
            {
                return dataSection.TryGetValue("id", out OSD oid) ? oid.AsLong().ToString() : string.Empty;
            }
        }  
    }

    // ==============================================================
    public class DestroySessionReq : JanusMessageReq
    {
        public DestroySessionReq() : base("destroy")
        {
            // Doesn't include the session ID because it is the URI
        }
    }

    // ==============================================================
    public class TrickleReq : JanusMessageReq
    {
        // An empty trickle request is used to signal the end of the trickle
        public TrickleReq(JanusViewerSession pVSession) : base("trickle")
        {
            m_message["candidate"] = new OSDMap()
            {
                { "completed", true },
            };

        }
        public TrickleReq(JanusViewerSession pVSession, OSD pCandidates) : base("trickle")
        {
            m_message["viewer_session"] = pVSession.ViewerSessionID;
            if (pCandidates is OSDArray)
                m_message["candidates"] = pCandidates;
            else
                m_message["candidate"] = pCandidates;
        }
    }

    // ==============================================================
    public class AttachPluginReq : JanusMessageReq
    {
        public AttachPluginReq(string pPlugin) : base("attach")
        {
            m_message["plugin"] = pPlugin;
        }
    }

    public class AttachPluginResp : JanusMessageResp
    {
        public AttachPluginResp(JanusMessageResp pResp) : base(pResp.RawBody)
        { }
        public string pluginId
        {
            get
            {
                return dataSection.TryGetValue("id", out OSD oid) ? oid.AsLong().ToString() : string.Empty;
            }
        }
    }

    // ==============================================================
    public class DetachPluginReq : JanusMessageReq
    {
        public DetachPluginReq() : base("detach")
        {
            // Doesn't include the session ID or plugin ID because it is the URI
        }
    }

    // ==============================================================
    public class HangupReq : JanusMessageReq
    {
        public HangupReq() : base("hangup")
        {
            // Doesn't include the session ID or plugin ID because it is the URI
        }
    }

    // ==============================================================
    // Plugin messages are defined here as wrappers around OSDMap.
    // The ToJson() method is overridden to put the OSDMap into the
    //    message body.
    // A plugin request is formatted like:
    //  {
    //    "janus": "message",
    //    "transaction": "baefcec8-70c5-4e79-b2c1-d653b9617dea",
    //    "body": {
    //        "request": "create",
    //        "room": 10,
    //        "is_private": false,
    // }
    public class PluginMsgReq : JanusMessageReq
    {
        private OSDMap m_body = new();

        // Note that the passed OSDMap is placed in the "body" section of the message
        public PluginMsgReq(OSDMap pBody) : base("message")
        {
            m_body = pBody;
        }

        public void AddStringToBody(string pKey, string pValue)
        {
            m_body[pKey] = pValue;
        }

        public void AddIntToBody(string pKey, int pValue)
        {
            m_body[pKey] = pValue;
        }

        public void AddBoolToBody(string pKey, bool pValue)
        {
            m_body[pKey] = pValue;
        }

        public void AddOSDToBody(string pKey, OSD pValue)
        {
            m_body[pKey] = pValue;
        }

        public override string ToJson()
        {
            m_message["body"] = m_body;
            return base.ToJson();
        }
    }

    // A plugin response is formatted like:
    //    {
    //    "janus": "success",
    //    "session_id": 5645225333294848,
    //    "transaction": "baefcec8-70c5-4e79-b2c1-d653b9617dea",
    //    "sender": 6969906757968657,
    //    "plugindata": {
    //        "plugin": "janus.plugin.audiobridge",
    //        "data": {
    //            "audiobridge": "created",
    //            "room": 10,
    //            "permanent": false
    //        }
    //    }
    public class PluginMsgResp : JanusMessageResp
    {
        public OSDMap m_pluginData;
        public OSDMap m_data;
        public PluginMsgResp(JanusMessageResp pResp) : base(pResp?.RawBody)
        {
            if (m_message is not null && m_message.TryGetOSDMap("plugindata", out m_pluginData))
            {
                // Move the plugin data up into the m_data var so it is easier to get to
                if (m_pluginData is not null)
                {
                    _ = m_pluginData.TryGetOSDMap("data", out m_data);
                    // m_log.DebugFormat("{0} AudioBridgeResp. Found both plugindata and data: data={1}", LogHeader, m_data.ToString());
                }
            }
        }

        public OSDMap PluginRespData
        {
            get { return m_data; }
        }

        // Get an integer value for a key in the response data or zero if not there
        public int PluginRespDataInt(string pKey)
        {
            if (m_data is null)
                return 0;
            return m_data.TryGetValue(pKey, out OSD okey) ? (int)okey.AsLong(): 0;
        }

        // Get an long value for a key in the response data or zero if not there
        public long PluginRespDataLong(string pKey)
        {
            if (m_data is null)
                return 0L;
            return m_data.TryGetValue(pKey, out OSD okey) ? okey.AsLong(): 0L;
        }

        // Get a string value for a key in the response data or empty string if not there
        public string PluginRespDataString(string pKey)
        {
            if (m_data is null)
                return string.Empty;
            return m_data.TryGetValue(pKey, out OSD okey) ? okey.AsString() : string.Empty;
        }
    }

    // ==============================================================
    // Plugin messages for the audio bridge.
    // Audiobridge responses are formatted like:
    //    {
    //    "janus": "success",
    //    "session_id": 5645225333294848,
    //    "transaction": "baefcec8-70c5-4e79-b2c1-d653b9617dea",
    //    "sender": 6969906757968657,
    //    "plugindata": {
    //        "plugin": "janus.plugin.audiobridge",
    //        "data": {
    //            "audiobridge": "created",
    //            "room": 10,
    //            "permanent": false
    //        }
    //    }
    public class AudioBridgeResp : PluginMsgResp
    {
        public AudioBridgeResp(JanusMessageResp pResp) : base(pResp)
        {
        }
        public override bool isSuccess { get { return PluginRespDataString("audiobridge") == "success"; } }
        // Return the return code if it is in the response or empty string if not
        public string AudioBridgeReturnCode { get { return PluginRespDataString("audiobridge"); } }
        // Return the error code if it is in the response or zero if not
        public int AudioBridgeErrorCode { get { return PluginRespDataInt("error_code"); } }
        // Return the room ID if it is in the response or zero if not
        public int RoomId { get { return PluginRespDataInt("room"); } }
    }

    // ==============================================================
    public class AudioBridgeCreateRoomReq : PluginMsgReq
    {
        public AudioBridgeCreateRoomReq(int pRoomId) : this(pRoomId, false, null, null)
        {
        }

        public AudioBridgeCreateRoomReq(int pRoomId, bool pSpatial, string pDesc, string credentials) : base(new OSDMap() {
                                                { "room", pRoomId },
                                                { "request", "create" },
                                                { "is_private", false },
                                                { "permanent", false },
                                                { "sampling_rate", 48000 },
                                                { "spatial_audio", pSpatial },
                                                { "denoise", false },
                                                { "record", false }
                                            })
        {
            if (!string.IsNullOrEmpty(pDesc))
                AddStringToBody("description", pDesc);
            if (!string.IsNullOrEmpty(credentials))
                AddStringToBody("pin", credentials);
        }
    }

    // ==============================================================
    public class AudioBridgeDestroyRoomReq : PluginMsgReq
    {
        public AudioBridgeDestroyRoomReq(int pRoomId) : base(new OSDMap() {
                                                { "request", "destroy" },
                                                { "room", pRoomId },
                                                { "permanent", true }
                                            })
        {
        }
    }

    // ==============================================================
    public class AudioBridgeJoinRoomReq : PluginMsgReq
    {
        public AudioBridgeJoinRoomReq(int pRoomId, string pAgentName) : base(new OSDMap() {
                                                { "request", "join" },
                                                { "room", pRoomId },
                                                { "display", pAgentName }
                                            })
        {
        }
    }

    public class AudioBridgeAgentJoinRoomReq : PluginMsgReq
    {
        public AudioBridgeAgentJoinRoomReq(int pRoomId, UUID Agent) : base(new OSDMap() {
                                        { "request", "join" },
                                        { "room", pRoomId },
                                        { "id", new OSDLong(Math.Abs((long)(Agent.ulonga ^ Agent.ulongb)))},
                                        { "display", Agent.ToString() }
                                    })
        {
        }
    }

    // A successful response contains the participant ID and the SDP
    public class AudioBridgeJoinRoomResp : AudioBridgeResp
    {
        public AudioBridgeJoinRoomResp(JanusMessageResp pResp) : base(pResp)
        {
        }

        public long ParticipantId { get { return PluginRespDataLong("id"); } }
    }

    // ==============================================================
    public class AudioBridgeConfigRoomReq : PluginMsgReq
    {
        // TODO:
        public AudioBridgeConfigRoomReq(int pRoomId, string pSdp) : base(new OSDMap() {
                                                { "request", "configure" }
                                            })
        {
        }
    }

    public class AudioBridgeConfigRoomResp : AudioBridgeResp
    {
        // TODO:
        public AudioBridgeConfigRoomResp(JanusMessageResp pResp) : base(pResp)
        {
        }
    }

    // ==============================================================
    public class AudioBridgeLeaveRoomReq : PluginMsgReq
    {
        public AudioBridgeLeaveRoomReq(int pRoomId, long pAttendeeId) : base(new OSDMap() {
                                                { "request", "leave" },
                                                { "room", pRoomId },
                                                { "id", pAttendeeId }
                                            })
        {
        }
    }

    // ==============================================================
    public class AudioBridgeListRoomsReq : PluginMsgReq
    {
        public AudioBridgeListRoomsReq() : base(new OSDMap() {
                                                { "request", "list" }
                                            })  
        {
        }
    }

    // ==============================================================
    public class AudioBridgeListParticipantsReq : PluginMsgReq
    {
        public AudioBridgeListParticipantsReq(int pRoom) : base(new OSDMap() {
                                                { "request", "listparticipants" },
                                                { "room", pRoom }
                                            })
        {
        }
    }

    // ==============================================================
    public class AudioBridgeEvent : AudioBridgeResp
    {
        public AudioBridgeEvent(JanusMessageResp pResp) : base(pResp)
        {
        }
    }

    // ==============================================================
    // The LongPoll request returns events from  the plugins. These are formatted
    //    like the other responses but are not responses to requests.
    // They are formatted like:
    //    {
    //    "janus": "event",
    //    "sender": 6969906757968657,
    //    "transaction": "baefcec8-70c5-4e79-b2c1-d653b9617dea",
    //    "plugindata": {
    //        "plugin": "janus.plugin.audiobridge",
    //        "data": {
    //            "audiobridge": "event",
    //            "room": 10,
    //            "participants": 1,
    //            "participants": [
    //                {
    //                    "id": 1234,
    //                    "display": "John Doe",
    //                    "audio_level": 0.0,
    //                    "video_room": false,
    //                    "video_muted": false,
    //                    "audio_muted": false,
    //                    "feed": 1234
    //                }
    //            ]
    //        }
    //    }

    public class EventResp : JanusMessageResp
    {
        public EventResp() : base()
        {
        }

        public EventResp(string pType) : base(pType)
        {
        }

        public EventResp(JanusMessageResp pResp) : base(pResp.RawBody)
        {
        }
    }
    // ==============================================================
}
