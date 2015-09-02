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
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework;

namespace OpenSim.Server.Handlers
{
    public class UserProfilesHandlers
    {
        public UserProfilesHandlers ()
        {
        }
    }

    public class JsonRpcProfileHandlers
    {
        static readonly ILog m_log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);
        
        public IUserProfilesService Service
        {
            get; private set;
        }
        
        public JsonRpcProfileHandlers(IUserProfilesService service)
        {
            Service = service;
        }
        
        #region Classifieds
        /// <summary>
        /// Request avatar's classified ads.
        /// </summary>
        /// <returns>
        /// An array containing all the calassified uuid and it's name created by the creator id
        /// </returns>
        /// <param name='json'>
        /// Our parameters are in the OSDMap json["params"]
        /// </param>
        /// <param name='response'>
        /// If set to <c>true</c> response.
        /// </param>
        public bool AvatarClassifiedsRequest(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                m_log.DebugFormat ("Classified Request");
                return false;
            }
            
            OSDMap request = (OSDMap)json["params"];
            UUID creatorId = new UUID(request["creatorId"].AsString());
            
            
            OSDArray data = (OSDArray) Service.AvatarClassifiedsRequest(creatorId);
            response.Result = data;
            
            return true;
        }
        
        public bool ClassifiedUpdate(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                response.Error.Message = "Error parsing classified update request";
                m_log.DebugFormat ("Classified Update Request");
                return false;
            }
            
            string result = string.Empty;
            UserClassifiedAdd ad = new UserClassifiedAdd();
            object Ad = (object)ad;
            OSD.DeserializeMembers(ref Ad, (OSDMap)json["params"]);
            if(Service.ClassifiedUpdate(ad, ref result))
            {
                response.Result = OSD.SerializeMembers(ad);
                return true;
            }
            
            response.Error.Code = ErrorCode.InternalError;
            response.Error.Message = string.Format("{0}", result);
            return false;
        }
        
        public bool ClassifiedDelete(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                m_log.DebugFormat ("Classified Delete Request");
                return false;
            }
            
            OSDMap request = (OSDMap)json["params"];
            UUID classifiedId = new UUID(request["classifiedId"].AsString());
            
            if (Service.ClassifiedDelete(classifiedId))
                return true;

            response.Error.Code = ErrorCode.InternalError;
            response.Error.Message = "data error removing record";
            return false;
        }
        
        public bool ClassifiedInfoRequest(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                response.Error.Message = "no parameters supplied";
                m_log.DebugFormat ("Classified Info Request");
                return false;
            }
            
            string result = string.Empty;
            UserClassifiedAdd ad = new UserClassifiedAdd();
            object Ad = (object)ad;
            OSD.DeserializeMembers(ref Ad, (OSDMap)json["params"]);
            if(Service.ClassifiedInfoRequest(ref ad, ref result))
            {
                response.Result = OSD.SerializeMembers(ad);
                return true;
            }
            
            response.Error.Code = ErrorCode.InternalError;
            response.Error.Message = string.Format("{0}", result);
            return false;
        }
        #endregion Classifieds
        
        #region Picks
        public bool AvatarPicksRequest(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                m_log.DebugFormat ("Avatar Picks Request");
                return false;
            }
            
            OSDMap request = (OSDMap)json["params"];
            UUID creatorId = new UUID(request["creatorId"].AsString());
            
            
            OSDArray data = (OSDArray) Service.AvatarPicksRequest(creatorId);
            response.Result = data;
            
            return true;
        }
        
        public bool PickInfoRequest(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                response.Error.Message = "no parameters supplied";
                m_log.DebugFormat ("Avatar Picks Info Request");
                return false;
            }
            
            string result = string.Empty;
            UserProfilePick pick = new UserProfilePick();
            object Pick = (object)pick;
            OSD.DeserializeMembers(ref Pick, (OSDMap)json["params"]);
            if(Service.PickInfoRequest(ref pick, ref result))
            {
                response.Result = OSD.SerializeMembers(pick);
                return true;
            }
            
            response.Error.Code = ErrorCode.InternalError;
            response.Error.Message = string.Format("{0}", result);
            return false;
        }
        
        public bool PicksUpdate(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                response.Error.Message = "no parameters supplied";
                m_log.DebugFormat ("Avatar Picks Update Request");
                return false;
            }
            
            string result = string.Empty;
            UserProfilePick pick = new UserProfilePick();
            object Pick = (object)pick;
            OSD.DeserializeMembers(ref Pick, (OSDMap)json["params"]);
            if(Service.PicksUpdate(ref pick, ref result))
            {
                response.Result = OSD.SerializeMembers(pick);
                return true;
            }
            
            response.Error.Code = ErrorCode.InternalError;
            response.Error.Message = "unable to update pick";
            
            return false;
        }
        
        public bool PicksDelete(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                m_log.DebugFormat ("Avatar Picks Delete Request");
                return false;
            }
            
            OSDMap request = (OSDMap)json["params"];
            UUID pickId = new UUID(request["pickId"].AsString());
            if(Service.PicksDelete(pickId))
                return true;
            
            response.Error.Code = ErrorCode.InternalError;
            response.Error.Message = "data error removing record";
            return false;
        }
        #endregion Picks
        
        #region Notes
        public bool AvatarNotesRequest(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                response.Error.Message = "Params missing";
                m_log.DebugFormat ("Avatar Notes Request");
                return false;
            }

            UserProfileNotes note = new UserProfileNotes();
            object Note = (object)note;
            OSD.DeserializeMembers(ref Note, (OSDMap)json["params"]);
            if(Service.AvatarNotesRequest(ref note))
            {
                response.Result = OSD.SerializeMembers(note);
                return true;
            }

            response.Error.Code = ErrorCode.InternalError;
            response.Error.Message = "Error reading notes";
            return false;
        }
        
        public bool NotesUpdate(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                response.Error.Message = "No parameters";
                m_log.DebugFormat ("Avatar Notes Update Request");
                return false;
            }
            
            string result = string.Empty;
            UserProfileNotes note = new UserProfileNotes();
            object Notes = (object) note;
            OSD.DeserializeMembers(ref Notes, (OSDMap)json["params"]);
            if(Service.NotesUpdate(ref note, ref result))
            {
                response.Result = OSD.SerializeMembers(note);
                return true;
            }
            return true;
        }
        #endregion Notes
        
        #region Profile Properties
        public bool AvatarPropertiesRequest(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                response.Error.Message = "no parameters supplied";
                m_log.DebugFormat ("Avatar Properties Request");
                return false;
            }
            
            string result = string.Empty;
            UserProfileProperties props = new UserProfileProperties();
            object Props = (object)props;
            OSD.DeserializeMembers(ref Props, (OSDMap)json["params"]);
            if(Service.AvatarPropertiesRequest(ref props, ref result))
            {
                response.Result = OSD.SerializeMembers(props);
                return true;
            }
            
            response.Error.Code = ErrorCode.InternalError;
            response.Error.Message = string.Format("{0}", result);
            return false;
        }
        
        public bool AvatarPropertiesUpdate(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                response.Error.Message = "no parameters supplied";
                m_log.DebugFormat ("Avatar Properties Update Request");
                return false;
            }
            
            string result = string.Empty;
            UserProfileProperties props = new UserProfileProperties();
            object Props = (object)props;
            OSD.DeserializeMembers(ref Props, (OSDMap)json["params"]);
            if(Service.AvatarPropertiesUpdate(ref props, ref result))
            {
                response.Result = OSD.SerializeMembers(props);
                return true;
            }
            
            response.Error.Code = ErrorCode.InternalError;
            response.Error.Message = string.Format("{0}", result);
            return false;
        }
        #endregion Profile Properties
        
        #region Interests
        public bool AvatarInterestsUpdate(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                response.Error.Message = "no parameters supplied";
                m_log.DebugFormat ("Avatar Interests Update Request");
                return false;
            }
            
            string result = string.Empty;
            UserProfileProperties props = new UserProfileProperties();
            object Props = (object)props;
            OSD.DeserializeMembers(ref Props, (OSDMap)json["params"]);
            if(Service.AvatarInterestsUpdate(props, ref result))
            {
                response.Result = OSD.SerializeMembers(props);
                return true;
            }
            
            response.Error.Code = ErrorCode.InternalError;
            response.Error.Message = string.Format("{0}", result);
            return false;
        }
        #endregion Interests

        #region User Preferences
        public bool UserPreferencesRequest(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                m_log.DebugFormat ("User Preferences Request");
                return false;
            }

            string result = string.Empty;
            UserPreferences prefs = new UserPreferences();
            object Prefs = (object)prefs;
            OSD.DeserializeMembers(ref Prefs, (OSDMap)json["params"]);
            if(Service.UserPreferencesRequest(ref prefs, ref result))
            {
                response.Result = OSD.SerializeMembers(prefs);
                return true;
            }
            
            response.Error.Code = ErrorCode.InternalError;
            response.Error.Message = string.Format("{0}", result);
//            m_log.InfoFormat("[PROFILES]: User preferences request error - {0}", response.Error.Message);
            return false;
        }

        public bool UserPreferenecesUpdate(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                response.Error.Message = "no parameters supplied";
                m_log.DebugFormat ("User Preferences Update Request");
                return false;
            }
            
            string result = string.Empty;
            UserPreferences prefs = new UserPreferences();
            object Prefs = (object)prefs;
            OSD.DeserializeMembers(ref Prefs, (OSDMap)json["params"]);
            if(Service.UserPreferencesUpdate(ref prefs, ref result))
            {
                response.Result = OSD.SerializeMembers(prefs);
                return true;
            }
            
            response.Error.Code = ErrorCode.InternalError;
            response.Error.Message = string.Format("{0}", result);
            m_log.InfoFormat("[PROFILES]: User preferences update error - {0}", response.Error.Message);
            return false;
        }
        #endregion User Preferences


        #region Utility
        public bool AvatarImageAssetsRequest(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                m_log.DebugFormat ("Avatar Image Assets Request");
                return false;
            }
            
            OSDMap request = (OSDMap)json["params"];
            UUID avatarId = new UUID(request["avatarId"].AsString());

            OSDArray data = (OSDArray) Service.AvatarImageAssetsRequest(avatarId);
            response.Result = data;
            
            return true;
        }
        #endregion Utiltiy

        #region UserData
        public bool RequestUserAppData(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                response.Error.Message = "no parameters supplied";
                m_log.DebugFormat ("User Application Service URL Request: No Parameters!");
                return false;
            }
            
            string result = string.Empty;
            UserAppData props = new UserAppData();
            object Props = (object)props;
            OSD.DeserializeMembers(ref Props, (OSDMap)json["params"]);
            if(Service.RequestUserAppData(ref props, ref result))
            {
                OSDMap res = new OSDMap();
                res["result"] = OSD.FromString("success");
                res["token"] = OSD.FromString (result);
                response.Result = res;
                
                return true;
            }
            
            response.Error.Code = ErrorCode.InternalError;
            response.Error.Message = string.Format("{0}", result);
            return false;
        }
        
        public bool UpdateUserAppData(OSDMap json, ref JsonRpcResponse response)
        {
            if(!json.ContainsKey("params"))
            {
                response.Error.Code = ErrorCode.ParseError;
                response.Error.Message = "no parameters supplied";
                m_log.DebugFormat ("User App Data Update Request");
                return false;
            }
            
            string result = string.Empty;
            UserAppData props = new UserAppData();
            object Props = (object)props;
            OSD.DeserializeMembers(ref Props, (OSDMap)json["params"]);
            if(Service.SetUserAppData(props, ref result))
            {
                response.Result = OSD.SerializeMembers(props);
                return true;
            }
            
            response.Error.Code = ErrorCode.InternalError;
            response.Error.Message = string.Format("{0}", result);
            return false;
        }
        #endregion UserData
    }
}

