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
using System.Reflection;

using OpenMetaverse.StructuredData;
using OpenMetaverse;

using log4net;

namespace WebRtcVoice
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

        protected OSDMap m_message = new OSDMap();

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

        public string TransactionId { 
            get { return m_message.ContainsKey("transaction") ? m_message["transaction"] : null; }
            set { m_message["transaction"] = value; }
        }
        public string Sender { 
            get { return m_message.ContainsKey("sender") ? m_message["sender"] : null; }
            set { m_message["sender"] = value; }
        }
        public OSDMap Jsep { 
            get { return m_message.ContainsKey("jsep") ? (m_message["jsep"] as OSDMap) : null; }
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
        // Note that the session_id is a long number in the JSON so we convert the string.
        public string sessionId { 
            get { return m_message.ContainsKey("session_id") ? OSDToLong(m_message["session_id"]).ToString() : String.Empty; }
            set { m_message["session_id"] = long.Parse(value); }
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
        public string sender {
            get { return m_message.ContainsKey("sender") ? m_message["sender"] : String.Empty; }
        }

        public virtual string ToJson()
        {
            return m_message.ToString();
        }
        public override string ToString()
        {
            return m_message.ToString();
        }
        // Utility function to convert an OSD object to an long. The OSD object can be an OSDInteger
        //    or an OSDArray of 4 or 8 integers. 
        // This exists because the JSON to OSD parser can return an OSDArray for a long number
        //    since there is not an OSDLong type.
        // The design of the OSD conversion functions kinda needs one to know how the number
        //    is stored in order to extract it. Like, if it's stored as a long value (8 bytes)
        //    and one fetches it with .AsInteger(), it will return the first 4 bytes as an integer
        //    and not the long value. So this function looks at the type of the OSD object and
        //    extracts the number appropriately.
        public long OSDToLong(OSD pIn)
        {
            long ret = 0;
            switch (pIn.Type)
            {
                case OSDType.Integer:
                    ret = (long)(pIn as OSDInteger).AsInteger();
                    break;
                case OSDType.Binary:
                    byte[] value = (pIn as OSDBinary).value;
                    if (value.Length == 4)
                    {
                        ret = (long)(pIn as OSDBinary).AsInteger();
                    }
                    if (value.Length == 8)
                    {
                        ret = (pIn as OSDBinary).AsLong();
                    }
                    break;
                case OSDType.Array:
                    if ((pIn as OSDArray).Count == 4)
                    {
                        ret = (long)pIn.AsInteger();
                    }
                    if ((pIn as OSDArray).Count == 8)
                    {
                        ret = pIn.AsLong();
                    }
                    break;
            }
            return ret;
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
            var newBody = OSDParser.DeserializeJson(pJson) as OSDMap;
            return new JanusMessageResp(newBody);
        }

        // Return the "data" portion of the response as an OSDMap or null if there is none
        public OSDMap dataSection { get { return m_message.ContainsKey("data") ? (m_message["data"] as OSDMap) : null; } }

        // Check if a successful response code is in the response
        public virtual bool isSuccess { get { return CheckReturnCode("success"); } }
        public virtual bool isEvent { get { return CheckReturnCode("event"); } }
        public virtual bool isError { get { return CheckReturnCode("error"); } }
        public virtual bool CheckReturnCode(string pCode)
        {
            return ReturnCode == pCode;
        }
        public virtual string ReturnCode { get { 
            string ret = String.Empty;
            if (m_message is not null && m_message.ContainsKey("janus"))
            {
                ret = m_message["janus"].AsString();
            }
            return ret;
        } }
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
        public int errorCode { get {
            int ret = 0;
            if (m_message.ContainsKey("error"))
            {
                var err = m_message["error"];
                if (err is OSDMap)
                    ret = (int)OSDToLong((err as OSDMap)["code"]);
            }
            return ret;
        }}

        // Dig through the response to get the error reason or empty string if there is none
        public string errorReason { get {
            string ret = String.Empty;
            if (m_message.ContainsKey("error"))
            {
                var err = m_message["error"];
                if (err is OSDMap)
                    ret = (err as OSDMap)["reason"];
            }
            // return ((m_message["error"] as OSDMap)?["reason"]) ?? String.Empty;
            return ret;
        }}
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
        public string returnedId { get {
            // The JSON response gives a long number (not a string)
            //    and the ODMap conversion interprets it as a long (OSDLong).
            // If one just does a "ToString()" on the OSD object, you
            //    get an interpretation of the binary value.
            return dataSection.ContainsKey("id") ? OSDToLong(dataSection["id"]).ToString() : String.Empty;
        }}  
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
        public string pluginId { get {
            return dataSection.ContainsKey("id") ? OSDToLong(dataSection["id"]).ToString() : String.Empty;
        }}
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
        private OSDMap m_body = new OSDMap();
        
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
        public PluginMsgResp(JanusMessageResp pResp) : base(pResp.RawBody)
        {
            if (m_message is not null && m_message.ContainsKey("plugindata"))
            {
                // Move the plugin data up into the m_data var so it is easier to get to
                m_pluginData = m_message["plugindata"] as OSDMap;
                if (m_pluginData is not null && m_pluginData.ContainsKey("data"))
                {
                    m_data = m_pluginData["data"] as OSDMap;
                    // m_log.DebugFormat("{0} AudioBridgeResp. Found both plugindata and data: data={1}", LogHeader, m_data.ToString());
                }
            }
        }

        public OSDMap PluginRespData { get { return m_data; } }

        // Get an integer value for a key in the response data or zero if not there
        public int PluginRespDataInt(string pKey)
        {
            if (m_data is null)
                return 0;
            return m_data.ContainsKey(pKey) ? (int)OSDToLong(m_data[pKey]) : 0;
        }
        // Get a string value for a key in the response data or empty string if not there
        public string PluginRespDataString(string pKey)
        {
            if (m_data is null)
                return String.Empty;
            return m_data.ContainsKey(pKey) ? m_data[pKey].AsString() : String.Empty;
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
    public class AudioBridgeResp: PluginMsgResp
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
        public AudioBridgeCreateRoomReq(int pRoomId) : this(pRoomId, false, null)
        {
        }
        public AudioBridgeCreateRoomReq(int pRoomId, bool pSpatial, string pDesc) : base(new OSDMap() {
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
            if (!String.IsNullOrEmpty(pDesc))
                AddStringToBody("description", pDesc);
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
    // A successful response contains the participant ID and the SDP
    public class AudioBridgeJoinRoomResp : AudioBridgeResp
    {
        public AudioBridgeJoinRoomResp(JanusMessageResp pResp) : base(pResp)
        {
        }
        public int ParticipantId { get { return PluginRespDataInt("id"); } }
    }
    // ==============================================================
    public class AudioBridgeConfigRoomReq : PluginMsgReq
    {
        // TODO:
        public AudioBridgeConfigRoomReq(int pRoomId, string pSdp) : base(new OSDMap() {
                                                { "request", "configure" },
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
        public AudioBridgeLeaveRoomReq(int pRoomId, int pAttendeeId) : base(new OSDMap() {
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
