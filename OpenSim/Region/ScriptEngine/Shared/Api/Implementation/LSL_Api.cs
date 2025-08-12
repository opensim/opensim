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

using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.Packets;
using OpenMetaverse.Rendering;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.World.Land;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Animation;
using OpenSim.Region.Framework.Scenes.Scripting;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.Hypergrid;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using AssetLandmark = OpenSim.Framework.AssetLandmark;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using MappingType = OpenMetaverse.MappingType;
using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;
using PermissionMask = OpenSim.Framework.PermissionMask;
using PresenceInfo = OpenSim.Services.Interfaces.PresenceInfo;
using PrimType = OpenSim.Region.Framework.Scenes.PrimType;
using RegionFlags = OpenSim.Framework.RegionFlags;
using RegionInfo = OpenSim.Framework.RegionInfo;
using System.Runtime.CompilerServices;

#pragma warning disable IDE1006

namespace OpenSim.Region.ScriptEngine.Shared.Api
{
    /// <summary>
    /// Contains all LSL ll-functions. This class will be in Default AppDomain.
    /// </summary>
    public class LSL_Api : ILSL_Api, IScriptApi
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private int m_llRequestAgentDataCacheTimeout;
        public int LlRequestAgentDataCacheTimeoutMs
        {
            get
            {
                return 1000 * m_llRequestAgentDataCacheTimeout;
            }
            set
            {
                m_llRequestAgentDataCacheTimeout = value / 1000;
            }
       }

        protected IScriptEngine m_ScriptEngine;
        public Scene World;

        protected SceneObjectPart m_host;

        protected UUID RegionScopeID = UUID.Zero;
        protected string m_regionName = String.Empty;
        /// <summary>
        /// The item that hosts this script
        /// </summary>
        protected TaskInventoryItem m_item;

        protected bool throwErrorOnNotImplemented = false;
        protected float m_ScriptDelayFactor = 1.0f;
        protected float m_Script10mDistance = 10.0f;
        protected float m_Script10mDistanceSquare = 100.0f;
        protected float m_MinTimerInterval = 0.5f;
        protected float m_recoilScaleFactor = 0.0f;
        protected bool m_AllowGodFunctions;

        protected double m_timer = Util.GetTimeStamp();
        protected bool m_waitingForScriptAnswer = false;
        protected bool m_automaticLinkPermission = false;
        protected int m_notecardLineReadCharsMax = 255;
        protected int m_scriptConsoleChannel = 0;
        protected bool m_scriptConsoleChannelEnabled = false;
        protected bool m_debuggerSafe = false;

        protected AsyncCommandManager m_AsyncCommands = null;
        protected IUrlModule m_UrlModule = null;
        protected IMaterialsModule m_materialsModule = null;
        protected IEnvironmentModule m_envModule = null;
        protected IEmailModule m_emailModule = null;
        protected IUserAccountService m_userAccountService = null;
        protected IMessageTransferModule m_TransferModule = null;

        protected ExpiringCacheOS<UUID, PresenceInfo> m_PresenceInfoCache = new(10000);
        protected int EMAIL_PAUSE_TIME = 20;  // documented delay value for smtp.
        protected int m_sleepMsOnSetTexture = 200;
        protected int m_sleepMsOnSetLinkTexture = 200;
        protected int m_sleepMsOnScaleTexture = 200;
        protected int m_sleepMsOnOffsetTexture = 200;
        protected int m_sleepMsOnRotateTexture = 200;
        protected int m_sleepMsOnSetPos = 200;
        protected int m_sleepMsOnSetRot = 200;
        protected int m_sleepMsOnSetLocalRot = 200;
        protected int m_sleepMsOnPreloadSound = 1000;
        protected int m_sleepMsOnMakeExplosion = 100;
        protected int m_sleepMsOnMakeFountain = 100;
        protected int m_sleepMsOnMakeSmoke = 100;
        protected int m_sleepMsOnMakeFire = 100;
        protected int m_sleepMsOnRezAtRoot = 100;
        protected int m_sleepMsOnInstantMessage = 2000;
        protected int m_sleepMsOnEmail = 30000;
        protected int m_sleepMsOnCreateLink = 1000;
        protected int m_sleepMsOnGiveInventory = 3000;
        protected int m_sleepMsOnRequestAgentData = 100;
        protected int m_sleepMsOnRequestInventoryData = 1000;
        protected int m_sleepMsOnSetDamage = 5000;
        protected int m_sleepMsOnTextBox = 1000;
        protected int m_sleepMsOnAdjustSoundVolume = 100;
        protected int m_sleepMsOnEjectFromLand = 1000;
        protected int m_sleepMsOnAddToLandPassList = 100;
        protected int m_sleepMsOnDialog = 1000;
        protected int m_sleepMsOnRemoteLoadScript = 3000;
        protected int m_sleepMsOnRemoteLoadScriptPin = 3000;
        protected int m_sleepMsOnOpenRemoteDataChannel = 1000;
        protected int m_sleepMsOnSendRemoteData = 3000;
        protected int m_sleepMsOnRemoteDataReply = 3000;
        protected int m_sleepMsOnCloseRemoteDataChannel = 1000;
        protected int m_sleepMsOnSetPrimitiveParams = 200;
        protected int m_sleepMsOnSetLinkPrimitiveParams = 200;
        protected int m_sleepMsOnXorBase64Strings = 300;
        protected int m_sleepMsOnSetParcelMusicURL = 2000;
        protected int m_sleepMsOnGetPrimMediaParams = 1000;
        protected int m_sleepMsOnGetLinkMedia = 1000;
        protected int m_sleepMsOnSetPrimMediaParams = 1000;
        protected int m_sleepMsOnSetLinkMedia = 1000;
        protected int m_sleepMsOnClearPrimMedia = 1000;
        protected int m_sleepMsOnClearLinkMedia = 1000;
        protected int m_sleepMsOnRequestSimulatorData = 1000;
        protected int m_sleepMsOnLoadURL = 1000;
        protected int m_sleepMsOnParcelMediaCommandList = 2000;
        protected int m_sleepMsOnParcelMediaQuery = 2000;
        protected int m_sleepMsOnModPow = 1000;
        protected int m_sleepMsOnSetPrimURL = 2000;
        protected int m_sleepMsOnRefreshPrimURL = 20000;
        protected int m_sleepMsOnMapDestination = 1000;
        protected int m_sleepMsOnAddToLandBanList = 100;
        protected int m_sleepMsOnRemoveFromLandPassList = 100;
        protected int m_sleepMsOnRemoveFromLandBanList = 100;
        protected int m_sleepMsOnResetLandBanList = 100;
        protected int m_sleepMsOnResetLandPassList = 100;
        protected int m_sleepMsOnGetParcelPrimOwners = 2000;
        protected int m_sleepMsOnGetNumberOfNotecardLines = 100;
        protected int m_sleepMsOnGetNotecardLine = 100;
        protected string m_internalObjectHost = "lsl.opensim.local";
        protected bool m_restrictEmail = false;
        protected ISoundModule m_SoundModule = null;

        protected float m_avatarHeightCorrection = 0.2f;
        protected bool m_useSimpleBoxesInGetBoundingBox = false;
        protected bool m_addStatsInGetBoundingBox = false;

        //LSL Avatar Bounding Box (lABB), lower (1) and upper (2),
        //standing (Std), Groundsitting (Grs), Sitting (Sit),
        //along X, Y and Z axes, constants (0) and coefficients (1)
        protected float m_lABB1StdX0 = -0.275f;
        protected float m_lABB2StdX0 = 0.275f;
        protected float m_lABB1StdY0 = -0.35f;
        protected float m_lABB2StdY0 = 0.35f;
        protected float m_lABB1StdZ0 = -0.1f;
        protected float m_lABB1StdZ1 = -0.5f;
        protected float m_lABB2StdZ0 = 0.1f;
        protected float m_lABB2StdZ1 = 0.5f;
        protected float m_lABB1GrsX0 = -0.3875f;
        protected float m_lABB2GrsX0 = 0.3875f;
        protected float m_lABB1GrsY0 = -0.5f;
        protected float m_lABB2GrsY0 = 0.5f;
        protected float m_lABB1GrsZ0 = -0.05f;
        protected float m_lABB1GrsZ1 = -0.375f;
        protected float m_lABB2GrsZ0 = 0.5f;
        protected float m_lABB2GrsZ1 = 0.0f;
        protected float m_lABB1SitX0 = -0.5875f;
        protected float m_lABB2SitX0 = 0.1875f;
        protected float m_lABB1SitY0 = -0.35f;
        protected float m_lABB2SitY0 = 0.35f;
        protected float m_lABB1SitZ0 = -0.35f;
        protected float m_lABB1SitZ1 = -0.375f;
        protected float m_lABB2SitZ0 = -0.25f;
        protected float m_lABB2SitZ1 = 0.25f;

        protected float m_primSafetyCoeffX = 2.414214f;
        protected float m_primSafetyCoeffY = 2.414214f;
        protected float m_primSafetyCoeffZ = 1.618034f;

        protected float m_floatToleranceInCastRay = 0.00001f;
        protected float m_floatTolerance2InCastRay = 0.001f;
        protected DetailLevel m_primLodInCastRay = DetailLevel.Medium;
        protected DetailLevel m_sculptLodInCastRay = DetailLevel.Medium;
        protected DetailLevel m_meshLodInCastRay = DetailLevel.Highest;
        protected DetailLevel m_avatarLodInCastRay = DetailLevel.Medium;
        protected int m_maxHitsInCastRay = 16;
        protected int m_maxHitsPerPrimInCastRay = 16;
        protected int m_maxHitsPerObjectInCastRay = 16;
        protected bool m_detectExitsInCastRay = false;
        protected bool m_doAttachmentsInCastRay = false;
        protected int m_msThrottleInCastRay = 200;
        protected int m_msPerRegionInCastRay = 40;
        protected int m_msPerAvatarInCastRay = 10;
        protected int m_msMinInCastRay = 2;
        protected int m_msMaxInCastRay = 40;
        protected static List<CastRayCall> m_castRayCalls = new();
        protected bool m_useMeshCacheInCastRay = true;
        protected static Dictionary<ulong, FacetedMesh> m_cachedMeshes = new();

        //protected Timer m_ShoutSayTimer;
        protected int m_SayShoutCount = 0;
        DateTime m_lastSayShoutCheck;

        private int m_whisperdistance = 10;
        private int m_saydistance = 20;
        private int m_shoutdistance = 100;

        bool m_disable_underground_movement = true;

        private string m_lsl_shard = "OpenSim";
        private string m_lsl_user_agent = string.Empty;

        private int m_linksetDataLimit = 32 * 1024;

        private static readonly Dictionary<string, string> MovementAnimationsForLSL = new(StringComparer.InvariantCultureIgnoreCase)
        {
            {"CROUCH", "Crouching"},
            {"CROUCHWALK", "CrouchWalking"},
            {"FALLDOWN", "Falling Down"},
            {"FLY", "Flying"},
            {"FLYSLOW", "FlyingSlow"},
            {"HOVER", "Hovering"},
            {"HOVER_UP", "Hovering Up"},
            {"HOVER_DOWN", "Hovering Down"},
            {"JUMP", "Jumping"},
            {"LAND", "Landing"},
            {"PREJUMP", "PreJumping"},
            {"RUN", "Running"},
            {"SIT","Sitting"},
            {"SITGROUND","Sitting on Ground"},
            {"STAND", "Standing"},
            {"STANDUP", "Standing Up"},
            {"STRIDE","Striding"},
            {"SOFT_LAND", "Soft Landing"},
            {"TURNLEFT", "Turning Left"},
            {"TURNRIGHT", "Turning Right"},
            {"WALK", "Walking"}
        };

        //llHTTPRequest custom headers use control
        // true means fatal error,
        // false means ignore,
        // missing means allowed
        private static readonly Dictionary<string,bool> HttpForbiddenHeaders = new(StringComparer.InvariantCultureIgnoreCase)
        {
            {"Accept", true},
            {"Accept-Charset", true},
            {"Accept-CH", false},
            {"Accept-CH-Lifetime", false},
            {"Access-Control-Request-Headers", false},
            {"Access-Control-Request-Method", false},
            {"Accept-Encoding", false},
            //{"Accept-Language", false},
            {"Accept-Patch", false}, // it is server side
            {"Accept-Post", false}, // it is server side
            {"Accept-Ranges", false}, // it is server side
            //{"Age", false},
            //{"Allow", false},
            //{"Authorization", false},
            {"Cache-Control", false},
            {"Connection", false},
            {"Content-Length", false},
            //{"Content-Encoding", false},
            //{"Content-Location", false},
            //{"Content-MD5", false},
            //{"Content-Range", false},
            {"Content-Type", true},
            {"Cookie", false},
            {"Cookie2", false},
            {"Date", false},
            {"Device-Memory", false},
            {"DTN", false},
            {"Early-Data", false},
            //{"ETag", false},
            {"Expect", false},
            //{"Expires", false},
            {"Feature-Policy", false},
            {"From", true},
            {"Host", true},
            {"Keep-Alive", false},
            {"If-Match", false},
            {"If-Modified-Since", false},
            {"If-None-Match", false},
            //{"If-Range", false},
            {"If-Unmodified-Since", false},
            //{"Last-Modified", false},
            //{"Location", false},
            {"Max-Forwards", false},
            {"Origin", false},
            {"Pragma", false},
            //{"Proxy-Authenticate", false},
            //{"Proxy-Authorization", false},
            //{"Range", false},
            {"Referer", true},
            //{"Retry-After", false},
            {"Server", false},
            {"Set-Cookie", false},
            {"Set-Cookie2", false},
            {"TE", true},
            {"Trailer", true},
            {"Transfer-Encoding", false},
            {"Upgrade", true},
            {"User-Agent", true},
            {"Vary", false},
            {"Via", true},
            {"Viewport-Width", false},
            {"Warning", false},
            {"Width", false},
            //{"WWW-Authenticate", false},

            {"X-Forwarded-For", false},
            {"X-Forwarded-Host", false},
            {"X-Forwarded-Proto", false},

            {"x-secondlife-shard", false},
            {"x-secondlife-object-name", false},
            {"x-secondlife-object-key", false},
            {"x-secondlife-region", false},
            {"x-secondlife-local-position", false},
            {"x-secondlife-local-velocity", false},
            {"x-secondlife-local-rotation", false},
            {"x-secondlife-owner-name", false},
            {"x-secondlife-owner-key", false},
        };

        private static readonly HashSet<string> HttpForbiddenInHeaders = new(StringComparer.InvariantCultureIgnoreCase)
        {
            "x-secondlife-shard", "x-secondlife-object-name",  "x-secondlife-object-key",
            "x-secondlife-region", "x-secondlife-local-position", "x-secondlife-local-velocity",
            "x-secondlife-local-rotation",  "x-secondlife-owner-name", "x-secondlife-owner-key",
            "connection", "content-length", "from", "host", "proxy-authorization",
            "referer", "trailer", "transfer-encoding", "via", "authorization"
        };

        public void Initialize(IScriptEngine scriptEngine, SceneObjectPart host, TaskInventoryItem item)
        {
            m_lastSayShoutCheck = DateTime.UtcNow;

            m_ScriptEngine = scriptEngine;
            World = m_ScriptEngine.World;
            m_host = host;
            m_item = item;
            m_debuggerSafe = m_ScriptEngine.Config.GetBoolean("DebuggerSafe", false);

            LoadConfig();

            m_TransferModule = m_ScriptEngine.World.RequestModuleInterface<IMessageTransferModule>();
            m_UrlModule = m_ScriptEngine.World.RequestModuleInterface<IUrlModule>();
            m_SoundModule = m_ScriptEngine.World.RequestModuleInterface<ISoundModule>();
            m_materialsModule = m_ScriptEngine.World.RequestModuleInterface<IMaterialsModule>();

            m_emailModule = m_ScriptEngine.World.RequestModuleInterface<IEmailModule>();
            m_envModule = m_ScriptEngine.World.RequestModuleInterface< IEnvironmentModule>();

            m_AsyncCommands = new AsyncCommandManager(m_ScriptEngine);
            m_userAccountService = World.UserAccountService;
            if(World.RegionInfo != null)
            {
                RegionScopeID = World.RegionInfo.ScopeID;
                m_regionName = World.RegionInfo.RegionName;
            }
        }

        /// <summary>
        /// Load configuration items that affect script, object and run-time behavior. */
        /// </summary>
        private void LoadConfig()
        {
            LlRequestAgentDataCacheTimeoutMs = 20000;

            IConfig seConfig = m_ScriptEngine.Config;

            if (seConfig is not null)
            {
                float scriptDistanceFactor = seConfig.GetFloat("ScriptDistanceLimitFactor", 1.0f);
                m_Script10mDistance = 10.0f * scriptDistanceFactor;
                m_Script10mDistanceSquare = m_Script10mDistance * m_Script10mDistance;

                m_ScriptDelayFactor = seConfig.GetFloat("ScriptDelayFactor", m_ScriptDelayFactor);
                m_MinTimerInterval         = seConfig.GetFloat("MinTimerInterval", m_MinTimerInterval);
                m_automaticLinkPermission  = seConfig.GetBoolean("AutomaticLinkPermission", m_automaticLinkPermission);
                m_notecardLineReadCharsMax = seConfig.GetInt("NotecardLineReadCharsMax", m_notecardLineReadCharsMax);

                // Rezzing an object with a velocity can create recoil. This feature seems to have been
                //    removed from recent versions of SL. The code computes recoil (vel*mass) and scales
                //    it by this factor. May be zero to turn off recoil all together.
                m_recoilScaleFactor = seConfig.GetFloat("RecoilScaleFactor", m_recoilScaleFactor);
                m_AllowGodFunctions = seConfig.GetBoolean("AllowGodFunctions", false);

                m_disable_underground_movement = seConfig.GetBoolean("DisableUndergroundMovement", true);

                m_linksetDataLimit = seConfig.GetInt("LinksetDataLimit", m_linksetDataLimit);
            }

            if (m_notecardLineReadCharsMax > 65535)
                m_notecardLineReadCharsMax = 65535;

            // load limits for particular subsystems.
            IConfigSource seConfigSource = m_ScriptEngine.ConfigSource;

            if (seConfigSource != null)
            {
                IConfig netConfig = seConfigSource.Configs["Network"];
                if (netConfig != null)
                {
                    m_lsl_shard = netConfig.GetString("shard", m_lsl_shard);
                    m_lsl_user_agent = netConfig.GetString("user_agent", m_lsl_user_agent);
                }

                IConfig lslConfig = seConfigSource.Configs["LL-Functions"];
                if (lslConfig != null)
                {
                    m_restrictEmail = lslConfig.GetBoolean("RestrictEmail", m_restrictEmail);
                    m_avatarHeightCorrection = lslConfig.GetFloat("AvatarHeightCorrection", m_avatarHeightCorrection);
                    m_useSimpleBoxesInGetBoundingBox = lslConfig.GetBoolean("UseSimpleBoxesInGetBoundingBox", m_useSimpleBoxesInGetBoundingBox);
                    m_addStatsInGetBoundingBox = lslConfig.GetBoolean("AddStatsInGetBoundingBox", m_addStatsInGetBoundingBox);
                    m_lABB1StdX0 = lslConfig.GetFloat("LowerAvatarBoundingBoxStandingXconst", m_lABB1StdX0);
                    m_lABB2StdX0 = lslConfig.GetFloat("UpperAvatarBoundingBoxStandingXconst", m_lABB2StdX0);
                    m_lABB1StdY0 = lslConfig.GetFloat("LowerAvatarBoundingBoxStandingYconst", m_lABB1StdY0);
                    m_lABB2StdY0 = lslConfig.GetFloat("UpperAvatarBoundingBoxStandingYconst", m_lABB2StdY0);
                    m_lABB1StdZ0 = lslConfig.GetFloat("LowerAvatarBoundingBoxStandingZconst", m_lABB1StdZ0);
                    m_lABB1StdZ1 = lslConfig.GetFloat("LowerAvatarBoundingBoxStandingZcoeff", m_lABB1StdZ1);
                    m_lABB2StdZ0 = lslConfig.GetFloat("UpperAvatarBoundingBoxStandingZconst", m_lABB2StdZ0);
                    m_lABB2StdZ1 = lslConfig.GetFloat("UpperAvatarBoundingBoxStandingZcoeff", m_lABB2StdZ1);
                    m_lABB1GrsX0 = lslConfig.GetFloat("LowerAvatarBoundingBoxGroundsittingXconst", m_lABB1GrsX0);
                    m_lABB2GrsX0 = lslConfig.GetFloat("UpperAvatarBoundingBoxGroundsittingXconst", m_lABB2GrsX0);
                    m_lABB1GrsY0 = lslConfig.GetFloat("LowerAvatarBoundingBoxGroundsittingYconst", m_lABB1GrsY0);
                    m_lABB2GrsY0 = lslConfig.GetFloat("UpperAvatarBoundingBoxGroundsittingYconst", m_lABB2GrsY0);
                    m_lABB1GrsZ0 = lslConfig.GetFloat("LowerAvatarBoundingBoxGroundsittingZconst", m_lABB1GrsZ0);
                    m_lABB1GrsZ1 = lslConfig.GetFloat("LowerAvatarBoundingBoxGroundsittingZcoeff", m_lABB1GrsZ1);
                    m_lABB2GrsZ0 = lslConfig.GetFloat("UpperAvatarBoundingBoxGroundsittingZconst", m_lABB2GrsZ0);
                    m_lABB2GrsZ1 = lslConfig.GetFloat("UpperAvatarBoundingBoxGroundsittingZcoeff", m_lABB2GrsZ1);
                    m_lABB1SitX0 = lslConfig.GetFloat("LowerAvatarBoundingBoxSittingXconst", m_lABB1SitX0);
                    m_lABB2SitX0 = lslConfig.GetFloat("UpperAvatarBoundingBoxSittingXconst", m_lABB2SitX0);
                    m_lABB1SitY0 = lslConfig.GetFloat("LowerAvatarBoundingBoxSittingYconst", m_lABB1SitY0);
                    m_lABB2SitY0 = lslConfig.GetFloat("UpperAvatarBoundingBoxSittingYconst", m_lABB2SitY0);
                    m_lABB1SitZ0 = lslConfig.GetFloat("LowerAvatarBoundingBoxSittingZconst", m_lABB1SitZ0);
                    m_lABB1SitZ1 = lslConfig.GetFloat("LowerAvatarBoundingBoxSittingZcoeff", m_lABB1SitZ1);
                    m_lABB2SitZ0 = lslConfig.GetFloat("UpperAvatarBoundingBoxSittingZconst", m_lABB2SitZ0);
                    m_lABB2SitZ1 = lslConfig.GetFloat("UpperAvatarBoundingBoxSittingZcoeff", m_lABB2SitZ1);
                    m_primSafetyCoeffX = lslConfig.GetFloat("PrimBoundingBoxSafetyCoefficientX", m_primSafetyCoeffX);
                    m_primSafetyCoeffY = lslConfig.GetFloat("PrimBoundingBoxSafetyCoefficientY", m_primSafetyCoeffY);
                    m_primSafetyCoeffZ = lslConfig.GetFloat("PrimBoundingBoxSafetyCoefficientZ", m_primSafetyCoeffZ);
                    m_floatToleranceInCastRay = lslConfig.GetFloat("FloatToleranceInLlCastRay", m_floatToleranceInCastRay);
                    m_floatTolerance2InCastRay = lslConfig.GetFloat("FloatTolerance2InLlCastRay", m_floatTolerance2InCastRay);
                    m_primLodInCastRay = (DetailLevel)lslConfig.GetInt("PrimDetailLevelInLlCastRay", (int)m_primLodInCastRay);
                    m_sculptLodInCastRay = (DetailLevel)lslConfig.GetInt("SculptDetailLevelInLlCastRay", (int)m_sculptLodInCastRay);
                    m_meshLodInCastRay = (DetailLevel)lslConfig.GetInt("MeshDetailLevelInLlCastRay", (int)m_meshLodInCastRay);
                    m_avatarLodInCastRay = (DetailLevel)lslConfig.GetInt("AvatarDetailLevelInLlCastRay", (int)m_avatarLodInCastRay);
                    m_maxHitsInCastRay = lslConfig.GetInt("MaxHitsInLlCastRay", m_maxHitsInCastRay);
                    m_maxHitsPerPrimInCastRay = lslConfig.GetInt("MaxHitsPerPrimInLlCastRay", m_maxHitsPerPrimInCastRay);
                    m_maxHitsPerObjectInCastRay = lslConfig.GetInt("MaxHitsPerObjectInLlCastRay", m_maxHitsPerObjectInCastRay);
                    m_detectExitsInCastRay = lslConfig.GetBoolean("DetectExitHitsInLlCastRay", m_detectExitsInCastRay);
                    m_doAttachmentsInCastRay = lslConfig.GetBoolean("DoAttachmentsInLlCastRay", m_doAttachmentsInCastRay);
                    m_msThrottleInCastRay = lslConfig.GetInt("ThrottleTimeInMsInLlCastRay", m_msThrottleInCastRay);
                    m_msPerRegionInCastRay = lslConfig.GetInt("AvailableTimeInMsPerRegionInLlCastRay", m_msPerRegionInCastRay);
                    m_msPerAvatarInCastRay = lslConfig.GetInt("AvailableTimeInMsPerAvatarInLlCastRay", m_msPerAvatarInCastRay);
                    m_msMinInCastRay = lslConfig.GetInt("RequiredAvailableTimeInMsInLlCastRay", m_msMinInCastRay);
                    m_msMaxInCastRay = lslConfig.GetInt("MaximumAvailableTimeInMsInLlCastRay", m_msMaxInCastRay);
                    m_useMeshCacheInCastRay = lslConfig.GetBoolean("UseMeshCacheInLlCastRay", m_useMeshCacheInCastRay);
                }

                IConfig smtpConfig = seConfigSource.Configs["SMTP"];
                if (smtpConfig != null)
                {
                    // there's an smtp config, so load in the snooze time.
                    EMAIL_PAUSE_TIME = smtpConfig.GetInt("email_pause_time", EMAIL_PAUSE_TIME);

                    m_internalObjectHost = smtpConfig.GetString("internal_object_host", m_internalObjectHost);
                }

                IConfig chatConfig = seConfigSource.Configs["SMTP"];
                if(chatConfig != null)
                {
                    m_whisperdistance = chatConfig.GetInt("whisper_distance", m_whisperdistance);
                    m_saydistance = chatConfig.GetInt("say_distance", m_saydistance);
                    m_shoutdistance = chatConfig.GetInt("shout_distance", m_shoutdistance);
                }
            }
            m_sleepMsOnEmail = EMAIL_PAUSE_TIME * 1000;
        }

        protected SceneObjectPart MonitoringObject()
        {
            UUID m = m_host.ParentGroup.MonitoringObject;
            if (m.IsZero())
                return null;

            SceneObjectPart p = m_ScriptEngine.World.GetSceneObjectPart(m);
            if (p == null)
                m_host.ParentGroup.MonitoringObject = UUID.Zero;

            return p;
        }

        protected virtual void ScriptSleep(int delay)
        {
            delay = (int)(delay * m_ScriptDelayFactor);
            if (delay < 10)
                return;

            Sleep(delay);
        }

        protected virtual void Sleep(int delay)
        {
            if (m_item == null) // Some unit tests don't set this
                Thread.Sleep(delay);
            else
                m_ScriptEngine.SleepScript(m_item.ItemID, delay);
        }

        [DebuggerNonUserCode]
        public void state(string newState)
        {
            m_ScriptEngine.SetState(m_item.ItemID, newState);
        }

        /// <summary>
        /// Reset the named script. The script must be present
        /// in the same prim.
        /// </summary>
        [DebuggerNonUserCode]
        public void llResetScript()
        {
            // We need to tell the URL module, if we hav one, to release
            // the allocated URLs
            m_UrlModule?.ScriptRemoved(m_item.ItemID);

            m_ScriptEngine.ApiResetScript(m_item.ItemID);
        }

        public void llResetOtherScript(string name)
        {
            UUID item = GetScriptByName(name);

            if (item.IsZero())
            {
                Error("llResetOtherScript", "Can't find script '" + name + "'");
                return;
            }
            if(item.Equals(m_item.ItemID))
                llResetScript();
            else
            {
                m_ScriptEngine.ResetScript(item);
            }
        }

        public LSL_Integer llGetScriptState(string name)
        {
            UUID item = GetScriptByName(name);

            if (!item.IsZero())
            {
                return m_ScriptEngine.GetScriptState(item) ?1:0;
            }

            Error("llGetScriptState", "Can't find script '" + name + "'");

            // If we didn't find it, then it's safe to
            // assume it is not running.

            return 0;
        }

        public void llSetScriptState(string name, int run)
        {
            UUID item = GetScriptByName(name);


            // These functions are supposed to be robust,
            // so get the state one step at a time.

            if (!item.IsZero())
            {
                m_ScriptEngine.SetScriptState(item, run != 0, item.Equals(m_item.ItemID));
            }
            else
            {
                Error("llSetScriptState", "Can't find script '" + name + "'");
            }
        }

        public List<ScenePresence> GetLinkAvatars(int linkType)
        {
            if (m_host is null)
                return new List<ScenePresence>();

            return GetLinkAvatars(linkType, m_host.ParentGroup);

        }

        public static List<ScenePresence> GetLinkAvatars(int linkType, SceneObjectGroup sog)
        {
            List<ScenePresence> ret = new();
            if (sog is null || sog.IsDeleted)
                return ret;

            List<ScenePresence> avs = sog.GetSittingAvatars();
            switch (linkType)
            {
                case ScriptBaseClass.LINK_SET:
                    return avs;

                case ScriptBaseClass.LINK_ROOT:
                    return ret;

                case ScriptBaseClass.LINK_ALL_OTHERS:
                    return avs;

                case ScriptBaseClass.LINK_ALL_CHILDREN:
                    return avs;

                case ScriptBaseClass.LINK_THIS:
                    return ret;

                default:
                    if (linkType < 0)
                        return ret;

                    int partCount = sog.GetPartCount();

                    linkType -= partCount;
                    if (linkType <= 0)
                    {
                        return ret;
                    }
                    else
                    {
                        if (linkType > avs.Count)
                        {
                            return ret;
                        }
                        else
                        {
                            ret.Add(avs[linkType - 1]);
                            return ret;
                        }
                    }
            }
        }

        /// <summary>
        /// Get a given link entity from a linkset (linked objects and any sitting avatars).
        /// </summary>
        /// <remarks>
        /// If there are any ScenePresence's in the linkset (i.e. because they are sat upon one of the prims), then
        /// these are counted as extra entities that correspond to linknums beyond the number of prims in the linkset.
        /// The ScenePresences receive linknums in the order in which they sat.
        /// </remarks>
        /// <returns>
        /// The link entity.  null if not found.
        /// </returns>
        /// <param name='part'></param>
        /// <param name='linknum'>
        /// Can be either a non-negative integer or ScriptBaseClass.LINK_THIS (-4).
        /// If ScriptBaseClass.LINK_THIS then the entity containing the script is returned.
        /// If the linkset has one entity and a linknum of zero is given, then the single entity is returned.  If any
        /// positive integer is given in this case then null is returned.
        /// If the linkset has more than one entity and a linknum greater than zero but equal to or less than the number
        /// of entities, then the entity which corresponds to that linknum is returned.
        /// Otherwise, if a positive linknum is given which is greater than the number of entities in the linkset, then
        /// null is returned.
        /// </param>
        public static ISceneEntity GetLinkEntity(SceneObjectPart part, int linknum)
        {
            if (linknum == ScriptBaseClass.LINK_THIS)
               return part;

            if (linknum <= part.ParentGroup.PrimCount)
                return part.ParentGroup.GetLinkNumPart(linknum);

            return part.ParentGroup.GetLinkSitingAvatar(linknum);
        }

        public List<SceneObjectPart> GetLinkParts(int linkType)
        {
            return GetLinkParts(m_host, linkType);
        }

        public static List<SceneObjectPart> GetLinkParts(SceneObjectPart part, int linkType)
        {
            if (part is null || part.ParentGroup is null || part.ParentGroup.IsDeleted)
                return new List<SceneObjectPart>();

            List<SceneObjectPart> ret;
            switch (linkType)
            {
                case ScriptBaseClass.LINK_SET:
                    return new List<SceneObjectPart>(part.ParentGroup.Parts);

                case ScriptBaseClass.LINK_ROOT:
                    return new List<SceneObjectPart> { part.ParentGroup.RootPart };

                case ScriptBaseClass.LINK_ALL_OTHERS:
                    ret = new List<SceneObjectPart>(part.ParentGroup.Parts);
                    ret.Remove(part);
                    return ret;

                case ScriptBaseClass.LINK_ALL_CHILDREN:
                    ret = new List<SceneObjectPart>(part.ParentGroup.Parts);
                    ret.Remove(part.ParentGroup.RootPart);
                    return ret;

                case ScriptBaseClass.LINK_THIS:
                    return new List<SceneObjectPart> { part };

                default:
                    SceneObjectPart target = part.ParentGroup.GetLinkNumPart(linkType);
                    if (target is not null)
                        return new List<SceneObjectPart> { target };
                    return new List<SceneObjectPart>();
            }
        }

        public List<ISceneEntity> GetLinkEntities(int linkType)
        {
            return GetLinkEntities(m_host, linkType);
        }

        public static List<ISceneEntity> GetLinkEntities(SceneObjectPart part, int linkType)
        {
            List<ISceneEntity> ret;

            switch (linkType)
            {
                case ScriptBaseClass.LINK_SET:
                    return new List<ISceneEntity>(part.ParentGroup.Parts);

                case ScriptBaseClass.LINK_ROOT:
                    return new List<ISceneEntity>() { part.ParentGroup.RootPart };

                case ScriptBaseClass.LINK_ALL_OTHERS:
                    ret = new List<ISceneEntity>(part.ParentGroup.Parts);
                    ret.Remove(part);
                    return ret;

                case ScriptBaseClass.LINK_ALL_CHILDREN:
                    ret = new List<ISceneEntity>(part.ParentGroup.Parts);
                    ret.Remove(part.ParentGroup.RootPart);

                    List<ScenePresence> avs = part.ParentGroup.GetSittingAvatars();
                    if(avs is not null && avs.Count > 0)
                        ret.AddRange(avs);

                    return ret;

                case ScriptBaseClass.LINK_THIS:
                    return new List<ISceneEntity>() { part };

                default:
                    if (linkType < 0)
                        return new List<ISceneEntity>();

                    ISceneEntity target = GetLinkEntity(part, linkType);
                    if (target is null)
                        return new List<ISceneEntity>();

                    return new List<ISceneEntity>() { target };
            }
        }

        //These are the implementations of the various ll-functions used by the LSL scripts.
        public LSL_Float llSin(double f)
        {
            return Math.Sin(f);
        }

        public LSL_Float llCos(double f)
        {
            return Math.Cos(f);
        }

        public LSL_Float llTan(double f)
        {
            return Math.Tan(f);
        }

        public LSL_Float llAtan2(LSL_Float y, LSL_Float x)
        {
            return Math.Atan2(y, x);
        }

        public LSL_Float llSqrt(double f)
        {
            return Math.Sqrt(f);
        }

        public LSL_Float llPow(double fbase, double fexponent)
        {
            return (double)Math.Pow(fbase, fexponent);
        }

        public LSL_Integer llAbs(LSL_Integer i)
        {
            // changed to replicate LSL behaviour whereby minimum int value is returned untouched.
            if (i == Int32.MinValue)
                return i;
            else
                return Math.Abs(i);
        }

        public LSL_Float llFabs(double f)
        {
            return (double)Math.Abs(f);
        }

        public LSL_Float llFrand(double mag)
        {
            return Random.Shared.NextDouble() * mag;
        }

        public LSL_Integer llFloor(double f)
        {
            return (int)Math.Floor(f);
        }

        public LSL_Integer llCeil(double f)
        {
            return (int)Math.Ceiling(f);
        }

        // Xantor 01/May/2008 fixed midpointrounding (2.5 becomes 3.0 instead of 2.0, default = ToEven)
        public LSL_Integer llRound(double f)
        {
            return (int)Math.Round(f, MidpointRounding.AwayFromZero);
        }

        //This next group are vector operations involving squaring and square root. ckrinke
        public LSL_Float llVecMag(LSL_Vector v)
        {
            return LSL_Vector.Mag(v);
        }

        public LSL_Vector llVecNorm(LSL_Vector v)
        {
            return LSL_Vector.Norm(v);
        }

        private static double VecDist(LSL_Vector a, LSL_Vector b)
        {
            double dx = a.x - b.x;
            double dy = a.y - b.y;
            double dz = a.z - b.z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static double VecDistSquare(LSL_Vector a, LSL_Vector b)
        {
            double dx = a.x - b.x;
            double dy = a.y - b.y;
            double dz = a.z - b.z;
            return dx * dx + dy * dy + dz * dz;
        }

        public LSL_Float llVecDist(LSL_Vector a, LSL_Vector b)
        {
            return VecDist(a, b);
        }

        public LSL_Vector llRot2Euler(LSL_Rotation q1)
        {
            double sqw = q1.s * q1.s;
            double sqx = q1.x * q1.x;
            double sqy = q1.z * q1.z;
            double sqz = q1.y * q1.y;
            double norm = sqx + sqy + sqz + sqw; // if normalised is one, otherwise is correction factor
            double halfnorm = 0.49999 * norm;

            double test = q1.x * q1.z + q1.y * q1.s;
            if (test > halfnorm) // singularity at north pole
            {
                return new LSL_Vector(
                    0,
                    Math.PI / 2,
                    2 * Math.Atan2(q1.x, q1.s)
                    );
            }
            if (test < -halfnorm) // singularity at south pole
            {
                return new LSL_Vector(
                    0,
                    -Math.PI / 2,
                    -2 * Math.Atan2(q1.x, q1.s)
                    );
            }

            return new LSL_Vector(
                Math.Atan2(2 * q1.x * q1.s - 2 * q1.z * q1.y , -sqx + sqy - sqz + sqw),
                Math.Asin( 2 * test / norm),
                Math.Atan2(2 * q1.z * q1.s - 2 * q1.x * q1.y, sqx - sqy - sqz + sqw)
                );
        }

        public LSL_Rotation llEuler2Rot(LSL_Vector v)
        {
            double a = v.x * 0.5;
            double s1 = Math.Sin(a);
            double c1 = Math.Cos(a);
            a = v.y * 0.5;
            double s2 = Math.Sin(a);
            double c2 = Math.Cos(a);
            a = v.z * 0.5;
            double s3 = Math.Sin(a);
            double c3 = Math.Cos(a);

            return new LSL_Rotation(
                s1 * c2 * c3 + c1 * s2 * s3,
                c1 * s2 * c3 - s1 * c2 * s3,
                s1 * s2 * c3 + c1 * c2 * s3,
                c1 * c2 * c3 - s1 * s2 * s3
                );
        }

        public LSL_Rotation llAxes2Rot(LSL_Vector fwd, LSL_Vector left, LSL_Vector up)
        {
            double s;
            double tr = fwd.x + left.y + up.z + 1.0;

            if (tr >= 1.0)
            {
                s = 0.5 / Math.Sqrt(tr);
                return new LSL_Rotation(
                        (left.z - up.y) * s,
                        (up.x - fwd.z) * s,
                        (fwd.y - left.x) * s,
                        0.25 / s);
            }
            else
            {
                double max = (left.y > up.z) ? left.y : up.z;

                if (max < fwd.x)
                {
                    s = Math.Sqrt(fwd.x - (left.y + up.z) + 1.0);
                    double x = s * 0.5;
                    s = 0.5 / s;
                    return new LSL_Rotation(
                            x,
                            (fwd.y + left.x) * s,
                            (up.x + fwd.z) * s,
                            (left.z - up.y) * s);
                }
                else if (max == left.y)
                {
                    s = Math.Sqrt(left.y - (up.z + fwd.x) + 1.0);
                    double y = s * 0.5;
                    s = 0.5 / s;
                    return new LSL_Rotation(
                            (fwd.y + left.x) * s,
                            y,
                            (left.z + up.y) * s,
                            (up.x - fwd.z) * s);
                }
                else
                {
                    s = Math.Sqrt(up.z - (fwd.x + left.y) + 1.0);
                    double z = s * 0.5;
                    s = 0.5 / s;
                    return new LSL_Rotation(
                            (up.x + fwd.z) * s,
                            (left.z + up.y) * s,
                            z,
                            (fwd.y - left.x) * s);
                }
            }
        }

        public LSL_Vector llRot2Fwd(LSL_Rotation r)
        {
            double m = r.x * r.x + r.y * r.y + r.z * r.z + r.s * r.s;
            if (Math.Abs(1.0 - m) > 0.000001)
            {
                m = 1.0 / Math.Sqrt(m);
                r.x *= m;
                r.y *= m;
                r.z *= m;
                r.s *= m;
            }

            return new LSL_Vector(
                r.x * r.x - r.y * r.y - r.z * r.z + r.s * r.s,
                2 * (r.x * r.y + r.z * r.s),
                2 * (r.x * r.z - r.y * r.s)
                );
        }

        public LSL_Vector llRot2Left(LSL_Rotation r)
        {
            double m = r.x * r.x + r.y * r.y + r.z * r.z + r.s * r.s;
            if (Math.Abs(1.0 - m) > 0.000001) // allow a little slop here for calculation precision
            {
                m = 1.0 / Math.Sqrt(m);
                r.x *= m;
                r.y *= m;
                r.z *= m;
                r.s *= m;
            }

            return new LSL_Vector(
                2 * (r.x * r.y - r.z * r.s),
                -r.x * r.x + r.y * r.y - r.z * r.z + r.s * r.s,
                2 * (r.x * r.s + r.y * r.z)
                );
        }

        public LSL_Vector llRot2Up(LSL_Rotation r)
        {
            double m = r.x * r.x + r.y * r.y + r.z * r.z + r.s * r.s;
            if (Math.Abs(1.0 - m) > 0.000001) // allow a little slop here for calculation precision
            {
                m = 1.0 / Math.Sqrt(m);
                r.x *= m;
                r.y *= m;
                r.z *= m;
                r.s *= m;
            }

            return new LSL_Vector(
                2 * (r.x * r.z + r.y * r.s),
                2 * (-r.x * r.s + r.y * r.z),
                -r.x * r.x - r.y * r.y + r.z * r.z + r.s * r.s
                );
        }

        public LSL_Rotation llRotBetween(LSL_Vector a, LSL_Vector b)
        {
            //A and B should both be normalized

            // This method mimics the 180 errors found in SL
            // See www.euclideanspace.com... angleBetween

            // Eliminate zero length
            LSL_Float vec_a_mag = LSL_Vector.MagSquare(a);
            LSL_Float vec_b_mag = LSL_Vector.MagSquare(b);
            if (vec_a_mag < 1e-12 ||
                vec_b_mag < 1e-12)
            {
                return new LSL_Rotation(0.0f, 0.0f, 0.0f, 1.0f);
            }

            // Normalize
            a = llVecNorm(a);
            b = llVecNorm(b);

            // Calculate axis and rotation angle
            LSL_Vector axis = a % b;
            LSL_Float cos_theta  = a * b;

            // Check if parallel
            if (cos_theta > 0.99999)
            {
                return new LSL_Rotation(0.0f, 0.0f, 0.0f, 1.0f);
            }

            // Check if anti-parallel
            else if (cos_theta < -0.99999)
            {
                LSL_Vector orthog_axis = new LSL_Vector(1.0, 0.0, 0.0) - (a.x / (a * a) * a);
                if (LSL_Vector.MagSquare(orthog_axis)  < 1e-12)
                    orthog_axis = new LSL_Vector(0.0, 0.0, 1.0);
                return new LSL_Rotation((float)orthog_axis.x, (float)orthog_axis.y, (float)orthog_axis.z, 0.0);
            }
            else // other rotation
            {
                LSL_Float theta = (LSL_Float)Math.Acos(cos_theta) * 0.5f;
                axis = llVecNorm(axis);
                double x, y, z, s, t;
                s = Math.Cos(theta);
                t = Math.Sin(theta);
                x = axis.x * t;
                y = axis.y * t;
                z = axis.z * t;
                return new LSL_Rotation(x,y,z,s);
            }
        }

        public void llWhisper(int channelID, string text)
        {
            byte[] binText = Utils.StringToBytesNoTerm(text, 1023);
            World.SimChat(binText,
                          ChatTypeEnum.Whisper, channelID, m_host.AbsolutePosition, m_host.Name, m_host.UUID, false);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm?.DeliverMessage(ChatTypeEnum.Whisper, channelID, m_host.Name, m_host.UUID, Util.UTF8.GetString(binText), m_host.AbsolutePosition);
        }

        private void CheckSayShoutTime()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - m_lastSayShoutCheck).Ticks > 10000000) // 1sec
            {
                m_lastSayShoutCheck = now;
                m_SayShoutCount = 0;
            }
            else
                m_SayShoutCount++;
        }

        public void ThrottleSay(int channelID, int timeMs)
        {
            if (channelID == 0)
                CheckSayShoutTime();
            if (m_SayShoutCount >= 11)
                ScriptSleep(timeMs);
        }

        public void llSay(int channelID, string text)
        {

            if (channelID == 0)
//                m_SayShoutCount++;
                CheckSayShoutTime();

            if (m_SayShoutCount >= 11)
                ScriptSleep(2000);

            if (m_scriptConsoleChannelEnabled && (channelID == m_scriptConsoleChannel))
            {
                Console.WriteLine(text);
            }
            else
            {
                byte[] binText = Utils.StringToBytesNoTerm(text, 1023);
                World.SimChat(binText,
                              ChatTypeEnum.Say, channelID, m_host.AbsolutePosition, m_host.Name, m_host.UUID, false);

                IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
                wComm?.DeliverMessage(ChatTypeEnum.Say, channelID, m_host.Name, m_host.UUID, Util.UTF8.GetString(binText), m_host.AbsolutePosition);
            }
        }

        public void llShout(int channelID, string text)
        {

            if (channelID == 0)
//                m_SayShoutCount++;
                CheckSayShoutTime();

            if (m_SayShoutCount >= 11)
                ScriptSleep(2000);

            byte[] binText = Utils.StringToBytesNoTerm(text, 1023);

            World.SimChat(binText,
                          ChatTypeEnum.Shout, channelID, m_host.AbsolutePosition, m_host.Name, m_host.UUID, true);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm?.DeliverMessage(ChatTypeEnum.Shout, channelID, m_host.Name, m_host.UUID, Util.UTF8.GetString(binText), m_host.AbsolutePosition);
        }

        public void llRegionSay(int channelID, string text)
        {
            if (channelID == 0)
            {
                Error("llRegionSay", "Cannot use on channel 0");
                return;
            }

            byte[] binText = Utils.StringToBytesNoTerm(text, 1023);

            // debug channel is also sent to avatars
            if (channelID == ScriptBaseClass.DEBUG_CHANNEL)
            {
                World.SimChat(binText,
                    ChatTypeEnum.Shout, channelID, m_host.ParentGroup.RootPart.AbsolutePosition, m_host.Name, m_host.UUID, true);
            }

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm?.DeliverMessage(ChatTypeEnum.Region, channelID, m_host.Name, m_host.UUID, Util.UTF8.GetString(binText));
        }

        public void  llRegionSayTo(string target, int channel, string msg)
        {
            if (channel == ScriptBaseClass.DEBUG_CHANNEL)
                return;

            if(UUID.TryParse(target, out UUID TargetID) && TargetID.IsNotZero())
            {
                IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
                if (wComm != null)
                {
                    if (msg.Length > 1023)
                        msg = msg[..1023];

                    wComm.DeliverMessageTo(TargetID, channel, m_host.AbsolutePosition, m_host.Name, m_host.UUID, msg);
                }
            }
        }

        public LSL_Integer llListen(int channelID, string name, string ID, string msg)
        {
            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            if (wComm is not null)
            { 
                _ = UUID.TryParse(ID, out UUID keyID);
                return wComm.Listen(m_item.ItemID, m_host.UUID, channelID, name, keyID, msg);
            }
            return -1;
        }

        public void llListenControl(int number, int active)
        {
            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm?.ListenControl(m_item.ItemID, number, active);
        }

        public void llListenRemove(int number)
        {
            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm?.ListenRemove(m_item.ItemID, number);
        }

        public void llSensor(string name, string id, int type, double range, double arc)
        {
            _ = UUID.TryParse(id, out UUID keyID);
            m_AsyncCommands.SensorRepeatPlugin.SenseOnce(m_host.LocalId, m_item.ItemID, name, keyID, type, range, arc, m_host);
        }

        public void llSensorRepeat(string name, string id, int type, double range, double arc, double rate)
        {
            _ = UUID.TryParse(id, out UUID keyID);
            m_AsyncCommands.SensorRepeatPlugin.SetSenseRepeatEvent(m_host.LocalId, m_item.ItemID, name, keyID, type, range, arc, rate, m_host);
        }

        public void llSensorRemove()
        {
            m_AsyncCommands.SensorRepeatPlugin.UnSetSenseRepeaterEvents(m_host.LocalId, m_item.ItemID);
        }

        public string resolveName(UUID objecUUID)
        {
            // try avatar username surname
            UserAccount account = m_userAccountService.GetUserAccount(RegionScopeID, objecUUID);
            if (account is not null)
            {
                string avatarname = account.Name;
                return avatarname;
            }
            // try an scene object
            SceneObjectPart SOP = World.GetSceneObjectPart(objecUUID);
            if (SOP is not null)
            {
                string objectname = SOP.Name;
                return objectname;
            }

            World.Entities.TryGetValue(objecUUID, out EntityBase SensedObject);

            if (SensedObject is null)
            {
                IGroupsModule groups = World.RequestModuleInterface<IGroupsModule>();
                if (groups is not null)
                {
                    GroupRecord gr = groups.GetGroupRecord(objecUUID);
                    if (gr is not null)
                        return gr.GroupName;
                }
                return string.Empty;
            }

            return SensedObject.Name;
        }

        public LSL_String llDetectedName(int number)
        {
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            return detectedParams is null ? LSL_String.Empty : detectedParams.Name;
        }

        public LSL_Key llDetectedKey(int number)
        {
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            return detectedParams is null ? LSL_String.Empty : detectedParams.Key.ToString();
        }

        public LSL_Key llDetectedOwner(int number)
        {
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            return detectedParams is null ? LSL_String.Empty : detectedParams.Owner.ToString();
        }

        public LSL_Integer llDetectedType(int number)
        {
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            return detectedParams is null ? 0 : new LSL_Integer(detectedParams.Type);
        }

        public LSL_Vector llDetectedPos(int number)
        {
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            return detectedParams is null ?  new LSL_Vector() : detectedParams.Position;
        }

        public LSL_Vector llDetectedVel(int number)
        {
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            return  detectedParams == null ? new LSL_Vector() : detectedParams.Velocity;
        }

        public LSL_Vector llDetectedGrab(int number)
        {
            DetectParams parms = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            return parms is null ? new LSL_Vector() : parms.OffsetPos;
        }

        public LSL_Rotation llDetectedRot(int number)
        {
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            return detectedParams is null ? new LSL_Rotation() : detectedParams.Rotation;
        }

        public LSL_Integer llDetectedGroup(int number)
        {
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            if (detectedParams is null)
                return new LSL_Integer(0);
            if (m_host.GroupID.Equals(detectedParams.Group))
                return new LSL_Integer(1);
            return new LSL_Integer(0);
        }

        public LSL_Integer llDetectedLinkNumber(int number)
        {
            DetectParams parms = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            return parms is null ? new LSL_Integer() : new LSL_Integer(parms.LinkNum);
        }

        /// <summary>
        /// See http://wiki.secondlife.com/wiki/LlDetectedTouchBinormal for details
        /// </summary>
        public LSL_Vector llDetectedTouchBinormal(int index)
        {
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, index);
             return detectedParams is null ? new LSL_Vector() : detectedParams.TouchBinormal;
        }

        /// <summary>
        /// See http://wiki.secondlife.com/wiki/LlDetectedTouchFace for details
        /// </summary>
        public LSL_Integer llDetectedTouchFace(int index)
        {
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, index);
            return detectedParams is null ?  new LSL_Integer(-1) : new LSL_Integer(detectedParams.TouchFace);
        }

        /// <summary>
        /// See http://wiki.secondlife.com/wiki/LlDetectedTouchNormal for details
        /// </summary>
        public LSL_Vector llDetectedTouchNormal(int index)
        {
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, index);
            return detectedParams is null ? new LSL_Vector() : detectedParams.TouchNormal;
        }

        /// <summary>
        /// See http://wiki.secondlife.com/wiki/LlDetectedTouchPos for details
        /// </summary>
        public LSL_Vector llDetectedTouchPos(int index)
        {
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, index);
            return detectedParams is null ? new LSL_Vector() : detectedParams.TouchPos;
        }

        /// <summary>
        /// See http://wiki.secondlife.com/wiki/LlDetectedTouchST for details
        /// </summary>
        public LSL_Vector llDetectedTouchST(int index)
        {
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, index);
            return detectedParams is null ? new LSL_Vector(-1.0, -1.0, 0.0) : detectedParams.TouchST;
        }

        /// <summary>
        /// See http://wiki.secondlife.com/wiki/LlDetectedTouchUV for details
        /// </summary>
        public LSL_Vector llDetectedTouchUV(int index)
        {
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, index);
            return detectedParams is null ? new LSL_Vector(-1.0, -1.0, 0.0) : detectedParams.TouchUV;
        }

        [DebuggerNonUserCode]
        public virtual void llDie()
        {
            if (!m_host.ParentGroup.IsAttachment) throw new SelfDeleteException();
        }

        public LSL_Float llGround(LSL_Vector offset)
        {
            Vector3 pos = m_host.GetWorldPosition();
            pos.X += (float)offset.x;
            pos.Y += (float)offset.y;

            return World.GetGroundHeight(pos.X, pos.Y);
        }

        public LSL_Float llCloud(LSL_Vector offset)
        {
            return 0;
        }

        public LSL_Vector llWind(LSL_Vector offset)
        {
            LSL_Vector wind = new();
            IWindModule module = World.RequestModuleInterface<IWindModule>();
            if (module is not null)
            {
                Vector3 pos = m_host.GetWorldPosition();
                int x = (int)(pos.X + offset.x);
                int y = (int)(pos.Y + offset.y);

                Vector3 windSpeed = module.WindSpeed(x, y, 0);

                wind.x = windSpeed.X;
                wind.y = windSpeed.Y;
            }
            return wind;
        }

        public void llSetStatus(int status, int value)
        {
            if (m_host is null || m_host.ParentGroup is null || m_host.ParentGroup.IsDeleted)
                return;

            if ((status & ScriptBaseClass.STATUS_CAST_SHADOWS) != 0)
                m_host.AddFlag(PrimFlags.CastShadows);

            if ((status & ScriptBaseClass.STATUS_BLOCK_GRAB) != 0)
                m_host.BlockGrab = value != 0;

            if ((status & ScriptBaseClass.STATUS_BLOCK_GRAB_OBJECT) != 0)
                m_host.ParentGroup.BlockGrabOverride = value != 0;

            if ((status & ScriptBaseClass.STATUS_DIE_AT_EDGE) != 0)
                    m_host.SetDieAtEdge(value != 0);

            if ((status & ScriptBaseClass.STATUS_RETURN_AT_EDGE) != 0)
                m_host.SetReturnAtEdge(value != 0);

            if ((status & ScriptBaseClass.STATUS_SANDBOX) != 0)
                m_host.SetStatusSandbox(value != 0);

            int statusrotationaxis = status & (ScriptBaseClass.STATUS_ROTATE_X | ScriptBaseClass.STATUS_ROTATE_Y | ScriptBaseClass.STATUS_ROTATE_Z);
            if (statusrotationaxis != 0)
                m_host.ParentGroup.SetAxisRotation(statusrotationaxis, value);

            if ((status & ScriptBaseClass.STATUS_PHANTOM) != 0)
                m_host.ParentGroup.ScriptSetPhantomStatus(value != 0);

            if ((status & ScriptBaseClass.STATUS_PHYSICS) != 0)
            {
                if (value != 0)
                {
                    SceneObjectGroup group = m_host.ParentGroup;
                    if (group.RootPart.PhysActor is null || !group.RootPart.PhysActor.IsPhysical)
                    {
                        bool allow = true;
                        int maxprims = World.m_linksetPhysCapacity;
                        bool checkShape = (maxprims > 0 && group.PrimCount > maxprims);

                        foreach (SceneObjectPart part in group.Parts)
                        {
                            if (part.PhysicsShapeType == (byte)PhysicsShapeType.None)
                                continue;

                            if (checkShape)
                            {
                                if (--maxprims < 0)
                                {
                                    allow = false;
                                    break;
                                }
                            }

                            if (part.Scale.X > World.m_maxPhys || part.Scale.Y > World.m_maxPhys || part.Scale.Z > World.m_maxPhys)
                            {
                                allow = false;
                                break;
                            }
                        }

                        if (allow)
                            m_host.ScriptSetPhysicsStatus(true);
                    }
                }
                else
                {
                    m_host.ScriptSetPhysicsStatus(false);
                }
            }
        }

        private bool IsPhysical()
        {
            return ((m_host.GetEffectiveObjectFlags() & (uint)PrimFlags.Physics) != 0);
        }

        public LSL_Integer llGetStatus(int status)
        {
            // m_log.Debug(m_host.ToString() + " status is " + m_host.GetEffectiveObjectFlags().ToString());
            return status switch
            {
                ScriptBaseClass.STATUS_PHYSICS => (LSL_Integer)(IsPhysical() ? 1 : 0),
                ScriptBaseClass.STATUS_PHANTOM => (LSL_Integer)((m_host.GetEffectiveObjectFlags() & (uint)PrimFlags.Phantom) != 0 ? 1 : 0),
                ScriptBaseClass.STATUS_CAST_SHADOWS => (LSL_Integer)((m_host.GetEffectiveObjectFlags() & (uint)PrimFlags.CastShadows) != 0 ? 1 : 0),
                ScriptBaseClass.STATUS_BLOCK_GRAB => (LSL_Integer)(m_host.BlockGrab ? 1 : 0),
                ScriptBaseClass.STATUS_BLOCK_GRAB_OBJECT => (LSL_Integer)(m_host.ParentGroup.BlockGrabOverride ? 1 : 0),
                ScriptBaseClass.STATUS_DIE_AT_EDGE => (LSL_Integer)(m_host.GetDieAtEdge() ? 1 : 0),
                ScriptBaseClass.STATUS_RETURN_AT_EDGE => (LSL_Integer)(m_host.GetReturnAtEdge() ? 1 : 0),
                ScriptBaseClass.STATUS_ROTATE_X => (LSL_Integer)(m_host.GetAxisRotation((int)SceneObjectGroup.axisSelect.STATUS_ROTATE_X) != 0 ? 1 : 0),
                ScriptBaseClass.STATUS_ROTATE_Y => (LSL_Integer)(m_host.GetAxisRotation((int)SceneObjectGroup.axisSelect.STATUS_ROTATE_Y) != 0 ? 1 : 0),
                ScriptBaseClass.STATUS_ROTATE_Z => (LSL_Integer)(m_host.GetAxisRotation((int)SceneObjectGroup.axisSelect.STATUS_ROTATE_Z) != 0 ? 1 : 0),
                ScriptBaseClass.STATUS_SANDBOX => (LSL_Integer)(m_host.GetStatusSandbox() ? 1 : 0),
                _ => (LSL_Integer)0,
            };
        }

        public LSL_Integer llScaleByFactor(double scaling_factor)
        {
            SceneObjectGroup group = m_host.ParentGroup;

            if(scaling_factor < 1e-6)
                return ScriptBaseClass.FALSE;
            if(scaling_factor > 1e6)
                return ScriptBaseClass.FALSE;

            if (group is null || group.IsDeleted || group.inTransit)
                return ScriptBaseClass.FALSE;

            if (group.RootPart.PhysActor is not null && group.RootPart.PhysActor.IsPhysical)
                return ScriptBaseClass.FALSE;

            if (group.RootPart.KeyframeMotion is not null)
                return ScriptBaseClass.FALSE;

            return group.GroupResize(scaling_factor) ? 1 : 0;
        }

        public LSL_Float llGetMaxScaleFactor()
        {
            SceneObjectGroup group = m_host.ParentGroup;

            if (group is null || group.IsDeleted || group.inTransit)
                return 1.0f;

            return (LSL_Float)group.GetMaxGroupResizeScale();
        }

        public LSL_Float llGetMinScaleFactor()
        {
            SceneObjectGroup group = m_host.ParentGroup;

            if (group is null || group.IsDeleted || group.inTransit)
                return 1.0f;

            return (LSL_Float)group.GetMinGroupResizeScale();
        }

        public void llSetScale(LSL_Vector scale)
        {
            SetScale(m_host, scale);
        }

        protected void SetScale(SceneObjectPart part, LSL_Vector scale)
        {
            // TODO: this needs to trigger a persistance save as well
            if (part is null || part.ParentGroup.IsDeleted)
                return;

            // First we need to check whether or not we need to clamp the size of a physics-enabled prim
            PhysicsActor pa = part.ParentGroup.RootPart.PhysActor;
            if (pa != null && pa.IsPhysical)
            {
                scale.x = Math.Max(World.m_minPhys, Math.Min(World.m_maxPhys, scale.x));
                scale.y = Math.Max(World.m_minPhys, Math.Min(World.m_maxPhys, scale.y));
                scale.z = Math.Max(World.m_minPhys, Math.Min(World.m_maxPhys, scale.z));
            }
            else
            {
                // If not physical, then we clamp the scale to the non-physical min/max
                scale.x = Math.Max(World.m_minNonphys, Math.Min(World.m_maxNonphys, scale.x));
                scale.y = Math.Max(World.m_minNonphys, Math.Min(World.m_maxNonphys, scale.y));
                scale.z = Math.Max(World.m_minNonphys, Math.Min(World.m_maxNonphys, scale.z));
            }

            Vector3 tmp = part.Scale;
            tmp.X = (float)scale.x;
            tmp.Y = (float)scale.y;
            tmp.Z = (float)scale.z;
            part.Scale = tmp;
            part.ParentGroup.HasGroupChanged = true;
            part.SendFullUpdateToAllClients();
        }

        public LSL_Vector llGetScale()
        {
            return new LSL_Vector(m_host.Scale.X, m_host.Scale.Y, m_host.Scale.Z);
        }

        public void llSetClickAction(int action)
        {
            m_host.ClickAction = (byte)action;
            m_host.ParentGroup.HasGroupChanged = true;
            m_host.ScheduleFullUpdate();
            return;
        }

        public void llSetColor(LSL_Vector color, int face)
        {

            SetColor(m_host, color, face);
        }

        protected void SetColor(SceneObjectPart part, LSL_Vector color, int face)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            m_host.SetFaceColorAlpha(face, color, null);
        }

        public void llSetContentType(LSL_Key reqid, LSL_Integer type)
        {
            if (m_UrlModule == null)
                return;

            if(!UUID.TryParse(reqid, out UUID id) || id.IsZero())
                return;

            switch (type)
            {
                case ScriptBaseClass.CONTENT_TYPE_HTML:
                {
                    // Make sure the content type is text/plain to start with
                    m_UrlModule.HttpContentType(id, "text/plain");

                    // Is the object owner online and in the region
                    ScenePresence agent = World.GetScenePresence(m_host.ParentGroup.OwnerID);
                    if (agent == null || agent.IsChildAgent || agent.IsDeleted)
                        return;  // Fail if the owner is not in the same region

                    // Is it the embeded browser?
                    string userAgent = m_UrlModule.GetHttpHeader(id, "user-agent");
                    if(string.IsNullOrEmpty(userAgent) || userAgent.IndexOf("SecondLife") < 0)
                        return; // Not the embedded browser

                    // Use the IP address of the client and check against the request
                    // seperate logins from the same IP will allow all of them to get non-text/plain as long
                    // as the owner is in the region. Same as SL!
                    string logonFromIPAddress = agent.ControllingClient.RemoteEndPoint.Address.ToString();
                    if (string.IsNullOrEmpty(logonFromIPAddress))
                        return;

                    string requestFromIPAddress = m_UrlModule.GetHttpHeader(id, "x-remote-ip");
                    //m_log.Debug("IP from header='" + requestFromIPAddress + "' IP from endpoint='" + logonFromIPAddress + "'");
                    if (requestFromIPAddress == null)
                        return;

                    requestFromIPAddress = requestFromIPAddress.Trim();

                    // If the request isnt from the same IP address then the request cannot be from the owner
                    if (!requestFromIPAddress.Equals(logonFromIPAddress))
                        return;

                    m_UrlModule.HttpContentType(id, "text/html");
                    break;
                }
                case ScriptBaseClass.CONTENT_TYPE_XML:
                    m_UrlModule.HttpContentType(id, "application/xml");
                    break;
                case ScriptBaseClass.CONTENT_TYPE_XHTML:
                    m_UrlModule.HttpContentType(id, "application/xhtml+xml");
                    break;
                case ScriptBaseClass.CONTENT_TYPE_ATOM:
                    m_UrlModule.HttpContentType(id, "application/atom+xml");
                    break;
                case ScriptBaseClass.CONTENT_TYPE_JSON:
                    m_UrlModule.HttpContentType(id, "application/json");
                    break;
                case ScriptBaseClass.CONTENT_TYPE_LLSD:
                    m_UrlModule.HttpContentType(id, "application/llsd+xml");
                    break;
                case ScriptBaseClass.CONTENT_TYPE_FORM:
                    m_UrlModule.HttpContentType(id, "application/x-www-form-urlencoded");
                    break;
                case ScriptBaseClass.CONTENT_TYPE_RSS:
                    m_UrlModule.HttpContentType(id, "application/rss+xml");
                    break;
                default:
                    m_UrlModule.HttpContentType(id, "text/plain");
                    break;
            }
        }

        public static void SetTexGen(SceneObjectPart part, int face,int style)
        {
            if (part is null || part.ParentGroup is null || part.ParentGroup.IsDeleted)
                return;

            Primitive.TextureEntry tex = part.Shape.Textures;
            MappingType textype;
            textype = MappingType.Default;
            if (style == ScriptBaseClass.PRIM_TEXGEN_PLANAR)
                textype = MappingType.Planar;

            int nsides = GetNumberOfSides(part);

            if (face >= 0 && face < nsides)
            {
                tex.CreateFace((uint) face);
                tex.FaceTextures[face].TexMapType = textype;
                part.UpdateTextureEntry(tex);
                return;
            }
            else if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (uint i = 0; i < nsides; i++)
                {
                    if (tex.FaceTextures[i] is not null)
                        tex.FaceTextures[i].TexMapType = textype;
                }
                tex.DefaultTexture.TexMapType = textype;
                part.UpdateTextureEntry(tex);
                return;
            }
        }

        public static void SetGlow(SceneObjectPart part, int face, float glow)
        {
            if (part is null || part.ParentGroup is null || part.ParentGroup.IsDeleted)
                return;

            Primitive.TextureEntry tex = part.Shape.Textures;
            int nsides = GetNumberOfSides(part);

            if (face >= 0 && face < nsides)
            {
                tex.CreateFace((uint) face);
                tex.FaceTextures[face].Glow = glow;
                part.UpdateTextureEntry(tex);
                return;
            }
            else if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (uint i = 0; i < nsides; i++)
                {
                    if (tex.FaceTextures[i] is not null)
                        tex.FaceTextures[i].Glow = glow;
                }
                tex.DefaultTexture.Glow = glow;
                part.UpdateTextureEntry(tex);
                return;
            }
        }

        public static void SetShiny(SceneObjectPart part, int face, int shiny, Bumpiness bump)
        {
            if (part is null || part.ParentGroup is null || part.ParentGroup.IsDeleted)
                return;
            var sval = shiny switch
            {
                0 => Shininess.None,
                1 => Shininess.Low,
                2 => Shininess.Medium,
                3 => Shininess.High,
                _ => Shininess.None,
            };
            int nsides = GetNumberOfSides(part);

            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face >= 0 && face < nsides)
            {
                tex.CreateFace((uint) face);
                tex.FaceTextures[face].Shiny = sval;
                tex.FaceTextures[face].Bump = bump;
                part.UpdateTextureEntry(tex);
                return;
            }
            else if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (uint i = 0; i < nsides; i++)
                {
                    if (tex.FaceTextures[i] is not null)
                    {
                        tex.FaceTextures[i].Shiny = sval;
                        tex.FaceTextures[i].Bump = bump;
                    }
                }
                tex.DefaultTexture.Shiny = sval;
                tex.DefaultTexture.Bump = bump;
                part.UpdateTextureEntry(tex);
                return;
            }
        }

        public static void SetFullBright(SceneObjectPart part, int face, bool bright)
        {
            if (part is null || part.ParentGroup is null || part.ParentGroup.IsDeleted)
                return;

             int nsides = GetNumberOfSides(part);
             Primitive.TextureEntry tex = part.Shape.Textures;
             if (face >= 0 && face < nsides)
             {
                 tex.CreateFace((uint) face);
                 tex.FaceTextures[face].Fullbright = bright;
                 part.UpdateTextureEntry(tex);
                 return;
             }
             else if (face == ScriptBaseClass.ALL_SIDES)
             {
                tex.DefaultTexture.Fullbright = bright;
                for (uint i = 0; i < nsides; i++)
                 {
                    if(tex.FaceTextures[i] is not null)
                        tex.FaceTextures[i].Fullbright = bright;
                 }
                 part.UpdateTextureEntry(tex);
                 return;
             }
         }

        public LSL_Float llGetAlpha(int face)
        {

            return GetAlpha(m_host, face);
        }

        protected static LSL_Float GetAlpha(SceneObjectPart part, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            int nsides = GetNumberOfSides(part);
            if (face == ScriptBaseClass.ALL_SIDES)
            {
                int i;
                double sum = 0.0;
                for (i = 0 ; i < nsides; i++)
                    sum += tex.GetFace((uint)i).RGBA.A;
                return sum;
            }
            if (face >= 0 && face < nsides)
            {
                return tex.GetFace((uint)face).RGBA.A;
            }
            return 0.0;
        }

        public void llSetAlpha(double alpha, int face)
        {

            SetAlpha(m_host, alpha, face);
        }

        public void llSetLinkAlpha(int linknumber, double alpha, int face)
        {

            List<SceneObjectPart> parts = GetLinkParts(linknumber);
            if (parts.Count > 0)
            {
                try
                {
                    foreach (SceneObjectPart part in parts)
                        SetAlpha(part, alpha, face);
                }
                finally { }
            }
        }

        protected static void SetAlpha(SceneObjectPart part, double alpha, int face)
        {
            if (part is null || part.ParentGroup is null || part.ParentGroup.IsDeleted)
                return;

            Primitive.TextureEntry tex = part.Shape.Textures;
            int nsides = GetNumberOfSides(part);
            Color4 texcolor;

            if (face >= 0 && face < nsides)
            {
                texcolor = tex.CreateFace((uint)face).RGBA;
                texcolor.A = Utils.Clamp((float)alpha, 0.0f, 1.0f);
                tex.FaceTextures[face].RGBA = texcolor;
                part.UpdateTextureEntry(tex);
                return;
            }
            else if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (int i = 0; i < nsides; i++)
                {
                    if (tex.FaceTextures[i] is not null)
                    {
                        texcolor = tex.FaceTextures[i].RGBA;
                        texcolor.A = Utils.Clamp((float)alpha, 0.0f, 1.0f);
                        tex.FaceTextures[i].RGBA = texcolor;
                    }
                }

                // In some cases, the default texture can be null, eg when every face
                // has a unique texture
                if (tex.DefaultTexture is not null)
                {
                    texcolor = tex.DefaultTexture.RGBA;
                    texcolor.A = Utils.Clamp((float)alpha, 0.0f, 1.0f);
                    tex.DefaultTexture.RGBA = texcolor;
                }

                part.UpdateTextureEntry(tex);
                return;
            }
        }

        /// <summary>
        /// Set flexi parameters of a part.
        ///
        /// FIXME: Much of this code should probably be within the part itself.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="flexi"></param>
        /// <param name="softness"></param>
        /// <param name="gravity"></param>
        /// <param name="friction"></param>
        /// <param name="wind"></param>
        /// <param name="tension"></param>
        /// <param name="Force"></param>
        protected static void SetFlexi(SceneObjectPart part, bool flexi, int softness, float gravity, float friction,
            float wind, float tension, LSL_Vector Force)
        {
            if (part == null)
                return;
            SceneObjectGroup sog = part.ParentGroup;

            if(sog == null || sog.IsDeleted || sog.inTransit)
                return;

            PrimitiveBaseShape pbs = part.Shape;
            pbs.FlexiSoftness = softness;
            pbs.FlexiGravity = gravity;
            pbs.FlexiDrag = friction;
            pbs.FlexiWind = wind;
            pbs.FlexiTension = tension;
            pbs.FlexiForceX = (float)Force.x;
            pbs.FlexiForceY = (float)Force.y;
            pbs.FlexiForceZ = (float)Force.z;

            pbs.FlexiEntry = flexi;

            if (!pbs.SculptEntry && (pbs.PathCurve == (byte)Extrusion.Straight || pbs.PathCurve == (byte)Extrusion.Flexible))
            {
                if(flexi)
                {
                    pbs.PathCurve = (byte)Extrusion.Flexible;
                    if(!sog.IsPhantom)
                    {
                        sog.ScriptSetPhantomStatus(true);
                        return;
                    }
                }
                else
                {
                    // Other values not set, they do not seem to be sent to the viewer
                    // Setting PathCurve appears to be what actually toggles the check box and turns Flexi on and off
                    pbs.PathCurve = (byte)Extrusion.Straight;
                }
            }
            part.ParentGroup.HasGroupChanged = true;
            part.ScheduleFullUpdate();
        }

        /// <summary>
        /// Set a light point on a part
        /// </summary>
        /// FIXME: Much of this code should probably be in SceneObjectGroup
        ///
        /// <param name="part"></param>
        /// <param name="light"></param>
        /// <param name="color"></param>
        /// <param name="intensity"></param>
        /// <param name="radius"></param>
        /// <param name="falloff"></param>
        protected static void SetPointLight(SceneObjectPart part, bool light, LSL_Vector color, float intensity, float radius, float falloff)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            PrimitiveBaseShape pbs = part.Shape;

            if (light)
            {
                pbs.LightEntry = true;
                pbs.LightColorR = Utils.Clamp((float)color.x, 0.0f, 1.0f);
                pbs.LightColorG = Utils.Clamp((float)color.y, 0.0f, 1.0f);
                pbs.LightColorB = Utils.Clamp((float)color.z, 0.0f, 1.0f);
                pbs.LightIntensity = Utils.Clamp(intensity, 0.0f, 1.0f);
                pbs.LightRadius = Utils.Clamp(radius, 0.1f, 20.0f);
                pbs.LightFalloff = Utils.Clamp(falloff, 0.01f, 2.0f);
            }
            else
            {
                pbs.LightEntry = false;
            }

            part.ParentGroup.HasGroupChanged = true;
            part.ScheduleFullUpdate();
        }

        public LSL_Vector llGetColor(int face)
        {
            return GetColor(m_host, face);
        }

        public LSL_Vector GetColor(SceneObjectPart part, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            Color4 texcolor;
            LSL_Vector rgb = new();
            int nsides = GetNumberOfSides(part);

            if (face == ScriptBaseClass.ALL_SIDES)
            {
                int i;
                for (i = 0; i < nsides; i++)
                {
                    texcolor = tex.GetFace((uint)i).RGBA;
                    rgb.x += texcolor.R;
                    rgb.y += texcolor.G;
                    rgb.z += texcolor.B;
                }

                float invnsides = 1.0f / (float)nsides;

                rgb.x *= invnsides;
                rgb.y *= invnsides;
                rgb.z *= invnsides;

                return rgb;
            }
            if (face >= 0 && face < nsides)
            {
                texcolor = tex.GetFace((uint)face).RGBA;
                rgb.x = texcolor.R;
                rgb.y = texcolor.G;
                rgb.z = texcolor.B;

                return rgb;
            }

            return LSL_Vector.Zero;
        }

        public void llSetTexture(string texture, int face)
        {
            SetTexture(m_host, texture, face);
            ScriptSleep(m_sleepMsOnSetTexture);
        }

        public void llSetLinkTexture(int linknumber, string texture, int face)
        {

            List<SceneObjectPart> parts = GetLinkParts(linknumber);
            if (parts.Count > 0)
            {
                try
                {
                    foreach (SceneObjectPart part in parts)
                        SetTexture(part, texture, face);
                }
                finally { }
             }
            ScriptSleep(m_sleepMsOnSetLinkTexture);
        }

        protected void SetTextureParams(SceneObjectPart part, string texture, double scaleU, double ScaleV,
                    double offsetU, double offsetV, double rotation, int face)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            UUID textureID = UUID.Zero;
            bool dotexture = false;
            if(!string.IsNullOrEmpty(texture) && texture != ScriptBaseClass.NULL_KEY)
            {
                textureID = ScriptUtils.GetAssetIdFromItemName(m_host, texture, (int)AssetType.Texture);
                dotexture = textureID.IsNotZero() || 
                    (UUID.TryParse(texture, out textureID) && textureID.IsNotZero());
            }

            Primitive.TextureEntry tex = part.Shape.Textures;
            int nsides = GetNumberOfSides(part);

            if (face >= 0 && face < nsides)
            {
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)face);
                if (dotexture)
                    texface.TextureID = textureID;
                texface.RepeatU = (float)scaleU;
                texface.RepeatV = (float)ScaleV;
                texface.OffsetU = (float)offsetU;
                texface.OffsetV = (float)offsetV;
                texface.Rotation = (float)rotation;
                tex.FaceTextures[face] = texface;
                part.UpdateTextureEntry(tex);
                return;
            }
            else if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (uint i = 0; i < nsides; i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        if (dotexture)
                            tex.FaceTextures[i].TextureID = textureID;
                        tex.FaceTextures[i].RepeatU = (float)scaleU;
                        tex.FaceTextures[i].RepeatV = (float)ScaleV;
                        tex.FaceTextures[i].OffsetU = (float)offsetU;
                        tex.FaceTextures[i].OffsetV = (float)offsetV;
                        tex.FaceTextures[i].Rotation = (float)rotation;
                    }
                }
                if (dotexture)
                    tex.DefaultTexture.TextureID = textureID;
                tex.DefaultTexture.RepeatU = (float)scaleU;
                tex.DefaultTexture.RepeatV = (float)ScaleV;
                tex.DefaultTexture.OffsetU = (float)offsetU;
                tex.DefaultTexture.OffsetV = (float)offsetV;
                tex.DefaultTexture.Rotation = (float)rotation;
                part.UpdateTextureEntry(tex);
                return;
            }
        }

        protected void SetTexture(SceneObjectPart part, string texture, int face)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            UUID textureID = ScriptUtils.GetAssetIdFromItemName(m_host, texture, (int)AssetType.Texture);
            if (textureID.IsZero())
            {
                if (!UUID.TryParse(texture, out textureID) || textureID.IsZero())
                    return;
            }

            Primitive.TextureEntry tex = part.Shape.Textures;
            int nsides = GetNumberOfSides(part);

            if (face >= 0 && face < nsides)
            {
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)face);
                texface.TextureID = textureID;
                tex.FaceTextures[face] = texface;
                part.UpdateTextureEntry(tex);
                return;
            }
            else if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (uint i = 0; i < nsides; i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        tex.FaceTextures[i].TextureID = textureID;
                    }
                }
                tex.DefaultTexture.TextureID = textureID;
                part.UpdateTextureEntry(tex);
                return;
            }
        }

        public void llScaleTexture(double u, double v, int face)
        {
            ScaleTexture(m_host, u, v, face);
            ScriptSleep(m_sleepMsOnScaleTexture);
        }

        protected static void ScaleTexture(SceneObjectPart part, double u, double v, int face)
        {
            if (part is null || part.ParentGroup is null || part.ParentGroup.IsDeleted)
                return;

            Primitive.TextureEntry tex = part.Shape.Textures;
            int nsides = GetNumberOfSides(part);

            if (face >= 0 && face < nsides)
            {
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)face);
                texface.RepeatU = (float)u;
                texface.RepeatV = (float)v;
                tex.FaceTextures[face] = texface;
                part.UpdateTextureEntry(tex);
                return;
            }
            if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (int i = 0; i < nsides; i++)
                {
                    if (tex.FaceTextures[i] is not null)
                    {
                        tex.FaceTextures[i].RepeatU = (float)u;
                        tex.FaceTextures[i].RepeatV = (float)v;
                    }
                }
                tex.DefaultTexture.RepeatU = (float)u;
                tex.DefaultTexture.RepeatV = (float)v;
                part.UpdateTextureEntry(tex);
                return;
            }
        }

        public void llOffsetTexture(double u, double v, int face)
        {
            OffsetTexture(m_host, u, v, face);
            ScriptSleep(m_sleepMsOnOffsetTexture);
        }

        protected static void OffsetTexture(SceneObjectPart part, double u, double v, int face)
        {
            if (part is null || part.ParentGroup is null || part.ParentGroup.IsDeleted)
                return;

            Primitive.TextureEntry tex = part.Shape.Textures;
            int nsides = GetNumberOfSides(part);

            if (face >= 0 && face < nsides)
            {
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)face);
                texface.OffsetU = (float)u;
                texface.OffsetV = (float)v;
                tex.FaceTextures[face] = texface;
                part.UpdateTextureEntry(tex);
                return;
            }
            if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (int i = 0; i < nsides; i++)
                {
                    if (tex.FaceTextures[i] is not null)
                    {
                        tex.FaceTextures[i].OffsetU = (float)u;
                        tex.FaceTextures[i].OffsetV = (float)v;
                    }
                }
                tex.DefaultTexture.OffsetU = (float)u;
                tex.DefaultTexture.OffsetV = (float)v;
                part.UpdateTextureEntry(tex);
                return;
            }
        }

        public void llRotateTexture(double rotation, int face)
        {
            RotateTexture(m_host, rotation, face);
            ScriptSleep(m_sleepMsOnRotateTexture);
        }

        protected static void RotateTexture(SceneObjectPart part, double rotation, int face)
        {
            if (part is null || part.ParentGroup is null || part.ParentGroup.IsDeleted)
                return;

            Primitive.TextureEntry tex = part.Shape.Textures;
            int nsides = GetNumberOfSides(part);

            if (face >= 0 && face < nsides)
            {
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)face);
                texface.Rotation = (float)rotation;
                tex.FaceTextures[face] = texface;
                part.UpdateTextureEntry(tex);
                return;
            }
            if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (int i = 0; i < nsides; i++)
                {
                    if (tex.FaceTextures[i] is not null)
                    {
                        tex.FaceTextures[i].Rotation = (float)rotation;
                    }
                }
                tex.DefaultTexture.Rotation = (float)rotation;
                part.UpdateTextureEntry(tex);
                return;
            }
        }

        public LSL_String llGetTexture(int face)
        {
            return GetTexture(m_host, face);
        }

        protected static LSL_String GetTexture(SceneObjectPart part, int face)
        {
            if (face == ScriptBaseClass.ALL_SIDES)
                face = 0;
            if (face < 0)
                return ScriptBaseClass.NULL_KEY;

            Primitive.TextureEntry tex = part.Shape.Textures;
            int nsides = GetNumberOfSides(part);
            if (face >= nsides)
                return ScriptBaseClass.NULL_KEY;

            Primitive.TextureEntryFace texface;
            texface = tex.GetFace((uint)face);

            lock (part.TaskInventory)
            {
                foreach (KeyValuePair<UUID, TaskInventoryItem> inv in part.TaskInventory)
                {
                    if (inv.Value.AssetID.Equals(texface.TextureID))
                        return inv.Value.Name.ToString();
                }
            }

            return texface.TextureID.ToString();
        }

        public void llSetPos(LSL_Vector pos)
        {

            SetPos(m_host, pos, true);

            ScriptSleep(m_sleepMsOnSetPos);
        }

        /// <summary>
        /// Tries to move the entire object so that the root prim is within 0.1m of position. http://wiki.secondlife.com/wiki/LlSetRegionPos
        /// Documentation indicates that the use of x/y coordinates up to 10 meters outside the bounds of a region will work but do not specify what happens if there is no adjacent region for the object to move into.
        /// Uses the RegionSize constant here rather than hard-coding 266.0 to alert any developer modifying OpenSim to support variable-sized regions that this method will need tweaking.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns>1 if successful, 0 otherwise.</returns>
        public LSL_Integer llSetRegionPos(LSL_Vector pos)
        {

            // BEGIN WORKAROUND
            // IF YOU GET REGION CROSSINGS WORKING WITH THIS FUNCTION, REPLACE THE WORKAROUND.
            //
            // This workaround is to prevent silent failure of this function.
            // According to the specification on the SL Wiki, providing a position outside of the
            if (pos.x < 0 || pos.x > World.RegionInfo.RegionSizeX || pos.y < 0 || pos.y > World.RegionInfo.RegionSizeY)
            {
                return 0;
            }
            // END WORK AROUND
            else if ( // this is not part of the workaround if-block because it's not related to the workaround.
                IsPhysical() ||
                m_host.ParentGroup.IsAttachment || // return FALSE if attachment
                (
                    pos.x < -10.0 || // return FALSE if more than 10 meters into a west-adjacent region.
                    pos.x > (World.RegionInfo.RegionSizeX + 10) || // return FALSE if more than 10 meters into a east-adjacent region.
                    pos.y < -10.0 || // return FALSE if more than 10 meters into a south-adjacent region.
                    pos.y > (World.RegionInfo.RegionSizeY + 10) || // return FALSE if more than 10 meters into a north-adjacent region.
                    pos.z > Constants.RegionHeight // return FALSE if altitude than 4096m
                )
            )
            {
                return 0;
            }

            // if we reach this point, then the object is not physical, it's not an attachment, and the destination is within the valid range.
            // this could possibly be done in the above else-if block, but we're doing the check here to keep the code easier to read.

            Vector3 objectPos = m_host.ParentGroup.RootPart.AbsolutePosition;
            LandData here = World.GetLandData(objectPos);
            LandData there = World.GetLandData(pos);

            // we're only checking prim limits if it's moving to a different parcel under the assumption that if the object got onto the parcel without exceeding the prim limits.

            bool sameParcel = here.GlobalID.Equals(there.GlobalID);

            if (!sameParcel && !World.Permissions.CanRezObject(
                m_host.ParentGroup.PrimCount, m_host.ParentGroup.OwnerID, pos))
            {
                return 0;
            }

            SetPos(m_host.ParentGroup.RootPart, pos, false);

            return VecDistSquare(pos, llGetRootPosition()) <= 0.01 ? 1 : 0;
        }

        // Capped movemment if distance > 10m (http://wiki.secondlife.com/wiki/LlSetPos)
        // note linked setpos is capped "differently"
        private LSL_Vector SetPosAdjust(LSL_Vector start, LSL_Vector end)
        {
            if (VecDistSquare(start, end) > m_Script10mDistanceSquare)
                return start + m_Script10mDistance * llVecNorm(end - start);
            else
                return end;
        }

        protected LSL_Vector GetSetPosTarget(SceneObjectPart part, LSL_Vector targetPos, LSL_Vector fromPos, bool adjust)
        {
            if (part == null)
                return targetPos;
            SceneObjectGroup grp = part.ParentGroup;
            if (grp == null || grp.IsDeleted || grp.inTransit)
                return targetPos;

            if (adjust)
                targetPos = SetPosAdjust(fromPos, targetPos);

            if (m_disable_underground_movement && grp.AttachmentPoint == 0)
            {
                if (part.IsRoot)
                {
                    float ground = World.GetGroundHeight((float)targetPos.x, (float)targetPos.y);
                    if ((targetPos.z < ground))
                        targetPos.z = ground;
                }
            }
            return targetPos;
        }

        /// <summary>
        /// set object position, optionally capping the distance.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="targetPos"></param>
        /// <param name="adjust">if TRUE, will cap the distance to 10m.</param>
        protected void SetPos(SceneObjectPart part, LSL_Vector targetPos, bool adjust)
        {
            if (part == null)
                return;

            SceneObjectGroup grp = part.ParentGroup;
            if (grp == null || grp.IsDeleted || grp.inTransit)
                return;

            LSL_Vector currentPos = GetPartLocalPos(part);
            LSL_Vector toPos = GetSetPosTarget(part, targetPos, currentPos, adjust);

            if (part.IsRoot)
            {
                if (!grp.IsAttachment && !World.Permissions.CanObjectEntry(grp, false, (Vector3)toPos))
                    return;
                grp.UpdateGroupPosition((Vector3)toPos);
            }
            else
            {
                part.OffsetPosition = (Vector3)toPos;
//                SceneObjectGroup parent = part.ParentGroup;
//                parent.HasGroupChanged = true;
//                parent.ScheduleGroupForTerseUpdate();
                part.ScheduleTerseUpdate();
            }
        }

        public LSL_Vector llGetPos()
        {
            return m_host.GetWorldPosition();
        }

        public LSL_Vector llGetLocalPos()
        {
            return GetPartLocalPos(m_host);
        }

        protected static LSL_Vector GetPartLocalPos(SceneObjectPart part)
        {
            if (part.IsRoot)
            {
                if (part.ParentGroup.IsAttachment)
                    return new LSL_Vector(part.AttachedPos);

                return new LSL_Vector(part.AbsolutePosition);
            }
            return new LSL_Vector(part.OffsetPosition);
        }

        public void llSetRot(LSL_Rotation rot)
        {
            // try to let this work as in SL...
            if (m_host.ParentID == 0 || (m_host.ParentGroup != null && m_host == m_host.ParentGroup.RootPart))
            {
                // special case: If we are root, rotate complete SOG to new rotation
                SetRot(m_host, rot);
            }
            else
            {
                // we are a child. The rotation values will be set to the one of root modified by rot, as in SL. Don't ask.
                SceneObjectPart rootPart = m_host.ParentGroup.RootPart;
                if (rootPart != null) // better safe than sorry
                {
                    SetRot(m_host, rootPart.RotationOffset * (Quaternion)rot);
                }
            }

            ScriptSleep(m_sleepMsOnSetRot);
        }

        public void llSetLocalRot(LSL_Rotation rot)
        {
            SetRot(m_host, rot);
            ScriptSleep(m_sleepMsOnSetLocalRot);
        }

        protected static void SetRot(SceneObjectPart part, Quaternion rot)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            bool isroot = (part == part.ParentGroup.RootPart);
            bool isphys;

            PhysicsActor pa = part.PhysActor;

            // keep using physactor ideia of isphysical
            // it should be SOP ideia of that
            // not much of a issue with ubOde
            if (pa != null && pa.IsPhysical)
                isphys = true;
            else
                isphys = false;

            // SL doesn't let scripts rotate root of physical linksets
            if (isroot && isphys)
                return;

            part.UpdateRotation(rot);

            // Update rotation does not move the object in the physics engine if it's a non physical linkset
            // so do a nasty update of parts positions if is a root part rotation
            if (isroot && pa != null) // with if above implies non physical  root part
            {
                part.ParentGroup.ResetChildPrimPhysicsPositions();
            }
            else // fix sitting avatars. This is only needed bc of how we link avas to child parts, not root part
            {
                //                List<ScenePresence> sittingavas = part.ParentGroup.GetLinkedAvatars();
                List<ScenePresence> sittingavas = part.ParentGroup.GetSittingAvatars();
                if (sittingavas.Count > 0)
                {
                    foreach (ScenePresence av in sittingavas)
                    {
                        if (isroot || part.LocalId == av.ParentID)
                            av.SendTerseUpdateToAllClients();
                    }
                }
            }
        }

        /// <summary>
        /// See http://lslwiki.net/lslwiki/wakka.php?wakka=ChildRotation
        /// </summary>
        public LSL_Rotation llGetRot()
        {
            // unlinked or root prim then use llRootRotation
            // see llRootRotaion for references.
            if (m_host.LinkNum == 0 || m_host.LinkNum == 1)
            {
                return llGetRootRotation();
            }

            Quaternion q = m_host.GetWorldRotation();

            if (m_host.ParentGroup != null && m_host.ParentGroup.AttachmentPoint != 0)
            {
                ScenePresence avatar = World.GetScenePresence(m_host.ParentGroup.AttachedAvatar);
                if (avatar != null)
                {
                    if ((avatar.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_MOUSELOOK) != 0)
                        q = avatar.CameraRotation * q; // Mouselook
                    else
                        q = avatar.Rotation * q; // Currently infrequently updated so may be inaccurate
                }
            }

            return new LSL_Rotation(q.X, q.Y, q.Z, q.W);
        }

        private LSL_Rotation GetPartRot(SceneObjectPart part)
        {
            Quaternion q;
            if (part.LinkNum == 0 || part.LinkNum == 1) // unlinked or root prim
            {
                if (part.ParentGroup.AttachmentPoint != 0)
                {
                    ScenePresence avatar = World.GetScenePresence(part.ParentGroup.AttachedAvatar);
                    if (avatar != null)
                    {
                        if ((avatar.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_MOUSELOOK) != 0)
                            q = avatar.CameraRotation; // Mouselook
                        else
                            q = avatar.GetWorldRotation(); // Currently infrequently updated so may be inaccurate
                    }
                    else
                        q = part.ParentGroup.GroupRotation; // Likely never get here but just in case
                }
                else
                    q = part.ParentGroup.GroupRotation; // just the group rotation

                return new LSL_Rotation(q);
            }

            q = part.GetWorldRotation();
            if (part.ParentGroup.AttachmentPoint != 0)
            {
                ScenePresence avatar = World.GetScenePresence(part.ParentGroup.AttachedAvatar);
                if (avatar != null)
                {
                    if ((avatar.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_MOUSELOOK) != 0)
                        q = avatar.CameraRotation * q; // Mouselook
                    else
                        q = avatar.Rotation * q; // Currently infrequently updated so may be inaccurate
                }
            }

            return new LSL_Rotation(q.X, q.Y, q.Z, q.W);
        }

        public LSL_Rotation llGetLocalRot()
        {
            return GetPartLocalRot(m_host);
        }

        private static LSL_Rotation GetPartLocalRot(SceneObjectPart part)
        {
            return new LSL_Rotation(part.RotationOffset);
        }

        public void llSetForce(LSL_Vector force, int local)
        {

            if (!m_host.ParentGroup.IsDeleted)
            {
                if (local != 0)
                    force *= llGetRot();

                m_host.ParentGroup.RootPart.SetForce(force);
            }
        }

        public LSL_Vector llGetForce()
        {
            if (!m_host.ParentGroup.IsDeleted)
                return m_host.ParentGroup.RootPart.GetForce();

            return LSL_Vector.Zero;
        }

        public void llSetVelocity(LSL_Vector vel, int local)
        {
            m_host.SetVelocity(new Vector3((float)vel.x, (float)vel.y, (float)vel.z), local != 0);
        }

        public void llSetAngularVelocity(LSL_Vector avel, int local)
        {
            m_host.SetAngularVelocity(new Vector3((float)avel.x, (float)avel.y, (float)avel.z), local != 0);
        }
        public LSL_Integer llTarget(LSL_Vector position, double range)
        {
            return m_host.ParentGroup.RegisterTargetWaypoint(m_item.ItemID, position, (float)range);
        }

        public void llTargetRemove(int number)
        {
            m_host.ParentGroup.UnregisterTargetWaypoint(number);
        }

        public LSL_Integer llRotTarget(LSL_Rotation rot, double error)
        {
            return m_host.ParentGroup.RegisterRotTargetWaypoint(m_item.ItemID, rot, (float)error);
        }

        public void llRotTargetRemove(int number)
        {
            m_host.ParentGroup.UnRegisterRotTargetWaypoint(number);
        }

        public void llMoveToTarget(LSL_Vector target, double tau)
        {
            m_host.ParentGroup.MoveToTarget(target, (float)tau);
        }

        public void llStopMoveToTarget()
        {
            m_host.ParentGroup.StopMoveToTarget();
        }

        public void llApplyImpulse(LSL_Vector force, LSL_Integer local)
        {
            //No energy force yet
            Vector3 v = force;
            if (v.Length() > 20000.0f)
            {
                v.Normalize();
                v *= 20000.0f;
            }
            m_host.ApplyImpulse(v, local != 0);
        }


        public void llApplyRotationalImpulse(LSL_Vector force, int local)
        {
            m_host.ParentGroup.RootPart.ApplyAngularImpulse(force, local != 0);
        }

        public void llSetTorque(LSL_Vector torque, int local)
        {
            m_host.ParentGroup.RootPart.SetAngularImpulse(torque, local != 0);
        }

        public LSL_Vector llGetTorque()
        {

            return new LSL_Vector(m_host.ParentGroup.GetTorque());
        }

        public void llSetForceAndTorque(LSL_Vector force, LSL_Vector torque, int local)
        {
            llSetForce(force, local);
            llSetTorque(torque, local);
        }


        public LSL_Vector llGetVel()
        {

            Vector3 vel = Vector3.Zero;

            if (m_host.ParentGroup.IsAttachment)
            {
                ScenePresence avatar = m_host.ParentGroup.Scene.GetScenePresence(m_host.ParentGroup.AttachedAvatar);
                if (avatar != null)
                    vel = avatar.GetWorldVelocity();
            }
            else
            {
                vel = m_host.ParentGroup.RootPart.Velocity;
            }

            return new LSL_Vector(vel);
        }

        public LSL_Vector llGetAccel()
        {

            return new LSL_Vector(m_host.Acceleration);
        }

        public LSL_Vector llGetOmega()
        {
            Vector3 avel = m_host.AngularVelocity;
            return new LSL_Vector(avel.X, avel.Y, avel.Z);
        }

        public LSL_Float llGetTimeOfDay()
        {
            return (double)((DateTime.Now.TimeOfDay.TotalMilliseconds / 1000) % (3600 * 4));
        }

        public LSL_Float llGetWallclock()
        {
            return DateTime.Now.TimeOfDay.TotalSeconds;
        }

        public LSL_Float llGetTime()
        {
            double ScriptTime = Util.GetTimeStamp() - m_timer;
            return Math.Round(ScriptTime, 3);
        }

        public void llResetTime()
        {
            m_timer = Util.GetTimeStamp();
        }

        public LSL_Float llGetAndResetTime()
        {
            double now = Util.GetTimeStamp();
            double ScriptTime = now - m_timer;
            m_timer = now;
            return Math.Round(ScriptTime, 3);
        }

        public void llSound(string sound, double volume, int queue, int loop)
        {
            Deprecated("llSound", "Use llPlaySound instead");
        }

        // Xantor 20080528 PlaySound updated so it accepts an objectinventory name -or- a key to a sound
        // 20080530 Updated to remove code duplication
        public void llPlaySound(string sound, double volume)
        {

            if (m_SoundModule == null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound, AssetType.Sound);
            if(soundID.IsNotZero())
                m_SoundModule.SendSound(m_host, soundID, volume, false, 0, false, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void llLinkPlaySound(LSL_Integer linknumber, string sound, double volume)
        {
            llLinkPlaySound(linknumber, sound, volume, 0);
        }

        public void llLinkPlaySound(LSL_Integer linknumber, string sound, double volume, LSL_Integer flags)
        {
            if (m_SoundModule is null)
                return;
            if (m_host.ParentGroup is null || m_host.ParentGroup.IsDeleted)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound, AssetType.Sound);
            if (soundID.IsZero())
                return;

            List<SceneObjectPart> parts = GetLinkParts(m_host, linknumber.value);
            if (parts.Count == 0)
                return;

            switch (flags)
            {
                case ScriptBaseClass.SOUND_PLAY: // play
                    foreach (SceneObjectPart sop in parts)
                        m_SoundModule.SendSound(sop, soundID, volume, false, 0, false, false);
                    break;
                case ScriptBaseClass.SOUND_LOOP: // loop
                    foreach (SceneObjectPart sop in parts)
                        m_SoundModule.LoopSound(sop, soundID, volume, false, false);
                    break;
                case ScriptBaseClass.SOUND_TRIGGER: //trigger
                    foreach (SceneObjectPart sop in parts)
                        m_SoundModule.SendSound(sop, soundID, volume, true, 0, false, false);
                    break;
                case ScriptBaseClass.SOUND_SYNC: // play slave
                    foreach (SceneObjectPart sop in parts)
                        m_SoundModule.SendSound(sop, soundID, volume, false, 0, true, false);
                    break;
                case ScriptBaseClass.SOUND_SYNC | ScriptBaseClass.SOUND_LOOP: // loop slave
                    foreach (SceneObjectPart sop in parts)
                        m_SoundModule.LoopSound(sop, soundID, volume, false, true);
                    break;
            }
        }

        public void llLoopSound(string sound, double volume)
        {

            if (m_SoundModule is null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound, AssetType.Sound);
            if(soundID.IsZero())
                return;

            m_SoundModule.LoopSound(m_host, soundID, volume, false, false);
        }

        public void llLoopSoundMaster(string sound, double volume)
        {
            if (m_SoundModule is null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound, AssetType.Sound);
            if(soundID.IsZero())
                return;

            m_SoundModule.LoopSound(m_host, soundID, volume, true, false);
        }

        public void llLoopSoundSlave(string sound, double volume)
        {
            if (m_SoundModule is null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound, AssetType.Sound);
            if(soundID.IsZero())
                return;

            m_SoundModule.LoopSound(m_host, soundID, volume, false, true);
        }

        public void llPlaySoundSlave(string sound, double volume)
        {
            if (m_SoundModule is null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound, AssetType.Sound);
            if(soundID.IsZero())
                return;

            // send the sound, once, to all clients in range
            m_SoundModule.SendSound(m_host, soundID, volume, false, 0, true, false);
        }

        public void llTriggerSound(string sound, double volume)
        {
            if (m_SoundModule is null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound, AssetType.Sound);
            if(soundID.IsZero())
                return;

            // send the sound, once, to all clients in rangeTrigger or play an attached sound in this part's inventory.
            m_SoundModule.SendSound(m_host, soundID, volume, true, 0, false, false);
        }

        public void llStopSound()
        {
            m_SoundModule?.StopSound(m_host);
        }

        public void llLinkStopSound(LSL_Integer linknumber)
        {
            if (m_SoundModule is not null)
            {
                foreach(SceneObjectPart sop in GetLinkParts(linknumber))
                    m_SoundModule.StopSound(sop);
            }
        }

        public void llPreloadSound(string sound)
        {
            if (m_SoundModule is null)
                return;

            UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound, AssetType.Sound);
            if(soundID.IsZero())
                return;

            m_SoundModule.PreloadSound(m_host, soundID);
            ScriptSleep(m_sleepMsOnPreloadSound);
        }

        /// <summary>
        /// Return a portion of the designated string bounded by
        /// inclusive indices (start and end). As usual, the negative
        /// indices, and the tolerance for out-of-bound values, makes
        /// this more complicated than it might otherwise seem.
        /// </summary>
        public LSL_String llGetSubString(string src, int start, int end)
        {
            // Normalize indices (if negative).
            // After normlaization they may still be
            // negative, but that is now relative to
            // the start, rather than the end, of the
            // sequence.

            if (start < 0)
            {
                start = src.Length+start;
            }
            if (end < 0)
            {
                end = src.Length+end;
            }

            // Conventional substring
            if (start <= end)
            {
                // Implies both bounds are out-of-range.
                if (end < 0 || start >= src.Length)
                {
                    return String.Empty;
                }
                // If end is positive, then it directly
                // corresponds to the lengt of the substring
                // needed (plus one of course). BUT, it
                // must be within bounds.
                if (end >= src.Length)
                {
                    end = src.Length-1;
                }

                if (start < 0)
                {
                    return src[..(end + 1)];
                }
                // Both indices are positive
                return src[start..(end + 1)];
            }

            // Inverted substring (end < start)
            else
            {
                // Implies both indices are below the
                // lower bound. In the inverted case, that
                // means the entire string will be returned
                // unchanged.
                if (start < 0)
                {
                    return src;
                }
                // If both indices are greater than the upper
                // bound the result may seem initially counter
                // intuitive.
                if (end >= src.Length)
                {
                    return src;
                }

                if (end < 0)
                {
                    if (start < src.Length)
                    {
                        return src[start..];
                    }
                    else
                    {
                        return String.Empty;
                    }
                }
                else
                {
                    if (start < src.Length)
                    {
                        return src[..(end + 1)] + src[start..];
                    }
                    else
                    {
                        return src[..(end + 1)];
                    }
                }
            }
         }

        /// <summary>
        /// Delete substring removes the specified substring bounded
        /// by the inclusive indices start and end. Indices may be
        /// negative (indicating end-relative) and may be inverted,
        /// i.e. end < start.
        /// </summary>
        public LSL_String llDeleteSubString(string src, int start, int end)
        {

            // Normalize indices (if negative).
            // After normlaization they may still be
            // negative, but that is now relative to
            // the start, rather than the end, of the
            // sequence.
            if (start < 0)
            {
                start = src.Length+start;
            }
            if (end < 0)
            {
                end = src.Length+end;
            }
            // Conventionally delimited substring
            if (start <= end)
            {
                // If both bounds are outside of the existing
                // string, then return unchanged.
                if (end < 0 || start >= src.Length)
                {
                    return src;
                }
                // At least one bound is in-range, so we
                // need to clip the out-of-bound argument.
                if (start < 0)
                {
                    start = 0;
                }

                if (end >= src.Length)
                {
                    end = src.Length-1;
                }

                return src.Remove(start,end-start+1);
            }
            // Inverted substring
            else
            {
                // In this case, out of bounds means that
                // the existing string is part of the cut.
                if (start < 0 || end >= src.Length)
                {
                    return String.Empty;
                }

                if (end > 0)
                {
                    if (start < src.Length)
                    {
                        return src.Remove(start).Remove(0, end + 1);
                    }
                    else
                    {
                        return src.Remove(0, end + 1);
                    }
                }
                else
                {
                    if (start < src.Length)
                    {
                        return src.Remove(start);
                    }
                    else
                    {
                        return src;
                    }
                }
            }
        }

        /// <summary>
        /// Insert string inserts the specified string identified by src
        /// at the index indicated by index. Index may be negative, in
        /// which case it is end-relative. The index may exceed either
        /// string bound, with the result being a concatenation.
        /// </summary>
        // this is actually wrong. according to SL wiki, this function should not support negative indexes.
        public LSL_String llInsertString(string dest, int index, string src)
        {
            // Normalize indices (if negative).
            // After normalization they may still be
            // negative, but that is now relative to
            // the start, rather than the end, of the
            // sequence.
            char c;
            if (index < 0)
            {
                index = dest.Length+index;

                // Negative now means it is less than the lower
                // bound of the string.
                if(index > 0)
                {
                    c = dest[index];
                    if (c >= 0xDC00 && c <= 0xDFFF)
                        --index;
                }

                if (index < 0)
                {
                    return src+dest;
                }

            }
            else
            {
                c = dest[index];
                if (c >= 0xDC00 && c <= 0xDFFF)
                    ++index;
            }

            if (index >= dest.Length)
            {
                return dest + src;
            }

            // The index is in bounds.
            // In this case the index refers to the index that will
            // be assigned to the first character of the inserted string.
            // So unlike the other string operations, we do not add one
            // to get the correct string length.
            return dest[..index] + src + dest[index..];

        }

        public LSL_String llToUpper(string src)
        {
            return src.ToUpper();
        }

        public LSL_String llToLower(string src)
        {
            return src.ToLower();
        }

        public LSL_Integer llGiveMoney(LSL_Key destination, LSL_Integer amount)
        {
            if (m_item.PermsGranter.IsZero())
                return 0;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_DEBIT) == 0)
            {
                Error("llGiveMoney", "No permissions to give money");
                return 0;
            }

            if (!UUID.TryParse(destination, out UUID toID))
            {
                Error("llGiveMoney", "Bad key in llGiveMoney");
                return 0;
            }

            IMoneyModule money = World.RequestModuleInterface<IMoneyModule>();
            if (money is null)
            {
                NotImplemented("llGiveMoney");
                return 0;
            }

            void act(string _)
            {
                money.ObjectGiveMoney(m_host.ParentGroup.RootPart.UUID, m_host.ParentGroup.RootPart.OwnerID,
                                        toID, amount, UUID.Zero, out _);
            }

            m_AsyncCommands.DataserverPlugin.RegisterRequest(m_host.LocalId, m_item.ItemID, act);
            return 0;
        }

        public void llMakeExplosion(int particles, double scale, double vel, double lifetime, double arc, string texture, LSL_Vector offset)
        {
            Deprecated("llMakeExplosion", "Use llParticleSystem instead");
            ScriptSleep(m_sleepMsOnMakeExplosion);
        }

        public void llMakeFountain(int particles, double scale, double vel, double lifetime, double arc, int bounce, string texture, LSL_Vector offset, double bounce_offset)
        {
            Deprecated("llMakeFountain", "Use llParticleSystem instead");
            ScriptSleep(m_sleepMsOnMakeFountain);
        }

        public void llMakeSmoke(int particles, double scale, double vel, double lifetime, double arc, string texture, LSL_Vector offset)
        {
            Deprecated("llMakeSmoke", "Use llParticleSystem instead");
            ScriptSleep(m_sleepMsOnMakeSmoke);
        }

        public void llMakeFire(int particles, double scale, double vel, double lifetime, double arc, string texture, LSL_Vector offset)
        {
            Deprecated("llMakeFire", "Use llParticleSystem instead");
            ScriptSleep(m_sleepMsOnMakeFire);
        }

        public void llRezAtRoot(string inventory, LSL_Vector pos, LSL_Vector vel, LSL_Rotation rot, int param)
        {
            doObjectRez(inventory, pos, vel, rot, param, true);
        }

        public void doObjectRez(string inventory, LSL_Vector pos, LSL_Vector vel, LSL_Rotation rot, int param, bool atRoot)
        {
            if (string.IsNullOrEmpty(inventory) || Double.IsNaN(rot.x) || Double.IsNaN(rot.y) || Double.IsNaN(rot.z) || Double.IsNaN(rot.s))
                return;

            if (VecDistSquare(llGetPos(), pos) > m_Script10mDistanceSquare)
                return;

            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(inventory);

            if (item == null)
            {
               Error("llRez(AtRoot/Object)", "Can't find object '" + inventory + "'");
               return;
            }

            if (item.InvType != (int)InventoryType.Object)
            {
               Error("llRez(AtRoot/Object)", "Can't create requested object; object is missing from database");
               return;
            }

            Util.FireAndForget(x =>
            {
                Quaternion wrot = rot;
                wrot.Normalize();
                List<SceneObjectGroup> new_groups = World.RezObject(m_host, item, pos, wrot, vel, param, atRoot);

                // If either of these are null, then there was an unknown error.
                if (new_groups == null || new_groups.Count == 0)
                    return;

                bool notAttachment = !m_host.ParentGroup.IsAttachment;

                foreach (SceneObjectGroup group in new_groups)
                {
                    // objects rezzed with this method are die_at_edge by default.
                    group.RootPart.SetDieAtEdge(true);

                    group.ResumeScripts();

                    m_ScriptEngine.PostObjectEvent(m_host.LocalId, new EventParams(
                            "object_rez", new Object[] {
                            new LSL_String(
                            group.RootPart.UUID.ToString()) },
                            Array.Empty<DetectParams>()));

                    if (notAttachment)
                    {
                        PhysicsActor pa = group.RootPart.PhysActor;

                        //Recoil.
                        if (pa != null && pa.IsPhysical && !((Vector3)vel).IsZero())
                        {
                            Vector3 recoil = -vel * group.GetMass() * m_recoilScaleFactor;
                            if (!recoil.IsZero())
                            {
                                llApplyImpulse(recoil, 0);
                            }
                        }
                    }
                 }
            }, null, "LSL_Api.doObjectRez");

            //ScriptSleep((int)((groupmass * velmag) / 10));
            ScriptSleep(m_sleepMsOnRezAtRoot);
        }

        public void llRezObject(string inventory, LSL_Vector pos, LSL_Vector vel, LSL_Rotation rot, int param)
        {
            doObjectRez(inventory, pos, vel, rot, param, false);
        }

        public LSL_Key llRezObjectWithParams(string inventory, LSL_List lparam)
        {
            /* flags not supported
             * REZ_FLAG_DIE_ON_NOENTRY
             * REZ_FLAG_NO_COLLIDE_OWNER
             * REZ_FLAG_NO_COLLIDE_FAMILY
             *
             * parameters not supported
             * REZ_ACCEL
             * REZ_DAMAGE
             * REZ_DAMAGE_TYPE
             * REZ_OMEGA only does viewer side lltargetomega
             */

            if (string.IsNullOrEmpty(inventory))
                return LSL_Key.NullKey;

            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(inventory);
            if (item == null)
            {
               Error("llRezObjectWithParams", "Can't find object '" + inventory + "'");
               return LSL_Key.NullKey;
            }

            if (item.InvType != (int)InventoryType.Object)
            {
               Error("llRezObjectWithParams", "Can't create requested object; object is missing from database");
               return LSL_Key.NullKey;
            }

            int flags = 0;
            Vector3 pos = Vector3.Zero;
            Vector3 vel = Vector3.Zero;
            Quaternion rot = Quaternion.Identity;
            Vector3 acel = Vector3.Zero;
            Vector3 omega = Vector3.Zero;
            float omegaSpin = 0;
            float omegaGain = 0;

            float damage = 0;

            string sound = null;
            float soundVol = 0;
            bool  soundLoop = false;

            string collisionSound = null;
            float CollisionSoundVol = 0;

            int param = 0;

            Vector3 lockAxis = Vector3.Zero;

            string stringparam = null;
            bool atRoot = false;

            if(lparam != null && lparam.Length > 0)
            {
                try
                {
                    int idx = 0;
                    while (idx < lparam.Length)
                    {
                        int rezrelative = 0;
                        int code = lparam.GetIntegerItem(idx++);
                        switch(code)
                        {
                            case ScriptBaseClass.REZ_PARAM:
                                try
                                {
                                    param = lparam.GetIntegerItem(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must a integer");
                                }
                                break;

                            case ScriptBaseClass.REZ_FLAGS:
                                try
                                {
                                    flags = lparam.GetIntegerItem(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must a integer");
                                }
                                break;

                            case ScriptBaseClass.REZ_POS:
                                try
                                {
                                    pos = lparam.GetVector3Item(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be vector");
                                }

                                idx++;
                                try
                                {
                                    rezrelative = lparam.GetIntegerItem(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be integer");
                                }

                                idx++;
                                int rezAtRot = 0;
                                try
                                {
                                    rezAtRot = lparam.GetIntegerItem(idx);
                                    atRoot = rezAtRot != 0;
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be integer");
                                }

                                if(rezrelative != 0)
                                {
                                    if(pos.LengthSquared() > m_Script10mDistanceSquare)
                                        return LSL_Key.NullKey;
                                    pos += m_host.GetWorldPosition();
                                }
                                else if ((pos - m_host.GetWorldPosition()).LengthSquared() > m_Script10mDistanceSquare)
                                    return LSL_Key.NullKey;

                                break;

                            case ScriptBaseClass.REZ_ROT:
                                try
                                {
                                    rot = lparam.GetQuaternionItem(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be rotation");
                                }

                                idx++;
                                try
                                {
                                    rezrelative = lparam.GetIntegerItem(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be integer");
                                }

                                if(rezrelative > 0)
                                    rot *= m_host.GetWorldRotation();

                                rot.Normalize();
                                break;

                            case ScriptBaseClass.REZ_VEL:
                                try
                                {
                                    vel = lparam.GetVector3Item(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be vector");
                                }

                                idx++;
                                try
                                {
                                    rezrelative = lparam.GetIntegerItem(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be integer");
                                }

                                idx++;
                                int addVel = 0;
                                try
                                {
                                    addVel = lparam.GetIntegerItem(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be integer");
                                }

                                if(rezrelative > 0)
                                    vel *= m_host.GetWorldRotation();
                                if(addVel > 0)
                                    vel += m_host.ParentGroup.Velocity;

                                break;

                            case ScriptBaseClass.REZ_ACCEL:
                                try
                                {
                                    acel = lparam.GetVector3Item(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be vector");
                                }

                                idx++;
                                try
                                {
                                    rezrelative = lparam.GetIntegerItem(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be integer");
                                }

                                break;

                            case ScriptBaseClass.REZ_OMEGA:
                                try
                                {
                                    omega = lparam.GetVector3Item(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be vector");
                                }

                                idx++;
                                try
                                {
                                    rezrelative = lparam.GetIntegerItem(idx);
                                    if(rezrelative > 0)
                                        omega *= m_host.GetWorldRotation();
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be integer");
                                }

                                idx++;
                                try
                                {
                                    omegaSpin = lparam.GetFloatItem(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be float");
                                }

                                idx++;
                                try
                                {
                                    omegaGain = lparam.GetFloatItem(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be float");
                                }

                                break;

                            case ScriptBaseClass.REZ_DAMAGE:
                                try
                                {
                                    damage = lparam.GetFloatItem(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be float");
                                }
                                break;

                            case ScriptBaseClass.REZ_SOUND:
                                try
                                {
                                    sound = lparam.GetStringItem(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be string");
                                }

                                idx++;
                                try
                                {
                                    soundVol = lparam.GetFloatItem(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be float");
                                }

                                idx++;
                                try
                                {
                                    soundLoop = lparam.GetIntegerItem(idx) > 0;
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be integer");
                                }
                                break;

                            case ScriptBaseClass.REZ_SOUND_COLLIDE:
                                try
                                {
                                    collisionSound = lparam.GetStringItem(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be string");
                                }

                                idx++;
                                try
                                {
                                    CollisionSoundVol = lparam.GetFloatItem(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be float");
                                }
                                break;

                            case ScriptBaseClass.REZ_LOCK_AXES:
                                try
                                {
                                    lockAxis = lparam.GetVector3Item(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be vector");
                                }
                                break;
                            case ScriptBaseClass.REZ_DAMAGE_TYPE:
                                try
                                {
                                    int damageType = lparam.GetIntegerItem(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be integer");
                                }
                                break;

                            case ScriptBaseClass.REZ_PARAM_STRING:
                                try
                                {
                                    stringparam = lparam.GetStringItem(idx);
                                }
                                catch (InvalidCastException)
                                {
                                    throw new InvalidCastException($"arg #{idx} must be string");
                                }

                                _ = Utils.osUTF8GetBytesCount(stringparam, 1024, out int maxsourcelen);
                                if(maxsourcelen < stringparam.Length)
                                    stringparam = stringparam[..maxsourcelen];
                                break;

                            default:
                                Error("llRezObjectWithParams", $"Unknown parameter {code} at {idx}");
                                return LSL_Key.NullKey;
                        }
                        idx++;
                    }
                }
                catch (Exception e)
                {
                    Error("llRezObjectWithParams", "error " + e.Message);
                    return LSL_Key.NullKey;
                }
            }

            UUID newID = UUID.Random();

            Util.FireAndForget(x =>
            {
                SceneObjectGroup sog = m_host.Inventory.GetSingleRezReadySceneObject(item, m_host.OwnerID, m_host.GroupID);
                if(sog is null)
                    return;

                int totalPrims  = sog.PrimCount;

                if (!World.Permissions.CanRezObject(totalPrims, m_host.OwnerID, pos))
                    return;

                if (!World.Permissions.BypassPermissions())
                {
                    if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                        m_host.Inventory.RemoveInventoryItem(item.ItemID);
                }

                //  position adjust
                if (!atRoot && totalPrims > 1) // nothing to do on a single prim
                {
                    pos -= sog.GetGeometricCenter() * rot;
                }

                if (sog.IsAttachment == false && sog.RootPart.Shape.State != 0)
                {
                    sog.RootPart.AttachedPos = sog.AbsolutePosition;
                    sog.RootPart.Shape.LastAttachPoint = (byte)sog.AttachmentPoint;
                }

                sog.RezzerID = m_host.ParentGroup.RootPart.UUID;
                sog.UUID = newID;

                // We can only call this after adding the scene object, since the scene object references the scene
                // to find out if scripts should be activated at all.
 
                SceneObjectPart groot = sog.RootPart;

                if(groot.PhysActor != null)
                    groot.PhysActor.Building = true;

                // objects rezzed with this method are die_at_edge by default.
                groot.SetDieAtEdge(true);

                if((flags & ScriptBaseClass.REZ_FLAG_TEMP) != 0)
                    groot.AddFlag(PrimFlags.TemporaryOnRez);
                else 
                    groot.RemFlag(PrimFlags.TemporaryOnRez);

                if(!sog.IsVolumeDetect && (flags & ScriptBaseClass.REZ_FLAG_PHYSICAL) != 0)
                {
                    groot.KeyframeMotion?.Stop();
                    groot.KeyframeMotion = null;
                    groot.AddFlag(PrimFlags.Physics);
                }
                else
                    groot.RemFlag(PrimFlags.Physics);

                if((flags & ScriptBaseClass.REZ_FLAG_PHANTOM) != 0)
                    groot.AddFlag(PrimFlags.Phantom);
                else if (!sog.IsVolumeDetect)
                    groot.RemFlag(PrimFlags.Phantom);

                sog.BlockGrabOverride = (flags & ScriptBaseClass.REZ_FLAG_BLOCK_GRAB_OBJECT) != 0;

                if(groot.PhysActor != null)
                    groot.PhysActor.Building = false;

                sog.RezStringParameter = stringparam;

                sog.InvalidateEffectivePerms();
                if(omegaGain > 1e-6)
                {
                    groot.UpdateAngularVelocity(omega * omegaSpin);
                }

                if(lockAxis.IsNotZero())
                {
                    byte axislock = 0;
                    if(lockAxis.X != 0)
                        axislock = 2;
                    if(lockAxis.Y != 0)
                        axislock |= 4;
                    if(lockAxis.X != 0)
                        axislock |= 8;
                    groot.RotationAxisLocks = axislock;
                }

                if(collisionSound != null)
                {
                    UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, collisionSound, AssetType.Sound);
                    if(soundID.IsNotZero())
                    {
                        groot.CollisionSoundType = 1;
                        groot.CollisionSoundVolume = CollisionSoundVol;
                        groot.CollisionSound = soundID;
                    }
                    else
                        groot.CollisionSoundType = -1;
                }

                World.AddNewSceneObject(sog, true, pos, rot, vel);

                sog.CreateScriptInstances(param, true, m_ScriptEngine.ScriptEngineName, 3);
                sog.ResumeScripts();

                sog.ScheduleGroupForUpdate(PrimUpdateFlags.FullUpdatewithAnimMatOvr);

                m_ScriptEngine.PostObjectEvent(m_host.LocalId, new EventParams(
                        "object_rez",
                        [
                            new LSL_String(groot.UUID.ToString())
                        ],
                        Array.Empty<DetectParams>()));

                if(soundVol > 0 && !string.IsNullOrEmpty(sound) && m_SoundModule is not null)
                {
                    UUID soundID = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound, AssetType.Sound);
                    if(soundID.IsNotZero())
                    {
                        if(soundLoop)
                            m_SoundModule.LoopSound(groot, soundID, soundVol, false, false);
                        else
                            m_SoundModule.SendSound(groot, soundID, soundVol, false, 0, false, false);
                    }
                }

            }, null, "LSL_Api.ObjectRezWithParam");

            ScriptSleep(m_sleepMsOnRezAtRoot);
            return new(newID.ToString());
        }

        public void llLookAt(LSL_Vector target, double strength, double damping)
        {
            SceneObjectGroup sog = m_host.ParentGroup;
            if (sog is null || sog.IsDeleted)
                return;

            // Get the normalized vector to the target
            LSL_Vector from = llGetPos();

             // normalized direction to target
            LSL_Vector dir = llVecNorm(target - from);

            LSL_Vector left = new(-dir.y, dir.x, 0.0f);
            left = llVecNorm(left);
            // make up orthogonal to left and dir
            LSL_Vector up  = LSL_Vector.Cross(dir, left);

            // compute rotation based on orthogonal axes
            // and rotate so Z points to target with X below horizont
            LSL_Rotation rot = new LSL_Rotation(0.0, 0.707107, 0.0, 0.707107) * llAxes2Rot(dir, left, up);

            if (!sog.UsesPhysics || sog.IsAttachment)
            {
                // Do nothing if either value is 0 (this has been checked in SL)
                if (strength <= 0.0 || damping <= 0.0)
                    return;

                llSetLocalRot(rot);
            }
            else
            {
                if (strength == 0)
                {
                    llSetLocalRot(rot);
                    return;
                }

                sog.StartLookAt(rot, (float)strength, (float)damping);
            }
        }

        public void llStopLookAt()
        {
            m_host.StopLookAt();
        }

        public void llSetTimerEvent(double sec)
        {
            if (sec != 0.0 && sec < m_MinTimerInterval)
                sec = m_MinTimerInterval;
            // Setting timer repeat
            m_AsyncCommands.TimerPlugin.SetTimerEvent(m_host.LocalId, m_item.ItemID, sec);
        }

        public virtual void llSleep(double sec)
        {
//            m_log.Info("llSleep snoozing " + sec + "s.");

            Sleep((int)(sec * 1000));
        }

        public LSL_Float llGetMass()
        {
            if (m_host.ParentGroup.IsAttachment)
            {
                ScenePresence attachedAvatar = World.GetScenePresence(m_host.ParentGroup.AttachedAvatar);
                return attachedAvatar is null ? 0 : attachedAvatar.GetMass();
            }
            else
                return m_host.ParentGroup.GetMass();
        }

        public LSL_Float llGetMassMKS()
        {
            return 100f * llGetMass();
        }

        public void llCollisionFilter(LSL_String name, LSL_Key id, LSL_Integer accept)
        {
            _ = UUID.TryParse(id, out UUID objectID);
            if(objectID.IsZero())
                m_host.SetCollisionFilter(accept != 0, name.m_string.ToLower(CultureInfo.InvariantCulture), string.Empty);
            else
                m_host.SetCollisionFilter(accept != 0, name.m_string.ToLower(CultureInfo.InvariantCulture), objectID.ToString());
        }

        public void llTakeControls(int controls, int accept, int pass_on)
        {
            if (!m_item.PermsGranter.IsZero())
            {
                ScenePresence presence = World.GetScenePresence(m_item.PermsGranter);

                if (presence != null)
                {
                    if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TAKE_CONTROLS) != 0)
                    {
                        presence.RegisterControlEventsToScript(controls, accept, pass_on, m_host.LocalId, m_item.ItemID);
                    }
                }
            }

        }

        public void llReleaseControls()
        {

            if (!m_item.PermsGranter.IsZero())
            {
                ScenePresence presence = World.GetScenePresence(m_item.PermsGranter);

                if (presence != null)
                {
                    if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TAKE_CONTROLS) != 0)
                    {
                        // Unregister controls from Presence
                        presence.UnRegisterControlEventsToScript(m_host.LocalId, m_item.ItemID);
                     // Remove Take Control permission.
                        m_item.PermsMask &= ~ScriptBaseClass.PERMISSION_TAKE_CONTROLS;
                    }
                }
            }
        }

        public void llReleaseURL(string url)
        {
            m_UrlModule?.ReleaseURL(url);
        }

        /// <summary>
        /// Attach the object containing this script to the avatar that owns it.
        /// </summary>
        /// <param name='attachmentPoint'>
        /// The attachment point (e.g. <see cref="OpenSim.Region.ScriptEngine.Shared.ScriptBase.ScriptBaseClass.ATTACH_CHEST">ATTACH_CHEST</see>)
        /// </param>
        /// <returns>true if the attach suceeded, false if it did not</returns>
        public bool AttachToAvatar(int attachmentPoint)
        {
            SceneObjectGroup grp = m_host.ParentGroup;
            ScenePresence presence = World.GetScenePresence(m_host.OwnerID);

            IAttachmentsModule attachmentsModule = m_ScriptEngine.World.AttachmentsModule;

            if (attachmentsModule != null)
                return attachmentsModule.AttachObject(presence, grp, (uint)attachmentPoint, false, true, true);
            else
                return false;
        }

        /// <summary>
        /// Detach the object containing this script from the avatar it is attached to.
        /// </summary>
        /// <remarks>
        /// Nothing happens if the object is not attached.
        /// </remarks>
        public void DetachFromAvatar()
        {
            Util.FireAndForget(DetachWrapper, m_host, "LSL_Api.DetachFromAvatar");
        }

        private void DetachWrapper(object o)
        {
            if (World.AttachmentsModule != null)
            {
                SceneObjectPart host = (SceneObjectPart)o;
                ScenePresence presence = World.GetScenePresence(host.OwnerID);
                World.AttachmentsModule.DetachSingleAttachmentToInv(presence, host.ParentGroup);
            }
        }

        public void llAttachToAvatar(LSL_Integer attachmentPoint)
        {

            if (m_item.PermsGranter != m_host.OwnerID)
                return;

            SceneObjectGroup grp = m_host.ParentGroup;
            if (grp == null || grp.IsDeleted || grp.IsAttachment)
                return;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_ATTACH) != 0)
                AttachToAvatar(attachmentPoint);
        }

        public void llAttachToAvatarTemp(LSL_Integer attachmentPoint)
        {
            IAttachmentsModule attachmentsModule = World.RequestModuleInterface<IAttachmentsModule>();
            if (attachmentsModule == null)
                return;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_ATTACH) == 0)
                return;

            SceneObjectGroup grp = m_host.ParentGroup;
            if (grp == null || grp.IsDeleted || grp.IsAttachment)
                return;

            if (!World.TryGetScenePresence(m_item.PermsGranter, out ScenePresence target))
                return;

            if (target.UUID != grp.OwnerID)
            {
                uint effectivePerms = grp.EffectiveOwnerPerms;

                if ((effectivePerms & (uint)PermissionMask.Transfer) == 0)
                    return;

                UUID permsgranter = m_item.PermsGranter;
                int permsmask = m_item.PermsMask;
                grp.SetOwner(target.UUID, target.ControllingClient.ActiveGroupId);

                if (World.Permissions.PropagatePermissions())
                {
                    foreach (SceneObjectPart child in grp.Parts)
                    {
                        child.Inventory.ChangeInventoryOwner(target.UUID);
                        child.TriggerScriptChangedEvent(Changed.OWNER);
                        child.ApplyNextOwnerPermissions();
                    }
                    grp.InvalidateEffectivePerms();
                }

                m_item.PermsMask = permsmask;
                m_item.PermsGranter = permsgranter;

                grp.RootPart.ObjectSaleType = 0;
                grp.RootPart.SalePrice = 10;

                grp.HasGroupChanged = true;
                grp.RootPart.SendPropertiesToClient(target.ControllingClient);
                grp.RootPart.ScheduleFullUpdate();
            }

            attachmentsModule.AttachObject(target, grp, (uint)attachmentPoint, false, false, true);
        }

    public void llDetachFromAvatar()
        {

            if (m_host.ParentGroup.AttachmentPoint == 0)
                return;

            if (m_item.PermsGranter != m_host.OwnerID)
                return;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_ATTACH) != 0)
                DetachFromAvatar();
        }

        public void llTakeCamera(string avatar)
        {
            Deprecated("llTakeCamera", "Use llSetCameraParams instead");
        }

        public void llReleaseCamera(string avatar)
        {
            Deprecated("llReleaseCamera", "Use llClearCameraParams instead");
        }

        public LSL_Key llGetOwner()
        {

            return m_host.OwnerID.ToString();
        }

        public void llInstantMessage(string userKey, string message)
        {
            if (m_TransferModule == null || String.IsNullOrEmpty(message))
                return;

           if (!UUID.TryParse(userKey, out UUID userID) || userID.IsZero())
            {
                Error("llInstantMessage","An invalid key  was passed to llInstantMessage");
                ScriptSleep(2000);
                return;
            }

            Vector3 pos = m_host.AbsolutePosition;
            GridInstantMessage msg = new()
            {
                fromAgentID = m_host.OwnerID.Guid,
                toAgentID = userID.Guid,
                imSessionID = m_host.UUID.Guid, // This is the item we're mucking with here
                timestamp = (uint)Util.UnixTimeSinceEpoch(),
                fromAgentName = m_host.Name, //client.FirstName + " " + client.LastName;// fromAgentName;
                dialog = 19, // MessageFromObject
                fromGroup = false,
                offline = 0,
                ParentEstateID = World.RegionInfo.EstateSettings.EstateID,
                Position = pos,
                RegionID = World.RegionInfo.RegionID.Guid,
                message = (message.Length > 1024) ? message[..1024] : message,
                binaryBucket = Util.StringToBytes256($"{m_regionName}/{(int)pos.X}/{(int)pos.Y}/{(int)pos.Z}")
            };

            m_TransferModule?.SendInstantMessage(msg, delegate(bool success) {});
            ScriptSleep(m_sleepMsOnInstantMessage);
      }

        public void llEmail(string address, string subject, string message)
        {
            if (m_emailModule == null)
            {
                Error("llEmail", "Email module not configured");
                return;
            }

            // this is a fire and forget no event is sent to script
            void act(string eventID)
            {
                //Restrict email destination to the avatars registered email address?
                //The restriction only applies if the destination address is not local.
                if (m_restrictEmail == true && address.Contains(m_internalObjectHost) == false)
                {
                    UserAccount account = m_userAccountService.GetUserAccount(RegionScopeID, m_host.OwnerID);
                    if (account == null)
                        return;

                    if (String.IsNullOrEmpty(account.Email))
                        return;
                    address = account.Email;
                }

                m_emailModule.SendEmail(m_host.UUID, m_host.ParentGroup.OwnerID, address, subject, message);
                // no dataserver event
            }

            m_AsyncCommands.DataserverPlugin.RegisterRequest(m_host.LocalId,
                                                     m_item.ItemID, act);
            ScriptSleep(m_sleepMsOnEmail);
        }

        public void llGetNextEmail(string address, string subject)
        {
            if (m_emailModule == null)
            {
                Error("llGetNextEmail", "Email module not configured");
                return;
            }
            Email email;

            email = m_emailModule.GetNextEmail(m_host.UUID, address, subject);

            if (email == null)
                return;

            m_ScriptEngine.PostObjectEvent(m_host.LocalId,
                    new EventParams("email",
                    new Object[] {
                        new LSL_String(email.time),
                        new LSL_String(email.sender),
                        new LSL_String(email.subject),
                        new LSL_String(email.message),
                        new LSL_Integer(email.numLeft)},
                    Array.Empty<DetectParams>()));

        }

        public void llTargetedEmail(LSL_Integer target, LSL_String subject, LSL_String message)
        {

            SceneObjectGroup parent = m_host.ParentGroup;
            if (parent == null || parent.IsDeleted)
                return;

            if (m_emailModule == null)
            {
                Error("llTargetedEmail", "Email module not configured");
                return;
            }

            if (subject.Length + message.Length > 4096)
            {
                Error("llTargetedEmail", "Message is too large");
                return;
            }

            // this is a fire and forget no event is sent to script
            void act(string eventID)
            {
                UserAccount account = null;
                if (target == ScriptBaseClass.TARGETED_EMAIL_OBJECT_OWNER)
                {
                    if (parent.OwnerID.Equals(parent.GroupID))
                        return;
                    account = m_userAccountService.GetUserAccount(RegionScopeID, parent.OwnerID);
                }
                else if (target == ScriptBaseClass.TARGETED_EMAIL_ROOT_CREATOR)
                {
                    // non standard avoid creator spam
                    if (m_item.CreatorID.Equals(parent.RootPart.CreatorID))
                        account = m_userAccountService.GetUserAccount(RegionScopeID, parent.RootPart.CreatorID);
                    else
                        return;
                }
                else
                    return;

                if (account == null)
                    return;

                if (String.IsNullOrEmpty(account.Email))
                    return;

                m_emailModule.SendEmail(m_host.UUID, m_host.ParentGroup.OwnerID, account.Email, subject, message);
            }

            m_AsyncCommands.DataserverPlugin.RegisterRequest(m_host.LocalId,
                                                     m_item.ItemID, act);
            ScriptSleep(m_sleepMsOnEmail);
        }

        public LSL_Key llGetKey()
        {
            return m_host.UUID.ToString();
        }

        public LSL_Key llGenerateKey()
        {
            return UUID.Random().ToString();
        }

        public void llSetBuoyancy(double buoyancy)
        {

            if (!m_host.ParentGroup.IsDeleted)
            {
                m_host.ParentGroup.RootPart.SetBuoyancy((float)buoyancy);
            }
        }

        /// <summary>
        /// Attempt to clamp the object on the Z axis at the given height over tau seconds.
        /// </summary>
        /// <param name="height">Height to hover.  Height of zero disables hover.</param>
        /// <param name="water">False if height is calculated just from ground, otherwise uses ground or water depending on whichever is higher</param>
        /// <param name="tau">Number of seconds over which to reach target</param>
        public void llSetHoverHeight(double height, int water, double tau)
        {

            PIDHoverType hoverType = PIDHoverType.Ground;
            if (water != 0)
            {
                hoverType = PIDHoverType.GroundAndWater;
            }
            m_host.SetHoverHeight((float)height, hoverType, (float)tau);
        }

        public void llStopHover()
        {
            m_host.SetHoverHeight(0f, PIDHoverType.Ground, 0f);
        }

        public void llMinEventDelay(double delay)
        {
            try
            {
                m_ScriptEngine.SetMinEventDelay(m_item.ItemID, delay);
            }
            catch (NotImplementedException)
            {
                // Currently not implemented in DotNetEngine only XEngine
                NotImplemented("llMinEventDelay", "In DotNetEngine");
            }
        }

        public void llSoundPreload(string sound)
        {
            Deprecated("llSoundPreload", "Use llPreloadSound instead");
        }

        public void llRotLookAt(LSL_Rotation target, double strength, double damping)
        {

            // Per discussion with Melanie, for non-physical objects llLookAt appears to simply
            // set the rotation of the object, copy that behavior
            SceneObjectGroup sog = m_host.ParentGroup;
            if(sog == null || sog.IsDeleted)
                return;

            if (strength == 0 || !sog.UsesPhysics || sog.IsAttachment)
            {
                llSetLocalRot(target);
            }
            else
            {
                sog.RotLookAt(target, (float)strength, (float)damping);
            }
        }

        public LSL_Integer llStringLength(string str)
        {
            if(str == null || str.Length <= 0)
                return 0;
            return str.Length;
        }

        public void llStartAnimation(string anim)
        {

            if (m_item.PermsGranter.IsZero())
                return;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION) != 0)
            {
                ScenePresence presence = World.GetScenePresence(m_item.PermsGranter);

                if (presence is not null)
                {
                    // Do NOT try to parse UUID, animations cannot be triggered by ID
                    UUID animID = ScriptUtils.GetAssetIdFromItemName(m_host, anim, (int)AssetType.Animation);
                    if (animID.IsZero())
                        presence.Animator.AddAnimation(anim, m_host.UUID);
                    else
                        presence.Animator.AddAnimation(animID, m_host.UUID);
                }
            }
        }
        public void llStopAnimation(string anim)
        {

            if (m_item.PermsGranter.IsZero())
                return;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION) != 0)
            {
                ScenePresence presence = World.GetScenePresence(m_item.PermsGranter);

                if (presence is not null)
                {
                    UUID animID = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, anim);
                    if (animID.IsNotZero())
                        presence.Animator.RemoveAnimation(animID, true);
                    else if (presence.TryGetAnimationOverride(anim.ToUpper(), out UUID sitanimID))
                        presence.Animator.RemoveAnimation(sitanimID, true);
                    else
                        presence.Animator.RemoveAnimation(anim);
                }
            }
        }

        public void llStartObjectAnimation(string anim)
        {
            // Do NOT try to parse UUID, animations cannot be triggered by ID
            UUID animID = ScriptUtils.GetAssetIdFromItemName(m_host, anim, (int)AssetType.Animation);
            if (animID.IsZero())
                animID = DefaultAvatarAnimations.GetDefaultAnimation(anim);
            if (animID.IsNotZero())
                m_host.AddAnimation(animID, anim);
        }

        public void llStopObjectAnimation(string anim)
        {
            m_host.RemoveAnimation(anim);
        }

        public LSL_List llGetObjectAnimationNames()
        {
            LSL_List ret = new();

            if(m_host.AnimationsNames is null || m_host.AnimationsNames.Count == 0)
                return ret;

            foreach (string name in m_host.AnimationsNames.Values)
                ret.Add(new LSL_String(name));
            return ret;
        }

        public void llPointAt(LSL_Vector pos)
        {
        }

        public void llStopPointAt()
        {
        }

        public void llTargetOmega(LSL_Vector axis, double spinrate, double gain)
        {
            TargetOmega(m_host, axis, (float)spinrate, (float)gain);
        }

        protected static void TargetOmega(SceneObjectPart part, LSL_Vector axis, float spinrate, float gain)
        {
            if(MathF.Abs(gain) < 1e-6f)
            {
                part.UpdateAngularVelocity(Vector3.Zero);
                part.ScheduleFullAnimUpdate();
            }
            else
                part.UpdateAngularVelocity((Vector3)axis * spinrate);
        }

        public LSL_Integer llGetStartParameter()
        {
            return m_ScriptEngine.GetStartParameter(m_item.ItemID);
        }

        public LSL_String  llGetStartString()
        {
            if(string.IsNullOrEmpty(m_host.ParentGroup.RezStringParameter))
                return LSL_String.Empty;
            return new(m_host.ParentGroup.RezStringParameter);
        }

        public void llRequestPermissions(string agent, int perm)
        {
            if (!UUID.TryParse(agent, out UUID agentID) || agentID.IsZero())
                return;

            if (agentID.IsZero() || perm == 0) // Releasing permissions
            {
                llReleaseControls();

                m_item.PermsGranter = UUID.Zero;
                m_item.PermsMask = 0;

                m_ScriptEngine.PostScriptEvent(m_item.ItemID, new EventParams(
                        "run_time_permissions", new Object[] {
                        new LSL_Integer(0) },
                        Array.Empty<DetectParams>()));

                return;
            }

            if (m_item.PermsGranter != agentID || (perm & ScriptBaseClass.PERMISSION_TAKE_CONTROLS) == 0)
                llReleaseControls();


            int implicitPerms = 0;

            if (m_host.ParentGroup.IsAttachment && (UUID)agent == m_host.ParentGroup.AttachedAvatar)
            {
                // When attached, certain permissions are implicit if requested from owner
                implicitPerms = ScriptBaseClass.PERMISSION_TAKE_CONTROLS |
                        ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION |
                        ScriptBaseClass.PERMISSION_CONTROL_CAMERA |
                        ScriptBaseClass.PERMISSION_TRACK_CAMERA |
                        ScriptBaseClass.PERMISSION_ATTACH |
                        ScriptBaseClass.PERMISSION_OVERRIDE_ANIMATIONS;
            }
            else
            {
                if (m_host.ParentGroup.HasSittingAvatar(agentID))
                {
                    // When agent is sitting, certain permissions are implicit if requested from sitting agent
                    implicitPerms = ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION |
                        ScriptBaseClass.PERMISSION_CONTROL_CAMERA |
                        ScriptBaseClass.PERMISSION_TRACK_CAMERA |
                        ScriptBaseClass.PERMISSION_TAKE_CONTROLS;
                }
                else
                {
                    if (World.GetExtraSetting("auto_grant_attach_perms") == "true")
                        implicitPerms = ScriptBaseClass.PERMISSION_ATTACH;
                }
                if (World.GetExtraSetting("auto_grant_all_perms") == "true")
                {
                    implicitPerms = perm;
                }
            }

            if ((perm & (~implicitPerms)) == 0) // Requested only implicit perms
            {
                m_host.TaskInventory.LockItemsForWrite(true);
                m_host.TaskInventory[m_item.ItemID].PermsGranter = agentID;
                m_host.TaskInventory[m_item.ItemID].PermsMask = perm;
                m_host.TaskInventory.LockItemsForWrite(false);

                m_ScriptEngine.PostScriptEvent(m_item.ItemID, new EventParams(
                        "run_time_permissions", new Object[] {
                        new LSL_Integer(perm) },
                        Array.Empty<DetectParams>()));

                return;
            }

            ScenePresence presence = World.GetScenePresence(agentID);

            if (presence != null)
            {
                // If permissions are being requested from an NPC and were not implicitly granted above then
                // auto grant all requested permissions if the script is owned by the NPC or the NPCs owner
                INPCModule npcModule = World.RequestModuleInterface<INPCModule>();
                if (npcModule != null && npcModule.IsNPC(agentID, World))
                {
                    if (npcModule.CheckPermissions(agentID, m_host.OwnerID))
                    {
                        lock (m_host.TaskInventory)
                        {
                            m_host.TaskInventory[m_item.ItemID].PermsGranter = agentID;
                            m_host.TaskInventory[m_item.ItemID].PermsMask = perm;
                        }

                        m_ScriptEngine.PostScriptEvent(
                            m_item.ItemID,
                            new EventParams(
                                "run_time_permissions", new Object[] { new LSL_Integer(perm) }, Array.Empty<DetectParams>()));
                    }

                    // it is an NPC, exit even if the permissions werent granted above, they are not going to answer
                    // the question!
                    return;
                }

                string ownerName = resolveName(m_host.ParentGroup.RootPart.OwnerID);
                if (ownerName == String.Empty)
                    ownerName = "(hippos)";

                if (!m_waitingForScriptAnswer)
                {
                    m_host.TaskInventory.LockItemsForWrite(true);
                    m_host.TaskInventory[m_item.ItemID].PermsGranter = agentID;
                    m_host.TaskInventory[m_item.ItemID].PermsMask = 0;
                    m_host.TaskInventory.LockItemsForWrite(false);

                    presence.ControllingClient.OnScriptAnswer += handleScriptAnswer;
                    m_waitingForScriptAnswer=true;
                }

                presence.ControllingClient.SendScriptQuestion(
                    m_host.UUID, m_host.ParentGroup.RootPart.Name, ownerName, m_item.ItemID, perm);

                return;
            }

            // Requested agent is not in range, refuse perms
            m_ScriptEngine.PostScriptEvent(
                m_item.ItemID,
                new EventParams("run_time_permissions", new Object[] { new LSL_Integer(0) }, Array.Empty<DetectParams>()));
        }

        void handleScriptAnswer(IClientAPI client, UUID taskID, UUID itemID, int answer)
        {
            if (taskID != m_host.UUID)
                return;

            client.OnScriptAnswer -= handleScriptAnswer;
            m_waitingForScriptAnswer = false;

            if ((answer & ScriptBaseClass.PERMISSION_TAKE_CONTROLS) == 0)
                llReleaseControls();

            m_host.TaskInventory.LockItemsForWrite(true);
            m_host.TaskInventory[m_item.ItemID].PermsMask = answer;
            m_host.TaskInventory.LockItemsForWrite(false);

            m_ScriptEngine.PostScriptEvent(m_item.ItemID, new EventParams(
                    "run_time_permissions", new Object[] {
                    new LSL_Integer(answer) },
                    Array.Empty<DetectParams>()));
        }

        public LSL_Key llGetPermissionsKey()
        {

            return m_item.PermsGranter.ToString();
        }

        public LSL_Integer llGetPermissions()
        {

            int perms = m_item.PermsMask;

            if (m_automaticLinkPermission)
                perms |= ScriptBaseClass.PERMISSION_CHANGE_LINKS;

            return perms;
        }

        public LSL_Integer llGetLinkNumber()
        {

            if (m_host.ParentGroup.PrimCount > 1)
            {
                return m_host.LinkNum;
            }
            else
            {
                return 0;
            }
        }

        public void llSetLinkColor(int linknumber, LSL_Vector color, int face)
        {
            List<SceneObjectPart> parts = GetLinkParts(linknumber);
            if (parts.Count > 0)
            {
                try
                {
                    foreach (SceneObjectPart part in parts)
                        part.SetFaceColorAlpha(face, color, null);
                }
                finally { }
            }
        }

        public void llCreateLink(LSL_Key target, LSL_Integer parent)
        {
            if (!m_automaticLinkPermission)
            {
                if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_CHANGE_LINKS) == 0)
                {
                    Error("llCreateLink", "PERMISSION_CHANGE_LINKS required");
                    return;
                }
                if (m_item.PermsGranter.NotEqual(m_host.ParentGroup.OwnerID))
                {
                    Error("llCreateLink", "PERMISSION_CHANGE_LINKS not set by script owner");
                    return;
                }
            }

            CreateLink(target, parent);
        }

        public void CreateLink(string target, int parent)
        {
            if (!UUID.TryParse(target, out UUID targetID) || targetID.IsZero())
                return;

            SceneObjectGroup hostgroup = m_host.ParentGroup;
            if (hostgroup.AttachmentPoint != 0)
                return; // Fail silently if attached
            if ((hostgroup.RootPart.OwnerMask & (uint)PermissionMask.Modify) == 0)
                return;

            SceneObjectPart targetPart = World.GetSceneObjectPart(targetID);
            if (targetPart == null)
                return;

            SceneObjectGroup targetgrp = targetPart.ParentGroup;

            if (targetgrp == null || targetgrp.OwnerID.NotEqual(hostgroup.OwnerID))
                return;

            if (targetgrp.AttachmentPoint != 0)
                return; // Fail silently if attached
            if ((targetgrp.RootPart.OwnerMask & (uint)PermissionMask.Modify) == 0)
                return;

            SceneObjectGroup parentPrim, childPrim;

            if (parent != 0)
            {
                parentPrim = hostgroup;
                childPrim = targetgrp;
            }
            else
            {
                parentPrim = targetgrp;
                childPrim = hostgroup;
            }

            // Required for linking
            childPrim.RootPart.ClearUpdateSchedule();
            parentPrim.LinkToGroup(childPrim, true);

            parentPrim.TriggerScriptChangedEvent(Changed.LINK);
            parentPrim.RootPart.CreateSelected = false;
            parentPrim.HasGroupChanged = true;
            parentPrim.ScheduleGroupForFullUpdate();

            IClientAPI client = null;
            ScenePresence sp = World.GetScenePresence(m_host.OwnerID);
            if (sp != null)
                client = sp.ControllingClient;

            if (client != null)
                parentPrim.SendPropertiesToClient(client);

            ScriptSleep(m_sleepMsOnCreateLink);
        }

        public void llBreakLink(int linknum)
        {
            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_CHANGE_LINKS) == 0
                && !m_automaticLinkPermission)
            {
                Error("llBreakLink", "PERMISSION_CHANGE_LINKS permission not set");
                return;
            }

            BreakLink(linknum);
        }

        public void BreakLink(int linknum)
        {
            if (linknum < ScriptBaseClass.LINK_THIS)
                return;

            SceneObjectGroup parentSOG = m_host.ParentGroup;

            if (parentSOG.AttachmentPoint != 0)
                return; // Fail silently if attached

            if ((parentSOG.RootPart.OwnerMask & (uint)PermissionMask.Modify) == 0)
                return;

            SceneObjectPart childPrim = null;

            switch (linknum)
            {
                case ScriptBaseClass.LINK_ROOT:
                case ScriptBaseClass.LINK_SET:
                case ScriptBaseClass.LINK_ALL_OTHERS:
                case ScriptBaseClass.LINK_ALL_CHILDREN:
                    break;
                case ScriptBaseClass.LINK_THIS: // not as spec
                    childPrim = m_host;
                    break;
                default:
                    childPrim = parentSOG.GetLinkNumPart(linknum);
                    break;
            }

            if (linknum == ScriptBaseClass.LINK_ROOT)
            {
                List<ScenePresence> avs = parentSOG.GetSittingAvatars();
                foreach (ScenePresence av in avs)
                    av.StandUp();

                List<SceneObjectPart> parts = new(parentSOG.Parts);
                parts.Remove(parentSOG.RootPart);
                if (parts.Count > 0)
                {
                    try
                    {
                        foreach (SceneObjectPart part in parts)
                        {
                            parentSOG.DelinkFromGroup(part.LocalId, true);
                        }
                    }
                    finally { }
                 }

                parentSOG.HasGroupChanged = true;
                parentSOG.ScheduleGroupForFullUpdate();
                parentSOG.TriggerScriptChangedEvent(Changed.LINK);

                if (parts.Count > 0)
                {
                    SceneObjectPart newRoot = parts[0];
                    parts.Remove(newRoot);

                    try
                    {
                        foreach (SceneObjectPart part in parts)
                        {
                            part.ClearUpdateSchedule();
                            newRoot.ParentGroup.LinkToGroup(part.ParentGroup);
                        }
                    }
                    finally { }

                    newRoot.ParentGroup.HasGroupChanged = true;
                    newRoot.ParentGroup.ScheduleGroupForFullUpdate();
                }
            }
            else
            {
                if (childPrim == null)
                    return;

                List<ScenePresence> avs = parentSOG.GetSittingAvatars();
                foreach (ScenePresence av in avs)
                    av.StandUp();

                parentSOG.DelinkFromGroup(childPrim.LocalId, true);
            }
        }

        public void llBreakAllLinks()
        {

            TaskInventoryItem item = m_item;

            if ((item.PermsMask & ScriptBaseClass.PERMISSION_CHANGE_LINKS) == 0
                && !m_automaticLinkPermission)
            {
                Error("llBreakAllLinks","Script trying to link but PERMISSION_CHANGE_LINKS permission not set!");
                return;
            }
            BreakAllLinks();
        }

        public void BreakAllLinks()
        {
            SceneObjectGroup parentPrim = m_host.ParentGroup;
            if (parentPrim.AttachmentPoint != 0)
                return; // Fail silently if attached

            List<SceneObjectPart> parts = new(parentPrim.Parts);
            parts.Remove(parentPrim.RootPart);

            foreach (SceneObjectPart part in parts)
            {
                parentPrim.DelinkFromGroup(part.LocalId, true);
                parentPrim.TriggerScriptChangedEvent(Changed.LINK);
            }
            parentPrim.HasGroupChanged = true;
            parentPrim.ScheduleGroupForFullUpdate();
        }

        public LSL_Key llGetLinkKey(int linknum)
        {
            if (linknum < 0)
            {
                if (linknum == ScriptBaseClass.LINK_THIS)
                    return m_host.UUID.ToString();
                return ScriptBaseClass.NULL_KEY;
            }

            SceneObjectGroup sog = m_host.ParentGroup;
            if (linknum < 2)
                return sog.RootPart.UUID.ToString();

            SceneObjectPart part = sog.GetLinkNumPart(linknum);
            if (part is not null)
            {
                return part.UUID.ToString();
            }
            else
            {
                if (linknum > sog.PrimCount)
                {
                    linknum -= sog.PrimCount + 1;

                    List<ScenePresence> avatars = GetLinkAvatars(ScriptBaseClass.LINK_SET, sog);
                    if (avatars.Count > linknum)
                    {
                        return avatars[linknum].UUID.ToString();
                    }
                }
                return ScriptBaseClass.NULL_KEY;
            }
        }

        public LSL_Key llGetObjectLinkKey(LSL_Key objectid, int linknum)
        {
            if (!UUID.TryParse(objectid, out UUID oID) || oID.IsZero())
                return ScriptBaseClass.NULL_KEY;

            if (!World.TryGetSceneObjectPart(oID, out SceneObjectPart sop))
                return ScriptBaseClass.NULL_KEY;

            if (linknum < 0)
            {
                if (linknum == ScriptBaseClass.LINK_THIS)
                    return sop.UUID.ToString();
                return ScriptBaseClass.NULL_KEY;
            }

            SceneObjectGroup sog = sop.ParentGroup;

            if (linknum < 2)
                return sog.RootPart.UUID.ToString();

            SceneObjectPart part = sog.GetLinkNumPart(linknum);
            if (part is not null)
            {
                return part.UUID.ToString();
            }
            else
            {
                if (linknum > sog.PrimCount)
                {
                    linknum -= sog.PrimCount + 1;

                    List<ScenePresence> avatars = GetLinkAvatars(ScriptBaseClass.LINK_SET, sog);
                    if (avatars.Count > linknum)
                    {
                        return avatars[linknum].UUID.ToString();
                    }
                }
                return ScriptBaseClass.NULL_KEY;
            }
        }

        /// <summary>
        /// Returns the name of the child prim or seated avatar matching the
        /// specified link number.
        /// </summary>
        /// <param name="linknum">
        /// The number of a link in the linkset or a link-related constant.
        /// </param>
        /// <returns>
        /// The name determined to match the specified link number, NULL_KEY
        /// </returns>
     
        public LSL_String llGetLinkName(int linknum)
        {
            ISceneEntity entity = GetLinkEntity(m_host, linknum);
            return (entity is null) ? ScriptBaseClass.NULL_KEY : entity.Name;
        }

        public LSL_Integer llGetInventoryNumber(int type)
        {
            int count = 0;

            m_host.TaskInventory.LockItemsForRead(true);
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Type == type || type == -1)
                    count++;
            }

            m_host.TaskInventory.LockItemsForRead(false);
            return count;
        }

        public LSL_String llGetInventoryName(int type, int number)
        {
            ArrayList keys = new();

            m_host.TaskInventory.LockItemsForRead(true);
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Type == type || type == -1)
                {
                    keys.Add(inv.Value.Name);
                }
            }
            m_host.TaskInventory.LockItemsForRead(false);

            if (keys.Count == 0)
            {
                return String.Empty;
            }
            keys.Sort();
            if (keys.Count > number)
            {
                return (string)keys[number];
            }
            return String.Empty;
        }

        public LSL_Float llGetEnergy()
        {
            // TODO: figure out real energy value
            return 1.0f;
        }

        public void llGiveInventory(LSL_Key destination, LSL_String inventory)
        {
            if (!UUID.TryParse(destination, out UUID destId) || destId.IsZero())
            {
                Error("llGiveInventory", "Can't parse destination key '" + destination + "'");
                return;
            }

            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(inventory);
            if (item is null)
            {
                Error("llGiveInventory", "Can't find inventory object '" + inventory + "'");
                return;
            }

            // check if destination is an object
            if (World.TryGetSceneObjectPart(destId, out _))
            {
                // destination is an object
                World.MoveTaskInventoryItem(destId, m_host, item.ItemID);
                return;
            }

            ScenePresence presence = World.GetScenePresence(destId);
            if (presence is null)
            {
                UserAccount account = m_userAccountService.GetUserAccount(RegionScopeID, destId);
                if (account is null)
                {
                    GridUserInfo info = World.GridUserService.GetGridUserInfo(destId.ToString());
                    if(info is null || info.Online == false)
                    {
                        Error("llGiveInventory", "Can't find destination '" + destId.ToString() + "'");
                        return;
                    }
                }
            }

            // destination is an avatar
            InventoryItemBase agentItem = World.MoveTaskInventoryItem(destId, UUID.Zero, m_host, item.ItemID, out string message);
            if (agentItem is null)
            {
                llSay(0, message);
                return;
            }

            byte[] bucket = new byte[1];
            bucket[0] = (byte)item.Type;

            GridInstantMessage msg = new(World, m_host.OwnerID, m_host.Name, destId,
                    (byte)InstantMessageDialog.TaskInventoryOffered,
                    m_host.OwnerID.Equals(m_host.GroupID), "'"+item.Name+"'. ("+m_host.Name+" is located at "+
                    m_regionName + " "+ m_host.AbsolutePosition.ToString() + ")",
                    agentItem.ID, true, m_host.AbsolutePosition,
                    bucket, true);

            if (World.TryGetScenePresence(destId, out ScenePresence sp))
                sp.ControllingClient.SendInstantMessage(msg);
            else
                m_TransferModule?.SendInstantMessage(msg, delegate(bool success) {});

            //This delay should only occur when giving inventory to avatars.
            ScriptSleep(m_sleepMsOnGiveInventory);
        }

        [DebuggerNonUserCode]
        public void llRemoveInventory(string name)
        {

            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(name);

            if (item == null)
                return;

            if (item.ItemID == m_item.ItemID)
                throw new ScriptDeleteException();
            else
                m_host.Inventory.RemoveInventoryItem(item.ItemID);
        }

        public void llSetText(string text, LSL_Vector color, double alpha)
        {
            Vector3 av3 = Vector3.Clamp(color, 0.0f, 1.0f);
            m_host.SetText(text, av3, Utils.Clamp((float)alpha, 0.0f, 1.0f));
        }

        public LSL_Float llWater(LSL_Vector offset)
        {
            return World.RegionInfo.RegionSettings.WaterHeight;
        }

        public void llPassTouches(int pass)
        {
            m_host.PassTouches = pass != 0;
        }

        public LSL_List llGetVisualParams(string id, LSL_List visualparams)
        {
            if (visualparams.Length < 1)
                return new LSL_List();
            if (UUID.TryParse(id, out UUID agentid))
            {
                ScenePresence agent = World.GetScenePresence(agentid);
                if (agent is null)
                    return new LSL_List();

                LSL_List returns = new LSL_List();
                for (int i = 0; i < visualparams.Length; i++)
                {
                    int val = visualparams[i].ToString() switch
                    {
                        "33" or "height" => agent.Appearance.VisualParams[(int)AvatarAppearance.VPElement.SHAPE_HEIGHT],
                        "38" or "torso_length" => agent.Appearance.VisualParams[(int)AvatarAppearance.VPElement.SHAPE_TORSO_LENGTH],
                        "80" or "male" => agent.Appearance.VisualParams[(int)AvatarAppearance.VPElement.SHAPE_MALE],
                        "198" or "heel_height" => agent.Appearance.VisualParams[(int)AvatarAppearance.VPElement.SHOES_HEEL_HEIGHT],
                        "503" or "platform_height" => agent.Appearance.VisualParams[(int)AvatarAppearance.VPElement.SHOES_PLATFORM_HEIGHT],
                        "616" or "shoe_height" => agent.Appearance.VisualParams[(int)AvatarAppearance.VPElement.SHOES_SHOE_HEIGHT],
                        "675" or "hand_size" => agent.Appearance.VisualParams[(int)AvatarAppearance.VPElement.SHAPE_HAND_SIZE],
                        "682" or "head_size" => agent.Appearance.VisualParams[(int)AvatarAppearance.VPElement.SHAPE_HEAD_SIZE],
                        "692" or  "leg_length" => agent.Appearance.VisualParams[(int)AvatarAppearance.VPElement.SHAPE_LEG_LENGTH],
                        "693" or "arm_length" => agent.Appearance.VisualParams[(int)AvatarAppearance.VPElement.SHAPE_ARM_LENGTH],
                        "756" or "neck_length" => agent.Appearance.VisualParams[(int)AvatarAppearance.VPElement.SHAPE_NECK_LENGTH],
                        "814" or "waist_height" => agent.Appearance.VisualParams[(int)AvatarAppearance.VPElement.PANTS_WAIST_HEIGHT],
                        "842" or "hip_length" => agent.Appearance.VisualParams[(int)AvatarAppearance.VPElement.SHAPE_HIP_LENGTH],
                        "11001" or "hover" => agent.Appearance.VisualParams[(int)AvatarAppearance.VPElement.SHAPE_HOVER],
                        _ => 9999,
                    };
                    if (val == 9999)
                        returns.Add(LSL_String.Empty);
                    else
                    {
                        float fval = MathF.Round(val * 0.0039215686f, 6); //(1/255)
                        returns.Add(fval.ToString());
                    }
                }
                if (returns.Length > 0)
                    return returns;
            }
            return new LSL_List();
        }

        public LSL_Key llRequestAgentData(string id, int data)
        {
            if(data < 1 || data > ScriptBaseClass.DATA_PAYINFO)
                return string.Empty;

            if (UUID.TryParse(id, out UUID uuid) && uuid.IsNotZero())
            {
                //pre process fast local avatars
                switch(data)
                {
                    case ScriptBaseClass.DATA_RATING:
                    case ScriptBaseClass.DATA_NAME: // DATA_NAME (First Last)
                    case ScriptBaseClass.DATA_ONLINE:
                        World.TryGetScenePresence(uuid, out ScenePresence sp);
                        if (sp != null)
                        {
                            string reply = data switch
                            {
                                ScriptBaseClass.DATA_RATING => "0,0,0,0,0,0",
                                ScriptBaseClass.DATA_NAME => sp.Firstname + " " + sp.Lastname,
                                _ => "1"
                            };
                            string ftid = m_AsyncCommands.DataserverPlugin.RequestWithImediatePost(m_host.LocalId,
                                                    m_item.ItemID, reply);
                            ScriptSleep(m_sleepMsOnRequestAgentData);
                            return ftid;
                        }
                        break;
                    case ScriptBaseClass.DATA_BORN: // DATA_BORN (YYYY-MM-DD)
                    case 7: // DATA_USERLEVEL (integer).  This is not available in LL and so has no constant.
                    case ScriptBaseClass.DATA_PAYINFO: // DATA_PAYINFO (0|1|2|3)
                        break;
                    default:
                        return string.Empty; // Raise no event
                }

                void act(string eventID)
                {
                    IUserManagement umm = World.RequestModuleInterface<IUserManagement>();
                    if(umm == null)
                        return;

                    UserData udt = umm.GetUserData(uuid);
                    if (udt == null || udt.IsUnknownUser)
                        return;

                    string reply = null;
                    switch(data)
                    {
                        case ScriptBaseClass.DATA_ONLINE:
                            if (!m_PresenceInfoCache.TryGetValue(uuid, out PresenceInfo pinfo))
                            {
                                PresenceInfo[] pinfos = World.PresenceService.GetAgents([uuid.ToString()]);
                                if (pinfos != null && pinfos.Length > 0)
                                {
                                    foreach (PresenceInfo p in pinfos)
                                    {
                                        if (!p.RegionID.IsZero())
                                        {
                                            pinfo = p;
                                        }
                                    }
                                }
                                m_PresenceInfoCache.AddOrUpdate(uuid, pinfo, m_llRequestAgentDataCacheTimeout);
                            }
                            reply = pinfo == null ? "0" : "1";
                            break;
                        case ScriptBaseClass.DATA_NAME:
                            reply = udt.FirstName + " " + udt.LastName;
                            break;
                        case ScriptBaseClass.DATA_RATING:
                            reply = "0,0,0,0,0,0";
                            break;
                        case 7:
                        case ScriptBaseClass.DATA_BORN:
                        case ScriptBaseClass.DATA_PAYINFO:
                            if (udt.IsLocal)
                            {
                                UserAccount account = m_userAccountService.GetUserAccount(RegionScopeID, uuid);
                                if (account is not null)
                                {
                                    reply = data switch
                                    { 
                                        7 => account.UserLevel.ToString(),
                                        ScriptBaseClass.DATA_BORN => Util.ToDateTime(account.Created).ToString("yyyy-MM-dd"),
                                        _ => ((account.UserFlags >> 2) & 0x03).ToString()
                                    };
                                }
                            }
                            else
                            {
                                if (data == 7)
                                    reply = "0";
                            }
                            break;
                        default:
                            break;
                    }
                    if(reply != null)
                        m_AsyncCommands.DataserverPlugin.DataserverReply(eventID, reply);
                }

                UUID tid = m_AsyncCommands.DataserverPlugin.RegisterRequest(m_host.LocalId,
                                                 m_item.ItemID, act);

                ScriptSleep(m_sleepMsOnRequestAgentData);
                return tid.ToString();
            }
            else
            {
                Error("llRequestAgentData","Invalid UUID passed to llRequestAgentData.");
            }
            return string.Empty;
        }

        //bad if lm is HG
        public LSL_Key llRequestInventoryData(LSL_String name)
        {
            void act(string eventID)
            {
                string reply = string.Empty;
                foreach (TaskInventoryItem item in m_host.Inventory.GetInventoryItems())
                {
                    if (item.Type == 3 && item.Name == name)
                    {
                        AssetBase a = World.AssetService.Get(item.AssetID.ToString());
                        if (a is not null)
                        {
                            AssetLandmark lm = new(a);
                            if (lm is not null)
                            {
                                double rx = (lm.RegionHandle >> 32) - (double)World.RegionInfo.WorldLocX + (double)lm.Position.X;
                                double ry = (lm.RegionHandle & 0xffffffff) - (double)World.RegionInfo.WorldLocY + (double)lm.Position.Y;
                                LSL_Vector region = new(rx, ry, lm.Position.Z);
                                reply = region.ToString();
                            }
                        }
                        break;
                    }
                }
                m_AsyncCommands.DataserverPlugin.DataserverReply(eventID, reply);
            }

            UUID tid = m_AsyncCommands.DataserverPlugin.RegisterRequest(m_host.LocalId,
                                                         m_item.ItemID, act);

            ScriptSleep(m_sleepMsOnRequestInventoryData);
            return tid.ToString();
        }

        public void llSetDamage(double damage)
        {
            m_host.ParentGroup.Damage = (float)damage;
        }

        public LSL_Float llGetHealth(LSL_String key)
        {
            if (UUID.TryParse(key, out UUID agent))
            {
                ScenePresence user = World.GetScenePresence(agent);
                if (user is not null)
                    return user.Health;
            }
            return new LSL_Float(-1.0);
        }

        public void llTeleportAgentHome(string agent)
        {
            if (UUID.TryParse(agent, out UUID agentId) && agentId.IsNotZero())
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence == null || presence.IsDeleted || presence.IsChildAgent || presence.IsNPC || presence.IsInTransit)
                    return;

                // agent must not be a god
                if (presence.GodController.UserLevel >= 200)
                    return;

                // agent must be over the owners land
                if (m_host.OwnerID.Equals(World.LandChannel.GetLandObject(presence.AbsolutePosition).LandData.OwnerID))
                {
                    World.TeleportClientHome(agentId, presence.ControllingClient);
                }
            }

            ScriptSleep(m_sleepMsOnSetDamage);
        }

        public void llTeleportAgent(string agent, string destination, LSL_Vector targetPos, LSL_Vector targetLookAt)
        {
            // If attached using llAttachToAvatarTemp, cowardly refuse
            if (m_host.ParentGroup.AttachmentPoint != 0 && m_host.ParentGroup.FromItemID.IsZero())
                return;

            if (UUID.TryParse(agent, out UUID agentId) && agentId.IsNotZero())
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence == null || presence.IsDeleted || presence.IsChildAgent || presence.IsNPC || presence.IsSatOnObject || presence.IsInTransit)
                    return;

                if (m_item.PermsGranter.Equals(agentId))
                {
                    if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TELEPORT) != 0)
                    {
                        DoLLTeleport(presence, destination, targetPos, targetLookAt);
                        return;
                    }
                }

                // special opensim legacy extra permissions, possible to remove
                // agent must be wearing the object
                if (m_host.ParentGroup.AttachmentPoint != 0 && m_host.OwnerID.Equals(presence.UUID))
                {
                    DoLLTeleport(presence, destination, targetPos, targetLookAt);
                    return;
                }

                // agent must not be a god
                if (presence.IsViewerUIGod)
                    return;

                // agent must be over the owners land
                ILandObject agentLand = World.LandChannel.GetLandObject(presence.AbsolutePosition);
                ILandObject objectLand = World.LandChannel.GetLandObject(m_host.AbsolutePosition);
                if (m_host.OwnerID.Equals(objectLand.LandData.OwnerID) && m_host.OwnerID.Equals(agentLand.LandData.OwnerID))
                {
                    DoLLTeleport(presence, destination, targetPos, targetLookAt);
                }
            }
        }

        public void llTeleportAgentGlobalCoords(string agent, LSL_Vector global_coords, LSL_Vector targetPos, LSL_Vector targetLookAt)
        {
            // If attached using llAttachToAvatarTemp, cowardly refuse
            if (m_host.ParentGroup.AttachmentPoint != 0 && m_host.ParentGroup.FromItemID.IsZero())
                return;

            if (UUID.TryParse(agent, out UUID agentId) && agentId.IsNotZero())
            {
                // This function is owner only!
                if (m_host.OwnerID.NotEqual(agentId))
                    return;

                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence == null || presence.IsDeleted || presence.IsChildAgent || presence.IsNPC || presence.IsSatOnObject || presence.IsInTransit)
                    return;

                if (m_item.PermsGranter.Equals(agentId))
                {
                    if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TELEPORT) != 0)
                    {
                        ulong regionHandle = Util.RegionWorldLocToHandle((uint)global_coords.x, (uint)global_coords.y);
                        World.RequestTeleportLocation(presence.ControllingClient, regionHandle, targetPos, targetLookAt, (uint)TeleportFlags.ViaLocation);
                    }
                }
            }
        }

        private void DoLLTeleport(ScenePresence sp, string destination, Vector3 targetPos, Vector3 targetLookAt)
        {
            UUID assetID = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, destination);

            // The destination is not an asset ID and also doesn't name a landmark.
            // Use it as a sim name
            if (assetID.IsZero())
            {
                if(string.IsNullOrEmpty(destination))
                    World.RequestTeleportLocation(sp.ControllingClient, m_regionName, targetPos, targetLookAt, (uint)TeleportFlags.ViaLocation);
                else
                    World.RequestTeleportLocation(sp.ControllingClient, destination, targetPos, targetLookAt, (uint)TeleportFlags.ViaLocation);
                return;
            }

            AssetBase lma = World.AssetService.Get(assetID.ToString());
            if (lma == null || lma.Data == null || lma.Data.Length == 0)
                return;

            if (lma.Type != (sbyte)AssetType.Landmark)
                return;

            AssetLandmark lm = new(lma);

            World.RequestTeleportLandmark(sp.ControllingClient, lm, targetLookAt);
        }

        public void llTextBox(string agent, string message, int chatChannel)
        {
            IDialogModule dm = World.RequestModuleInterface<IDialogModule>();

            if (dm == null)
                return;

            if (!UUID.TryParse(agent, out UUID av) || av.IsZero())
            {
                Error("llTextBox", "First parameter must be a valid agent key");
                return;
            }

            if (message.Length == 0)
            {
                Error("llTextBox", "Empty message");
            }
            else if (Encoding.UTF8.GetByteCount(message) > 512)
            {
                Error("llTextBox", "Message longer than 512 bytes");
            }
            else if(m_host.GetOwnerName(out string fname, out string lname))
            {
                dm.SendTextBoxToUser(av, message, chatChannel, m_host.Name, m_host.UUID, fname, lname, m_host.OwnerID);
                ScriptSleep(m_sleepMsOnTextBox);
            }
        }

        public void llModifyLand(int action, int brush)
        {
            ITerrainModule tm = m_ScriptEngine.World.RequestModuleInterface<ITerrainModule>();
            tm?.ModifyTerrain(m_host.OwnerID, m_host.AbsolutePosition, (byte) brush, (byte) action);
        }

        public void llCollisionSound(LSL_String impact_sound, LSL_Float impact_volume)
        {

            if(String.IsNullOrEmpty(impact_sound.m_string))
            {
                m_host.CollisionSoundVolume = (float)impact_volume;
                m_host.CollisionSound = m_host.invalidCollisionSoundUUID;
                m_host.CollisionSoundType = -1; // disable all sounds
                m_host.aggregateScriptEvents();
                return;
            }

            // TODO: Parameter check logic required.
            UUID soundId = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, impact_sound, AssetType.Sound);
            if(soundId.IsZero())
                m_host.CollisionSoundType = -1;
            else
            {
                m_host.CollisionSound = soundId;
                m_host.CollisionSoundVolume = (float)impact_volume;
                m_host.CollisionSoundType = 1;
            }

            m_host.aggregateScriptEvents();
        }

        public LSL_String llGetAnimation(LSL_Key id)
        {
            // This should only return a value if the avatar is in the same region
            if(!UUID.TryParse(id, out UUID avatar) || avatar.IsZero())
                return "";

            ScenePresence presence = World.GetScenePresence(avatar);
            if (presence == null || presence.IsChildAgent || presence.Animator == null)
                return string.Empty;

            //if (presence.SitGround)
            //    return "Sitting on Ground";
            //if (presence.ParentID != 0 || presence.ParentUUID != UUID.Zero)
            //    return "Sitting";
            string movementAnimation = presence.Animator.CurrentMovementAnimation;
            if (MovementAnimationsForLSL.TryGetValue(movementAnimation, out string lslMovementAnimation))
                return lslMovementAnimation;

            return string.Empty;
        }

        public void llMessageLinked(int linknumber, int num, string msg, string id)
        {

            List<SceneObjectPart> parts = GetLinkParts(linknumber);

            UUID partItemID;
            foreach (SceneObjectPart part in parts)
            {
                foreach (TaskInventoryItem item in part.Inventory.GetInventoryItems())
                {
                    if (item.Type == ScriptBaseClass.INVENTORY_SCRIPT)
                    {
                        partItemID = item.ItemID;
                        int linkNumber = m_host.LinkNum;
                        if (m_host.ParentGroup.PrimCount == 1)
                            linkNumber = 0;

                        object[] resobj = new object[]
                                  {
                                      new LSL_Integer(linkNumber), new LSL_Integer(num), new LSL_String(msg), new LSL_String(id)
                                  };

                        m_ScriptEngine.PostScriptEvent(partItemID,
                                new EventParams("link_message",
                                resobj, Array.Empty<DetectParams>()));
                    }
                }
            }
        }

        public void llPushObject(string target, LSL_Vector impulse, LSL_Vector ang_impulse, int local)
        {
            bool pushrestricted = World.RegionInfo.RegionSettings.RestrictPushing;
            bool pushAllowed = false;

            bool pusheeIsAvatar = false;

            if (!UUID.TryParse(target, out UUID targetID) || targetID.IsZero())
                return;

            ScenePresence pusheeav = null;
            Vector3 PusheePos = Vector3.Zero;
            SceneObjectPart pusheeob = null;

            ScenePresence avatar = World.GetScenePresence(targetID);
            if (avatar != null)
            {
                pusheeIsAvatar = true;

                // Pushee doesn't have a physics actor
                if (avatar.PhysicsActor == null)
                    return;

                // Pushee is in GodMode this pushing object isn't owned by them
                if (avatar.IsViewerUIGod && m_host.OwnerID != targetID)
                    return;

                pusheeav = avatar;

                // Find pushee position
                // Pushee Linked?
                SceneObjectPart sitPart = pusheeav.ParentPart;
                if (sitPart != null)
                    PusheePos = sitPart.AbsolutePosition;
                else
                    PusheePos = pusheeav.AbsolutePosition;
            }

            if (!pusheeIsAvatar)
            {
                // not an avatar so push is not affected by parcel flags
                pusheeob = World.GetSceneObjectPart((UUID)target);

                // We can't find object
                if (pusheeob == null)
                    return;

                // Object not pushable.  Not an attachment and has no physics component
                if (!pusheeob.ParentGroup.IsAttachment && pusheeob.PhysActor == null)
                    return;

                PusheePos = pusheeob.AbsolutePosition;
                pushAllowed = true;
            }
            else
            {
                if (pushrestricted)
                {
                    ILandObject targetlandObj = World.LandChannel.GetLandObject(PusheePos);

                    // We didn't find the parcel but region is push restricted so assume it is NOT ok
                    if (targetlandObj == null)
                        return;

                    // Need provisions for Group Owned here
                    if (m_host.OwnerID.Equals(targetlandObj.LandData.OwnerID) ||
                        targetlandObj.LandData.IsGroupOwned || m_host.OwnerID.Equals(targetID))
                    {
                        pushAllowed = true;
                    }
                }
                else
                {
                    ILandObject targetlandObj = World.LandChannel.GetLandObject(PusheePos);
                    if (targetlandObj == null)
                    {
                        // We didn't find the parcel but region isn't push restricted so assume it's ok
                        pushAllowed = true;
                    }
                    else
                    {
                        // Parcel push restriction
                        if ((targetlandObj.LandData.Flags & (uint)ParcelFlags.RestrictPushObject) == (uint)ParcelFlags.RestrictPushObject)
                        {
                            // Need provisions for Group Owned here
                            if (m_host.OwnerID.Equals(targetlandObj.LandData.OwnerID) ||
                                targetlandObj.LandData.IsGroupOwned ||
                                m_host.OwnerID.Equals(targetID))
                            {
                                pushAllowed = true;
                            }

                            //ParcelFlags.RestrictPushObject
                            //pushAllowed = true;
                        }
                        else
                        {
                            // Parcel isn't push restricted
                            pushAllowed = true;
                        }
                    }
                }
            }

            if (pushAllowed)
            {
                float distance = (PusheePos - m_host.AbsolutePosition).Length();
                //float distance_term = distance * distance * distance; // Script Energy
                // use total object mass and not part
                //float pusher_mass = m_host.ParentGroup.GetMass();

                float PUSH_ATTENUATION_DISTANCE = 17f;
                float PUSH_ATTENUATION_SCALE = 5f;
                float distance_attenuation = 1f;
                if (distance > PUSH_ATTENUATION_DISTANCE)
                {
                    float normalized_units = 1f + (distance - PUSH_ATTENUATION_DISTANCE) / PUSH_ATTENUATION_SCALE;
                    distance_attenuation = 1f / normalized_units;
                }

                Vector3 applied_linear_impulse = impulse;
                {
                    //float impulse_length = applied_linear_impulse.Length();
                    //float desired_energy = impulse_length * pusher_mass;

                    float scaling_factor = 1f;
                    scaling_factor *= distance_attenuation;
                    applied_linear_impulse *= scaling_factor;
                }

                if (pusheeIsAvatar)
                {
                    if (pusheeav is not null)
                    {
                        PhysicsActor pa = pusheeav.PhysicsActor;
                        if (pa is not null)
                        {
                            if (local != 0)
                                applied_linear_impulse *= pusheeav.GetWorldRotation();

                            pa.AddForce(applied_linear_impulse, true);
                        }
                    }
                }
                else
                {
                    if (pusheeob is not null)
                    {
                        if (pusheeob.PhysActor is not null)
                            pusheeob.ApplyImpulse(applied_linear_impulse, local != 0);
                    }
                }
            }
        }

        public void llPassCollisions(int pass)
        {
            m_host.PassCollisions = pass == 1;
        }

        public LSL_String llGetScriptName()
        {
            return m_item.Name ?? string.Empty;
        }

        public LSL_Integer llGetLinkNumberOfSides(int link)
        {

            SceneObjectPart linkedPart;

            if (link == ScriptBaseClass.LINK_ROOT)
                linkedPart = m_host.ParentGroup.RootPart;
            else if (link == ScriptBaseClass.LINK_THIS)
                linkedPart = m_host;
            else
                linkedPart = m_host.ParentGroup.GetLinkNumPart(link);

            return GetNumberOfSides(linkedPart);
        }

        public LSL_Integer llGetNumberOfSides()
        {
            return m_host.GetNumberOfSides();
        }

        protected static int GetNumberOfSides(SceneObjectPart part)
        {
            return part.GetNumberOfSides();
        }

        // Xantor 29/apr/2008
        // Returns rotation described by rotating angle radians about axis.
        // q = cos(a/2) + i (x * sin(a/2)) + j (y * sin(a/2)) + k (z * sin(a/2))
        public LSL_Rotation llAxisAngle2Rot(LSL_Vector axis, double angle)
        {
            double x, y, z, s, t;

            s = Math.Cos(angle * 0.5);
            t = Math.Sin(angle * 0.5); // temp value to avoid 2 more sin() calcs
            axis =  LSL_Vector.Norm(axis);
            x = axis.x * t;
            y = axis.y * t;
            z = axis.z * t;

            return new LSL_Rotation(x,y,z,s);
        }

        /// <summary>
        /// Returns the axis of rotation for a quaternion
        /// </summary>
        /// <returns></returns>
        /// <param name='rot'></param>
        public LSL_Vector llRot2Axis(LSL_Rotation rot)
        {
            rot.Normalize();

            double s = Math.Sqrt(1 - rot.s * rot.s);
            if (s < 1e-8)
                return new LSL_Vector(0, 0, 0);

            double invS = 1.0 / s;
            if (rot.s < 0)
                invS = -invS;
            return new LSL_Vector(rot.x * invS, rot.y * invS, rot.z * invS);
        }


        // Returns the angle of a quaternion (see llRot2Axis for the axis)
        public LSL_Float llRot2Angle(LSL_Rotation rot)
        {
            rot.Normalize();

            double angle = 2 * Math.Acos(rot.s);
            if (angle > Math.PI)
                angle = 2 * Math.PI - angle;

            return angle;
        }

        public LSL_Float llAcos(LSL_Float val)
        {
            return Math.Acos(val);
        }

        public LSL_Float llAsin(LSL_Float val)
        {
            return Math.Asin(val);
        }

        // jcochran 5/jan/2012
        public LSL_Float llAngleBetween(LSL_Rotation a, LSL_Rotation b)
        {
            double aa = (a.x * a.x + a.y * a.y + a.z * a.z + a.s * a.s);
            double bb = (b.x * b.x + b.y * b.y + b.z * b.z + b.s * b.s);
            double aa_bb = aa * bb;
            if (aa_bb == 0) return 0.0;
            double ab = (a.x * b.x + a.y * b.y + a.z * b.z + a.s * b.s);
            double quotient = (ab * ab) / aa_bb;
            if (quotient >= 1.0) return 0.0;
            return Math.Acos(2 * quotient - 1);
        }

        public LSL_Key llGetInventoryKey(string name)
        {
            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(name);
            if (item is null)
                return ScriptBaseClass.NULL_KEY;

            if ((item.CurrentPermissions
                 & (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
                    == (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
            {
                return item.AssetID.ToString();
            }

            return ScriptBaseClass.NULL_KEY;
        }

        public void llAllowInventoryDrop(LSL_Integer add)
        {
            m_host.ParentGroup.RootPart.AllowedDrop = add != 0;
            m_host.ParentGroup.RootPart.aggregateScriptEvents();
        }

        public LSL_Vector llGetTextureOffset(int face)
        {
            return GetTextureOffset(m_host, face);
        }

        protected static LSL_Vector GetTextureOffset(SceneObjectPart part, int face)
        {
            if (face == ScriptBaseClass.ALL_SIDES)
                face = 0;

            if (face >= 0 && face < GetNumberOfSides(part))
            {
                Primitive.TextureEntryFace teface = part.Shape.Textures.GetFace((uint)face);
                return new LSL_Vector (teface.OffsetU, teface.OffsetV, 0.0);
            }
            return LSL_Vector.Zero;
        }

        public LSL_Vector llGetTextureScale(int side)
        {
            Primitive.TextureEntryFace teface;
            if (side == ScriptBaseClass.ALL_SIDES)
                teface = m_host.Shape.Textures.GetFace(0);
            else
                teface = m_host.Shape.Textures.GetFace((uint)side);
            return new LSL_Vector(teface.RepeatU, teface.RepeatV, 0.0);
        }

        public LSL_Float llGetTextureRot(int face)
        {
            return GetTextureRot(m_host, face);
        }

        protected static LSL_Float GetTextureRot(SceneObjectPart part, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face == ScriptBaseClass.ALL_SIDES)
                face = 0;

            if (face >= 0 && face < GetNumberOfSides(part))
                return tex.GetFace((uint)face).Rotation;

            return 0.0;
        }

        public LSL_Integer llSubStringIndex(string source, string pattern)
        {
            if (string.IsNullOrEmpty(source))
                return -1;
            if (string.IsNullOrEmpty(pattern))
                return 0;
            return source.IndexOf(pattern);
        }

        public LSL_Key llGetOwnerKey(string id)
        {
            if (UUID.TryParse(id, out UUID key))
            {
                if(key.IsZero())
                    return id;

                SceneObjectPart obj = World.GetSceneObjectPart(key);
                return (obj == null) ? id : obj.OwnerID.ToString();
            }
            else
            {
                return ScriptBaseClass.NULL_KEY;
            }
        }

        public LSL_Vector llGetCenterOfMass()
        {
            return new LSL_Vector(m_host.GetCenterOfMass());
        }

        public LSL_List llListSort(LSL_List src, int stride, int ascending)
        {
            return src.Sort(stride, ascending == 1);
        }

        public LSL_List llListSortStrided(LSL_List src, int stride, int stride_index, int ascending)
        {
            return src.Sort(stride, stride_index, ascending == 1);
        }

        public LSL_Integer llGetListLength(LSL_List src)
        {
            return src.Length;
        }

        public LSL_Integer llList2Integer(LSL_List src, int index)
        {
            if (index < 0)
                index = src.Length + index;

            if (index >= src.Length || index < 0)
                return 0;

            object item = src.Data[index];

            // Vectors & Rotations always return zero in SL, but
            //  keys don't always return zero, it seems to be a bit complex.
            if (item is LSL_Vector || item is LSL_Rotation)
                return 0;

            try
            {
                if (item is LSL_Integer LSL_Integeritem)
                    return LSL_Integeritem;
                if (item is int LSL_Intitem)
                    return new LSL_Integer(LSL_Intitem);
                if (item is LSL_Float LSL_Floatitem)
                    return new LSL_Integer(LSL_Floatitem.value);
                return new LSL_Integer(item.ToString());
            }
            catch (FormatException)
            {
                return 0;
            }
        }

        public LSL_Float llList2Float(LSL_List src, int index)
        {
            if (index < 0)
                index = src.Length + index;

            if (index >= src.Length || index < 0)
                return 0;

            object item = src.Data[index];

            // Vectors & Rotations always return zero in SL
            if(item is LSL_Vector || item is LSL_Rotation)
                return 0;

            // valid keys seem to get parsed as integers then converted to floats
            if (item is LSL_Key lslk)
            {
                if(UUID.TryParse(lslk.m_string, out UUID _))
                    return Convert.ToDouble(new LSL_Integer(lslk.m_string).value);
                // we can't do this because a string is also a LSL_Key for now :(
                //else
                //   return 0;
            }

            try
            {
                if (item is LSL_Float floatitem)
                    return floatitem;
                if (item is LSL_Integer intitem)
                    return new LSL_Float(intitem.value);
                if (item is int LSL_Intitem)
                    return new LSL_Float(LSL_Intitem);
                if (item is LSL_String lstringitem)
                {
                    Match m = Regex.Match(lstringitem.m_string, "^\\s*(-?\\+?[,0-9]+\\.?[0-9]*)");
                    if (m != Match.Empty)
                    {
                         if (Double.TryParse(m.Value, out double d))
                            return d;
                    }
                    return 0.0;
                }
                return Convert.ToDouble(item);
            }
            catch (FormatException)
            {
                return 0.0;
            }
        }

        public LSL_String llList2String(LSL_List src, int index)
        {
            if (index < 0)
                index = src.Length + index;

            if (index >= src.Length || index < 0)
                return String.Empty;

            return src.Data[index].ToString();
        }

        public LSL_Key llList2Key(LSL_List src, int index)
        {
            if (index < 0)
                index = src.Length + index;

            if (index >= src.Length || index < 0)
                return String.Empty;

            object item = src.Data[index];

            // SL spits out an empty string for types other than key & string
            // At the time of patching, LSL_Key is currently LSL_String,
            // so the OR check may be a little redundant, but it's being done
            // for completion and should LSL_Key ever be implemented
            // as it's own struct
            // NOTE: 3rd case is needed because a NULL_KEY comes through as
            // type 'obj' and wrongly returns ""
            if (!(item is LSL_String ||
                       item is LSL_Key ||
                       item.ToString().Equals("00000000-0000-0000-0000-000000000000")))
            {
                return String.Empty;
            }

            return item.ToString();
        }

        public LSL_Vector llList2Vector(LSL_List src, int index)
        {
            if (index < 0)
                index = src.Length + index;

            if (index >= src.Length || index < 0)
                return LSL_Vector.Zero;

            object item = src.Data[index];

            if(item is LSL_Vector vec)
                return vec;
            if (item is LSL_String lsv)
                return new LSL_Vector(lsv);
            if (item is string sv) // xengine sees string
                return new LSL_Vector(sv);

            return LSL_Vector.Zero;
        }

        public LSL_Rotation llList2Rot(LSL_List src, int index)
        {
            if (index < 0)
                index = src.Length + index;

            if (index >= src.Length || index < 0)
                return LSL_Rotation.Identity;

            object item = src.Data[index];

            if (item is LSL_Rotation rot)
                return rot;
            if (item is LSL_String lls)
                return new LSL_Rotation(lls);
            if (item is string ls) // xengine sees string)
                return new LSL_Rotation(ls);

            return LSL_Rotation.Identity;
        }

        public LSL_List llList2List(LSL_List src, int start, int end)
        {
            return src.GetSublist(start, end);
        }

        public LSL_List llDeleteSubList(LSL_List src, int start, int end)
        {
            return src.DeleteSublist(start, end);
        }

        public LSL_Integer llGetListEntryType(LSL_List src, int index)
        {
            if (index < 0)
            {
                index = src.Length + index;
                if (index < 0)
                    return 0;
            }
            else if (index >= src.Length)
                return 0;

            object o = src.Data[index];
            if (o is null)
                return 0;
            if (o is LSL_Integer || o is Int32)
                return 1;
            if (o is LSL_Float || o is Single || o is Double)
                return 2;
            if (o is LSL_String || o is String)
                return UUID.TryParse(o.ToString(), out UUID _) ? 4 : 3;
            if (o is LSL_Key)
                return 4;
            if (o is LSL_Vector)
                return 5;
            if (o is LSL_Rotation)
                return 6;
            if (o is LSL_List)
                return 7;
            return 0;
        }

        /// <summary>
        /// Process the supplied list and return the
        /// content of the list formatted as a comma
        /// separated list. There is a space after
        /// each comma.
        /// </summary>
        public LSL_String llList2CSV(LSL_List src)
        {
            return string.Join(", ",
                    (new List<object>(src.Data)).ConvertAll<string>(o =>
                    {
                        return o.ToString();
                    }).ToArray());
        }

        /// <summary>
        /// The supplied string is scanned for commas
        /// and converted into a list. Commas are only
        /// effective if they are encountered outside
        /// of '<' '>' delimiters. Any whitespace
        /// before or after an element is trimmed.
        /// </summary>

        public LSL_List llCSV2List(string src)
        {
            LSL_List result = new();
            int parens = 0;
            int start  = 0;
            int length = 0;
            
            ReadOnlySpan<char> s = src.AsSpan();
            for (int i = 0; i < s.Length; i++)
            {
                switch (s[i])
                {
                    case '<':
                        parens++;
                        length++;
                        break;
                    case '>':
                        if (parens > 0)
                            parens--;
                        length++;
                        break;
                    case ',':
                        if (parens == 0)
                        {
                            result.Add(new LSL_String(src.Substring(start,length).Trim()));
                            start += length+1;
                            length = 0;
                        }
                        else
                        {
                            length++;
                        }
                        break;
                    default:
                        length++;
                        break;
                }
            }

            result.Add(new LSL_String(src.Substring(start,length).Trim()));

            return result;
        }

        ///  <summary>
        ///  Randomizes the list, be arbitrarily reordering
        ///  sublists of stride elements. As the stride approaches
        ///  the size of the list, the options become very
        ///  limited.
        ///  </summary>
        ///  <remarks>
        ///  This could take a while for very large list
        ///  sizes.
        ///  </remarks>

        public LSL_List llListRandomize(LSL_List src, int stride)
        {
            LSL_List result;

            int   chunkk;
            int[] chunks;

            if (stride <= 0)
            {
                stride = 1;
            }

            // Stride MUST be a factor of the list length
            // If not, then return the src list. This also
            // traps those cases where stride > length.

            if (src.Length != stride && src.Length % stride == 0)
            {
                chunkk = src.Length/stride;

                chunks = new int[chunkk];

                for (int i = 0; i < chunkk; i++)
                {
                    chunks[i] = i;
                }

                // Knuth shuffle the chunkk index
                for (int i = chunkk - 1; i > 0; i--)
                {
                    // Elect an unrandomized chunk to swap
                    int index = Random.Shared.Next(i + 1);

                    // and swap position with first unrandomized chunk
                    int tmp = chunks[i];
                    chunks[i] = chunks[index];
                    chunks[index] = tmp;
                }

                // Construct the randomized list

                result = new LSL_List();

                for (int i = 0; i < chunkk; i++)
                {
                    for (int j = 0; j < stride; j++)
                    {
                        result.Add(src.Data[chunks[i] * stride + j]);
                    }
                }
            }
            else
            {
                object[] array = new object[src.Length];
                Array.Copy(src.Data, 0, array, 0, src.Length);
                result = new LSL_List(array);
            }

            return result;
        }

        public LSL_List llList2ListStrided(LSL_List src, int start, int end, int stride)
        {
            if (start < 0)
            {
                start += src.Length;
                if (start < 0)
                    start = 0;
            }
            if (end < 0)
            {
                end += src.Length;
                if (end < 0)
                    end = 0;
            }

            if (start > end)
            {
                start = 0;
                end = src.Length - 1;
            }
            else
            {
                if (start >= src.Length)
                    return new LSL_List();
                if (end >= src.Length)
                    end = src.Length - 1;
            }

            if (stride < 1)
                stride = 1;

            int size;
            if (stride > 1)
            {
                if (start > 0)
                {
                    int sst = start / stride;
                    sst *= stride;
                    if (sst != start)
                        start = sst + stride;

                    if (start > end)
                        return new LSL_List();
                }
                size = end - start + 1;
                int sz = size / stride;
                if (sz * stride < size)
                    sz++;
                size = sz;
            }
            else
                size = end - start + 1;

            object[] res = new object[size];
            int j = 0;
            for (int i = start; i <= end; i += stride, j++)
                res[j] = src.Data[i];

            return new LSL_List(res);
        }

        public LSL_List llList2ListSlice(LSL_List src, int start, int end, int stride, int stride_index)
        {
            if (start < 0)
            {
                start += src.Length;
                if (start < 0)
                    start = 0;
            }
            if (end < 0)
            {
                end += src.Length;
                if (end < 0)
                    end = 0;
            }

            if (start > end)
            {
                start = 0;
                end = src.Length - 1;
            }
            else
            {
                if (start >= src.Length)
                    return new LSL_List();
                if (end >= src.Length)
                    end = src.Length - 1;
            }

            if (stride < 1)
                stride = 1;

            if (stride_index < 0)
            {
                stride_index += stride;
                if (stride_index < 0)
                    return new LSL_List();
            }
            else if (stride_index >= stride)
                return new LSL_List();

            int size;
            if (stride > 1)
            {
                if (start > 0)
                {
                    int sst = start / stride;
                    sst *= stride;
                    if (sst != start)
                        start = sst + stride;

                    if (start > end)
                        return new LSL_List();
                }
                start += stride_index;
                size = end - start + 1;
                int sz = size / stride;
                if (sz * stride < size)
                    sz++;
                size = sz;
            }
            else
                size = end - start + 1;

            object[] res = new object[size];
            int j = 0;
            for (int i = start; i <= end; i += stride, j++)
                res[j] = src.Data[i];

            //m_log.Debug($" test {size} {j}");
            return new LSL_List(res);
        }

        public LSL_Integer llGetRegionAgentCount()
        {

            int count = 0;
            World.ForEachRootScenePresence(delegate(ScenePresence sp) {
                count++;
            });

            return new LSL_Integer(count);
        }

        public LSL_Vector llGetRegionCorner()
        {
            return new LSL_Vector(World.RegionInfo.WorldLocX, World.RegionInfo.WorldLocY, 0);
        }

        public LSL_String llGetEnv(LSL_String name)
        {
            string sname = name;
            sname = sname.ToLower();
            switch(sname)
            {
                case "agent_limit":
                    return World.RegionInfo.RegionSettings.AgentLimit.ToString();

                case "dynamic_pathfinding":
                    return "0";

                case "estate_id":
                    return World.RegionInfo.EstateSettings.EstateID.ToString();

                case "estate_name":
                    return World.RegionInfo.EstateSettings.EstateName;

                case "frame_number":
                    return World.Frame.ToString();

                case "region_cpu_ratio":
                    return "1";

                case "region_idle":
                    return "0";

                case "region_product_name":
                    if (World.RegionInfo.RegionType != String.Empty)
                        return World.RegionInfo.RegionType;
                    else
                        return "";

                case "region_product_sku":
                    return "OpenSim";

                case "region_start_time":
                    return World.UnixStartTime.ToString();

                case "region_up_time":
                    int time = Util.UnixTimeSinceEpoch() - World.UnixStartTime;
                    return time.ToString();

                case "sim_channel":
                    return "OpenSim";

                case "sim_version":
                    return World.GetSimulatorVersion();

                case "simulator_hostname":
                    IUrlModule UrlModule = World.RequestModuleInterface<IUrlModule>();
                    return UrlModule.ExternalHostNameForLSL;

                case "region_max_prims":
                    return World.RegionInfo.ObjectCapacity.ToString();

                case "region_object_bonus":
                    return World.RegionInfo.RegionSettings.ObjectBonus.ToString();

                case "whisper_range":
                    return m_whisperdistance.ToString();

                case "chat_range":
                    return m_saydistance.ToString();

                case "shout_range":
                    return m_shoutdistance.ToString();

                default:
                    return "";
            }
        }

        /// <summary>
        /// Insert the list identified by <paramref name="src"/> into the
        /// list designated by <paramref name="dest"/> such that the first
        /// new element has the index specified by <paramref name="index"/>
        /// </summary>

        public LSL_List llListInsertList(LSL_List dest, LSL_List src, int index)
        {

            LSL_List pref;
            LSL_List suff;


            if (index < 0)
            {
                index += dest.Length;
                if (index < 0)
                {
                    index = 0;
                }
            }

            if (index != 0)
            {
                pref = dest.GetSublist(0,index-1);
                if (index < dest.Length)
                {
                    suff = dest.GetSublist(index,-1);
                    return pref + src + suff;
                }
                else
                {
                    return pref + src;
                }
            }
            else
            {
                if (index < dest.Length)
                {
                    suff = dest.GetSublist(index,-1);
                    return src + suff;
                }
                else
                {
                    return src;
                }
            }

        }



        /// <summary>
        /// Returns the index of the first occurrence of test
        /// in src.
        /// </summary>
        /// <param name="src">Source list</param>
        /// <param name="test">List to search for</param>
        /// <returns>
        /// The index number of the point in src where test was found if it was found.
        /// Otherwise returns -1
        /// </returns>
        public LSL_Integer llListFindList(LSL_List lsrc, LSL_List ltest)
        {
            int srclen = lsrc.Length;
            int testlen = ltest.Length;
            if (srclen == 0)
                return -1;
            if (testlen == 0)
                return 0;
            if (testlen > srclen)
                return -1;

            object[] src = lsrc.Data;
            object[] test = ltest.Data;

            object test0 = test[0];
            for (int i = 0; i <= srclen - testlen; i++)
            {
                if (LSL_List.ListFind_areEqual(test0, src[i]))
                {
                    int k = i + 1;
                    int j = 1;
                    while(j < testlen)
                    {
                        if (!LSL_List.ListFind_areEqual(test[j], src[k]))
                            break;
                        ++j;
                        ++k;
                    }

                    if (j == testlen)
                        return i;
                 }
            }
            return -1;
        }

        public LSL_Integer llListFindListNext(LSL_List lsrc, LSL_List ltest, LSL_Integer linstance)
        {
            int srclen = lsrc.Length;
            int testlen = ltest.Length;
            if (srclen == 0)
                return testlen == 0 ? 0 : -1;

            int instance = linstance.value;
            if (testlen == 0)
            {
                if(instance >= 0)
                    return instance < srclen ? instance : -1;

                instance += srclen;
                return instance >= 0 ? instance : -1;
            }

            if (testlen > srclen)
                return -1;

            object[] src = lsrc.Data;
            object[] test = ltest.Data;

            object test0 = test[0];
            int nmatchs = 0;

            if(instance >= 0)
            {
                for (int i = 0; i <= srclen - testlen; i++)
                {
                    if (LSL_List.ListFind_areEqual(test0, src[i]))
                    {
                        int k = i + 1;
                        int j = 1;
                        while(j < testlen)
                        {
                            if (!LSL_List.ListFind_areEqual(test[j], src[k]))
                                break;
                            ++j;
                            ++k;
                        }

                        if (j == testlen)
                        {
                            if(nmatchs == instance)
                                return i;

                            nmatchs++;
                        }
                     }
                }
            }
            else
            {
                instance++;
                instance = -instance;

                for (int i = srclen - testlen; i >= 0 ; i--)
                {
                    if (LSL_List.ListFind_areEqual(test0, src[i]))
                    {
                        int k = i + 1;
                        int j = 1;
                        while(j < testlen)
                        {
                            if (!LSL_List.ListFind_areEqual(test[j], src[k]))
                                break;
                            ++j;
                            ++k;
                        }

                        if (j == testlen)
                        {
                            if(nmatchs == instance)
                                return i;

                            nmatchs++;
                        }
                     }
                }
            }
            return -1;
        }

        public LSL_Integer llListFindStrided(LSL_List lsrc, LSL_List ltest, LSL_Integer lstart, LSL_Integer lend, LSL_Integer lstride)
        {
            int srclen = lsrc.Length;
            int testlen = ltest.Length;
            if (srclen == 0)
                return -1;
            if (testlen == 0)
                return 0;
            if (testlen > srclen)
                return -1;

            int start = lstart.value;
            if (start < 0)
            {
                start += srclen;
                if (start < 0)
                    return -1;
            }
            else if (start >= srclen)
                return -1;

            int end = lend.value;
            if (end < 0)
            {
                end += srclen;
                if (end < 0)
                    return -1;
                end -= testlen - 1;
            }
            else if (end >= srclen)
                end = srclen - testlen;

            int stride = lstride.value;
            if (stride < 1)
                stride = 1;

            object[] src = lsrc.Data;
            object[] test = ltest.Data;

            object test0 = test[0];
            for (int i = start; i <= end; i += stride)
            {
                if (LSL_List.ListFind_areEqual(test0, src[i]))
                {
                    int k = i + 1;
                    int j = 1;
                    while (j < test.Length)
                    {
                        if (!LSL_List.ListFind_areEqual(test[j], src[k]))
                            break;
                        ++j;
                        ++k;
                    }

                    if (j == testlen)
                        return i;
                }
            }
            return -1;
        }

        public LSL_String llGetObjectName()
        {
            return m_host.Name ?? string.Empty;
        }

        public void llSetObjectName(string name)
        {
            m_host.Name = name ?? String.Empty;
        }

        public LSL_String llGetDate()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd");
        }

        public LSL_Integer llEdgeOfWorld(LSL_Vector pos, LSL_Vector dir)
        {

            if(dir.x == 0 && dir.y == 0)
                return 1; // SL wiki

            float rsx = World.RegionInfo.RegionSizeX;
            float rsy = World.RegionInfo.RegionSizeY;

            // can understand what sl does if position is not in region, so do something :)
            float px = Math.Clamp((float)pos.x, 0.5f, rsx - 0.5f);
            float py = Math.Clamp((float)pos.y, 0.5f, rsy - 0.5f);

            float ex, ey;

            if (dir.x == 0)
            {
                ex = px;
                ey = dir.y > 0 ? rsy + 1.0f : -1.0f;
            }
            else if(dir.y == 0)
            {
                ex = dir.x > 0 ? rsx + 1.0f : -1.0f;
                ey = py;
            }
            else
            {
                float dx = (float) dir.x;
                float dy = (float) dir.y;

                float t1 = dx * dx + dy * dy;
                t1 = (float)Math.Sqrt(t1);
                dx /= t1;
                dy /= t1;

                if(dx > 0)
                    t1 = (rsx + 1f - px)/dx;
                else
                    t1 = -(px + 1f)/dx;

                float t2;
                if(dy > 0)
                    t2 = (rsy + 1f - py)/dy;
                else
                    t2 = -(py + 1f)/dy;

                if(t1 > t2)
                    t1 = t2;

                ex = px + t1 * dx;
                ey = py + t1 * dy;
            }

            ex += World.RegionInfo.WorldLocX;
            ey += World.RegionInfo.WorldLocY;

            if(World.GridService.GetRegionByPosition(RegionScopeID, (int)ex, (int)ey) != null)
                return 0;
            return 1;
        }

        /// <summary>
        /// Not fully implemented yet. Still to do:-
        /// AGENT_BUSY
        /// Remove as they are done
        /// </summary>
        static readonly UUID busyAnimation = new("efcf670c-2d18-8128-973a-034ebc806b67");

        public LSL_Integer llGetAgentInfo(LSL_Key id)
        {

            if (!UUID.TryParse(id, out UUID key) || key.IsZero())
            {
                return 0;
            }

            ScenePresence agent = World.GetScenePresence(key);
            if (agent == null)
            {
                return 0;
            }

            if (agent.IsChildAgent || agent.IsDeleted)
                return 0; // Fail if they are not in the same region

            int flags = 0;
            try
            {
                // note: in OpenSim, sitting seems to cancel AGENT_ALWAYS_RUN, unlike SL
                if (agent.SetAlwaysRun)
                {
                    flags |= ScriptBaseClass.AGENT_ALWAYS_RUN;
                }

                if (agent.HasAttachments())
                {
                    flags |= ScriptBaseClass.AGENT_ATTACHMENTS;
                    if (agent.HasScriptedAttachments())
                        flags |= ScriptBaseClass.AGENT_SCRIPTED;
                }

                if ((agent.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_FLY) != 0)
                {
                    flags |= ScriptBaseClass.AGENT_FLYING;
                    flags |= ScriptBaseClass.AGENT_IN_AIR; // flying always implies in-air, even if colliding with e.g. a wall
                }

                if ((agent.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_AWAY) != 0)
                {
                    flags |= ScriptBaseClass.AGENT_AWAY;
                }

                if(agent.Animator.HasAnimation(busyAnimation))
                {
                    flags |= ScriptBaseClass.AGENT_BUSY;
                }

                // seems to get unset, even if in mouselook, when avatar is sitting on a prim???
                if ((agent.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_MOUSELOOK) != 0)
                {
                    flags |= ScriptBaseClass.AGENT_MOUSELOOK;
                }

                if ((agent.State & (byte)AgentState.Typing) != 0)
                {
                    flags |= ScriptBaseClass.AGENT_TYPING;
                }

                string agentMovementAnimation = agent.Animator.CurrentMovementAnimation;

                if (agentMovementAnimation == "CROUCH")
                {
                    flags |= ScriptBaseClass.AGENT_CROUCHING;
                }
                else if (agentMovementAnimation == "WALK" || agentMovementAnimation == "CROUCHWALK")
                {
                    flags |= ScriptBaseClass.AGENT_WALKING;
                }

                // not colliding implies in air. Note: flying also implies in-air, even if colliding (see above)

                // note: AGENT_IN_AIR and AGENT_WALKING seem to be mutually exclusive states in SL.

                // note: this may need some tweaking when walking downhill. you "fall down" for a brief instant
                // and don't collide when walking downhill, which instantly registers as in-air, briefly. should
                // there be some minimum non-collision threshold time before claiming the avatar is in-air?
                if ((flags & ScriptBaseClass.AGENT_WALKING) == 0 && !agent.IsColliding )
                {
                    flags |= ScriptBaseClass.AGENT_IN_AIR;
                }

                 if (agent.ParentPart != null)
                 {
                     flags |= ScriptBaseClass.AGENT_ON_OBJECT;
                     flags |= ScriptBaseClass.AGENT_SITTING;
                 }

                 if (agent.Animator.Animations.ImplicitDefaultAnimation.AnimID.Equals(DefaultAvatarAnimations.AnimsUUIDbyName["SIT_GROUND_CONSTRAINED"]))
                 {
                     flags |= ScriptBaseClass.AGENT_SITTING;
                 }

                 if (agent.Appearance.VisualParams[(int)AvatarAppearance.VPElement.SHAPE_MALE] > 0)
                 {
                     flags |= ScriptBaseClass.AGENT_MALE;
                 }
             }
             catch
             {
                return 0;
             }

            return flags;
        }

        public LSL_String llGetAgentLanguage(LSL_Key id)
        {
            // This should only return a value if the avatar is in the same region, but eh. idc.
            if (World.AgentPreferencesService == null)
            {
                Error("llGetAgentLanguage", "No AgentPreferencesService present");
            }
            else
            {
                if (UUID.TryParse(id, out UUID key) && key.IsNotZero())
                {
                    return new LSL_String(World.AgentPreferencesService.GetLang(key));
                }
            }
            return new LSL_String("en-us");
        }
        /// <summary>
        /// http://wiki.secondlife.com/wiki/LlGetAgentList
        /// The list of options is currently not used in SL
        /// scope is one of:-
        /// AGENT_LIST_REGION - all in the region
        /// AGENT_LIST_PARCEL - all in the same parcel as the scripted object
        /// AGENT_LIST_PARCEL_OWNER - all in any parcel owned by the owner of the
        /// current parcel.
        /// AGENT_LIST_EXCLUDENPC ignore NPCs (bit mask)
        /// </summary>
        public LSL_List llGetAgentList(LSL_Integer scope, LSL_List options)
        {

            // do our bit masks part
            bool noNPC = (scope & ScriptBaseClass.AGENT_LIST_EXCLUDENPC) !=0;

            // remove bit masks part
            scope &= ~ ScriptBaseClass.AGENT_LIST_EXCLUDENPC;

            // the constants are 1, 2 and 4 so bits are being set, but you
            // get an error "INVALID_SCOPE" if it is anything but 1, 2 and 4
            bool regionWide = scope == ScriptBaseClass.AGENT_LIST_REGION;
            bool parcelOwned = scope == ScriptBaseClass.AGENT_LIST_PARCEL_OWNER;
            bool parcel = scope == ScriptBaseClass.AGENT_LIST_PARCEL;

            LSL_List result = new();

            if (!regionWide && !parcelOwned && !parcel)
            {
                result.Add("INVALID_SCOPE");
                return result;
            }

            ILandObject land;
            UUID id = UUID.Zero;

            if (parcel || parcelOwned)
            {
                land = World.LandChannel.GetLandObject(m_host.ParentGroup.RootPart.GetWorldPosition());
                if (land == null)
                {
                    id = UUID.Zero;
                }
                else
                {
                    if (parcelOwned)
                    {
                        id = land.LandData.OwnerID;
                    }
                    else
                    {
                        id = land.LandData.GlobalID;
                    }
                }
            }

            World.ForEachRootScenePresence(
                delegate (ScenePresence ssp)
                {
                    if(noNPC && ssp.IsNPC)
                        return;

                    // Gods are not listed in SL
                    if (!ssp.IsDeleted && !ssp.IsViewerUIGod && !ssp.IsChildAgent)
                    {
                        if (!regionWide)
                        {
                            land = World.LandChannel.GetLandObject(ssp.AbsolutePosition);
                            if (land != null)
                            {
                                if (parcelOwned && land.LandData.OwnerID.Equals(id) ||
                                    parcel && land.LandData.GlobalID.Equals(id))
                                {
                                    result.Add(new LSL_Key(ssp.UUID.ToString()));
                                }
                            }
                        }
                        else
                        {
                            result.Add(new LSL_Key(ssp.UUID.ToString()));
                        }
                    }
                    // Maximum of 100 results
                    if (result.Length > 99)
                    {
                        return;
                    }
                }
            );
            return result;
        }

        public void llAdjustSoundVolume(LSL_Float volume)
        {
            m_host.AdjustSoundGain(volume);
            ScriptSleep(m_sleepMsOnAdjustSoundVolume);
        }

        public void llLinkAdjustSoundVolume(LSL_Integer linknumber, LSL_Float volume)
        {
            foreach (SceneObjectPart part in GetLinkParts(linknumber))
                part.AdjustSoundGain(volume);

            ScriptSleep(m_sleepMsOnAdjustSoundVolume);
        }

        public void llSetSoundRadius(double radius)
        {
            m_host.SoundRadius = radius;
        }

        public void llLinkSetSoundRadius(int linknumber, double radius)
        {
            foreach (SceneObjectPart sop in GetLinkParts(linknumber))
                sop.SoundRadius = radius;
        }

        public LSL_String llKey2Name(LSL_Key id)
        {
            if (UUID.TryParse(id, out UUID key) && key.IsNotZero())
            {
                ScenePresence presence = World.GetScenePresence(key);
                if (presence is not null)
                    return presence.Name;

                SceneObjectPart sop = World.GetSceneObjectPart(key);
                if (sop is not null)
                    return sop.Name;
            }
            return LSL_String.Empty;
        }

        public LSL_Key llName2Key(LSL_String name)
        {
            if(string.IsNullOrWhiteSpace(name))
                return ScriptBaseClass.NULL_KEY;

            int nc = Util.ParseAvatarName(name, out string firstName, out string lastName, out string server);
            if (nc < 2)
                return ScriptBaseClass.NULL_KEY;

            string sname;
            if (nc == 2)
                sname = firstName + " " + lastName;
            else
                sname = firstName + "." + lastName + " @" + server;

            foreach (ScenePresence sp in World.GetScenePresences())
            {
                if (sp.IsDeleted || sp.IsChildAgent)
                    continue;
                if (String.Compare(sname, sp.Name, true) == 0)
                    return sp.UUID.ToString();
            }

            return ScriptBaseClass.NULL_KEY;
        }

        public LSL_Key llRequestUserKey(LSL_String username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return ScriptBaseClass.NULL_KEY;

            int nc = Util.ParseAvatarName(username, out string firstName, out string lastName, out string server);
            if (nc < 2)
                return ScriptBaseClass.NULL_KEY;

            string sname;
            if (nc == 2)
                sname = firstName + " " + lastName;
            else
                sname = firstName + "." + lastName + " @" + server;

            foreach (ScenePresence sp in World.GetScenePresences())
            {
                if (sp.IsDeleted || sp.IsChildAgent)
                    continue;
                if (String.Compare(sname, sp.Name, true) == 0)
                {
                    string ftid = m_AsyncCommands.DataserverPlugin.RequestWithImediatePost(m_host.LocalId,
                                                        m_item.ItemID, sp.UUID.ToString());
                    return ftid;
                }
            }

            void act(string eventID)
            {
                string reply = ScriptBaseClass.NULL_KEY;
                UUID userID = UUID.Zero;
                IUserManagement userManager = World.RequestModuleInterface<IUserManagement>();
                if (nc == 2)
                {
                    if (userManager is not null)
                    {
                        userID = userManager.GetUserIdByName(firstName, lastName);
                        if (!userID.IsZero())
                            reply = userID.ToString();
                    }
                }
                else
                {
                    string url = "http://" + server;
                    if (Uri.TryCreate(url, UriKind.Absolute, out Uri dummy))
                    {
                        bool notfound = true;
                        if (userManager is not null)
                        {
                            string hgfirst = firstName + "." + lastName;
                            string hglast = "@" + server;
                            userID = userManager.GetUserIdByName(hgfirst, hglast);
                            if (!userID.IsZero())
                            {
                                notfound = false;
                                reply = userID.ToString();
                            }
                        }

                        if (notfound)
                        {
                            try
                            {
                                UserAgentServiceConnector userConnection = new(url);
                                if (userConnection is not null)
                                {
                                    userID = userConnection.GetUUID(firstName, lastName);
                                    if (!userID.IsZero())
                                    {
                                        userManager?.AddUser(userID, firstName, lastName, url);
                                        reply = userID.ToString();
                                    }
                                }
                            }
                            catch
                            {
                                reply = ScriptBaseClass.NULL_KEY;
                            }
                        }
                    }
                }
                m_AsyncCommands.DataserverPlugin.DataserverReply(eventID, reply);
            }

            UUID tid = m_AsyncCommands.DataserverPlugin.RegisterRequest(m_host.LocalId, m_item.ItemID, act);
            ScriptSleep(m_sleepMsOnRequestAgentData);
            return tid.ToString();
        }

        public void llSetTextureAnim(int mode, int face, int sizex, int sizey, double start, double length, double rate)
        {

            SetTextureAnim(m_host, mode, face, sizex, sizey, start, length, rate);
        }

        public void llSetLinkTextureAnim(int linknumber, int mode, int face, int sizex, int sizey, double start, double length, double rate)
        {

            List<SceneObjectPart> parts = GetLinkParts(linknumber);

            try
            {
                foreach (SceneObjectPart part in parts)
                {
                    SetTextureAnim(part, mode, face, sizex, sizey, start, length, rate);
                }
            }
            finally
            {
            }
        }

        private static void SetTextureAnim(SceneObjectPart part, int mode, int face, int sizex, int sizey, double start, double length, double rate)
        {
            //ALL_SIDES
            if (face == ScriptBaseClass.ALL_SIDES)
                face = 255;

            Primitive.TextureAnimation pTexAnim = new()
            {
                Flags = (Primitive.TextureAnimMode)mode,
                Face = (uint)face,
                Length = (float)length,
                Rate = (float)rate,
                SizeX = (uint)sizex,
                SizeY = (uint)sizey,
                Start = (float)start
            };

            part.AddTextureAnimation(pTexAnim);
            part.SendFullUpdateToAllClients();
            part.ParentGroup.HasGroupChanged = true;
        }

        public void llTriggerSoundLimited(string sound, double volume, LSL_Vector top_north_east,
                                          LSL_Vector bottom_south_west)
        {
            m_SoundModule?.TriggerSoundLimited(m_host.UUID,
                        ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound, AssetType.Sound), volume,
                        bottom_south_west, top_north_east);
        }

        public void llEjectFromLand(LSL_Key pest)
        {
            if (UUID.TryParse(pest, out UUID agentID) && agentID.IsNotZero())
            {
                ScenePresence presence = World.GetScenePresence(agentID);
                if (presence != null)
                {
                    // agent must be over the owners land
                    ILandObject land = World.LandChannel.GetLandObject(presence.AbsolutePosition);
                    if (land == null)
                        return;

                    if (m_host.OwnerID.Equals(land.LandData.OwnerID))
                    {
                        Vector3 p = World.GetNearestAllowedPosition(presence, land);
                        presence.TeleportOnEject(p);
                        presence.ControllingClient.SendAlertMessage("You have been ejected from this land");
                    }
                }
            }
            ScriptSleep(m_sleepMsOnEjectFromLand);
        }

        public LSL_List llParseString2List(string str, LSL_List separators, LSL_List in_spacers)
        {
            return ParseString2List(str, separators, in_spacers, false);
        }

        public LSL_Integer llOverMyLand(string id)
        {
            if (UUID.TryParse(id, out UUID key) && key.IsNotZero())
            {
                try
                {
                    ScenePresence presence = World.GetScenePresence(key);
                    if (presence != null) // object is an avatar
                    {
                        if (m_host.OwnerID.Equals(World.LandChannel.GetLandObject(presence.AbsolutePosition).LandData.OwnerID))
                            return 1;
                    }
                    else // object is not an avatar
                    {
                        SceneObjectPart obj = World.GetSceneObjectPart(key);

                        if (obj != null &&
                            m_host.OwnerID.Equals(World.LandChannel.GetLandObject(obj.AbsolutePosition).LandData.OwnerID))
                        return 1;
                    }
                }
                catch { }
            }

            return 0;
        }

        public LSL_Key llGetLandOwnerAt(LSL_Vector pos)
        {
            ILandObject land = World.LandChannel.GetLandObject((float)pos.x, (float)pos.y);
            if (land == null)
                return ScriptBaseClass.NULL_KEY;
            return land.LandData.OwnerID.ToString();
        }

        /// <summary>
        /// According to http://lslwiki.net/lslwiki/wakka.php?wakka=llGetAgentSize
        /// only the height of avatars vary and that says:
        /// Width (x) and depth (y) are constant. (0.45m and 0.6m respectively).
        /// </summary>
        public LSL_Vector llGetAgentSize(LSL_Key id)
        {

            if(!UUID.TryParse(id, out UUID avID) || avID.IsZero())
                return ScriptBaseClass.ZERO_VECTOR;

            ScenePresence avatar = World.GetScenePresence(avID);
            if (avatar == null || avatar.IsChildAgent) // Fail if not in the same region
                return ScriptBaseClass.ZERO_VECTOR;

            return new LSL_Vector(avatar.Appearance.AvatarSize);
        }

        public LSL_Integer llSameGroup(string id)
        {
            if (!UUID.TryParse(id, out UUID uuid) || uuid.IsZero())
                return 0;

            // Check if it's a group key
            if (uuid.Equals(m_host.ParentGroup.RootPart.GroupID))
                return 1;

            // Handle object case
            SceneObjectPart part = World.GetSceneObjectPart(uuid);
            if (part != null)
            {
                if(part.ParentGroup.IsAttachment)
                {
                    uuid = part.ParentGroup.AttachedAvatar;
                }
                else
                {
                    // This will handle both deed and non-deed and also the no
                    // group case
                    if (part.ParentGroup.RootPart.GroupID.Equals(m_host.ParentGroup.RootPart.GroupID))
                        return 1;
                    return 0;
                }
            }

            // Handle the case where id names an avatar
            ScenePresence presence = World.GetScenePresence(uuid);
            if (presence != null)
            {
                if (presence.IsChildAgent)
                    return 0;

                IClientAPI client = presence.ControllingClient;
                if (m_host.ParentGroup.RootPart.GroupID.Equals(client.ActiveGroupId))
                    return 1;

                return 0;
            }

            return 0;
        }

        public void llUnSit(string id)
        {
            if (!UUID.TryParse(id, out UUID key) || key.IsZero())
                return;

            ScenePresence av = World.GetScenePresence(key);
            if(av == null)
                return;

            List<ScenePresence> sittingAvatars = m_host.ParentGroup.GetSittingAvatars();

            if (sittingAvatars.Contains(av))
            {
                av.StandUp();
            }
            else
            {
                // If the object owner also owns the parcel
                // or
                // if the land is group owned and the object is group owned by the same group
                // or
                // if the object is owned by a person with estate access.
                ILandObject parcel = World.LandChannel.GetLandObject(av.AbsolutePosition);
                if (parcel != null)
                {
                    if (m_host.OwnerID.Equals(parcel.LandData.OwnerID) ||
                        (m_host.OwnerID.Equals(m_host.GroupID) && m_host.GroupID.Equals(parcel.LandData.GroupID)
                        && parcel.LandData.IsGroupOwned) || World.Permissions.IsGod(m_host.OwnerID))
                    {
                        av.StandUp();
                    }
                }
            }
        }

        public LSL_Vector llGroundSlope(LSL_Vector offset)
        {

            //Get the slope normal.  This gives us the equation of the plane tangent to the slope.
            LSL_Vector vsn = llGroundNormal(offset);

            //Plug the x,y coordinates of the slope normal into the equation of the plane to get
            //the height of that point on the plane.  The resulting vector gives the slope.
            Vector3 vsl = vsn;
            vsl.Z = (float)(((vsn.x * vsn.x) + (vsn.y * vsn.y)) / (-1 * vsn.z));
            vsl.Normalize();
            //Normalization might be overkill here

            vsn.x = vsl.X;
            vsn.y = vsl.Y;
            vsn.z = vsl.Z;

            return vsn;
        }

        public LSL_Vector llGroundNormal(LSL_Vector offset)
        {
            Vector3 pos = m_host.GetWorldPosition();
            int posX = (int)(pos.X + (float)offset.x);
            int posY = (int)(pos.Y + (float)offset.y);

            // Clamp to valid position
            if (posX < 0)
                posX = 0;
            else if (posX >= World.Heightmap.Width)
                posX = World.Heightmap.Width - 1;

            if (posY < 0)
                posY = 0;
            else if (posY >= World.Heightmap.Height)
                posY = World.Heightmap.Height - 1;

            //Find two points in addition to the position to define a plane
            float h0 = (float)World.Heightmap[(int)pos.X, (int)pos.Y];
            float h1;
            float h2;
            int posxplus = posX + 1;
            if (posxplus >= World.Heightmap.Width)
                h1 = h0;
            else
                h1 = (float)World.Heightmap[posxplus, posY];

            int posyplus = posY + 1;
            if (posyplus >= World.Heightmap.Height)
                h2 = h0;
            else
                h2 = (float)World.Heightmap[posX, posyplus];

            Vector3 vsn = new(h0 - h1, h0 - h2, 1.0f);
            vsn.Normalize();

            return new LSL_Vector(vsn);
        }

        public LSL_Vector llGroundContour(LSL_Vector offset)
        {
            LSL_Vector x = llGroundSlope(offset);
            return new LSL_Vector(-x.y, x.x, 0.0);
        }

        public LSL_Integer llGetAttached()
        {
            return m_host.ParentGroup.AttachmentPoint;
        }

        public LSL_List llGetAttachedList(LSL_Key id)
        {
            if(!UUID.TryParse(id, out UUID avID) || avID.IsZero())
                return new LSL_List("NOT_FOUND");

            ScenePresence av = World.GetScenePresence(avID);
            if (av is null || av.IsDeleted)
                return new LSL_List("NOT_FOUND");

            if (av.IsChildAgent || av.IsInTransit)
                return new LSL_List("NOT_ON_REGION");

            LSL_List AttachmentsList = new();
            List<SceneObjectGroup> Attachments = av.GetAttachments();

            foreach (SceneObjectGroup Attachment in Attachments)
            {
                if(Attachment.HasPrivateAttachmentPoint)
                    continue;
                AttachmentsList.Add(new LSL_Key(Attachment.UUID.ToString()));
            }

            return AttachmentsList;
        }

        public virtual LSL_Integer llGetFreeMemory()
        {
            // Make scripts designed for Mono happy
            return 65536;
        }

        public LSL_Integer llGetFreeURLs()
        {
            if (m_UrlModule != null)
                return new LSL_Integer(m_UrlModule.GetFreeUrls());
            return new LSL_Integer(0);
        }


        public LSL_String llGetRegionName()
        {
            return m_regionName;
        }

        public LSL_Float llGetRegionTimeDilation()
        {
            return (double)World.TimeDilation;
        }

        /// <summary>
        /// Returns the value reported in the client Statistics window
        /// </summary>
        public LSL_Float llGetRegionFPS()
        {
            return World.StatsReporter.LastReportedSimFPS;
        }


        /* particle system rules should be coming into this routine as doubles, that is
        rule[0] should be an integer from this list and rule[1] should be the arg
        for the same integer. wiki.secondlife.com has most of this mapping, but some
        came from http://www.caligari-designs.com/p4u2

        We iterate through the list for 'Count' elements, incrementing by two for each
        iteration and set the members of Primitive.ParticleSystem, one at a time.
        */

        public enum PrimitiveRule : int
        {
            PSYS_PART_FLAGS = 0,
            PSYS_PART_START_COLOR = 1,
            PSYS_PART_START_ALPHA = 2,
            PSYS_PART_END_COLOR = 3,
            PSYS_PART_END_ALPHA = 4,
            PSYS_PART_START_SCALE = 5,
            PSYS_PART_END_SCALE = 6,
            PSYS_PART_MAX_AGE = 7,
            PSYS_SRC_ACCEL = 8,
            PSYS_SRC_PATTERN = 9,
            PSYS_SRC_INNERANGLE = 10,
            PSYS_SRC_OUTERANGLE = 11,
            PSYS_SRC_TEXTURE = 12,
            PSYS_SRC_BURST_RATE = 13,
            PSYS_SRC_BURST_PART_COUNT = 15,
            PSYS_SRC_BURST_RADIUS = 16,
            PSYS_SRC_BURST_SPEED_MIN = 17,
            PSYS_SRC_BURST_SPEED_MAX = 18,
            PSYS_SRC_MAX_AGE = 19,
            PSYS_SRC_TARGET_KEY = 20,
            PSYS_SRC_OMEGA = 21,
            PSYS_SRC_ANGLE_BEGIN = 22,
            PSYS_SRC_ANGLE_END = 23,
            PSYS_PART_BLEND_FUNC_SOURCE = 24,
            PSYS_PART_BLEND_FUNC_DEST = 25,
            PSYS_PART_START_GLOW = 26,
            PSYS_PART_END_GLOW = 27
        }

        internal static Primitive.ParticleSystem.ParticleDataFlags ConvertUINTtoFlags(uint flags)
        {
            Primitive.ParticleSystem.ParticleDataFlags returnval = Primitive.ParticleSystem.ParticleDataFlags.None;

            return returnval;
        }

        protected static Primitive.ParticleSystem getNewParticleSystemWithSLDefaultValues()
        {
            return new Primitive.ParticleSystem()
            {
                PartStartColor = new Color4(1.0f, 1.0f, 1.0f, 1.0f),
                PartEndColor = new Color4(1.0f, 1.0f, 1.0f, 1.0f),
                PartStartScaleX = 1.0f,
                PartStartScaleY = 1.0f,
                PartEndScaleX = 1.0f,
                PartEndScaleY = 1.0f,
                BurstSpeedMin = 1.0f,
                BurstSpeedMax = 1.0f,
                BurstRate = 0.1f,
                PartMaxAge = 10.0f,
                BurstPartCount = 1,
                BlendFuncSource = ScriptBaseClass.PSYS_PART_BF_SOURCE_ALPHA,
                BlendFuncDest = ScriptBaseClass.PSYS_PART_BF_ONE_MINUS_SOURCE_ALPHA,
                PartStartGlow = 0.0f,
                PartEndGlow = 0.0f
            };
        }

        public void llLinkParticleSystem(int linknumber, LSL_List rules)
        {

            List<SceneObjectPart> parts = GetLinkParts(linknumber);

            foreach (SceneObjectPart part in parts)
            {
                SetParticleSystem(part, rules, "llLinkParticleSystem");
            }
        }

        public void llParticleSystem(LSL_List rules)
        {
            SetParticleSystem(m_host, rules, "llParticleSystem");
        }

        public void SetParticleSystem(SceneObjectPart part, LSL_List rules, string originFunc, bool expire = false)
        {
            if (rules.Length == 0)
            {
                part.RemoveParticleSystem();
                part.ParentGroup.HasGroupChanged = true;
            }
            else
            {
                Primitive.ParticleSystem prules = getNewParticleSystemWithSLDefaultValues();
                LSL_Vector tempv;
                float tempf;
                int tmpi;

                for (int i = 0; i < rules.Length; i += 2)
                {
                    int psystype;
                    try
                    {
                        psystype = rules.GetIntegerItem(i);
                    }
                    catch (InvalidCastException)
                    {
                        Error(originFunc, string.Format("Error running particle system params index #{0}: particle system parameter type must be integer", i));
                        return;
                    }
                    switch (psystype)
                    {
                        case ScriptBaseClass.PSYS_PART_FLAGS:
                            try
                            {
                                prules.PartDataFlags = (Primitive.ParticleSystem.ParticleDataFlags)(uint)rules.GetIntegerItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_FLAGS: arg #{0} - parameter 1 must be integer", i + 1));
                                return;
                            }
                            break;

                        case ScriptBaseClass.PSYS_PART_START_COLOR:
                            try
                            {
                                tempv = rules.GetVector3Item(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_START_COLOR: arg #{0} - parameter 1 must be vector", i + 1));
                                return;
                            }
                            prules.PartStartColor.R = (float)tempv.x;
                            prules.PartStartColor.G = (float)tempv.y;
                            prules.PartStartColor.B = (float)tempv.z;
                            break;

                        case ScriptBaseClass.PSYS_PART_START_ALPHA:
                            try
                            {
                                tempf = rules.GetStrictFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_START_ALPHA: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.PartStartColor.A = tempf;
                            break;

                        case ScriptBaseClass.PSYS_PART_END_COLOR:
                            try
                            {
                                tempv = rules.GetVector3Item(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_END_COLOR: arg #{0} - parameter 1 must be vector", i + 1));
                                return;
                            }
                            prules.PartEndColor.R = (float)tempv.x;
                            prules.PartEndColor.G = (float)tempv.y;
                            prules.PartEndColor.B = (float)tempv.z;
                            break;

                        case ScriptBaseClass.PSYS_PART_END_ALPHA:
                            try
                            {
                                tempf = rules.GetStrictFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_END_ALPHA: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.PartEndColor.A = tempf;
                            break;

                        case ScriptBaseClass.PSYS_PART_START_SCALE:
                            try
                            {
                                tempv = rules.GetVector3Item(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_START_SCALE: arg #{0} - parameter 1 must be vector", i + 1));
                                return;
                            }
                            prules.PartStartScaleX = validParticleScale((float)tempv.x);
                            prules.PartStartScaleY = validParticleScale((float)tempv.y);
                            break;

                        case ScriptBaseClass.PSYS_PART_END_SCALE:
                            try
                            {
                                tempv = rules.GetVector3Item(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_END_SCALE: arg #{0} - parameter 1 must be vector", i + 1));
                                return;
                            }
                            prules.PartEndScaleX = validParticleScale((float)tempv.x);
                            prules.PartEndScaleY = validParticleScale((float)tempv.y);
                            break;

                        case ScriptBaseClass.PSYS_PART_MAX_AGE:
                            try
                            {
                                tempf = rules.GetStrictFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_MAX_AGE: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.PartMaxAge = tempf;
                            break;

                        case ScriptBaseClass.PSYS_SRC_ACCEL:
                            try
                            {
                                tempv = rules.GetVector3Item(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_ACCEL: arg #{0} - parameter 1 must be vector", i + 1));
                                return;
                            }
                            prules.PartAcceleration.X = (float)tempv.x;
                            prules.PartAcceleration.Y = (float)tempv.y;
                            prules.PartAcceleration.Z = (float)tempv.z;
                            break;

                        case ScriptBaseClass.PSYS_SRC_PATTERN:
                            try
                            {
                                tmpi = rules.GetIntegerItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_PATTERN: arg #{0} - parameter 1 must be integer", i + 1));
                                return;
                            }
                            prules.Pattern = (Primitive.ParticleSystem.SourcePattern)tmpi;
                            break;

                        // PSYS_SRC_INNERANGLE and PSYS_SRC_ANGLE_BEGIN use the same variables. The
                        // PSYS_SRC_OUTERANGLE and PSYS_SRC_ANGLE_END also use the same variable. The
                        // client tells the difference between the two by looking at the 0x02 bit in
                        // the PartFlags variable.
                        case ScriptBaseClass.PSYS_SRC_INNERANGLE:
                            try
                            {
                                tempf = rules.GetStrictFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_INNERANGLE: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.InnerAngle = tempf;
                            prules.PartFlags &= 0xFFFFFFFD; // Make sure new angle format is off.
                            break;

                        case ScriptBaseClass.PSYS_SRC_OUTERANGLE:
                            try
                            {
                                tempf = rules.GetStrictFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_OUTERANGLE: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.OuterAngle = tempf;
                            prules.PartFlags &= 0xFFFFFFFD; // Make sure new angle format is off.
                            break;

                        case ScriptBaseClass.PSYS_PART_BLEND_FUNC_SOURCE:
                            try
                            {
                                tmpi = rules.GetIntegerItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_BLEND_FUNC_SOURCE: arg #{0} - parameter 1 must be integer", i + 1));
                                return;
                            }
                            prules.BlendFuncSource = (byte)tmpi;
                            break;

                        case ScriptBaseClass.PSYS_PART_BLEND_FUNC_DEST:
                            try
                            {
                                tmpi = rules.GetIntegerItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_BLEND_FUNC_DEST: arg #{0} - parameter 1 must be integer", i + 1));
                                return;
                            }
                            prules.BlendFuncDest = (byte)tmpi;
                            break;

                        case ScriptBaseClass.PSYS_PART_START_GLOW:
                            try
                            {
                                tempf = rules.GetStrictFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_START_GLOW: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.PartStartGlow = tempf;
                            break;

                        case ScriptBaseClass.PSYS_PART_END_GLOW:
                            try
                            {
                                tempf = rules.GetStrictFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_END_GLOW: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.PartEndGlow = tempf;
                            break;

                        case ScriptBaseClass.PSYS_SRC_TEXTURE:
                            try
                            {
                                prules.Texture = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, rules.GetStrictStringItem(i + 1));
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_TEXTURE: arg #{0} - parameter 1 must be string or key", i + 1));
                                return;
                            }
                            break;

                        case ScriptBaseClass.PSYS_SRC_BURST_RATE:
                            try
                            {
                                tempf = rules.GetStrictFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_BURST_RATE: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.BurstRate = tempf;
                            break;

                        case ScriptBaseClass.PSYS_SRC_BURST_PART_COUNT:
                            try
                            {
                                prules.BurstPartCount = (byte)rules.GetIntegerItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_BURST_PART_COUNT: arg #{0} - parameter 1 must be integer", i + 1));
                                return;
                            }
                            break;

                        case ScriptBaseClass.PSYS_SRC_BURST_RADIUS:
                            try
                            {
                                tempf = rules.GetStrictFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_BURST_RADIUS: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.BurstRadius = tempf;
                            break;

                        case ScriptBaseClass.PSYS_SRC_BURST_SPEED_MIN:
                            try
                            {
                                tempf = rules.GetStrictFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_BURST_SPEED_MIN: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.BurstSpeedMin = tempf;
                            break;

                        case ScriptBaseClass.PSYS_SRC_BURST_SPEED_MAX:
                            try
                            {
                                tempf = rules.GetStrictFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_BURST_SPEED_MAX: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.BurstSpeedMax = tempf;
                            break;

                        case ScriptBaseClass.PSYS_SRC_MAX_AGE:
                            try
                            {
                                tempf = rules.GetStrictFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_MAX_AGE: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.MaxAge = tempf;
                            break;

                        case ScriptBaseClass.PSYS_SRC_TARGET_KEY:
                            if (UUID.TryParse(rules.Data[i + 1].ToString(), out UUID key))
                            {
                                prules.Target = key;
                            }
                            else
                            {
                                prules.Target = part.UUID;
                            }
                            break;

                        case ScriptBaseClass.PSYS_SRC_OMEGA:
                            // AL: This is an assumption, since it is the only thing that would match.
                            try
                            {
                                tempv = rules.GetVector3Item(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_OMEGA: arg #{0} - parameter 1 must be vector", i + 1));
                                return;
                            }
                            prules.AngularVelocity.X = (float)tempv.x;
                            prules.AngularVelocity.Y = (float)tempv.y;
                            prules.AngularVelocity.Z = (float)tempv.z;
                            break;

                        case ScriptBaseClass.PSYS_SRC_ANGLE_BEGIN:
                            try
                            {
                                tempf = rules.GetStrictFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_ANGLE_BEGIN: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.InnerAngle = tempf;
                            prules.PartFlags |= 0x02; // Set new angle format.
                            break;

                        case ScriptBaseClass.PSYS_SRC_ANGLE_END:
                            try
                            {
                                tempf = rules.GetStrictFloatItem(i + 1);
                            }
                            catch (InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_ANGLE_END: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.OuterAngle = tempf;
                            prules.PartFlags |= 0x02; // Set new angle format.
                            break;
                    }
                }
                prules.CRC = 1;

                part.AddNewParticleSystem(prules, expire);
                if(!expire || prules.MaxAge != 0 || prules.MaxAge > 300)
                    part.ParentGroup.HasGroupChanged = true;
            }
            part.SendFullUpdateToAllClients();
        }

        private static float validParticleScale(float value)
        {
            return value > 7.96f ? 7.96f : value;
        }

        public void llGroundRepel(double height, int water, double tau)
        {
            if (m_host.PhysActor != null)
            {
                float ground = (float)llGround(new LSL_Types.Vector3(0, 0, 0));
                float waterLevel = (float)llWater(new LSL_Types.Vector3(0, 0, 0));
                PIDHoverType hoverType = PIDHoverType.Ground;
                if (water != 0)
                {
                    hoverType = PIDHoverType.GroundAndWater;
                    if (ground < waterLevel)
                        height += waterLevel;
                    else
                        height += ground;
                }
                else
                {
                    height += ground;
                }

                m_host.SetHoverHeight((float)height, hoverType, (float)tau);
            }
        }

        public void llGiveInventoryList(LSL_Key destination, LSL_String category, LSL_List inventory)
        {
            if (inventory.Length == 0)
                return;

            if (!UUID.TryParse(destination, out UUID destID) || destID.IsZero())
                return;

            bool isNotOwner = true;
            if (!World.TryGetSceneObjectPart(destID, out SceneObjectPart destSop))
            {
                if (!World.TryGetScenePresence(destID, out ScenePresence sp))
                {
                    // we could check if it is a grid user and allow the transfer as in older code
                    // but that increases security risk
                    Error("llGiveInventoryList", "Unable to give list, destination not found");
                    ScriptSleep(100);
                    return;
                }
                isNotOwner = sp.UUID.NotEqual(m_host.OwnerID);
            }

            List<UUID> itemList = new(inventory.Length);
            foreach (object item in inventory.Data)
            {
                string rawItemString = item.ToString();
                TaskInventoryItem taskItem = (UUID.TryParse(rawItemString, out UUID itemID)) ?
                    m_host.Inventory.GetInventoryItem(itemID) : m_host.Inventory.GetInventoryItem(rawItemString);

                if(taskItem is null)
                    continue;

                if ((taskItem.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                    continue;

                if (destSop is not null)
                {
                    if(!World.Permissions.CanDoObjectInvToObjectInv(taskItem, m_host, destSop))
                        continue;
                }
                else
                {
                    if(isNotOwner)
                    {
                        if ((taskItem.CurrentPermissions & (uint)PermissionMask.Transfer) == 0)
                            continue;
                    }
                }

                itemList.Add(taskItem.ItemID);
            }

            if (itemList.Count == 0)
            {
                Error("llGiveInventoryList", "Unable to give list, no items found");
                ScriptSleep(100);
                return;
            }

            UUID folderID = m_ScriptEngine.World.MoveTaskInventoryItems(destID, category, m_host, itemList, false);

            if (folderID.IsZero())
            {
                Error("llGiveInventoryList", "Unable to give list");
                ScriptSleep(100);
                return;
            }

            if (destSop is not null)
            {
                ScriptSleep(100);
                return;
            }

            if (m_TransferModule != null)
            {
                byte[] bucket = new byte[] { (byte)AssetType.Folder };

                Vector3 pos = m_host.AbsolutePosition;

                GridInstantMessage msg = new(World, m_host.OwnerID, m_host.Name, destID,
                        (byte)InstantMessageDialog.TaskInventoryOffered,
                        m_host.OwnerID.Equals(m_host.GroupID),
                        string.Format("'{0}'", category),
                        //string.Format("'{0}'  ( http://slurl.com/secondlife/{1}/{2}/{3}/{4} )", category, World.Name, (int)pos.X, (int)pos.Y, (int)pos.Z),
                        folderID, false, pos,
                        bucket, false);

                m_TransferModule.SendInstantMessage(msg, delegate(bool success) {});
            }

            ScriptSleep(3000);
        }

        public void llSetVehicleType(int type)
        {

            if (!m_host.ParentGroup.IsDeleted)
            {
                m_host.ParentGroup.RootPart.SetVehicleType(type);
            }
        }

        //CFK 9/28: Most, but not all of the underlying plumbing between here and the physics modules is in
        //CFK 9/28: so these are not complete yet.
        public void llSetVehicleFloatParam(int param, LSL_Float value)
        {

            if (!m_host.ParentGroup.IsDeleted)
            {
                m_host.ParentGroup.RootPart.SetVehicleFloatParam(param, (float)value);
            }
        }

        //CFK 9/28: Most, but not all of the underlying plumbing between here and the physics modules is in
        //CFK 9/28: so these are not complete yet.
        public void llSetVehicleVectorParam(int param, LSL_Vector vec)
        {

            if (!m_host.ParentGroup.IsDeleted)
            {
                m_host.ParentGroup.RootPart.SetVehicleVectorParam(param, vec);
            }
        }

        //CFK 9/28: Most, but not all of the underlying plumbing between here and the physics modules is in
        //CFK 9/28: so these are not complete yet.
        public void llSetVehicleRotationParam(int param, LSL_Rotation rot)
        {

            if (!m_host.ParentGroup.IsDeleted)
            {
                m_host.ParentGroup.RootPart.SetVehicleRotationParam(param, rot);
            }
        }

        public void llSetVehicleFlags(int flags)
        {

            if (!m_host.ParentGroup.IsDeleted)
            {
                m_host.ParentGroup.RootPart.SetVehicleFlags(flags, false);
            }
        }

        public void llRemoveVehicleFlags(int flags)
        {

            if (!m_host.ParentGroup.IsDeleted)
            {
                m_host.ParentGroup.RootPart.SetVehicleFlags(flags, true);
            }
        }

        protected static void SitTarget(SceneObjectPart part, LSL_Vector offset, LSL_Rotation rot)
        {
            // LSL quaternions can normalize to 0, normal Quaternions can't.
            if (rot.s == 0 && rot.x == 0 && rot.y == 0 && rot.z == 0)
                rot.s = 1; // ZERO_ROTATION = 0,0,0,1

            part.SitTargetPosition = offset;
            part.SitTargetOrientation = rot;
            part.ParentGroup.HasGroupChanged = true;
        }

        public void llSitTarget(LSL_Vector offset, LSL_Rotation rot)
        {
            SitTarget(m_host, offset, rot);
        }

        public void llLinkSitTarget(LSL_Integer link, LSL_Vector offset, LSL_Rotation rot)
        {
            if (link == ScriptBaseClass.LINK_ROOT)
                SitTarget(m_host.ParentGroup.RootPart, offset, rot);
            else if (link == ScriptBaseClass.LINK_THIS)
                SitTarget(m_host, offset, rot);
            else
            {
                SceneObjectPart part = m_host.ParentGroup.GetLinkNumPart(link);
                if (null != part)
                {
                    SitTarget(part, offset, rot);
                }
            }
        }

        public LSL_Key llAvatarOnSitTarget()
        {
            return m_host.SitTargetAvatar.ToString();
        }

        // http://wiki.secondlife.com/wiki/LlAvatarOnLinkSitTarget
        public LSL_Key llAvatarOnLinkSitTarget(LSL_Integer linknum)
        {
            if(linknum == ScriptBaseClass.LINK_SET ||
                    linknum == ScriptBaseClass.LINK_ALL_CHILDREN ||
                    linknum == ScriptBaseClass.LINK_ALL_OTHERS ||
                    linknum == 0)
                return ScriptBaseClass.NULL_KEY;

            List<SceneObjectPart> parts = GetLinkParts(linknum);
            if (parts.Count == 0)
                return ScriptBaseClass.NULL_KEY;
            return parts[0].SitTargetAvatar.ToString();
        }


        public void llAddToLandPassList(LSL_Key avatar, LSL_Float hours)
        {
            if(!UUID.TryParse(avatar, out UUID key) || key.IsZero())
                return;

            ILandObject parcel = World.LandChannel.GetLandObject(m_host.AbsolutePosition);
            if (World.Permissions.CanEditParcelProperties(m_host.OwnerID, parcel, GroupPowers.LandManagePasses, false))
            {
                int expires = (hours != 0) ? Util.UnixTimeSinceEpoch() + (int)(3600.0 * hours) : 0;
                LandData land = parcel.LandData;
                foreach(LandAccessEntry e in land.ParcelAccessList)
                {
                    if (e.Flags == AccessList.Access && e.AgentID.Equals(key))
                    {
                        if (e.Expires != 0 && expires > e.Expires)
                        {
                            e.Expires = expires;
                            World.EventManager.TriggerLandObjectUpdated((uint)land.LocalID, parcel);
                        }
                        return;
                    }
                }

                LandAccessEntry entry = new()
                {
                    AgentID = key,
                    Flags = AccessList.Access,
                    Expires = expires
                };
                land.ParcelAccessList.Add(entry);
                World.EventManager.TriggerLandObjectUpdated((uint)land.LocalID, parcel);
            }
            ScriptSleep(m_sleepMsOnAddToLandPassList);
        }

        public void llSetTouchText(string text)
        {
            if(text.Length <= 9)
                m_host.TouchName = text;
            else
                m_host.TouchName = text[..9];
        }

        public void llSetSitText(string text)
        {
            if (text.Length <= 9)
                m_host.SitName = text;
            else
                m_host.SitName = text[..9];
        }

        public void llSetCameraEyeOffset(LSL_Vector offset)
        {
            m_host.SetCameraEyeOffset(offset);
        }

        public void llSetCameraAtOffset(LSL_Vector offset)
        {
            m_host.SetCameraAtOffset(offset);
        }

        public void llSetLinkCamera(LSL_Integer link, LSL_Vector eye, LSL_Vector at)
        {

            if (link == ScriptBaseClass.LINK_SET ||
                link == ScriptBaseClass.LINK_ALL_CHILDREN ||
                link == ScriptBaseClass.LINK_ALL_OTHERS) return;

            SceneObjectPart part = (int)link switch
            {
                ScriptBaseClass.LINK_ROOT => m_host.ParentGroup.RootPart,
                ScriptBaseClass.LINK_THIS => m_host,
                _ => m_host.ParentGroup.GetLinkNumPart(link),
            };
            if (part is not null)
            {
                part.SetCameraEyeOffset(eye);
                part.SetCameraAtOffset(at);
            }
        }

        public LSL_String llDumpList2String(LSL_List src, string seperator)
        {
            if (src.Length == 0)
            {
                return LSL_String.Empty;
            }
            string ret = String.Empty;
            foreach (object o in src.Data)
            {
                ret = ret + o.ToString() + seperator;
            }
            ret = ret[..^seperator.Length];
            return ret;
        }

        public LSL_Integer llScriptDanger(LSL_Vector pos)
        {
            return World.LSLScriptDanger(m_host, pos) ? 1 : 0;
        }

        public void llDialog(LSL_Key avatar, LSL_String message, LSL_List buttons, int chat_channel)
        {
            IDialogModule dm = World.RequestModuleInterface<IDialogModule>();

            if (dm == null)
                return;

            if (!UUID.TryParse(avatar,out UUID av) || av.IsZero())
            {
                Error("llDialog", "First parameter must be a valid key");
                return;
            }

            if (!m_host.GetOwnerName(out string fname, out string lname))
                return;

            int length = buttons.Length;
            if (length < 1)
            {
                buttons.Add(new LSL_String("Ok"));
                length = 1;
            }
            else if (length > 12)
            {
                Error("llDialog", "No more than 12 buttons can be shown");
                return;
            }

            if (message.Length == 0)
            {
                Error("llDialog", "Empty message");
            }
            else if (Encoding.UTF8.GetByteCount(message) > 512)
            {
                Error("llDialog", "Message longer than 512 bytes");
            }

            string[] buts = new string[length];
            for (int i = 0; i < length; i++)
            {
                buts[i] = buttons.Data[i].ToString();
                if (buts[i].Length == 0)
                {
                    Error("llDialog", "Button label cannot be blank");
                    return;
                }
/*
                if (buttons.Data[i].ToString().Length > 24)
                {
                    Error("llDialog", "Button label cannot be longer than 24 characters");
                    return;
                }
*/
                buts[i] = buttons.Data[i].ToString();
            }

            dm.SendDialogToUser(
                av, m_host.Name, m_host.UUID, m_host.OwnerID, fname, lname,
                message, new UUID("00000000-0000-2222-3333-100000001000"), chat_channel, buts);

            ScriptSleep(m_sleepMsOnDialog);
        }

        public void llVolumeDetect(int detect)
        {

            if (!m_host.ParentGroup.IsDeleted)
                m_host.ParentGroup.ScriptSetVolumeDetect(detect != 0);
        }

        public void llRemoteLoadScript(string target, string name, int running, int start_param)
        {
            Deprecated("llRemoteLoadScript", "Use llRemoteLoadScriptPin instead");
            ScriptSleep(m_sleepMsOnRemoteLoadScript);
        }

        public void llSetRemoteScriptAccessPin(int pin)
        {
            m_host.ScriptAccessPin = pin;
        }

        public void llRemoteLoadScriptPin(string target, string name, int pin, int running, int start_param)
        {
            if (!UUID.TryParse(target, out UUID destId) || destId.IsZero())
            {
                Error("llRemoteLoadScriptPin", "invalid key '" + target + "'");
                return;
            }

            // target must be a different prim than the one containing the script
            if (m_host.UUID.Equals(destId))
            {
                return;
            }

            // copy the first script found with this inventory name
            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(name);

            // make sure the object is a script
            if (item == null || item.Type != 10)
            {
                Error("llRemoteLoadScriptPin", "Can't find script '" + name + "'");
                return;
            }
            if ((item.BasePermissions & (uint)PermissionMask.Copy) == 0)
            {
                Error("llRemoteLoadScriptPin", "No copy rights");
                return;
            }

            // the rest of the permission checks are done in RezScript, so check the pin there as well
            World.RezScriptFromPrim(item.ItemID, m_host, destId, pin, running, start_param);

            // this will cause the delay even if the script pin or permissions were wrong - seems ok
            ScriptSleep(m_sleepMsOnRemoteLoadScriptPin);
        }

        public void llOpenRemoteDataChannel()
        {
            IXMLRPC xmlrpcMod = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            if (xmlrpcMod != null && xmlrpcMod.IsEnabled())
            {
                UUID channelID = xmlrpcMod.OpenXMLRPCChannel(m_host.LocalId, m_item.ItemID, UUID.Zero);
                IXmlRpcRouter xmlRpcRouter = m_ScriptEngine.World.RequestModuleInterface<IXmlRpcRouter>();
                if (xmlRpcRouter != null)
                {
                    string ExternalHostName = m_ScriptEngine.World.RegionInfo.ExternalHostName;

                    xmlRpcRouter.RegisterNewReceiver(m_ScriptEngine.ScriptModule, channelID, m_host.UUID,
                                                     m_item.ItemID, String.Format("http://{0}:{1}/", ExternalHostName,
                                                                             xmlrpcMod.Port.ToString()));
                }
                object[] resobj = new object[]
                    {
                        new LSL_Integer(1),
                        new LSL_String(channelID.ToString()),
                        new LSL_String(ScriptBaseClass.NULL_KEY),
                        new LSL_String(String.Empty),
                        new LSL_Integer(0),
                        new LSL_String(String.Empty)
                    };
                m_ScriptEngine.PostScriptEvent(m_item.ItemID, new EventParams("remote_data", resobj,
                                                                         Array.Empty<DetectParams>()));
            }
            ScriptSleep(m_sleepMsOnOpenRemoteDataChannel);
        }

        public LSL_Key llSendRemoteData(string channel, string dest, int idata, string sdata)
        {
            IXMLRPC xmlrpcMod = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            ScriptSleep(m_sleepMsOnSendRemoteData);
            if (xmlrpcMod == null)
                return "";
            return (xmlrpcMod.SendRemoteData(m_host.LocalId, m_item.ItemID, channel, dest, idata, sdata)).ToString();
        }

        public void llRemoteDataReply(string channel, string message_id, string sdata, int idata)
        {
            IXMLRPC xmlrpcMod = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            xmlrpcMod?.RemoteDataReply(channel, message_id, sdata, idata);
            ScriptSleep(m_sleepMsOnRemoteDataReply);
        }

        public void llCloseRemoteDataChannel(string channel)
        {

            IXmlRpcRouter xmlRpcRouter = m_ScriptEngine.World.RequestModuleInterface<IXmlRpcRouter>();
            xmlRpcRouter?.UnRegisterReceiver(channel, m_item.ItemID);

            IXMLRPC xmlrpcMod = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            xmlrpcMod?.CloseXMLRPCChannel((UUID)channel);
            ScriptSleep(m_sleepMsOnCloseRemoteDataChannel);
        }

        public LSL_String llMD5String(string src, int nonce)
        {
            return Util.Md5Hash(String.Format("{0}:{1}", src, nonce.ToString()), Encoding.UTF8);
        }

        public LSL_String llSHA1String(string src)
        {
            return Util.SHA1Hash(src, Encoding.UTF8).ToLower();
        }

        public LSL_String llSHA256String(LSL_String input)
        {
            byte[] bytes;
            // Create a SHA256   
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array
                bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input.m_string));
            }
            return Util.bytesToLowcaseHexString(bytes);
        }

        protected static ObjectShapePacket.ObjectDataBlock SetPrimitiveBlockShapeParams(SceneObjectPart part, int holeshape, LSL_Vector cut, float hollow, LSL_Vector twist, byte profileshape, byte pathcurve)
        {
            float tempFloat;
            ObjectShapePacket.ObjectDataBlock shapeBlock = new();
            if (part is null || part.ParentGroup is null || part.ParentGroup.IsDeleted)
                return shapeBlock;

            if (holeshape != ScriptBaseClass.PRIM_HOLE_DEFAULT &&
                holeshape != ScriptBaseClass.PRIM_HOLE_CIRCLE &&
                holeshape != ScriptBaseClass.PRIM_HOLE_SQUARE &&
                holeshape != ScriptBaseClass.PRIM_HOLE_TRIANGLE)
            {
                holeshape = ScriptBaseClass.PRIM_HOLE_DEFAULT;
            }
            shapeBlock.PathCurve = pathcurve;
            shapeBlock.ProfileCurve = (byte)holeshape;          // Set the hole shape.
            shapeBlock.ProfileCurve += profileshape;            // Add in the profile shape.
            if (cut.x < 0f)
            {
                cut.x = 0f;
            }
            else if (cut.x > 1f)
            {
                cut.x = 1f;
            }
            if (cut.y < 0f)
            {
                cut.y = 0f;
            }
            else if (cut.y > 1f)
            {
                cut.y = 1f;
            }
            if (cut.y - cut.x < 0.02f)
            {
                cut.x = cut.y - 0.02f;
                if (cut.x < 0.0f)
                {
                    cut.x = 0.0f;
                    cut.y = 0.02f;
                }
            }
            shapeBlock.ProfileBegin = (ushort)(50000 * cut.x);
            shapeBlock.ProfileEnd = (ushort)(50000 * (1 - cut.y));
            if (hollow < 0f)
            {
                hollow = 0f;
            }
            // If the prim is a Cylinder, Prism, Sphere, Torus or Ring (or not a
            // Box or Tube) and the hole shape is a square, hollow is limited to
            // a max of 70%. The viewer performs its own check on this value but
            // we need to do it here also so llGetPrimitiveParams can have access
            // to the correct value.
            if (profileshape != (byte)ProfileCurve.Square &&
                holeshape == ScriptBaseClass.PRIM_HOLE_SQUARE)
            {
                if (hollow > 0.70f)
                {
                    hollow = 0.70f;
                }
            }
            // Otherwise, hollow is limited to 99%.
            else
            {
                if (hollow > 0.99f)
                {
                    hollow = 0.99f;
                }
            }
            shapeBlock.ProfileHollow = (ushort)(50000 * hollow);
            if (twist.x < -1.0f)
            {
                twist.x = -1.0f;
            }
            else if (twist.x > 1.0f)
            {
                twist.x = 1.0f;
            }
            if (twist.y < -1.0f)
            {
                twist.y = -1.0f;
            }
            else if (twist.y > 1.0f)
            {
                twist.y = 1.0f;
            }
            tempFloat = 100.0f * (float)twist.x;
            if (tempFloat >= 0)
                tempFloat += 0.5f;
            else
                tempFloat -= 0.5f;
            shapeBlock.PathTwistBegin = (sbyte)tempFloat;

            tempFloat = 100.0f * (float)twist.y;
            if (tempFloat >= 0)
                tempFloat += 0.5f;
            else
                tempFloat -= 0.5f;
            shapeBlock.PathTwist = (sbyte)tempFloat;

            shapeBlock.ObjectLocalID = part.LocalId;

            part.Shape.SculptEntry = false;
            return shapeBlock;
        }

        // Prim type box, cylinder and prism.
        protected static void SetPrimitiveShapeParams(SceneObjectPart part, int holeshape, LSL_Vector cut, float hollow, LSL_Vector twist, LSL_Vector taper_b, LSL_Vector topshear, byte profileshape, byte pathcurve)
        {
            if (part is null || part.ParentGroup is null || part.ParentGroup.IsDeleted)
                return;

            float tempFloat;                                    // Use in float expressions below to avoid byte cast precision issues.
            ObjectShapePacket.ObjectDataBlock shapeBlock;

            shapeBlock = SetPrimitiveBlockShapeParams(part, holeshape, cut, hollow, twist, profileshape, pathcurve);

            if (taper_b.x < 0f)
            {
                taper_b.x = 0f;
            }
            else if (taper_b.x > 2f)
            {
                taper_b.x = 2f;
            }
            if (taper_b.y < 0f)
            {
                taper_b.y = 0f;
            }
            else if (taper_b.y > 2f)
            {
                taper_b.y = 2f;
            }
            tempFloat = 100.0f * (2.0f - (float)taper_b.x);
            if (tempFloat >= 0)
                tempFloat += 0.5f;
            else
                tempFloat -= 0.5f;
            shapeBlock.PathScaleX = (byte)tempFloat;

            tempFloat = 100.0f * (2.0f - (float)taper_b.y);
            if (tempFloat >= 0)
                tempFloat += 0.5f;
            else
                tempFloat -= 0.5f;
            shapeBlock.PathScaleY = (byte)tempFloat;

            if (topshear.x < -0.5f)
            {
                topshear.x = -0.5f;
            }
            else if (topshear.x > 0.5f)
            {
                topshear.x = 0.5f;
            }
            if (topshear.y < -0.5f)
            {
                topshear.y = -0.5f;
            }
            else if (topshear.y > 0.5f)
            {
                topshear.y = 0.5f;
            }
            tempFloat = 100.0f * (float)topshear.x;
            if (tempFloat >= 0)
                tempFloat += 0.5f;
            else
                tempFloat -= 0.5f;
            shapeBlock.PathShearX = (byte)tempFloat;

            tempFloat = 100.0f * (float)topshear.y;
            if (tempFloat >= 0)
                tempFloat += 0.5f;
            else
                tempFloat -= 0.5f;
            shapeBlock.PathShearY = (byte)tempFloat;

            part.Shape.SculptEntry = false;
            part.UpdateShape(shapeBlock);
        }

        // Prim type sphere.
        protected static void SetPrimitiveShapeParams(SceneObjectPart part, int holeshape, LSL_Vector cut, float hollow, LSL_Vector twist, LSL_Vector dimple, byte profileshape, byte pathcurve)
        {
            if (part is null || part.ParentGroup is null || part.ParentGroup.IsDeleted)
                return;

            ObjectShapePacket.ObjectDataBlock shapeBlock;

            shapeBlock = SetPrimitiveBlockShapeParams(part, holeshape, cut, hollow, twist, profileshape, pathcurve);

            // profile/path swapped for a sphere
            shapeBlock.PathBegin = shapeBlock.ProfileBegin;
            shapeBlock.PathEnd = shapeBlock.ProfileEnd;

            shapeBlock.PathScaleX = 100;
            shapeBlock.PathScaleY = 100;

            if (dimple.x < 0f)
            {
                dimple.x = 0f;
            }
            else if (dimple.x > 1f)
            {
                dimple.x = 1f;
            }
            if (dimple.y < 0f)
            {
                dimple.y = 0f;
            }
            else if (dimple.y > 1f)
            {
                dimple.y = 1f;
            }
            if (dimple.y - dimple.x < 0.02f)
            {
                dimple.x = dimple.y - 0.02f;
                if (dimple.x < 0.0f)
                {
                    dimple.x = 0.0f;
                    dimple.y = 0.02f;
                }
            }
            shapeBlock.ProfileBegin = (ushort)(50000 * dimple.x);
            shapeBlock.ProfileEnd   = (ushort)(50000 * (1 - dimple.y));

            part.Shape.SculptEntry = false;
            part.UpdateShape(shapeBlock);
        }

        // Prim type torus, tube and ring.
        protected static void SetPrimitiveShapeParams(SceneObjectPart part, int holeshape, LSL_Vector cut, float hollow, LSL_Vector twist, LSL_Vector holesize, LSL_Vector topshear, LSL_Vector profilecut, LSL_Vector taper_a, float revolutions, float radiusoffset, float skew, byte profileshape, byte pathcurve)
        {
            if (part is null || part.ParentGroup is null || part.ParentGroup.IsDeleted)
                return;

            float tempFloat; // Use in float expressions below to avoid byte cast precision issues.
            ObjectShapePacket.ObjectDataBlock shapeBlock;

            shapeBlock = SetPrimitiveBlockShapeParams(part, holeshape, cut, hollow, twist, profileshape, pathcurve);

            // profile/path swapped for a torrus, tube, ring
            shapeBlock.PathBegin = shapeBlock.ProfileBegin;
            shapeBlock.PathEnd = shapeBlock.ProfileEnd;

            if (holesize.x < 0.01f)
            {
                holesize.x = 0.01f;
            }
            else if (holesize.x > 1f)
            {
                holesize.x = 1f;
            }
            tempFloat = 100.0f * (2.0f - (float)holesize.x) + 0.5f;
            shapeBlock.PathScaleX = (byte)tempFloat;

            if (holesize.y < 0.01f)
            {
                holesize.y = 0.01f;
            }
            else if (holesize.y > 0.5f)
            {
                holesize.y = 0.5f;
            }
            tempFloat = 100.0f * (2.0f - (float)holesize.y) + 0.5f;
            shapeBlock.PathScaleY = (byte)tempFloat;

            if (topshear.x < -0.5f)
            {
                topshear.x = -0.5f;
            }
            else if (topshear.x > 0.5f)
            {
                topshear.x = 0.5f;
            }
            tempFloat = (float)(100.0d * topshear.x);
            if (tempFloat >= 0)
                tempFloat += 0.5f;
            else
                tempFloat -= 0.5f;
            shapeBlock.PathShearX = (byte)tempFloat;

            if (topshear.y < -0.5f)
            {
                topshear.y = -0.5f;
            }
            else if (topshear.y > 0.5f)
            {
                topshear.y = 0.5f;
            }
            tempFloat = (float)(100.0d * topshear.y);
            if (tempFloat >= 0)
                tempFloat += 0.5f;
            else
                tempFloat -= 0.5f;
            shapeBlock.PathShearY = (byte)tempFloat;

            if (profilecut.x < 0f)
            {
                profilecut.x = 0f;
            }
            else if (profilecut.x > 1f)
            {
                profilecut.x = 1f;
            }
            if (profilecut.y < 0f)
            {
                profilecut.y = 0f;
            }
            else if (profilecut.y > 1f)
            {
                profilecut.y = 1f;
            }
            if (profilecut.y - profilecut.x < 0.02f)
            {
                profilecut.x = profilecut.y - 0.02f;
                if (profilecut.x < 0.0f)
                {
                    profilecut.x = 0.0f;
                    profilecut.y = 0.02f;
                }
            }
            shapeBlock.ProfileBegin = (ushort)(50000 * profilecut.x);
            shapeBlock.ProfileEnd = (ushort)(50000 * (1 - profilecut.y));
            if (taper_a.x < -1f)
            {
                taper_a.x = -1f;
            }
            if (taper_a.x > 1f)
            {
                taper_a.x = 1f;
            }
            tempFloat = 100.0f * (float)taper_a.x;
            if (tempFloat >= 0)
                tempFloat += 0.5f;
            else
                tempFloat -= 0.5f;
            shapeBlock.PathTaperX = (sbyte)tempFloat;

            if (taper_a.y < -1f)
            {
                taper_a.y = -1f;
            }
            else if (taper_a.y > 1f)
            {
                taper_a.y = 1f;
            }
            tempFloat = 100.0f * (float)taper_a.y;
            if (tempFloat >= 0)
                tempFloat += 0.5f;
            else
                tempFloat -= 0.5f;
            shapeBlock.PathTaperY = (sbyte)tempFloat;

            if (revolutions < 1f)
            {
                revolutions = 1f;
            }
            if (revolutions > 4f)
            {
                revolutions = 4f;
            }
            tempFloat = 66.66667f * (revolutions - 1.0f) + 0.5f;
            shapeBlock.PathRevolutions = (byte)tempFloat;
            // limits on radiusoffset depend on revolutions and hole size (how?) seems like the maximum range is 0 to 1
            if (radiusoffset < 0f)
            {
                radiusoffset = 0f;
            }
            if (radiusoffset > 1f)
            {
                radiusoffset = 1f;
            }
            tempFloat = 100.0f * radiusoffset + 0.5f;
            shapeBlock.PathRadiusOffset = (sbyte)tempFloat;
            if (skew < -0.95f)
            {
                skew = -0.95f;
            }
            if (skew > 0.95f)
            {
                skew = 0.95f;
            }
            tempFloat = 100.0f * skew;
            if (tempFloat >= 0)
                tempFloat += 0.5f;
            else
                tempFloat -= 0.5f;
            shapeBlock.PathSkew = (sbyte)tempFloat;

            part.Shape.SculptEntry = false;
            part.UpdateShape(shapeBlock);
        }

        // Prim type sculpt.
        protected void SetPrimitiveShapeSculptParams(SceneObjectPart part, string map, int type, byte pathcurve)
        {
            bool partIsMesh = part.Shape.SculptEntry && (part.Shape.SculptType & ScriptBaseClass.PRIM_SCULPT_TYPE_MASK) == ScriptBaseClass.PRIM_SCULPT_TYPE_MESH;

            int base_type = type & ScriptBaseClass.PRIM_SCULPT_TYPE_MASK;
            if(base_type == ScriptBaseClass.PRIM_SCULPT_TYPE_MESH)
            {
                if(!partIsMesh)
                    return;

                bool animeshEnable = (type & ScriptBaseClass.PRIM_SCULPT_FLAG_ANIMESH) != 0;
                if (animeshEnable != part.Shape.AnimeshEnabled)
                {
                    part.Shape.AnimeshEnabled = animeshEnable;
                    part.ParentGroup.HasGroupChanged = true;
                    part.TriggerScriptChangedEvent(Changed.SHAPE);
                    part.ScheduleFullAnimUpdate();
                }
                return;
            }
            if (base_type > 5)
                return;

            if (partIsMesh)
                return;

            type &= ~ScriptBaseClass.PRIM_SCULPT_FLAG_ANIMESH;

            if (!UUID.TryParse(map, out UUID sculptId))
                sculptId = ScriptUtils.GetAssetIdFromItemName(m_host, map, (int)AssetType.Texture);

            if (sculptId.IsZero())
                return;

            part.Shape.SetSculptProperties((byte)type, sculptId);
            part.Shape.SculptEntry = true;

            ObjectShapePacket.ObjectDataBlock shapeBlock = new()
            {
                PathCurve = pathcurve,
                ObjectLocalID = part.LocalId,
                PathScaleX = 100,
                PathScaleY = 150
            };
            part.UpdateShape(shapeBlock);
        }

        public void llSetPrimitiveParams(LSL_List rules)
        {
            SetLinkPrimParams(ScriptBaseClass.LINK_THIS, rules, "llSetPrimitiveParams");

            ScriptSleep(m_sleepMsOnSetPrimitiveParams);
        }

        public void llSetLinkPrimitiveParams(int linknumber, LSL_List rules)
        {
            SetLinkPrimParams(linknumber, rules, "llSetLinkPrimitiveParams");

            ScriptSleep(m_sleepMsOnSetLinkPrimitiveParams);
        }

        public void llSetLinkPrimitiveParamsFast(int linknumber, LSL_List rules)
        {
            SetLinkPrimParams(linknumber, rules, "llSetLinkPrimitiveParamsFast");
        }

        private void SetLinkPrimParams(int linknumber, LSL_List rules, string originFunc)
        {
            List<object> parts = new();
            List<SceneObjectPart> prims = GetLinkParts(linknumber);
            List<ScenePresence> avatars = GetLinkAvatars(linknumber);
            foreach (SceneObjectPart p in prims)
                parts.Add(p);
            foreach (ScenePresence p in avatars)
                parts.Add(p);

            LSL_List remaining = new();
            uint rulesParsed = 0;

            if (parts.Count > 0)
            {
                foreach (object part in parts)
                {
                    if (part is SceneObjectPart sop)
                        remaining = SetPrimParams(sop, rules, originFunc, ref rulesParsed);
                    else
                        remaining = SetPrimParams((ScenePresence)part, rules, originFunc, ref rulesParsed);
                }

                while (remaining.Length > 2)
                {
                    linknumber = remaining.GetIntegerItem(0);
                    rules = remaining.GetSublist(1, -1);
                    parts.Clear();
                    prims = GetLinkParts(linknumber);
                    avatars = GetLinkAvatars(linknumber);
                    foreach (SceneObjectPart p in prims)
                        parts.Add(p);
                    foreach (ScenePresence p in avatars)
                        parts.Add(p);

                    remaining = new LSL_List();
                    foreach (object part in parts)
                    {
                        if (part is SceneObjectPart sop)
                            remaining = SetPrimParams(sop, rules, originFunc, ref rulesParsed);
                        else
                            remaining = SetPrimParams((ScenePresence)part, rules, originFunc, ref rulesParsed);
                    }
                }
            }
        }

        public void llSetKeyframedMotion(LSL_List frames, LSL_List options)
        {
            SceneObjectGroup group = m_host.ParentGroup;

            if (group.RootPart.PhysActor != null && group.RootPart.PhysActor.IsPhysical)
                return;
            if (group.IsAttachment)
                return;

            if (frames.Data.Length > 0) // We are getting a new motion
            {
                group.RootPart.KeyframeMotion?.Delete();
                group.RootPart.KeyframeMotion = null;

                int idx = 0;

                KeyframeMotion.PlayMode mode = KeyframeMotion.PlayMode.Forward;
                KeyframeMotion.DataFormat data = KeyframeMotion.DataFormat.Translation | KeyframeMotion.DataFormat.Rotation;

                while (idx < options.Data.Length)
                {
                    int option = options.GetIntegerItem(idx++);
                    int remain = options.Data.Length - idx;

                    switch (option)
                    {
                        case ScriptBaseClass.KFM_MODE:
                            if (remain < 1)
                                break;
                            int modeval = options.GetIntegerItem(idx++);
                            switch(modeval)
                            {
                                case ScriptBaseClass.KFM_FORWARD:
                                    mode = KeyframeMotion.PlayMode.Forward;
                                    break;
                                case ScriptBaseClass.KFM_REVERSE:
                                    mode = KeyframeMotion.PlayMode.Reverse;
                                    break;
                                case ScriptBaseClass.KFM_LOOP:
                                    mode = KeyframeMotion.PlayMode.Loop;
                                    break;
                                case ScriptBaseClass.KFM_PING_PONG:
                                    mode = KeyframeMotion.PlayMode.PingPong;
                                    break;
                            }
                            break;
                        case ScriptBaseClass.KFM_DATA:
                            if (remain < 1)
                                break;
                            int dataval = options.GetIntegerItem(idx++);
                            data = (KeyframeMotion.DataFormat)dataval;
                            break;
                    }
                }

                group.RootPart.KeyframeMotion = new KeyframeMotion(group, mode, data);

                idx = 0;

                int elemLength = 2;
                if (data == (KeyframeMotion.DataFormat.Translation | KeyframeMotion.DataFormat.Rotation))
                    elemLength = 3;

                List<KeyframeMotion.Keyframe> keyframes = new();
                bool hasTranslation = (data & KeyframeMotion.DataFormat.Translation) != 0;
                bool hasRotation = (data & KeyframeMotion.DataFormat.Rotation) != 0;
                while (idx < frames.Data.Length)
                {
                    int remain = frames.Data.Length - idx;

                    if (remain < elemLength)
                        break;

                    KeyframeMotion.Keyframe frame = new()
                    {
                        Position = null,
                        Rotation = null
                    };

                    if (hasTranslation)
                    {
                        LSL_Types.Vector3 tempv = frames.GetVector3Item(idx++);
                        frame.Position = new Vector3((float)tempv.x, (float)tempv.y, (float)tempv.z);
                    }
                    if (hasRotation)
                    {
                        LSL_Types.Quaternion tempq = frames.GetQuaternionItem(idx++);
                        Quaternion q = new((float)tempq.x, (float)tempq.y, (float)tempq.z, (float)tempq.s);
                        frame.Rotation = q;
                    }

                    float tempf = frames.GetStrictFloatItem(idx++);
                    frame.TimeMS = (int)(tempf * 1000.0f);

                    keyframes.Add(frame);
                }

                group.RootPart.KeyframeMotion.SetKeyframes(keyframes.ToArray());
                group.RootPart.KeyframeMotion.Start();
            }
            else
            {
                if (group.RootPart.KeyframeMotion == null)
                    return;

                if (options.Data.Length == 0)
                {
                    group.RootPart.KeyframeMotion.Stop();
                    return;
                }

                int idx = 0;

                while (idx < options.Data.Length)
                {
                    int option = options.GetIntegerItem(idx++);

                    switch (option)
                    {
                        case ScriptBaseClass.KFM_COMMAND:
                            int cmd = options.GetIntegerItem(idx++);
                            switch (cmd)
                            {
                                case ScriptBaseClass.KFM_CMD_PLAY:
                                    group.RootPart.KeyframeMotion.Start();
                                    break;
                                case ScriptBaseClass.KFM_CMD_STOP:
                                    group.RootPart.KeyframeMotion.Stop();
                                    break;
                                case ScriptBaseClass.KFM_CMD_PAUSE:
                                    group.RootPart.KeyframeMotion.Pause();
                                    break;
                            }
                            break;
                    }
                }
            }
        }

        public LSL_List llGetPhysicsMaterial()
        {
            LSL_List result = new();

            result.Add(new LSL_Float(m_host.GravityModifier));
            result.Add(new LSL_Float(m_host.Restitution));
            result.Add(new LSL_Float(m_host.Friction));
            result.Add(new LSL_Float(m_host.Density));

            return result;
        }

        private static void SetPhysicsMaterial(SceneObjectPart part, int material_bits,
                float material_density, float material_friction,
                float material_restitution, float material_gravity_modifier)
        {
            ExtraPhysicsData physdata = new()
            {
                PhysShapeType = (PhysShapeType)part.PhysicsShapeType,
                Density = part.Density,
                Friction = part.Friction,
                Bounce = part.Restitution,
                GravitationModifier = part.GravityModifier
            };

            if ((material_bits & ScriptBaseClass.DENSITY) != 0)
                physdata.Density = material_density;
            if ((material_bits & ScriptBaseClass.FRICTION) != 0)
                physdata.Friction = material_friction;
            if ((material_bits & ScriptBaseClass.RESTITUTION) != 0)
                physdata.Bounce = material_restitution;
            if ((material_bits & ScriptBaseClass.GRAVITY_MULTIPLIER) != 0)
                physdata.GravitationModifier = material_gravity_modifier;

            part.UpdateExtraPhysics(physdata);
        }

        public void llSetPhysicsMaterial(int material_bits,
                LSL_Float material_gravity_modifier, LSL_Float material_restitution,
                LSL_Float material_friction, LSL_Float material_density)
        {
            SetPhysicsMaterial(m_host, material_bits, (float)material_density, (float)material_friction, (float)material_restitution, (float)material_gravity_modifier);
        }

        // vector up using libomv (c&p from sop )
        // vector up rotated by r
        private static Vector3 Zrot(Quaternion r)
        {
            double x, y, z, m;

            m = r.X * r.X + r.Y * r.Y + r.Z * r.Z + r.W * r.W;
            if (Math.Abs(1.0 - m) > 0.000001)
            {
                m = 1.0 / Math.Sqrt(m);
                r.X *= (float)m;
                r.Y *= (float)m;
                r.Z *= (float)m;
                r.W *= (float)m;
            }

            x = 2 * (r.X * r.Z + r.Y * r.W);
            y = 2 * (-r.X * r.W + r.Y * r.Z);
            z = -r.X * r.X - r.Y * r.Y + r.Z * r.Z + r.W * r.W;

            return new Vector3((float)x, (float)y, (float)z);
        }

        protected LSL_List SetPrimParams(SceneObjectPart part, LSL_List rules, string originFunc, ref uint rulesParsed)
        {
            if (part is null || part.ParentGroup is null || part.ParentGroup.IsDeleted)
                return new LSL_List();

            int idx = 0;
            int idxStart = 0;

            bool positionChanged = false;
            bool materialChanged = false;
            LSL_Vector currentPosition = GetPartLocalPos(part);

            try
            {
                while (idx < rules.Length)
                {
                    ++rulesParsed;
                    int code = rules.GetIntegerItem(idx++);

                    int remain = rules.Length - idx;
                    idxStart = idx;

                    int face;
                    LSL_Vector v;

                    switch (code)
                    {
                        case ScriptBaseClass.PRIM_POSITION:
                        case ScriptBaseClass.PRIM_POS_LOCAL:
                            if (remain < 1)
                                return new LSL_List();

                            try
                            {
                                v = rules.GetVector3Item(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                if(code == ScriptBaseClass.PRIM_POSITION)
                                    Error(originFunc, string.Format("Error running rule #{0} -> PRIM_POSITION: arg #{1} - parameter 1 must be vector", rulesParsed, idx - idxStart - 1));
                                else
                                    Error(originFunc, string.Format("Error running rule #{0} -> PRIM_POS_LOCAL: arg #{1} - parameter 1 must be vector", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            if (part.IsRoot && !part.ParentGroup.IsAttachment)
                                currentPosition = GetSetPosTarget(part, v, currentPosition, true);
                            else
                                currentPosition = GetSetPosTarget(part, v, currentPosition, false);
                            positionChanged = true;

                            break;
                        case ScriptBaseClass.PRIM_SIZE:
                            if (remain < 1)
                                return new LSL_List();

                            v=rules.GetVector3Item(idx++);
                            SetScale(part, v);

                            break;
                        case ScriptBaseClass.PRIM_ROTATION:
                            if (remain < 1)
                                return new LSL_List();
                            LSL_Rotation q;
                            try
                            {
                                q = rules.GetQuaternionItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_ROTATION: arg #{1} - parameter 1 must be rotation", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            // try to let this work as in SL...
                            if (part.ParentID == 0 || (part.ParentGroup != null && part == part.ParentGroup.RootPart))
                            {
                                // special case: If we are root, rotate complete SOG to new rotation
                                SetRot(part, q);
                            }
                            else
                            {
                                // we are a child. The rotation values will be set to the one of root modified by rot, as in SL. Don't ask.
                                SceneObjectPart rootPart = part.ParentGroup.RootPart;
                                SetRot(part, rootPart.RotationOffset * (Quaternion)q);
                            }

                            break;

                        case ScriptBaseClass.PRIM_TYPE:
                            if (remain < 3)
                                return new LSL_List();

                            try
                            {
                                code = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE: arg #{1} - parameter 1 must be integer", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            remain = rules.Length - idx;
                            float hollow;
                            LSL_Vector twist;
                            LSL_Vector taper_b;
                            LSL_Vector topshear;
                            float revolutions;
                            float radiusoffset;
                            float skew;
                            LSL_Vector holesize;
                            LSL_Vector profilecut;

                            switch (code)
                            {
                                case ScriptBaseClass.PRIM_TYPE_BOX:
                                    if (remain < 6)
                                        return new LSL_List();

                                    try
                                    {
                                        face = rules.GetIntegerItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_BOX: arg #{1} - parameter 2 must be integer", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        v = rules.GetVector3Item(idx++); // cut
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_BOX: arg #{1} - parameter 3 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        hollow = rules.GetStrictFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_BOX: arg #{1} - parameter 4 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        twist = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_BOX: arg #{1} - parameter 5 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        taper_b = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_BOX: arg #{1} - parameter 6 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        topshear = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_BOX: arg #{1} - parameter 7 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }

                                    SetPrimitiveShapeParams(part, face, v, hollow, twist, taper_b, topshear,
                                        (byte)ProfileShape.Square, (byte)Extrusion.Straight);
                                    break;

                                case ScriptBaseClass.PRIM_TYPE_CYLINDER:
                                    if (remain < 6)
                                        return new LSL_List();

                                    try
                                    {
                                        face = rules.GetIntegerItem(idx++); // holeshape
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_CYLINDER: arg #{1} - parameter 3 must be integer", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        v = rules.GetVector3Item(idx++); // cut
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_CYLINDER: arg #{1} - parameter 4 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        hollow = rules.GetStrictFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_CYLINDER: arg #{1} - parameter 5 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        twist = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_CYLINDER: arg #{1} - parameter 6 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        taper_b = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_CYLINDER: arg #{1} - parameter 7 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        topshear = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_CYLINDER: arg #{1} - parameter 8 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    SetPrimitiveShapeParams(part, face, v, hollow, twist, taper_b, topshear,
                                        (byte)ProfileShape.Circle, (byte)Extrusion.Straight);
                                    break;

                                case ScriptBaseClass.PRIM_TYPE_PRISM:
                                    if (remain < 6)
                                        return new LSL_List();

                                    try
                                    {
                                        face = rules.GetIntegerItem(idx++); // holeshape
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_PRISM: arg #{1} - parameter 3 must be integer", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        v = rules.GetVector3Item(idx++); //cut
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_PRISM: arg #{1} - parameter 4 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        hollow = rules.GetStrictFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_PRISM: arg #{1} - parameter 5 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        twist = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_PRISM: arg #{1} - parameter 6 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        taper_b = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_PRISM: arg #{1} - parameter 7 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        topshear = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_PRISM: arg #{1} - parameter 8 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    SetPrimitiveShapeParams(part, face, v, hollow, twist, taper_b, topshear,
                                        (byte)ProfileShape.EquilateralTriangle, (byte)Extrusion.Straight);
                                    break;

                                case ScriptBaseClass.PRIM_TYPE_SPHERE:
                                    if (remain < 5)
                                        return new LSL_List();

                                    try
                                    {
                                        face = rules.GetIntegerItem(idx++); // holeshape
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_SPHERE: arg #{1} - parameter 3 must be integer", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        v = rules.GetVector3Item(idx++); // cut
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_SPHERE: arg #{1} - parameter 4 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        hollow = rules.GetStrictFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_SPHERE: arg #{1} - parameter 5 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        twist = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_SPHERE: arg #{1} - parameter 6 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        taper_b = rules.GetVector3Item(idx++); // dimple
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_SPHERE: arg #{1} - parameter 7 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    SetPrimitiveShapeParams(part, face, v, hollow, twist, taper_b,
                                        (byte)ProfileShape.HalfCircle, (byte)Extrusion.Curve1);
                                    break;

                                case ScriptBaseClass.PRIM_TYPE_TORUS:
                                    if (remain < 11)
                                        return new LSL_List();

                                    try
                                    {
                                        face = rules.GetIntegerItem(idx++); // holeshape
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TORUS: arg #{1} - parameter 3 must be integer", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        v = rules.GetVector3Item(idx++); //cut
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TORUS: arg #{1} - parameter 4 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        hollow = rules.GetStrictFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TORUS: arg #{1} - parameter 5 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        twist = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TORUS: arg #{1} - parameter 6 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        holesize = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TORUS: arg #{1} - parameter 7 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        topshear = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TORUS: arg #{1} - parameter 8 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        profilecut = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TORUS: arg #{1} - parameter 9 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        taper_b = rules.GetVector3Item(idx++); // taper_a
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TORUS: arg #{1} - parameter 10 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        revolutions = rules.GetStrictFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TORUS: arg #{1} - parameter 11 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        radiusoffset = rules.GetStrictFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TORUS: arg #{1} - parameter 12 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        skew = rules.GetStrictFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TORUS: arg #{1} - parameter 13 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    SetPrimitiveShapeParams(part, face, v, hollow, twist, holesize, topshear, profilecut, taper_b,
                                        revolutions, radiusoffset, skew, (byte)ProfileShape.Circle, (byte)Extrusion.Curve1);
                                    break;

                                case ScriptBaseClass.PRIM_TYPE_TUBE:
                                    if (remain < 11)
                                        return new LSL_List();

                                    try
                                    {
                                        face = rules.GetIntegerItem(idx++); // holeshape
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TUBE: arg #{1} - parameter 3 must be integer", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        v = rules.GetVector3Item(idx++); //cut
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TUBE: arg #{1} - parameter 4 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        hollow = rules.GetStrictFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TUBE: arg #{1} - parameter 5 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        twist = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TUBE: arg #{1} - parameter 6 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        holesize = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TUBE: arg #{1} - parameter 7 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        topshear = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TUBE: arg #{1} - parameter 8 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        profilecut = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TUBE: arg #{1} - parameter 9 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        taper_b = rules.GetVector3Item(idx++); // taper_a
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TUBE: arg #{1} - parameter 10 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        revolutions = rules.GetStrictFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TUBE: arg #{1} - parameter 11 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        radiusoffset = rules.GetStrictFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TUBE: arg #{1} - parameter 12 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        skew = rules.GetStrictFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TUBE: arg #{1} - parameter 13 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    SetPrimitiveShapeParams(part, face, v, hollow, twist, holesize, topshear, profilecut, taper_b,
                                        revolutions, radiusoffset, skew, (byte)ProfileShape.Square, (byte)Extrusion.Curve1);
                                    break;

                                case ScriptBaseClass.PRIM_TYPE_RING:
                                    if (remain < 11)
                                        return new LSL_List();

                                    try
                                    {
                                        face = rules.GetIntegerItem(idx++); // holeshape
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_RING: arg #{1} - parameter 3 must be integer", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        v = rules.GetVector3Item(idx++); //cut
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_RING: arg #{1} - parameter 4 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        hollow = rules.GetStrictFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_RING: arg #{1} - parameter 5 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        twist = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_RING: arg #{1} - parameter 6 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        holesize = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_RING: arg #{1} - parameter 7 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        topshear = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_RING: arg #{1} - parameter 8 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        profilecut = rules.GetVector3Item(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_RING: arg #{1} - parameter 9 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        taper_b = rules.GetVector3Item(idx++); // taper_a
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_RING: arg #{1} - parameter 10 must be vector", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        revolutions = rules.GetStrictFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_RING: arg #{1} - parameter 11 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        radiusoffset = rules.GetStrictFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_RING: arg #{1} - parameter 12 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        skew = rules.GetStrictFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_RING: arg #{1} - parameter 13 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    SetPrimitiveShapeParams(part, face, v, hollow, twist, holesize, topshear, profilecut, taper_b,
                                        revolutions, radiusoffset, skew, (byte)ProfileShape.EquilateralTriangle, (byte)Extrusion.Curve1);
                                    break;

                                case ScriptBaseClass.PRIM_TYPE_SCULPT:
                                    if (remain < 2)
                                        return new LSL_List();

                                    string map = rules.Data[idx++].ToString();
                                    try
                                    {
                                        face = rules.GetIntegerItem(idx++); // type
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_SCULPT: arg #{1} - parameter 4 must be integer", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    SetPrimitiveShapeSculptParams(part, map, face, (byte)Extrusion.Curve1);
                                    break;
                            }

                            break;

                        case ScriptBaseClass.PRIM_TEXTURE:
                            if (remain < 5)
                                return new LSL_List();

                            face=rules.GetIntegerItem(idx++);
                            string tex;
                            LSL_Vector repeats;
                            LSL_Vector offsets;
                            double rotation;

                            tex = rules.Data[idx++].ToString();
                            try
                            {
                                repeats = rules.GetVector3Item(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TEXTURE: arg #{1} - parameter 3 must be vector", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                offsets = rules.GetVector3Item(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TEXTURE: arg #{1} - parameter 4 must be vector", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                rotation = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TEXTURE: arg #{1} - parameter 5 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            SetTextureParams(part, tex, repeats.x, repeats.y, offsets.x, offsets.y, rotation, face);
                            break;

                        case ScriptBaseClass.PRIM_COLOR:
                            if (remain < 3)
                                return new LSL_List();

                            LSL_Vector color;
                            float alpha;

                            try
                            {
                                face = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_COLOR: arg #{1} - parameter 2 must be integer", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                color = rules.GetVector3Item(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_COLOR: arg #{1} - parameter 3 must be vector", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                alpha = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_COLOR: arg #{1} - parameter 4 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            part.SetFaceColorAlpha(face, color, alpha);

                            break;

                        case ScriptBaseClass.PRIM_FLEXIBLE:
                            if (remain < 7)
                                return new LSL_List();
                            bool flexi;
                            int softness;
                            float gravity;
                            float friction;
                            float wind;
                            float tension;
                            LSL_Vector force;

                            try
                            {
                                flexi = rules.GetIntegerItem(idx++) != 0;
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_FLEXIBLE: arg #{1} - parameter 2 must be integer", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                softness = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_FLEXIBLE: arg #{1} - parameter 3 must be integer", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                gravity = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_FLEXIBLE: arg #{1} - parameter 4 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                friction = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_FLEXIBLE: arg #{1} - parameter 5 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                wind = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_FLEXIBLE: arg #{1} - parameter 6 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                tension = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_FLEXIBLE: arg #{1} - parameter 7 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                force = rules.GetVector3Item(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_FLEXIBLE: arg #{1} - parameter 8 must be vector", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            SetFlexi(part, flexi, softness, gravity, friction, wind, tension, force);

                            break;

                        case ScriptBaseClass.PRIM_POINT_LIGHT:
                            if (remain < 5)
                                return new LSL_List();
                            bool light;
                            LSL_Vector lightcolor;
                            float intensity;
                            float radius;
                            float falloff;

                            try
                            {
                                light = rules.GetIntegerItem(idx++) != 0;
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_POINT_LIGHT: arg #{1} - parameter 2 must be integer", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                lightcolor = rules.GetVector3Item(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_POINT_LIGHT: arg #{1} - parameter 3 must be vector", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                intensity = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_POINT_LIGHT: arg #{1} - parameter 4 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                radius = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_POINT_LIGHT: arg #{1} - parameter 5 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                falloff = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_POINT_LIGHT: arg #{1} - parameter 6 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            SetPointLight(part, light, lightcolor, intensity, radius, falloff);

                            break;

                        case ScriptBaseClass.PRIM_REFLECTION_PROBE:
                            if (remain < 4)
                                return new LSL_List();

                            bool reflection_probe_active;
                            float reflection_probe_ambiance;
                            float reflection_probe_clip_distance;
                            int reflection_probe_flags;

                            try
                            {
                                reflection_probe_active = rules.GetIntegerItem(idx++) != 0;
                            }
                            catch (InvalidCastException)
                            {
                                Error(originFunc, $"Error running rule #{rulesParsed} -> PRIM_REFLECTION_PROBE: arg #{idx - idxStart - 1} - parameter 1 (active) must be integer");
                                return new LSL_List();
                            }
                            try
                            {
                                reflection_probe_ambiance = rules.GetStrictFloatItem(idx++);
                            }
                            catch (InvalidCastException)
                            {
                                Error(originFunc, $"Error running rule #{rulesParsed} -> PRIM_REFLECTION_PROBE: arg #{idx - idxStart - 1} - parameter 2 (ambiance) must be float");
                                return new LSL_List();
                            }
                            try
                            {
                                reflection_probe_clip_distance = rules.GetStrictFloatItem(idx++);
                            }
                            catch (InvalidCastException)
                            {
                                Error(originFunc, $"Error running rule #{rulesParsed} -> PRIM_REFLECTION_PROBE: arg #{idx - idxStart - 1} - parameter 3 (clip_distance) must be float");
                                return new LSL_List();
                            }
                            try
                            {
                                reflection_probe_flags = rules.GetIntegerItem(idx++);
                            }
                            catch (InvalidCastException)
                            {
                                Error(originFunc, $"Error running rule #{rulesParsed} -> PRIM_REFLECTION_PROBE: arg #{idx - idxStart - 1} - parameter 4 (flags) must be integer");
                                return new LSL_List();
                            }

                            bool probechanged;
                            if(reflection_probe_active)
                            {
                                probechanged = part.Shape.ReflectionProbe is null;
                                if(probechanged)
                                    part.Shape.ReflectionProbe = new();

                                reflection_probe_ambiance = Utils.Clamp(reflection_probe_ambiance, 0f, 100f);
                                probechanged |= part.Shape.ReflectionProbe.Ambiance != reflection_probe_ambiance;
                                part.Shape.ReflectionProbe.Ambiance = reflection_probe_ambiance;

                                reflection_probe_clip_distance = Utils.Clamp(reflection_probe_clip_distance, 0f, 1024f);
                                probechanged |= part.Shape.ReflectionProbe.ClipDistance != reflection_probe_clip_distance;
                                part.Shape.ReflectionProbe.ClipDistance = reflection_probe_clip_distance;

                                probechanged |= part.Shape.ReflectionProbe.Flags != reflection_probe_flags;
                                part.Shape.ReflectionProbe.Flags = (byte)reflection_probe_flags;
                            }
                            else
                            {
                                probechanged = part.Shape.ReflectionProbe is not null;
                                part.Shape.ReflectionProbe = null;
                            }

                            if(probechanged)
                            {
                                part.ParentGroup.HasGroupChanged = true;
                                part.ScheduleFullUpdate();
                            }
                            break;

                        case ScriptBaseClass.PRIM_GLOW:
                            if (remain < 2)
                                return new LSL_List();

                            float glow;

                            try
                            {
                                face = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_GLOW: arg #{1} - parameter 2 must be integer", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                glow = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_GLOW: arg #{1} - parameter 3 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            SetGlow(part, face, glow);

                            break;

                        case ScriptBaseClass.PRIM_BUMP_SHINY:
                            if (remain < 3)
                                return new LSL_List();

                            int shiny;
                            Bumpiness bump;

                            try
                            {
                                face = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_BUMP_SHINY: arg #{1} - parameter 2 must be integer", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                shiny = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_BUMP_SHINY: arg #{1} - parameter 3 must be integer", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                bump = (Bumpiness)rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_BUMP_SHINY: arg #{1} - parameter 4 must be integer", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            SetShiny(part, face, shiny, bump);

                            break;

                        case ScriptBaseClass.PRIM_FULLBRIGHT:
                            if (remain < 2)
                                return new LSL_List();
                            bool st;

                            try
                            {
                                face = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_FULLBRIGHT: arg #{1} - parameter 2 must be integer", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }
                            try
                            {
                                st = rules.GetIntegerItem(idx++) != 0;
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_FULLBRIGHT: arg #{1} - parameter 4 must be integer", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            SetFullBright(part, face , st);
                            break;

                        case ScriptBaseClass.PRIM_MATERIAL:
                            if (remain < 1)
                                return new LSL_List();
                            int mat;

                            try
                            {
                                mat = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_MATERIAL: arg #{1} - parameter 2 must be integer", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }
                            if (mat < 0 || mat > 7)
                                return new LSL_List();

                            part.Material = Convert.ToByte(mat);
                            break;

                        case ScriptBaseClass.PRIM_PHANTOM:
                            if (remain < 1)
                                return new LSL_List();

                            string ph = rules.Data[idx++].ToString();
                            part.ParentGroup.ScriptSetPhantomStatus(ph.Equals("1"));

                            break;

                        case ScriptBaseClass.PRIM_PHYSICS:
                           if (remain < 1)
                                return new LSL_List();
                            string phy = rules.Data[idx++].ToString();
                            part.ScriptSetPhysicsStatus(phy.Equals("1"));
                            break;

                        case ScriptBaseClass.PRIM_PHYSICS_SHAPE_TYPE:
                            if (remain < 1)
                                return new LSL_List();

                            int shape_type;

                            try
                            {
                                shape_type = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_PHYSICS_SHAPE_TYPE: arg #{1} - parameter 2 must be integer", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            ExtraPhysicsData physdata = new()
                            {
                                Density = part.Density,
                                Bounce = part.Restitution,
                                GravitationModifier = part.GravityModifier,
                                PhysShapeType = (PhysShapeType)shape_type
                            };

                            part.UpdateExtraPhysics(physdata);

                            break;

                        case ScriptBaseClass.PRIM_PHYSICS_MATERIAL:
                            if (remain < 5)
                                return new LSL_List();

                            int material_bits = rules.GetIntegerItem(idx++);
                            float material_density = rules.GetFloatItem(idx++);
                            float material_friction = rules.GetFloatItem(idx++);
                            float material_restitution = rules.GetFloatItem(idx++);
                            float material_gravity_modifier = rules.GetFloatItem(idx++);

                            SetPhysicsMaterial(part, material_bits, material_density, material_friction, material_restitution, material_gravity_modifier);

                            break;

                        case ScriptBaseClass.PRIM_TEMP_ON_REZ:
                            if (remain < 1)
                                return new LSL_List();
                            string temp = rules.Data[idx++].ToString();

                            part.ParentGroup.ScriptSetTemporaryStatus(temp.Equals("1"));

                            break;

                        case ScriptBaseClass.PRIM_TEXGEN:
                            if (remain < 2)
                                return new LSL_List();
                                //face,type
                            int style;

                            try
                            {
                                face = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TEXGEN: arg #{1} - parameter 2 must be integer", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }
                            try
                            {
                                style = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TEXGEN: arg #{1} - parameter 3 must be integer", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }
                            SetTexGen(part, face, style);
                            break;
                        case ScriptBaseClass.PRIM_TEXT:
                            if (remain < 3)
                                return new LSL_List();
                            string primText;
                            LSL_Vector primTextColor;
                            float primTextAlpha;

                            try
                            {
                                primText = rules.GetStrictStringItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TEXT: arg #{1} - parameter 2 must be string", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }
                            try
                            {
                                primTextColor = rules.GetVector3Item(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TEXT: arg #{1} - parameter 3 must be vector", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }
                            try
                            {
                                primTextAlpha = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TEXT: arg #{1} - parameter 4 must be float", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }
                            Vector3 av3 = Vector3.Clamp(primTextColor, 0.0f, 1.0f);
                            part.SetText(primText, av3, Utils.Clamp(primTextAlpha, 0.0f, 1.0f));

                            break;

                        case ScriptBaseClass.PRIM_NAME:
                            if (remain < 1)
                                return new LSL_List();
                            try
                            {
                                string primName = rules.GetStrictStringItem(idx++);
                                part.Name = primName;
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_NAME: arg #{1} - parameter 2 must be string", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }
                            break;
                        case ScriptBaseClass.PRIM_DESC:
                            if (remain < 1)
                                return new LSL_List();
                            try
                            {
                                string primDesc = rules.GetStrictStringItem(idx++);
                                part.Description = primDesc;
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_DESC: arg #{1} - parameter 2 must be string", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }
                            break;
                        case ScriptBaseClass.PRIM_ROT_LOCAL:
                            if (remain < 1)
                                return new LSL_List();
                            LSL_Rotation rot;
                            try
                            {
                                rot = rules.GetQuaternionItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_ROT_LOCAL: arg #{1} - parameter 2 must be rotation", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }
                            SetRot(part, rot);
                            break;

                        case ScriptBaseClass.PRIM_OMEGA:
                            if (remain < 3)
                                return new LSL_List();
                            LSL_Vector axis;
                            float spinrate;
                            float gain;

                            try
                            {
                                axis = rules.GetVector3Item(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_OMEGA: arg #{1} - parameter 2 must be vector", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                spinrate = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_OMEGA: arg #{1} - parameter 3 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                gain = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_OMEGA: arg #{1} - parameter 4 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            TargetOmega(part, axis, spinrate, gain);
                            break;

                        case ScriptBaseClass.PRIM_SLICE:
                            if (remain < 1)
                                return new LSL_List();
                            LSL_Vector slice;
                            try
                            {
                                slice = rules.GetVector3Item(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_SLICE: arg #{1} - parameter 2 must be vector", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            part.UpdateSlice((float)slice.x, (float)slice.y);
                            break;

                        case ScriptBaseClass.PRIM_SIT_TARGET:
                            if (remain < 3)
                                return new LSL_List();

                            int active;
                            try
                            {
                                active = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_SIT_TARGET: arg #{1} - parameter 1 must be integer", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }
                            LSL_Vector offset;
                            try
                            {
                                offset = rules.GetVector3Item(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_SIT_TARGET: arg #{1} - parameter 2 must be vector", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            LSL_Rotation sitrot;
                            try
                            {
                                sitrot = rules.GetQuaternionItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_SIT_TARGET: arg #{1} - parameter 3 must be rotation", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }

                            // not SL compatible since we don't have a independent flag to control active target but use the values of offset and rotation
                            if(active == 1)
                            {
                                if(offset.x == 0 && offset.y == 0 && offset.z == 0 && sitrot.s == 1.0)
                                    offset.z = 1e-5f; // hack
                                SitTarget(part,offset,sitrot);
                            }
                            else if(active == 0)
                                SitTarget(part, Vector3.Zero , Quaternion.Identity);

                            break;

                        case ScriptBaseClass.PRIM_ALPHA_MODE:
                            if (remain < 3)
                                return new LSL_List();

                            try
                            {
                                face = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_ALPHA_MODE: arg #{1} - must be integer", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }

                            int materialAlphaMode;
                            try
                            {
                                materialAlphaMode = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_ALPHA_MODE: arg #{1} - must be integer", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }

                            if(materialAlphaMode < 0 || materialAlphaMode > 3)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_ALPHA_MODE: arg #{1} - must be 0 to 3", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }

                            int materialMaskCutoff;
                            try
                            {
                                materialMaskCutoff = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_ALPHA_MODE: arg #{1} - must be integer", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }

                            if(materialMaskCutoff < 0 || materialMaskCutoff > 255)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_ALPHA_MODE: arg #{1} - must be 0 to 255", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }

                            materialChanged |= SetMaterialAlphaMode(part, face, materialAlphaMode, materialMaskCutoff);
                            break;

                        case ScriptBaseClass.PRIM_NORMAL:
                            if (remain < 5)
                                return new LSL_List();

                            try
                            {
                                face = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_NORMAL: arg #{1} - must be integer", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }

                            string mapname = rules.Data[idx++].ToString();
                            UUID mapID = UUID.Zero;
                            if (!string.IsNullOrEmpty(mapname))
                            {
                                mapID = ScriptUtils.GetAssetIdFromItemName(m_host, mapname, (int)AssetType.Texture);
                                if (mapID.IsZero())
                                {
                                    if (!UUID.TryParse(mapname, out mapID))
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_NORMAL: arg #{1} - must be a UUID or a texture name on object inventory", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                }
                            }
                            LSL_Vector mnrepeat;
                            try
                            {
                                mnrepeat = rules.GetVector3Item(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_NORMAL: arg #{1} - must be vector", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            LSL_Vector mnoffset;
                            try
                            {
                                mnoffset = rules.GetVector3Item(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_NORMAL: arg #{1} - must be vector", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            float mnrot;
                            try
                            {
                                mnrot = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_NORMAL: arg #{1} - must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            float repeatX = Math.Clamp((float)mnrepeat.x,-100.0f, 100.0f);
                            float repeatY = Math.Clamp((float)mnrepeat.y,-100.0f, 100.0f);
                            float offsetX = Math.Clamp((float)mnoffset.x, 0f, 1.0f);
                            float offsetY = Math.Clamp((float)mnoffset.y, 0f, 1.0f);

                            materialChanged |= SetMaterialNormalMap(part, face, mapID, repeatX, repeatY, offsetX, offsetY, mnrot);
                            break;

                        case ScriptBaseClass.PRIM_SPECULAR:
                            if (remain < 8)
                                return new LSL_List();

                            try
                            {
                                face = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_SPECULAR: arg #{1} - must be integer", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }

                            string smapname = rules.Data[idx++].ToString();
                            UUID smapID = UUID.Zero;
                            if(!string.IsNullOrEmpty(smapname))
                            {
                                smapID = ScriptUtils.GetAssetIdFromItemName(m_host, smapname, (int)AssetType.Texture);
                                if (smapID.IsZero())
                                {
                                    if (!UUID.TryParse(smapname, out smapID))
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_SPECULAR: arg #{1} - must be a UUID or a texture name on object inventory", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                }
                            }
                            LSL_Vector msrepeat;
                            try
                            {
                                msrepeat = rules.GetVector3Item(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_SPECULAR: arg #{1} - must be vector", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            LSL_Vector msoffset;
                            try
                            {
                                msoffset = rules.GetVector3Item(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_SPECULAR: arg #{1} - must be vector", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            float msrot;
                            try
                            {
                                msrot = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_SPECULAR: arg #{1} - must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            LSL_Vector mscolor;
                            try
                            {
                                mscolor = rules.GetVector3Item(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_SPECULAR: arg #{1} - must be vector", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            LSL_Integer msgloss;
                            try
                            {
                                msgloss = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_SPECULAR: arg #{1} - must be integer", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }

                            LSL_Integer msenv;
                            try
                            {
                                msenv = rules.GetIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_SPECULAR: arg #{1} - must be integer", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }

                            float srepeatX = Math.Clamp((float)msrepeat.x, -100.0f, 100.0f);
                            float srepeatY = Math.Clamp((float)msrepeat.y, -100.0f, 100.0f);
                            float soffsetX = Math.Clamp((float)msoffset.x, -1.0f, 1.0f);
                            float soffsetY = Math.Clamp((float)msoffset.y, -1.0f, 1.0f);
                            byte colorR = (byte)(255.0f * Math.Clamp((float)mscolor.x, 0f, 1.0f) + 0.5f);
                            byte colorG = (byte)(255.0f * Math.Clamp((float)mscolor.y, 0f, 1.0f) + 0.5f);
                            byte colorB = (byte)(255.0f * Math.Clamp((float)mscolor.z, 0f, 1.0f) + 0.5f);
                            byte gloss = (byte)Math.Clamp((int)msgloss, 0, 255);
                            byte env = (byte)Math.Clamp((int)msenv, 0, 255);

                            materialChanged |= SetMaterialSpecMap(part, face, smapID, srepeatX, srepeatY, soffsetX, soffsetY,
                                                msrot, colorR, colorG, colorB, gloss, env);

                            break;

                        case ScriptBaseClass.PRIM_LINK_TARGET:
                            if (remain < 3) // setting to 3 on the basis that parsing any usage of PRIM_LINK_TARGET that has nothing following it is pointless.
                                return new LSL_List();

                            return rules.GetSublist(idx, -1);

                        case ScriptBaseClass.PRIM_PROJECTOR:
                            if (remain < 4)
                                return new LSL_List();

                            string stexname = rules.Data[idx++].ToString();
                            UUID stexID = UUID.Zero;
                            if(!string.IsNullOrEmpty(stexname))
                            {
                                stexID = ScriptUtils.GetAssetIdFromItemName(m_host, stexname, (int)AssetType.Texture);
                                if (stexID.IsZero())
                                {
                                    if (!UUID.TryParse(stexname, out stexID))
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_PROJECTOR: arg #{1} - must be a UUID or a texture name on object inventory", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                }
                            }

                            float fov;
                            try
                            {
                                fov = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_PROJECTOR: arg #{1} - must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            float focus;
                            try
                            {
                                focus = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_PROJECTOR: arg #{1} - must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            float amb;
                            try
                            {
                                amb = rules.GetStrictFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_PROJECTOR: arg #{1} - must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            if(stexID.IsNotZero())
                            {
                                part.Shape.ProjectionEntry = true;
                                part.Shape.ProjectionTextureUUID = stexID;
                                part.Shape.ProjectionFOV = Math.Clamp(fov, 0, 3.0f);
                                part.Shape.ProjectionFocus = Math.Clamp(focus, -20.0f, 20.0f);
                                part.Shape.ProjectionAmbiance = Math.Clamp(amb, 0, 1.0f);

                                part.ParentGroup.HasGroupChanged = true;
                                part.ScheduleFullUpdate();
                            }
                            else if(part.Shape.ProjectionEntry)
                            {
                                part.Shape.ProjectionEntry = false;

                                part.ParentGroup.HasGroupChanged = true;
                                part.ScheduleFullUpdate();
                            }
                            break;

                        default:
                            Error(originFunc, string.Format("Error running rule #{0}: arg #{1} - unsupported parameter", rulesParsed, idx - idxStart));
                            return new LSL_List();
                    }
                }
            }
            catch (InvalidCastException e)
            {
                Error(originFunc, string.Format("Error running rule #{0}: arg #{1} - ", rulesParsed, idx - idxStart) + e.Message);
            }
            finally
            {
                if (positionChanged)
                {
                    if (part.ParentGroup.RootPart == part)
                    {
                        SceneObjectGroup parent = part.ParentGroup;
//                        Util.FireAndForget(delegate(object x) {
                            parent.UpdateGroupPosition(currentPosition);
//                        });
                    }
                    else
                    {
                        part.OffsetPosition = currentPosition;
//                        SceneObjectGroup parent = part.ParentGroup;
//                        parent.HasGroupChanged = true;
//                        parent.ScheduleGroupForTerseUpdate();
                        part.ScheduleTerseUpdate();
                    }
                }
                if(materialChanged)
                {
                    if (part.ParentGroup != null && !part.ParentGroup.IsDeleted)
                    {
                        part.TriggerScriptChangedEvent(Changed.TEXTURE);
                        part.ScheduleFullUpdate();
                        part.ParentGroup.HasGroupChanged = true;
                    }
                }
            }

            return new LSL_List();
        }

        protected bool SetMaterialAlphaMode(SceneObjectPart part, int face, int materialAlphaMode, int materialMaskCutoff)
        {
            if(m_materialsModule == null)
                return false;

            int nsides =  part.GetNumberOfSides();

            if(face == ScriptBaseClass.ALL_SIDES)
            {
                bool changed = false;
                for(int i = 0; i < nsides; i++)
                    changed |= SetFaceMaterialAlphaMode(part, i, materialAlphaMode, materialMaskCutoff);
                return changed;
            }

            if( face >= 0 && face < nsides)
                return SetFaceMaterialAlphaMode(part, face, materialAlphaMode, materialMaskCutoff);

            return false;
        }

        protected bool SetFaceMaterialAlphaMode(SceneObjectPart part, int face, int materialAlphaMode, int materialMaskCutoff)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            Primitive.TextureEntryFace texface = tex.CreateFace((uint)face);
            if(texface == null)
                return false;

            FaceMaterial mat = null;
            UUID oldid = texface.MaterialID;
            if (oldid.IsZero())
            {
                if (materialAlphaMode == 1)
                    return false;
            }
            else
                mat = m_materialsModule.GetMaterialCopy(oldid);

            mat ??= new FaceMaterial();

            mat.DiffuseAlphaMode = (byte)materialAlphaMode;
            mat.AlphaMaskCutoff = (byte)materialMaskCutoff;

            UUID id = m_materialsModule.AddNewMaterial(mat); // id is a hash of entire material hash, so this means no change
            if(oldid.Equals(id))
                return false;

            texface.MaterialID = id;
            part.Shape.TextureEntry = tex.GetBytes(9);
            m_materialsModule.RemoveMaterial(oldid);
            return true;
        }

        protected bool SetMaterialNormalMap(SceneObjectPart part, int face, UUID mapID, float repeatX, float repeatY,
                                            float offsetX, float offsetY, float rot)
        {
            if(m_materialsModule == null)
                return false;

            int nsides =  part.GetNumberOfSides();

            if(face == ScriptBaseClass.ALL_SIDES)
            {
                bool changed = false;
                for(int i = 0; i < nsides; i++)
                    changed |= SetFaceMaterialNormalMap(part, i, mapID, repeatX, repeatY, offsetX, offsetY, rot);
                return changed;
            }

            if( face >= 0 && face < nsides)
                return SetFaceMaterialNormalMap(part, face, mapID, repeatX, repeatY, offsetX, offsetY, rot);

            return false;
        }

        protected bool SetFaceMaterialNormalMap(SceneObjectPart part, int face, UUID mapID, float repeatX, float repeatY,
                                                float offsetX, float offsetY, float rot)

        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            Primitive.TextureEntryFace texface = tex.CreateFace((uint)face);
            if(texface == null)
                return false;

            FaceMaterial mat = null;
            UUID oldid = texface.MaterialID;

            if (oldid.IsZero())
            {
                if (mapID.IsZero())
                    return false;
            }
            else
                mat = m_materialsModule.GetMaterialCopy(oldid);

            mat ??= new FaceMaterial();

            mat.NormalMapID = mapID;
            mat.NormalOffsetX = offsetX;
            mat.NormalOffsetY = offsetY;
            mat.NormalRepeatX = repeatX;
            mat.NormalRepeatY = repeatY;
            mat.NormalRotation = rot;

            mapID = m_materialsModule.AddNewMaterial(mat);

            if(oldid.Equals(mapID))
                return false;

            texface.MaterialID = mapID;
            part.Shape.TextureEntry = tex.GetBytes(9);
            m_materialsModule.RemoveMaterial(oldid);
            return true;
        }

        protected bool SetMaterialSpecMap(SceneObjectPart part, int face, UUID mapID, float repeatX, float repeatY,
                                            float offsetX, float offsetY, float rot,
                                            byte colorR, byte colorG,  byte colorB,
                                            byte gloss, byte env)
        {
            if(m_materialsModule == null)
                return false;

            int nsides =  part.GetNumberOfSides();

            if(face == ScriptBaseClass.ALL_SIDES)
            {
                bool changed = false;
                for(int i = 0; i < nsides; i++)
                    changed |= SetFaceMaterialSpecMap(part, i, mapID, repeatX, repeatY, offsetX, offsetY, rot,
                                            colorR, colorG, colorB, gloss, env);
                return changed;
            }

            if( face >= 0 && face < nsides)
                return SetFaceMaterialSpecMap(part, face, mapID, repeatX, repeatY, offsetX, offsetY, rot,
                                            colorR, colorG, colorB, gloss, env);

            return false;
        }

        protected bool SetFaceMaterialSpecMap(SceneObjectPart part, int face, UUID mapID, float repeatX, float repeatY,
                                            float offsetX, float offsetY, float rot,
                                            byte colorR, byte colorG, byte colorB,
                                            byte gloss, byte env)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            Primitive.TextureEntryFace texface = tex.CreateFace((uint)face);
            if(texface == null)
                return false;

            FaceMaterial mat = null;
            UUID oldid = texface.MaterialID;

            if(oldid.IsZero())
            {
                if(mapID.IsZero())
                    return false;
            }
            else
                mat = m_materialsModule.GetMaterialCopy(oldid);

            mat ??= new FaceMaterial();

            mat.SpecularMapID = mapID;
            mat.SpecularOffsetX = offsetX;
            mat.SpecularOffsetY = offsetY;
            mat.SpecularRepeatX = repeatX;
            mat.SpecularRepeatY = repeatY;
            mat.SpecularRotation = rot;
            mat.SpecularLightColorR = colorR;
            mat.SpecularLightColorG = colorG;
            mat.SpecularLightColorB = colorB;
            mat.SpecularLightExponent = gloss;
            mat.EnvironmentIntensity = env;

            mapID = m_materialsModule.AddNewMaterial(mat);

            if(oldid.Equals(mapID))
                return false;

            texface.MaterialID = mapID;
            part.Shape.TextureEntry = tex.GetBytes(9);
            m_materialsModule.RemoveMaterial(oldid);
            return true;
        }

        protected LSL_List SetAgentParams(ScenePresence sp, LSL_List rules, string originFunc, ref uint rulesParsed)
        {
            int idx = 0;
            int idxStart = 0;

            try
            {
                while (idx < rules.Length)
                {
                    ++rulesParsed;
                    int code = rules.GetIntegerItem(idx++);

                    int remain = rules.Length - idx;
                    idxStart = idx;

                    switch (code)
                    {
                        case ScriptBaseClass.PRIM_POSITION:
                        case ScriptBaseClass.PRIM_POS_LOCAL:
                            if (remain < 1)
                                return new LSL_List();

                            try
                            {
                                sp.OffsetPosition = rules.GetVector3Item(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                if (code == ScriptBaseClass.PRIM_POSITION)
                                {
                                    Error(originFunc, string.Format("Error running rule #{0} -> PRIM_POSITION: arg #{1} - parameter 2 must be vector", rulesParsed, idx - idxStart - 1));
                                }
                                else
                                {
                                    Error(originFunc, string.Format("Error running rule #{0} -> PRIM_POS_LOCAL: arg #{1} - parameter 2 must be vector", rulesParsed, idx - idxStart - 1));
                                }
                                return new LSL_List();
                            }
                            break;

                        case ScriptBaseClass.PRIM_ROTATION:
                            if (remain < 1)
                                return new LSL_List();

                            Quaternion inRot;

                            try
                            {
                                inRot = rules.GetQuaternionItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_ROTATION: arg #{1} - parameter 2 must be rotation", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            SceneObjectPart parentPart = sp.ParentPart;

                            if (parentPart != null)
                                sp.Rotation =  m_host.GetWorldRotation() * inRot;

                            break;

                        case ScriptBaseClass.PRIM_ROT_LOCAL:
                            if (remain < 1)
                                return new LSL_List();

                            try
                            {
                                sp.Rotation = rules.GetQuaternionItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_ROT_LOCAL: arg #{1} - parameter 2 must be rotation", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            break;

                        case ScriptBaseClass.PRIM_TYPE:
                            Error(originFunc, "PRIM_TYPE disallowed on agent");
                            return new LSL_List();

                        case ScriptBaseClass.PRIM_OMEGA:
                            Error(originFunc, "PRIM_OMEGA disallowed on agent");
                            return new LSL_List();

                        case ScriptBaseClass.PRIM_LINK_TARGET:
                            if (remain < 3) // setting to 3 on the basis that parsing any usage of PRIM_LINK_TARGET that has nothing following it is pointless.
                                return new LSL_List();

                            return rules.GetSublist(idx, -1);

                        default:
                            Error(originFunc,
                                string.Format("Error running rule #{0} on agent: arg #{1} - disallowed on agent", rulesParsed, idx - idxStart));
                            return new LSL_List();
                    }
                }
            }
            catch (InvalidCastException e)
            {
                Error(
                    originFunc,
                    string.Format("Error running rule #{0}: arg #{1} - ", rulesParsed, idx - idxStart) + e.Message);
            }

            return new LSL_List();
        }

        public LSL_String llStringToBase64(string str)
        {
            try
            {
                byte[] encData_byte;
                encData_byte = Util.UTF8.GetBytes(str);
                string encodedData = Convert.ToBase64String(encData_byte);
                return encodedData;
            }
            catch
            {
                Error("llBase64ToString", "Error encoding string");
                return LSL_String.Empty;
            }
        }

        public LSL_String llBase64ToString(string str)
        {
            try
            {
                byte[] b = Convert.FromBase64String(str);
                return Encoding.UTF8.GetString(b);
            }
            catch
            {
                Error("llBase64ToString", "Error decoding string");
                return LSL_String.Empty;
            }
        }


        public void llRemoteDataSetRegion()
        {
            Deprecated("llRemoteDataSetRegion", "Use llOpenRemoteDataChannel instead");
        }

        public LSL_Float llLog10(double val)
        {
            return (double)Math.Log10(val);
        }

        public LSL_Float llLog(double val)
        {
            return (double)Math.Log(val);
        }

        public LSL_List llGetAnimationList(LSL_Key id)
        {

            if(!UUID.TryParse(id, out UUID avID) || avID.IsZero())
                return new LSL_List();

            ScenePresence av = World.GetScenePresence(avID);
            if (av == null || av.IsChildAgent) // only if in the region
                return new LSL_List();

            UUID[] anims = av.Animator.GetAnimationArray();
            LSL_List l = new();
            foreach (UUID foo in anims)
                l.Add(new LSL_Key(foo.ToString()));
            return l;
        }

        public void llSetParcelMusicURL(string url)
        {

            ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition);

            if (land.LandData.OwnerID != m_host.OwnerID)
                return;

            land.SetMusicUrl(url);

            ScriptSleep(m_sleepMsOnSetParcelMusicURL);
        }

        public LSL_String llGetParcelMusicURL()
        {

            ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition);

            if (land.LandData.OwnerID != m_host.OwnerID)
                return LSL_String.Empty;

            return land.GetMusicUrl();
        }

        public LSL_Vector llGetRootPosition()
        {

            return new LSL_Vector(m_host.ParentGroup.AbsolutePosition);
        }

        /// <summary>
        /// http://lslwiki.net/lslwiki/wakka.php?wakka=llGetRot
        /// http://lslwiki.net/lslwiki/wakka.php?wakka=ChildRotation
        /// Also tested in sl in regards to the behaviour in attachments/mouselook
        /// In the root prim:-
        ///     Returns the object rotation if not attached
        ///     Returns the avatars rotation if attached
        ///     Returns the camera rotation if attached and the avatar is in mouselook
        /// </summary>
        public LSL_Rotation llGetRootRotation()
        {
            Quaternion q;
            if (m_host.ParentGroup.AttachmentPoint != 0)
            {
                ScenePresence avatar = World.GetScenePresence(m_host.ParentGroup.AttachedAvatar);
                if (avatar != null)
                    if ((avatar.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_MOUSELOOK) != 0)
                        q = avatar.CameraRotation; // Mouselook
                    else
                        q = avatar.GetWorldRotation(); // Currently infrequently updated so may be inaccurate
                else
                    q = m_host.ParentGroup.GroupRotation; // Likely never get here but just in case
            }
            else
                q = m_host.ParentGroup.GroupRotation; // just the group rotation

            return new LSL_Rotation(q);
        }

        public LSL_String llGetObjectDesc()
        {
            return m_host.Description ?? String.Empty;
        }

        public void llSetObjectDesc(string desc)
        {
            m_host.Description = desc ?? String.Empty;
        }

        public LSL_Key llGetCreator()
        {
            return m_host.CreatorID.ToString();
        }

        public LSL_String llGetTimestamp()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
        }

        public LSL_Integer llGetNumberOfPrims()
        {

            return m_host.ParentGroup.PrimCount + m_host.ParentGroup.GetSittingAvatarsCount();
        }

        /// <summary>
        /// Full implementation of llGetBoundingBox according to SL 2015-04-15.
        /// http://wiki.secondlife.com/wiki/LlGetBoundingBox
        /// http://lslwiki.net/lslwiki/wakka.php?wakka=llGetBoundingBox
        /// Returns local bounding box of avatar without attachments
        ///   if target is non-seated avatar or prim/mesh in avatar attachment.
        /// Returns local bounding box of object
        /// if target is seated avatar or prim/mesh in object.
        /// Uses less accurate box models for speed.
        /// </summary>
        public LSL_List llGetBoundingBox(string obj)
        {
            LSL_List result = new();

            // If the ID is not valid, return null result
            if (!UUID.TryParse(obj, out UUID objID) || objID.IsZero())
            {
                result.Add(new LSL_Vector());
                result.Add(new LSL_Vector());
                return result;
            }

            // Check if this is an attached prim. If so, replace
            // the UUID with the avatar UUID and report it's bounding box
            SceneObjectPart part = World.GetSceneObjectPart(objID);
            if (part != null && part.ParentGroup.IsAttachment)
                objID = part.ParentGroup.AttachedAvatar;

            // Find out if this is an avatar ID. If so, return it's box
            ScenePresence presence = World.GetScenePresence(objID);
            if (presence != null)
            {
                LSL_Vector lower;
                LSL_Vector upper;

                Vector3 box = presence.Appearance.AvatarBoxSize * 0.5f;

                if (presence.Animator.Animations.ImplicitDefaultAnimation.AnimID.Equals(
                    DefaultAvatarAnimations.AnimsUUIDbyName["SIT_GROUND_CONSTRAINED"]))
                {
                    // This is for ground sitting avatars TODO!
                    lower = new LSL_Vector(-box.X - 0.1125, -box.Y, box.Z * -1.0f);
                    upper = new LSL_Vector(box.X + 0.1125, box.Y, box.Z * -1.0f);
                }
                else
                {
                    // This is for standing/flying avatars
                    lower = new LSL_Vector(-box.X, -box.Y, -box.Z);
                    upper = new LSL_Vector(box.X, box.Y, box.Z);
                }

                if (lower.x > upper.x)
                    lower.x = upper.x;
                if (lower.y > upper.y)
                    lower.y = upper.y;
                if (lower.z > upper.z)
                    lower.z = upper.z;

                result.Add(lower);
                result.Add(upper);
                return result;
            }

            part = World.GetSceneObjectPart(objID);

            // Currently only works for single prims without a sitting avatar
            if (part == null)
            {
                result.Add(new LSL_Vector());
                result.Add(new LSL_Vector());
                return result;
            }

            SceneObjectGroup sog = part.ParentGroup;
            if(sog.IsDeleted)
            {
                result.Add(new LSL_Vector());
                result.Add(new LSL_Vector());
                return result;
            }

            sog.GetBoundingBox(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ);

            result.Add(new LSL_Vector(minX, minY, minZ));
            result.Add(new LSL_Vector(maxX, maxY, maxZ));
            return result;
        }

        /// <summary>
        /// Helper to calculate bounding box of an avatar.
        /// </summary>
        private void BoundingBoxOfScenePresence(ScenePresence sp, out Vector3 lower, out Vector3 upper)
        {
            // Adjust from OS model
            // avatar height = visual height - 0.2, bounding box height = visual height
            // to SL model
            // avatar height = visual height, bounding box height = visual height + 0.2
            float height = sp.Appearance.AvatarHeight + m_avatarHeightCorrection;

            // According to avatar bounding box in SL 2015-04-18:
            // standing = <-0.275,-0.35,-0.1-0.5*h> : <0.275,0.35,0.1+0.5*h>
            // groundsitting = <-0.3875,-0.5,-0.05-0.375*h> : <0.3875,0.5,0.5>
            // sitting = <-0.5875,-0.35,-0.35-0.375*h> : <0.1875,0.35,-0.25+0.25*h>

            // When avatar is sitting
            if (sp.ParentPart != null)
            {
                lower = new Vector3(m_lABB1SitX0, m_lABB1SitY0, m_lABB1SitZ0 + m_lABB1SitZ1 * height);
                upper = new Vector3(m_lABB2SitX0, m_lABB2SitY0, m_lABB2SitZ0 + m_lABB2SitZ1 * height);
            }
            // When avatar is groundsitting
            else if (sp.Animator.Animations.ImplicitDefaultAnimation.AnimID.Equals(DefaultAvatarAnimations.AnimsUUIDbyName["SIT_GROUND_CONSTRAINED"]))
            {
                lower = new Vector3(m_lABB1GrsX0, m_lABB1GrsY0, m_lABB1GrsZ0 + m_lABB1GrsZ1 * height);
                upper = new Vector3(m_lABB2GrsX0, m_lABB2GrsY0, m_lABB2GrsZ0 + m_lABB2GrsZ1 * height);
            }
            // When avatar is standing or flying
            else
            {
                lower = new Vector3(m_lABB1StdX0, m_lABB1StdY0, m_lABB1StdZ0 + m_lABB1StdZ1 * height);
                upper = new Vector3(m_lABB2StdX0, m_lABB2StdY0, m_lABB2StdZ0 + m_lABB2StdZ1 * height);
            }
        }


        public LSL_Vector llGetGeometricCenter()
        {
            return new LSL_Vector(m_host.GetGeometricCenter());
        }

        public LSL_List llGetPrimitiveParams(LSL_List rules)
        {
            LSL_List result = new();

            LSL_List remaining = GetPrimParams(m_host, rules, ref result);

            while (remaining is not null && remaining.Length > 1)
            {
                int linknumber = remaining.GetIntegerItem(0);
                rules = remaining.GetSublist(1, -1);
                List<SceneObjectPart> parts = GetLinkParts(linknumber);
                if(parts.Count == 0)
                    break;
                foreach (SceneObjectPart part in parts)
                    remaining = GetPrimParams(part, rules, ref result);
            }

            return result;
        }

        public LSL_List llGetLinkPrimitiveParams(int linknumber, LSL_List rules)
        {

            // according to SL wiki this must indicate a single link number or link_root or link_this.
            // keep other options as before

            List<SceneObjectPart> parts;
            List<ScenePresence> avatars;

            LSL_List res = new();
            LSL_List remaining;

            while (rules.Length > 0)
            {
                parts = GetLinkParts(linknumber);
                avatars = GetLinkAvatars(linknumber);

                remaining = new LSL_List();
                foreach (SceneObjectPart part in parts)
                {
                    remaining = GetPrimParams(part, rules, ref res);
                }
                foreach (ScenePresence avatar in avatars)
                {
                    remaining = GetPrimParams(avatar, rules, ref res);
                }

                if (remaining.Length > 0)
                {
                    linknumber = remaining.GetIntegerItem(0);
                    rules = remaining.GetSublist(1, -1);
                }
                else
                    break;
            }

            return res;
        }

        public LSL_List GetPrimParams(SceneObjectPart part, LSL_List rules, ref LSL_List res)
        {
            int idx = 0;
            int face;
            Primitive.TextureEntry tex;
            int nsides = GetNumberOfSides(part);

            while (idx < rules.Length)
            {
                int code = rules.GetIntegerItem(idx++);
                int remain = rules.Length - idx;

                switch (code)
                {
                    case ScriptBaseClass.PRIM_MATERIAL:
                        res.Add(new LSL_Integer(part.Material));
                        break;

                    case ScriptBaseClass.PRIM_PHYSICS:
                        if ((part.GetEffectiveObjectFlags() & (uint)PrimFlags.Physics) != 0)
                            res.Add(new LSL_Integer(1));
                        else
                            res.Add(new LSL_Integer(0));
                        break;

                    case ScriptBaseClass.PRIM_TEMP_ON_REZ:
                        if ((part.GetEffectiveObjectFlags() & (uint)PrimFlags.TemporaryOnRez) != 0)
                            res.Add(new LSL_Integer(1));
                        else
                            res.Add(new LSL_Integer(0));
                        break;

                    case ScriptBaseClass.PRIM_PHANTOM:
                        if ((part.GetEffectiveObjectFlags() & (uint)PrimFlags.Phantom) != 0)
                            res.Add(new LSL_Integer(1));
                        else
                            res.Add(new LSL_Integer(0));
                        break;

                    case ScriptBaseClass.PRIM_POSITION:
                        LSL_Vector v = new(part.AbsolutePosition);
                        res.Add(v);
                        break;

                    case ScriptBaseClass.PRIM_SIZE:
                        res.Add(new LSL_Vector(part.Scale));
                        break;

                    case ScriptBaseClass.PRIM_ROTATION:
                        res.Add(GetPartRot(part));
                        break;

                    case ScriptBaseClass.PRIM_PHYSICS_SHAPE_TYPE:
                        res.Add(new LSL_Integer((int)part.PhysicsShapeType));
                        break;

                    case ScriptBaseClass.PRIM_TYPE:
                        // implementing box
                        PrimitiveBaseShape Shape = part.Shape;
                        int primType = (int)part.GetPrimType();
                        res.Add(new LSL_Integer(primType));
                        double topshearx = (double)(sbyte)Shape.PathShearX / 100.0; // Fix negative values for PathShearX
                        double topsheary = (double)(sbyte)Shape.PathShearY / 100.0; // and PathShearY.
                        switch (primType)
                        {
                            case ScriptBaseClass.PRIM_TYPE_BOX:
                            case ScriptBaseClass.PRIM_TYPE_CYLINDER:
                            case ScriptBaseClass.PRIM_TYPE_PRISM:
                                res.Add(new LSL_Integer(Shape.ProfileCurve) & 0xf0);    // Isolate hole shape nibble.
                                res.Add(new LSL_Vector(Shape.ProfileBegin / 50000.0, 1 - Shape.ProfileEnd / 50000.0, 0));
                                res.Add(new LSL_Float(Shape.ProfileHollow / 50000.0));
                                res.Add(new LSL_Vector(Shape.PathTwistBegin / 100.0, Shape.PathTwist / 100.0, 0));
                                res.Add(new LSL_Vector(1 - (Shape.PathScaleX / 100.0 - 1), 1 - (Shape.PathScaleY / 100.0 - 1), 0));
                                res.Add(new LSL_Vector(topshearx, topsheary, 0));
                                break;

                            case ScriptBaseClass.PRIM_TYPE_SPHERE:
                                res.Add(new LSL_Integer(Shape.ProfileCurve) & 0xf0);    // Isolate hole shape nibble.
                                res.Add(new LSL_Vector(Shape.PathBegin / 50000.0, 1 - Shape.PathEnd / 50000.0, 0));
                                res.Add(new LSL_Float(Shape.ProfileHollow / 50000.0));
                                res.Add(new LSL_Vector(Shape.PathTwistBegin / 100.0, Shape.PathTwist / 100.0, 0));
                                res.Add(new LSL_Vector(Shape.ProfileBegin / 50000.0, 1 - Shape.ProfileEnd / 50000.0, 0));
                                break;

                            case ScriptBaseClass.PRIM_TYPE_SCULPT:
                                res.Add(new LSL_String(Shape.SculptTexture.ToString()));
                                int stype = Shape.SculptType;
                                if (Shape.AnimeshEnabled)
                                    stype |= ScriptBaseClass.PRIM_SCULPT_FLAG_ANIMESH;
                                else
                                    stype &= ~ScriptBaseClass.PRIM_SCULPT_FLAG_ANIMESH;
                                res.Add(new LSL_Integer(stype));
                                break;

                            case ScriptBaseClass.PRIM_TYPE_RING:
                            case ScriptBaseClass.PRIM_TYPE_TUBE:
                            case ScriptBaseClass.PRIM_TYPE_TORUS:
                                // holeshape
                                res.Add(new LSL_Integer(Shape.ProfileCurve) & 0xf0);    // Isolate hole shape nibble.

                                // cut
                                res.Add(new LSL_Vector(Shape.PathBegin / 50000.0, 1 - Shape.PathEnd / 50000.0, 0));

                                // hollow
                                res.Add(new LSL_Float(Shape.ProfileHollow / 50000.0));

                                // twist
                                res.Add(new LSL_Vector(Shape.PathTwistBegin / 100.0, Shape.PathTwist / 100.0, 0));

                                // vector holesize
                                res.Add(new LSL_Vector(1 - (Shape.PathScaleX / 100.0 - 1), 1 - (Shape.PathScaleY / 100.0 - 1), 0));

                                // vector topshear
                                res.Add(new LSL_Vector(topshearx, topsheary, 0));

                                // vector profilecut
                                res.Add(new LSL_Vector(Shape.ProfileBegin / 50000.0, 1 - Shape.ProfileEnd / 50000.0, 0));

                                // vector tapera
                                res.Add(new LSL_Vector(Shape.PathTaperX / 100.0, Shape.PathTaperY / 100.0, 0));

                                // float revolutions
                                res.Add(new LSL_Float(Math.Round(Shape.PathRevolutions * 0.015d, 2, MidpointRounding.AwayFromZero)) + 1.0d);
                                // Slightly inaccurate, because an unsigned byte is being used to represent
                                // the entire range of floating-point values from 1.0 through 4.0 (which is how
                                // SL does it).
                                //
                                // Using these formulas to store and retrieve PathRevolutions, it is not
                                // possible to use all values between 1.00 and 4.00. For instance, you can't
                                // represent 1.10. You can represent 1.09 and 1.11, but not 1.10. So, if you
                                // use llSetPrimitiveParams to set revolutions to 1.10 and then retreive them
                                // with llGetPrimitiveParams, you'll retrieve 1.09. You can also see a similar
                                // behavior in the viewer as you cannot set 1.10. The viewer jumps to 1.11.
                                // In SL, llSetPrimitveParams and llGetPrimitiveParams can set and get a value
                                // such as 1.10. So, SL must store and retreive the actual user input rather
                                // than only storing the encoded value.

                                // float radiusoffset
                                res.Add(new LSL_Float(Shape.PathRadiusOffset / 100.0));

                                // float skew
                                res.Add(new LSL_Float(Shape.PathSkew / 100.0));
                                break;
                        }
                        break;

                    case ScriptBaseClass.PRIM_TEXTURE:
                        if (remain < 1)
                            return new LSL_List();

                        face = rules.GetIntegerItem(idx++);
                        tex = part.Shape.Textures;

                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < nsides; face++)
                            {
                                Primitive.TextureEntryFace texface = tex.GetFace((uint)face);

                                res.Add(new LSL_String(texface.TextureID.ToString()));
                                res.Add(new LSL_Vector(texface.RepeatU,
                                                       texface.RepeatV,
                                                       0));
                                res.Add(new LSL_Vector(texface.OffsetU,
                                                       texface.OffsetV,
                                                       0));
                                res.Add(new LSL_Float(texface.Rotation));
                            }
                        }
                        else
                        {
                            if (face >= 0 && face < nsides)
                            {
                                Primitive.TextureEntryFace texface = tex.GetFace((uint)face);

                                res.Add(new LSL_String(texface.TextureID.ToString()));
                                res.Add(new LSL_Vector(texface.RepeatU,
                                                       texface.RepeatV,
                                                       0));
                                res.Add(new LSL_Vector(texface.OffsetU,
                                                       texface.OffsetV,
                                                       0));
                                res.Add(new LSL_Float(texface.Rotation));
                            }
                        }
                        break;

                    case ScriptBaseClass.PRIM_COLOR:
                        if (remain < 1)
                            return new LSL_List();

                        face = rules.GetIntegerItem(idx++);
                        tex = part.Shape.Textures;
                        Color4 texcolor;

                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < nsides; face++)
                            {
                                texcolor = tex.GetFace((uint)face).RGBA;
                                res.Add(new LSL_Vector(texcolor.R,
                                                       texcolor.G,
                                                       texcolor.B));
                                res.Add(new LSL_Float(texcolor.A));
                            }
                        }
                        else
                        {
                            texcolor = tex.GetFace((uint)face).RGBA;
                            res.Add(new LSL_Vector(texcolor.R,
                                                   texcolor.G,
                                                   texcolor.B));
                            res.Add(new LSL_Float(texcolor.A));
                        }
                        break;

                    case ScriptBaseClass.PRIM_BUMP_SHINY:
                    {
                        if (remain < 1)
                            return new LSL_List();

                        face = rules.GetIntegerItem(idx++);
                        tex = part.Shape.Textures;
                        int shiny;
                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < nsides; face++)
                            {
                                Shininess shinyness = tex.GetFace((uint)face).Shiny;
                                if (shinyness == Shininess.High)
                                {
                                    shiny = ScriptBaseClass.PRIM_SHINY_HIGH;
                                }
                                else if (shinyness == Shininess.Medium)
                                {
                                    shiny = ScriptBaseClass.PRIM_SHINY_MEDIUM;
                                }
                                else if (shinyness == Shininess.Low)
                                {
                                    shiny = ScriptBaseClass.PRIM_SHINY_LOW;
                                }
                                else
                                {
                                    shiny = ScriptBaseClass.PRIM_SHINY_NONE;
                                }
                                res.Add(new LSL_Integer(shiny));
                                res.Add(new LSL_Integer((int)tex.GetFace((uint)face).Bump));
                            }
                        }
                        else
                        {
                            Shininess shinyness = tex.GetFace((uint)face).Shiny;
                            if (shinyness == Shininess.High)
                            {
                                shiny = ScriptBaseClass.PRIM_SHINY_HIGH;
                            }
                            else if (shinyness == Shininess.Medium)
                            {
                                shiny = ScriptBaseClass.PRIM_SHINY_MEDIUM;
                            }
                            else if (shinyness == Shininess.Low)
                            {
                                shiny = ScriptBaseClass.PRIM_SHINY_LOW;
                            }
                            else
                            {
                                shiny = ScriptBaseClass.PRIM_SHINY_NONE;
                            }
                            res.Add(new LSL_Integer(shiny));
                            res.Add(new LSL_Integer((int)tex.GetFace((uint)face).Bump));
                        }
                        break;
                    }
                    case ScriptBaseClass.PRIM_FULLBRIGHT:
                    {
                        if (remain < 1)
                            return new LSL_List();

                        face = rules.GetIntegerItem(idx++);

                        tex = part.Shape.Textures;
                        int fullbright;
                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < nsides; face++)
                            {
                                if (tex.GetFace((uint)face).Fullbright == true)
                                {
                                    fullbright = ScriptBaseClass.TRUE;
                                }
                                else
                                {
                                    fullbright = ScriptBaseClass.FALSE;
                                }
                                res.Add(new LSL_Integer(fullbright));
                            }
                        }
                        else
                        {
                            if (tex.GetFace((uint)face).Fullbright == true)
                            {
                                fullbright = ScriptBaseClass.TRUE;
                            }
                            else
                            {
                                fullbright = ScriptBaseClass.FALSE;
                            }
                            res.Add(new LSL_Integer(fullbright));
                        }
                        break;
                    }
                    case ScriptBaseClass.PRIM_FLEXIBLE:
                        PrimitiveBaseShape shape = part.Shape;

                        // at sl this does not return true state, but if data was set
                        if (shape.FlexiEntry)
                        // correct check should had been:
                        //if (shape.PathCurve == (byte)Extrusion.Flexible)
                            res.Add(new LSL_Integer(1));              // active
                        else
                            res.Add(new LSL_Integer(0));
                        res.Add(new LSL_Integer(shape.FlexiSoftness));// softness
                        res.Add(new LSL_Float(shape.FlexiGravity));   // gravity
                        res.Add(new LSL_Float(shape.FlexiDrag));      // friction
                        res.Add(new LSL_Float(shape.FlexiWind));      // wind
                        res.Add(new LSL_Float(shape.FlexiTension));   // tension
                        res.Add(new LSL_Vector(shape.FlexiForceX,       // force
                                               shape.FlexiForceY,
                                               shape.FlexiForceZ));
                        break;

                    case ScriptBaseClass.PRIM_TEXGEN:
                        // (PRIM_TEXGEN_DEFAULT, PRIM_TEXGEN_PLANAR)
                        if (remain < 1)
                            return new LSL_List();

                        face = rules.GetIntegerItem(idx++);

                        tex = part.Shape.Textures;
                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < nsides; face++)
                            {
                                if (tex.GetFace((uint)face).TexMapType == MappingType.Planar)
                                {
                                    res.Add(new LSL_Integer(ScriptBaseClass.PRIM_TEXGEN_PLANAR));
                                }
                                else
                                {
                                    res.Add(new LSL_Integer(ScriptBaseClass.PRIM_TEXGEN_DEFAULT));
                                }
                            }
                        }
                        else
                        {
                            if (tex.GetFace((uint)face).TexMapType == MappingType.Planar)
                            {
                                res.Add(new LSL_Integer(ScriptBaseClass.PRIM_TEXGEN_PLANAR));
                            }
                            else
                            {
                                res.Add(new LSL_Integer(ScriptBaseClass.PRIM_TEXGEN_DEFAULT));
                            }
                        }
                        break;

                    case ScriptBaseClass.PRIM_POINT_LIGHT:
                        shape = part.Shape;

                        if (shape.LightEntry)
                            res.Add(new LSL_Integer(1));              // active
                        else
                            res.Add(new LSL_Integer(0));
                        res.Add(new LSL_Vector(shape.LightColorR,       // color
                                               shape.LightColorG,
                                               shape.LightColorB));
                        res.Add(new LSL_Float(shape.LightIntensity)); // intensity
                        res.Add(new LSL_Float(shape.LightRadius));    // radius
                        res.Add(new LSL_Float(shape.LightFalloff));   // falloff
                        break;

                    case ScriptBaseClass.PRIM_REFLECTION_PROBE:
                        shape = part.Shape;
                        if (shape.ReflectionProbe is null)
                        {
                            res.Add(new LSL_Integer(0));
                            res.Add(new LSL_Float(0f)); // ambiance
                            res.Add(new LSL_Float(0f)); // clip
                            res.Add(new LSL_Float(0f)); // flags
                        }
                        else
                        {
                            res.Add(new LSL_Integer(1));
                            res.Add(new LSL_Float(shape.ReflectionProbe.Ambiance)); // ambiance
                            res.Add(new LSL_Float(shape.ReflectionProbe.ClipDistance)); // clip
                            res.Add(new LSL_Float(shape.ReflectionProbe.Flags)); // flags
                        }
                        break;

                    case ScriptBaseClass.PRIM_GLOW:
                        if (remain < 1)
                            return new LSL_List();

                        face = rules.GetIntegerItem(idx++);

                        tex = part.Shape.Textures;
                        float primglow;
                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < nsides; face++)
                            {
                                primglow = tex.GetFace((uint)face).Glow;
                                res.Add(new LSL_Float(primglow));
                            }
                        }
                        else
                        {
                            primglow = tex.GetFace((uint)face).Glow;
                            res.Add(new LSL_Float(primglow));
                        }
                        break;

                    case ScriptBaseClass.PRIM_TEXT:
                        Color4 textColor = part.GetTextColor();
                        res.Add(new LSL_String(part.Text));
                        res.Add(new LSL_Vector(textColor.R,
                                               textColor.G,
                                               textColor.B));
                        res.Add(new LSL_Float(textColor.A));
                        break;

                    case ScriptBaseClass.PRIM_NAME:
                        res.Add(new LSL_String(part.Name));
                        break;

                    case ScriptBaseClass.PRIM_DESC:
                        res.Add(new LSL_String(part.Description));
                        break;
                    case ScriptBaseClass.PRIM_ROT_LOCAL:
                        res.Add(new LSL_Rotation(part.RotationOffset));
                        break;

                    case ScriptBaseClass.PRIM_POS_LOCAL:
                        res.Add(new LSL_Vector(GetPartLocalPos(part)));
                        break;
                    case ScriptBaseClass.PRIM_SLICE:
                        PrimType prim_type = part.GetPrimType();
                        bool useProfileBeginEnd = (prim_type == PrimType.SPHERE || prim_type == PrimType.TORUS || prim_type == PrimType.TUBE || prim_type == PrimType.RING);
                        res.Add(new LSL_Vector(
                            (useProfileBeginEnd ? part.Shape.ProfileBegin : part.Shape.PathBegin) / 50000.0,
                            1 - (useProfileBeginEnd ? part.Shape.ProfileEnd : part.Shape.PathEnd) / 50000.0,
                            0
                        ));
                        break;

                    case ScriptBaseClass.PRIM_OMEGA:
                        // this may return values diferent from SL since we don't handle set the same way
                        float gain = 1.0f; // we don't use gain and don't store it
                        Vector3 axis = part.AngularVelocity;
                        float spin = axis.Length();
                        if(spin < 1.0e-6)
                        {
                            axis = Vector3.Zero;
                            gain = 0.0f;
                            spin = 0.0f;
                        }
                        else
                        {
                            axis *= (1.0f/spin);
                        }

                        res.Add(new LSL_Vector(axis));
                        res.Add(new LSL_Float(spin));
                        res.Add(new LSL_Float(gain));
                        break;

                    case ScriptBaseClass.PRIM_SIT_TARGET:
                        if(part.IsSitTargetSet)
                        {
                            res.Add(new LSL_Integer(1));
                            res.Add(new LSL_Vector(part.SitTargetPosition));
                            res.Add(new LSL_Rotation(part.SitTargetOrientation));
                        }
                        else
                        {
                            res.Add(new LSL_Integer(0));
                            res.Add(LSL_Vector.Zero);
                            res.Add(LSL_Rotation.Identity);
                        }
                        break;

                    case ScriptBaseClass.PRIM_NORMAL:
                    case ScriptBaseClass.PRIM_SPECULAR:
                    case ScriptBaseClass.PRIM_ALPHA_MODE:
                        if (remain < 1)
                            return new LSL_List();

                        face = rules.GetIntegerItem(idx++);
                        tex = part.Shape.Textures;
                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < nsides; face++)
                            {
                                Primitive.TextureEntryFace texface = tex.GetFace((uint)face);
                                getLSLFaceMaterial(ref res, code, part, texface);
                            }
                        }
                        else
                        {
                            if (face >= 0 && face < nsides)
                            {
                                Primitive.TextureEntryFace texface = tex.GetFace((uint)face);
                                getLSLFaceMaterial(ref res, code, part, texface);
                            }
                        }
                        break;

                    case ScriptBaseClass.PRIM_LINK_TARGET:

                        // TODO: Should be issuing a runtime script warning in this case.
                        if (remain < 2)
                            return new LSL_List();

                        return rules.GetSublist(idx, -1);

                    case ScriptBaseClass.PRIM_PROJECTOR:
                        if (part.Shape.ProjectionEntry)
                        {
                            res.Add(new LSL_String(part.Shape.ProjectionTextureUUID.ToString()));
                            res.Add(new LSL_Float(part.Shape.ProjectionFOV));
                            res.Add(new LSL_Float(part.Shape.ProjectionFocus));
                            res.Add(new LSL_Float(part.Shape.ProjectionAmbiance));
                        }
                        else
                        {
                            res.Add(new LSL_String(ScriptBaseClass.NULL_KEY));
                            res.Add(new LSL_Float(0));
                            res.Add(new LSL_Float(0));
                            res.Add(new LSL_Float(0));
                        }

                        break;
                }
            }

            return new LSL_List();
        }

        private string GetMaterialTextureUUIDbyRights(UUID origID, SceneObjectPart part)
        {
            if(World.Permissions.CanEditObject(m_host.ParentGroup.UUID, m_host.ParentGroup.RootPart.OwnerID))
                return origID.ToString();

            lock(part.TaskInventory)
            {
                foreach(KeyValuePair<UUID, TaskInventoryItem> inv in part.TaskInventory)
                {
                    if(inv.Value.InvType == (int)InventoryType.Texture && inv.Value.AssetID.Equals(origID))
                        return origID.ToString();
                }
            }

            return ScriptBaseClass.NULL_KEY;
        }

        private void getLSLFaceMaterial(ref LSL_List res, int code, SceneObjectPart part, Primitive.TextureEntryFace texface)
        {
            UUID matID = UUID.Zero;
            if(m_materialsModule != null)
                matID = texface.MaterialID;

            if(!matID.IsZero())
            {
                FaceMaterial mat = m_materialsModule.GetMaterial(matID);
                if(mat != null)
                {
                    if(code == ScriptBaseClass.PRIM_NORMAL)
                    {
                        res.Add(new LSL_String(GetMaterialTextureUUIDbyRights(mat.NormalMapID, part)));
                        res.Add(new LSL_Vector(mat.NormalRepeatX, mat.NormalRepeatY, 0));
                        res.Add(new LSL_Vector(mat.NormalOffsetX, mat.NormalOffsetY, 0));
                        res.Add(new LSL_Float(mat.NormalRotation));
                    }
                    else if(code == ScriptBaseClass.PRIM_SPECULAR)
                    {
                        const float colorScale = 1.0f / 255f;
                        res.Add(new LSL_String(GetMaterialTextureUUIDbyRights(mat.SpecularMapID, part)));
                        res.Add(new LSL_Vector(mat.SpecularRepeatX, mat.SpecularRepeatY, 0));
                        res.Add(new LSL_Vector(mat.SpecularOffsetX, mat.SpecularOffsetY, 0));
                        res.Add(new LSL_Float(mat.SpecularRotation));
                        res.Add(new LSL_Vector(mat.SpecularLightColorR * colorScale,
                                mat.SpecularLightColorG * colorScale,
                                mat.SpecularLightColorB * colorScale));
                        res.Add(new LSL_Integer(mat.SpecularLightExponent));
                        res.Add(new LSL_Integer(mat.EnvironmentIntensity));
                    }
                    else if(code == ScriptBaseClass.PRIM_ALPHA_MODE)
                    {
                        res.Add(new LSL_Integer(mat.DiffuseAlphaMode));
                        res.Add(new LSL_Integer(mat.AlphaMaskCutoff));
                    }
                    return;
                }
            }

            // material not found
            if(code == ScriptBaseClass.PRIM_NORMAL || code == ScriptBaseClass.PRIM_SPECULAR)
            {
                res.Add(new LSL_String(ScriptBaseClass.NULL_KEY));
                res.Add(new LSL_Vector(1.0, 1.0, 0));
                res.Add(new LSL_Vector(0, 0, 0));
                res.Add(new LSL_Float(0));

                if(code == ScriptBaseClass.PRIM_SPECULAR)
                {
                    res.Add(new LSL_Vector(1.0, 1.0, 1.0));
                    res.Add(new LSL_Integer(51));
                    res.Add(new LSL_Integer(0));
                }
            }
            else if(code == ScriptBaseClass.PRIM_ALPHA_MODE)
            {
                res.Add(new LSL_Integer(1));
                res.Add(new LSL_Integer(0));
            }
        }

        public LSL_List llGetPrimMediaParams(int face, LSL_List rules)
        {
            ScriptSleep(m_sleepMsOnGetPrimMediaParams);
            return GetPrimMediaParams(m_host, face, rules);
        }

        public LSL_List llGetLinkMedia(LSL_Integer link, LSL_Integer face, LSL_List rules)
        {
            ScriptSleep(m_sleepMsOnGetLinkMedia);
            if (link == ScriptBaseClass.LINK_ROOT)
                return GetPrimMediaParams(m_host.ParentGroup.RootPart, face, rules);
            else if (link == ScriptBaseClass.LINK_THIS)
                return GetPrimMediaParams(m_host, face, rules);
            else
            {
                SceneObjectPart part = m_host.ParentGroup.GetLinkNumPart(link);
                if (null != part)
                    return GetPrimMediaParams(part, face, rules);
            }

            return new LSL_List();
        }

        private LSL_List GetPrimMediaParams(SceneObjectPart part, int face, LSL_List rules)
        {
            // LSL Spec http://wiki.secondlife.com/wiki/LlGetPrimMediaParams says to fail silently if face is invalid
            // TODO: Need to correctly handle case where a face has no media (which gives back an empty list).
            // Assuming silently fail means give back an empty list.  Ideally, need to check this.
            if (face < 0 || face > part.GetNumberOfSides() - 1)
                return new LSL_List();

            IMoapModule module = m_ScriptEngine.World.RequestModuleInterface<IMoapModule>();
            if (null == module)
                return new LSL_List();

            MediaEntry me = module.GetMediaEntry(part, face);

            // As per http://wiki.secondlife.com/wiki/LlGetPrimMediaParams
            if (null == me)
                return new LSL_List();

            LSL_List res = new();

            for (int i = 0; i < rules.Length; i++)
            {
                int code = rules.GetIntegerItem(i);

                switch (code)
                {
                    case ScriptBaseClass.PRIM_MEDIA_ALT_IMAGE_ENABLE:
                        // Not implemented
                        res.Add(new LSL_Integer(0));
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_CONTROLS:
                        if (me.Controls == MediaControls.Standard)
                            res.Add(new LSL_Integer(ScriptBaseClass.PRIM_MEDIA_CONTROLS_STANDARD));
                        else
                            res.Add(new LSL_Integer(ScriptBaseClass.PRIM_MEDIA_CONTROLS_MINI));
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_CURRENT_URL:
                        res.Add(new LSL_String(me.CurrentURL));
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_HOME_URL:
                        res.Add(new LSL_String(me.HomeURL));
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_AUTO_LOOP:
                        res.Add(me.AutoLoop ? ScriptBaseClass.TRUE : ScriptBaseClass.FALSE);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_AUTO_PLAY:
                        res.Add(me.AutoPlay ? ScriptBaseClass.TRUE : ScriptBaseClass.FALSE);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_AUTO_SCALE:
                        res.Add(me.AutoScale ? ScriptBaseClass.TRUE : ScriptBaseClass.FALSE);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_AUTO_ZOOM:
                        res.Add(me.AutoZoom ? ScriptBaseClass.TRUE : ScriptBaseClass.FALSE);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_FIRST_CLICK_INTERACT:
                        res.Add(me.InteractOnFirstClick ? ScriptBaseClass.TRUE : ScriptBaseClass.FALSE);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_WIDTH_PIXELS:
                        res.Add(new LSL_Integer(me.Width));
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_HEIGHT_PIXELS:
                        res.Add(new LSL_Integer(me.Height));
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_WHITELIST_ENABLE:
                        res.Add(me.EnableWhiteList ? ScriptBaseClass.TRUE : ScriptBaseClass.FALSE);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_WHITELIST:
                        string[] urls = (string[])me.WhiteList.Clone();

                        for (int j = 0; j < urls.Length; j++)
                            urls[j] = Uri.EscapeDataString(urls[j]);

                        res.Add(new LSL_String(string.Join(", ", urls)));
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_PERMS_INTERACT:
                        res.Add(new LSL_Integer((int)me.InteractPermissions));
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_PERMS_CONTROL:
                        res.Add(new LSL_Integer((int)me.ControlPermissions));
                        break;

                    default: return ScriptBaseClass.LSL_STATUS_MALFORMED_PARAMS;
                }
            }

            return res;
        }

        public LSL_Integer llSetPrimMediaParams(LSL_Integer face, LSL_List rules)
        {
            ScriptSleep(m_sleepMsOnSetPrimMediaParams);
            return SetPrimMediaParams(m_host, face, rules);
        }

        public LSL_Integer llSetLinkMedia(LSL_Integer link, LSL_Integer face, LSL_List rules)
        {
            ScriptSleep(m_sleepMsOnSetLinkMedia);
            if (link == ScriptBaseClass.LINK_ROOT)
                return SetPrimMediaParams(m_host.ParentGroup.RootPart, face, rules);
            else if (link == ScriptBaseClass.LINK_THIS)
                return SetPrimMediaParams(m_host, face, rules);
            else
            {
                SceneObjectPart part = m_host.ParentGroup.GetLinkNumPart(link);
                if (null != part)
                    return SetPrimMediaParams(part, face, rules);
            }

            return ScriptBaseClass.LSL_STATUS_NOT_FOUND;
        }

        private LSL_Integer SetPrimMediaParams(SceneObjectPart part, LSL_Integer face, LSL_List rules)
        {
            // LSL Spec http://wiki.secondlife.com/wiki/LlSetPrimMediaParams says to fail silently if face is invalid
            // Assuming silently fail means sending back LSL_STATUS_OK.  Ideally, need to check this.
            // Don't perform the media check directly
            if (face < 0 || face > part.GetNumberOfSides() - 1)
                return ScriptBaseClass.LSL_STATUS_NOT_FOUND;

            IMoapModule module = m_ScriptEngine.World.RequestModuleInterface<IMoapModule>();
            if (null == module)
                return ScriptBaseClass.LSL_STATUS_NOT_SUPPORTED;

            MediaEntry me = module.GetMediaEntry(part, face);
            if (null == me)
                me = new MediaEntry();

            int i = 0;

            while (i < rules.Length - 1)
            {
                int code = rules.GetIntegerItem(i++);

                switch (code)
                {
                    case ScriptBaseClass.PRIM_MEDIA_ALT_IMAGE_ENABLE:
                        me.EnableAlterntiveImage = rules.GetIntegerItem(i++) != 0;
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_CONTROLS:
                        int v = rules.GetIntegerItem(i++);
                        if (ScriptBaseClass.PRIM_MEDIA_CONTROLS_STANDARD == v)
                            me.Controls = MediaControls.Standard;
                        else
                            me.Controls = MediaControls.Mini;
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_CURRENT_URL:
                        me.CurrentURL = rules.GetStringItem(i++);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_HOME_URL:
                        me.HomeURL = rules.GetStringItem(i++);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_AUTO_LOOP:
                        me.AutoLoop = rules.GetIntegerItem(i++) != 0;
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_AUTO_PLAY:
                        me.AutoPlay = rules.GetIntegerItem(i++) != 0;
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_AUTO_SCALE:
                        me.AutoScale = rules.GetIntegerItem(i++) != 0;
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_AUTO_ZOOM:
                        me.AutoZoom = rules.GetIntegerItem(i++) != 0;
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_FIRST_CLICK_INTERACT:
                        me.InteractOnFirstClick = rules.GetIntegerItem(i++) != 0;
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_WIDTH_PIXELS:
                        me.Width = rules.GetIntegerItem(i++);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_HEIGHT_PIXELS:
                        me.Height = rules.GetIntegerItem(i++);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_WHITELIST_ENABLE:
                        me.EnableWhiteList = rules.GetIntegerItem(i++) != 0;
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_WHITELIST:
                        string[] rawWhiteListUrls = rules.GetStringItem(i++).Split(new char[] { ',' });
                        List<string> whiteListUrls = new();
                        Array.ForEach(
                            rawWhiteListUrls, delegate(string rawUrl) { whiteListUrls.Add(rawUrl.Trim()); });
                        me.WhiteList = whiteListUrls.ToArray();
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_PERMS_INTERACT:
                        me.InteractPermissions = (MediaPermission)(byte)rules.GetIntegerItem(i++);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_PERMS_CONTROL:
                        me.ControlPermissions = (MediaPermission)(byte)rules.GetIntegerItem(i++);
                        break;

                    default: return ScriptBaseClass.LSL_STATUS_MALFORMED_PARAMS;
                }
            }

            module.SetMediaEntry(part, face, me);

            return ScriptBaseClass.LSL_STATUS_OK;
        }

        public LSL_Integer llClearPrimMedia(LSL_Integer face)
        {
            ScriptSleep(m_sleepMsOnClearPrimMedia);
            return ClearPrimMedia(m_host, face);
        }

        public LSL_Integer llClearLinkMedia(LSL_Integer link, LSL_Integer face)
        {
            ScriptSleep(m_sleepMsOnClearLinkMedia);
            if (link == ScriptBaseClass.LINK_ROOT)
                return ClearPrimMedia(m_host.ParentGroup.RootPart, face);
            else if (link == ScriptBaseClass.LINK_THIS)
                return ClearPrimMedia(m_host, face);
            else
            {
                SceneObjectPart part = m_host.ParentGroup.GetLinkNumPart(link);
                if (null != part)
                    return ClearPrimMedia(part, face);
            }

            return ScriptBaseClass.LSL_STATUS_NOT_FOUND;
        }

        private LSL_Integer ClearPrimMedia(SceneObjectPart part, LSL_Integer face)
        {
            // LSL Spec http://wiki.secondlife.com/wiki/LlClearPrimMedia says to fail silently if face is invalid
            // Assuming silently fail means sending back LSL_STATUS_OK.  Ideally, need to check this.
            // FIXME: Don't perform the media check directly
            if (face < 0 || face > part.GetNumberOfSides() - 1)
                return ScriptBaseClass.LSL_STATUS_NOT_FOUND;

            IMoapModule module = m_ScriptEngine.World.RequestModuleInterface<IMoapModule>();
            if (null == module)
                return ScriptBaseClass.LSL_STATUS_NOT_SUPPORTED;

            module.ClearMediaEntry(part, face);

            return ScriptBaseClass.LSL_STATUS_OK;
        }

        //  <remarks>
        //  <para>
        //  The .NET definition of base 64 is:
        //  <list>
        //  <item>
        //  Significant: A-Z a-z 0-9 + -
        //  </item>
        //  <item>
        //  Whitespace: \t \n \r ' '
        //  </item>
        //  <item>
        //  Valueless: =
        //  </item>
        //  <item>
        //  End-of-string: \0 or '=='
        //  </item>
        //  </list>
        //  </para>
        //  <para>
        //  Each point in a base-64 string represents
        //  a 6 bit value. A 32-bit integer can be
        //  represented using 6 characters (with some
        //  redundancy).
        //  </para>
        //  <para>
        // LSL also uses '/'
        //  rather than '-' (MIME compliant).
        //  </para>
        //  <para>
        //  RFC 1341 used as a reference (as specified
        //  by the SecondLife Wiki).
        //  </para>
        //  <para>
        //  SL do not record any kind of exception for
        //  these functions, so the string to integer
        //  conversion returns '0' if an invalid
        //  character is encountered during conversion.
        //  </para>
        //  <para>
        //  References
        //  <list>
        //  <item>
        //  http://lslwiki.net/lslwiki/wakka.php?wakka=Base64
        //  </item>
        //  <item>
        //  </item>
        //  </list>
        //  </para>
        //  </remarks>

        //  <summary>
        //  Table for converting 6-bit integers into
        //  base-64 characters
        //  </summary>

        protected static readonly char[] i2ctable =
        {
            'A','B','C','D','E','F','G','H',
            'I','J','K','L','M','N','O','P',
            'Q','R','S','T','U','V','W','X',
            'Y','Z',
            'a','b','c','d','e','f','g','h',
            'i','j','k','l','m','n','o','p',
            'q','r','s','t','u','v','w','x',
            'y','z',
            '0','1','2','3','4','5','6','7',
            '8','9',
            '+','/'
        };

        //  <summary>
        //  Table for converting base-64 characters
        //  into 6-bit integers.
        //  </summary>

        protected static readonly int[] c2itable =
        {
            -1,-1,-1,-1,-1,-1,-1,-1,    // 0x
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // 1x
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // 2x
            -1,-1,-1,63,-1,-1,-1,64,
            53,54,55,56,57,58,59,60,    // 3x
            61,62,-1,-1,-1,0,-1,-1,
            -1,1,2,3,4,5,6,7,           // 4x
            8,9,10,11,12,13,14,15,
            16,17,18,19,20,21,22,23,    // 5x
            24,25,26,-1,-1,-1,-1,-1,
            -1,27,28,29,30,31,32,33,    // 6x
            34,35,36,37,38,39,40,41,
            42,43,44,45,46,47,48,49,    // 7x
            50,51,52,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // 8x
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // 9x
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // Ax
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // Bx
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // Cx
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // Dx
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // Ex
            -1,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,    // Fx
            -1,-1,-1,-1,-1,-1,-1,-1
        };

        //  <summary>
        //  Converts a 32-bit integer into a Base64
        //  character string. Base64 character strings
        //  are always 8 characters long. All iinteger
        //  values are acceptable.
        //  </summary>
        //  <param name="number">
        //  32-bit integer to be converted.
        //  </param>
        //  <returns>
        //  8 character string. The 1st six characters
        //  contain the encoded number, the last two
        //  characters are padded with "=".
        //  </returns>

        public LSL_String llIntegerToBase64(int number)
        {
            // uninitialized string

            char[] imdt = new char[8];


            // Manually unroll the loop

            imdt[7] = '=';
            imdt[6] = '=';
            imdt[5] = i2ctable[number<<4  & 0x3F];
            imdt[4] = i2ctable[number>>2  & 0x3F];
            imdt[3] = i2ctable[number>>8  & 0x3F];
            imdt[2] = i2ctable[number>>14 & 0x3F];
            imdt[1] = i2ctable[number>>20 & 0x3F];
            imdt[0] = i2ctable[number>>26 & 0x3F];

            return new string(imdt);
        }

        //  <summary>
        //  Converts an eight character base-64 string
        //  into a 32-bit integer.
        //  </summary>
        //  <param name="str">
        //  8 characters string to be converted. Other
        //  length strings return zero.
        //  </param>
        //  <returns>
        //  Returns an integer representing the
        //  encoded value providedint he 1st 6
        //  characters of the string.
        //  </returns>
        //  <remarks>
        //  This is coded to behave like LSL's
        //  implementation (I think), based upon the
        //  information available at the Wiki.
        //  If more than 8 characters are supplied,
        //  zero is returned.
        //  If a NULL string is supplied, zero will
        //  be returned.
        //  If fewer than 6 characters are supplied, then
        //  the answer will reflect a partial
        //  accumulation of full bytes
        //  <para>
        //  The 6-bit segments are
        //  extracted left-to-right in big-endian mode,
        //  which means that segment 6 only contains the
        //  two low-order bits of the 32 bit integer as
        //  its high order 2 bits. A short string therefore
        //  means loss of low-order information. E.g.
        //
        //  |<---------------------- 32-bit integer ----------------------->|<-Pad->|
        //  |<--Byte 0----->|<--Byte 1----->|<--Byte 2----->|<--Byte 3----->|<-Pad->|
        //  |3|3|2|2|2|2|2|2|2|2|2|2|1|1|1|1|1|1|1|1|1|1| | | | | | | | | | |P|P|P|P|
        //  |1|0|9|8|7|6|5|4|3|2|1|0|9|8|7|6|5|4|3|2|1|0|9|8|7|6|5|4|3|2|1|0|P|P|P|P|
        //  |  str[0]   |  str[1]   |  str[2]   |  str[3]   |  str[4]   |  str[6]   |
        //
        //  </para>
        //  </remarks>

        public LSL_Integer llBase64ToInteger(string str)
        {
            if (str is null || str.Length < 2 || str.Length > 8)
                return 0;

            int digit;
            if ((digit = c2itable[str[0]]) <= 0)
                return 0;

            int number = --digit << 26;

            if ((digit = c2itable[str[1]]) <= 0)
                return 0;

            if (str.Length == 2)
                return number | (--digit & 0x30) << 20;

            int next = --digit << 20;

            if ((digit = c2itable[str[2]]) <= 0)
                return number;

            number |= next;
            if (str.Length == 3)
                return number | (--digit & 0x3C) << 14;

            next = --digit << 14;

            if ((digit = c2itable[str[3]]) <= 0)
                return number;

            number |= next;
            number |= --digit << 8;
            if (str.Length == 4)
                return number;

            if ((digit = c2itable[str[4]]) <= 0)
                return number;

            if (str.Length == 5)
                return number;

            next = --digit << 2;

            if ((digit = c2itable[str[5]]) <= 0)
                return number;

            number |= next;
            number |= --digit >> 4;

            return number;
        }

        public LSL_Float llGetGMTclock()
        {
            return DateTime.UtcNow.TimeOfDay.TotalSeconds;
        }

        public LSL_String llGetHTTPHeader(LSL_Key request_id, string header)
        {

           if (m_UrlModule != null)
               return m_UrlModule.GetHttpHeader(new UUID(request_id), header);
           return LSL_String.Empty;
        }


        public LSL_String llGetSimulatorHostname()
        {
            IUrlModule UrlModule = World.RequestModuleInterface<IUrlModule>();
            return UrlModule.ExternalHostNameForLSL;
        }

        //  <summary>
        //  Scan the string supplied in 'src' and
        //  tokenize it based upon two sets of
        //  tokenizers provided in two lists,
        //  separators and spacers.
        //  </summary>
        //
        //  <remarks>
        //  Separators demarcate tokens and are
        //  elided as they are encountered. Spacers
        //  also demarcate tokens, but are themselves
        //  retained as tokens.
        //
        //  Both separators and spacers may be arbitrarily
        //  long strings. i.e. ":::".
        //
        //  The function returns an ordered list
        //  representing the tokens found in the supplied
        //  sources string. If two successive tokenizers
        //  are encountered, then a null-string entry is
        //  added to the list.
        //
        //  It is a precondition that the source and
        //  toekizer lisst are non-null. If they are null,
        //  then a null pointer exception will be thrown
        //  while their lengths are being determined.
        //
        //  A small amount of working memoryis required
        //  of approximately 8*#tokenizers + 8*srcstrlen.
        //
        //  There are many ways in which this function
        //  can be implemented, this implementation is
        //  fairly naive and assumes that when the
        //  function is invooked with a short source
        //  string and/or short lists of tokenizers, then
        //  performance will not be an issue.
        //
        //  In order to minimize the perofrmance
        //  effects of long strings, or large numbers
        //  of tokeizers, the function skips as far as
        //  possible whenever a toekenizer is found,
        //  and eliminates redundant tokenizers as soon
        //  as is possible.
        //
        //  The implementation tries to minimize temporary
        //  garbage generation.
        //  </remarks>

        public LSL_List llParseStringKeepNulls(string src, LSL_List separators, LSL_List spacers)
        {
            return ParseString2List(src, separators, spacers, true);
        }

        private static LSL_List ParseString2List(string src, LSL_List separators, LSL_List spacers, bool keepNulls)
        {
            int          srclen    = src.Length;
            int          seplen    = separators.Length;
            object[]     separray  = separators.Data;
            int          spclen    = spacers.Length;
            object[]     spcarray  = spacers.Data;
            int          dellen    = 0;
            string[]     delarray  = new string[seplen+spclen];

            int          outlen    = 0;
            string[]     outarray  = new string[srclen*2+1];

            int          i, j;
            string       d;

            /*
             * Convert separator and spacer lists to C# strings.
             * Also filter out null strings so we don't hang.
             */
            for (i = 0; i < seplen; i ++)
            {
                d = separray[i].ToString();
                if (d.Length > 0)
                {
                    delarray[dellen++] = d;
                }
            }
            seplen = dellen;

            for (i = 0; i < spclen; i ++)
            {
                d = spcarray[i].ToString();
                if (d.Length > 0)
                {
                    delarray[dellen++] = d;
                }
            }

            /*
             * Scan through source string from beginning to end.
             */
            for (i = 0;;)
            {

                /*
                 * Find earliest delimeter in src starting at i (if any).
                 */
                int    earliestDel = -1;
                int    earliestSrc = srclen;
                string earliestStr = null;
                for (j = 0; j < dellen; j ++)
                {
                    d = delarray[j];
                    if (d != null)
                    {
                        int index = src.IndexOf(d, i, StringComparison.Ordinal);
                        if (index < 0)
                        {
                            delarray[j] = null;     // delim nowhere in src, don't check it anymore
                        }
                        else if (index < earliestSrc)
                        {
                            earliestSrc = index;    // where delimeter starts in source string
                            earliestDel = j;        // where delimeter is in delarray[]
                            earliestStr = d;        // the delimeter string from delarray[]
                            if (index == i) break;  // can't do any better than found at beg of string
                        }
                    }
                }

                /*
                 * Output source string starting at i through start of earliest delimeter.
                 */
                if (keepNulls || (earliestSrc > i))
                {
                    outarray[outlen++] = src[i..earliestSrc];
                }

                /*
                 * If no delimeter found at or after i, we're done scanning.
                 */
                if (earliestDel < 0) break;

                /*
                 * If delimeter was a spacer, output the spacer.
                 */
                if (earliestDel >= seplen)
                {
                    outarray[outlen++] = earliestStr;
                }

                /*
                 * Look at rest of src string following delimeter.
                 */
                i = earliestSrc + earliestStr.Length;
            }

            /*
             * Make up an exact-sized output array suitable for an LSL_List object.
             */
            object[] outlist = new object[outlen];
            for (i = 0; i < outlen; i ++)
            {
                outlist[i] = new LSL_String(outarray[i]);
            }
            return new LSL_List(outlist);
        }

        private const uint fullperms = (uint)PermissionMask.All; // no export for now

        private static int PermissionMaskToLSLPerm(uint value)
        {
            value &= fullperms;
            if (value == fullperms)
                return ScriptBaseClass.PERM_ALL;
            if( value == 0)
                return 0;

            int ret = 0;

            if ((value & (uint)PermissionMask.Copy) != 0)
                ret |= ScriptBaseClass.PERM_COPY;

            if ((value & (uint)PermissionMask.Modify) != 0)
                ret |= ScriptBaseClass.PERM_MODIFY;

            if ((value & (uint)PermissionMask.Move) != 0)
                ret |= ScriptBaseClass.PERM_MOVE;

            if ((value & (uint)PermissionMask.Transfer) != 0)
                ret |= ScriptBaseClass.PERM_TRANSFER;

            return ret;
        }

        private static uint LSLPermToPermissionMask(int lslperm, uint oldvalue)
        {
            lslperm &= ScriptBaseClass.PERM_ALL;
            if (lslperm == ScriptBaseClass.PERM_ALL)
                return oldvalue |= fullperms;

            oldvalue &= ~fullperms;
            if(lslperm != 0)
            {
                if ((lslperm & ScriptBaseClass.PERM_COPY) != 0)
                    oldvalue |= (uint)PermissionMask.Copy;

                if ((lslperm & ScriptBaseClass.PERM_MODIFY) != 0)
                    oldvalue |= (uint)PermissionMask.Modify;

                if ((lslperm & ScriptBaseClass.PERM_MOVE) != 0)
                    oldvalue |= (uint)PermissionMask.Move;

                if ((lslperm & ScriptBaseClass.PERM_TRANSFER) != 0)
                    oldvalue |= (uint)PermissionMask.Transfer;
            }

            return oldvalue;
        }

        private static int fixedCopyTransfer(int value)
        {
            if ((value & (ScriptBaseClass.PERM_COPY | ScriptBaseClass.PERM_TRANSFER)) == 0)
                value |= ScriptBaseClass.PERM_TRANSFER;
            return value;
        }

        public LSL_Integer llGetObjectPermMask(int mask)
        {
            return mask switch
            {
                ScriptBaseClass.MASK_BASE => (LSL_Integer)PermissionMaskToLSLPerm(m_host.BaseMask),
                ScriptBaseClass.MASK_OWNER => (LSL_Integer)PermissionMaskToLSLPerm(m_host.OwnerMask),
                ScriptBaseClass.MASK_GROUP => (LSL_Integer)PermissionMaskToLSLPerm(m_host.GroupMask),
                ScriptBaseClass.MASK_EVERYONE => (LSL_Integer)PermissionMaskToLSLPerm(m_host.EveryoneMask),
                ScriptBaseClass.MASK_NEXT => (LSL_Integer)PermissionMaskToLSLPerm(m_host.NextOwnerMask),
                _ => (LSL_Integer)(-1),
            };
        }

        public void llSetObjectPermMask(int mask, int value)
        {

            if (!m_AllowGodFunctions || !World.Permissions.IsAdministrator(m_host.OwnerID))
                return;

            // not even admins have right to violate basic rules
            if (mask != ScriptBaseClass.MASK_BASE)
            {
                mask &= PermissionMaskToLSLPerm(m_host.BaseMask);
                if (mask != ScriptBaseClass.MASK_OWNER)
                    mask &= PermissionMaskToLSLPerm(m_host.OwnerMask);
            }

            switch (mask)
            {
                case ScriptBaseClass.MASK_BASE:
                    value = fixedCopyTransfer(value);
                    m_host.BaseMask = LSLPermToPermissionMask(value, m_host.BaseMask);
                    break;

                case ScriptBaseClass.MASK_OWNER:
                    value = fixedCopyTransfer(value);
                    m_host.OwnerMask = LSLPermToPermissionMask(value, m_host.OwnerMask);
                    break;

                case ScriptBaseClass.MASK_GROUP:
                    m_host.GroupMask = LSLPermToPermissionMask(value, m_host.GroupMask);
                    break;

                case ScriptBaseClass.MASK_EVERYONE:
                    m_host.EveryoneMask = LSLPermToPermissionMask(value, m_host.EveryoneMask);
                    break;

                case ScriptBaseClass.MASK_NEXT:
                    value = fixedCopyTransfer(value);
                    m_host.NextOwnerMask = LSLPermToPermissionMask(value, m_host.NextOwnerMask);
                    break;
                default:
                    return;
            }
            m_host.ParentGroup.AggregatePerms();
        }

        public LSL_Integer llGetInventoryPermMask(string itemName, int mask)
        {

            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(itemName);
            if (item is null)
                return -1;

            return mask switch
            {
                ScriptBaseClass.MASK_BASE => (LSL_Integer)PermissionMaskToLSLPerm(item.BasePermissions),
                ScriptBaseClass.MASK_OWNER => (LSL_Integer)PermissionMaskToLSLPerm(item.CurrentPermissions),
                ScriptBaseClass.MASK_GROUP => (LSL_Integer)PermissionMaskToLSLPerm(item.GroupPermissions),
                ScriptBaseClass.MASK_EVERYONE => (LSL_Integer)PermissionMaskToLSLPerm(item.EveryonePermissions),
                ScriptBaseClass.MASK_NEXT => (LSL_Integer)PermissionMaskToLSLPerm(item.NextPermissions),
                _ => (LSL_Integer)(-1),
            };
        }

        public void llSetInventoryPermMask(string itemName, int mask, int value)
        {
            if(!m_AllowGodFunctions || !World.Permissions.IsAdministrator(m_host.OwnerID))
                return;

            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(itemName);
            if (item is not null)
            {
                if (mask != ScriptBaseClass.MASK_BASE)
                {
                    mask &= PermissionMaskToLSLPerm(item.BasePermissions);
                    if (mask != ScriptBaseClass.MASK_OWNER)
                        mask &= PermissionMaskToLSLPerm(item.CurrentPermissions);
                }

                /*
                if(item.Type == (int)(AssetType.Settings))
                    value |= ScriptBaseClass.PERM_COPY;
                */

                switch (mask)
                {
                    case ScriptBaseClass.MASK_BASE:
                        value = fixedCopyTransfer(value);
                        item.BasePermissions = LSLPermToPermissionMask(value, item.BasePermissions);
                        break;
                    case ScriptBaseClass.MASK_OWNER:
                        value = fixedCopyTransfer(value);
                        item.CurrentPermissions = LSLPermToPermissionMask(value, item.CurrentPermissions);
                        break;
                    case ScriptBaseClass.MASK_GROUP:
                        item.GroupPermissions = LSLPermToPermissionMask(value, item.GroupPermissions);
                        break;
                    case ScriptBaseClass.MASK_EVERYONE:
                        item.EveryonePermissions = LSLPermToPermissionMask(value, item.EveryonePermissions);
                        break;
                    case ScriptBaseClass.MASK_NEXT:
                        value = fixedCopyTransfer(value);
                        item.NextPermissions = LSLPermToPermissionMask(value, item.NextPermissions);
                        break;
                    default:
                        return;
                }

                m_host.ParentGroup.InvalidateDeepEffectivePerms();
                m_host.ParentGroup.AggregatePerms();
            }
        }

        public LSL_Key llGetInventoryCreator(string itemName)
        {

            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(itemName);
            if (item is null)
            {
                Error("llGetInventoryCreator", $"Can't find item '{itemName}'");
                return string.Empty;
            }

            return item.CreatorID.ToString();
        }

        public LSL_String llGetInventoryAcquireTime(string itemName)
        {
            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(itemName);
            if (item is null)
            {
                Error("llGetInventoryAcquireTime", $"Can't find item '{itemName}'");
                return LSL_String.Empty;
            }

            DateTime date = Util.ToDateTime(item.CreationDate);
            return date.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        public void llOwnerSay(string msg)
        {
            if(m_host.OwnerID.Equals(m_host.GroupID))
                return;
            World.SimChatBroadcast(msg, ChatTypeEnum.Owner, 0,
                                   m_host.AbsolutePosition, m_host.Name, m_host.UUID, false);
        }

        public LSL_Key llRequestSecureURL()
        {
            if (m_UrlModule != null)
                return m_UrlModule.RequestSecureURL(m_ScriptEngine.ScriptModule, m_host, m_item.ItemID, null).ToString();
            return ScriptBaseClass.NULL_KEY;
        }

        public LSL_Float llGetSimStats(LSL_Integer stat_type)
        {
            return stat_type.value switch
            {
                ScriptBaseClass.SIM_STAT_PCT_CHARS_STEPPED => 0,     // Not implemented
                ScriptBaseClass.SIM_STAT_PHYSICS_FPS => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.PhysicsFPS],
                ScriptBaseClass.SIM_STAT_AGENT_UPDATES => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.AgentUpdates],
                ScriptBaseClass.SIM_STAT_FRAME_MS => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.FrameMS],
                ScriptBaseClass.SIM_STAT_NET_MS => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.NetMS],
                ScriptBaseClass.SIM_STAT_OTHER_MS => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.OtherMS],
                ScriptBaseClass.SIM_STAT_PHYSICS_MS => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.PhysicsMS],
                ScriptBaseClass.SIM_STAT_AGENT_MS => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.AgentMS],
                ScriptBaseClass.SIM_STAT_IMAGE_MS => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.ImageMS],
                ScriptBaseClass.SIM_STAT_SCRIPT_MS => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.ScriptMS],
                ScriptBaseClass.SIM_STAT_AGENT_COUNT => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.Agents],
                ScriptBaseClass.SIM_STAT_CHILD_AGENT_COUNT => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.ChildAgents],
                ScriptBaseClass.SIM_STAT_ACTIVE_SCRIPT_COUNT => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.ActiveScripts],
                ScriptBaseClass.SIM_STAT_PACKETS_IN => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.InPacketsPerSecond],
                ScriptBaseClass.SIM_STAT_PACKETS_OUT => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.OutPacketsPerSecond],
                ScriptBaseClass.SIM_STAT_ASSET_DOWNLOADS => 0,  // Not implemented
                ScriptBaseClass.SIM_STAT_ASSET_UPLOADS  => 0,  // Not implemented
                ScriptBaseClass.SIM_STAT_UNACKED_BYTES => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.UnAckedBytes],
                ScriptBaseClass.SIM_STAT_PHYSICS_STEP_MS => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.SimPhysicsStepMs],
                ScriptBaseClass.SIM_STAT_PHYSICS_SHAPE_MS => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.SimPhysicsShapeMs],
                ScriptBaseClass.SIM_STAT_PHYSICS_OTHER_MS => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.SimPhysicsOtherMs],
                ScriptBaseClass.SIM_STAT_SCRIPT_EPS => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.ScriptEps],
                ScriptBaseClass.SIM_STAT_SPARE_MS => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.SimSpareMs],
                ScriptBaseClass.SIM_STAT_SLEEP_MS => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.SimSleepMs],
                ScriptBaseClass.SIM_STAT_IO_PUMP_MS => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.SimIoPumpTime],
                ScriptBaseClass.SIM_STAT_SCRIPT_RUN_PCT => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.SimPCTSscriptsRun],
                ScriptBaseClass.SIM_STAT_AI_MS => World.StatsReporter.LastReportedSimStats[(int)StatsIndex.SimAIStepTimeMS],
                _ => 0
            };
        }

        public LSL_Key llRequestSimulatorData(string simulator, int data)
        {
            try
            {

                if (m_regionName.Equals(simulator))
                {
                    string lreply = String.Empty;
                    RegionInfo rinfo = World.RegionInfo;
                    switch (data)
                    {
                        case ScriptBaseClass.DATA_SIM_POS:
                            lreply = new LSL_Vector(
                                    rinfo.RegionLocX,
                                    rinfo.RegionLocY,
                                    0).ToString();
                            break;
                        case ScriptBaseClass.DATA_SIM_STATUS:
                            lreply = "up"; // Duh!
                            break;
                        case ScriptBaseClass.DATA_SIM_RATING:
                            lreply = rinfo.RegionSettings.Maturity switch
                            {
                                0 => "PG",
                                1 => "MATURE",
                                2 => "ADULT",
                                _ => "UNKNOWN",
                            };
                            break;
                        case ScriptBaseClass.DATA_SIM_RELEASE:
                            lreply = "OpenSim";
                            break;
                        default:
                            ScriptSleep(m_sleepMsOnRequestSimulatorData);
                            return ScriptBaseClass.NULL_KEY; // Raise no event
                    }
                    string ltid = m_AsyncCommands.DataserverPlugin.RequestWithImediatePost(m_host.LocalId,
                                                                        m_item.ItemID, lreply);
                    ScriptSleep(m_sleepMsOnRequestSimulatorData);
                    return ltid;
                }

                void act(string eventID)
                {
                    GridRegion info = World.GridService.GetRegionByName(RegionScopeID, simulator);
                    string reply = "unknown";
                    if (info is not null)
                    {
                        switch (data)
                        {
                            case ScriptBaseClass.DATA_SIM_POS:
                                // Hypergrid is currently placing real destination region co-ords into RegionSecret.
                                // But other code can also use this field for a genuine RegionSecret!  Therefore, if
                                // anything is present we need to disambiguate.
                                //
                                // FIXME: Hypergrid should be storing this data in a different field.
                                RegionFlags regionFlags = (RegionFlags)m_ScriptEngine.World.GridService.GetRegionFlags(
                                        info.ScopeID, info.RegionID);

                                if ((regionFlags & RegionFlags.Hyperlink) != 0)
                                {
                                    Utils.LongToUInts(Convert.ToUInt64(info.RegionSecret), out uint rx, out uint ry);
                                    reply = new LSL_Vector(rx, ry, 0).ToString();
                                }
                                else
                                {
                                    // Local grid co-oridnates
                                    reply = new LSL_Vector(info.RegionLocX, info.RegionLocY, 0).ToString();
                                }
                                break;
                            case ScriptBaseClass.DATA_SIM_STATUS:
                                reply = "up"; // Duh!
                                break;
                            case ScriptBaseClass.DATA_SIM_RATING:
                                reply = info.Maturity switch
                                {
                                    0 => "PG",
                                    1 => "MATURE",
                                    2 => "ADULT",
                                    _ => "UNKNOWN",
                                };
                                break;
                            case ScriptBaseClass.DATA_SIM_RELEASE:
                                reply = "OpenSim";
                                break;
                            default:
                                break;
                        }
                    }
                    m_AsyncCommands.DataserverPlugin.DataserverReply(eventID, reply);
                }

                UUID tid = m_AsyncCommands.DataserverPlugin.RegisterRequest(
                    m_host.LocalId, m_item.ItemID, act);

                ScriptSleep(m_sleepMsOnRequestSimulatorData);
                return tid.ToString();
            }
            catch(Exception)
            {
                //m_log.Error("[LSL_API]: llRequestSimulatorData" + e.ToString());
                return ScriptBaseClass.NULL_KEY;
            }
        }

        public LSL_Key llRequestURL()
        {

            if (m_UrlModule != null)
                return m_UrlModule.RequestURL(m_ScriptEngine.ScriptModule, m_host, m_item.ItemID, null).ToString();
            return ScriptBaseClass.NULL_KEY;
        }

        public void llForceMouselook(int mouselook)
        {
            m_host.SetForceMouselook(mouselook != 0);
        }

        public LSL_Float llGetObjectMass(LSL_Key id)
        {
            if (!UUID.TryParse(id, out UUID key) || key.IsZero())
                return 0;

            // return total object mass
            SceneObjectPart part = World.GetSceneObjectPart(key);
            if (part != null)
                return part.ParentGroup.GetMass();

            // the object is null so the key is for an avatar
            ScenePresence avatar = World.GetScenePresence(key);
            if (avatar != null)
            {
                if (avatar.IsChildAgent)
                {
                    // reference http://www.lslwiki.net/lslwiki/wakka.php?wakka=llGetObjectMass
                    // child agents have a mass of 1.0
                    return 1;
                }
                else
                {
                    return avatar.GetMass();
                }
            }

            return 0;
        }

        /// <summary>
        /// llListReplaceList removes the sub-list defined by the inclusive indices
        /// start and end and inserts the src list in its place. The inclusive
        /// nature of the indices means that at least one element must be deleted
        /// if the indices are within the bounds of the existing list. I.e. 2,2
        /// will remove the element at index 2 and replace it with the source
        /// list. Both indices may be negative, with the usual interpretation. An
        /// interesting case is where end is lower than start. As these indices
        /// bound the list to be removed, then 0->end, and start->lim are removed
        /// and the source list is added as a suffix.
        /// </summary>

        public LSL_List llListReplaceList(LSL_List dest, LSL_List src, int start, int end)
        {
            LSL_List pref;

            // Note that although we have normalized, both
            // indices could still be negative.
            if (start < 0)
                start += dest.Length;

            if (end < 0)
                end += dest.Length;
            // The comventional case, remove a sequence starting with
            // start and ending with end. And then insert the source
            // list.
            if (start <= end)
            {
                // If greater than zero, then there is going to be a
                // surviving prefix. Otherwise the inclusive nature
                // of the indices mean that we're going to add the
                // source list as a prefix.
                if (start > 0)
                {
                    pref = dest.GetSublist(0,start-1);
                    // Only add a suffix if there is something
                    // beyond the end index (it's inclusive too).
                    if (end + 1 < dest.Length)
                    {
                        return pref + src + dest.GetSublist(end + 1, -1);
                    }
                    else
                    {
                        return pref + src;
                    }
                }
                // If start is less than or equal to zero, then
                // the new list is simply a prefix. We still need to
                // figure out any necessary surgery to the destination
                // based upon end. Note that if end exceeds the upper
                // bound in this case, the entire destination list
                // is removed.
                else if (start == 0)
                {
                    if (end + 1 < dest.Length)
                        return src + dest.GetSublist(end + 1, -1);
                    else
                        return src;
                }
                else // Start < 0
                {
                    if (end + 1 < dest.Length)
                        return dest.GetSublist(end + 1, -1);
                    else
                        return new LSL_List();
                }
            }
            // Finally, if start > end, we strip away a prefix and
            // a suffix, to leave the list that sits <between> ens
            // and start, and then tag on the src list. AT least
            // that's my interpretation. We can get sublist to do
            // this for us. Note that one, or both of the indices
            // might have been negative.
            else
            {
                return dest.GetSublist(end + 1, start - 1) + src;
            }
        }

        public void llLoadURL(string avatar_id, string message, string url)
        {
            if(m_host.OwnerID.Equals(m_host.GroupID))
                return;
            try
            {
                Uri m_checkuri = new(url);
                if (m_checkuri.Scheme != Uri.UriSchemeHttp && m_checkuri.Scheme != Uri.UriSchemeHttps)
                {
                    Error("llLoadURL","Invalid url schema");
                    ScriptSleep(200);
                    return;
                }
            }
            catch
            {
                Error("llLoadURL","Invalid url");
                ScriptSleep(200);
                return;
            }

            IDialogModule dm = World.RequestModuleInterface<IDialogModule>();
            dm?.SendUrlToUser(
                    new UUID(avatar_id), m_host.Name, m_host.UUID, m_host.OwnerID, false, message, url);

            ScriptSleep(m_sleepMsOnLoadURL);
        }

        public void llParcelMediaCommandList(LSL_List commandList)
        {
            // according to the docs, this command only works if script owner and land owner are the same
            // lets add estate owners and gods, too, and use the generic permission check.
            ILandObject landObject = World.LandChannel.GetLandObject(m_host.AbsolutePosition);
            if (!World.Permissions.CanEditParcelProperties(m_host.OwnerID, landObject, GroupPowers.ChangeMedia, false)) return;

            bool update = false; // send a ParcelMediaUpdate (and possibly change the land's media URL)?
            byte loop = 0;

            LandData landData = landObject.LandData;
            string url = landData.MediaURL;
            string texture = landData.MediaID.ToString();
            bool autoAlign = landData.MediaAutoScale != 0;
            string mediaType = ""; // TODO these have to be added as soon as LandData supports it
            string description = "";
            int width = 0;
            int height = 0;

            ParcelMediaCommandEnum? commandToSend = null;
            float time = 0.0f; // default is from start

            uint cmndFlags = 0;
            ScenePresence presence = null;
            int cmd;
            for (int i = 0; i < commandList.Data.Length; i++)
            {
                if(commandList.Data[i] is LSL_Integer LSL_Integerdt)
                    cmd = LSL_Integerdt;
                else
                    cmd = (int)commandList.Data[i];

                ParcelMediaCommandEnum command = (ParcelMediaCommandEnum)cmd;

                switch (command)
                {
                    case ParcelMediaCommandEnum.Agent:
                        // we send only to one agent
                        if ((i + 1) < commandList.Length)
                        {
                            if (commandList.Data[i + 1] is LSL_String LSL_Stringdt)
                            {
                                if (UUID.TryParse(LSL_Stringdt, out UUID agentID) && agentID.IsNotZero())
                                {
                                    presence = World.GetScenePresence(agentID);
                                    if(presence == null || presence.IsNPC)
                                        return;
                                }
                            }
                            else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_AGENT must be a key");
                            ++i;
                        }
                        break;

                    case ParcelMediaCommandEnum.Loop:
                        loop = 1;
                        cmndFlags |= (1 << ScriptBaseClass.PARCEL_MEDIA_COMMAND_LOOP);
                        commandToSend = command;
                        update = true; //need to send the media update packet to set looping
                        break;

                    case ParcelMediaCommandEnum.Play:
                        loop = 0;
                        cmndFlags |= (1 << ScriptBaseClass.PARCEL_MEDIA_COMMAND_PLAY);
                        commandToSend = command;
                        update = true; //need to send the media update packet to make sure it doesn't loop
                        break;

                    case ParcelMediaCommandEnum.Pause:
                        cmndFlags |= (1 << ScriptBaseClass.PARCEL_MEDIA_COMMAND_PAUSE);
                        commandToSend = command;
                        break;
                    case ParcelMediaCommandEnum.Stop:
                        cmndFlags |= (1 << ScriptBaseClass.PARCEL_MEDIA_COMMAND_STOP);
                        commandToSend = command;
                        break;
                    case ParcelMediaCommandEnum.Unload:
                        cmndFlags |= (1 << ScriptBaseClass.PARCEL_MEDIA_COMMAND_UNLOAD);
                        commandToSend = command;
                        break;

                    case ParcelMediaCommandEnum.Url:
                        if ((i + 1) < commandList.Length)
                        {
                            if (commandList.Data[i + 1] is LSL_String LSL_Stringurl)
                            {
                                url = LSL_Stringurl.m_string;
                                if(string.IsNullOrWhiteSpace(url))
                                    url = string.Empty;
                                else
                                {
                                    try
                                    {
                                        Uri dummy = new(url, UriKind.Absolute);
                                    }
                                    catch
                                    {
                                        Error("llParcelMediaCommandList", "invalid PARCEL_MEDIA_COMMAND_URL");
                                        return;
                                    }
                                }
                                update = true;
                            }
                            else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_URL must be a string");
                            ++i;
                        }
                        break;

                    case ParcelMediaCommandEnum.Texture:
                        if ((i + 1) < commandList.Length)
                        {
                            if (commandList.Data[i + 1] is LSL_String LSL_Stringdt)
                            {
                                texture = LSL_Stringdt.m_string;
                                update = true;
                            }
                            else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_TEXTURE must be a string or a key");
                            ++i;
                        }
                        break;

                    case ParcelMediaCommandEnum.Time:
                        if ((i + 1) < commandList.Length)
                        {
                            if (commandList.Data[i + 1] is LSL_Float LSL_Floatdt)
                            {
                                time = (float)LSL_Floatdt;
                                cmndFlags |= (1 << ScriptBaseClass.PARCEL_MEDIA_COMMAND_TIME);
                            }
                            else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_TIME must be a float");
                            ++i;
                        }
                        break;

                    case ParcelMediaCommandEnum.AutoAlign:
                        if ((i + 1) < commandList.Length)
                        {
                            if (commandList.Data[i + 1] is LSL_Integer LSL_Integerdta)
                            {
                                autoAlign = LSL_Integerdta;
                                update = true;
                            }

                            else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_AUTO_ALIGN must be an integer");
                            ++i;
                        }
                        break;

                    case ParcelMediaCommandEnum.Type:
                        if ((i + 1) < commandList.Length)
                        {
                            if (commandList.Data[i + 1] is LSL_String LSL_Stringdt)
                            {
                                mediaType = LSL_Stringdt.m_string;
                                update = true;
                            }
                            else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_TYPE must be a string");
                            ++i;
                        }
                        break;

                    case ParcelMediaCommandEnum.Desc:
                        if ((i + 1) < commandList.Length)
                        {
                            if (commandList.Data[i + 1] is LSL_String LSL_Stringdesc)
                            {
                                description = LSL_Stringdesc.m_string;
                                update = true;
                            }
                            else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_DESC must be a string");
                            ++i;
                        }
                        break;

                    case ParcelMediaCommandEnum.Size:
                        if ((i + 2) < commandList.Length)
                        {
                            if (commandList.Data[i + 1] is LSL_Integer LSL_IntegerWitdh)
                            {
                                if (commandList.Data[i + 2] is LSL_Integer LSL_Integerheight)
                                {
                                    width = LSL_IntegerWitdh;
                                    height = LSL_Integerheight;
                                    update = true;
                                }
                                else Error("llParcelMediaCommandList", "The second argument of PARCEL_MEDIA_COMMAND_SIZE must be an integer");
                            }
                            else Error("llParcelMediaCommandList", "The first argument of PARCEL_MEDIA_COMMAND_SIZE must be an integer");
                            i += 2;
                        }
                        break;

                    default:
                        NotImplemented("llParcelMediaCommandList", "Parameter not supported yet: " + Enum.Parse(typeof(ParcelMediaCommandEnum), commandList.Data[i].ToString()).ToString());
                        break;
                }//end switch
            }//end for

            // if we didn't get a presence, we send to all and change the url
            // if we did get a presence, we only send to the agent specified, and *don't change the land settings*!

            // did something important change or do we only start/stop/pause?
            if (update)
            {
                if (presence == null)
                {
                    // we send to all
                    landData.MediaID = new UUID(texture);
                    landData.MediaAutoScale = autoAlign ? (byte)1 : (byte)0;
                    landData.MediaWidth = width;
                    landData.MediaHeight = height;
                    landData.MediaType = mediaType;

                    // do that one last, it will cause a ParcelPropertiesUpdate
                    landObject.SetMediaUrl(url);

                    // now send to all (non-child) agents in the parcel
                    World.ForEachRootScenePresence(delegate(ScenePresence sp)
                    {
                        if (sp.currentParcelUUID.Equals(landData.GlobalID))
                        {
                            sp.ControllingClient.SendParcelMediaUpdate(landData.MediaURL,
                                                                          landData.MediaID,
                                                                          landData.MediaAutoScale,
                                                                          mediaType,
                                                                          description,
                                                                          width, height,
                                                                          loop);
                        }
                    });
                }
                else if (!presence.IsChildAgent)
                {
                    // we only send to one (root) agent
                    presence.ControllingClient.SendParcelMediaUpdate(url,
                                                                     new UUID(texture),
                                                                     autoAlign ? (byte)1 : (byte)0,
                                                                     mediaType,
                                                                     description,
                                                                     width, height,
                                                                     loop);
                }
            }

            if (commandToSend != null)
            {
                // the commandList contained a start/stop/... command, too
                if (presence == null)
                {
                    // send to all (non-child) agents in the parcel
                    World.ForEachRootScenePresence(delegate(ScenePresence sp)
                    {
                        if (sp.currentParcelUUID.Equals(landData.GlobalID))
                        {
                            sp.ControllingClient.SendParcelMediaCommand(cmndFlags,
                                            commandToSend.Value, time);
                        }
                    });
                }
                else if (!presence.IsChildAgent)
                {
                    presence.ControllingClient.SendParcelMediaCommand(cmndFlags,
                                            commandToSend.Value, time);
                }
            }
            ScriptSleep(m_sleepMsOnParcelMediaCommandList);
        }

        public LSL_List llParcelMediaQuery(LSL_List aList)
        {
            LSL_List list = new();
            Vector3 pos = m_host.AbsolutePosition;

            ILandObject landObject = World.LandChannel.GetLandObject(pos);
            if(landObject is null)
                return list;

            if (!World.Permissions.CanEditParcelProperties(m_host.OwnerID, landObject, GroupPowers.ChangeMedia, false))
                return list;

            LandData land = landObject.LandData;
            if (land is null)
                return list;

            //TO DO: make the implementation for the missing commands
            //PARCEL_MEDIA_COMMAND_LOOP_SET    float loop      Use this to get or set the parcel's media loop duration. (1.19.1 RC0 or later)
            for (int i = 0; i < aList.Data.Length; i++)
            {
                if (aList.Data[i] != null)
                {
                    switch ((ParcelMediaCommandEnum) Convert.ToInt32(aList.Data[i].ToString()))
                    {
                        case ParcelMediaCommandEnum.Url:
                            list.Add(new LSL_String(land.MediaURL));
                            break;
                        case ParcelMediaCommandEnum.Desc:
                            list.Add(new LSL_String(land.MediaDescription));
                            break;
                        case ParcelMediaCommandEnum.Texture:
                            list.Add(new LSL_String(land.MediaID.ToString()));
                            break;
                        case ParcelMediaCommandEnum.Type:
                            list.Add(new LSL_String(land.MediaType));
                            break;
                        case ParcelMediaCommandEnum.Size:
                            list.Add(new LSL_String(land.MediaWidth));
                            list.Add(new LSL_String(land.MediaHeight));
                            break;
                        default:
                            ParcelMediaCommandEnum mediaCommandEnum = ParcelMediaCommandEnum.Url;
                            NotImplemented("llParcelMediaQuery", "Parameter not supported yet: " + Enum.Parse(mediaCommandEnum.GetType() , aList.Data[i].ToString()).ToString());
                            break;
                    }
                }
            }
            ScriptSleep(m_sleepMsOnParcelMediaQuery);
            return list;
        }

        public LSL_Integer llModPow(int a, int b, int c)
        {
            Math.DivRem((long)Math.Pow(a, b), c, out long tmp);
            ScriptSleep(m_sleepMsOnModPow);
            return (int)tmp;
        }

        public LSL_String llGetInventoryDesc(string name)
        {
            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(name);
            if (item is null)
            {
                Error("llGetInventoryDesc", "Item " + name + " not found");
                return new LSL_String();
            }

            return new LSL_String(item.Description);
        }

        public LSL_Integer llGetInventoryType(string name)
        {
            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(name);
            return item is null ? -1 : item.Type;
        }

        public void llSetPayPrice(int price, LSL_List quick_pay_buttons)
        {
            if(m_host.LocalId != m_host.ParentGroup.RootPart.LocalId)
                return;

            if (quick_pay_buttons.Data.Length < 4)
            {
                int x;
                for (x=quick_pay_buttons.Data.Length; x<= 4; x++)
                {
                    quick_pay_buttons.Add(ScriptBaseClass.PAY_HIDE);
                }
            }
            int[] nPrice = new int[5];
            nPrice[0] = price;
            nPrice[1] = quick_pay_buttons.GetIntegerItem(0);
            nPrice[2] = quick_pay_buttons.GetIntegerItem(1);
            nPrice[3] = quick_pay_buttons.GetIntegerItem(2);
            nPrice[4] = quick_pay_buttons.GetIntegerItem(3);
            m_host.ParentGroup.RootPart.PayPrice = nPrice;
            m_host.ParentGroup.HasGroupChanged = true;
        }

        public LSL_Vector llGetCameraPos()
        {

            if (m_item.PermsGranter.IsZero())
                return LSL_Vector.Zero;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TRACK_CAMERA) == 0)
            {
                Error("llGetCameraPos", "No permissions to track the camera");
                return LSL_Vector.Zero;
            }

            ScenePresence presence = World.GetScenePresence(m_item.PermsGranter);
            if (presence is not null)
                return new LSL_Vector(presence.CameraPosition);

            return LSL_Vector.Zero;
        }

        public LSL_Rotation llGetCameraRot()
        {
            if (m_item.PermsGranter.IsZero())
                return LSL_Rotation.Identity;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TRACK_CAMERA) == 0)
            {
                Error("llGetCameraRot", "No permissions to track the camera");
                return LSL_Rotation.Identity;
            }

            ScenePresence presence = World.GetScenePresence(m_item.PermsGranter);
            if (presence is not null)
            {
                return new LSL_Rotation(presence.CameraRotation);
            }

            return LSL_Rotation.Identity;
        }

        public LSL_Float llGetCameraFOV()
        {
            if (m_item.PermsGranter.IsZero())
                return LSL_Float.Zero;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TRACK_CAMERA) == 0)
            {
                Error("llGetCameraAspect", "No permissions to track the camera");
                return LSL_Float.Zero;
            }

            ScenePresence presence = World.GetScenePresence(m_item.PermsGranter);
            if (presence is not null && presence.ControllingClient is not null)
            {
                return new LSL_Float(presence.ControllingClient.FOV);
            }
            return 1.4f;
        }

        public LSL_Float llGetCameraAspect()
        {
            if (m_item.PermsGranter.IsZero())
                return 1f;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TRACK_CAMERA) == 0)
            {
                Error("llGetCameraAspect", "No permissions to track the camera");
                return 1f;
            }

            ScenePresence presence = World.GetScenePresence(m_item.PermsGranter);
            if (presence is not null && presence.ControllingClient is not null)
            {
                int h = presence.ControllingClient.viewHeight;
                return new LSL_Float(h > 0 ? (float)presence.ControllingClient.viewWidth / h : 1.0f);
            }
            return 1f;
        }

        public void llSetPrimURL(string url)
        {
            Deprecated("llSetPrimURL", "Use llSetPrimMediaParams instead");
            ScriptSleep(m_sleepMsOnSetPrimURL);
        }

        public void llRefreshPrimURL()
        {
            Deprecated("llRefreshPrimURL");
            ScriptSleep(m_sleepMsOnRefreshPrimURL);
        }

        public LSL_String llEscapeURL(string url)
        {
            try
            {
                return Uri.EscapeDataString(url);
            }
            catch (Exception ex)
            {
                return "llEscapeURL: " + ex.ToString();
            }
        }

        public LSL_String llUnescapeURL(string url)
        {
            try
            {
                return Uri.UnescapeDataString(url);
            }
            catch (Exception ex)
            {
                return "llUnescapeURL: " + ex.ToString();
            }
        }

        public void llMapDestination(string simname, LSL_Vector pos, LSL_Vector lookAt)
        {
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, 0);
            if (detectedParams is null)
            {
                if (m_host.ParentGroup.IsAttachment)
                {
                    detectedParams = new DetectParams { Key = m_host.OwnerID };
                }
            else
                return;
            }

            ScenePresence avatar = World.GetScenePresence(detectedParams.Key);
            avatar?.ControllingClient.SendScriptTeleportRequest(m_host.Name,
                    simname, pos, lookAt, ScriptBaseClass.BEACON_FOCUS_MAP | ScriptBaseClass.BEACON_SHOW_MAP);
            ScriptSleep(m_sleepMsOnMapDestination);
        }

        public void llAddToLandBanList(LSL_Key avatar, LSL_Float hours)
        {
            if (!UUID.TryParse(avatar, out UUID key) || key.IsZero())
                return;

            ILandObject parcel = World.LandChannel.GetLandObject(m_host.AbsolutePosition);
            if (World.Permissions.CanEditParcelProperties(m_host.OwnerID, parcel, GroupPowers.LandManageBanned, false))
            {
                int expires = (hours != 0) ? Util.UnixTimeSinceEpoch() + (int)(3600.0 * hours) : 0;
                LandData land = parcel.LandData;
                foreach (LandAccessEntry e in land.ParcelAccessList)
                {
                    if (e.Flags == AccessList.Ban && e.AgentID.Equals(key))
                    {
                        if (e.Expires != 0 && e.Expires < expires)
                        {
                            e.Expires = expires;
                            World.EventManager.TriggerLandObjectUpdated((uint)land.LocalID, parcel);
                        }
                        return;
                    }
                }

                LandAccessEntry entry = new()
                {
                    AgentID = key,
                    Flags = AccessList.Ban,
                    Expires = expires
                };

                land.ParcelAccessList.Add(entry);
                land.Flags |= (uint)ParcelFlags.UseBanList;

                World.EventManager.TriggerLandObjectUpdated((uint)land.LocalID, parcel);
            }
            ScriptSleep(m_sleepMsOnAddToLandBanList);
        }

        public void llRemoveFromLandPassList(string avatar)
        {
            if (!UUID.TryParse(avatar, out UUID key) || key.IsZero())
                return;

            ILandObject parcel = World.LandChannel.GetLandObject(m_host.AbsolutePosition);
            if (World.Permissions.CanEditParcelProperties(m_host.OwnerID, parcel, GroupPowers.LandManagePasses, false))
            {
                LandData land = parcel.LandData;
                for(int i = 0; i < land.ParcelAccessList.Count; ++i)
                {
                    LandAccessEntry e = land.ParcelAccessList[i];
                    if (e.Flags == AccessList.Access && e.AgentID.Equals(key))
                    {
                        land.ParcelAccessList.RemoveAt(i);
                        World.EventManager.TriggerLandObjectUpdated((uint)land.LocalID, parcel);
                        break;
                    }
                }
            }
            ScriptSleep(m_sleepMsOnRemoveFromLandPassList);
        }

        public void llRemoveFromLandBanList(string avatar)
        {
            if (!UUID.TryParse(avatar, out UUID key) || key.IsZero())
                return;

            ILandObject parcel = World.LandChannel.GetLandObject(m_host.AbsolutePosition);
            if (World.Permissions.CanEditParcelProperties(m_host.OwnerID, parcel, GroupPowers.LandManageBanned, false))
            {
                LandData land = parcel.LandData;
                for (int i = 0; i < land.ParcelAccessList.Count; ++i)
                {
                    LandAccessEntry e = land.ParcelAccessList[i];
                    if (e.Flags == AccessList.Ban && e.AgentID.Equals(key))
                    {
                        land.ParcelAccessList.RemoveAt(i);
                        World.EventManager.TriggerLandObjectUpdated((uint)land.LocalID, parcel);
                        break;
                    }
                }
            }
            ScriptSleep(m_sleepMsOnRemoveFromLandBanList);
        }

        public void llSetCameraParams(LSL_List rules)
        {

            // the object we are in
            UUID objectID = m_host.ParentUUID;
            if (objectID.IsZero())
                return;

            // we need the permission first, to know which avatar we want to set the camera for
            UUID agentID = m_item.PermsGranter;

            if (agentID.IsZero())
                return;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_CONTROL_CAMERA) == 0)
                return;

            ScenePresence presence = World.GetScenePresence(agentID);

            // we are not interested in child-agents
            if (presence.IsChildAgent) return;

            SortedDictionary<int, float> parameters = new();
            object[] data = rules.Data;
            for (int i = 0; i < data.Length; ++i)
            {
                int type;
                try
                {
                    type = Convert.ToInt32(data[i++].ToString());
                }
                catch
                {
                    Error("llSetCameraParams", string.Format("Invalid camera param type {0}", data[i - 1]));
                    return;
                }
                if (i >= data.Length) break; // odd number of entries => ignore the last

                // some special cases: Vector parameters are split into 3 float parameters (with type+1, type+2, type+3)
                switch (type)
                {
                case ScriptBaseClass.CAMERA_FOCUS:
                case ScriptBaseClass.CAMERA_FOCUS_OFFSET:
                case ScriptBaseClass.CAMERA_POSITION:
                    LSL_Vector v = (LSL_Vector)data[i];
                    try
                    {
                        parameters.Add(type + 1, (float)v.x);
                    }
                    catch
                    {
                        switch(type)
                        {
                            case ScriptBaseClass.CAMERA_FOCUS:
                                Error("llSetCameraParams", "CAMERA_FOCUS: Parameter x is invalid");
                                return;
                            case ScriptBaseClass.CAMERA_FOCUS_OFFSET:
                                Error("llSetCameraParams", "CAMERA_FOCUS_OFFSET: Parameter x is invalid");
                                return;
                            case ScriptBaseClass.CAMERA_POSITION:
                                Error("llSetCameraParams", "CAMERA_POSITION: Parameter x is invalid");
                                return;
                        }
                    }
                    try
                    {
                        parameters.Add(type + 2, (float)v.y);
                    }
                    catch
                    {
                        switch(type)
                        {
                            case ScriptBaseClass.CAMERA_FOCUS:
                                Error("llSetCameraParams", "CAMERA_FOCUS: Parameter y is invalid");
                                return;
                            case ScriptBaseClass.CAMERA_FOCUS_OFFSET:
                                Error("llSetCameraParams", "CAMERA_FOCUS_OFFSET: Parameter y is invalid");
                                return;
                            case ScriptBaseClass.CAMERA_POSITION:
                                Error("llSetCameraParams", "CAMERA_POSITION: Parameter y is invalid");
                                return;
                        }
                    }
                    try
                    {
                        parameters.Add(type + 3, (float)v.z);
                    }
                    catch
                    {
                        switch(type)
                        {
                            case ScriptBaseClass.CAMERA_FOCUS:
                                Error("llSetCameraParams", "CAMERA_FOCUS: Parameter z is invalid");
                                return;
                            case ScriptBaseClass.CAMERA_FOCUS_OFFSET:
                                Error("llSetCameraParams", "CAMERA_FOCUS_OFFSET: Parameter z is invalid");
                                return;
                            case ScriptBaseClass.CAMERA_POSITION:
                                Error("llSetCameraParams", "CAMERA_POSITION: Parameter z is invalid");
                                return;
                        }
                    }
                    break;
                default:
                    // TODO: clean that up as soon as the implicit casts are in
                    if (data[i] is LSL_Float LSL_Floatv)
                        parameters.Add(type, (float)LSL_Floatv.value);
                    else if (data[i] is LSL_Integer LSL_Integerv)
                        parameters.Add(type, LSL_Integerv.value);
                    else
                    {
                        try
                        {
                            parameters.Add(type, Convert.ToSingle(data[i]));
                        }
                        catch
                        {
                            Error("llSetCameraParams", string.Format("{0}: Parameter is invalid", type));
                        }
                    }
                    break;
                }
            }
            if (parameters.Count > 0) presence.ControllingClient.SendSetFollowCamProperties(objectID, parameters);
        }

        public void llClearCameraParams()
        {

            // the object we are in
            UUID objectID = m_host.ParentUUID;
            if (objectID.IsZero())
                return;

            // we need the permission first, to know which avatar we want to clear the camera for
            UUID agentID = m_item.PermsGranter;

            if (agentID.IsZero())
                return;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_CONTROL_CAMERA) == 0)
                return;

            ScenePresence presence = World.GetScenePresence(agentID);

            // we are not interested in child-agents
            if (presence is null || presence.IsChildAgent)
                return;

            presence.ControllingClient.SendClearFollowCamProperties(objectID);
        }

        public LSL_Float llListStatistics(int operation, LSL_List src)
        {
            return operation switch
            {
                ScriptBaseClass.LIST_STAT_RANGE => (LSL_Float)src.Range(),
                ScriptBaseClass.LIST_STAT_MIN => (LSL_Float)src.Min(),
                ScriptBaseClass.LIST_STAT_MAX => (LSL_Float)src.Max(),
                ScriptBaseClass.LIST_STAT_MEAN => (LSL_Float)src.Mean(),
                ScriptBaseClass.LIST_STAT_MEDIAN => (LSL_Float)LSL_List.ToDoubleList(src).Median(),
                ScriptBaseClass.LIST_STAT_NUM_COUNT => (LSL_Float)src.NumericLength(),
                ScriptBaseClass.LIST_STAT_STD_DEV => (LSL_Float)src.StdDev(),
                ScriptBaseClass.LIST_STAT_SUM => (LSL_Float)src.Sum(),
                ScriptBaseClass.LIST_STAT_SUM_SQUARES => (LSL_Float)src.SumSqrs(),
                ScriptBaseClass.LIST_STAT_GEOMETRIC_MEAN => (LSL_Float)src.GeometricMean(),
                ScriptBaseClass.LIST_STAT_HARMONIC_MEAN => (LSL_Float)src.HarmonicMean(),
                _ => (LSL_Float)0.0,
            };
        }

        public LSL_Integer llGetUnixTime()
        {
            return Util.UnixTimeSinceEpoch();
        }

        public LSL_Integer llGetParcelFlags(LSL_Vector pos)
        {
            return (int)World.LandChannel.GetLandObject((float)pos.x, (float)pos.y).LandData.Flags;
        }

        public LSL_Integer llGetRegionFlags()
        {
            IEstateModule estate = World.RequestModuleInterface<IEstateModule>();
            if (estate == null)
                return 67108864;
            return (int)estate.GetRegionFlags();
        }

        private const string b64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

        public LSL_String llXorBase64Strings(string str1, string str2)
        {
            int padding = 0;

            ScriptSleep(300);

            if (str1.Length == 0)
                return LSL_String.Empty;
            if (str2.Length == 0)
                return str1;

            int len = str2.Length;
            if ((len % 4) != 0) // LL is EVIL!!!!
            {
                while (str2.EndsWith("="))
                    str2 = str2[..^1];

                len = str2.Length;
                int mod = len % 4;

                if (mod == 1)
                    str2 = str2[..^1];
                else if (mod == 2)
                    str2 += "==";
                else if (mod == 3)
                    str2 += "=";
            }

            byte[] data1;
            byte[] data2;
            try
            {
                data1 = Convert.FromBase64String(str1);
                data2 = Convert.FromBase64String(str2);
            }
            catch
            {
                return LSL_String.Empty;
            }

            // Remove padding
            while (str1.EndsWith('='))
            {
                str1 = str1[..^1];
                padding++;
            }
            while (str2.EndsWith('='))
                str2 = str2[..^1];

            byte[] d1 = new byte[str1.Length];
            byte[] d2 = new byte[str2.Length];

            for (int i = 0; i < str1.Length; i++)
            {
                int idx = b64.IndexOf(str1.Substring(i, 1));
                if (idx == -1)
                    idx = 0;
                d1[i] = (byte)idx;
            }

            for (int i = 0; i < str2.Length; i++)
            {
                int idx = b64.IndexOf(str2.Substring(i, 1));
                if (idx == -1)
                    idx = 0;
                d2[i] = (byte)idx;
            }

            string output = string.Empty;

            for (int pos = 0; pos < d1.Length; pos++)
                output += b64[d1[pos] ^ d2[pos % d2.Length]];

            // Here's a funny thing: LL blithely violate the base64
            // standard pretty much everywhere. Here, padding is
            // added only if the first input string had it, rather
            // than when the data actually needs it. This can result
            // in invalid base64 being returned. Go figure.

            while (padding-- > 0)
                output += '=';

            return output;
        }

        public LSL_String llXorBase64StringsCorrect(string str1, string str2)
        {

            if (str1.Length == 0)
                return LSL_String.Empty;
            if (str2.Length == 0)
                return str1;

            int len = str2.Length;
            if ((len % 4) != 0) // LL is EVIL!!!!
            {
                str2 = str2.TrimEnd(new char[] { '=' });

                len = str2.Length;
                if(len == 0)
                    return str1;

                int mod = len % 4;

                if (mod == 1)
                    str2 = str2[..(len - 1)];
                else if (mod == 2)
                    str2 += "==";
                else if (mod == 3)
                    str2 += "=";
            }

            byte[] data1;
            byte[] data2;
            try
            {
                data1 = Convert.FromBase64String(str1);
                data2 = Convert.FromBase64String(str2);
            }
            catch (Exception)
            {
                return LSL_String.Empty;
            }

            int len2 = data2.Length;
            if (len2 == 0)
                return str1;

            for (int pos = 0, pos2 = 0; pos < data1.Length; pos++)
            {
                data1[pos] ^= data2[pos2];
                if (++pos2 >= len2)
                    pos2 = 0;
            }

            return Convert.ToBase64String(data1);
        }

        private static string truncateBase64(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            int paddingPos = -1;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c >= 'A' && c <= 'Z')
                    continue;
                if (c >= 'a' && c <= 'z')
                    continue;
                if (c >= '0' && c <= '9')
                    continue;
                if (c == '+' || c == '/')
                    continue;
                paddingPos = i;
                break;
            }

            if (paddingPos == 0)
                return string.Empty;

            if (paddingPos > 0)
                input = input[..paddingPos];

            int remainder = input.Length % 4;
            return remainder switch
            {
                0 => input,
                1 => input[..^1],
                2 => input + "==",
                _ => input + "=",
            };
        }

        public LSL_String llXorBase64(string str1, string str2)
        {

            if (string.IsNullOrEmpty(str2))
                return str1;

            str1 = truncateBase64(str1);
            if (string.IsNullOrEmpty(str1))
                return LSL_String.Empty;

            str2 = truncateBase64(str2);
            if (string.IsNullOrEmpty(str2))
                return str1;

            byte[] data1;
            byte[] data2;
            try
            {
                data1 = Convert.FromBase64String(str1);
                data2 = Convert.FromBase64String(str2);
            }
            catch (Exception)
            {
                return LSL_String.Empty;
            }

            int len2 = data2.Length;
            if (len2 == 0)
                return str1;

            for (int pos = 0, pos2 = 0; pos < data1.Length; pos++)
            {
                data1[pos] ^= data2[pos2];
                if (++pos2 >= len2)
                    pos2 = 0;
            }

            return Convert.ToBase64String(data1);
        }

        static Regex llHTTPRequestRegex = new(@"^(https?:\/\/)(\w+):(\w+)@(.*)$", RegexOptions.Compiled);

        public LSL_Key llHTTPRequest(string url, LSL_List parameters, string body)
        {
            IHttpRequestModule httpScriptMod = m_ScriptEngine.World.RequestModuleInterface<IHttpRequestModule>();
            if(httpScriptMod == null)
                return string.Empty;

            if(!httpScriptMod.CheckThrottle(m_host.LocalId, m_host.OwnerID))
                return ScriptBaseClass.NULL_KEY;

            if(!Uri.TryCreate(url, UriKind.Absolute, out Uri m_checkuri))
            {
                Error("llHTTPRequest", "Invalid url");
                return string.Empty;
            }

            if (m_checkuri.Scheme != Uri.UriSchemeHttp && m_checkuri.Scheme != Uri.UriSchemeHttps)
            {
                Error("llHTTPRequest", "Invalid url schema");
                return string.Empty;
            }

            if (!httpScriptMod.CheckAllowed(m_checkuri))
            {
                Error("llHttpRequest", string.Format("Request to {0} disallowed by filter", url));
                return string.Empty;
            }

            Dictionary<string, string> httpHeaders = new();
            List<string> param = new();
            int nCustomHeaders = 0;
            int flag;

            for (int i = 0; i < parameters.Data.Length; i += 2)
            {
                object di = parameters.Data[i];
                if(di is LSL_Integer li )
                    flag = li.value;
                else if (di is int ldi)
                    flag = ldi;
                else flag = -1;

                if(flag < 0 || flag > (int)HttpRequestConstants.HTTP_PRAGMA_NO_CACHE)
                {
                    Error("llHTTPRequest", "Parameter " + i.ToString() + " is an invalid flag");
                    ScriptSleep(200);
                    return string.Empty;
                }

                if (flag != (int)HttpRequestConstants.HTTP_CUSTOM_HEADER)
                {
                    param.Add(flag.ToString());       //Add parameter flag
                    param.Add(parameters.Data[i+1].ToString()); //Add parameter value
                }
                else
                {
                    //Parameters are in pairs and custom header takes
                    //arguments in pairs so adjust for header marker.
                    ++i;

                    //Maximum of 8 headers are allowed based on the
                    //Second Life documentation for llHTTPRequest.
                    for (int count = 1; count <= 8; ++count)
                    {
                        if(nCustomHeaders >= 8)
                        {
                            Error("llHTTPRequest", "Max number of custom headers is 8, excess ignored");
                            break;
                        }

                        //Enough parameters remaining for (another) header?
                        if (parameters.Data.Length - i < 2)
                        {
                            //There must be at least one name/value pair for custom header
                            if (count == 1)
                                Error("llHTTPRequest", "Missing name/value for custom header at parameter " + i.ToString());
                            return string.Empty;
                        }

                        string paramName = parameters.Data[i].ToString();

                        string paramNamelwr = paramName.ToLower();
                        if (paramNamelwr.StartsWith("proxy-"))
                        {
                            Error("llHTTPRequest", "Name is invalid as a custom header at parameter " + i.ToString());
                            return string.Empty;
                        }
                        if (paramNamelwr.StartsWith("sec-"))
                        {
                            Error("llHTTPRequest", "Name is invalid as a custom header at parameter " + i.ToString());
                            return string.Empty;
                        }

                        bool noskip = true;
                        if (HttpForbiddenHeaders.TryGetValue(paramName, out bool fatal))
                        {
                            if(fatal)
                            {
                                Error("llHTTPRequest", "Name is invalid as a custom header at parameter " + i.ToString());
                                return string.Empty;
                            }
                            noskip = false;
                        }

                        if (noskip)
                        {
                            string paramValue = parameters.Data[i + 1].ToString();
                            if (paramName.Length + paramValue.Length > 253)
                            {
                                Error("llHTTPRequest", "name and value length exceds 253 characters for custom header at parameter " + i.ToString());
                                return string.Empty;
                            }
                            httpHeaders[paramName] = paramValue;
                            nCustomHeaders++;
                        }

                        //Have we reached the end of the list of headers?
                        //End is marked by a string with a single digit.
                        if (i + 2 >= parameters.Data.Length ||
                            Char.IsDigit(parameters.Data[i + 2].ToString()[0]))
                        {
                            break;
                        }

                        i += 2;
                    }
                }
            }

            Vector3 position = m_host.AbsolutePosition;
            Vector3 velocity = m_host.Velocity;
            Quaternion rotation = m_host.GetWorldRotation();

            string ownerName;
            ScenePresence scenePresence = World.GetScenePresence(m_host.OwnerID);
            if (scenePresence is null)
                ownerName = resolveName(m_host.OwnerID);
            else
                ownerName = scenePresence.Name;

            RegionInfo regionInfo = World.RegionInfo;

            if (!string.IsNullOrWhiteSpace(m_lsl_shard))
                httpHeaders["X-SecondLife-Shard"] = m_lsl_shard;
            httpHeaders["X-SecondLife-Object-Name"] = m_host.Name;
            httpHeaders["X-SecondLife-Object-Key"] = m_host.UUID.ToString();
            httpHeaders["X-SecondLife-Region"] = string.Format("{0} ({1}, {2})", regionInfo.RegionName, regionInfo.WorldLocX, regionInfo.WorldLocY);
            httpHeaders["X-SecondLife-Local-Position"] = string.Format("({0:0.000000}, {1:0.000000}, {2:0.000000})", position.X, position.Y, position.Z);
            httpHeaders["X-SecondLife-Local-Velocity"] = string.Format("({0:0.000000}, {1:0.000000}, {2:0.000000})", velocity.X, velocity.Y, velocity.Z);
            httpHeaders["X-SecondLife-Local-Rotation"] = string.Format("({0:0.000000}, {1:0.000000}, {2:0.000000}, {3:0.000000})", rotation.X, rotation.Y, rotation.Z, rotation.W);
            httpHeaders["X-SecondLife-Owner-Name"] = ownerName;
            httpHeaders["X-SecondLife-Owner-Key"] = m_host.OwnerID.ToString();
            if (!string.IsNullOrWhiteSpace(m_lsl_user_agent))
                httpHeaders["User-Agent"] = m_lsl_user_agent;

            // See if the URL contains any header hacks
            string[] urlParts = url.Split(new char[] {'\n'});
            if (urlParts.Length > 1)
            {
                // Iterate the passed headers and parse them
                for (int i = 1 ; i < urlParts.Length ; i++ )
                {
                    // The rest of those would be added to the body in SL.
                    // Let's not do that.
                    if (urlParts[i].Length == 0)
                        break;

                    // See if this could be a valid header
                    string[] headerParts = urlParts[i].Split(new char[] {':'}, 2);
                    if (headerParts.Length != 2)
                        continue;

                    string headerName = headerParts[0].Trim();
                    if(!HttpForbiddenInHeaders.Contains(headerName))
                    {
                        string headerValue = headerParts[1].Trim();
                        httpHeaders[headerName] = headerValue;
                    }
                }

                // Finally, strip any protocol specifier from the URL
                url = urlParts[0].Trim();
                int idx = url.IndexOf(" HTTP/");
                if (idx != -1)
                    url = url[..idx];
            }

            Match m = llHTTPRequestRegex.Match(url);
            if (m.Success)
            {
                if (m.Groups.Count == 5)
                {
                    httpHeaders["Authorization"] = String.Format("Basic {0}", Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(m.Groups[2].ToString() + ":" + m.Groups[3].ToString())));
                    url = m.Groups[1].ToString() + m.Groups[4].ToString();
                }
            }

            UUID reqID = httpScriptMod.StartHttpRequest(m_host.LocalId, m_item.ItemID, url, param, httpHeaders, body);
            return reqID.IsZero() ? string.Empty : reqID.ToString();
        }

        public void llHTTPResponse(LSL_Key id, int status, string body)
        {
            // Partial implementation: support for parameter flags needed
            //   see http://wiki.secondlife.com/wiki/llHTTPResponse

            m_UrlModule?.HttpResponse(new UUID(id), status, body);
        }

        public void llResetLandBanList()
        {
            ILandObject parcel = World.LandChannel.GetLandObject(m_host.AbsolutePosition);
            if (World.Permissions.CanEditParcelProperties(m_host.OwnerID, parcel, GroupPowers.LandManageBanned, false))
            {
                LandData land = parcel.LandData;
                var tokeep = new List<LandAccessEntry>();
                foreach (LandAccessEntry entry in land.ParcelAccessList)
                {
                    if (entry.Flags != AccessList.Ban)
                        tokeep.Add(entry);
                }
                land.ParcelAccessList = tokeep;
                land.Flags &= ~(uint)ParcelFlags.UseBanList;
            }
            ScriptSleep(m_sleepMsOnResetLandBanList);
        }

        public void llResetLandPassList()
        {
            ILandObject parcel = World.LandChannel.GetLandObject(m_host.AbsolutePosition);
            if (World.Permissions.CanEditParcelProperties(m_host.OwnerID, parcel, GroupPowers.LandManagePasses, false))
            {
                LandData land = parcel.LandData;
                var tokeep = new List<LandAccessEntry>();
                foreach (LandAccessEntry entry in land.ParcelAccessList)
                {
                    if (entry.Flags != AccessList.Access)
                        tokeep.Add(entry);
                }
                land.ParcelAccessList = tokeep;
            }
            ScriptSleep(m_sleepMsOnResetLandPassList);
        }

        public LSL_Integer llGetParcelPrimCount(LSL_Vector pos, int category, int sim_wide)
        {

            ILandObject lo = World.LandChannel.GetLandObject((float)pos.x, (float)pos.y);

            if (lo == null)
                return 0;

            IPrimCounts pc = lo.PrimCounts;

            if (sim_wide != ScriptBaseClass.FALSE)
            {
                if (category == ScriptBaseClass.PARCEL_COUNT_TOTAL)
                {
                    return pc.Simulator;
                }
                else
                {
                    // counts not implemented yet
                    return 0;
                }
            }
            else
            {
                if (category == ScriptBaseClass.PARCEL_COUNT_TOTAL)
                    return pc.Total;
                else if (category == ScriptBaseClass.PARCEL_COUNT_OWNER)
                    return pc.Owner;
                else if (category == ScriptBaseClass.PARCEL_COUNT_GROUP)
                    return pc.Group;
                else if (category == ScriptBaseClass.PARCEL_COUNT_OTHER)
                    return pc.Others;
                else if (category == ScriptBaseClass.PARCEL_COUNT_SELECTED)
                    return pc.Selected;
                else if (category == ScriptBaseClass.PARCEL_COUNT_TEMP)
                    return 0; // counts not implemented yet
            }

            return 0;
        }

        public LSL_List llGetParcelPrimOwners(LSL_Vector pos)
        {
            LandObject land = (LandObject)World.LandChannel.GetLandObject((float)pos.x, (float)pos.y);
            LSL_List ret = new ();
            if (land is not null)
            {
                foreach (KeyValuePair<UUID, int> detectedParams in land.GetLandObjectOwners())
                {
                    ret.Add(new LSL_String(detectedParams.Key.ToString()));
                    ret.Add(new LSL_Integer(detectedParams.Value));
                }
            }
            ScriptSleep(m_sleepMsOnGetParcelPrimOwners);
            return ret;
        }

        public LSL_Integer llGetObjectPrimCount(LSL_Key object_id)
        {
            if(!UUID.TryParse(object_id, out UUID id) || id.IsZero())
                return 0;

            SceneObjectPart part = World.GetSceneObjectPart(id);
            if (part is null || part.ParentGroup.IsAttachment)
                return 0;

            return part.ParentGroup.PrimCount;
        }

        public LSL_Integer llGetParcelMaxPrims(LSL_Vector pos, int sim_wide)
        {

            ILandObject lo = World.LandChannel.GetLandObject((float)pos.x, (float)pos.y);

            if (lo == null)
                return 0;

            if (sim_wide != 0)
                return lo.GetSimulatorMaxPrimCount();
            else
                return lo.GetParcelMaxPrimCount();
        }

        public LSL_List llGetParcelDetails(LSL_Vector pos, LSL_List param)
        {
            ILandObject parcel = World.LandChannel.GetLandObject(pos);
            if (parcel is null)
                return new LSL_List(0);

            return GetParcelDetails(parcel, param);
        }

        public LSL_List GetParcelDetails(ILandObject parcel, LSL_List param)
        {
            LandData land = parcel.LandData;
            if (land is null)
                return new LSL_List(0);

            LSL_List ret = new();
            foreach (object o in param.Data)
            {
                if (o is not LSL_Integer io)
                {
                    Error("GetParcelDetails", $"Unknown parameter {o}");
                    return new LSL_List(0);
                }

                switch (io.value)
                {
                    case ScriptBaseClass.PARCEL_DETAILS_NAME:
                        ret.Add(new LSL_String(land.Name));
                        break;
                    case ScriptBaseClass.PARCEL_DETAILS_DESC:
                        ret.Add(new LSL_String(land.Description));
                        break;
                    case ScriptBaseClass.PARCEL_DETAILS_OWNER:
                        ret.Add(new LSL_Key(land.OwnerID.ToString()));
                        break;
                    case ScriptBaseClass.PARCEL_DETAILS_GROUP:
                        ret.Add(new LSL_Key(land.GroupID.ToString()));
                        break;
                    case ScriptBaseClass.PARCEL_DETAILS_AREA:
                        ret.Add(new LSL_Integer(land.Area));
                        break;
                    case ScriptBaseClass.PARCEL_DETAILS_ID:
                        ret.Add(new LSL_Key(land.GlobalID.ToString()));
                        break;
                    case ScriptBaseClass.PARCEL_DETAILS_SEE_AVATARS:
                        ret.Add(new LSL_Integer(land.SeeAVs ? 1 : 0));
                        break;
                    case ScriptBaseClass.PARCEL_DETAILS_PRIM_CAPACITY:
                        ret.Add(new LSL_Integer(parcel.GetParcelMaxPrimCount()));
                        break;
                    case 8:
                        ret.Add(new LSL_Integer(parcel.PrimCounts.Total));
                        break;
                    case ScriptBaseClass.PARCEL_DETAILS_LANDING_POINT:
                        ret.Add(new LSL_Vector(land.UserLocation));
                        break;
                    case ScriptBaseClass.PARCEL_DETAILS_LANDING_LOOKAT:
                        ret.Add(new LSL_Vector(land.UserLookAt));
                        break;
                    case ScriptBaseClass.PARCEL_DETAILS_TP_ROUTING:
                        ret.Add(new LSL_Integer(land.LandingType));
                        break;
                    case ScriptBaseClass.PARCEL_DETAILS_FLAGS:
                        ret.Add(new LSL_Integer(land.Flags));
                        break;
                    case ScriptBaseClass.PARCEL_DETAILS_SCRIPT_DANGER:
                        ret.Add(new LSL_Integer(World.LSLScriptDanger(m_host, parcel) ? 1 : 0));
                        break;
                    case ScriptBaseClass.PARCEL_DETAILS_DWELL:
                        ret.Add(new LSL_Integer(land.Dwell));
                        break;
                    case ScriptBaseClass.PARCEL_DETAILS_GETCLAIMDATE:
                        ret.Add(new LSL_Integer(land.ClaimDate));
                        break;
                    case ScriptBaseClass.PARCEL_DETAILS_GEOMETRICCENTER:
                        ret.Add(new LSL_Vector(parcel.CenterPoint.X, parcel.CenterPoint.Y, 0));
                        break;
                    default:
                        Error("GetParcelDetails", $"Unknown parameter {io.value}");
                        return new LSL_List(0);
                }
            }
            return ret;
        }

        public LSL_String llStringTrim(LSL_String src, LSL_Integer type)
        {
            if (type == ScriptBaseClass.STRING_TRIM_HEAD) { return ((string)src).TrimStart(); }
            if (type == ScriptBaseClass.STRING_TRIM_TAIL) { return ((string)src).TrimEnd(); }
            if (type == ScriptBaseClass.STRING_TRIM) { return ((string)src).Trim(); }
            return src;
        }

        public LSL_List llGetObjectDetails(LSL_Key id, LSL_List args)
        {
            LSL_List ret = new();
            if (!UUID.TryParse(id, out UUID key) || key.IsZero())
                return ret;

            int count;
            ScenePresence av = World.GetScenePresence(key);
            if (av is not null)
            {
                List<SceneObjectGroup> Attachments = null;
                int? nAnimated = null;
                foreach (object o in args.Data)
                {
                    switch (int.Parse(o.ToString()))
                    {
                        case ScriptBaseClass.OBJECT_NAME:
                            ret.Add(new LSL_String($"{av.Firstname} {av.Lastname}"));
                            break;
                        case ScriptBaseClass.OBJECT_DESC:
                            ret.Add(new LSL_String(""));
                            break;
                        case ScriptBaseClass.OBJECT_POS:
                            Vector3 avpos;

                            if (av.ParentID != 0 && av.ParentPart is not null &&
                                av.ParentPart.ParentGroup is not null && av.ParentPart.ParentGroup.RootPart is not null )
                            {
                                avpos = av.OffsetPosition;

                                if(!av.LegacySitOffsets)
                                {
                                    Vector3 sitOffset = (Zrot(av.Rotation)) * (av.Appearance.AvatarHeight * 0.02638f *2.0f);
                                    avpos -= sitOffset;
                                }

                                SceneObjectPart sitRoot = av.ParentPart.ParentGroup.RootPart;
                                avpos = sitRoot.GetWorldPosition() + avpos * sitRoot.GetWorldRotation();
                            }
                            else
                                avpos = av.AbsolutePosition;

                            ret.Add(new LSL_Vector(avpos));
                            break;
                        case ScriptBaseClass.OBJECT_ROT:
                            Quaternion avrot = av.GetWorldRotation();
                            ret.Add(new LSL_Rotation(avrot));
                            break;
                        case ScriptBaseClass.OBJECT_VELOCITY:
                            Vector3 avvel = av.GetWorldVelocity();
                            ret.Add(new LSL_Vector(avvel));
                            break;
                        case ScriptBaseClass.OBJECT_OWNER:
                            ret.Add(new LSL_Key((string)id));
                            break;
                        case ScriptBaseClass.OBJECT_GROUP:
                            ret.Add(new LSL_String(ScriptBaseClass.NULL_KEY));
                            break;
                        case ScriptBaseClass.OBJECT_CREATOR:
                            ret.Add(new LSL_Key(ScriptBaseClass.NULL_KEY));
                            break;
                        // For the following 8 see the Object version below
                        case ScriptBaseClass.OBJECT_RUNNING_SCRIPT_COUNT:
                            ret.Add(new LSL_Integer(av.RunningScriptCount()));
                            break;
                        case ScriptBaseClass.OBJECT_TOTAL_SCRIPT_COUNT:
                            ret.Add(new LSL_Integer(av.ScriptCount()));
                            break;
                        case ScriptBaseClass.OBJECT_SCRIPT_MEMORY:
                            ret.Add(new LSL_Integer(av.RunningScriptCount() * 16384));
                            break;
                        case ScriptBaseClass.OBJECT_SCRIPT_TIME:
                            ret.Add(new LSL_Float(av.ScriptExecutionTime() / 1000.0f));
                            break;
                        case ScriptBaseClass.OBJECT_PRIM_EQUIVALENCE:
                            ret.Add(new LSL_Integer(1));
                            break;
                        case ScriptBaseClass.OBJECT_SERVER_COST:
                            ret.Add(new LSL_Float(0));
                            break;
                        case ScriptBaseClass.OBJECT_STREAMING_COST:
                            ret.Add(new LSL_Float(0));
                            break;
                        case ScriptBaseClass.OBJECT_PHYSICS_COST:
                            ret.Add(new LSL_Float(0));
                            break;
                        case ScriptBaseClass.OBJECT_CHARACTER_TIME: // Pathfinding
                            ret.Add(new LSL_Float(0));
                            break;
                        case ScriptBaseClass.OBJECT_ROOT:
                            SceneObjectPart p = av.ParentPart;
                            if (p is not null)
                                ret.Add(new LSL_String(p.ParentGroup.RootPart.UUID.ToString()));
                            else
                                ret.Add(new LSL_Key((string)id));
                            break;
                        case ScriptBaseClass.OBJECT_ATTACHED_POINT:
                            ret.Add(new LSL_Integer(0));
                            break;
                        case ScriptBaseClass.OBJECT_PATHFINDING_TYPE: // Pathfinding
                            ret.Add(new LSL_Integer(ScriptBaseClass.OPT_AVATAR));
                            break;
                        case ScriptBaseClass.OBJECT_PHYSICS:
                            ret.Add(new LSL_Integer(0));
                            break;
                        case ScriptBaseClass.OBJECT_PHANTOM:
                            ret.Add(new LSL_Integer(0));
                            break;
                        case ScriptBaseClass.OBJECT_TEMP_ON_REZ:
                            ret.Add(new LSL_Integer(0));
                            break;
                        case ScriptBaseClass.OBJECT_RENDER_WEIGHT:
                            ret.Add(new LSL_Integer(-1));
                            break;
                        case ScriptBaseClass.OBJECT_HOVER_HEIGHT:
                            ret.Add(new LSL_Float(0));
                            break;
                        case ScriptBaseClass.OBJECT_BODY_SHAPE_TYPE:
                            LSL_Float shapeType;
                            if (av.Appearance.VisualParams[(int)AvatarAppearance.VPElement.SHAPE_MALE] != 0)
                                shapeType = new LSL_Float(1);
                            else
                                shapeType = new LSL_Float(0);
                            ret.Add(shapeType);
                            break;
                        case ScriptBaseClass.OBJECT_LAST_OWNER_ID:
                            ret.Add(new LSL_Key(ScriptBaseClass.NULL_KEY));
                            break;
                        case ScriptBaseClass.OBJECT_CLICK_ACTION:
                            ret.Add(new LSL_Integer(0));
                            break;
                        case ScriptBaseClass.OBJECT_OMEGA:
                            ret.Add(new LSL_Vector(Vector3.Zero));
                            break;
                        case ScriptBaseClass.OBJECT_PRIM_COUNT:
                            Attachments ??= av.GetAttachments();
                            count = 0;
                            try
                            {
                                foreach (SceneObjectGroup Attachment in Attachments)
                                    count += Attachment.PrimCount;
                            }
                            catch { };
                            ret.Add(new LSL_Integer(count));
                            break;
                        case ScriptBaseClass.OBJECT_TOTAL_INVENTORY_COUNT:
                            Attachments ??= av.GetAttachments();
                            count = 0;
                            try
                            {
                                foreach (SceneObjectGroup Attachment in Attachments)
                                {
                                    SceneObjectPart[] parts = Attachment.Parts;
                                    for(int i = 0; i < parts.Length; i++)
                                        count += parts[i].Inventory.Count;
                                }
                            } catch { };
                            ret.Add(new LSL_Integer(count));
                            break;
                        case ScriptBaseClass.OBJECT_REZZER_KEY:
                            ret.Add(new LSL_Key((string)id));
                            break;
                        case ScriptBaseClass.OBJECT_GROUP_TAG:
                            ret.Add(new LSL_String(av.Grouptitle));
                            break;
                        case ScriptBaseClass.OBJECT_TEMP_ATTACHED:
                            ret.Add(new LSL_Integer(0));
                            break;
                        case ScriptBaseClass.OBJECT_ATTACHED_SLOTS_AVAILABLE:
                            ret.Add(new LSL_Integer(Constants.MaxAgentAttachments - av.GetAttachmentsCount()));
                            break;
                        case ScriptBaseClass.OBJECT_CREATION_TIME:
                            ret.Add(new LSL_String(""));
                            break;
                        case ScriptBaseClass.OBJECT_SELECT_COUNT:
                            ret.Add(new LSL_Integer(0));
                            break;
                        case ScriptBaseClass.OBJECT_SIT_COUNT:
                            ret.Add(new LSL_Integer(0));
                            break;
                        case ScriptBaseClass.OBJECT_ANIMATED_COUNT:
                            count = 0;
                            if (nAnimated.HasValue)
                                count = nAnimated.Value;
                            else
                            {
                                Attachments ??= av.GetAttachments();
                                try
                                {
                                    for(int i = 0; i < Attachments.Count;++i)
                                    {
                                        if(Attachments[i].RootPart.Shape.MeshFlagEntry)
                                            ++count;
                                    }
                                }
                                catch { };
                                nAnimated = count;
                            }
                            ret.Add(new LSL_Integer(count));
                            break;

                        case ScriptBaseClass.OBJECT_ANIMATED_SLOTS_AVAILABLE:
                            count = 0;
                            if (nAnimated.HasValue)
                                count = nAnimated.Value;
                            else
                            {
                                Attachments ??= av.GetAttachments();
                                count = 0;
                                try
                                {
                                    for (int i = 0; i < Attachments.Count; ++i)
                                    {
                                        if (Attachments[i].RootPart.Shape.MeshFlagEntry)
                                            ++count;
                                    }
                                }
                                catch { };
                                nAnimated = count;
                            }
                            count = 2 - count; // for now hardcoded max (simulator features, viewers settings, etc)
                            if(count < 0)
                                count = 0;
                            ret.Add(new LSL_Integer(count));
                            break;

                        case ScriptBaseClass.OBJECT_ACCOUNT_LEVEL:
                            ret.Add(new LSL_Integer(1));
                            break;
                        case ScriptBaseClass.OBJECT_MATERIAL:
                            ret.Add(new LSL_Integer((int)Material.Flesh));
                            break;
                        case ScriptBaseClass.OBJECT_MASS:
                            ret.Add(new LSL_Float(av.GetMass()));
                            break;
                        case ScriptBaseClass.OBJECT_TEXT:
                            ret.Add(new LSL_String(""));
                            break;
                        case ScriptBaseClass.OBJECT_REZ_TIME:
                            ret.Add(new LSL_String(""));
                            break;
                        case ScriptBaseClass.OBJECT_LINK_NUMBER:
                            ret.Add(new LSL_Integer(0));
                            break;
                        case ScriptBaseClass.OBJECT_SCALE:
                            ret.Add(new LSL_Vector(av.Appearance.AvatarBoxSize));
                            break;
                        case ScriptBaseClass.OBJECT_TEXT_COLOR:
                            ret.Add(new LSL_Vector(0f, 0f, 0f));
                            break;
                        case ScriptBaseClass.OBJECT_TEXT_ALPHA:
                            ret.Add(new LSL_Float(1.0f));
                            break;
                        default:
                            // Invalid or unhandled constant.
                            ret.Add(new LSL_Integer(ScriptBaseClass.OBJECT_UNKNOWN_DETAIL));
                            break;
                    }
                }
                return ret;
            }

            SceneObjectPart obj = World.GetSceneObjectPart(key);
            if (obj is not null)
            {
                foreach (object o in args.Data)
                {
                    switch (int.Parse(o.ToString()))
                    {
                        case ScriptBaseClass.OBJECT_NAME:
                            ret.Add(new LSL_String(obj.Name));
                            break;
                        case ScriptBaseClass.OBJECT_DESC:
                            ret.Add(new LSL_String(obj.Description));
                            break;
                        case ScriptBaseClass.OBJECT_POS:
                            ret.Add(new LSL_Vector(obj.AbsolutePosition));
                            break;
                        case ScriptBaseClass.OBJECT_ROT:
                            Quaternion rot;
                            if (obj.ParentGroup.IsAttachment)
                            {
                                ScenePresence sp = World.GetScenePresence(obj.ParentGroup.AttachedAvatar);
                                rot = sp != null ? sp.GetWorldRotation() : Quaternion.Identity;
                            }
                            else
                            {
                                if (obj.ParentGroup.RootPart.LocalId == obj.LocalId)
                                    rot = obj.ParentGroup.GroupRotation;
                                else
                                    rot = obj.GetWorldRotation();
                            }

                            ret.Add(new LSL_Rotation(rot));

                            break;
                        case ScriptBaseClass.OBJECT_VELOCITY:
                            Vector3 vel;
                            if (obj.ParentGroup.IsAttachment)
                            {
                                ScenePresence sp = World.GetScenePresence(obj.ParentGroup.AttachedAvatar);
                                if(sp is null)
                                    vel = Vector3.Zero;
                                else
                                    vel = sp.GetWorldVelocity();
                            }
                            else
                            {
                                vel = obj.Velocity;
                            }

                            ret.Add(new LSL_Vector(vel));
                            break;
                        case ScriptBaseClass.OBJECT_OWNER:
                            ret.Add(new LSL_String(obj.OwnerID.ToString()));
                            break;
                        case ScriptBaseClass.OBJECT_GROUP:
                            ret.Add(new LSL_String(obj.GroupID.ToString()));
                            break;
                        case ScriptBaseClass.OBJECT_CREATOR:
                            ret.Add(new LSL_String(obj.CreatorID.ToString()));
                            break;
                        case ScriptBaseClass.OBJECT_RUNNING_SCRIPT_COUNT:
                            ret.Add(new LSL_Integer(obj.ParentGroup.RunningScriptCount()));
                            break;
                        case ScriptBaseClass.OBJECT_TOTAL_SCRIPT_COUNT:
                            ret.Add(new LSL_Integer(obj.ParentGroup.ScriptCount()));
                            break;
                        case ScriptBaseClass.OBJECT_SCRIPT_MEMORY:
                            // The value returned in SL for mono scripts is 65536 * number of active scripts
                            // and 16384 * number of active scripts for LSO. since llGetFreememory
                            // is coded to give the LSO value use it here
                            ret.Add(new LSL_Integer(obj.ParentGroup.RunningScriptCount() * 16384));
                            break;
                        case ScriptBaseClass.OBJECT_SCRIPT_TIME:
                            // Average cpu time in seconds per simulator frame expended on all scripts in the object
                            ret.Add(new LSL_Float(obj.ParentGroup.ScriptExecutionTime() / 1000.0f));
                            break;
                        case ScriptBaseClass.OBJECT_PRIM_EQUIVALENCE:
                            // according to the SL wiki A prim or linkset will have prim
                            // equivalent of the number of prims in a linkset if it does not
                            // contain a mesh anywhere in the link set or is not a normal prim
                            // The value returned in SL for normal prims is prim count
                            ret.Add(new LSL_Integer(obj.ParentGroup.PrimCount));
                            break;

                        // costs below may need to be diferent for root parts, need to check
                        case ScriptBaseClass.OBJECT_SERVER_COST:
                            // The linden calculation is here
                            // http://wiki.secondlife.com/wiki/Mesh/Mesh_Server_Weight
                            // The value returned in SL for normal prims looks like the prim count
                            ret.Add(new LSL_Float(0));
                            break;
                        case ScriptBaseClass.OBJECT_STREAMING_COST:
                            // The value returned in SL for normal prims is prim count * 0.06
                            ret.Add(new LSL_Float(obj.StreamingCost));
                            break;
                        case ScriptBaseClass.OBJECT_PHYSICS_COST:
                            // The value returned in SL for normal prims is prim count
                            ret.Add(new LSL_Float(obj.PhysicsCost));
                            break;
                        case ScriptBaseClass.OBJECT_CHARACTER_TIME: // Pathfinding
                            ret.Add(new LSL_Float(0));
                            break;
                        case ScriptBaseClass.OBJECT_ROOT:
                            ret.Add(new LSL_String(obj.ParentGroup.RootPart.UUID.ToString()));
                            break;
                        case ScriptBaseClass.OBJECT_ATTACHED_POINT:
                            ret.Add(new LSL_Integer(obj.ParentGroup.AttachmentPoint));
                            break;
                        case ScriptBaseClass.OBJECT_PATHFINDING_TYPE:
                            byte pcode = obj.Shape.PCode;
                            if (obj.ParentGroup.AttachmentPoint != 0
                                || pcode == (byte)PCode.Grass
                                || pcode == (byte)PCode.Tree
                                || pcode == (byte)PCode.NewTree)
                            {
                                ret.Add(new LSL_Integer(ScriptBaseClass.OPT_OTHER));
                            }
                            else
                            {
                                ret.Add(new LSL_Integer(ScriptBaseClass.OPT_LEGACY_LINKSET));
                            }
                            break;
                        case ScriptBaseClass.OBJECT_PHYSICS:
                            if (obj.ParentGroup.AttachmentPoint != 0)
                                ret.Add(new LSL_Integer(0)); // Always false if attached
                            else
                                ret.Add(new LSL_Integer(obj.ParentGroup.UsesPhysics ? 1 : 0));
                            break;
                        case ScriptBaseClass.OBJECT_PHANTOM:
                            if (obj.ParentGroup.AttachmentPoint != 0)
                                ret.Add(new LSL_Integer(0)); // Always false if attached
                            else
                                ret.Add(new LSL_Integer(obj.ParentGroup.IsPhantom ? 1 : 0));
                            break;
                        case ScriptBaseClass.OBJECT_TEMP_ON_REZ:
                            ret.Add(new LSL_Integer(obj.ParentGroup.IsTemporary ? 1 : 0));
                            break;
                        case ScriptBaseClass.OBJECT_RENDER_WEIGHT:
                            ret.Add(new LSL_Integer(0));
                            break;
                        case ScriptBaseClass.OBJECT_HOVER_HEIGHT:
                            ret.Add(new LSL_Float(0));
                            break;
                        case ScriptBaseClass.OBJECT_BODY_SHAPE_TYPE:
                            ret.Add(new LSL_Float(-1));
                            break;
                        case ScriptBaseClass.OBJECT_LAST_OWNER_ID:
                            ret.Add(new LSL_Key(obj.ParentGroup.LastOwnerID.ToString()));
                            break;
                        case ScriptBaseClass.OBJECT_CLICK_ACTION:
                            ret.Add(new LSL_Integer(obj.ClickAction));
                            break;
                        case ScriptBaseClass.OBJECT_OMEGA:
                            ret.Add(new LSL_Vector(obj.AngularVelocity));
                            break;
                        case ScriptBaseClass.OBJECT_PRIM_COUNT:
                            ret.Add(new LSL_Integer(obj.ParentGroup.PrimCount));
                            break;
                        case ScriptBaseClass.OBJECT_TOTAL_INVENTORY_COUNT:
                            SceneObjectPart[] parts = obj.ParentGroup.Parts;
                            count = 0;
                            for(int i = 0; i < parts.Length; i++)
                                count += parts[i].Inventory.Count;
                            ret.Add(new LSL_Integer(count));
                            break;
                        case ScriptBaseClass.OBJECT_REZZER_KEY:
                            ret.Add(new LSL_Key(obj.ParentGroup.RezzerID.ToString()));
                            break;
                        case ScriptBaseClass.OBJECT_GROUP_TAG:
                            ret.Add(new LSL_String(String.Empty));
                            break;
                        case ScriptBaseClass.OBJECT_TEMP_ATTACHED:
                            if (obj.ParentGroup.AttachmentPoint != 0 && obj.ParentGroup.FromItemID.IsZero())
                                ret.Add(new LSL_Integer(1));
                            else
                                ret.Add(new LSL_Integer(0));
                            break;
                        case ScriptBaseClass.OBJECT_ATTACHED_SLOTS_AVAILABLE:
                            ret.Add(new LSL_Integer(0));
                            break;
                        case ScriptBaseClass.OBJECT_CREATION_TIME:
                            DateTime date = Util.ToDateTime(obj.ParentGroup.RootPart.CreationDate);
                            ret.Add(new LSL_String(date.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)));
                            break;
                        case ScriptBaseClass.OBJECT_SELECT_COUNT:
                            ret.Add(new LSL_Integer(0));
                            break;
                        case ScriptBaseClass.OBJECT_SIT_COUNT:
                            ret.Add(new LSL_Integer(obj.ParentGroup.GetSittingAvatarsCount()));
                            break;
                        case ScriptBaseClass.OBJECT_ANIMATED_COUNT:
                            if(obj.ParentGroup.RootPart.Shape.MeshFlagEntry)
                                ret.Add(new LSL_Integer(1));
                            else
                                ret.Add(new LSL_Integer(0));
                            break;
                        case ScriptBaseClass.OBJECT_ANIMATED_SLOTS_AVAILABLE:
                            ret.Add(new LSL_Integer(0));
                            break;
                        case ScriptBaseClass.OBJECT_ACCOUNT_LEVEL:
                            ret.Add(new LSL_Integer(1));
                            break;
                        case ScriptBaseClass.OBJECT_MATERIAL:
                            ret.Add(new LSL_Integer(obj.Material));
                            break;
                        case ScriptBaseClass.OBJECT_MASS:
                            float mass;
                            if (obj.ParentGroup.IsAttachment)
                            {
                                ScenePresence attachedAvatar = World.GetScenePresence(obj.ParentGroup.AttachedAvatar);
                                mass = attachedAvatar is null ? 0 : attachedAvatar.GetMass();
                            }
                            else
                                mass = obj.ParentGroup.GetMass();
                            mass *= 100f;
                            ret.Add(new LSL_Float(mass));
                            break;
                        case ScriptBaseClass.OBJECT_TEXT:
                            ret.Add(new LSL_String(obj.Text));
                            break;
                        case ScriptBaseClass.OBJECT_REZ_TIME:
                            ret.Add(new LSL_String(obj.Rezzed.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ", CultureInfo.InvariantCulture)));
                            break;
                        case ScriptBaseClass.OBJECT_LINK_NUMBER:
                            ret.Add(new LSL_Integer(obj.LinkNum));
                            break;
                        case ScriptBaseClass.OBJECT_SCALE:
                            ret.Add(new LSL_Vector(obj.Scale));
                            break;
                        case ScriptBaseClass.OBJECT_TEXT_COLOR:
                            Color4 textColor = obj.GetTextColor();
                            ret.Add(new LSL_Vector(textColor.R, textColor.G, textColor.B));
                            break;
                        case ScriptBaseClass.OBJECT_TEXT_ALPHA:
                            ret.Add(new LSL_Float(obj.GetTextAlpha()));
                            break;
                        default:
                            // Invalid or unhandled constant.
                            ret.Add(new LSL_Integer(ScriptBaseClass.OBJECT_UNKNOWN_DETAIL));
                            break;
                    }
                }
            }

            return ret;
        }

        internal UUID GetScriptByName(string name)
        {
            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(name);

            if (item == null || item.Type != 10)
                return UUID.Zero;

            return item.ItemID;
        }

        /// <summary>
        /// Reports the script error in the viewer's Script Warning/Error dialog and shouts it on the debug channel.
        /// </summary>
        /// <param name="command">The name of the command that generated the error.</param>
        /// <param name="message">The error message to report to the user.</param>
        internal void Error(string command, string message)
        {
            string text = command + ": " + message;
            if (text.Length > 1023)
                text = text[..1023];

            World.SimChat(Utils.StringToBytes(text), ChatTypeEnum.DebugChannel, ScriptBaseClass.DEBUG_CHANNEL,
                m_host.ParentGroup.RootPart.AbsolutePosition, m_host.Name, m_host.UUID, false);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm?.DeliverMessage(ChatTypeEnum.Shout, ScriptBaseClass.DEBUG_CHANNEL, m_host.Name, m_host.UUID, text);
            Sleep(1000);
        }

        /// <summary>
        /// Reports that the command is not implemented as a script error.
        /// </summary>
        /// <param name="command">The name of the command that is not implemented.</param>
        /// <param name="message">Additional information to report to the user. (Optional)</param>
        internal void NotImplemented(string command, string message = "")
        {
            if (throwErrorOnNotImplemented)
            {
                if (message != "")
                {
                    message = " - " + message;
                }

                throw new NotImplementedException("Command not implemented: " + command + message);
            }
            else
            {
                string text = "Command not implemented";
                if (message != "")
                {
                    text = text + " - " + message;
                }

                Error(command, text);
            }
        }

        /// <summary>
        /// Reports that the command is deprecated as a script error.
        /// </summary>
        /// <param name="command">The name of the command that is deprecated.</param>
        /// <param name="message">Additional information to report to the user. (Optional)</param>
        internal void Deprecated(string command, string message = "")
        {
            string text = "Command deprecated";
            if (message != "")
            {
                text = text + " - " + message;
            }

            Error(command, text);
        }

        public delegate void AssetRequestCallback(UUID assetID, AssetBase asset);
        protected void WithNotecard(UUID assetID, AssetRequestCallback cb)
        {
            World.AssetService.Get(assetID.ToString(), this,
                delegate(string i, object sender, AssetBase a)
                {
                    _ = UUID.TryParse(i, out UUID uuid);
                    cb(uuid, a);
                });
        }

        public LSL_Key llGetNumberOfNotecardLines(string name)
        {
            if (!UUID.TryParse(name, out UUID assetID))
            {
                TaskInventoryItem item = m_host.Inventory.GetInventoryItem(name);

                if (item is not null && item.Type == 7)
                    assetID = item.AssetID;
            }

            if (assetID.IsZero())
            {
                // => complain loudly, as specified by the LSL docs
                Error("llGetNumberOfNotecardLines", "Can't find notecard '" + name + "'");
                return ScriptBaseClass.NULL_KEY;
            }

            if (NotecardCache.IsCached(assetID))
            {
                string ftid = m_AsyncCommands.DataserverPlugin.RequestWithImediatePost(m_host.LocalId, m_item.ItemID, NotecardCache.GetLines(assetID).ToString());
                ScriptSleep(m_sleepMsOnGetNumberOfNotecardLines);
                return ftid;
            }

            void act(string eventID)
            {
                if (NotecardCache.IsCached(assetID))
                {
                    m_AsyncCommands.DataserverPlugin.DataserverReply(eventID, NotecardCache.GetLines(assetID).ToString());
                    return;
                }

                AssetBase a = World.AssetService.Get(assetID.ToString());
                if (a is null || a.Type != 7)
                {
                    m_AsyncCommands.DataserverPlugin.DataserverReply(eventID, string.Empty);
                    return;
                }

                NotecardCache.Cache(assetID, a.Data);
                m_AsyncCommands.DataserverPlugin.DataserverReply(eventID, NotecardCache.GetLines(assetID).ToString());
            }

            UUID tid = m_AsyncCommands.DataserverPlugin.RegisterRequest(m_host.LocalId, m_item.ItemID, act);
            ScriptSleep(m_sleepMsOnGetNumberOfNotecardLines);
            return tid.ToString();
        }

        public LSL_String llGetNotecardLineSync(string name, int line)
        {
            if (line < 0)
                return ScriptBaseClass.NAK;

            if (!UUID.TryParse(name, out UUID assetID))
            {
                TaskInventoryItem item = m_host.Inventory.GetInventoryItem(name, 7);

                if (item is null)
                {
                    Error("llGetNotecardLineSync", "Can't find notecard '" + name + "'");
                    return ScriptBaseClass.NAK;
                }
                assetID = item.AssetID;
            }

            if (NotecardCache.IsCached(assetID))
            {
                return NotecardCache.GetllLine(assetID, line, 1024);
            }
            else
            {
                return ScriptBaseClass.NAK;
            }
        }

        public LSL_Key llGetNotecardLine(string name, int line)
        {
            if (!UUID.TryParse(name, out UUID assetID))
            {
                TaskInventoryItem item = m_host.Inventory.GetInventoryItem(name);

                if (item != null && item.Type == 7)
                    assetID = item.AssetID;
            }

            if (assetID.IsZero())
            {
                // => complain loudly, as specified by the LSL docs
                Error("llGetNotecardLine", "Can't find notecard '" + name + "'");
                return ScriptBaseClass.NULL_KEY;
            }

            if (NotecardCache.IsCached(assetID))
            {
                string eid = m_AsyncCommands.DataserverPlugin.RequestWithImediatePost(m_host.LocalId, m_item.ItemID,
                    NotecardCache.GetLine(assetID, line, m_notecardLineReadCharsMax));

                ScriptSleep(m_sleepMsOnGetNotecardLine);
                return eid;
            }

            void act(string eventID)
            {
                if (NotecardCache.IsCached(assetID))
                {
                    m_AsyncCommands.DataserverPlugin.DataserverReply(eventID, NotecardCache.GetLine(assetID, line, m_notecardLineReadCharsMax));
                    return;
                }

                AssetBase a = World.AssetService.Get(assetID.ToString());
                if (a == null || a.Type != 7)
                {
                    m_AsyncCommands.DataserverPlugin.DataserverReply(eventID, string.Empty);
                    return;
                }

                NotecardCache.Cache(assetID, a.Data);
                m_AsyncCommands.DataserverPlugin.DataserverReply(
                   eventID, NotecardCache.GetLine(assetID, line, m_notecardLineReadCharsMax));
            }

            UUID tid = m_AsyncCommands.DataserverPlugin.RegisterRequest(m_host.LocalId, m_item.ItemID, act);
            ScriptSleep(m_sleepMsOnGetNotecardLine);
            return tid.ToString();
        }

        public void SetPrimitiveParamsEx(LSL_Key prim, LSL_List rules, string originFunc)
        {
            if (!UUID.TryParse(prim, out UUID id) || id.IsZero())
                return;

            SceneObjectPart obj = World.GetSceneObjectPart(id);
            if (obj == null)
                return;

            SceneObjectGroup sog = obj.ParentGroup;
            if (sog == null || sog.IsDeleted)
                return;

            SceneObjectPart objRoot = sog.RootPart;
            if (objRoot == null || objRoot.OwnerID.NotEqual(m_host.OwnerID) || (objRoot.OwnerMask & (uint)PermissionMask.Modify) == 0)
                return;

            uint rulesParsed = 0;
            LSL_List remaining = SetPrimParams(obj, rules, originFunc, ref rulesParsed);

            while (remaining.Length > 2)
            {
                int linknumber;
                try
                {
                    linknumber = remaining.GetIntegerItem(0);
                }
                catch (InvalidCastException)
                {
                    Error(originFunc, string.Format("Error running rule #{0} -> PRIM_LINK_TARGET parameter must be integer", rulesParsed));
                    return;
                }

                List<ISceneEntity> entities = GetLinkEntities(obj, linknumber);
                if (entities.Count == 0)
                    break;

                rules = remaining.GetSublist(1, -1);
                foreach (ISceneEntity entity in entities)
                {
                    if (entity is SceneObjectPart sop)
                        remaining = SetPrimParams(sop, rules, originFunc, ref rulesParsed);
                    else
                        remaining = SetAgentParams((ScenePresence)entity, rules, originFunc, ref rulesParsed);
                }
            }
        }

        public LSL_List GetPrimitiveParamsEx(LSL_Key prim, LSL_List rules)
        {
            LSL_List result = new();

            if (!UUID.TryParse(prim, out UUID id))
                return result;

            SceneObjectPart obj = World.GetSceneObjectPart(id);
            if (obj is null)
                return result;

            SceneObjectGroup sog = obj.ParentGroup;
            if (sog is null || sog.IsDeleted)
                return result;

            SceneObjectPart objRoot = sog.RootPart;
            if (objRoot is null || objRoot.OwnerID.NotEqual(m_host.OwnerID) || (objRoot.OwnerMask & (uint)PermissionMask.Modify) == 0)
                return result;

            LSL_List remaining = GetPrimParams(obj, rules, ref result);

            while (remaining.Length > 1)
            {
                int linknumber;
                try
                {
                    linknumber = remaining.GetIntegerItem(0);
                }
                catch (InvalidCastException)
                {
                    Error("", string.Format("Error PRIM_LINK_TARGET: parameter must be integer"));
                    return result;
                }

                List<ISceneEntity> entities = GetLinkEntities(obj, linknumber);
                if (entities.Count == 0)
                    break;

                rules = remaining.GetSublist(1, -1);
                foreach (ISceneEntity entity in entities)
                {
                    if (entity is SceneObjectPart sop)
                        remaining = GetPrimParams(sop, rules, ref result);
                    else
                        remaining = GetPrimParams((ScenePresence)entity, rules, ref result);
                }
            }

            return result;
        }

        public void print(string str)
        {
            // yes, this is a real LSL function. See: http://wiki.secondlife.com/wiki/Print
            IOSSL_Api ossl = (IOSSL_Api)m_ScriptEngine.GetApi(m_item.ItemID, "OSSL");
            if (ossl != null)
            {
                ossl.CheckThreatLevel(ThreatLevel.High, "print");
                m_log.Info("LSL print():" + str);
            }
        }

        public LSL_Integer llGetLinkNumberOfSides(LSL_Integer link)
        {
            List<SceneObjectPart> parts = GetLinkParts(link);
            if (parts.Count < 1)
                return 0;

            return GetNumberOfSides(parts[0]);
        }

        private static string Name2Username(string name)
        {
            string[] parts = name.Split();
            if (parts.Length < 2)
                return name.ToLower();
            if (parts[1].Equals("Resident"))
                return parts[0].ToLower();

            return name.Replace(" ", ".").ToLower();
        }

        public LSL_String llGetUsername(LSL_Key id)
        {
            return Name2Username(llKey2Name(id));
        }

        public LSL_Key llRequestUsername(LSL_Key id)
        {
            if (!UUID.TryParse(id, out UUID key) || key.IsZero())
                return string.Empty;

            ScenePresence lpresence = World.GetScenePresence(key);
            if (lpresence != null)
            {
                string lname = lpresence.Name;
                string ftid = m_AsyncCommands.DataserverPlugin.RequestWithImediatePost(m_host.LocalId,
                                                    m_item.ItemID, Name2Username(lname));
                return ftid;
            }

            void act(string eventID)
            {
                string name = String.Empty;
                ScenePresence presence = World.GetScenePresence(key);
                if (presence != null)
                {
                    name = presence.Name;
                }
                else if (World.TryGetSceneObjectPart(key, out SceneObjectPart sop))
                {
                    name = sop.Name;
                }
                else
                {
                    UserAccount account = m_userAccountService.GetUserAccount(RegionScopeID, key);
                    if (account != null)
                    {
                        name = account.FirstName + " " + account.LastName;
                    }
                }
                m_AsyncCommands.DataserverPlugin.DataserverReply(eventID, Name2Username(name));
            }

            UUID rq = m_AsyncCommands.DataserverPlugin.RegisterRequest(m_host.LocalId, m_item.ItemID, act);
            ScriptSleep(m_sleepMsOnRequestAgentData);
            return rq.ToString();
        }

        public LSL_String llGetDisplayName(LSL_Key id)
        {
            if (UUID.TryParse(id, out UUID key) && key.IsNotZero())
            {
                ScenePresence presence = World.GetScenePresence(key);
                if (presence != null)
                {
                    return presence.Name;
                }
            }
            return LSL_String.Empty;
        }

        public LSL_Key llRequestDisplayName(LSL_Key id)
        {
            if (!UUID.TryParse(id, out UUID key) || key.IsZero())
                return string.Empty;

            ScenePresence lpresence = World.GetScenePresence(key);
            if (lpresence != null)
            {
                string lname = lpresence.Name;
                string ftid = m_AsyncCommands.DataserverPlugin.RequestWithImediatePost(m_host.LocalId,
                                                                   m_item.ItemID, lname);
                return ftid;
            }

            void act(string eventID)
            {
                string name = string.Empty;
                ScenePresence presence = World.GetScenePresence(key);
                if (presence is not null)
                {
                    name = presence.Name;
                }
                else if (World.TryGetSceneObjectPart(key, out SceneObjectPart sop))
                {
                    name = sop.Name;
                }
                else
                {
                    UserAccount account = m_userAccountService.GetUserAccount(RegionScopeID, key);
                    if (account is not null)
                    {
                        name = account.FirstName + " " + account.LastName;
                    }
                }
                m_AsyncCommands.DataserverPlugin.DataserverReply(eventID, name);
            }

            UUID rq = m_AsyncCommands.DataserverPlugin.RegisterRequest(m_host.LocalId, m_item.ItemID, act);
            return rq.ToString();
        }

        private struct Tri
        {
            public Vector3 p1;
            public Vector3 p2;
            public Vector3 p3;
        }

        private static bool InBoundingBox(ScenePresence avatar, Vector3 point)
        {
            float height = avatar.Appearance.AvatarHeight;
            Vector3 b1 = avatar.AbsolutePosition + new Vector3(-0.22f, -0.22f, -height/2);
            Vector3 b2 = avatar.AbsolutePosition + new Vector3(0.22f, 0.22f, height/2);

            if (point.X > b1.X && point.X < b2.X &&
                point.Y > b1.Y && point.Y < b2.Y &&
                point.Z > b1.Z && point.Z < b2.Z)
                return true;
            return false;
        }

        private ContactResult[] AvatarIntersection(Vector3 rayStart, Vector3 rayEnd, bool skipPhys)
        {
            List<ContactResult> contacts = new();

            Vector3 ab = rayEnd - rayStart;
            float ablen = ab.Length();

            World.ForEachScenePresence(delegate(ScenePresence sp)
            {
                if(skipPhys && sp.PhysicsActor is not null)
                    return;

                Vector3 ac = sp.AbsolutePosition - rayStart;

                double d = Math.Abs(Vector3.Mag(Vector3.Cross(ab, ac)) / ablen);

                if (d > 1.5)
                    return;

                double d2 = Vector3.Dot(Vector3.Negate(ab), ac);

                if (d2 > 0)
                    return;

                double dp = Math.Sqrt(Vector3.Mag(ac) * Vector3.Mag(ac) - d * d);
                Vector3 p = rayStart + Vector3.Divide(Vector3.Multiply(ab, (float)dp), (float)Vector3.Mag(ab));

                if (!InBoundingBox(sp, p))
                    return;

                ContactResult result = new()
                {
                    ConsumerID = sp.LocalId,
                    Depth = Vector3.Distance(rayStart, p),
                    Normal = Vector3.Zero,
                    Pos = p
                };

                contacts.Add(result);
            });

            return contacts.ToArray();
        }

        private ContactResult[] ObjectIntersection(Vector3 rayStart, Vector3 rayEnd, bool includePhysical, bool includeNonPhysical, bool includePhantom)
        {
            Vector3 ab = rayEnd - rayStart;
            Ray ray = new(rayStart, Vector3.Normalize(ref ab));
            List<ContactResult> contacts = new();

            World.ForEachSOG(delegate(SceneObjectGroup group)
            {
                if (m_host.ParentGroup == group)
                    return;

                if (group.IsAttachment)
                    return;

                if (group.RootPart.PhysActor == null)
                {
                    if (!includePhantom)
                        return;
                }
                else
                {
                    if (group.RootPart.PhysActor.IsPhysical)
                    {
                        if (!includePhysical)
                            return;
                    }
                    else
                    {
                        if (!includeNonPhysical)
                            return;
                    }
                }

                // Find the radius ouside of which we don't even need to hit test
                float radius = 0.0f;
                group.GetAxisAlignedBoundingBoxRaw(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ);

                if (Math.Abs(minX) > radius)
                    radius = Math.Abs(minX);
                if (Math.Abs(minY) > radius)
                    radius = Math.Abs(minY);
                if (Math.Abs(minZ) > radius)
                    radius = Math.Abs(minZ);
                if (Math.Abs(maxX) > radius)
                    radius = Math.Abs(maxX);
                if (Math.Abs(maxY) > radius)
                    radius = Math.Abs(maxY);
                if (Math.Abs(maxZ) > radius)
                    radius = Math.Abs(maxZ);
                radius *= 1.413f;
                Vector3 ac = group.AbsolutePosition - rayStart;

                double d = Math.Abs(Vector3.Mag(Vector3.Cross(ab, ac)) / Vector3.Distance(rayStart, rayEnd));

                // Too far off ray, don't bother
                if (d > radius)
                    return;

                // Behind ray, drop
                double d2 = Vector3.Dot(Vector3.Negate(ab), ac);
                if (d2 > 0)
                    return;

                ray = new Ray(rayStart, Vector3.Normalize(ref ab));
                EntityIntersection intersection = group.TestIntersection(ray, true, false);
                // Miss.
                if (!intersection.HitTF)
                    return;

                Vector3 b1 = group.AbsolutePosition + new Vector3(minX, minY, minZ);
                Vector3 b2 = group.AbsolutePosition + new Vector3(maxX, maxY, maxZ);
                //m_log.DebugFormat("[LLCASTRAY]: min<{0},{1},{2}>, max<{3},{4},{5}> = hitp<{6},{7},{8}>", b1.X,b1.Y,b1.Z,b2.X,b2.Y,b2.Z,intersection.ipoint.X,intersection.ipoint.Y,intersection.ipoint.Z);
                if (!(intersection.ipoint.X >= b1.X && intersection.ipoint.X <= b2.X &&
                    intersection.ipoint.Y >= b1.Y && intersection.ipoint.Y <= b2.Y &&
                    intersection.ipoint.Z >= b1.Z && intersection.ipoint.Z <= b2.Z))
                    return;

                ContactResult result = new()
                {
                    ConsumerID = group.LocalId,
                    //Depth = intersection.distance;
                    Normal = intersection.normal,
                    Pos = intersection.ipoint
                };
                result.Depth = Vector3.Mag(rayStart - result.Pos);

                contacts.Add(result);
            });

            return contacts.ToArray();
        }

        private ContactResult? GroundIntersection(Vector3 rayStart, Vector3 rayEnd)
        {
            double[,] heightfield = World.Heightmap.GetDoubles();
            List<ContactResult> contacts = new();

            double min = 2048.0;
            double max = 0.0;

            // Find the min and max of the heightfield
            for (int x = 0 ; x < World.Heightmap.Width ; x++)
            {
                for (int y = 0 ; y < World.Heightmap.Height ; y++)
                {
                    if (heightfield[x, y] > max)
                        max = heightfield[x, y];
                    if (heightfield[x, y] < min)
                        min = heightfield[x, y];
                }
            }


            // A ray extends past rayEnd, but doesn't go back before
            // rayStart. If the start is above the highest point of the ground
            // and the ray goes up, we can't hit the ground. Ever.
            if (rayStart.Z > max && rayEnd.Z >= rayStart.Z)
                return null;

            // Same for going down
            if (rayStart.Z < min && rayEnd.Z <= rayStart.Z)
                return null;

            List<Tri> trilist = new();

            // Create our triangle list
            for (int x = 1 ; x < World.Heightmap.Width ; x++)
            {
                for (int y = 1 ; y < World.Heightmap.Height ; y++)
                {
                    Tri t1 = new();
                    Tri t2 = new();

                    Vector3 p1 = new(x-1, y-1, (float)heightfield[x-1, y-1]);
                    Vector3 p2 = new(x, y-1, (float)heightfield[x, y-1]);
                    Vector3 p3 = new(x, y, (float)heightfield[x, y]);
                    Vector3 p4 = new(x-1, y, (float)heightfield[x-1, y]);

                    t1.p1 = p1;
                    t1.p2 = p2;
                    t1.p3 = p3;

                    t2.p1 = p3;
                    t2.p2 = p4;
                    t2.p3 = p1;

                    trilist.Add(t1);
                    trilist.Add(t2);
                }
            }

            // Ray direction
            Vector3 rayDirection = rayEnd - rayStart;

            foreach (Tri t in trilist)
            {
                // Compute triangle plane normal and edges
                Vector3 u = t.p2 - t.p1;
                Vector3 v = t.p3 - t.p1;
                Vector3 n = Vector3.Cross(u, v);

                if (n.IsZero())
                    continue;

                Vector3 w0 = rayStart - t.p1;
                double a = -Vector3.Dot(n, w0);
                double b = Vector3.Dot(n, rayDirection);

                // Not intersecting the plane, or in plane (same thing)
                // Ignoring this MAY cause the ground to not be detected
                // sometimes
                if (Math.Abs(b) < 0.000001)
                    continue;

                double r = a / b;

                // ray points away from plane
                if (r < 0.0)
                    continue;

                Vector3 ip = rayStart + Vector3.Multiply(rayDirection, (float)r);

                float uu = Vector3.Dot(u, u);
                float uv = Vector3.Dot(u, v);
                float vv = Vector3.Dot(v, v);
                Vector3 w = ip - t.p1;
                float wu = Vector3.Dot(w, u);
                float wv = Vector3.Dot(w, v);
                float d = uv * uv - uu * vv;

                float cs = (uv * wv - vv * wu) / d;
                if (cs < 0 || cs > 1.0)
                    continue;
                float ct = (uv * wu - uu * wv) / d;
                if (ct < 0 || (cs + ct) > 1.0)
                    continue;

                // Add contact point
                ContactResult result = new()
                {
                    ConsumerID = 0,
                    Depth = Vector3.Distance(rayStart, ip),
                    Normal = n,
                    Pos = ip
                };

                contacts.Add(result);
            }

            if (contacts.Count == 0)
                return null;

            contacts.Sort(delegate(ContactResult a, ContactResult b)
            {
                return (int)(a.Depth - b.Depth);
            });

            return contacts[0];
        }
/*
        // not done:
        private ContactResult[] testRay2NonPhysicalPhantom(Vector3 rayStart, Vector3 raydir, float raylenght)
        {
            ContactResult[] contacts = null;
            World.ForEachSOG(delegate(SceneObjectGroup group)
            {
                if (m_host.ParentGroup == group)
                    return;

                if (group.IsAttachment)
                    return;

                if(group.RootPart.PhysActor != null)
                    return;

                contacts = group.RayCastGroupPartsOBBNonPhysicalPhantom(rayStart, raydir, raylenght);
            });
            return contacts;
        }
*/

        public LSL_List llCastRay(LSL_Vector start, LSL_Vector end, LSL_List options)
        {
            LSL_List list = new();

            Vector3 rayStart = start;
            Vector3 rayEnd = end;
            Vector3 dir = rayEnd - rayStart;

            float dist = dir.LengthSquared();
            if (dist < 1e-6)
            {
                list.Add(new LSL_Integer(0));
                return list;
            }

            int count = 1;
            bool detectPhantom = false;
            int dataFlags = 0;
            int rejectTypes = 0;

            for (int i = 0; i < options.Length; i += 2)
            {
                if (options.GetIntegerItem(i) == ScriptBaseClass.RC_MAX_HITS)
                    count = options.GetIntegerItem(i + 1);
                else if (options.GetIntegerItem(i) == ScriptBaseClass.RC_DETECT_PHANTOM)
                    detectPhantom = (options.GetIntegerItem(i + 1) > 0);
                else if (options.GetIntegerItem(i) == ScriptBaseClass.RC_DATA_FLAGS)
                    dataFlags = options.GetIntegerItem(i + 1);
                else if (options.GetIntegerItem(i) == ScriptBaseClass.RC_REJECT_TYPES)
                    rejectTypes = options.GetIntegerItem(i + 1);
            }

            if (count > 16)
                count = 16;

            List<ContactResult> results = new();

            bool checkTerrain = (rejectTypes & ScriptBaseClass.RC_REJECT_LAND) == 0;
            bool checkAgents = (rejectTypes & ScriptBaseClass.RC_REJECT_AGENTS) == 0;
            bool checkNonPhysical = (rejectTypes & ScriptBaseClass.RC_REJECT_NONPHYSICAL) == 0;
            bool checkPhysical = (rejectTypes & ScriptBaseClass.RC_REJECT_PHYSICAL) == 0;
            bool rejectHost = (rejectTypes & ScriptBaseClass.RC_REJECT_HOST) != 0;
            bool rejectHostGroup = (rejectTypes & ScriptBaseClass.RC_REJECT_HOSTGROUP) != 0;

            if (World.SupportsRayCastFiltered())
            {
                RayFilterFlags rayfilter = 0;
                if (checkTerrain)
                    rayfilter = RayFilterFlags.land;
                if (checkAgents)
                    rayfilter |= RayFilterFlags.agent;
                if (checkPhysical)
                    rayfilter |= RayFilterFlags.physical;
                if (checkNonPhysical)
                    rayfilter |= RayFilterFlags.nonphysical;
                if (detectPhantom)
                    rayfilter |= RayFilterFlags.LSLPhantom;

                if(rayfilter == 0)
                {
                    list.Add(new LSL_Integer(0));
                    return list;
                }

                rayfilter |= RayFilterFlags.BackFaceCull;

                dist = (float)Math.Sqrt(dist);
                Vector3 direction = dir * (1.0f / dist);

                // get some more contacts to sort ???
                object physresults = World.RayCastFiltered(rayStart, direction, dist, 2 * count, rayfilter);

                if (physresults != null)
                {
                    results = (List<ContactResult>)physresults;
                }

                // for now physics doesn't detect sitted avatars so do it outside physics
                if (checkAgents)
                {
                    ContactResult[] agentHits = AvatarIntersection(rayStart, rayEnd, true);
                    foreach (ContactResult r in agentHits)
                        results.Add(r);
                }

                // TODO: Replace this with a better solution. ObjectIntersection can only
                // detect nonphysical phantoms. They are detected by virtue of being
                // nonphysical (e.g. no PhysActor) so will not conflict with detecting
                // physicsl phantoms as done by the physics scene
                // We don't want anything else but phantoms here.
                if (detectPhantom)
                {
                    ContactResult[] objectHits = ObjectIntersection(rayStart, rayEnd, false, false, true);
                    foreach (ContactResult r in objectHits)
                        results.Add(r);
                }
                // Double check this because of current ODE distance problems
                if (checkTerrain && dist > 60)
                {
                    bool skipGroundCheck = false;

                    foreach (ContactResult c in results)
                    {
                        if (c.ConsumerID == 0) // Physics gave us a ground collision
                            skipGroundCheck = true;
                    }

                    if (!skipGroundCheck)
                    {
                        float tmp = dir.X * dir.X + dir.Y * dir.Y;
                        if(tmp > 2500)
                        {
                            ContactResult? groundContact = GroundIntersection(rayStart, rayEnd);
                            if (groundContact != null)
                                results.Add((ContactResult)groundContact);
                        }
                    }
                }
            }
            else
            {
                if (checkAgents)
                {
                    ContactResult[] agentHits = AvatarIntersection(rayStart, rayEnd, false);
                    foreach (ContactResult r in agentHits)
                        results.Add(r);
                }

                if (checkPhysical || checkNonPhysical || detectPhantom)
                {
                    ContactResult[] objectHits = ObjectIntersection(rayStart, rayEnd, checkPhysical, checkNonPhysical, detectPhantom);
                    for (int iter = 0; iter < objectHits.Length; iter++)
                    {
                        // Redistance the Depth because the Scene RayCaster returns distance from center to make the rezzing code simpler.
                        objectHits[iter].Depth = Vector3.Distance(objectHits[iter].Pos, rayStart);
                        results.Add(objectHits[iter]);
                    }
                }

                if (checkTerrain)
                {
                    ContactResult? groundContact = GroundIntersection(rayStart, rayEnd);
                    if (groundContact != null)
                        results.Add((ContactResult)groundContact);
                }
            }

            results.Sort(delegate(ContactResult a, ContactResult b)
            {
                return a.Depth.CompareTo(b.Depth);
            });

            int values = 0;
            SceneObjectGroup thisgrp = m_host.ParentGroup;

            foreach (ContactResult result in results)
            {
                if (result.Depth > dist)
                    continue;

                // physics ray can return colisions with host prim
                if (rejectHost && m_host.LocalId == result.ConsumerID)
                    continue;

                UUID itemID = UUID.Zero;
                int linkNum = 0;

                SceneObjectPart part = World.GetSceneObjectPart(result.ConsumerID);
                // It's a prim!
                if (part != null)
                {
                    if (rejectHostGroup && part.ParentGroup == thisgrp)
                        continue;

                    if ((dataFlags & ScriptBaseClass.RC_GET_ROOT_KEY) != 0)
                        itemID = part.ParentGroup.UUID;
                    else
                        itemID = part.UUID;

                    linkNum = part.LinkNum;
                }
                else
                {
                    ScenePresence sp = World.GetScenePresence(result.ConsumerID);
                    /// It it a boy? a girl?
                    if (sp != null)
                        itemID = sp.UUID;
                }

                list.Add(new LSL_String(itemID.ToString()));
                list.Add(new LSL_String(result.Pos.ToString()));

                if ((dataFlags & ScriptBaseClass.RC_GET_LINK_NUM) != 0)
                    list.Add(new LSL_Integer(linkNum));

                if ((dataFlags & ScriptBaseClass.RC_GET_NORMAL) != 0)
                    list.Add(new LSL_Vector(result.Normal));

                values++;
                if (values >= count)
                    break;
            }

            list.Add(new LSL_Integer(values));
            return list;
        }


        /// <summary>
        /// Implementation of llCastRay similar to SL 2015-04-21.
        /// http://wiki.secondlife.com/wiki/LlCastRay
        /// Uses pure geometry, bounding shapes, meshing and no physics
        /// for prims, sculpts, meshes, avatars and terrain.
        /// Implements all flags, reject types and data flags.
        /// Can handle both objects/groups and prims/parts, by config.
        /// May sometimes be inaccurate owing to calculation precision,
        /// meshing detail level and a bug in libopenmetaverse PrimMesher.
        /// </summary>
        public LSL_List llCastRayV3(LSL_Vector start, LSL_Vector end, LSL_List options)
        {
            LSL_List result = new();

            // Prepare throttle data
            int calledMs = Environment.TickCount;
            Stopwatch stopWatch = new();
            stopWatch.Start();
            UUID regionId = World.RegionInfo.RegionID;
            UUID userId = UUID.Zero;
            int msAvailable = 0;
            // Throttle per owner when attachment or "vehicle" (sat upon)
            if (m_host.ParentGroup.IsAttachment || m_host.ParentGroup.GetSittingAvatarsCount() > 0)
            {
                userId = m_host.OwnerID;
                msAvailable = m_msPerAvatarInCastRay;
            }
            // Throttle per parcel when not attachment or vehicle
            else
            {
                LandData land = World.GetLandData(m_host.GetWorldPosition());
                if (land != null)
                    msAvailable = m_msPerRegionInCastRay * land.Area / 65536;
            }
            // Clamp for "oversized" parcels on varregions
            if (msAvailable > m_msMaxInCastRay)
                msAvailable = m_msMaxInCastRay;

            // Check throttle data
            int fromCalledMs = calledMs - m_msThrottleInCastRay;
            lock (m_castRayCalls)
            {
                for (int i = m_castRayCalls.Count - 1; i >= 0; i--)
                {
                    // Delete old calls from throttle data
                    if (m_castRayCalls[i].CalledMs < fromCalledMs)
                        m_castRayCalls.RemoveAt(i);
                    // Use current region (in multi-region sims)
                    else if (m_castRayCalls[i].RegionId.Equals(regionId))
                    {
                        // Reduce available time with recent calls
                        if (m_castRayCalls[i].UserId.Equals(userId))
                            msAvailable -= m_castRayCalls[i].UsedMs;
                    }
                }

                // Return failure if not enough available time
                if (msAvailable < m_msMinInCastRay)
                {
                    result.Add(new LSL_Integer(ScriptBaseClass.RCERR_CAST_TIME_EXCEEDED));
                    return result;
                }
            }

            // Initialize
            List<RayHit> rayHits = new();
            float tol = m_floatToleranceInCastRay;
            Vector3 pos1Ray = start;
            Vector3 pos2Ray = end;

            // Get input options
            int rejectTypes = 0;
            int dataFlags = 0;
            int maxHits = 1;
            bool notdetectPhantom = true;
            for (int i = 0; i < options.Length; i += 2)
            {
                if (options.GetIntegerItem(i) == ScriptBaseClass.RC_REJECT_TYPES)
                    rejectTypes = options.GetIntegerItem(i + 1);
                else if (options.GetIntegerItem(i) == ScriptBaseClass.RC_DATA_FLAGS)
                    dataFlags = options.GetIntegerItem(i + 1);
                else if (options.GetIntegerItem(i) == ScriptBaseClass.RC_MAX_HITS)
                    maxHits = options.GetIntegerItem(i + 1);
                else if (options.GetIntegerItem(i) == ScriptBaseClass.RC_DETECT_PHANTOM)
                    notdetectPhantom = (options.GetIntegerItem(i + 1) == 0);
            }
            if (maxHits > m_maxHitsInCastRay)
                maxHits = m_maxHitsInCastRay;
            bool rejectAgents = ((rejectTypes & ScriptBaseClass.RC_REJECT_AGENTS) != 0);
            bool rejectPhysical = ((rejectTypes & ScriptBaseClass.RC_REJECT_PHYSICAL) != 0);
            bool rejectNonphysical = ((rejectTypes & ScriptBaseClass.RC_REJECT_NONPHYSICAL) != 0);
            bool rejectLand = ((rejectTypes & ScriptBaseClass.RC_REJECT_LAND) != 0);
            bool getNormal = ((dataFlags & ScriptBaseClass.RC_GET_NORMAL) != 0);
            bool getRootKey = ((dataFlags & ScriptBaseClass.RC_GET_ROOT_KEY) != 0);
            bool getLinkNum = ((dataFlags & ScriptBaseClass.RC_GET_LINK_NUM) != 0);

            // Calculate some basic parameters
            Vector3 vecRay = pos2Ray - pos1Ray;
            float rayLength = vecRay.Length();

            // Try to get a mesher and return failure if none, degenerate ray, or max 0 hits
            IRendering primMesher = null;
            List<string> renderers = RenderingLoader.ListRenderers(Util.ExecutingDirectory());
            if (renderers.Count < 1 || rayLength < tol || m_maxHitsInCastRay < 1)
            {
                result.Add(new LSL_Integer(ScriptBaseClass.RCERR_UNKNOWN));
                return result;
            }
            primMesher = RenderingLoader.LoadRenderer(renderers[0]);

            // Iterate over all objects/groups and prims/parts in region
            World.ForEachSOG(
                delegate(SceneObjectGroup group)
                {
                    if(group.IsDeleted || group.RootPart == null)
                        return;
                    // Check group filters unless part filters are configured
                    bool isPhysical = (group.RootPart.PhysActor != null && group.RootPart.PhysActor.IsPhysical);
                    bool isNonphysical = !isPhysical;
                    bool isPhantom = group.IsPhantom || group.IsVolumeDetect;
                    bool isAttachment = group.IsAttachment;
                    if (isPhysical && rejectPhysical)
                        return;
                    if (isNonphysical && rejectNonphysical)
                        return;
                    if (isPhantom && notdetectPhantom)
                        return;
                    if (isAttachment && !m_doAttachmentsInCastRay)
                        return;

                    // Parse object/group if passed filters
                    // Iterate over all prims/parts in object/group
                    foreach(SceneObjectPart part in group.Parts)
                    {
                        // ignore PhysicsShapeType.None as physics engines do
                        // or we will get into trouble in future
                        if(part.PhysicsShapeType == (byte)PhysicsShapeType.None)
                            continue;
                        isPhysical = (part.PhysActor != null && part.PhysActor.IsPhysical);
                        isNonphysical = !isPhysical;
                        isPhantom = ((part.Flags & PrimFlags.Phantom) != 0) ||
                                            (part.VolumeDetectActive);

                        if (isPhysical && rejectPhysical)
                            continue;
                        if (isNonphysical && rejectNonphysical)
                            continue;
                        if (isPhantom && notdetectPhantom)
                            continue;

                        // Parse prim/part and project ray if passed filters
                        Vector3 scalePart = part.Scale;
                        Vector3 posPart = part.GetWorldPosition();
                        Quaternion rotPart = part.GetWorldRotation();
                        Quaternion rotPartInv = Quaternion.Inverse(rotPart);
                        Vector3 pos1RayProj = ((pos1Ray - posPart) * rotPartInv) / scalePart;
                        Vector3 pos2RayProj = ((pos2Ray - posPart) * rotPartInv) / scalePart;

                        // Filter parts by shape bounding boxes
                        Vector3 shapeBoxMax = new(0.5f, 0.5f, 0.5f);
                        if (!part.Shape.SculptEntry)
                            shapeBoxMax *=  new Vector3(m_primSafetyCoeffX, m_primSafetyCoeffY, m_primSafetyCoeffZ);
                        shapeBoxMax += new Vector3(tol, tol, tol);
                        if (RayIntersectsShapeBox(pos1RayProj, pos2RayProj, shapeBoxMax))
                        {
                            // Prepare data needed to check for ray hits
                            RayTrans rayTrans = new()
                            {
                                PartId = part.UUID,
                                GroupId = part.ParentGroup.UUID,
                                Link = group.PrimCount > 1 ? part.LinkNum : 0,
                                ScalePart = scalePart,
                                PositionPart = posPart,
                                RotationPart = rotPart,
                                ShapeNeedsEnds = true,
                                Position1Ray = pos1Ray,
                                Position1RayProj = pos1RayProj,
                                VectorRayProj = pos2RayProj - pos1RayProj
                            };

                            // Get detail level depending on type
                            int lod = 0;
                            // Mesh detail level
                            if (part.Shape.SculptEntry && part.Shape.SculptType == (byte)SculptType.Mesh)
                                lod = (int)m_meshLodInCastRay;
                            // Sculpt detail level
                            else if (part.Shape.SculptEntry && part.Shape.SculptType == (byte)SculptType.Mesh)
                                lod = (int)m_sculptLodInCastRay;
                            // Shape detail level
                            else if (!part.Shape.SculptEntry)
                                lod = (int)m_primLodInCastRay;

                            // Try to get cached mesh if configured
                            ulong meshKey = 0;
                            FacetedMesh mesh = null;
                            if (m_useMeshCacheInCastRay)
                            {
                                meshKey = part.Shape.GetMeshKey(Vector3.One, (float)(4 << lod));
                                lock (m_cachedMeshes)
                                {
                                    m_cachedMeshes.TryGetValue(meshKey, out mesh);
                                }
                            }

                            // Create mesh if no cached mesh
                            if (mesh == null)
                            {
                                // Make an OMV prim to be able to mesh part
                                Primitive omvPrim = part.Shape.ToOmvPrimitive(posPart, rotPart);
                                byte[] sculptAsset = null;
                                if (omvPrim.Sculpt != null)
                                    sculptAsset = World.AssetService.GetData(omvPrim.Sculpt.SculptTexture.ToString());

                                // When part is mesh, get mesh
                                if (omvPrim.Sculpt != null && omvPrim.Sculpt.Type == SculptType.Mesh && sculptAsset != null)
                                {
                                    AssetMesh meshAsset = new(omvPrim.Sculpt.SculptTexture, sculptAsset);
                                    FacetedMesh.TryDecodeFromAsset(omvPrim, meshAsset, m_meshLodInCastRay, out mesh);
                                    meshAsset = null;
                                }

                                // When part is sculpt, create mesh
                                // Quirk: Generated sculpt mesh is about 2.8% smaller in X and Y than visual sculpt.
                                else if (omvPrim.Sculpt != null && omvPrim.Sculpt.Type != SculptType.Mesh && sculptAsset != null)
                                {
                                    IJ2KDecoder imgDecoder = World.RequestModuleInterface<IJ2KDecoder>();
                                    if (imgDecoder != null)
                                    {
                                        Image sculpt = imgDecoder.DecodeToImage(sculptAsset);
                                        if (sculpt != null)
                                        {
                                            mesh = primMesher.GenerateFacetedSculptMesh(omvPrim, (Bitmap)sculpt, m_sculptLodInCastRay);
                                            sculpt.Dispose();
                                        }
                                    }
                                }

                                // When part is shape, create mesh
                                else if (omvPrim.Sculpt == null)
                                {
                                    if (
                                        omvPrim.PrimData.PathBegin == 0.0 && omvPrim.PrimData.PathEnd == 1.0 &&
                                        omvPrim.PrimData.PathTaperX == 0.0 && omvPrim.PrimData.PathTaperY == 0.0 &&
                                        omvPrim.PrimData.PathSkew == 0.0 &&
                                        omvPrim.PrimData.PathTwist - omvPrim.PrimData.PathTwistBegin == 0.0
                                    )
                                        rayTrans.ShapeNeedsEnds = false;
                                    mesh = primMesher.GenerateFacetedMesh(omvPrim, m_primLodInCastRay);
                                }

                                // Cache mesh if configured
                                if (m_useMeshCacheInCastRay && mesh != null)
                                {
                                    lock(m_cachedMeshes)
                                    {
                                        if (!m_cachedMeshes.ContainsKey(meshKey))
                                            m_cachedMeshes.Add(meshKey, mesh);
                                    }
                                }
                            }
                            // Check mesh for ray hits
                            AddRayInFacetedMesh(mesh, rayTrans, ref rayHits);
                            mesh = null;
                        }
                    }
                }
            );

            // Check avatar filter
            if (!rejectAgents)
            {
                // Iterate over all avatars in region
                World.ForEachRootScenePresence(
                    delegate (ScenePresence sp)
                    {
                        // Get bounding box
                        BoundingBoxOfScenePresence(sp, out Vector3 lower, out Vector3 upper);
                        // Parse avatar
                        Vector3 scalePart = upper - lower;
                        Vector3 posPart = sp.AbsolutePosition;
                        Quaternion rotPart = sp.GetWorldRotation();
                        Quaternion rotPartInv = Quaternion.Inverse(rotPart);
                        posPart += (lower + upper) * 0.5f * rotPart;
                        // Project ray
                        Vector3 pos1RayProj = ((pos1Ray - posPart) * rotPartInv) / scalePart;
                        Vector3 pos2RayProj = ((pos2Ray - posPart) * rotPartInv) / scalePart;

                        // Filter avatars by shape bounding boxes
                        Vector3 shapeBoxMax = new(0.5f + tol, 0.5f + tol, 0.5f + tol);
                        if (RayIntersectsShapeBox(pos1RayProj, pos2RayProj, shapeBoxMax))
                        {
                            // Prepare data needed to check for ray hits
                            RayTrans rayTrans = new()
                            {
                                PartId = sp.UUID,
                                GroupId = sp.ParentPart != null ? sp.ParentPart.ParentGroup.UUID : sp.UUID,
                                Link = sp.ParentPart != null ? UUID2LinkNumber(sp.ParentPart, sp.UUID) : 0,
                                ScalePart = scalePart,
                                PositionPart = posPart,
                                RotationPart = rotPart,
                                ShapeNeedsEnds = false,
                                Position1Ray = pos1Ray,
                                Position1RayProj = pos1RayProj,
                                VectorRayProj = pos2RayProj - pos1RayProj
                            };

                            // Try to get cached mesh if configured
                            PrimitiveBaseShape prim = PrimitiveBaseShape.CreateSphere();
                            int lod = (int)m_avatarLodInCastRay;
                            ulong meshKey = prim.GetMeshKey(Vector3.One, (float)(4 << lod));
                            FacetedMesh mesh = null;
                            if (m_useMeshCacheInCastRay)
                            {
                                lock (m_cachedMeshes)
                                {
                                    m_cachedMeshes.TryGetValue(meshKey, out mesh);
                                }
                            }

                            // Create mesh if no cached mesh
                            if (mesh == null)
                            {
                                // Make OMV prim and create mesh
                                prim.Scale = scalePart;
                                Primitive omvPrim = prim.ToOmvPrimitive(posPart, rotPart);
                                mesh = primMesher.GenerateFacetedMesh(omvPrim, m_avatarLodInCastRay);

                                // Cache mesh if configured
                                if (m_useMeshCacheInCastRay && mesh != null)
                                {
                                    lock(m_cachedMeshes)
                                    {
                                        if (!m_cachedMeshes.ContainsKey(meshKey))
                                            m_cachedMeshes.Add(meshKey, mesh);
                                    }
                                }
                            }

                            // Check mesh for ray hits
                            AddRayInFacetedMesh(mesh, rayTrans, ref rayHits);
                            mesh = null;
                        }
                    }
                );
            }

            // Check terrain filter
            if (!rejectLand)
            {
                // Parse terrain

                // Mesh terrain and check bounding box
                List<Tri> triangles = TrisFromHeightmapUnderRay(pos1Ray, pos2Ray, out Vector3 lower, out Vector3 upper);
                lower.Z -= tol;
                upper.Z += tol;
                if ((pos1Ray.Z >= lower.Z || pos2Ray.Z >= lower.Z) && (pos1Ray.Z <= upper.Z || pos2Ray.Z <= upper.Z))
                {
                    // Prepare data needed to check for ray hits
                    RayTrans rayTrans = new()
                    {
                        PartId = UUID.Zero,
                        GroupId = UUID.Zero,
                        Link = 0,
                        ScalePart = new Vector3(1.0f, 1.0f, 1.0f),
                        PositionPart = Vector3.Zero,
                        RotationPart = Quaternion.Identity,
                        ShapeNeedsEnds = true,
                        Position1Ray = pos1Ray,
                        Position1RayProj = pos1Ray,
                        VectorRayProj = vecRay
                    };

                    // Check mesh
                    AddRayInTris(triangles, rayTrans, ref rayHits);
                    triangles = null;
                }
            }

            // Sort hits by ascending distance
            rayHits.Sort((s1, s2) => s1.Distance.CompareTo(s2.Distance));

            // Check excess hits per part and group
            for (int t = 0; t < 2; t++)
            {
                int maxHitsPerType = 0;
                UUID id = UUID.Zero;
                if (t == 0)
                    maxHitsPerType = m_maxHitsPerPrimInCastRay;
                else
                    maxHitsPerType = m_maxHitsPerObjectInCastRay;

                // Handle excess hits only when needed
                if (maxHitsPerType < m_maxHitsInCastRay)
                {
                    // Find excess hits
                    Hashtable hits = new();
                    for (int i = rayHits.Count - 1; i >= 0; i--)
                    {
                        if (t == 0)
                            id = rayHits[i].PartId;
                        else
                            id = rayHits[i].GroupId;
                        if (hits.ContainsKey(id))
                            hits[id] = (int)hits[id] + 1;
                        else
                            hits[id] = 1;
                    }

                    // Remove excess hits
                    for (int i = rayHits.Count - 1; i >= 0; i--)
                    {
                        if (t == 0)
                            id = rayHits[i].PartId;
                        else
                            id = rayHits[i].GroupId;
                        int hit = (int)hits[id];
                        if (hit > m_maxHitsPerPrimInCastRay)
                        {
                            rayHits.RemoveAt(i);
                            hit--;
                            hits[id] = hit;
                        }
                    }
                }
            }

            // Parse hits into result list according to data flags
            int hitCount = rayHits.Count;
            if (hitCount > maxHits)
                hitCount = maxHits;
            for (int i = 0; i < hitCount; i++)
            {
                RayHit rayHit = rayHits[i];
                if (getRootKey)
                    result.Add(new LSL_Key(rayHit.GroupId.ToString()));
                else
                    result.Add(new LSL_Key(rayHit.PartId.ToString()));
                result.Add(new LSL_Vector(rayHit.Position));
                if (getLinkNum)
                    result.Add(new LSL_Integer(rayHit.Link));
                if (getNormal)
                    result.Add(new LSL_Vector(rayHit.Normal));
            }
            result.Add(new LSL_Integer(hitCount));

            // Add to throttle data
            stopWatch.Stop();
            lock (m_castRayCalls)
            {
                CastRayCall castRayCall = new()
                {
                    RegionId = regionId,
                    UserId = userId,
                    CalledMs = calledMs,
                    UsedMs = (int)stopWatch.ElapsedMilliseconds
                };
                m_castRayCalls.Add(castRayCall);
            }

            // Return hits
            return result;
        }

        /// <summary>
        /// Struct for transmitting parameters required for finding llCastRay ray hits.
        /// </summary>
        public struct RayTrans
        {
            public UUID PartId;
            public UUID GroupId;
            public int Link;
            public Vector3 ScalePart;
            public Vector3 PositionPart;
            public Quaternion RotationPart;
            public bool ShapeNeedsEnds;
            public Vector3 Position1Ray;
            public Vector3 Position1RayProj;
            public Vector3 VectorRayProj;
        }

        /// <summary>
        /// Struct for llCastRay ray hits.
        /// </summary>
        public struct RayHit
        {
            public UUID PartId;
            public UUID GroupId;
            public int Link;
            public Vector3 Position;
            public Vector3 Normal;
            public float Distance;
        }

        /// <summary>
        /// Struct for llCastRay throttle data.
        /// </summary>
        public struct CastRayCall
        {
            public UUID RegionId;
            public UUID UserId;
            public int CalledMs;
            public int UsedMs;
        }

        /// <summary>
        /// Helper to check if a ray intersects a shape bounding box.
        /// </summary>
        private bool RayIntersectsShapeBox(Vector3 pos1RayProj, Vector3 pos2RayProj, Vector3 shapeBoxMax)
        {
            // Skip if ray can't intersect bounding box;
            Vector3 rayBoxProjMin = Vector3.Min(pos1RayProj, pos2RayProj);
            Vector3 rayBoxProjMax = Vector3.Max(pos1RayProj, pos2RayProj);
            if (
                rayBoxProjMin.X > shapeBoxMax.X || rayBoxProjMin.Y > shapeBoxMax.Y || rayBoxProjMin.Z > shapeBoxMax.Z ||
                rayBoxProjMax.X < -shapeBoxMax.X || rayBoxProjMax.Y < -shapeBoxMax.Y || rayBoxProjMax.Z < -shapeBoxMax.Z
            )
                return false;

            // Check if ray intersect any bounding box side
            int sign;
            float dist;
            Vector3 posProj;
            Vector3 vecRayProj = pos2RayProj - pos1RayProj;

            // Check both X sides unless ray is parallell to them
            if (Math.Abs(vecRayProj.X) > m_floatToleranceInCastRay)
            {
                for (sign = -1; sign <= 1; sign += 2)
                {
                    dist = ((float)sign * shapeBoxMax.X - pos1RayProj.X) / vecRayProj.X;
                    posProj = pos1RayProj + vecRayProj * dist;
                    if (Math.Abs(posProj.Y) <= shapeBoxMax.Y && Math.Abs(posProj.Z) <= shapeBoxMax.Z)
                        return true;
                }
            }

            // Check both Y sides unless ray is parallell to them
            if (Math.Abs(vecRayProj.Y) > m_floatToleranceInCastRay)
            {
                for (sign = -1; sign <= 1; sign += 2)
                {
                    dist = ((float)sign * shapeBoxMax.Y - pos1RayProj.Y) / vecRayProj.Y;
                    posProj = pos1RayProj + vecRayProj * dist;
                    if (Math.Abs(posProj.X) <= shapeBoxMax.X && Math.Abs(posProj.Z) <= shapeBoxMax.Z)
                        return true;
                }
            }

            // Check both Z sides unless ray is parallell to them
            if (Math.Abs(vecRayProj.Z) > m_floatToleranceInCastRay)
            {
                for (sign = -1; sign <= 1; sign += 2)
                {
                    dist = ((float)sign * shapeBoxMax.Z - pos1RayProj.Z) / vecRayProj.Z;
                    posProj = pos1RayProj + vecRayProj * dist;
                    if (Math.Abs(posProj.X) <= shapeBoxMax.X && Math.Abs(posProj.Y) <= shapeBoxMax.Y)
                        return true;
                }
            }

            // No hits on bounding box so return false
            return false;
        }

        /// <summary>
        /// Helper to parse FacetedMesh for ray hits.
        /// </summary>
        private void AddRayInFacetedMesh(FacetedMesh mesh, RayTrans rayTrans, ref List<RayHit> rayHits)
        {
            if (mesh != null)
            {
                foreach (Face face in mesh.Faces)
                {
                    for (int i = 0; i < face.Indices.Count; i += 3)
                    {
                        Tri triangle = new()
                        {
                            p1 = face.Vertices[face.Indices[i]].Position,
                            p2 = face.Vertices[face.Indices[i + 1]].Position,
                            p3 = face.Vertices[face.Indices[i + 2]].Position
                        };
                        AddRayInTri(triangle, rayTrans, ref rayHits);
                    }
                }
            }
        }

        /// <summary>
        /// Helper to parse Tri (triangle) List for ray hits.
        /// </summary>
        private void AddRayInTris(List<Tri> triangles, RayTrans rayTrans, ref List<RayHit> rayHits)
        {
            foreach (Tri triangle in triangles)
            {
                AddRayInTri(triangle, rayTrans, ref rayHits);
            }
        }

        /// <summary>
        /// Helper to add ray hit in a Tri (triangle).
        /// </summary>
        private void AddRayInTri(Tri triProj, RayTrans rayTrans, ref List<RayHit> rayHits)
        {
            // Check for hit in triangle
            if (HitRayInTri(triProj, rayTrans.Position1RayProj, rayTrans.VectorRayProj, out Vector3 posHitProj, out Vector3 normalProj))
            {
                // Hack to circumvent ghost face bug in PrimMesher by removing hits in (ghost) face plane through shape center
                if (Math.Abs(Vector3.Dot(posHitProj, normalProj)) < m_floatToleranceInCastRay && !rayTrans.ShapeNeedsEnds)
                    return;

                // Transform hit and normal to region coordinate system
                Vector3 posHit = rayTrans.PositionPart + (posHitProj * rayTrans.ScalePart) * rayTrans.RotationPart;

                Vector3 normal = (normalProj * rayTrans.ScalePart) * rayTrans.RotationPart;
                normal.Normalize();

                // Remove duplicate hits at triangle intersections
                float distance = Vector3.Distance(rayTrans.Position1Ray, posHit);
                for (int i = rayHits.Count - 1; i >= 0; i--)
                {
                    if (rayHits[i].PartId != rayTrans.PartId)
                        break;
                    if (Math.Abs(rayHits[i].Distance - distance) < m_floatTolerance2InCastRay)
                        return;
                }

                // Build result data set
                RayHit rayHit = new()
                {
                    PartId = rayTrans.PartId,
                    GroupId = rayTrans.GroupId,
                    Link = rayTrans.Link,
                    Position = posHit,
                    Normal = normal,
                    Distance = distance
                };
                rayHits.Add(rayHit);
            }
        }

        /// <summary>
        /// Helper to find ray hit in triangle
        /// </summary>
        bool HitRayInTri(Tri triProj, Vector3 pos1RayProj, Vector3 vecRayProj, out Vector3 posHitProj, out Vector3 normalProj)
        {
            float tol = m_floatToleranceInCastRay;
            posHitProj = Vector3.Zero;

            // Calculate triangle edge vectors
            Vector3 vec1Proj = triProj.p2 - triProj.p1;
            Vector3 vec2Proj = triProj.p3 - triProj.p2;
            Vector3 vec3Proj = triProj.p1 - triProj.p3;

            // Calculate triangle normal
            normalProj = Vector3.Cross(vec1Proj, vec2Proj);

            // Skip if degenerate triangle or ray parallell with triangle plane
            float divisor = Vector3.Dot(vecRayProj, normalProj);
            if (Math.Abs(divisor) < tol)
                return false;

            // Skip if exit and not configured to detect
            if (divisor > tol && !m_detectExitsInCastRay)
                return false;

            // Skip if outside ray ends
            float distanceProj = Vector3.Dot(triProj.p1 - pos1RayProj, normalProj) / divisor;
            if (distanceProj < -tol || distanceProj > 1 + tol)
                return false;

            // Calculate hit position in triangle
            posHitProj = pos1RayProj + vecRayProj * distanceProj;

            // Skip if outside triangle bounding box
            Vector3 triProjMin = Vector3.Min(Vector3.Min(triProj.p1, triProj.p2), triProj.p3);
            Vector3 triProjMax = Vector3.Max(Vector3.Max(triProj.p1, triProj.p2), triProj.p3);
            if (
                posHitProj.X < triProjMin.X - tol || posHitProj.Y < triProjMin.Y - tol || posHitProj.Z < triProjMin.Z - tol ||
                posHitProj.X > triProjMax.X + tol || posHitProj.Y > triProjMax.Y + tol || posHitProj.Z > triProjMax.Z + tol
            )
                return false;

            // Skip if outside triangle
            if (
                Vector3.Dot(Vector3.Cross(vec1Proj, normalProj), posHitProj - triProj.p1) > tol ||
                Vector3.Dot(Vector3.Cross(vec2Proj, normalProj), posHitProj - triProj.p2) > tol ||
                Vector3.Dot(Vector3.Cross(vec3Proj, normalProj), posHitProj - triProj.p3) > tol
            )
                 return false;

            // Return hit
            return true;
        }

        /// <summary>
        /// Helper to parse selected parts of HeightMap into a Tri (triangle) List and calculate bounding box.
        /// </summary>
        private List<Tri> TrisFromHeightmapUnderRay(Vector3 posStart, Vector3 posEnd, out Vector3 lower, out Vector3 upper)
        {
            // Get bounding X-Y rectangle of terrain under ray
            lower = Vector3.Min(posStart, posEnd);
            upper = Vector3.Max(posStart, posEnd);
            lower.X = (float)Math.Floor(lower.X);
            lower.Y = (float)Math.Floor(lower.Y);
            float zLower = float.MaxValue;
            upper.X = (float)Math.Ceiling(upper.X);
            upper.Y = (float)Math.Ceiling(upper.Y);
            float zUpper = float.MinValue;

            // Initialize Tri (triangle) List
            List<Tri> triangles = new();

            // Set parsing lane direction to major ray X-Y axis
            Vector3 vec = posEnd - posStart;
            float xAbs = Math.Abs(vec.X);
            float yAbs = Math.Abs(vec.Y);
            bool bigX = true;
            if (yAbs > xAbs)
            {
                bigX = false;
                vec /= yAbs;
            }
            else if (xAbs > yAbs || xAbs > 0.0f)
                vec /= xAbs;
            else
                vec = new Vector3(1.0f, 1.0f, 0.0f);

            // Simplify by start parsing in lower end of lane
            if ((bigX && vec.X < 0.0f) || (!bigX && vec.Y < 0.0f))
            {
                Vector3 posTemp = posStart;
                posStart = posEnd;
                posEnd = posTemp;
                vec *= -1.0f;
            }

            // First 1x1 rectangle under ray
            float xFloorOld;
            float yFloorOld;
            Vector3 pos = posStart;
            float xFloor = (float)Math.Floor(pos.X);
            float yFloor = (float)Math.Floor(pos.Y);
            AddTrisFromHeightmap(xFloor, yFloor, ref triangles, ref zLower, ref zUpper);

            // Parse every remaining 1x1 rectangle under ray
            while (pos != posEnd)
            {
                // Next 1x1 rectangle under ray
                xFloorOld = xFloor;
                yFloorOld = yFloor;
                pos += vec;

                // Clip position to 1x1 rectangle border
                xFloor = (float)Math.Floor(pos.X);
                yFloor = (float)Math.Floor(pos.Y);
                if (bigX && pos.X > xFloor)
                {
                    pos.Y -= vec.Y * (pos.X - xFloor);
                    pos.X = xFloor;
                }
                else if (!bigX && pos.Y > yFloor)
                {
                    pos.X -= vec.X * (pos.Y - yFloor);
                    pos.Y = yFloor;
                }

                // Last 1x1 rectangle under ray
                if ((bigX && pos.X >= posEnd.X) || (!bigX && pos.Y >= posEnd.Y))
                {
                    pos = posEnd;
                    xFloor = (float)Math.Floor(pos.X);
                    yFloor = (float)Math.Floor(pos.Y);
                }

                // Add new 1x1 rectangle in lane
                if ((bigX && xFloor != xFloorOld) || (!bigX && yFloor != yFloorOld))
                    AddTrisFromHeightmap(xFloor, yFloor, ref triangles, ref zLower, ref zUpper);
                // Add last 1x1 rectangle in old lane at lane shift
                if (bigX && yFloor != yFloorOld)
                    AddTrisFromHeightmap(xFloor, yFloorOld, ref triangles, ref zLower, ref zUpper);
                if (!bigX && xFloor != xFloorOld)
                    AddTrisFromHeightmap(xFloorOld, yFloor, ref triangles, ref zLower, ref zUpper);
            }

            // Finalize bounding box Z
            lower.Z = zLower;
            upper.Z = zUpper;

            // Done and returning Tri (triangle)List
            return triangles;
        }

        /// <summary>
        /// Helper to add HeightMap squares into Tri (triangle) List and adjust bounding box.
        /// </summary>
        private void AddTrisFromHeightmap(float xPos, float yPos, ref List<Tri> triangles, ref float zLower, ref float zUpper)
        {
            int xInt = (int)xPos;
            int yInt = (int)yPos;

            // Corner 1 of 1x1 rectangle
            int x = Math.Clamp(xInt+1, 0, World.Heightmap.Width - 1);
            int y = Math.Clamp(yInt+1, 0, World.Heightmap.Height - 1);
            Vector3 pos1 = new(x, y, (float)World.Heightmap[x, y]);
            // Adjust bounding box
            zLower = Math.Min(zLower, pos1.Z);
            zUpper = Math.Max(zUpper, pos1.Z);

            // Corner 2 of 1x1 rectangle
            x = Math.Clamp(xInt, 0, World.Heightmap.Width - 1);
            y = Math.Clamp(yInt+1, 0, World.Heightmap.Height - 1);
            Vector3 pos2 = new(x, y, (float)World.Heightmap[x, y]);
            // Adjust bounding box
            zLower = Math.Min(zLower, pos2.Z);
            zUpper = Math.Max(zUpper, pos2.Z);

            // Corner 3 of 1x1 rectangle
            x = Math.Clamp(xInt, 0, World.Heightmap.Width - 1);
            y = Math.Clamp(yInt, 0, World.Heightmap.Height - 1);
            Vector3 pos3 = new(x, y, (float)World.Heightmap[x, y]);
            // Adjust bounding box
            zLower = Math.Min(zLower, pos3.Z);
            zUpper = Math.Max(zUpper, pos3.Z);

            // Corner 4 of 1x1 rectangle
            x = Math.Clamp(xInt+1, 0, World.Heightmap.Width - 1);
            y = Math.Clamp(yInt, 0, World.Heightmap.Height - 1);
            Vector3 pos4 = new(x, y, (float)World.Heightmap[x, y]);
            // Adjust bounding box
            zLower = Math.Min(zLower, pos4.Z);
            zUpper = Math.Max(zUpper, pos4.Z);

            // Add triangle 1
            Tri triangle1 = new()
            {
                p1 = pos1,
                p2 = pos2,
                p3 = pos3
            };
            triangles.Add(triangle1);

            // Add triangle 2
            Tri triangle2 = new()
            {
                p1 = pos3,
                p2 = pos4,
                p3 = pos1
            };
            triangles.Add(triangle2);
        }

        /// <summary>
        /// Helper to get link number for a UUID.
        /// </summary>
        private static int UUID2LinkNumber(SceneObjectPart part, UUID id)
        {
            SceneObjectGroup group = part.ParentGroup;
            if (group is not null)
            {
                SceneObjectPart sop = group.GetPart(id);
                if(sop is not null)
                    return sop.LinkNum;

                if(group.GetSittingAvatarsCount() > 0)
                {
                    List<ScenePresence> sps = group.GetSittingAvatars();
                    int ln = group.PrimCount;
                    foreach (ScenePresence sp in sps)
                    {
                        if(sp.UUID.Equals(id))
                            return ln;
                        ++ln;
                    }
                }
            }
            // Return link number 0 if no links or UUID matches
            return 0;
        }

        public LSL_Integer llManageEstateAccess(int action, string avatar)
        {
            if (!UUID.TryParse(avatar, out UUID id) || id.IsZero())
                return 0;

            EstateSettings estate = World.RegionInfo.EstateSettings;
            if (!estate.IsEstateOwner(m_host.OwnerID) || !estate.IsEstateManagerOrOwner(m_host.OwnerID))
                return 0;

            UserAccount account = m_userAccountService.GetUserAccount(RegionScopeID, id);
            bool isAccount = account is not null;
            bool isGroup = false;
            if (!isAccount)
            {
                IGroupsModule groups = World.RequestModuleInterface<IGroupsModule>();
                if (groups is not null)
                {
                    GroupRecord group = groups.GetGroupRecord(id);
                    isGroup = group is not null;
                    if (!isGroup)
                        return 0;
                }
                else
                    return 0;
            }

            switch (action)
            {
                case ScriptBaseClass.ESTATE_ACCESS_ALLOWED_AGENT_ADD:
                    if (!isAccount) return 0;
                    if (estate.HasAccess(id)) return 1;
                    if (estate.IsBanned(id, World.GetUserFlags(id)))
                        estate.RemoveBan(id);
                    estate.AddEstateUser(id);
                    break;
                case ScriptBaseClass.ESTATE_ACCESS_ALLOWED_AGENT_REMOVE:
                    if (!isAccount || !estate.HasAccess(id)) return 0;
                    estate.RemoveEstateUser(id);
                    break;
                case ScriptBaseClass.ESTATE_ACCESS_ALLOWED_GROUP_ADD:
                    if (!isGroup) return 0;
                    if (estate.GroupAccess(id)) return 1;
                    estate.AddEstateGroup(id);
                    break;
                case ScriptBaseClass.ESTATE_ACCESS_ALLOWED_GROUP_REMOVE:
                    if (!isGroup || !estate.GroupAccess(id)) return 0;
                    estate.RemoveEstateGroup(id);
                    break;
                case ScriptBaseClass.ESTATE_ACCESS_BANNED_AGENT_ADD:
                    if (!isAccount) return 0;
                    if(World.Permissions.IsAdministrator(id)) return 0;
                    if (estate.IsBanned(id, World.GetUserFlags(id))) return 1;
                    EstateBan ban = new()
                    {
                        EstateID = estate.EstateID,
                        BannedUserID = id
                    };
                    estate.AddBan(ban);
                    break;
                case ScriptBaseClass.ESTATE_ACCESS_BANNED_AGENT_REMOVE:
                    if (!isAccount || !estate.IsBanned(id, World.GetUserFlags(id))) return 0;
                    estate.RemoveBan(id);
                    break;
                default: return 0;
            }
            return 1;
        }

        public LSL_Integer llGetMemoryLimit()
        {
            // The value returned for Mono scripts in SL
            return 65536;
        }

        public LSL_Integer llSetMemoryLimit(LSL_Integer limit)
        {
            // Treat as an LSO script
            return ScriptBaseClass.FALSE;
        }

        public LSL_Integer llGetSPMaxMemory()
        {
            // The value returned for Mono scripts in SL
            return 65536;
        }

        public virtual LSL_Integer llGetUsedMemory()
        {
            // The value returned for Mono scripts in SL
            return 65536;
        }

        public void llScriptProfiler(LSL_Integer flags)
        {
            // This does nothing for LSO scripts in SL
        }

        public void llSetSoundQueueing(int queue)
        {
            m_SoundModule?.SetSoundQueueing(m_host.UUID, queue == ScriptBaseClass.TRUE.value);
        }

        public void llLinkSetSoundQueueing(int linknumber, int queue)
        {
            if (m_SoundModule is not null)
            {
                foreach (SceneObjectPart sop in GetLinkParts(linknumber))
                    m_SoundModule.SetSoundQueueing(sop.UUID, queue == ScriptBaseClass.TRUE.value);
            }
        }

        #region Not Implemented
        //
        // Listing the unimplemented lsl functions here, please move
        // them from this region as they are completed
        //
        public void llCollisionSprite(LSL_String impact_sprite)
        {
            // Viewer 2.0 broke this and it's likely LL has no intention
            // of fixing it. Therefore, letting this be a NOP seems appropriate.
        }

        public void llGodLikeRezObject(string inventory, LSL_Vector pos)
        {
            if (!World.Permissions.IsGod(m_host.OwnerID))
                NotImplemented("llGodLikeRezObject");

            AssetBase rezAsset = World.AssetService.Get(inventory);
            if (rezAsset == null)
            {
                llSay(0, "Asset not found");
                return;
            }

            SceneObjectGroup group;

            try
            {
                string xmlData = Utils.BytesToString(rezAsset.Data);
                group = SceneObjectSerializer.FromOriginalXmlFormat(xmlData);
            }
            catch
            {
                llSay(0, "Asset not found");
                return;
            }

            if (group == null)
            {
                llSay(0, "Asset not found");
                return;
            }

            group.RootPart.AttachedPos = group.AbsolutePosition;

            group.ResetIDs();

            Vector3 llpos = new((float)pos.x, (float)pos.y, (float)pos.z);
            World.AddNewSceneObject(group, true, llpos, Quaternion.Identity, Vector3.Zero);
            group.CreateScriptInstances(0, true, World.DefaultScriptEngine, 3);
            group.ScheduleGroupForFullUpdate();

            // objects rezzed with this method are die_at_edge by default.
            group.RootPart.SetDieAtEdge(true);

            group.ResumeScripts();

            m_ScriptEngine.PostObjectEvent(m_host.LocalId, new EventParams(
                    "object_rez", new Object[] {
                    new LSL_String(
                    group.RootPart.UUID.ToString()) },
                    Array.Empty<DetectParams>()));
        }

        public LSL_Key llTransferLindenDollars(LSL_Key destination, LSL_Integer amount)
        {

            IMoneyModule money = World.RequestModuleInterface<IMoneyModule>();
            UUID txn = UUID.Random();
            UUID toID = UUID.Zero;

            string replydata = "UnKnownError";
            bool bad = true;
            while(true)
            {
                if (amount <= 0)
                {
                    replydata = "INVALID_AMOUNT";
                    break;
                }

                if (money == null)
                {
                    replydata = "TRANSFERS_DISABLED";
                    break;
                }

                if (m_host.OwnerID.Equals(m_host.GroupID))
                {
                    replydata = "GROUP_OWNED";
                    break;
                }

                if (m_item == null)
                {
                    replydata = "SERVICE_ERROR";
                    break;
                }

                if (m_item.PermsGranter.IsZero())
                {
                    replydata = "MISSING_PERMISSION_DEBIT";
                    break;
                }

                if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_DEBIT) == 0)
                {
                    replydata = "MISSING_PERMISSION_DEBIT";
                    break;
                }

                if (!UUID.TryParse(destination, out toID))
                {
                    replydata = "INVALID_AGENT";
                    break;
                }
                bad = false;
                break;
            }
            if(bad)
            {
                m_ScriptEngine.PostScriptEvent(m_item.ItemID, new EventParams(
                        "transaction_result", new Object[] {
                            new LSL_String(txn.ToString()),
                            new LSL_Integer(0),
                            new LSL_String(replydata) },
                        Array.Empty<DetectParams>()));
                return txn.ToString();
            }

            //fire and forget...
            void act(string eventID)
            {
                int replycode = 0;
                try
                {
                    UserAccount account = m_userAccountService.GetUserAccount(RegionScopeID, toID);
                    if (account is null)
                    {
                        replydata = "LINDENDOLLAR_ENTITYDOESNOTEXIST";
                        return;
                    }

                    bool result = money.ObjectGiveMoney(m_host.ParentGroup.RootPart.UUID, m_host.ParentGroup.RootPart.OwnerID,
                                toID, amount, txn, out string reason);
                    if (result)
                    {
                        replycode = 1;
                        replydata = destination + "," + amount.ToString();
                        return;
                    }
                    replydata = reason;
                }
                finally
                {
                    m_ScriptEngine.PostScriptEvent(m_item.ItemID, new EventParams(
                            "transaction_result", new Object[] {
                            new LSL_String(txn.ToString()),
                            new LSL_Integer(replycode),
                            new LSL_String(replydata) },
                            Array.Empty<DetectParams>()));
                }
            }

            m_AsyncCommands.DataserverPlugin.RegisterRequest(m_host.LocalId, m_item.ItemID, act);
            return txn.ToString();
        }

        #endregion


        protected LSL_List SetPrimParams(ScenePresence av, LSL_List rules, string originFunc, ref uint rulesParsed)
        {
            //This is a special version of SetPrimParams to deal with avatars which are sitting on the linkset.

            int idx = 0;
            int idxStart = 0;

            bool positionChanged = false;
            try
            {
                while (idx < rules.Length)
                {
                    ++rulesParsed;
                    int code = rules.GetIntegerItem(idx++);

                    int remain = rules.Length - idx;
                    idxStart = idx;

                    switch (code)
                    {
                        case ScriptBaseClass.PRIM_POSITION:
                        case ScriptBaseClass.PRIM_POS_LOCAL:
                            {
                                if (remain < 1)
                                    return new LSL_List();

                                LSL_Vector v;
                                v = rules.GetVector3Item(idx++);

                                if(!av.LegacySitOffsets)
                                {
                                    LSL_Vector sitOffset = (llRot2Up(new LSL_Rotation(av.Rotation.X, av.Rotation.Y, av.Rotation.Z, av.Rotation.W)) * av.Appearance.AvatarHeight * 0.02638f);

                                    v += 2.0 * sitOffset;
                                }

                                av.OffsetPosition = new Vector3((float)v.x, (float)v.y, (float)v.z);
                                positionChanged = true;
                            }
                            break;

                        case ScriptBaseClass.PRIM_ROTATION:
                            {
                                if (remain < 1)
                                    return new LSL_List();

                                Quaternion r;
                                r = rules.GetQuaternionItem(idx++);

                                av.Rotation = m_host.GetWorldRotation() * r;
                                positionChanged = true;
                            }
                            break;

                        case ScriptBaseClass.PRIM_ROT_LOCAL:
                            {
                                if (remain < 1)
                                    return new LSL_List();

                                LSL_Rotation r;
                                r = rules.GetQuaternionItem(idx++);

                                av.Rotation = r;
                                positionChanged = true;
                            }
                            break;

                        // parse rest doing nothing but number of parameters error check
                        case ScriptBaseClass.PRIM_SIZE:
                        case ScriptBaseClass.PRIM_MATERIAL:
                        case ScriptBaseClass.PRIM_PHANTOM:
                        case ScriptBaseClass.PRIM_PHYSICS:
                        case ScriptBaseClass.PRIM_PHYSICS_SHAPE_TYPE:
                        case ScriptBaseClass.PRIM_TEMP_ON_REZ:
                        case ScriptBaseClass.PRIM_NAME:
                        case ScriptBaseClass.PRIM_DESC:
                            if (remain < 1)
                                return new LSL_List();
                            idx++;
                            break;

                        case ScriptBaseClass.PRIM_GLOW:
                        case ScriptBaseClass.PRIM_FULLBRIGHT:
                        case ScriptBaseClass.PRIM_TEXGEN:
                            if (remain < 2)
                                return new LSL_List();
                            idx += 2;
                            break;

                        case ScriptBaseClass.PRIM_TYPE:
                            if (remain < 3)
                                return new LSL_List();
                            code = (int)rules.GetIntegerItem(idx++);
                            remain = rules.Length - idx;
                            switch (code)
                            {
                                case ScriptBaseClass.PRIM_TYPE_BOX:
                                case ScriptBaseClass.PRIM_TYPE_CYLINDER:
                                case ScriptBaseClass.PRIM_TYPE_PRISM:
                                    if (remain < 6)
                                        return new LSL_List();
                                    idx += 6;
                                    break;

                                case ScriptBaseClass.PRIM_TYPE_SPHERE:
                                    if (remain < 5)
                                        return new LSL_List();
                                    idx += 5;
                                    break;

                                case ScriptBaseClass.PRIM_TYPE_TORUS:
                                case ScriptBaseClass.PRIM_TYPE_TUBE:
                                case ScriptBaseClass.PRIM_TYPE_RING:
                                    if (remain < 11)
                                        return new LSL_List();
                                    idx += 11;
                                    break;

                                case ScriptBaseClass.PRIM_TYPE_SCULPT:
                                    if (remain < 2)
                                        return new LSL_List();
                                    idx += 2;
                                    break;
                            }
                            break;

                        case ScriptBaseClass.PRIM_COLOR:
                        case ScriptBaseClass.PRIM_TEXT:
                        case ScriptBaseClass.PRIM_BUMP_SHINY:
                        case ScriptBaseClass.PRIM_OMEGA:
                        case ScriptBaseClass.PRIM_SIT_TARGET:
                            if (remain < 3)
                                return new LSL_List();
                            idx += 3;
                            break;

                        case ScriptBaseClass.PRIM_TEXTURE:
                        case ScriptBaseClass.PRIM_POINT_LIGHT:
                        case ScriptBaseClass.PRIM_PHYSICS_MATERIAL:
                            if (remain < 5)
                                return new LSL_List();
                            idx += 5;
                            break;

                        case ScriptBaseClass.PRIM_FLEXIBLE:
                            if (remain < 7)
                                return new LSL_List();

                            idx += 7;
                            break;

                        case ScriptBaseClass.PRIM_LINK_TARGET:
                            if (remain < 3) // setting to 3 on the basis that parsing any usage of PRIM_LINK_TARGET that has nothing following it is pointless.
                                return new LSL_List();

                            return rules.GetSublist(idx, -1);
                    }
                }
            }
            catch (InvalidCastException e)
            {
                Error(originFunc,string.Format(
                        " error running rule #{0}: arg #{1} {2}",
                         rulesParsed, idx - idxStart, e.Message));
            }
            finally
            {
                if (positionChanged)
                    av.SendTerseUpdateToAllClients();
            }
            return new LSL_List();
        }

        public LSL_List GetPrimParams(ScenePresence avatar, LSL_List rules, ref LSL_List res)
        {
            // avatars case
            // replies as SL wiki

//            SceneObjectPart sitPart = avatar.ParentPart; // most likelly it will be needed
            SceneObjectPart sitPart = World.GetSceneObjectPart(avatar.ParentID); // maybe better do this expensive search for it in case it's gone??

            int idx = 0;
            while (idx < rules.Length)
            {
                int code = rules.GetIntegerItem(idx++);
                int remain = rules.Length - idx;

                switch (code)
                {
                    case ScriptBaseClass.PRIM_MATERIAL:
                        res.Add(new LSL_Integer((int)SOPMaterialData.SopMaterial.Flesh));
                        break;

                    case ScriptBaseClass.PRIM_PHYSICS:
                            res.Add(new LSL_Integer(0));
                        break;

                    case ScriptBaseClass.PRIM_TEMP_ON_REZ:
                            res.Add(new LSL_Integer(0));
                        break;

                    case ScriptBaseClass.PRIM_PHANTOM:
                            res.Add(new LSL_Integer(0));
                        break;

                    case ScriptBaseClass.PRIM_POSITION:
                        Vector3 pos;

                        if (sitPart.ParentGroup.RootPart != null)
                        {
                            pos = avatar.OffsetPosition;

                            if(!avatar.LegacySitOffsets)
                            {
                                Vector3 sitOffset = (Zrot(avatar.Rotation)) * (avatar.Appearance.AvatarHeight * 0.02638f *2.0f);
                                pos -= sitOffset;
                            }

                            SceneObjectPart sitroot = sitPart.ParentGroup.RootPart;
                            pos = sitroot.AbsolutePosition + pos * sitroot.GetWorldRotation();
                        }
                        else
                            pos = avatar.AbsolutePosition;

                        res.Add(new LSL_Vector(pos.X,pos.Y,pos.Z));
                        break;

                    case ScriptBaseClass.PRIM_SIZE:
                        Vector3 s = avatar.Appearance.AvatarSize;
                        res.Add(new LSL_Vector(s.X, s.Y, s.Z));

                        break;

                    case ScriptBaseClass.PRIM_ROTATION:
                        res.Add(new LSL_Rotation(avatar.GetWorldRotation()));
                        break;

                    case ScriptBaseClass.PRIM_TYPE:
                        res.Add(new LSL_Integer(ScriptBaseClass.PRIM_TYPE_BOX));
                        res.Add(new LSL_Integer(ScriptBaseClass.PRIM_HOLE_DEFAULT));
                        res.Add(new LSL_Vector(0f,1.0f,0f));
                        res.Add(new LSL_Float(0.0f));
                        res.Add(new LSL_Vector(0, 0, 0));
                        res.Add(new LSL_Vector(1.0f,1.0f,0f));
                        res.Add(new LSL_Vector(0, 0, 0));
                        break;

                    case ScriptBaseClass.PRIM_TEXTURE:
                        if (remain < 1)
                            return new LSL_List();

                        int face = rules.GetIntegerItem(idx++);
                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < 21; face++)
                            {
                                res.Add(new LSL_String(""));
                                res.Add(new LSL_Vector(0,0,0));
                                res.Add(new LSL_Vector(0,0,0));
                                res.Add(new LSL_Float(0.0));
                            }
                        }
                        else
                        {
                            if (face >= 0 && face < 21)
                            {
                                res.Add(new LSL_String(""));
                                res.Add(new LSL_Vector(0,0,0));
                                res.Add(new LSL_Vector(0,0,0));
                                res.Add(new LSL_Float(0.0));
                            }
                        }
                        break;

                    case ScriptBaseClass.PRIM_COLOR:
                        if (remain < 1)
                            return new LSL_List();

                        face = rules.GetIntegerItem(idx++);

                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < 21; face++)
                            {
                                res.Add(new LSL_Vector(0,0,0));
                                res.Add(new LSL_Float(0));
                            }
                        }
                        else
                        {
                                res.Add(new LSL_Vector(0,0,0));
                                res.Add(new LSL_Float(0));
                        }
                        break;

                    case ScriptBaseClass.PRIM_BUMP_SHINY:
                        if (remain < 1)
                            return new LSL_List();
                        face = rules.GetIntegerItem(idx++);

                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < 21; face++)
                            {
                                res.Add(new LSL_Integer(ScriptBaseClass.PRIM_SHINY_NONE));
                                res.Add(new LSL_Integer(ScriptBaseClass.PRIM_BUMP_NONE));
                            }
                        }
                        else
                        {
                                res.Add(new LSL_Integer(ScriptBaseClass.PRIM_SHINY_NONE));
                                res.Add(new LSL_Integer(ScriptBaseClass.PRIM_BUMP_NONE));
                        }
                        break;

                    case ScriptBaseClass.PRIM_FULLBRIGHT:
                        if (remain < 1)
                            return new LSL_List();
                        face = rules.GetIntegerItem(idx++);

                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < 21; face++)
                            {
                                res.Add(new LSL_Integer(ScriptBaseClass.FALSE));
                            }
                        }
                        else
                        {
                                res.Add(new LSL_Integer(ScriptBaseClass.FALSE));
                        }
                        break;

                    case ScriptBaseClass.PRIM_FLEXIBLE:
                        res.Add(new LSL_Integer(0));
                        res.Add(new LSL_Integer(0));// softness
                        res.Add(new LSL_Float(0.0f));   // gravity
                        res.Add(new LSL_Float(0.0f));      // friction
                        res.Add(new LSL_Float(0.0f));      // wind
                        res.Add(new LSL_Float(0.0f));   // tension
                        res.Add(new LSL_Vector(0f,0f,0f));
                        break;

                    case ScriptBaseClass.PRIM_TEXGEN:
                        // (PRIM_TEXGEN_DEFAULT, PRIM_TEXGEN_PLANAR)
                        if (remain < 1)
                            return new LSL_List();
                        face = rules.GetIntegerItem(idx++);

                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < 21; face++)
                            {
                                    res.Add(new LSL_Integer(ScriptBaseClass.PRIM_TEXGEN_DEFAULT));
                            }
                        }
                        else
                        {
                                res.Add(new LSL_Integer(ScriptBaseClass.PRIM_TEXGEN_DEFAULT));
                        }
                        break;

                    case ScriptBaseClass.PRIM_POINT_LIGHT:
                        res.Add(new LSL_Integer(0));
                        res.Add(new LSL_Vector(0f, 0f, 0f));
                        res.Add(new LSL_Float(0f)); // intensity
                        res.Add(new LSL_Float(0f));    // radius
                        res.Add(new LSL_Float(0f));   // falloff
                        break;

                    case ScriptBaseClass.PRIM_REFLECTION_PROBE:
                        res.Add(new LSL_Integer(0));
                        res.Add(new LSL_Float(0f)); // ambiance
                        res.Add(new LSL_Float(0f)); // clip
                        res.Add(new LSL_Float(0f)); // flags
                        break;

                    case ScriptBaseClass.PRIM_GLOW:
                        if (remain < 1)
                            return new LSL_List();
                        face = rules.GetIntegerItem(idx++);

                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < 21; face++)
                            {
                                res.Add(new LSL_Float(0f));
                            }
                        }
                        else
                        {
                            res.Add(new LSL_Float(0f));
                        }
                        break;

                    case ScriptBaseClass.PRIM_TEXT:
                        res.Add(new LSL_String(""));
                        res.Add(new LSL_Vector(0f,0f,0f));
                        res.Add(new LSL_Float(1.0f));
                        break;

                    case ScriptBaseClass.PRIM_NAME:
                        res.Add(new LSL_String(avatar.Name));
                        break;

                    case ScriptBaseClass.PRIM_DESC:
                        res.Add(new LSL_String(""));
                        break;

                    case ScriptBaseClass.PRIM_ROT_LOCAL:
                        Quaternion lrot = avatar.Rotation;
                        res.Add(new LSL_Rotation(lrot.X, lrot.Y, lrot.Z, lrot.W));
                        break;

                    case ScriptBaseClass.PRIM_POS_LOCAL:
                        Vector3 lpos = avatar.OffsetPosition;

                        if(!avatar.LegacySitOffsets)
                        {
                            Vector3 lsitOffset = (Zrot(avatar.Rotation)) * (avatar.Appearance.AvatarHeight * 0.02638f * 2.0f);
                            lpos -= lsitOffset;
                        }

                        res.Add(new LSL_Vector(lpos.X,lpos.Y,lpos.Z));
                        break;

                    case ScriptBaseClass.PRIM_LINK_TARGET:
                        if (remain < 3) // setting to 3 on the basis that parsing any usage of PRIM_LINK_TARGET that has nothing following it is pointless.
                            return new LSL_List();

                        return rules.GetSublist(idx, -1);
                }
            }

            return new LSL_List();
        }

        public void llSetAnimationOverride(LSL_String animState, LSL_String anim)
        {
            if (m_item.PermsGranter.IsZero())
            {
                llShout(ScriptBaseClass.DEBUG_CHANNEL, "No permission to override animations");
                return;
            }

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_OVERRIDE_ANIMATIONS) == 0)
            {
                llShout(ScriptBaseClass.DEBUG_CHANNEL, "No permission to override animations");
                return;
            }

            ScenePresence presence = World.GetScenePresence(m_item.PermsGranter);
            if (presence == null)
                return;

            string state = String.Empty;

            foreach (KeyValuePair<string, string> kvp in MovementAnimationsForLSL)
            {
                if (kvp.Value.ToLower() == ((string)animState).ToLower())
                {
                    state = kvp.Key;
                    break;
                }
            }

            if (state.Length == 0)
            {
                llShout(ScriptBaseClass.DEBUG_CHANNEL, "Invalid animation state " + animState);
                return;
            }


            UUID animID;

            animID = ScriptUtils.GetAssetIdFromItemName(m_host, anim, (int)AssetType.Animation);

            if (animID.IsZero())
            {
                String animupper = ((string)anim).ToUpperInvariant();
                DefaultAvatarAnimations.AnimsUUIDbyName.TryGetValue(animupper, out animID);
            }

            if (animID.IsZero())
            {
                llShout(ScriptBaseClass.DEBUG_CHANNEL, "Animation not found");
                return;
            }

            presence.SetAnimationOverride(state, animID);
        }

        public void llResetAnimationOverride(LSL_String animState)
        {
            ScenePresence presence = World.GetScenePresence(m_item.PermsGranter);
            if (presence == null)
                return;

            if (m_item.PermsGranter.IsZero())
            {
                llShout(ScriptBaseClass.DEBUG_CHANNEL, "No permission to override animations");
                return;
            }

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_OVERRIDE_ANIMATIONS) == 0)
            {
                llShout(ScriptBaseClass.DEBUG_CHANNEL, "No permission to override animations");
                return;
            }

            if (animState == "ALL")
            {
                presence.SetAnimationOverride("ALL", UUID.Zero);
                return;
            }

            string state = String.Empty;

            foreach (KeyValuePair<string, string> kvp in MovementAnimationsForLSL)
            {
                if (kvp.Value.ToLower() == ((string)animState).ToLower())
                {
                    state = kvp.Key;
                    break;
                }
            }

            if (state.Length == 0)
            {
                return;
            }

            presence.SetAnimationOverride(state, UUID.Zero);
        }

        public LSL_String llGetAnimationOverride(LSL_String animState)
        {

            ScenePresence presence = World.GetScenePresence(m_item.PermsGranter);
            if (presence is null)
                return LSL_String.Empty;

            if (m_item.PermsGranter.IsZero())
            {
                llShout(ScriptBaseClass.DEBUG_CHANNEL, "No permission to override animations");
                return LSL_String.Empty;
            }

            if ((m_item.PermsMask & (ScriptBaseClass.PERMISSION_OVERRIDE_ANIMATIONS | ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION)) == 0)
            {
                llShout(ScriptBaseClass.DEBUG_CHANNEL, "No permission to override animations");
                return LSL_String.Empty;
            }

            string state = string.Empty;

            foreach (KeyValuePair<string, string> kvp in MovementAnimationsForLSL)
            {
                if (kvp.Value.ToLower() == ((string)animState).ToLower())
                {
                    state = kvp.Key;
                    break;
                }
            }

            if (state.Length == 0)
            {
                return LSL_String.Empty;
            }

            if (!presence.TryGetAnimationOverride(state, out UUID animID) || animID.IsZero())
                return animState;

            foreach (KeyValuePair<string, UUID> kvp in DefaultAvatarAnimations.AnimsUUIDbyName)
            {
                if (kvp.Value.Equals(animID))
                    return kvp.Key.ToLower();
            }

            foreach (TaskInventoryItem item in m_host.Inventory.GetInventoryItems())
            {
                if (item.AssetID.Equals(animID))
                    return item.Name;
            }

            return LSL_String.Empty;
        }

        public LSL_Integer llGetDayLength()
        {

            if (m_envModule == null)
                return 14400;

            return m_envModule.GetDayLength(m_host.GetWorldPosition());
        }

        public LSL_Integer llGetRegionDayLength()
        {

            if (m_envModule == null)
                return 14400;

            return m_envModule.GetRegionDayLength();
        }

        public LSL_Integer llGetDayOffset()
        {

            if (m_envModule == null)
                return 57600;

            return m_envModule.GetDayOffset(m_host.GetWorldPosition());
        }

        public LSL_Integer llGetRegionDayOffset()
        {

            if (m_envModule == null)
                return 57600;

            return m_envModule.GetRegionDayOffset();
        }

        public LSL_Vector llGetSunDirection()
        {

            if (m_envModule == null)
                return Vector3.Zero;

            return m_envModule.GetSunDir(m_host.GetWorldPosition());
        }

        public LSL_Vector llGetRegionSunDirection()
        {

            if (m_envModule == null)
                return Vector3.Zero;

            float z = m_host.GetWorldPosition().Z;
            return m_envModule.GetRegionSunDir(z);
        }

        public LSL_Vector llGetMoonDirection()
        {

            if (m_envModule == null)
                return Vector3.Zero;

            return m_envModule.GetMoonDir(m_host.GetWorldPosition());
        }

        public LSL_Vector llGetRegionMoonDirection()
        {

            if (m_envModule == null)
                return Vector3.Zero;

            float z = m_host.GetWorldPosition().Z;
            return m_envModule.GetRegionMoonDir(z);
        }

        public LSL_Rotation llGetSunRotation()
        {
            if (m_envModule is null)
                return LSL_Rotation.Identity;

            return m_envModule.GetSunRot(m_host.GetWorldPosition());
        }

        public LSL_Rotation llGetRegionSunRotation()
        {
            if (m_envModule is null)
                return LSL_Rotation.Identity;

            float z = m_host.GetWorldPosition().Z;
            return m_envModule.GetRegionSunRot(z);
        }

        public LSL_Rotation llGetMoonRotation()
        {
            if (m_envModule is null)
                return LSL_Rotation.Identity;

            return m_envModule.GetMoonRot(m_host.GetWorldPosition());
        }

        public LSL_Rotation llGetRegionMoonRotation()
        {
            if (m_envModule is null)
                return LSL_Rotation.Identity;

            float z = m_host.GetWorldPosition().Z;
            return m_envModule.GetRegionMoonRot(z);
        }

        public LSL_List llJson2List(LSL_String json)
        {

            if (String.IsNullOrEmpty(json))
                return new LSL_List();
            if(json == "[]")
                return new LSL_List();
            if(json == "{}")
                return new LSL_List();
            char first = ((string)json)[0];

            if(first != '[' && first !='{')
            {
                // we already have a single element
                LSL_List l = new();
                l.Add(json);
                return l;
            }

            LitJson.JsonData jsdata;
            try
            {
                jsdata = LitJson.JsonMapper.ToObject(json);
            }
            catch //(Exception e)
            {
                //string m = e.Message; // debug point
                return json;
            }
            try
            {
                return JsonParseTop(jsdata);
            }
            catch //(Exception e)
            {
                //string m = e.Message; // debug point
                return (LSL_String)ScriptBaseClass.JSON_INVALID;
            }
        }

        private static LSL_List JsonParseTop(LitJson.JsonData  elem)
        {
            LSL_List retl = new();
            if(elem is null)
                retl.Add((LSL_String)ScriptBaseClass.JSON_NULL);

            LitJson.JsonType elemType = elem.GetJsonType();
            switch (elemType)
            {
                case  LitJson.JsonType.Int:
                    retl.Add(new LSL_Integer((int)elem));
                    return retl;
                case  LitJson.JsonType.Boolean:
                    retl.Add((LSL_String)((bool)elem ? ScriptBaseClass.JSON_TRUE : ScriptBaseClass.JSON_FALSE));
                    return retl;
                case LitJson.JsonType.Double:
                    retl.Add(new LSL_Float((double)elem));
                    return retl;
                case LitJson.JsonType.None:
                    retl.Add((LSL_String)ScriptBaseClass.JSON_NULL);
                    return retl;
                case LitJson.JsonType.String:
                    retl.Add(new LSL_String((string)elem));
                    return retl;
                case LitJson.JsonType.Array:
                    foreach (LitJson.JsonData subelem in elem)
                        retl.Add(JsonParseTopNodes(subelem));
                    return retl;
                case LitJson.JsonType.Object:
                    IDictionaryEnumerator e = ((IOrderedDictionary)elem).GetEnumerator();
                    while (e.MoveNext())
                    {
                        retl.Add(new LSL_String((string)e.Key));
                        retl.Add(JsonParseTopNodes((LitJson.JsonData)e.Value));
                    }
                    return retl;
                default:
                    throw new Exception(ScriptBaseClass.JSON_INVALID);
            }
        }

        private static object JsonParseTopNodes(LitJson.JsonData  elem)
        {
            if(elem is null)
                return ((LSL_String)ScriptBaseClass.JSON_NULL);

            LitJson.JsonType elemType = elem.GetJsonType();
            switch (elemType)
            {
                case LitJson.JsonType.Int:
                    return (new LSL_Integer((int)elem));
                case LitJson.JsonType.Boolean:
                    return ((bool)elem ? (LSL_String)ScriptBaseClass.JSON_TRUE : (LSL_String)ScriptBaseClass.JSON_FALSE);
                case LitJson.JsonType.Double:
                    return (new LSL_Float((double)elem));
                case LitJson.JsonType.None:
                    return ((LSL_String)ScriptBaseClass.JSON_NULL);
                case LitJson.JsonType.String:
                    return (new LSL_String((string)elem));
                case LitJson.JsonType.Array:
                case LitJson.JsonType.Object:
                    string s = LitJson.JsonMapper.ToJson(elem);
                    return (LSL_String)s;
                default:
                    throw new Exception(ScriptBaseClass.JSON_INVALID);
            }
        }

        public LSL_String llList2Json(LSL_String type, LSL_List values)
        {
            try
            {
                StringBuilder sb = new();
                if (type == ScriptBaseClass.JSON_ARRAY)
                {
                    sb.Append('[');
                    int i= 0;
                    foreach (object o in values.Data)
                    {
                        sb.Append(ListToJson(o));
                        if((i++) < values.Data.Length - 1)
                            sb.Append(',');
                    }
                    sb.Append(']');
                    return (LSL_String)sb.ToString();
                }
                else if (type == ScriptBaseClass.JSON_OBJECT)
                {
                    sb.Append('{');
                    for (int i = 0; i < values.Data.Length; i += 2)
                    {
                        if (values.Data[i] is not LSL_String LSL_StringVal)
                            return ScriptBaseClass.JSON_INVALID;
                        string key = LSL_StringVal.m_string;
                        key = EscapeForJSON(key, true);
                        sb.Append(key);
                        sb.Append(':');
                        sb.Append(ListToJson(values.Data[i+1]));
                        if(i < values.Data.Length - 2)
                            sb.Append(',');
                    }
                    sb.Append('}');
                    return (LSL_String)sb.ToString();
                }
                return ScriptBaseClass.JSON_INVALID;
            }
            catch
            {
                return ScriptBaseClass.JSON_INVALID;
            }
        }

        private static string ListToJson(object o)
        {
            if (o is double od)
            {
                 if(double.IsInfinity(od))
                    return  "\"Inf\"";
                if(double.IsNaN(od))
                    return  "\"NaN\"";

                return od.ToString();
            }
            if (o is LSL_Float olf)
            {
                 if (double.IsInfinity(olf.value))
                    return "\"Inf\"";
                if (double.IsNaN(olf.value))
                    return "\"NaN\"";

                return olf.value.ToString();
            }
            if (o is LSL_Integer LSL_Integero)
            {
                return LSL_Integero.value.ToString();
            }
            if(o is int into)
            {
                return into.ToString();
            }
            if (o is LSL_Rotation LSL_Rotationo)
            {
                return $"\"{LSL_Rotationo}\"";
            }
            if (o is LSL_Vector LSL_Vectoro)
            {
                return $"\"{LSL_Vectoro}\"";
            }
            if (o is string str)
            {
                str = str.Trim();
                if (str.Length == 0)
                    return "\"\"";
                if (str[0] == '{')
                    return str;
                if (str[0] == '[')
                    return str;
                if (str.Equals(ScriptBaseClass.JSON_TRUE) || str.Equals("true"))
                    return "true";
                if(str.Equals(ScriptBaseClass.JSON_FALSE) || str.Equals("false"))
                    return "false";
                if(str.Equals(ScriptBaseClass.JSON_NULL) || str.Equals("null"))
                    return "null";
                return EscapeForJSON(str, true);
            }
            if (o is LSL_String olstr)
            {
                string lstr = olstr.m_string.Trim();
                if (lstr.Length == 0)
                    return "\"\"";
                if (lstr[0] == '{')
                    return lstr;
                if (lstr[0] == '[')
                    return lstr;
                if (lstr.Equals(ScriptBaseClass.JSON_TRUE) || lstr.Equals( "true"))
                    return "true";
                if (lstr.Equals(ScriptBaseClass.JSON_FALSE) || lstr.Equals("false"))
                    return "false";
                if (lstr.Equals(ScriptBaseClass.JSON_NULL) || lstr.Equals( "null"))
                    return "null";
                return EscapeForJSON(lstr, true);
            }
            throw new IndexOutOfRangeException();
        }

        private static string EscapeForJSON(string s, bool AddOuter)
        {
            int i;
            char c;
            String t;
            int len = s.Length;

            StringBuilder sb = new(len + 64);
            if(AddOuter)
                sb.Append('\"');

            for (i = 0; i < len; i++)
            {
                c = s[i];
                switch (c)
                {
                    case '\\':
                    case '"':
                    case '/':
                        sb.Append('\\');
                        sb.Append(c);
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    default:
                        if (c < ' ')
                        {
                            t = "000" + String.Format("{0:X}", c);
                            sb.Append("\\u");
                            sb.Append(t.AsSpan(t.Length - 4));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            if(AddOuter)
                sb.Append('\"');
            return sb.ToString();
        }

        public LSL_String llJsonSetValue(LSL_String json, LSL_List specifiers, LSL_String value)
        {
            bool noSpecifiers = specifiers.Length == 0;
            LitJson.JsonData workData;
            try
            {
                if(noSpecifiers)
                    specifiers.Add(new LSL_Integer(0));

                if(!String.IsNullOrEmpty(json))
                    workData = LitJson.JsonMapper.ToObject(json);
                else
                {
                    workData = new  LitJson.JsonData();
                    workData.SetJsonType(LitJson.JsonType.Array);
                }
            }
            catch //(Exception e)
            {
                //string m = e.Message; // debug point
                return ScriptBaseClass.JSON_INVALID;
            }
            try
            {
                LitJson.JsonData replace = JsonSetSpecific(workData, specifiers, 0, value);
                if(replace != null)
                    workData = replace;
            }
            catch //(Exception e)
            {
                //string m = e.Message; // debug point
                return ScriptBaseClass.JSON_INVALID;
            }

            try
            {
                string r = LitJson.JsonMapper.ToJson(workData);
                if(noSpecifiers)
                    r = r[1..^1]; // strip leading and trailing brakets
                return r;
            }
            catch //(Exception e)
            {
                //string m = e.Message; // debug point
            }
            return ScriptBaseClass.JSON_INVALID;
        }

        private LitJson.JsonData JsonSetSpecific(LitJson.JsonData elem, LSL_List specifiers, int level, LSL_String val)
        {
            object spec = specifiers.Data[level];
            if(spec is LSL_String LSL_Stringspec)
                spec = LSL_Stringspec.m_string;
            else if (spec is LSL_Integer LSL_Integerspec)
                spec = LSL_Integerspec.value;

            if(!(spec is string || spec is int))
                throw new IndexOutOfRangeException();

            int speclen = specifiers.Data.Length - 1;

            bool hasvalue = false;
            LitJson.JsonData value = null;

            LitJson.JsonType elemType = elem.GetJsonType();
            if (elemType == LitJson.JsonType.Array)
            {
                if (spec is int v)
                {
                    int c = elem.Count;
                    if(v < 0 || (v != 0 && v > c))
                        throw new IndexOutOfRangeException();
                    if(v == c)
                        elem.Add(JsonBuildRestOfSpec(specifiers, level + 1, val));
                    else
                    {
                        hasvalue = true;
                        value = elem[v];
                    }
                }
                else if (spec is string stringspec)
                {
                    if(stringspec == ScriptBaseClass.JSON_APPEND)
                        elem.Add(JsonBuildRestOfSpec(specifiers, level + 1, val));
                    else if(elem.Count < 2)
                    {
                        // our initial guess of array was wrong
                        LitJson.JsonData newdata = new();
                        newdata.SetJsonType(LitJson.JsonType.Object);
                        IOrderedDictionary no = newdata as IOrderedDictionary;
                        no.Add((string)spec,JsonBuildRestOfSpec(specifiers, level + 1, val));
                        return newdata;
                    }
                }
            }
            else if (elemType == LitJson.JsonType.Object)
            {
                if (spec is string key)
                {
                    IOrderedDictionary e = elem as IOrderedDictionary;
                    if(e.Contains(key))
                    {
                        hasvalue = true;
                        value = (LitJson.JsonData)e[key];
                    }
                    else
                        e.Add(key, JsonBuildRestOfSpec(specifiers, level + 1, val));
                }
                else if(spec is int intspec && intspec == 0)
                {
                    //we are replacing a object by a array
                    LitJson.JsonData newData = new();
                    newData.SetJsonType(LitJson.JsonType.Array);
                    newData.Add(JsonBuildRestOfSpec(specifiers, level + 1, val));
                    return newData;
                }
            }
            else
            {
                LitJson.JsonData newData = JsonBuildRestOfSpec(specifiers, level, val);
                return newData;
            }

            if (hasvalue)
            {
                if (level < speclen)
                {
                    LitJson.JsonData replace = JsonSetSpecific(value, specifiers, level + 1, val);
                    if(replace is not null)
                    {
                        if(elemType == LitJson.JsonType.Array)
                        {
                            if(spec is int intspec)
                                elem[intspec] = replace;
                            else if( spec is string stringspec)
                            {
                                LitJson.JsonData newdata = new();
                                newdata.SetJsonType(LitJson.JsonType.Object);
                                IOrderedDictionary no = newdata as IOrderedDictionary;
                                no.Add(stringspec, replace);
                                return newdata;
                            }
                        }
                        else if(elemType == LitJson.JsonType.Object)
                        {
                            if(spec is string stringspec)
                                elem[stringspec] = replace;
                            else if(spec is int intspec && intspec == 0)
                            {
                                LitJson.JsonData newdata = new();
                                newdata.SetJsonType(LitJson.JsonType.Array);
                                newdata.Add(replace);
                                return newdata;
                            }
                        }
                    }
                    return null;
                }
                else if(speclen == level)
                {
                    if(val == ScriptBaseClass.JSON_DELETE)
                    {
                        if(elemType == LitJson.JsonType.Array)
                        {
                            if(spec is int intspec)
                            {
                                IList el = elem as IList;
                                el.RemoveAt(intspec);
                            }
                        }
                        else if(elemType == LitJson.JsonType.Object)
                        {
                            if(spec is string stringspec)
                            {
                                IOrderedDictionary eo = elem as IOrderedDictionary;
                                eo.Remove(stringspec);
                            }
                        }
                        return null;
                    }

                    LitJson.JsonData newval;
                    if(val == null || val == ScriptBaseClass.JSON_NULL || val == "null")
                        newval = null;
                    else if(val == ScriptBaseClass.JSON_TRUE || val == "true")
                        newval = new LitJson.JsonData(true);
                    else if(val == ScriptBaseClass.JSON_FALSE || val == "false")
                        newval = new LitJson.JsonData(false);
                    else if(float.TryParse(val, out float num))
                    {
                        // assuming we are at en.us already
                        if(num - (int)num == 0.0f && !val.Contains("."))
                            newval = new LitJson.JsonData((int)num);
                        else
                        {
                            num = (float)Math.Round(num,6);
                            newval = new LitJson.JsonData((double)num);
                        }
                    }
                    else
                    {
                        string str = val.m_string;
                        newval = new LitJson.JsonData(str);
                    }

                    if(elemType == LitJson.JsonType.Array)
                    {
                        if(spec is int intspec)
                            elem[intspec] = newval;
                        else if( spec is string stringspec)
                        {
                            LitJson.JsonData newdata = new();
                            newdata.SetJsonType(LitJson.JsonType.Object);
                            IOrderedDictionary no = newdata as IOrderedDictionary;
                            no.Add(stringspec,newval);
                            return newdata;
                        }
                    }
                    else if(elemType == LitJson.JsonType.Object)
                    {
                        if(spec is string stringspec)
                            elem[stringspec] = newval;
                        else if(spec is int intspec && intspec == 0)
                        {
                            LitJson.JsonData newdata = new();
                            newdata.SetJsonType(LitJson.JsonType.Array);
                            newdata.Add(newval);
                            return newdata;
                        }
                    }
                }
            }
            if(val == ScriptBaseClass.JSON_DELETE)
                throw new IndexOutOfRangeException();
            return null;
        }

        private LitJson.JsonData JsonBuildRestOfSpec(LSL_List specifiers, int level, LSL_String val)
        {
            object spec = level >= specifiers.Data.Length ? null : specifiers.Data[level];
            // 20131224 not used            object specNext = i+1 >= specifiers.Data.Length ? null : specifiers.Data[i+1];

            if (spec == null)
            {
                if(val == null || val == ScriptBaseClass.JSON_NULL || val == "null")
                    return null;
                if(val == ScriptBaseClass.JSON_DELETE)
                    throw new IndexOutOfRangeException();
                if(val == ScriptBaseClass.JSON_TRUE || val == "true")
                    return new LitJson.JsonData(true);
                if(val == ScriptBaseClass.JSON_FALSE || val == "false")
                    return new LitJson.JsonData(false);
                if(val == null || val == ScriptBaseClass.JSON_NULL || val == "null")
                    return null;
                if(float.TryParse(val, out float num))
                {
                    // assuming we are at en.us already
                    if(num - (int)num == 0.0f && !val.Contains("."))
                        return new LitJson.JsonData((int)num);
                    else
                    {
                        num = (float)Math.Round(num,6);
                        return new LitJson.JsonData(num);
                    }
                }
                else
                {
                    string str = val.m_string;
                    return new LitJson.JsonData(str);
                }
                throw new IndexOutOfRangeException();
            }

            if(spec is LSL_String LSL_Stringspec)
                spec = LSL_Stringspec.m_string;
            else if (spec is LSL_Integer LSL_Integerspec)
                spec = LSL_Integerspec.value;

            if (spec is int ||
                (spec is string stringspec && stringspec == ScriptBaseClass.JSON_APPEND) )
            {
                if(spec is int intspec && intspec != 0)
                    throw new IndexOutOfRangeException();
                LitJson.JsonData newdata = new();
                newdata.SetJsonType(LitJson.JsonType.Array);
                newdata.Add(JsonBuildRestOfSpec(specifiers, level + 1, val));
                return newdata;
            }
            else if (spec is string sspec)
            {
                LitJson.JsonData newdata = new();
                newdata.SetJsonType(LitJson.JsonType.Object);
                IOrderedDictionary no = newdata as IOrderedDictionary;
                no.Add(sspec,JsonBuildRestOfSpec(specifiers, level + 1, val));
                return newdata;
            }
            throw new IndexOutOfRangeException();
        }

        private bool JsonFind(LitJson.JsonData elem, LSL_List specifiers, int level, out LitJson.JsonData value)
        {
            value = null;
            if(elem == null)
                return false;

            object spec;
            spec = specifiers.Data[level];

            bool haveVal = false;
            LitJson.JsonData next = null;

            if (elem.GetJsonType() == LitJson.JsonType.Array)
            {
                if (spec is LSL_Integer)
                {
                    int indx = (LSL_Integer)spec;
                    if(indx >= 0 && indx < elem.Count)
                    {
                        haveVal = true;
                        next = (LitJson.JsonData)elem[indx];
                    }
                }
            }
            else if (elem.GetJsonType() == LitJson.JsonType.Object)
            {
                if (spec is LSL_String LSL_Stringspec)
                {
                    IOrderedDictionary e = elem as IOrderedDictionary;
                    string key = LSL_Stringspec.m_string;
                    if(e.Contains(key))
                    {
                        haveVal = true;
                        next = (LitJson.JsonData)e[key];
                    }
                }
            }

            if (haveVal)
            {
                if(level == specifiers.Data.Length - 1)
                {
                    value = next;
                    return true;
                }

                level++;
                if(next == null)
                    return false;

                LitJson.JsonType nextType = next.GetJsonType();
                if(nextType != LitJson.JsonType.Object && nextType != LitJson.JsonType.Array)
                    return false;

                return JsonFind(next, specifiers, level, out value);
            }
            return false;
        }

        public LSL_String llJsonGetValue(LSL_String json, LSL_List specifiers)
        {
            if(String.IsNullOrWhiteSpace(json))
                return ScriptBaseClass.JSON_INVALID;

            if(specifiers.Length > 0 && (json == "{}" || json == "[]"))
                return ScriptBaseClass.JSON_INVALID;

            char first = ((string)json)[0];
            if((first != '[' && first !='{'))
            {
                if(specifiers.Length > 0)
                    return ScriptBaseClass.JSON_INVALID;
                json = "[" + json + "]"; // could handle single element case.. but easier like this
                specifiers.Add((LSL_Integer)0);
            }

            LitJson.JsonData jsonData;
            try
            {
                jsonData = LitJson.JsonMapper.ToObject(json);
            }
            catch //(Exception e)
            {
                //string m = e.Message; // debug point
                return ScriptBaseClass.JSON_INVALID;
            }

            LitJson.JsonData elem;
            if(specifiers.Length == 0)
                elem = jsonData;
            else
            {
                if(!JsonFind(jsonData, specifiers, 0, out elem))
                    return ScriptBaseClass.JSON_INVALID;
            }
            return JsonElementToString(elem);
        }

        private static LSL_String JsonElementToString(LitJson.JsonData elem)
        {
            if(elem is null)
                return ScriptBaseClass.JSON_NULL;

            LitJson.JsonType elemType = elem.GetJsonType();
            switch(elemType)
            {
                case LitJson.JsonType.Array:
                    return new LSL_String(LitJson.JsonMapper.ToJson(elem));
                case LitJson.JsonType.Boolean:
                    return new LSL_String((bool)elem ? ScriptBaseClass.JSON_TRUE : ScriptBaseClass.JSON_FALSE);
                case LitJson.JsonType.Double:
                    double d= (double)elem;
                    string sd = String.Format(Culture.FormatProvider, "{0:0.0#####}",d);
                    return new LSL_String(sd);
                case LitJson.JsonType.Int:
                    int i = (int)elem;
                    return new LSL_String(i.ToString());
                case LitJson.JsonType.Long:
                    long l = (long)elem;
                    return new LSL_String(l.ToString());
                case LitJson.JsonType.Object:
                    return new LSL_String(LitJson.JsonMapper.ToJson(elem));
                case LitJson.JsonType.String:
                    string s = (string)elem;
                    return new LSL_String(s);
                case LitJson.JsonType.None:
                    return ScriptBaseClass.JSON_NULL;
                default:
                    return ScriptBaseClass.JSON_INVALID;
            }
        }

        public LSL_String llJsonValueType(LSL_String json, LSL_List specifiers)
        {
            if(String.IsNullOrWhiteSpace(json))
                return ScriptBaseClass.JSON_INVALID;

            if(specifiers.Length > 0 && (json == "{}" || json == "[]"))
                return ScriptBaseClass.JSON_INVALID;

            char first = ((string)json)[0];
            if((first != '[' && first !='{'))
            {
                if(specifiers.Length > 0)
                    return ScriptBaseClass.JSON_INVALID;
                json = "[" + json + "]"; // could handle single element case.. but easier like this
                specifiers.Add((LSL_Integer)0);
            }

            LitJson.JsonData jsonData;
            try
            {
                jsonData = LitJson.JsonMapper.ToObject(json);
            }
            catch //(Exception e)
            {
                //string m = e.Message; // debug point
                return ScriptBaseClass.JSON_INVALID;
            }

            LitJson.JsonData elem;
            if(specifiers.Length == 0)
                elem = jsonData;
            else
            {
                if(!JsonFind(jsonData, specifiers, 0, out elem))
                    return ScriptBaseClass.JSON_INVALID;
            }

            if(elem == null)
                return ScriptBaseClass.JSON_NULL;

            LitJson.JsonType elemType = elem.GetJsonType();
            switch(elemType)
            {
                case LitJson.JsonType.Array:
                    return ScriptBaseClass.JSON_ARRAY;
                case LitJson.JsonType.Boolean:
                    return (bool)elem ? ScriptBaseClass.JSON_TRUE : ScriptBaseClass.JSON_FALSE;
                case LitJson.JsonType.Double:
                case LitJson.JsonType.Int:
                case LitJson.JsonType.Long:
                    return ScriptBaseClass.JSON_NUMBER;
                case LitJson.JsonType.Object:
                    return ScriptBaseClass.JSON_OBJECT;
                case LitJson.JsonType.String:
                    string s = (string)elem;
                    if(s == ScriptBaseClass.JSON_NULL)
                        return ScriptBaseClass.JSON_NULL;
                    if(s == ScriptBaseClass.JSON_TRUE)
                        return ScriptBaseClass.JSON_TRUE;
                    if(s == ScriptBaseClass.JSON_FALSE)
                        return ScriptBaseClass.JSON_FALSE;
                    return ScriptBaseClass.JSON_STRING;
                case LitJson.JsonType.None:
                    return ScriptBaseClass.JSON_NULL;
                default:
                    return ScriptBaseClass.JSON_INVALID;
            }
        }

        public LSL_String llChar(LSL_Integer unicode)
        {
            if(unicode == 0)
                return LSL_String.Empty;
            try
            {
                return Char.ConvertFromUtf32(unicode);
            }
            catch { }

            return "\ufffd";
        }

        public LSL_Integer llOrd(LSL_String s, LSL_Integer index)
        {
            if (string.IsNullOrEmpty(s))
                return 0;

            if(index < 0)
                index += s.Length;

            if (index < 0 || index >= s.Length)
                return 0;

            char c = s.m_string[index];
            if(c >= 0xdc00 && c <= 0xdfff)
            {
                --index;
                if (index < 0)
                    return 0;

                int a = c - 0xdc00;
                c = s.m_string[index];
                if (c < 0xd800 || c > 0xdbff)
                    return 0;
                c -= (char)(0xd800 - 0x40);
                return a + (c << 10);
            }

            if (c >= 0xd800)
            {
                if(c < 0xdc00)
                {
                    ++index;
                    if (index >= s.Length)
                        return 0;

                    c -= (char)(0xd800 - 0x40);
                    int a = (c << 10);

                    c = s.m_string[index];
                    if (c < 0xdc00 || c > 0xdfff)
                        return 0;
                    c -= (char)0xdc00;
                    return a + c;
                }
                else if(c < 0xe000)
                    return 0;
            }
            return (int)c;
        }

        public LSL_Integer llHash(LSL_String s)
        {
            if (string.IsNullOrEmpty(s))
                return 0;
            int hash = 0;
            char c;
            for(int i = 0; i < s.Length; ++i)
            {
                hash *= 65599;
                // on modern intel/amd this is faster than the tradicional optimization:
                // hash = (hash << 6) + (hash << 16) - hash;
                c = s.m_string[i];
                if (c >= 0xd800)
                {
                    if(c < 0xdc00)
                    {
                        ++i;
                        if(i >= s.Length)
                            return 0;

                        c -= (char)(0xd800 - 0x40);
                        hash += (c << 10);

                        c = s.m_string[i];
                        if(c < 0xdc00 || c > 0xdfff)
                            return 0;
                        c -= (char)(0xdc00);
                    }
                    else if(c < 0xe000)
                        return 0;
                }
                hash += c;
            }
            return hash;
        }

        public LSL_String llReplaceSubString(LSL_String src, LSL_String pattern, LSL_String replacement, int count)
        {
            RegexOptions RegexOptions;
            if (count < 0)
            {
                RegexOptions = RegexOptions.CultureInvariant | RegexOptions.RightToLeft;
                count = -count;
            }
            else
            {
                RegexOptions = RegexOptions.CultureInvariant;
                if (count == 0)
                    count = -1;
            }

            try
            {
                if (string.IsNullOrEmpty(src.m_string))
                    return src;

                if (string.IsNullOrEmpty(pattern.m_string))
                    return src;

                Regex rx = new(pattern, RegexOptions, TimeSpan.FromMilliseconds(10));
                if (replacement == null)
                    return rx.Replace(src.m_string, string.Empty, count);

                return rx.Replace(src.m_string, replacement.m_string, count);
            }
            catch
            {
                return src;
            }
        }

        public LSL_Vector llLinear2sRGB(LSL_Vector src)
        {
            return new LSL_Vector(Util.LinearTosRGB((float)src.x), Util.LinearTosRGB((float)src.y), Util.LinearTosRGB((float)src.z));
        }

        public LSL_Vector llsRGB2Linear(LSL_Vector src)
        {
            return new LSL_Vector(Util.sRGBtoLinear((float)src.x), Util.sRGBtoLinear((float)src.y), Util.sRGBtoLinear((float)src.z));
        }

        public LSL_Integer llLinksetDataAvailable()
        {
            if (m_host.ParentGroup.LinksetData is null)
                return m_linksetDataLimit;

            return new LSL_Integer(m_host.ParentGroup.LinksetData.Free());
        }

        public LSL_Integer llLinksetDataCountKeys()
        {
            if (m_host.ParentGroup.LinksetData is null)
                return 0;

            return new LSL_Integer(m_host.ParentGroup.LinksetData.Count());
        }

        public LSL_String llLinksetDataRead(LSL_String name)
        {
            if (m_host.ParentGroup.LinksetData is null || string.IsNullOrEmpty(name.m_string))
                return new LSL_String(string.Empty);

            return new LSL_String(m_host.ParentGroup.LinksetData.Get(name.m_string));
        }

        public LSL_String llLinksetDataReadProtected(LSL_String name, LSL_String pass)
        {
            if (m_host.ParentGroup.LinksetData is null || string.IsNullOrEmpty(name.m_string))
                return new LSL_String(string.Empty);

            return new LSL_String(m_host.ParentGroup.LinksetData.Get(name.m_string, pass.m_string));
        }

        public LSL_Integer llLinksetDataDelete(LSL_String name)
        {
            if (string.IsNullOrEmpty(name.m_string))
                return ScriptBaseClass.LINKSETDATA_ENOKEY;
            if (m_host.ParentGroup.LinksetData is null)
                return ScriptBaseClass.LINKSETDATA_NOTFOUND;

            int ret = m_host.ParentGroup.LinksetData.Remove(name.m_string);
            if (ret == 0)
            {
                m_ScriptEngine.PostObjectLinksetDataEvent(m_host.LocalId, ScriptBaseClass.LINKSETDATA_DELETE, name.m_string, string.Empty);
                m_host.ParentGroup.HasGroupChanged = true;
            }
            return ret;
        }

        public LSL_Integer llLinksetDataDeleteProtected(LSL_String name, LSL_String pass)
        {
            if (string.IsNullOrEmpty(name.m_string))
                return ScriptBaseClass.LINKSETDATA_ENOKEY;
            if (m_host.ParentGroup.LinksetData is null)
                return ScriptBaseClass.LINKSETDATA_NOTFOUND;

            int ret = m_host.ParentGroup.LinksetData.Remove(name.m_string, pass.m_string);
            if (ret == 0)
            {
                m_ScriptEngine.PostObjectLinksetDataEvent(m_host.LocalId, ScriptBaseClass.LINKSETDATA_DELETE, name.m_string, string.Empty);
                m_host.ParentGroup.HasGroupChanged = true;
            }
            return ret;
        }

        public void llLinksetDataReset()
        {
            if (m_host.ParentGroup.LinksetData is null)
                return;

            bool changed = m_host.ParentGroup.LinksetData.Count() > 0;
            m_host.ParentGroup.LinksetData = null;

            if(changed)
            {
                m_ScriptEngine.PostObjectLinksetDataEvent(m_host.LocalId, ScriptBaseClass.LINKSETDATA_RESET, string.Empty, string.Empty);
                m_host.ParentGroup.HasGroupChanged = true;
            }
        }

        public LSL_Integer llLinksetDataWrite(LSL_String name, LSL_String value)
        {
            if (string.IsNullOrEmpty(name.m_string))
                return ScriptBaseClass.LINKSETDATA_ENOKEY;

            int ret;
            if (string.IsNullOrEmpty(value.m_string))
            {
                if (m_host.ParentGroup.LinksetData is null)
                    return ScriptBaseClass.LINKSETDATA_NOTFOUND;

                ret = m_host.ParentGroup.LinksetData.Remove(name.m_string);
                if (ret == 0)
                {
                    m_ScriptEngine.PostObjectLinksetDataEvent(m_host.LocalId, ScriptBaseClass.LINKSETDATA_DELETE, name.m_string, string.Empty);
                    m_host.ParentGroup.HasGroupChanged = true;
                }
                return ret;
            }

            m_host.ParentGroup.LinksetData ??= new(m_linksetDataLimit);
            ret = m_host.ParentGroup.LinksetData.AddOrUpdate(name.m_string, value.m_string);
            if (ret == 0)
            {
                m_ScriptEngine.PostObjectLinksetDataEvent(m_host.LocalId, ScriptBaseClass.LINKSETDATA_UPDATE, name.m_string, value.m_string);
                m_host.ParentGroup.HasGroupChanged = true;
            }
            return ret;
        }

        public LSL_Integer llLinksetDataWriteProtected(LSL_String name, LSL_String value, LSL_String pass)
        {
            if (string.IsNullOrEmpty(name.m_string))
                return ScriptBaseClass.LINKSETDATA_ENOKEY;

            int ret;
            if (string.IsNullOrEmpty(value.m_string))
            {
                if (m_host.ParentGroup.LinksetData is null)
                    return ScriptBaseClass.LINKSETDATA_NOTFOUND;

                ret = m_host.ParentGroup.LinksetData.Remove(name.m_string, pass.m_string);
                if (ret == 0)
                {
                    m_ScriptEngine.PostObjectLinksetDataEvent(m_host.LocalId, ScriptBaseClass.LINKSETDATA_DELETE, name.m_string, string.Empty);
                    m_host.ParentGroup.HasGroupChanged = true;
                }
                return ret;
            }

            m_host.ParentGroup.LinksetData ??= new(m_linksetDataLimit);
            ret = m_host.ParentGroup.LinksetData.AddOrUpdate(name.m_string, value.m_string, pass.m_string);
            if (ret == 0)
            {
                m_ScriptEngine.PostObjectLinksetDataEvent(m_host.LocalId, ScriptBaseClass.LINKSETDATA_UPDATE, name.m_string, string.Empty);
                m_host.ParentGroup.HasGroupChanged = true;
            }
            return ret;
        }

        public LSL_List llLinksetDataDeleteFound(LSL_String pattern, LSL_String pass)
        {
            if (string.IsNullOrEmpty(pattern.m_string) || m_host.ParentGroup.LinksetData is null)
                return new LSL_List(new object[] { new LSL_Integer(0), new LSL_Integer(0)});

            string[] deleted = m_host.ParentGroup.LinksetData.RemoveByPattern(pattern.m_string, pass.m_string, out int notDeleted);
            int deletedCount = deleted.Length;
            if(deleted.Length > 0)
            {
                string deletedList = string.Join(",", deleted);
                m_ScriptEngine.PostObjectLinksetDataEvent(m_host.LocalId, ScriptBaseClass.LINKSETDATA_MULTIDELETE, deletedList, string.Empty);
                m_host.ParentGroup.HasGroupChanged = true;
            }
            return new LSL_List(new object[] { new LSL_Integer(deleted.Length), new LSL_Integer(notDeleted) });
        }

        public LSL_Integer llLinksetDataCountFound(LSL_String pattern)
        {
            if (string.IsNullOrEmpty(pattern.m_string) || m_host.ParentGroup.LinksetData is null)
                return new LSL_Integer(0);

            return m_host.ParentGroup.LinksetData.CountByPattern(pattern.m_string);
        }

        public LSL_List llLinksetDataListKeys(LSL_Integer start, LSL_Integer count)
        {
            if (m_host.ParentGroup.LinksetData is null)
                return new LSL_List();

            return new LSL_List(m_host.ParentGroup.LinksetData.ListKeys(start, count));
        }

        public LSL_List llLinksetDataFindKeys(LSL_String pattern, LSL_Integer start, LSL_Integer count)
        {
            if (string.IsNullOrEmpty(pattern.m_string) || m_host.ParentGroup.LinksetData is null)
                return new LSL_List();

            return new LSL_List(m_host.ParentGroup.LinksetData.ListKeysByPatttern(pattern.m_string, start, count));
        }

        public LSL_Integer llIsFriend(LSL_Key agent_id)
        {
            SceneObjectGroup parentsog = m_host.ParentGroup;
            if (parentsog is null || parentsog.IsDeleted)
                return 0;

            if (parentsog.OwnerID.Equals(parentsog.GroupID))
                return llSameGroup(agent_id);

            if (!UUID.TryParse(agent_id, out UUID agent) || agent.IsZero())
                return 0;

            IFriendsModule fm = World.RequestModuleInterface<IFriendsModule>();
            if(fm is null)
                return 0;

            if (World.TryGetSceneRootPresence(agent, out _))
                return fm.IsFriend(agent, parentsog.OwnerID) ? 1 : 0;

            if (World.TryGetSceneRootPresence(parentsog.OwnerID, out _))
                return fm.IsFriend(parentsog.OwnerID, agent) ? 1 : 0;

            return 0;
        }

        public LSL_Integer llDerezObject(LSL_Key objectUUID, LSL_Integer flag)
        {
            if (!UUID.TryParse(objectUUID, out UUID objUUID))
                return new LSL_Integer(0);

            if (objUUID.IsZero())
                return new LSL_Integer(0);

            SceneObjectGroup sceneOG = World.GetSceneObjectGroup(objUUID);

            if (sceneOG is null || sceneOG.IsDeleted || sceneOG.IsAttachment)
                return new LSL_Integer(0);

            if (sceneOG.OwnerID.NotEqual(m_host.OwnerID))
                return new LSL_Integer(0);

            // restrict to objects rezzed by host
            if (sceneOG.RezzerID.NotEqual(m_host.ParentGroup.UUID))
                return new LSL_Integer(0);

            if (sceneOG.UUID.Equals(m_host.ParentGroup.UUID))
                return new LSL_Integer(0);

            if (flag.value == 0)
                World.DeleteSceneObject(sceneOG, false);
            else
                sceneOG.RootPart.AddFlag(PrimFlags.TemporaryOnRez);

            return new LSL_Integer(1);
        }

        public LSL_Integer llGetLinkSitFlags(LSL_Integer linknum)
        {
            SceneObjectPart part = linknum == ScriptBaseClass.LINK_THIS ? m_host : m_host.ParentGroup.GetLinkNumPart(linknum);
            if (part is not null)
            {
                int flags = ScriptBaseClass.SIT_FLAG_OPENSIMFORCED;
                if(part.IsSitTargetSet)
                    flags |= 0x01;
                return new LSL_Integer(flags);
            }
            return new LSL_Integer(0);
        }

        public void llSetLinkSitFlags(LSL_Integer linknum, LSL_Integer flags)
        {
            // does nothing since we do not have any of the flags
            /*
            SceneObjectPart part = linknum == ScriptBaseClass.LINK_THIS ? m_host : m_host.ParentGroup.GetLinkNumPart(linknum);
            if (part is not null)
            {
            }
            */
        }

        public LSL_String llComputeHash(LSL_String message, LSL_String algo)
        {
            switch (algo)
            {
                case "md5":
                    using (MD5 md5 = MD5.Create())
                    {
                        byte[] hashedBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(message));
                        return Util.bytesToLowcaseHexString(hashedBytes);
                    }
                case "md5_sha1":
                    using (MD5 md5 = MD5.Create())
                    using (SHA1 sha1 = SHA1.Create())
                    {
                        byte[] inputBytes = Encoding.UTF8.GetBytes(message);
                        byte[] md5Hash = md5.ComputeHash(inputBytes);
                        byte[] sha1Hash = sha1.ComputeHash(inputBytes);

                        byte[] combinedHash = new byte[md5Hash.Length + sha1Hash.Length];
                        Buffer.BlockCopy(md5Hash, 0, combinedHash, 0, md5Hash.Length);
                        Buffer.BlockCopy(sha1Hash, 0, combinedHash, md5Hash.Length, sha1Hash.Length);
                        return BitConverter.ToString(combinedHash).Replace("-", "").ToLower();
                    }
                case "sha1":
                    using (SHA1 sha1 = SHA1.Create())
                    {
                        byte[] hashedBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(message));
                        return Util.bytesToLowcaseHexString(hashedBytes);
                    }
                case "sha224":
                    using (SHA224 sha224 = SHA224.Create())
                    {
                        byte[] hashedBytes = sha224.ComputeHash(Encoding.UTF8.GetBytes(message));
                        return Util.bytesToLowcaseHexString(hashedBytes);
                    }
                case "sha256":
                    using (SHA256 sha1 = SHA256.Create())
                    {
                        byte[] hashedBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(message));
                        return Util.bytesToLowcaseHexString(hashedBytes);
                    }
                case "sha384":
                    using (SHA384 sha1 = SHA384.Create())
                    {
                        byte[] hashedBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(message));
                        return Util.bytesToLowcaseHexString(hashedBytes);
                    }
                case "sha512":
                    using (SHA512 sha1 = SHA512.Create())
                    {
                        byte[] hashedBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(message));
                        return Util.bytesToLowcaseHexString(hashedBytes);
                    }
                default:
                    break;
            }
            return new LSL_String();
        }

        static string HMAC_SHA224(string key, string message)
        {
            const int blockSize = 64;
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);

            if (keyBytes.Length > blockSize)
            {
                using (SHA224 sha224 = SHA224.Create())
                {
                    byte[] hashedBytes = sha224.ComputeHash(keyBytes);
                }
            }

            if (keyBytes.Length < blockSize)
            {
                byte[] tmp = new byte[blockSize];
                Array.Copy(keyBytes, tmp, keyBytes.Length);
                keyBytes = tmp;
            }

            byte[] o_key_pad = new byte[blockSize];
            byte[] i_key_pad = new byte[blockSize];

            for (int i = 0; i < blockSize; i++)
            {
                o_key_pad[i] = (byte)(keyBytes[i] ^ 0x5c);
                i_key_pad[i] = (byte)(keyBytes[i] ^ 0x36);
            }

            byte[] messageBytes = Encoding.UTF8.GetBytes(message);

            byte[] inner = new byte[i_key_pad.Length + messageBytes.Length];
            Array.Copy(i_key_pad, 0, inner, 0, i_key_pad.Length);
            Array.Copy(messageBytes, 0, inner, i_key_pad.Length, messageBytes.Length);

            byte[] innerHash;

            using (SHA224 sha224 = SHA224.Create())
            {
                innerHash = sha224.ComputeHash(inner);
            }

            byte[] outer = new byte[o_key_pad.Length + innerHash.Length];
            Array.Copy(o_key_pad, 0, outer, 0, o_key_pad.Length);
            Array.Copy(innerHash, 0, outer, o_key_pad.Length, innerHash.Length);

            using (SHA224 sha224 = SHA224.Create())
            {
                return Util.bytesToLowcaseHexString(sha224.ComputeHash(outer));
            }
        }

        public LSL_String llHMAC(LSL_String private_key, LSL_String message, LSL_String algo)
        {
            if (private_key.Length < 1 || message.Length < 1)
                return new LSL_String();

            try
            {
                HMAC hasher;
                switch (algo)
                {
                    case "md5":
                        hasher = new HMACMD5(Encoding.UTF8.GetBytes(private_key));
                        break;
                    case "sha1":
                        hasher = new HMACSHA1(Encoding.UTF8.GetBytes(private_key));
                        break;
                    case "sha224":
                        return HMAC_SHA224(private_key, message);

                    case "sha256":
                        hasher = new HMACSHA256(Encoding.UTF8.GetBytes(private_key));
                        break;
                    case "sha384":
                        hasher = new HMACSHA384(Encoding.UTF8.GetBytes(private_key));
                        break;
                    case "sha512":
                        hasher = new HMACSHA512(Encoding.UTF8.GetBytes(private_key));
                        break;
                    default:
                        return new LSL_String();
                }

                byte[] hashBytes;
                try
                {
                    hashBytes = hasher.ComputeHash(Encoding.UTF8.GetBytes(message));
                }
                catch
                {
                    return new LSL_String();
                }
                finally
                {
                    hasher.Dispose();
                }
                return new LSL_String(Util.bytesToLowcaseHexString(hashBytes));
            }
            catch { }
            return new LSL_String();
        }
    }

    public class NotecardCache
    {
        private static readonly ExpiringCacheOS<UUID, string[]> m_Notecards = new(30000);

        public static void Cache(UUID assetID, byte[] text)
        {
            if (m_Notecards.ContainsKey(assetID, 30000))
                return;

            m_Notecards.AddOrUpdate(assetID, SLUtil.ParseNotecardToArray(text), 30);
        }

        public static bool IsCached(UUID assetID)
        {
            return m_Notecards.ContainsKey(assetID, 30000);
        }

        public static int GetLines(UUID assetID)
        {
            if (m_Notecards.TryGetValue(assetID, 30000, out string[] text))
                return text.Length;
            return -1;
        }

        /// <summary>
        /// Get a notecard line.
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="lineNumber">Lines start at index 0</param>
        /// <returns></returns>
        public static string GetLine(UUID assetID, int lineNumber)
        {
            if (lineNumber >= 0 && m_Notecards.TryGetValue(assetID, 30000, out string[] text))
            {
                if (lineNumber >= text.Length)
                    return "\n\n\n";
                return text[lineNumber];
            }
            return "";
        }

        public static string GetllLine(UUID assetID, int lineNumber, int maxLength)
        {
            if (m_Notecards.TryGetValue(assetID, 30000, out string[] text))
            {
                if (lineNumber >= text.Length)
                    return "\n\n\n";

                return text[lineNumber].Length < maxLength ? text[lineNumber] : text[lineNumber][..maxLength];
            }
            return ScriptBaseClass.NAK;
        }

        /// <summary>
        /// Get a notecard line.
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="lineNumber">Lines start at index 0</param>
        /// <param name="maxLength">
        /// Maximum length of the returned line.
        /// </param>
        /// <returns>
        /// If the line length is longer than <paramref name="maxLength"/>,
        /// the return string will be truncated.
        /// </returns>
        public static string GetLine(UUID assetID, int lineNumber, int maxLength)
        {
            string line = GetLine(assetID, lineNumber);

            if (line.Length > maxLength)
                return line[..maxLength];

            return line;
        }

    }

    // C# doesn't have a native way to do this so instead of adding a library this will do
    public class SHA224 : HashAlgorithm, IDisposable
    {
        private byte[] stream;

        public SHA224()
        {
            HashSizeValue = 224;
        }

        public override void Initialize()
        {
            stream = new byte[224];
        }

        public static new SHA224 Create()
        {
            return new SHA224();
        }

        public new void Dispose()
        {
            base.Dispose();
        }

        private static byte[] ComputeHashInternal(byte[] data)
        {
            uint[] K = {
            0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
            0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
            0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
            0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
            0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
            0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
            0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
            0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
            };

            uint[] H = {
            0xc1059ed8, 0x367cd507, 0x3070dd17, 0xf70e5939,
            0xffc00b31, 0x68581511, 0x64f98fa7, 0xbefa4fa4
            };

            ulong bitLength = (ulong)data.Length * 8;
            int paddedLength = ((data.Length + 9 + 63) / 64) * 64;
            byte[] padded = new byte[paddedLength];
            Array.Copy(data, padded, data.Length);
            padded[data.Length] = 0x80;

            for (int i = 0; i < 8; i++)
                padded[padded.Length - 8 + i] = (byte)((bitLength >> ((7 - i) * 8)) & 0xFF);

            for (int chunk = 0; chunk < padded.Length / 64; chunk++)
            {
                uint[] W = new uint[64];

                for (int i = 0; i < 16; i++)
                {
                    W[i] = (uint)(padded[chunk * 64 + i * 4] << 24) |
                           (uint)(padded[chunk * 64 + i * 4 + 1] << 16) |
                           (uint)(padded[chunk * 64 + i * 4 + 2] << 8) |
                           (uint)(padded[chunk * 64 + i * 4 + 3]);
                }

                for (int i = 16; i < 64; i++)
                {
                    uint s0 = (W[i - 15] >> 7 | W[i - 15] << 25) ^ (W[i - 15] >> 18 | W[i - 15] << 14) ^ (W[i - 15] >> 3);
                    uint s1 = (W[i - 2] >> 17 | W[i - 2] << 15) ^ (W[i - 2] >> 19 | W[i - 2] << 13) ^ (W[i - 2] >> 10);
                    W[i] = W[i - 16] + s0 + W[i - 7] + s1;
                }

                uint a = H[0], b = H[1], c = H[2], d = H[3], e = H[4], f = H[5], g = H[6], h = H[7];

                for (int i = 0; i < 64; i++)
                {
                    uint S1 = (e >> 6 | e << 26) ^ (e >> 11 | e << 21) ^ (e >> 25 | e << 7);
                    uint ch = (e & f) ^ (~e & g);
                    uint temp1 = h + S1 + ch + K[i] + W[i];
                    uint S0 = (a >> 2 | a << 30) ^ (a >> 13 | a << 19) ^ (a >> 22 | a << 10);
                    uint maj = (a & b) ^ (a & c) ^ (b & c);
                    uint temp2 = S0 + maj;

                    h = g;
                    g = f;
                    f = e;
                    e = d + temp1;
                    d = c;
                    c = b;
                    b = a;
                    a = temp1 + temp2;
                }

                H[0] += a;
                H[1] += b;
                H[2] += c;
                H[3] += d;
                H[4] += e;
                H[5] += f;
                H[6] += g;
                H[7] += h;
            }

            byte[] result = new byte[28];
            for (int i = 0; i < 7; i++)
            {
                result[i * 4 + 0] = (byte)((H[i] >> 24) & 0xFF);
                result[i * 4 + 1] = (byte)((H[i] >> 16) & 0xFF);
                result[i * 4 + 2] = (byte)((H[i] >> 8) & 0xFF);
                result[i * 4 + 3] = (byte)(H[i] & 0xFF);
            }

            return result;
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            stream = array;
        }

        protected override byte[] HashFinal()
        {
            return ComputeHashInternal(stream);
        }
    }
}
