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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using System.Timers;
using Nini.Config;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Packets;
using OpenMetaverse.Rendering;
using OpenSim;
using OpenSim.Framework;

using OpenSim.Region.CoreModules;
using OpenSim.Region.CoreModules.World.Land;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Region.Framework.Scenes.Animation;
using OpenSim.Region.Framework.Scenes.Scripting;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api.Plugins;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using PresenceInfo = OpenSim.Services.Interfaces.PresenceInfo;
using PrimType = OpenSim.Region.Framework.Scenes.PrimType;
using AssetLandmark = OpenSim.Framework.AssetLandmark;
using RegionFlags = OpenSim.Framework.RegionFlags;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;
using System.Reflection;
using Timer = System.Timers.Timer;
using System.Linq;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Region.ScriptEngine.Shared.Api
{
    // MUST be a ref type
    public class UserInfoCacheEntry
    {
        public int time;
        public UserAccount account;
        public PresenceInfo pinfo;
    }

    /// <summary>
    /// Contains all LSL ll-functions. This class will be in Default AppDomain.
    /// </summary>
    public class LSL_Api : MarshalByRefObject, ILSL_Api, IScriptApi
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public int LlRequestAgentDataCacheTimeoutMs { get; set; }

        protected IScriptEngine m_ScriptEngine;
        protected SceneObjectPart m_host;

        /// <summary>
        /// Used for script sleeps when we are using co-operative script termination.
        /// </summary>
        /// <remarks>null if co-operative script termination is not active</remarks>
        /// <summary>
        /// The item that hosts this script
        /// </summary>
        protected TaskInventoryItem m_item;

        protected bool throwErrorOnNotImplemented = false;
        protected AsyncCommandManager AsyncCommands = null;
        protected float m_ScriptDelayFactor = 1.0f;
        protected float m_ScriptDistanceFactor = 1.0f;
        protected float m_MinTimerInterval = 0.5f;
        protected float m_recoilScaleFactor = 0.0f;

        protected DateTime m_timer = DateTime.Now;
        protected bool m_waitingForScriptAnswer = false;
        protected bool m_automaticLinkPermission = false;
        protected IMessageTransferModule m_TransferModule = null;
        protected int m_notecardLineReadCharsMax = 255;
        protected int m_scriptConsoleChannel = 0;
        protected bool m_scriptConsoleChannelEnabled = false;
        protected bool m_debuggerSafe = false;
        protected IUrlModule m_UrlModule = null;

        protected Dictionary<UUID, UserInfoCacheEntry> m_userInfoCache = new Dictionary<UUID, UserInfoCacheEntry>();
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
        protected int m_sleepMsOnEmail = 20000;
        protected int m_sleepMsOnCreateLink = 1000;
        protected int m_sleepMsOnGiveInventory = 3000;
        protected int m_sleepMsOnRequestAgentData = 100;
        protected int m_sleepMsOnRequestInventoryData = 1000;
        protected int m_sleepMsOnSetDamage = 5000;
        protected int m_sleepMsOnTextBox = 1000;
        protected int m_sleepMsOnAdjustSoundVolume = 100;
        protected int m_sleepMsOnEjectFromLand = 5000;
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
        protected int m_sleepMsOnLoadURL = 10000;
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
        protected bool m_useCastRayV3 = false;
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
        protected bool m_filterPartsInCastRay = false;
        protected bool m_doAttachmentsInCastRay = false;
        protected int m_msThrottleInCastRay = 200;
        protected int m_msPerRegionInCastRay = 40;
        protected int m_msPerAvatarInCastRay = 10;
        protected int m_msMinInCastRay = 2;
        protected int m_msMaxInCastRay = 40;
        protected static List<CastRayCall> m_castRayCalls = new List<CastRayCall>();
        protected bool m_useMeshCacheInCastRay = true;
        protected static Dictionary<ulong, FacetedMesh> m_cachedMeshes = new Dictionary<ulong, FacetedMesh>();

//        protected Timer m_ShoutSayTimer;
        protected int m_SayShoutCount = 0;
        DateTime m_lastSayShoutCheck;

        private Dictionary<string, string> MovementAnimationsForLSL =
                new Dictionary<string, string> {
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

        //An array of HTTP/1.1 headers that are not allowed to be used
        //as custom headers by llHTTPRequest.
        private string[] HttpStandardHeaders =
        {
            "Accept", "Accept-Charset", "Accept-Encoding", "Accept-Language",
            "Accept-Ranges", "Age", "Allow", "Authorization", "Cache-Control",
            "Connection", "Content-Encoding", "Content-Language",
            "Content-Length", "Content-Location", "Content-MD5",
            "Content-Range", "Content-Type", "Date", "ETag", "Expect",
            "Expires", "From", "Host", "If-Match", "If-Modified-Since",
            "If-None-Match", "If-Range", "If-Unmodified-Since", "Last-Modified",
            "Location", "Max-Forwards", "Pragma", "Proxy-Authenticate",
            "Proxy-Authorization", "Range", "Referer", "Retry-After", "Server",
            "TE", "Trailer", "Transfer-Encoding", "Upgrade", "User-Agent",
            "Vary", "Via", "Warning", "WWW-Authenticate"
        };

        public void Initialize(
            IScriptEngine scriptEngine, SceneObjectPart host, TaskInventoryItem item)
        {
            m_lastSayShoutCheck = DateTime.UtcNow;

            m_ScriptEngine = scriptEngine;
            m_host = host;
            m_item = item;
            m_debuggerSafe = m_ScriptEngine.Config.GetBoolean("DebuggerSafe", false);
 
            LoadConfig();

            m_TransferModule =
                    m_ScriptEngine.World.RequestModuleInterface<IMessageTransferModule>();
            m_UrlModule = m_ScriptEngine.World.RequestModuleInterface<IUrlModule>();
            m_SoundModule = m_ScriptEngine.World.RequestModuleInterface<ISoundModule>();

            AsyncCommands = new AsyncCommandManager(m_ScriptEngine);
        }

        /// <summary>
        /// Load configuration items that affect script, object and run-time behavior. */
        /// </summary>
        private void LoadConfig()
        {
            LlRequestAgentDataCacheTimeoutMs = 20000;

            IConfig seConfig = m_ScriptEngine.Config;

            if (seConfig != null)
            {
                m_ScriptDelayFactor =
                    seConfig.GetFloat("ScriptDelayFactor", m_ScriptDelayFactor);
                m_ScriptDistanceFactor =
                    seConfig.GetFloat("ScriptDistanceLimitFactor", m_ScriptDistanceFactor);
                m_MinTimerInterval =
                    seConfig.GetFloat("MinTimerInterval", m_MinTimerInterval);
                m_automaticLinkPermission =
                    seConfig.GetBoolean("AutomaticLinkPermission", m_automaticLinkPermission);
                m_notecardLineReadCharsMax =
                    seConfig.GetInt("NotecardLineReadCharsMax", m_notecardLineReadCharsMax);

                // Rezzing an object with a velocity can create recoil. This feature seems to have been
                //    removed from recent versions of SL. The code computes recoil (vel*mass) and scales
                //    it by this factor. May be zero to turn off recoil all together.
                m_recoilScaleFactor = m_ScriptEngine.Config.GetFloat("RecoilScaleFactor", m_recoilScaleFactor);
            }

            if (m_notecardLineReadCharsMax > 65535)
                m_notecardLineReadCharsMax = 65535;

            // load limits for particular subsystems.
            IConfigSource seConfigSource = m_ScriptEngine.ConfigSource;

            if (seConfigSource != null)
            {
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
                    m_useCastRayV3 = lslConfig.GetBoolean("UseLlCastRayV3", m_useCastRayV3);
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
                    m_filterPartsInCastRay = lslConfig.GetBoolean("FilterPartsInLlCastRay", m_filterPartsInCastRay);
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
            }
            m_sleepMsOnEmail = EMAIL_PAUSE_TIME * 1000;
        }

        public override Object InitializeLifetimeService()
        {
            ILease lease = (ILease)base.InitializeLifetimeService();

            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.FromMinutes(0);
//                lease.RenewOnCallTime = TimeSpan.FromSeconds(10.0);
//                lease.SponsorshipTimeout = TimeSpan.FromMinutes(1.0);
            }
            return lease;
        }

        protected virtual void ScriptSleep(int delay)
        {
            delay = (int)((float)delay * m_ScriptDelayFactor);
            if (delay == 0)
                return;

            Sleep(delay);
        }

        protected virtual void Sleep(int delay)
        {
            if (m_item == null) // Some unit tests don't set this
            {
                Thread.Sleep(delay);
                return;
            }

            m_ScriptEngine.SleepScript(m_item.ItemID, delay);
        }

        /// <summary>
        /// Check for co-operative termination.
        /// </summary>
        /// <param name='delay'>If called with 0, then just the check is performed with no wait.</param>

        public Scene World
        {
            get { return m_ScriptEngine.World; }
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
            m_host.AddScriptLPS(1);

            // We need to tell the URL module, if we hav one, to release
            // the allocated URLs
            if (m_UrlModule != null)
                m_UrlModule.ScriptRemoved(m_item.ItemID);

            m_ScriptEngine.ApiResetScript(m_item.ItemID);
        }

        public void llResetOtherScript(string name)
        {
            UUID item;

            m_host.AddScriptLPS(1);

            if ((item = GetScriptByName(name)) != UUID.Zero)
                m_ScriptEngine.ResetScript(item);
            else
                Error("llResetOtherScript", "Can't find script '" + name + "'");
        }

        public LSL_Integer llGetScriptState(string name)
        {
            UUID item;

            m_host.AddScriptLPS(1);

            if ((item = GetScriptByName(name)) != UUID.Zero)
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
            UUID item;

            m_host.AddScriptLPS(1);

            // These functions are supposed to be robust,
            // so get the state one step at a time.

            if ((item = GetScriptByName(name)) != UUID.Zero)
            {
                m_ScriptEngine.SetScriptState(item, run == 0 ? false : true);
            }
            else
            {
                Error("llSetScriptState", "Can't find script '" + name + "'");
            }
        }

        public List<ScenePresence> GetLinkAvatars(int linkType)
        {
            List<ScenePresence> ret = new List<ScenePresence>();
            if (m_host == null || m_host.ParentGroup == null || m_host.ParentGroup.IsDeleted)
                return ret;

            //            List<ScenePresence> avs = m_host.ParentGroup.GetLinkedAvatars();
            // this needs check
            List<ScenePresence> avs = m_host.ParentGroup.GetSittingAvatars();
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

                    int partCount = m_host.ParentGroup.GetPartCount();

                    if (linkType <= partCount)
                    {
                        return ret;
                    }
                    else
                    {
                        linkType = linkType - partCount;
                        if (linkType > avs.Count)
                        {
                            return ret;
                        }
                        else
                        {
                            ret.Add(avs[linkType-1]);
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
        public ISceneEntity GetLinkEntity(SceneObjectPart part, int linknum)
        {
            if (linknum < 0)
            {
                if (linknum == ScriptBaseClass.LINK_THIS)
                    return part;
                else
                    return null;
            }

            int actualPrimCount = part.ParentGroup.PrimCount;
            List<ScenePresence> sittingAvatars = part.ParentGroup.GetSittingAvatars();
            int adjustedPrimCount = actualPrimCount + sittingAvatars.Count;

            // Special case for a single prim.  In this case the linknum is zero.  However, this will not match a single
            // prim that has any avatars sat upon it (in which case the root prim is link 1).
            if (linknum == 0)
            {
                if (actualPrimCount == 1 && sittingAvatars.Count == 0)
                    return part;

                return null;
            }
            // Special case to handle a single prim with sitting avatars.  GetLinkPart() would only match zero but
            // here we must match 1 (ScriptBaseClass.LINK_ROOT).
            else if (linknum == ScriptBaseClass.LINK_ROOT && actualPrimCount == 1)
            {
                if (sittingAvatars.Count > 0)
                    return part.ParentGroup.RootPart;
                else
                    return null;
            }
            else if (linknum <= adjustedPrimCount)
            {
                if (linknum <= actualPrimCount)
                {
                    return part.ParentGroup.GetLinkNumPart(linknum);
                }
                else
                {
                    return sittingAvatars[linknum - actualPrimCount - 1];
                }
            }
            else
            {
                return null;
            }
        }

        public List<SceneObjectPart> GetLinkParts(int linkType)
        {
            return GetLinkParts(m_host, linkType);
        }

        public static List<SceneObjectPart> GetLinkParts(SceneObjectPart part, int linkType)
        {
            List<SceneObjectPart> ret = new List<SceneObjectPart>();
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return ret;
            ret.Add(part);

            switch (linkType)
            {
            case ScriptBaseClass.LINK_SET:
                return new List<SceneObjectPart>(part.ParentGroup.Parts);

            case ScriptBaseClass.LINK_ROOT:
                ret = new List<SceneObjectPart>();
                ret.Add(part.ParentGroup.RootPart);
                return ret;

            case ScriptBaseClass.LINK_ALL_OTHERS:
                ret = new List<SceneObjectPart>(part.ParentGroup.Parts);

                if (ret.Contains(part))
                    ret.Remove(part);

                return ret;

            case ScriptBaseClass.LINK_ALL_CHILDREN:
                ret = new List<SceneObjectPart>(part.ParentGroup.Parts);

                if (ret.Contains(part.ParentGroup.RootPart))
                    ret.Remove(part.ParentGroup.RootPart);
                return ret;

            case ScriptBaseClass.LINK_THIS:
                return ret;

            default:
                if (linkType < 0)
                    return new List<SceneObjectPart>();

                SceneObjectPart target = part.ParentGroup.GetLinkNumPart(linkType);
                if (target == null)
                    return new List<SceneObjectPart>();
                ret = new List<SceneObjectPart>();
                ret.Add(target);
                return ret;
            }
        }

        public List<ISceneEntity> GetLinkEntities(int linkType)
        {
            return GetLinkEntities(m_host, linkType);
        }

        public List<ISceneEntity> GetLinkEntities(SceneObjectPart part, int linkType)
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

                    if (ret.Contains(part))
                        ret.Remove(part);

                    return ret;

                case ScriptBaseClass.LINK_ALL_CHILDREN:
                    ret = new List<ISceneEntity>(part.ParentGroup.Parts);

                    if (ret.Contains(part.ParentGroup.RootPart))
                        ret.Remove(part.ParentGroup.RootPart);

                    return ret;

                case ScriptBaseClass.LINK_THIS:
                    return new List<ISceneEntity>() { part };

                default:
                    if (linkType < 0)
                        return new List<ISceneEntity>();

                    ISceneEntity target = GetLinkEntity(part, linkType);
                    if (target == null)
                        return new List<ISceneEntity>();

                    return new List<ISceneEntity>() { target };
            }
        }

        //These are the implementations of the various ll-functions used by the LSL scripts.
        public LSL_Float llSin(double f)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Sin(f);
        }

        public LSL_Float llCos(double f)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Cos(f);
        }

        public LSL_Float llTan(double f)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Tan(f);
        }

        public LSL_Float llAtan2(double x, double y)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Atan2(x, y);
        }

        public LSL_Float llSqrt(double f)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Sqrt(f);
        }

        public LSL_Float llPow(double fbase, double fexponent)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Pow(fbase, fexponent);
        }

        public LSL_Integer llAbs(int i)
        {
            // changed to replicate LSL behaviour whereby minimum int value is returned untouched.
            m_host.AddScriptLPS(1);
            if (i == Int32.MinValue)
                return i;
            else
                return (int)Math.Abs(i);
        }

        public LSL_Float llFabs(double f)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Abs(f);
        }

        public LSL_Float llFrand(double mag)
        {
            m_host.AddScriptLPS(1);
            lock (Util.RandomClass)
            {
				return Util.RandomClass.NextDouble() * mag;
			}
        }

        public LSL_Integer llFloor(double f)
        {
            m_host.AddScriptLPS(1);
            return (int)Math.Floor(f);
        }

        public LSL_Integer llCeil(double f)
        {
            m_host.AddScriptLPS(1);
            return (int)Math.Ceiling(f);
        }

        // Xantor 01/May/2008 fixed midpointrounding (2.5 becomes 3.0 instead of 2.0, default = ToEven)
        public LSL_Integer llRound(double f)
        {
            m_host.AddScriptLPS(1);
            return (int)Math.Round(f, MidpointRounding.AwayFromZero);
        }

        //This next group are vector operations involving squaring and square root. ckrinke
        public LSL_Float llVecMag(LSL_Vector v)
        {
            m_host.AddScriptLPS(1);
            return LSL_Vector.Mag(v);
        }

        public LSL_Vector llVecNorm(LSL_Vector v)
        {
            m_host.AddScriptLPS(1);
            return LSL_Vector.Norm(v);
        }

        private double VecDist(LSL_Vector a, LSL_Vector b)
        {
            double dx = a.x - b.x;
            double dy = a.y - b.y;
            double dz = a.z - b.z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public LSL_Float llVecDist(LSL_Vector a, LSL_Vector b)
        {
            m_host.AddScriptLPS(1);
            return VecDist(a, b);
        }

        //Now we start getting into quaternions which means sin/cos, matrices and vectors. ckrinke

        // Utility function for llRot2Euler

        public LSL_Vector llRot2Euler(LSL_Rotation q1)
        {
            m_host.AddScriptLPS(1);
            LSL_Vector eul = new LSL_Vector();

            double sqw = q1.s*q1.s;
            double sqx = q1.x*q1.x;
            double sqy = q1.z*q1.z;
            double sqz = q1.y*q1.y;
            double unit = sqx + sqy + sqz + sqw; // if normalised is one, otherwise is correction factor
            double test = q1.x*q1.z + q1.y*q1.s;
            if (test > 0.4999*unit) { // singularity at north pole
                eul.z = 2 * Math.Atan2(q1.x,q1.s);
                eul.y = Math.PI/2;
                eul.x = 0;
                return eul;
            }
            if (test < -0.4999*unit) { // singularity at south pole
                eul.z = -2 * Math.Atan2(q1.x,q1.s);
                eul.y = -Math.PI/2;
                eul.x = 0;
                return eul;
            }
            eul.z = Math.Atan2(2*q1.z*q1.s-2*q1.x*q1.y , sqx - sqy - sqz + sqw);
            eul.y = Math.Asin(2*test/unit);
            eul.x = Math.Atan2(2*q1.x*q1.s-2*q1.z*q1.y , -sqx + sqy - sqz + sqw);
            return eul;
        }

        /* From wiki:
        The Euler angle vector (in radians) is converted to a rotation by doing the rotations around the 3 axes
        in Z, Y, X order. So llEuler2Rot(<1.0, 2.0, 3.0> * DEG_TO_RAD) generates a rotation by taking the zero rotation,
        a vector pointing along the X axis, first rotating it 3 degrees around the global Z axis, then rotating the resulting
        vector 2 degrees around the global Y axis, and finally rotating that 1 degree around the global X axis.
        */

        /* How we arrived at this llEuler2Rot
         *
         * Experiment in SL to determine conventions:
         *   llEuler2Rot(<PI,0,0>)=<1,0,0,0>
         *   llEuler2Rot(<0,PI,0>)=<0,1,0,0>
         *   llEuler2Rot(<0,0,PI>)=<0,0,1,0>
         *
         * Important facts about Quaternions
         *  - multiplication is non-commutative (a*b != b*a)
         *  - http://en.wikipedia.org/wiki/Quaternion#Basis_multiplication
         *
         * Above SL experiment gives (c1,c2,c3,s1,s2,s3 as defined in our llEuler2Rot):
         *   Qx = c1+i*s1
         *   Qy = c2+j*s2;
         *   Qz = c3+k*s3;
         *
         * Rotations applied in order (from above) Z, Y, X
         * Q = (Qz * Qy) * Qx
         * ((c1+i*s1)*(c2+j*s2))*(c3+k*s3)
         * (c1*c2+i*s1*c2+j*c1*s2+ij*s1*s2)*(c3+k*s3)
         * (c1*c2+i*s1*c2+j*c1*s2+k*s1*s2)*(c3+k*s3)
         * c1*c2*c3+i*s1*c2*c3+j*c1*s2*c3+k*s1*s2*c3+k*c1*c2*s3+ik*s1*c2*s3+jk*c1*s2*s3+kk*s1*s2*s3
         * c1*c2*c3+i*s1*c2*c3+j*c1*s2*c3+k*s1*s2*c3+k*c1*c2*s3 -j*s1*c2*s3 +i*c1*s2*s3   -s1*s2*s3
         * regroup: x=i*(s1*c2*c3+c1*s2*s3)
         *          y=j*(c1*s2*c3-s1*c2*s3)
         *          z=k*(s1*s2*c3+c1*c2*s3)
         *          s=   c1*c2*c3-s1*s2*s3
         *
         * This implementation agrees with the functions found here:
         * http://lslwiki.net/lslwiki/wakka.php?wakka=LibraryRotationFunctions
         * And with the results in SL.
         *
         * It's also possible to calculate llEuler2Rot by direct multiplication of
         * the Qz, Qy, and Qx vectors (as above - and done in the "accurate" function
         * from the wiki).
         * Apparently in some cases this is better from a numerical precision perspective?
         */

        public LSL_Rotation llEuler2Rot(LSL_Vector v)
        {
            m_host.AddScriptLPS(1);

            double x,y,z,s;
            v.x *= 0.5;
            v.y *= 0.5;
            v.z *= 0.5;
            double c1 = Math.Cos(v.x);
            double c2 = Math.Cos(v.y);
            double c1c2 = c1 * c2;
            double s1 = Math.Sin(v.x);
            double s2 = Math.Sin(v.y);
            double s1s2 = s1 * s2;
            double c1s2 = c1 * s2;
            double s1c2 = s1 * c2;
            double c3 = Math.Cos(v.z);
            double s3 = Math.Sin(v.z);

            x = s1c2 * c3 + c1s2 * s3;
            y = c1s2 * c3 - s1c2 * s3;
            z = s1s2 * c3 + c1c2 * s3;
            s = c1c2 * c3 - s1s2 * s3;

            return new LSL_Rotation(x, y, z, s);
        }

        public LSL_Rotation llAxes2Rot(LSL_Vector fwd, LSL_Vector left, LSL_Vector up)
        {
            m_host.AddScriptLPS(1);
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
            m_host.AddScriptLPS(1);

            double x, y, z, m;

            m = r.x * r.x + r.y * r.y + r.z * r.z + r.s * r.s;
            // m is always greater than zero
            // if m is not equal to 1 then Rotation needs to be normalized
            if (Math.Abs(1.0 - m) > 0.000001) // allow a little slop here for calculation precision
            {
                m = 1.0 / Math.Sqrt(m);
                r.x *= m;
                r.y *= m;
                r.z *= m;
                r.s *= m;
            }

            // Fast Algebric Calculations instead of Vectors & Quaternions Product
            x = r.x * r.x - r.y * r.y - r.z * r.z + r.s * r.s;
            y = 2 * (r.x * r.y + r.z * r.s);
            z = 2 * (r.x * r.z - r.y * r.s);
            return (new LSL_Vector(x, y, z));
        }

        public LSL_Vector llRot2Left(LSL_Rotation r)
        {
            m_host.AddScriptLPS(1);

            double x, y, z, m;

            m = r.x * r.x + r.y * r.y + r.z * r.z + r.s * r.s;
            // m is always greater than zero
            // if m is not equal to 1 then Rotation needs to be normalized
            if (Math.Abs(1.0 - m) > 0.000001) // allow a little slop here for calculation precision
            {
                m = 1.0 / Math.Sqrt(m);
                r.x *= m;
                r.y *= m;
                r.z *= m;
                r.s *= m;
            }

            // Fast Algebric Calculations instead of Vectors & Quaternions Product
            x = 2 * (r.x * r.y - r.z * r.s);
            y = -r.x * r.x + r.y * r.y - r.z * r.z + r.s * r.s;
            z = 2 * (r.x * r.s + r.y * r.z);
            return (new LSL_Vector(x, y, z));
        }

        public LSL_Vector llRot2Up(LSL_Rotation r)
        {
            m_host.AddScriptLPS(1);
            double x, y, z, m;

            m = r.x * r.x + r.y * r.y + r.z * r.z + r.s * r.s;
            // m is always greater than zero
            // if m is not equal to 1 then Rotation needs to be normalized
            if (Math.Abs(1.0 - m) > 0.000001) // allow a little slop here for calculation precision
            {
                m = 1.0 / Math.Sqrt(m);
                r.x *= m;
                r.y *= m;
                r.z *= m;
                r.s *= m;
            }

            // Fast Algebric Calculations instead of Vectors & Quaternions Product
            x = 2 * (r.x * r.z + r.y * r.s);
            y = 2 * (-r.x * r.s + r.y * r.z);
            z = -r.x * r.x - r.y * r.y + r.z * r.z + r.s * r.s;
            return (new LSL_Vector(x, y, z));
        }

        public LSL_Rotation llRotBetween(LSL_Vector a, LSL_Vector b)
        {
            //A and B should both be normalized
            m_host.AddScriptLPS(1);
            /*  This method is more accurate than the SL one, and thus causes problems
                for scripts that deal with the SL inaccuracy around 180-degrees -.- .._.
                
            double dotProduct = LSL_Vector.Dot(a, b);
            LSL_Vector crossProduct = LSL_Vector.Cross(a, b);
            double magProduct = LSL_Vector.Mag(a) * LSL_Vector.Mag(b);
            double angle = Math.Acos(dotProduct / magProduct);
            LSL_Vector axis = LSL_Vector.Norm(crossProduct);
            double s = Math.Sin(angle / 2);

            double x = axis.x * s;
            double y = axis.y * s;
            double z = axis.z * s;
            double w = Math.Cos(angle / 2);

            if (Double.IsNaN(x) || Double.IsNaN(y) || Double.IsNaN(z) || Double.IsNaN(w))
                return new LSL_Rotation(0.0f, 0.0f, 0.0f, 1.0f);

            return new LSL_Rotation((float)x, (float)y, (float)z, (float)w);
            */
            
            // This method mimics the 180 errors found in SL
            // See www.euclideanspace.com... angleBetween
            LSL_Vector vec_a = a;
            LSL_Vector vec_b = b;
            
            // Eliminate zero length
            LSL_Float vec_a_mag = LSL_Vector.Mag(vec_a);
            LSL_Float vec_b_mag = LSL_Vector.Mag(vec_b);
            if (vec_a_mag < 0.00001 ||
                vec_b_mag < 0.00001)
            {
                return new LSL_Rotation(0.0f, 0.0f, 0.0f, 1.0f);
            }
            
            // Normalize
            vec_a = llVecNorm(vec_a);
            vec_b = llVecNorm(vec_b);

            // Calculate axis and rotation angle
            LSL_Vector axis = vec_a % vec_b;
            LSL_Float cos_theta  = vec_a * vec_b;
    
            // Check if parallel
            if (cos_theta > 0.99999)
            {
                return new LSL_Rotation(0.0f, 0.0f, 0.0f, 1.0f);
            }
            
            // Check if anti-parallel
            else if (cos_theta < -0.99999)
            {
                LSL_Vector orthog_axis = new LSL_Vector(1.0, 0.0, 0.0) - (vec_a.x / (vec_a * vec_a) * vec_a);
                if (LSL_Vector.Mag(orthog_axis)  < 0.000001)  orthog_axis = new LSL_Vector(0.0, 0.0, 1.0);
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
            m_host.AddScriptLPS(1);

            if (text.Length > 1023)
                text = text.Substring(0, 1023);

            World.SimChat(Utils.StringToBytes(text),
                          ChatTypeEnum.Whisper, channelID, m_host.ParentGroup.RootPart.AbsolutePosition, m_host.Name, m_host.UUID, false);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            if (wComm != null)
                wComm.DeliverMessage(ChatTypeEnum.Whisper, channelID, m_host.Name, m_host.UUID, text);
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

        public void llSay(int channelID, string text)
        {
            m_host.AddScriptLPS(1);

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
                if (text.Length > 1023)
                    text = text.Substring(0, 1023);

                World.SimChat(Utils.StringToBytes(text),
                              ChatTypeEnum.Say, channelID, m_host.ParentGroup.RootPart.AbsolutePosition, m_host.Name, m_host.UUID, false);

                IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
                if (wComm != null)
                    wComm.DeliverMessage(ChatTypeEnum.Say, channelID, m_host.Name, m_host.UUID, text);
            }
        }

        public void llShout(int channelID, string text)
        {
            m_host.AddScriptLPS(1);

            if (channelID == 0)
//                m_SayShoutCount++;
                CheckSayShoutTime();

            if (m_SayShoutCount >= 11)
                ScriptSleep(2000);

            if (text.Length > 1023)
                text = text.Substring(0, 1023);

            World.SimChat(Utils.StringToBytes(text),
                          ChatTypeEnum.Shout, channelID, m_host.ParentGroup.RootPart.AbsolutePosition, m_host.Name, m_host.UUID, true);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            if (wComm != null)
                wComm.DeliverMessage(ChatTypeEnum.Shout, channelID, m_host.Name, m_host.UUID, text);
        }

        public void llRegionSay(int channelID, string text)
        {
            if (channelID == 0)
            {
                Error("llRegionSay", "Cannot use on channel 0");
                return;
            }

            if (text.Length > 1023)
                text = text.Substring(0, 1023);

            m_host.AddScriptLPS(1);

            // debug channel is also sent to avatars
            if (channelID == ScriptBaseClass.DEBUG_CHANNEL)
            {
                World.SimChat(Utils.StringToBytes(text),
                    ChatTypeEnum.Shout, channelID, m_host.ParentGroup.RootPart.AbsolutePosition, m_host.Name, m_host.UUID, true);
            
            }

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            if (wComm != null)
                wComm.DeliverMessage(ChatTypeEnum.Region, channelID, m_host.Name, m_host.UUID, text);
        }

        public void  llRegionSayTo(string target, int channel, string msg)
        {
            if (msg.Length > 1023)
                msg = msg.Substring(0, 1023);

            m_host.AddScriptLPS(1);

            if (channel == ScriptBaseClass.DEBUG_CHANNEL)
                return;

            UUID TargetID;
            UUID.TryParse(target, out TargetID);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            if (wComm != null)
                wComm.DeliverMessageTo(TargetID, channel, m_host.AbsolutePosition, m_host.Name, m_host.UUID, msg);
        }

        public LSL_Integer llListen(int channelID, string name, string ID, string msg)
        {
            m_host.AddScriptLPS(1);
            UUID keyID;
            UUID.TryParse(ID, out keyID);
            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            if (wComm != null)
                return wComm.Listen(m_host.LocalId, m_item.ItemID, m_host.UUID, channelID, name, keyID, msg);
            else
                return -1;
        }

        public void llListenControl(int number, int active)
        {
            m_host.AddScriptLPS(1);
            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            if (wComm != null)
                wComm.ListenControl(m_item.ItemID, number, active);
        }

        public void llListenRemove(int number)
        {
            m_host.AddScriptLPS(1);
            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            if (wComm != null)
                wComm.ListenRemove(m_item.ItemID, number);
        }

        public void llSensor(string name, string id, int type, double range, double arc)
        {
            m_host.AddScriptLPS(1);
            UUID keyID = UUID.Zero;
            UUID.TryParse(id, out keyID);

            AsyncCommands.SensorRepeatPlugin.SenseOnce(m_host.LocalId, m_item.ItemID, name, keyID, type, range, arc, m_host);
       }

        public void llSensorRepeat(string name, string id, int type, double range, double arc, double rate)
        {
            m_host.AddScriptLPS(1);
            UUID keyID = UUID.Zero;
            UUID.TryParse(id, out keyID);

            AsyncCommands.SensorRepeatPlugin.SetSenseRepeatEvent(m_host.LocalId, m_item.ItemID, name, keyID, type, range, arc, rate, m_host);
        }

        public void llSensorRemove()
        {
            m_host.AddScriptLPS(1);
            AsyncCommands.SensorRepeatPlugin.UnSetSenseRepeaterEvents(m_host.LocalId, m_item.ItemID);
        }

        public string resolveName(UUID objecUUID)
        {
            // try avatar username surname
            UserAccount account = World.UserAccountService.GetUserAccount(World.RegionInfo.ScopeID, objecUUID);
            if (account != null)
            {
                string avatarname = account.Name;
                return avatarname;
            }
            // try an scene object
            SceneObjectPart SOP = World.GetSceneObjectPart(objecUUID);
            if (SOP != null)
            {
                string objectname = SOP.Name;
                return objectname;
            }

            EntityBase SensedObject;
            World.Entities.TryGetValue(objecUUID, out SensedObject);

            if (SensedObject == null)
            {
                IGroupsModule groups = World.RequestModuleInterface<IGroupsModule>();
                if (groups != null)
                {
                    GroupRecord gr = groups.GetGroupRecord(objecUUID);
                    if (gr != null)
                        return gr.GroupName;
                }
                return String.Empty;
            }

            return SensedObject.Name;
        }

        public LSL_String llDetectedName(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            if (detectedParams == null)
                return String.Empty;
            return detectedParams.Name;
        }

        public LSL_String llDetectedKey(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            if (detectedParams == null)
                return String.Empty;
            return detectedParams.Key.ToString();
        }

        public LSL_String llDetectedOwner(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            if (detectedParams == null)
                return String.Empty;
            return detectedParams.Owner.ToString();
        }

        public LSL_Integer llDetectedType(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            if (detectedParams == null)
                return 0;
            return new LSL_Integer(detectedParams.Type);
        }

        public LSL_Vector llDetectedPos(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            if (detectedParams == null)
                return new LSL_Vector();
            return detectedParams.Position;
        }

        public LSL_Vector llDetectedVel(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            if (detectedParams == null)
                return new LSL_Vector();
            return detectedParams.Velocity;
        }

        public LSL_Vector llDetectedGrab(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams parms = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            if (parms == null)
                return new LSL_Vector(0, 0, 0);

            return parms.OffsetPos;
        }

        public LSL_Rotation llDetectedRot(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            if (detectedParams == null)
                return new LSL_Rotation();
            return detectedParams.Rotation;
        }

        public LSL_Integer llDetectedGroup(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            if (detectedParams == null)
                return new LSL_Integer(0);
            if (m_host.GroupID == detectedParams.Group)
                return new LSL_Integer(1);
            return new LSL_Integer(0);
        }

        public LSL_Integer llDetectedLinkNumber(int number)
        {
            m_host.AddScriptLPS(1);
            DetectParams parms = m_ScriptEngine.GetDetectParams(m_item.ItemID, number);
            if (parms == null)
                return new LSL_Integer(0);

            return new LSL_Integer(parms.LinkNum);
        }

        /// <summary>
        /// See http://wiki.secondlife.com/wiki/LlDetectedTouchBinormal for details
        /// </summary>
        public LSL_Vector llDetectedTouchBinormal(int index)
        {
            m_host.AddScriptLPS(1);
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, index);
            if (detectedParams == null)
                return new LSL_Vector();
            return detectedParams.TouchBinormal;
        }

        /// <summary>
        /// See http://wiki.secondlife.com/wiki/LlDetectedTouchFace for details
        /// </summary>
        public LSL_Integer llDetectedTouchFace(int index)
        {
            m_host.AddScriptLPS(1);
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, index);
            if (detectedParams == null)
                return new LSL_Integer(-1);
            return new LSL_Integer(detectedParams.TouchFace);
        }

        /// <summary>
        /// See http://wiki.secondlife.com/wiki/LlDetectedTouchNormal for details
        /// </summary>
        public LSL_Vector llDetectedTouchNormal(int index)
        {
            m_host.AddScriptLPS(1);
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, index);
            if (detectedParams == null)
                return new LSL_Vector();
            return detectedParams.TouchNormal;
        }

        /// <summary>
        /// See http://wiki.secondlife.com/wiki/LlDetectedTouchPos for details
        /// </summary>
        public LSL_Vector llDetectedTouchPos(int index)
        {
            m_host.AddScriptLPS(1);
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, index);
            if (detectedParams == null)
                return new LSL_Vector();
            return detectedParams.TouchPos;
        }

        /// <summary>
        /// See http://wiki.secondlife.com/wiki/LlDetectedTouchST for details
        /// </summary>
        public LSL_Vector llDetectedTouchST(int index)
        {
            m_host.AddScriptLPS(1);
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, index);
            if (detectedParams == null)
                return new LSL_Vector(-1.0, -1.0, 0.0);
            return detectedParams.TouchST;
        }

        /// <summary>
        /// See http://wiki.secondlife.com/wiki/LlDetectedTouchUV for details
        /// </summary>
        public LSL_Vector llDetectedTouchUV(int index)
        {
            m_host.AddScriptLPS(1);
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, index);
            if (detectedParams == null)
                return new LSL_Vector(-1.0, -1.0, 0.0);
            return detectedParams.TouchUV;
        }

        [DebuggerNonUserCode]
        public virtual void llDie()
        {
            m_host.AddScriptLPS(1);
            if (!m_host.ParentGroup.IsAttachment) throw new SelfDeleteException();
        }

        public LSL_Float llGround(LSL_Vector offset)
        {
            m_host.AddScriptLPS(1);
            Vector3 pos = m_host.GetWorldPosition() + (Vector3)offset;

            //Get the slope normal.  This gives us the equation of the plane tangent to the slope.
            LSL_Vector vsn = llGroundNormal(offset);

            // Clamp to valid position
            if (pos.X < 0)
                pos.X = 0;
            else if (pos.X >= World.Heightmap.Width)
                pos.X = World.Heightmap.Width - 1;
            if (pos.Y < 0)
                pos.Y = 0;
            else if (pos.Y >= World.Heightmap.Height)
                pos.Y = World.Heightmap.Height - 1;

            //Get the height for the integer coordinates from the Heightmap
            float baseheight = (float)World.Heightmap[(int)pos.X, (int)pos.Y];

            //Calculate the difference between the actual coordinates and the integer coordinates
            float xdiff = pos.X - (float)((int)pos.X);
            float ydiff = pos.Y - (float)((int)pos.Y);

            //Use the equation of the tangent plane to adjust the height to account for slope

            return (((vsn.x * xdiff) + (vsn.y * ydiff)) / (-1 * vsn.z)) + baseheight;
        }

        public LSL_Float llCloud(LSL_Vector offset)
        {
            m_host.AddScriptLPS(1);
            float cloudCover = 0f;
            ICloudModule module = World.RequestModuleInterface<ICloudModule>();
            if (module != null)
            {
                Vector3 pos = m_host.GetWorldPosition();
                int x = (int)(pos.X + offset.x);
                int y = (int)(pos.Y + offset.y);

                cloudCover = module.CloudCover(x, y, 0);

            }
            return cloudCover;
        }

        public LSL_Vector llWind(LSL_Vector offset)
        {
            m_host.AddScriptLPS(1);
            LSL_Vector wind = new LSL_Vector(0, 0, 0);
            IWindModule module = World.RequestModuleInterface<IWindModule>();
            if (module != null)
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
            if (m_host == null || m_host.ParentGroup == null || m_host.ParentGroup.IsDeleted)
                return;
            m_host.AddScriptLPS(1);

            int statusrotationaxis = 0;

            if ((status & ScriptBaseClass.STATUS_PHYSICS) == ScriptBaseClass.STATUS_PHYSICS)
            {
                if (value != 0)
                {
                    SceneObjectGroup group = m_host.ParentGroup;
                    bool allow = true;

                    int maxprims = World.m_linksetPhysCapacity;
                    bool checkShape = (maxprims > 0 && group.PrimCount > maxprims);

                    foreach (SceneObjectPart part in group.Parts)
                    {
                        if (part.Scale.X > World.m_maxPhys || part.Scale.Y > World.m_maxPhys || part.Scale.Z > World.m_maxPhys)
                        {
                            allow = false;
                            break;
                        }
                        if (checkShape && part.PhysicsShapeType != (byte)PhysicsShapeType.None)
                        {
                            if (--maxprims < 0)
                            {
                                allow = false;
                                break;
                            }
                        }
                    }

                    if (!allow)
                        return;

                    if (m_host.ParentGroup.RootPart.PhysActor != null &&
                        m_host.ParentGroup.RootPart.PhysActor.IsPhysical)
                        return;

                    m_host.ScriptSetPhysicsStatus(true);
                }
                else
                {
                    m_host.ScriptSetPhysicsStatus(false);
                }
            }

            if ((status & ScriptBaseClass.STATUS_PHANTOM) == ScriptBaseClass.STATUS_PHANTOM)
            {
                m_host.ParentGroup.ScriptSetPhantomStatus(value != 0);
            }

            if ((status & ScriptBaseClass.STATUS_CAST_SHADOWS) == ScriptBaseClass.STATUS_CAST_SHADOWS)
            {
                m_host.AddFlag(PrimFlags.CastShadows);
            }

            if ((status & ScriptBaseClass.STATUS_ROTATE_X) == ScriptBaseClass.STATUS_ROTATE_X)
            {
                statusrotationaxis |= ScriptBaseClass.STATUS_ROTATE_X;
            }

            if ((status & ScriptBaseClass.STATUS_ROTATE_Y) == ScriptBaseClass.STATUS_ROTATE_Y)
            {
                statusrotationaxis |= ScriptBaseClass.STATUS_ROTATE_Y;
            }

            if ((status & ScriptBaseClass.STATUS_ROTATE_Z) == ScriptBaseClass.STATUS_ROTATE_Z)
            {
                statusrotationaxis |= ScriptBaseClass.STATUS_ROTATE_Z;
            }

            if ((status & ScriptBaseClass.STATUS_BLOCK_GRAB) == ScriptBaseClass.STATUS_BLOCK_GRAB)
                m_host.BlockGrab = value != 0;

            if ((status & ScriptBaseClass.STATUS_BLOCK_GRAB_OBJECT) == ScriptBaseClass.STATUS_BLOCK_GRAB_OBJECT)
                m_host.ParentGroup.BlockGrabOverride = value != 0;

            if ((status & ScriptBaseClass.STATUS_DIE_AT_EDGE) == ScriptBaseClass.STATUS_DIE_AT_EDGE)
            {
                if (value != 0)
                    m_host.SetDieAtEdge(true);
                else
                    m_host.SetDieAtEdge(false);
            }

            if ((status & ScriptBaseClass.STATUS_RETURN_AT_EDGE) == ScriptBaseClass.STATUS_RETURN_AT_EDGE)
            {
                if (value != 0)
                    m_host.SetReturnAtEdge(true);
                else
                    m_host.SetReturnAtEdge(false);
            }

            if ((status & ScriptBaseClass.STATUS_SANDBOX) == ScriptBaseClass.STATUS_SANDBOX)
            {
                if (value != 0)
                    m_host.SetStatusSandbox(true);
                else
                    m_host.SetStatusSandbox(false);
            }

            if (statusrotationaxis != 0)
            {
                m_host.SetAxisRotation(statusrotationaxis, value);
            }
        }

        private bool IsPhysical()
        {
            return ((m_host.GetEffectiveObjectFlags() & (uint)PrimFlags.Physics) == (uint)PrimFlags.Physics);
        }

        public LSL_Integer llGetStatus(int status)
        {
            m_host.AddScriptLPS(1);
            // m_log.Debug(m_host.ToString() + " status is " + m_host.GetEffectiveObjectFlags().ToString());
            switch (status)
            {
                case ScriptBaseClass.STATUS_PHYSICS:
                    return IsPhysical() ? 1 : 0;

                case ScriptBaseClass.STATUS_PHANTOM:
                    if ((m_host.GetEffectiveObjectFlags() & (uint)PrimFlags.Phantom) == (uint)PrimFlags.Phantom)
                    {
                        return 1;
                    }
                    return 0;

                case ScriptBaseClass.STATUS_CAST_SHADOWS:
                    if ((m_host.GetEffectiveObjectFlags() & (uint)PrimFlags.CastShadows) == (uint)PrimFlags.CastShadows)
                    {
                        return 1;
                    }
                    return 0;

                case ScriptBaseClass.STATUS_BLOCK_GRAB:
                    return m_host.BlockGrab ? 1 : 0;

                case ScriptBaseClass.STATUS_BLOCK_GRAB_OBJECT:
                    return m_host.ParentGroup.BlockGrabOverride ? 1 : 0;

                case ScriptBaseClass.STATUS_DIE_AT_EDGE:
                    if (m_host.GetDieAtEdge())
                        return 1;
                    else
                        return 0;

                case ScriptBaseClass.STATUS_RETURN_AT_EDGE:
                    if (m_host.GetReturnAtEdge())
                        return 1;
                    else
                        return 0;

                case ScriptBaseClass.STATUS_ROTATE_X:
                    // if (m_host.GetAxisRotation(2) != 0)
                    if (m_host.GetAxisRotation((int)SceneObjectGroup.axisSelect.STATUS_ROTATE_X) != 0)
                        return 1;
                    else
                        return 0;

                case ScriptBaseClass.STATUS_ROTATE_Y:
                    if (m_host.GetAxisRotation((int)SceneObjectGroup.axisSelect.STATUS_ROTATE_Y) != 0)
                        return 1;
                    else
                        return 0;

                case ScriptBaseClass.STATUS_ROTATE_Z:
                    if (m_host.GetAxisRotation((int)SceneObjectGroup.axisSelect.STATUS_ROTATE_Z) != 0)
                        return 1;
                    else
                        return 0;

                case ScriptBaseClass.STATUS_SANDBOX:
                    if (m_host.GetStatusSandbox())
                        return 1;
                    else
                        return 0;
            }
            return 0;
        }

        public void llSetScale(LSL_Vector scale)
        {
            m_host.AddScriptLPS(1);
            SetScale(m_host, scale);
        }

        protected void SetScale(SceneObjectPart part, LSL_Vector scale)
        {
            // TODO: this needs to trigger a persistance save as well
            if (part == null || part.ParentGroup.IsDeleted)
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
            part.SendFullUpdateToAllClients();
        }

        public LSL_Vector llGetScale()
        {
            m_host.AddScriptLPS(1);
            return new LSL_Vector(m_host.Scale.X, m_host.Scale.Y, m_host.Scale.Z);
        }

        public void llSetClickAction(int action)
        {
            m_host.AddScriptLPS(1);
            m_host.ClickAction = (byte)action;
            m_host.ParentGroup.HasGroupChanged = true;
            m_host.ScheduleFullUpdate();
            return;
        }

        public void llSetColor(LSL_Vector color, int face)
        {
            m_host.AddScriptLPS(1);

            SetColor(m_host, color, face);
        }

        protected void SetColor(SceneObjectPart part, LSL_Vector color, int face)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            Primitive.TextureEntry tex = part.Shape.Textures;
            Color4 texcolor;
            if (face >= 0 && face < GetNumberOfSides(part))
            {
                texcolor = tex.CreateFace((uint)face).RGBA;
                texcolor.R = Util.Clip((float)color.x, 0.0f, 1.0f);
                texcolor.G = Util.Clip((float)color.y, 0.0f, 1.0f);
                texcolor.B = Util.Clip((float)color.z, 0.0f, 1.0f);
                tex.FaceTextures[face].RGBA = texcolor;
                part.UpdateTextureEntry(tex.GetBytes());
                return;
            }
            else if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (uint i = 0; i < GetNumberOfSides(part); i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        texcolor = tex.FaceTextures[i].RGBA;
                        texcolor.R = Util.Clip((float)color.x, 0.0f, 1.0f);
                        texcolor.G = Util.Clip((float)color.y, 0.0f, 1.0f);
                        texcolor.B = Util.Clip((float)color.z, 0.0f, 1.0f);
                        tex.FaceTextures[i].RGBA = texcolor;
                    }
                    texcolor = tex.DefaultTexture.RGBA;
                    texcolor.R = Util.Clip((float)color.x, 0.0f, 1.0f);
                    texcolor.G = Util.Clip((float)color.y, 0.0f, 1.0f);
                    texcolor.B = Util.Clip((float)color.z, 0.0f, 1.0f);
                    tex.DefaultTexture.RGBA = texcolor;
                }
                part.UpdateTextureEntry(tex.GetBytes());
                return;
            }

            if (face == ScriptBaseClass.ALL_SIDES)
                face = SceneObjectPart.ALL_SIDES;

            m_host.SetFaceColorAlpha(face, color, null);
        }

        public void llSetContentType(LSL_Key id, LSL_Integer type)
        {
            m_host.AddScriptLPS(1);

            if (m_UrlModule == null)
                return;

            // Make sure the content type is text/plain to start with
            m_UrlModule.HttpContentType(new UUID(id), "text/plain");

            // Is the object owner online and in the region
            ScenePresence agent = World.GetScenePresence(m_host.ParentGroup.OwnerID);
            if (agent == null || agent.IsChildAgent)
                return;  // Fail if the owner is not in the same region

            // Is it the embeded browser?
            string userAgent = m_UrlModule.GetHttpHeader(new UUID(id), "user-agent");
            if (userAgent.IndexOf("SecondLife") < 0)
                return; // Not the embedded browser. Is this check good enough?
				
            // Use the IP address of the client and check against the request
            // seperate logins from the same IP will allow all of them to get non-text/plain as long
            // as the owner is in the region. Same as SL!
            string logonFromIPAddress = agent.ControllingClient.RemoteEndPoint.Address.ToString();
            string requestFromIPAddress = m_UrlModule.GetHttpHeader(new UUID(id), "remote_addr");
            //m_log.Debug("IP from header='" + requestFromIPAddress + "' IP from endpoint='" + logonFromIPAddress + "'");
            if (requestFromIPAddress == null || requestFromIPAddress.Trim() == "")
                return;
            if (logonFromIPAddress == null || logonFromIPAddress.Trim() == "")
                return;

            // If the request isnt from the same IP address then the request cannot be from the owner
            if (!requestFromIPAddress.Trim().Equals(logonFromIPAddress.Trim()))
                return;

            switch (type)
            {
                case ScriptBaseClass.CONTENT_TYPE_HTML:
                    m_UrlModule.HttpContentType(new UUID(id), "text/html");
                    break;
                case ScriptBaseClass.CONTENT_TYPE_XML:
                    m_UrlModule.HttpContentType(new UUID(id), "application/xml");
                    break;
                case ScriptBaseClass.CONTENT_TYPE_XHTML:
                    m_UrlModule.HttpContentType(new UUID(id), "application/xhtml+xml");
                    break;
                case ScriptBaseClass.CONTENT_TYPE_ATOM:
                    m_UrlModule.HttpContentType(new UUID(id), "application/atom+xml");
                    break;
                case ScriptBaseClass.CONTENT_TYPE_JSON:
                    m_UrlModule.HttpContentType(new UUID(id), "application/json");
                    break;
                case ScriptBaseClass.CONTENT_TYPE_LLSD:
                    m_UrlModule.HttpContentType(new UUID(id), "application/llsd+xml");
                    break;
                case ScriptBaseClass.CONTENT_TYPE_FORM:
                    m_UrlModule.HttpContentType(new UUID(id), "application/x-www-form-urlencoded");
                    break;
                case ScriptBaseClass.CONTENT_TYPE_RSS:
                    m_UrlModule.HttpContentType(new UUID(id), "application/rss+xml");
                    break;
                default:
                    m_UrlModule.HttpContentType(new UUID(id), "text/plain");
                    break;
            }
        }

/*
        public void llSetContentType(LSL_Key id, LSL_Integer content_type)
        {
            if (m_UrlModule != null)
            {
                string type = "text.plain";
                if (content_type == (int)ScriptBaseClass.CONTENT_TYPE_HTML)
                    type = "text/html";

                m_UrlModule.HttpContentType(new UUID(id),type);
            }
        }
*/		
        public void SetTexGen(SceneObjectPart part, int face,int style)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            Primitive.TextureEntry tex = part.Shape.Textures;
            MappingType textype;
            textype = MappingType.Default;
            if (style == (int)ScriptBaseClass.PRIM_TEXGEN_PLANAR)
                textype = MappingType.Planar;

            if (face >= 0 && face < GetNumberOfSides(part))
            {
                tex.CreateFace((uint) face);
                tex.FaceTextures[face].TexMapType = textype;
                part.UpdateTextureEntry(tex.GetBytes());
                return;
            }
            else if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (uint i = 0; i < GetNumberOfSides(part); i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        tex.FaceTextures[i].TexMapType = textype;
                    }
                    tex.DefaultTexture.TexMapType = textype;
                }
                part.UpdateTextureEntry(tex.GetBytes());
                return;
            }
        }

        public void SetGlow(SceneObjectPart part, int face, float glow)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face >= 0 && face < GetNumberOfSides(part))
            {
                tex.CreateFace((uint) face);
                tex.FaceTextures[face].Glow = glow;
                part.UpdateTextureEntry(tex.GetBytes());
                return;
            }
            else if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (uint i = 0; i < GetNumberOfSides(part); i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        tex.FaceTextures[i].Glow = glow;
                    }
                    tex.DefaultTexture.Glow = glow;
                }
                part.UpdateTextureEntry(tex.GetBytes());
                return;
            }
        }

        public void SetShiny(SceneObjectPart part, int face, int shiny, Bumpiness bump)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            Shininess sval = new Shininess();

            switch (shiny)
            {
            case 0:
                sval = Shininess.None;
                break;
            case 1:
                sval = Shininess.Low;
                break;
            case 2:
                sval = Shininess.Medium;
                break;
            case 3:
                sval = Shininess.High;
                break;
            default:
                sval = Shininess.None;
                break;
            }

            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face >= 0 && face < GetNumberOfSides(part))
            {
                tex.CreateFace((uint) face);
                tex.FaceTextures[face].Shiny = sval;
                tex.FaceTextures[face].Bump = bump;
                part.UpdateTextureEntry(tex.GetBytes());
                return;
            }
            else if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (uint i = 0; i < GetNumberOfSides(part); i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        tex.FaceTextures[i].Shiny = sval;
                        tex.FaceTextures[i].Bump = bump;
                    }
                    tex.DefaultTexture.Shiny = sval;
                    tex.DefaultTexture.Bump = bump;
                }
                part.UpdateTextureEntry(tex.GetBytes());
                return;
            }
        }

        public void SetFullBright(SceneObjectPart part, int face, bool bright)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

             Primitive.TextureEntry tex = part.Shape.Textures;
             if (face >= 0 && face < GetNumberOfSides(part))
             {
                 tex.CreateFace((uint) face);
                 tex.FaceTextures[face].Fullbright = bright;
                 part.UpdateTextureEntry(tex.GetBytes());
                 return;
             }
             else if (face == ScriptBaseClass.ALL_SIDES)
             {
                 for (uint i = 0; i < GetNumberOfSides(part); i++)
                 {
                     if (tex.FaceTextures[i] != null)
                     {
                         tex.FaceTextures[i].Fullbright = bright;
                     }
                 }
                 tex.DefaultTexture.Fullbright = bright;
                 part.UpdateTextureEntry(tex.GetBytes());
                 return;
             }
         }

        public LSL_Float llGetAlpha(int face)
        {
            m_host.AddScriptLPS(1);

            return GetAlpha(m_host, face);
        }

        protected LSL_Float GetAlpha(SceneObjectPart part, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face == ScriptBaseClass.ALL_SIDES)
            {
                int i;
                double sum = 0.0;
                for (i = 0 ; i < GetNumberOfSides(part); i++)
                    sum += (double)tex.GetFace((uint)i).RGBA.A;
                return sum;
            }
            if (face >= 0 && face < GetNumberOfSides(part))
            {
                return (double)tex.GetFace((uint)face).RGBA.A;
            }
            return 0.0;
        }

        public void llSetAlpha(double alpha, int face)
        {
            m_host.AddScriptLPS(1);

            SetAlpha(m_host, alpha, face);
        }

        public void llSetLinkAlpha(int linknumber, double alpha, int face)
        {
            m_host.AddScriptLPS(1);

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

        protected void SetAlpha(SceneObjectPart part, double alpha, int face)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            Primitive.TextureEntry tex = part.Shape.Textures;
            Color4 texcolor;
            if (face >= 0 && face < GetNumberOfSides(part))
            {
                texcolor = tex.CreateFace((uint)face).RGBA;
                texcolor.A = Util.Clip((float)alpha, 0.0f, 1.0f);
                tex.FaceTextures[face].RGBA = texcolor;
                part.UpdateTextureEntry(tex.GetBytes());
                return;
            }
            else if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (int i = 0; i < GetNumberOfSides(part); i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        texcolor = tex.FaceTextures[i].RGBA;
                        texcolor.A = Util.Clip((float)alpha, 0.0f, 1.0f);
                        tex.FaceTextures[i].RGBA = texcolor;
                    }
                }

                // In some cases, the default texture can be null, eg when every face
                // has a unique texture
                if (tex.DefaultTexture != null)
                {
                    texcolor = tex.DefaultTexture.RGBA;
                    texcolor.A = Util.Clip((float)alpha, 0.0f, 1.0f);
                    tex.DefaultTexture.RGBA = texcolor;
                }

                part.UpdateTextureEntry(tex.GetBytes());
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
        protected void SetFlexi(SceneObjectPart part, bool flexi, int softness, float gravity, float friction,
            float wind, float tension, LSL_Vector Force)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            if (flexi)
            {
                part.Shape.FlexiEntry = true;   // this setting flexi true isn't working, but the below parameters do
                                                                // work once the prim is already flexi
                part.Shape.FlexiSoftness = softness;
                part.Shape.FlexiGravity = gravity;
                part.Shape.FlexiDrag = friction;
                part.Shape.FlexiWind = wind;
                part.Shape.FlexiTension = tension;
                part.Shape.FlexiForceX = (float)Force.x;
                part.Shape.FlexiForceY = (float)Force.y;
                part.Shape.FlexiForceZ = (float)Force.z;
                part.Shape.PathCurve = (byte)Extrusion.Flexible;
            }
            else
            {
                // Other values not set, they do not seem to be sent to the viewer
                // Setting PathCurve appears to be what actually toggles the check box and turns Flexi on and off
                part.Shape.PathCurve = (byte)Extrusion.Straight;
                part.Shape.FlexiEntry = false;
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
        protected void SetPointLight(SceneObjectPart part, bool light, LSL_Vector color, float intensity, float radius, float falloff)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            if (light)
            {
                part.Shape.LightEntry = true;
                part.Shape.LightColorR = Util.Clip((float)color.x, 0.0f, 1.0f);
                part.Shape.LightColorG = Util.Clip((float)color.y, 0.0f, 1.0f);
                part.Shape.LightColorB = Util.Clip((float)color.z, 0.0f, 1.0f);
                part.Shape.LightIntensity = Util.Clip((float)intensity, 0.0f, 1.0f);
                part.Shape.LightRadius = Util.Clip((float)radius, 0.1f, 20.0f);
                part.Shape.LightFalloff = Util.Clip((float)falloff, 0.01f, 2.0f);
            }
            else
            {
                part.Shape.LightEntry = false;
            }

            part.ParentGroup.HasGroupChanged = true;
            part.ScheduleFullUpdate();
        }

        public LSL_Vector llGetColor(int face)
        {
            m_host.AddScriptLPS(1);
            return GetColor(m_host, face);
        }

        protected LSL_Vector GetColor(SceneObjectPart part, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            Color4 texcolor;
            LSL_Vector rgb = new LSL_Vector();
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
            else
            {
                return new LSL_Vector();
            }
        }

        public void llSetTexture(string texture, int face)
        {
            m_host.AddScriptLPS(1);
            SetTexture(m_host, texture, face);
            ScriptSleep(m_sleepMsOnSetTexture);
        }

        public void llSetLinkTexture(int linknumber, string texture, int face)
        {
            m_host.AddScriptLPS(1);

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

        protected void SetTexture(SceneObjectPart part, string texture, int face)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            UUID textureID = new UUID();

            textureID = ScriptUtils.GetAssetIdFromItemName(m_host, texture, (int)AssetType.Texture);
            if (textureID == UUID.Zero)
            {
                if (!UUID.TryParse(texture, out textureID))
                    return;
            }

            Primitive.TextureEntry tex = part.Shape.Textures;

            if (face >= 0 && face < GetNumberOfSides(part))
            {
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)face);
                texface.TextureID = textureID;
                tex.FaceTextures[face] = texface;
                part.UpdateTextureEntry(tex.GetBytes());
                return;
            }
            else if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (uint i = 0; i < GetNumberOfSides(part); i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        tex.FaceTextures[i].TextureID = textureID;
                    }
                }
                tex.DefaultTexture.TextureID = textureID;
                part.UpdateTextureEntry(tex.GetBytes());
                return;
            }
        }

        public void llScaleTexture(double u, double v, int face)
        {
            m_host.AddScriptLPS(1);

            ScaleTexture(m_host, u, v, face);
            ScriptSleep(m_sleepMsOnScaleTexture);
        }

        protected void ScaleTexture(SceneObjectPart part, double u, double v, int face)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face >= 0 && face < GetNumberOfSides(part))
            {
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)face);
                texface.RepeatU = (float)u;
                texface.RepeatV = (float)v;
                tex.FaceTextures[face] = texface;
                part.UpdateTextureEntry(tex.GetBytes());
                return;
            }
            if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (int i = 0; i < GetNumberOfSides(part); i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        tex.FaceTextures[i].RepeatU = (float)u;
                        tex.FaceTextures[i].RepeatV = (float)v;
                    }
                }
                tex.DefaultTexture.RepeatU = (float)u;
                tex.DefaultTexture.RepeatV = (float)v;
                part.UpdateTextureEntry(tex.GetBytes());
                return;
            }
        }

        public void llOffsetTexture(double u, double v, int face)
        {
            m_host.AddScriptLPS(1);
            OffsetTexture(m_host, u, v, face);
            ScriptSleep(m_sleepMsOnOffsetTexture);
        }

        protected void OffsetTexture(SceneObjectPart part, double u, double v, int face)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face >= 0 && face < GetNumberOfSides(part))
            {
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)face);
                texface.OffsetU = (float)u;
                texface.OffsetV = (float)v;
                tex.FaceTextures[face] = texface;
                part.UpdateTextureEntry(tex.GetBytes());
                return;
            }
            if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (int i = 0; i < GetNumberOfSides(part); i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        tex.FaceTextures[i].OffsetU = (float)u;
                        tex.FaceTextures[i].OffsetV = (float)v;
                    }
                }
                tex.DefaultTexture.OffsetU = (float)u;
                tex.DefaultTexture.OffsetV = (float)v;
                part.UpdateTextureEntry(tex.GetBytes());
                return;
            }
        }

        public void llRotateTexture(double rotation, int face)
        {
            m_host.AddScriptLPS(1);
            RotateTexture(m_host, rotation, face);
            ScriptSleep(m_sleepMsOnRotateTexture);
        }

        protected void RotateTexture(SceneObjectPart part, double rotation, int face)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face >= 0 && face < GetNumberOfSides(part))
            {
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)face);
                texface.Rotation = (float)rotation;
                tex.FaceTextures[face] = texface;
                part.UpdateTextureEntry(tex.GetBytes());
                return;
            }
            if (face == ScriptBaseClass.ALL_SIDES)
            {
                for (int i = 0; i < GetNumberOfSides(part); i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        tex.FaceTextures[i].Rotation = (float)rotation;
                    }
                }
                tex.DefaultTexture.Rotation = (float)rotation;
                part.UpdateTextureEntry(tex.GetBytes());
                return;
            }
        }

        public LSL_String llGetTexture(int face)
        {
            m_host.AddScriptLPS(1);
            return GetTexture(m_host, face);
        }

        protected LSL_String GetTexture(SceneObjectPart part, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face == ScriptBaseClass.ALL_SIDES)
            {
                face = 0;
            }

            if (face >= 0 && face < GetNumberOfSides(part))
            {
                Primitive.TextureEntryFace texface;
                texface = tex.GetFace((uint)face);
                string texture = texface.TextureID.ToString();

                lock (part.TaskInventory)
                {
                    foreach (KeyValuePair<UUID, TaskInventoryItem> inv in part.TaskInventory)
                    {
                        if (inv.Value.AssetID == texface.TextureID)
                        {
                            texture = inv.Value.Name.ToString();
                            break;
                        }
                    }
                }

                return texture;
            }
            else
            {
                return UUID.Zero.ToString();
            }
        }

        public void llSetPos(LSL_Vector pos)
        {
            m_host.AddScriptLPS(1);

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
            m_host.AddScriptLPS(1);

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

            bool sameParcel = here.GlobalID == there.GlobalID;

            if (!sameParcel && !World.Permissions.CanRezObject(
                m_host.ParentGroup.PrimCount, m_host.ParentGroup.OwnerID, pos))
            {
                return 0;
            }

            SetPos(m_host.ParentGroup.RootPart, pos, false);

            return VecDist(pos, llGetRootPosition()) <= 0.1 ? 1 : 0;
        }

        // Capped movemment if distance > 10m (http://wiki.secondlife.com/wiki/LlSetPos)
        // note linked setpos is capped "differently"
        private LSL_Vector SetPosAdjust(LSL_Vector start, LSL_Vector end)
        {
            if (llVecDist(start, end) > 10.0f * m_ScriptDistanceFactor)
                return start + m_ScriptDistanceFactor * 10.0f * llVecNorm(end - start);
            else
                return end;
        }

        protected LSL_Vector GetSetPosTarget(SceneObjectPart part, LSL_Vector targetPos, LSL_Vector fromPos, bool adjust)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return fromPos;

            // Capped movemment if distance > 10m (http://wiki.secondlife.com/wiki/LlSetPos)


            float ground = World.GetGroundHeight((float)targetPos.x, (float)targetPos.y);
            bool disable_underground_movement = m_ScriptEngine.Config.GetBoolean("DisableUndergroundMovement", true);

            if (part.ParentGroup.RootPart == part)
            {
                if ((targetPos.z < ground) && disable_underground_movement && m_host.ParentGroup.AttachmentPoint == 0)
                    targetPos.z = ground;
            }
            if (adjust)
                return SetPosAdjust(fromPos, targetPos);

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
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            LSL_Vector currentPos = GetPartLocalPos(part);
            LSL_Vector toPos = GetSetPosTarget(part, targetPos, currentPos, adjust);


            if (part.ParentGroup.RootPart == part)
            {
                SceneObjectGroup parent = part.ParentGroup;
                if (!World.Permissions.CanObjectEntry(parent.UUID, false, (Vector3)toPos))
                    return;
                parent.UpdateGroupPosition((Vector3)toPos);
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
            m_host.AddScriptLPS(1);
            return m_host.GetWorldPosition();
        }

        public LSL_Vector llGetLocalPos()
        {
            m_host.AddScriptLPS(1);
            return GetPartLocalPos(m_host);
        }

        protected LSL_Vector GetPartLocalPos(SceneObjectPart part)
        {
            m_host.AddScriptLPS(1);

            Vector3 pos;

            if (!part.IsRoot)
            {
                pos = part.OffsetPosition;
            }
            else
            {
                if (part.ParentGroup.IsAttachment)
                    pos = part.AttachedPos;
                else
                    pos = part.AbsolutePosition;
            }

//            m_log.DebugFormat("[LSL API]: Returning {0} in GetPartLocalPos()", pos);

            return new LSL_Vector(pos);
        }

        public void llSetRot(LSL_Rotation rot)
        {
            m_host.AddScriptLPS(1);
          
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
            m_host.AddScriptLPS(1);
            SetRot(m_host, rot);
            ScriptSleep(m_sleepMsOnSetLocalRot);
        }

        protected void SetRot(SceneObjectPart part, Quaternion rot)
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

            m_host.AddScriptLPS(1);
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

        private LSL_Rotation GetPartLocalRot(SceneObjectPart part)
        {
            m_host.AddScriptLPS(1);
            Quaternion rot = part.RotationOffset;
            return new LSL_Rotation(rot.X, rot.Y, rot.Z, rot.W);
        }

        public void llSetForce(LSL_Vector force, int local)
        {
            m_host.AddScriptLPS(1);

            if (!m_host.ParentGroup.IsDeleted)
            {
                if (local != 0)
                    force *= llGetRot();

                m_host.ParentGroup.RootPart.SetForce(force);
            }
        }

        public LSL_Vector llGetForce()
        {
            LSL_Vector force = new LSL_Vector(0.0, 0.0, 0.0);

            m_host.AddScriptLPS(1);

            if (!m_host.ParentGroup.IsDeleted)
            {
                force = m_host.ParentGroup.RootPart.GetForce();
            }

            return force;
        }

        public void llSetVelocity(LSL_Vector vel, int local)
        {
            m_host.AddScriptLPS(1);
            m_host.SetVelocity(new Vector3((float)vel.x, (float)vel.y, (float)vel.z), local != 0);
        }
		
        public void llSetAngularVelocity(LSL_Vector avel, int local)
        {
            m_host.AddScriptLPS(1);
            m_host.SetAngularVelocity(new Vector3((float)avel.x, (float)avel.y, (float)avel.z), local != 0);
        }
        public LSL_Integer llTarget(LSL_Vector position, double range)
        {
            m_host.AddScriptLPS(1);
            return m_host.ParentGroup.registerTargetWaypoint(position,
                (float)range);
        }

        public void llTargetRemove(int number)
        {
            m_host.AddScriptLPS(1);
            m_host.ParentGroup.unregisterTargetWaypoint(number);
        }

        public LSL_Integer llRotTarget(LSL_Rotation rot, double error)
        {
            m_host.AddScriptLPS(1);
            return m_host.ParentGroup.registerRotTargetWaypoint(rot, (float)error);
        }

        public void llRotTargetRemove(int number)
        {
            m_host.AddScriptLPS(1);
            m_host.ParentGroup.unregisterRotTargetWaypoint(number);
        }

        public void llMoveToTarget(LSL_Vector target, double tau)
        {
            m_host.AddScriptLPS(1);
            m_host.MoveToTarget(target, (float)tau);
        }

        public void llStopMoveToTarget()
        {
            m_host.AddScriptLPS(1);
            m_host.StopMoveToTarget();
        }

        public void llApplyImpulse(LSL_Vector force, int local)
        {
            m_host.AddScriptLPS(1);
            //No energy force yet
            Vector3 v = force;
            if (v.Length() > 20000.0f)
            {
                v.Normalize();
                v = v * 20000.0f;
            }
            m_host.ApplyImpulse(v, local != 0);
        }


        public void llApplyRotationalImpulse(LSL_Vector force, int local)
        {
            m_host.AddScriptLPS(1);
            m_host.ParentGroup.RootPart.ApplyAngularImpulse(force, local != 0);
        }

        public void llSetTorque(LSL_Vector torque, int local)
        {
            m_host.AddScriptLPS(1);
            m_host.ParentGroup.RootPart.SetAngularImpulse(torque, local != 0);
        }

        public LSL_Vector llGetTorque()
        {
            m_host.AddScriptLPS(1);

            return new LSL_Vector(m_host.ParentGroup.GetTorque());
        }

        public void llSetForceAndTorque(LSL_Vector force, LSL_Vector torque, int local)
        {
            m_host.AddScriptLPS(1);
            llSetForce(force, local);
            llSetTorque(torque, local);
        }


        public LSL_Vector llGetVel()
        {
            m_host.AddScriptLPS(1);

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
            m_host.AddScriptLPS(1);

            return new LSL_Vector(m_host.Acceleration);
        }

        public LSL_Vector llGetOmega()
        {
            m_host.AddScriptLPS(1);
            Vector3 avel = m_host.AngularVelocity;
            return new LSL_Vector(avel.X, avel.Y, avel.Z);
        }

        public LSL_Float llGetTimeOfDay()
        {
            m_host.AddScriptLPS(1);
            return (double)((DateTime.Now.TimeOfDay.TotalMilliseconds / 1000) % (3600 * 4));
        }

        public LSL_Float llGetWallclock()
        {
            m_host.AddScriptLPS(1);
            return DateTime.Now.TimeOfDay.TotalSeconds;
        }

        public LSL_Float llGetTime()
        {
            m_host.AddScriptLPS(1);
            TimeSpan ScriptTime = DateTime.Now - m_timer;
            return (double)(ScriptTime.TotalMilliseconds / 1000);
        }

        public void llResetTime()
        {
            m_host.AddScriptLPS(1);
            m_timer = DateTime.Now;
        }

        public LSL_Float llGetAndResetTime()
        {
            m_host.AddScriptLPS(1);
            TimeSpan ScriptTime = DateTime.Now - m_timer;
            m_timer = DateTime.Now;
            return (double)(ScriptTime.TotalMilliseconds / 1000);
        }

        public void llSound(string sound, double volume, int queue, int loop)
        {
            m_host.AddScriptLPS(1);
            Deprecated("llSound", "Use llPlaySound instead");
        }

        // Xantor 20080528 PlaySound updated so it accepts an objectinventory name -or- a key to a sound
        // 20080530 Updated to remove code duplication
        public void llPlaySound(string sound, double volume)
        {
            m_host.AddScriptLPS(1);

            // send the sound, once, to all clients in range
            if (m_SoundModule != null)
            {
                m_SoundModule.SendSound(
                    m_host.UUID,
                    ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound, AssetType.Sound), 
                    volume, false, 0,
                    0, false, false);
            }
        }

        public void llLoopSound(string sound, double volume)
        {
            m_host.AddScriptLPS(1);
            if (m_SoundModule != null)
            {
                m_SoundModule.LoopSound(m_host.UUID, ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound),
                        volume, 20, false,false);
            }
        }

        public void llLoopSoundMaster(string sound, double volume)
        {
            m_host.AddScriptLPS(1);
            if (m_SoundModule != null)
            {
                m_SoundModule.LoopSound(m_host.UUID, ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound),
                        volume, 20, true, false);
            }
        }

        public void llLoopSoundSlave(string sound, double volume)
        {
            m_host.AddScriptLPS(1);
            if (m_SoundModule != null)
            {
                m_SoundModule.LoopSound(m_host.UUID, ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound),
                        volume, 20, false, true);
            }
        }

        public void llPlaySoundSlave(string sound, double volume)
        {
            m_host.AddScriptLPS(1);

            // send the sound, once, to all clients in range
            if (m_SoundModule != null)
            {
                m_SoundModule.SendSound(m_host.UUID,
                        ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound, AssetType.Sound), volume, false, 0,
                        0, true, false);
            }
        }

        public void llTriggerSound(string sound, double volume)
        {
            m_host.AddScriptLPS(1);
            // send the sound, once, to all clients in rangeTrigger or play an attached sound in this part's inventory.
            if (m_SoundModule != null)
            {
                m_SoundModule.SendSound(m_host.UUID,
                        ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound, AssetType.Sound), volume, true, 0, 0,
                        false, false);
            }
        }

        public void llStopSound()
        {
            m_host.AddScriptLPS(1);

            if (m_SoundModule != null)
                m_SoundModule.StopSound(m_host.UUID);
        }

        public void llPreloadSound(string sound)
        {
            m_host.AddScriptLPS(1);
            if (m_SoundModule != null)
                m_SoundModule.PreloadSound(m_host.UUID, ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound), 0);
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
            m_host.AddScriptLPS(1);

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
                    return src.Substring(0,end+1);
                }
                // Both indices are positive
                return src.Substring(start, (end+1) - start);
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
                        return src.Substring(start);
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
                        return src.Substring(0,end+1) + src.Substring(start);
                    }
                    else
                    {
                        return src.Substring(0,end+1);
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
            m_host.AddScriptLPS(1);

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
                // string, then return unchanges.
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
                        return src.Remove(start).Remove(0,end+1);
                    }
                    else
                    {
                        return src.Remove(0,end+1);
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
        public LSL_String llInsertString(string dest, int index, string src)
        {
            m_host.AddScriptLPS(1);

            // Normalize indices (if negative).
            // After normlaization they may still be
            // negative, but that is now relative to
            // the start, rather than the end, of the
            // sequence.
            if (index < 0)
            {
                index = dest.Length+index;

                // Negative now means it is less than the lower
                // bound of the string.

                if (index < 0)
                {
                    return src+dest;
                }

            }

            if (index >= dest.Length)
            {
                return dest+src;
            }

            // The index is in bounds.
            // In this case the index refers to the index that will
            // be assigned to the first character of the inserted string.
            // So unlike the other string operations, we do not add one
            // to get the correct string length.
            return dest.Substring(0,index)+src+dest.Substring(index);

        }

        public LSL_String llToUpper(string src)
        {
            m_host.AddScriptLPS(1);
            return src.ToUpper();
        }

        public LSL_String llToLower(string src)
        {
            m_host.AddScriptLPS(1);
            return src.ToLower();
        }

        public LSL_Integer llGiveMoney(string destination, int amount)
        {
            Util.FireAndForget(x =>
            {
                m_host.AddScriptLPS(1);

                if (m_item.PermsGranter == UUID.Zero)
                    return;

                if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_DEBIT) == 0)
                {
                    Error("llGiveMoney", "No permissions to give money");
                    return;
                }

                UUID toID = new UUID();

                if (!UUID.TryParse(destination, out toID))
                {
                    Error("llGiveMoney", "Bad key in llGiveMoney");
                    return;
                }

                IMoneyModule money = World.RequestModuleInterface<IMoneyModule>();

                if (money == null)
                {
                    NotImplemented("llGiveMoney");
                    return;
                }

                string reason;
                money.ObjectGiveMoney(

                    m_host.ParentGroup.RootPart.UUID, m_host.ParentGroup.RootPart.OwnerID, toID, amount,UUID.Zero, out reason);
            }, null, "LSL_Api.llGiveMoney");

            return 0;
        }

        public void llMakeExplosion(int particles, double scale, double vel, double lifetime, double arc, string texture, LSL_Vector offset)
        {
            m_host.AddScriptLPS(1);
            Deprecated("llMakeExplosion", "Use llParticleSystem instead");
            ScriptSleep(m_sleepMsOnMakeExplosion);
        }

        public void llMakeFountain(int particles, double scale, double vel, double lifetime, double arc, int bounce, string texture, LSL_Vector offset, double bounce_offset)
        {
            m_host.AddScriptLPS(1);
            Deprecated("llMakeFountain", "Use llParticleSystem instead");
            ScriptSleep(m_sleepMsOnMakeFountain);
        }

        public void llMakeSmoke(int particles, double scale, double vel, double lifetime, double arc, string texture, LSL_Vector offset)
        {
            m_host.AddScriptLPS(1);
            Deprecated("llMakeSmoke", "Use llParticleSystem instead");
            ScriptSleep(m_sleepMsOnMakeSmoke);
        }

        public void llMakeFire(int particles, double scale, double vel, double lifetime, double arc, string texture, LSL_Vector offset)
        {
            m_host.AddScriptLPS(1);
            Deprecated("llMakeFire", "Use llParticleSystem instead");
            ScriptSleep(m_sleepMsOnMakeFire);
        }

        public void llRezAtRoot(string inventory, LSL_Vector pos, LSL_Vector vel, LSL_Rotation rot, int param)
        {
            doObjectRez(inventory, pos, vel, rot, param, true);
        }

        public void doObjectRez(string inventory, LSL_Vector pos, LSL_Vector vel, LSL_Rotation rot, int param, bool atRoot)
        {
            m_host.AddScriptLPS(1);

            Util.FireAndForget(x =>
            {
                if (Double.IsNaN(rot.x) || Double.IsNaN(rot.y) || Double.IsNaN(rot.z) || Double.IsNaN(rot.s))
                    return;

                float dist = (float)llVecDist(llGetPos(), pos);

                if (dist > m_ScriptDistanceFactor * 10.0f)
                    return;

                TaskInventoryItem item = m_host.Inventory.GetInventoryItem(inventory);

                if (item == null)
                {
                    Error("llRezAtRoot", "Can't find object '" + inventory + "'");
                    return;
                }

                if (item.InvType != (int)InventoryType.Object)
                {
                    Error("llRezAtRoot", "Can't create requested object; object is missing from database");
                    return;
                }

                List<SceneObjectGroup> new_groups = World.RezObject(m_host, item, pos, rot, vel, param, atRoot);

                // If either of these are null, then there was an unknown error.
                if (new_groups == null)
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
                            new DetectParams[0]));

                    if (notAttachment)
                    {
                        float groupmass = group.GetMass();

                        PhysicsActor pa = group.RootPart.PhysActor;

                        //Recoil.
                        if (pa != null && pa.IsPhysical && (Vector3)vel != Vector3.Zero)
                        {
                            Vector3 recoil = -vel * groupmass * m_recoilScaleFactor;
                            if (recoil != Vector3.Zero)
                            {
                                llApplyImpulse(recoil, 0);
                            }
                        }
                    }
                    // Variable script delay? (see (http://wiki.secondlife.com/wiki/LSL_Delay)
                }

            }, null, "LSL_Api.llRezAtRoot");

            //ScriptSleep((int)((groupmass * velmag) / 10));
            ScriptSleep(m_sleepMsOnRezAtRoot);
        }

        public void llRezObject(string inventory, LSL_Vector pos, LSL_Vector vel, LSL_Rotation rot, int param)
        {
            doObjectRez(inventory, pos, vel, rot, param, false);
        }

        public void llLookAt(LSL_Vector target, double strength, double damping)
        {
            m_host.AddScriptLPS(1);

            // Get the normalized vector to the target
            LSL_Vector d1 = llVecNorm(target - llGetPos());

            // Get the bearing (yaw)
            LSL_Vector a1 = new LSL_Vector(0,0,0);
            a1.z = llAtan2(d1.y, d1.x);

            // Get the elevation (pitch)
            LSL_Vector a2 = new LSL_Vector(0,0,0);
            a2.y= -llAtan2(d1.z, llSqrt((d1.x * d1.x) + (d1.y * d1.y)));

            LSL_Rotation r1 = llEuler2Rot(a1);
            LSL_Rotation r2 = llEuler2Rot(a2);
            LSL_Rotation r3 = new LSL_Rotation(0.000000, 0.707107, 0.000000, 0.707107);

            if (m_host.PhysActor == null || !m_host.PhysActor.IsPhysical)
            {
                // Do nothing if either value is 0 (this has been checked in SL)
                if (strength <= 0.0 || damping <= 0.0)
                    return;

                llSetRot(r3 * r2 * r1);
            }
            else
            {
                if (strength == 0)
                {
                    llSetRot(r3 * r2 * r1);
                    return;
                }

                m_host.StartLookAt((Quaternion)(r3 * r2 * r1), (float)strength, (float)damping);
            }
        }

        public void llStopLookAt()
        {
            m_host.AddScriptLPS(1);
            m_host.StopLookAt();
        }

        public void llSetTimerEvent(double sec)
        {
            if (sec != 0.0 && sec < m_MinTimerInterval)
                sec = m_MinTimerInterval;
            m_host.AddScriptLPS(1);
            // Setting timer repeat
            AsyncCommands.TimerPlugin.SetTimerEvent(m_host.LocalId, m_item.ItemID, sec);
        }

        public virtual void llSleep(double sec)
        {
//            m_log.Info("llSleep snoozing " + sec + "s.");
            m_host.AddScriptLPS(1);

            Sleep((int)(sec * 1000));
        }

        public LSL_Float llGetMass()
        {
            m_host.AddScriptLPS(1);

            if (m_host.ParentGroup.IsAttachment)
            {
                ScenePresence attachedAvatar = World.GetScenePresence(m_host.ParentGroup.AttachedAvatar);

                if (attachedAvatar != null)
                {
                    return attachedAvatar.GetMass();
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                // new SL always returns object mass
//                if (m_host.IsRoot)
//                {
                    return m_host.ParentGroup.GetMass();
//                }
//                else
//                {
//                    return m_host.GetMass();
//                }
            }
        }

        public LSL_Float llGetMassMKS()
        {
            return 100f * llGetMass();
        }

        public void llCollisionFilter(string name, string id, int accept)
        {
            m_host.AddScriptLPS(1);
            m_host.CollisionFilter.Clear();
            UUID objectID;

            if (!UUID.TryParse(id, out objectID))
                objectID = UUID.Zero;

            if (objectID == UUID.Zero && name == "")
                return;

            m_host.CollisionFilter.Add(accept,objectID.ToString() + name);
        }

        public void llTakeControls(int controls, int accept, int pass_on)
        {
            if (m_item.PermsGranter != UUID.Zero)
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

            m_host.AddScriptLPS(1);
        }

        public void llReleaseControls()
        {
            m_host.AddScriptLPS(1);

            if (m_item.PermsGranter != UUID.Zero)
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
            m_host.AddScriptLPS(1);
            if (m_UrlModule != null)
                m_UrlModule.ReleaseURL(url);
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

        public void llAttachToAvatar(int attachmentPoint)
        {
            m_host.AddScriptLPS(1);

            if (m_item.PermsGranter != m_host.OwnerID)
                return;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_ATTACH) != 0)
                AttachToAvatar(attachmentPoint);
        }

        public void llDetachFromAvatar()
        {
            m_host.AddScriptLPS(1);

            if (m_host.ParentGroup.AttachmentPoint == 0)
                return;

            if (m_item.PermsGranter != m_host.OwnerID)
                return;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_ATTACH) != 0)
                DetachFromAvatar();
        }

        public void llTakeCamera(string avatar)
        {
            m_host.AddScriptLPS(1);
            Deprecated("llTakeCamera", "Use llSetCameraParams instead");
        }

        public void llReleaseCamera(string avatar)
        {
            m_host.AddScriptLPS(1);
            Deprecated("llReleaseCamera", "Use llClearCameraParams instead");
        }

        public LSL_String llGetOwner()
        {
            m_host.AddScriptLPS(1);

            return m_host.OwnerID.ToString();
        }

        public void llInstantMessage(string user, string message)
        {
            m_host.AddScriptLPS(1);
            UUID result;
            if (!UUID.TryParse(user, out result) || result == UUID.Zero)
            {
                Error("llInstantMessage","An invalid key  was passed to llInstantMessage");
                ScriptSleep(2000);
                return;
            }
            
            // We may be able to use ClientView.SendInstantMessage here, but we need a client instance.
            // InstantMessageModule.OnInstantMessage searches through a list of scenes for a client matching the toAgent,
            // but I don't think we have a list of scenes available from here.
            // (We also don't want to duplicate the code in OnInstantMessage if we can avoid it.)

            // user is a UUID

            // TODO: figure out values for client, fromSession, and imSessionID
            // client.SendInstantMessage(m_host.UUID, fromSession, message, user, imSessionID, m_host.Name, AgentManager.InstantMessageDialog.MessageFromAgent, (uint)Util.UnixTimeSinceEpoch());
            UUID friendTransactionID = UUID.Random();

            //m_pendingFriendRequests.Add(friendTransactionID, fromAgentID);
            
            GridInstantMessage msg = new GridInstantMessage();
            msg.fromAgentID = new Guid(m_host.OwnerID.ToString()); // fromAgentID.Guid;
            msg.toAgentID = new Guid(user); // toAgentID.Guid;
            msg.imSessionID = new Guid(m_host.UUID.ToString()); // This is the item we're mucking with here
            msg.timestamp = (uint)Util.UnixTimeSinceEpoch();
            msg.fromAgentName = m_host.Name;//client.FirstName + " " + client.LastName;// fromAgentName;

            if (message != null && message.Length > 1024)
                msg.message = message.Substring(0, 1024);
            else
                msg.message = message;
            msg.dialog = (byte)19; // MessageFromObject
            msg.fromGroup = false;// fromGroup;
            msg.offline = (byte)0; //offline;
            msg.ParentEstateID = World.RegionInfo.EstateSettings.EstateID;
            msg.Position = new Vector3(m_host.AbsolutePosition);
            msg.RegionID = World.RegionInfo.RegionID.Guid;//RegionID.Guid;

            Vector3 pos = m_host.AbsolutePosition;
            msg.binaryBucket
                = Util.StringToBytes256(
                    "{0}/{1}/{2}/{3}",
                    World.RegionInfo.RegionName,
                    (int)Math.Floor(pos.X),
                    (int)Math.Floor(pos.Y),
                    (int)Math.Floor(pos.Z));

            if (m_TransferModule != null)
            {
                m_TransferModule.SendInstantMessage(msg, delegate(bool success) {});
            }

            ScriptSleep(m_sleepMsOnInstantMessage);
      }

        public void llEmail(string address, string subject, string message)
        {
            m_host.AddScriptLPS(1);
            IEmailModule emailModule = m_ScriptEngine.World.RequestModuleInterface<IEmailModule>();
            if (emailModule == null)
            {
                Error("llEmail", "Email module not configured");
                return;
            }

            //Restrict email destination to the avatars registered email address?
            //The restriction only applies if the destination address is not local.
            if (m_restrictEmail == true && address.Contains(m_internalObjectHost) == false)
            {
                UserAccount account =
                        World.UserAccountService.GetUserAccount(
                            World.RegionInfo.ScopeID,
                            m_host.OwnerID);

                if (account == null)
                {
                    Error("llEmail", "Can't find user account for '" + m_host.OwnerID.ToString() + "'");
                    return;
                }

                if (String.IsNullOrEmpty(account.Email))
                {
                    Error("llEmail", "User account has not registered an email address.");
                    return;
                }

                address = account.Email;
            }

            emailModule.SendEmail(m_host.UUID, address, subject, message);
            ScriptSleep(m_sleepMsOnEmail);
        }

        public void llGetNextEmail(string address, string subject)
        {
            m_host.AddScriptLPS(1);
            IEmailModule emailModule = m_ScriptEngine.World.RequestModuleInterface<IEmailModule>();
            if (emailModule == null)
            {
                Error("llGetNextEmail", "Email module not configured");
                return;
            }
            Email email;

            email = emailModule.GetNextEmail(m_host.UUID, address, subject);

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
                    new DetectParams[0]));

        }

        public LSL_String llGetKey()
        {
            m_host.AddScriptLPS(1);
            return m_host.UUID.ToString();
        }

        public LSL_Key llGenerateKey()
        {
            m_host.AddScriptLPS(1);
            return UUID.Random().ToString();
        }

        public void llSetBuoyancy(double buoyancy)
        {
            m_host.AddScriptLPS(1);

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
            m_host.AddScriptLPS(1);

            PIDHoverType hoverType = PIDHoverType.Ground;
            if (water != 0)
            {
                hoverType = PIDHoverType.GroundAndWater;
            }
            m_host.SetHoverHeight((float)height, hoverType, (float)tau);
        }

        public void llStopHover()
        {
            m_host.AddScriptLPS(1);
            m_host.SetHoverHeight(0f, PIDHoverType.Ground, 0f);
        }

        public void llMinEventDelay(double delay)
        {
            m_host.AddScriptLPS(1);
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
            m_host.AddScriptLPS(1);
            Deprecated("llSoundPreload", "Use llPreloadSound instead");
        }

        public void llRotLookAt(LSL_Rotation target, double strength, double damping)
        {
            m_host.AddScriptLPS(1);

            // Per discussion with Melanie, for non-physical objects llLookAt appears to simply
            // set the rotation of the object, copy that behavior
            PhysicsActor pa = m_host.PhysActor;

            if (strength == 0 || pa == null || !pa.IsPhysical)
            {
                llSetLocalRot(target);
            }
            else
            {
                m_host.RotLookAt(target, (float)strength, (float)damping);
            }
        }

        public LSL_Integer llStringLength(string str)
        {
            m_host.AddScriptLPS(1);
            if (str.Length > 0)
            {
                return str.Length;
            }
            else
            {
                return 0;
            }
        }

        public void llStartAnimation(string anim)
        {
            m_host.AddScriptLPS(1);

            if (m_item.PermsGranter == UUID.Zero)
                return;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION) != 0)
            {
                ScenePresence presence = World.GetScenePresence(m_item.PermsGranter);

                if (presence != null)
                {
                    // Do NOT try to parse UUID, animations cannot be triggered by ID
                    UUID animID = ScriptUtils.GetAssetIdFromItemName(m_host, anim, (int)AssetType.Animation);
                    if (animID == UUID.Zero)
                        presence.Animator.AddAnimation(anim, m_host.UUID);
                    else
                        presence.Animator.AddAnimation(animID, m_host.UUID);
                }
            }
        }

        public void llStopAnimation(string anim)
        {
            m_host.AddScriptLPS(1);

            if (m_item.PermsGranter == UUID.Zero)
                return;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION) != 0)
            {
                ScenePresence presence = World.GetScenePresence(m_item.PermsGranter);

                if (presence != null)
                {
                    UUID animID = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, anim);

                    if (animID == UUID.Zero)
                        presence.Animator.RemoveAnimation(anim);
                    else
                        presence.Animator.RemoveAnimation(animID, true);
                }
            }
        }

        public void llPointAt(LSL_Vector pos)
        {
            m_host.AddScriptLPS(1);
        }

        public void llStopPointAt()
        {
            m_host.AddScriptLPS(1);
        }

        public void llTargetOmega(LSL_Vector axis, double spinrate, double gain)
        {
            m_host.AddScriptLPS(1);
            TargetOmega(m_host, axis, spinrate, gain);
        }

        protected void TargetOmega(SceneObjectPart part, LSL_Vector axis, double spinrate, double gain)
        {
            PhysicsActor pa = part.PhysActor;
            if ( ( pa == null || !pa.IsPhysical ) && gain == 0.0d )
                spinrate = 0.0d;
            part.UpdateAngularVelocity(axis * spinrate);
         }

        public LSL_Integer llGetStartParameter()
        {
            m_host.AddScriptLPS(1);
            return m_ScriptEngine.GetStartParameter(m_item.ItemID);
        }

        public void llRequestPermissions(string agent, int perm)
        {
            UUID agentID;

            if (!UUID.TryParse(agent, out agentID))
                return;

            if (agentID == UUID.Zero || perm == 0) // Releasing permissions
            {
                llReleaseControls();

                m_item.PermsGranter = UUID.Zero;
                m_item.PermsMask = 0;

                m_ScriptEngine.PostScriptEvent(m_item.ItemID, new EventParams(
                        "run_time_permissions", new Object[] {
                        new LSL_Integer(0) },
                        new DetectParams[0]));

                return;
            }

            if (m_item.PermsGranter != agentID || (perm & ScriptBaseClass.PERMISSION_TAKE_CONTROLS) == 0)
                llReleaseControls();

            m_host.AddScriptLPS(1);

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
                if (m_host.ParentGroup.GetSittingAvatars().SingleOrDefault(sp => sp.UUID == agentID) != null)
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
                        new DetectParams[0]));

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
                                "run_time_permissions", new Object[] { new LSL_Integer(perm) }, new DetectParams[0]));
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
                new EventParams("run_time_permissions", new Object[] { new LSL_Integer(0) }, new DetectParams[0]));
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
                    new DetectParams[0]));
        }

        public LSL_String llGetPermissionsKey()
        {
            m_host.AddScriptLPS(1);

            return m_item.PermsGranter.ToString();
        }

        public LSL_Integer llGetPermissions()
        {
            m_host.AddScriptLPS(1);

            int perms = m_item.PermsMask;

            if (m_automaticLinkPermission)
                perms |= ScriptBaseClass.PERMISSION_CHANGE_LINKS;

            return perms;
        }

        public LSL_Integer llGetLinkNumber()
        {
            m_host.AddScriptLPS(1);

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

        public void llCreateLink(string target, int parent)
        {
            m_host.AddScriptLPS(1);

            UUID targetID;

            if (!UUID.TryParse(target, out targetID))
                return;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_CHANGE_LINKS) == 0
                && !m_automaticLinkPermission)
            {
                Error("llCreateLink", "PERMISSION_CHANGE_LINKS permission not set");
                return;
            }

            CreateLink(target, parent);
        }

        public void CreateLink(string target, int parent)
        {
            UUID targetID;

            if (!UUID.TryParse(target, out targetID))
                return;           

            SceneObjectPart targetPart = World.GetSceneObjectPart((UUID)targetID);

            if (targetPart.ParentGroup.AttachmentPoint != 0)
                return; // Fail silently if attached

            if (targetPart.ParentGroup.RootPart.OwnerID != m_host.ParentGroup.RootPart.OwnerID)
                return;

            SceneObjectGroup parentPrim = null, childPrim = null;

            if (targetPart != null)
            {
                if (parent != 0)
                {
                    parentPrim = m_host.ParentGroup;
                    childPrim = targetPart.ParentGroup;
                }
                else
                {
                    parentPrim = targetPart.ParentGroup;
                    childPrim = m_host.ParentGroup;
                }

                // Required for linking
                childPrim.RootPart.ClearUpdateSchedule();
                parentPrim.LinkToGroup(childPrim, true);
            }

            parentPrim.TriggerScriptChangedEvent(Changed.LINK);
            parentPrim.RootPart.CreateSelected = true;
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
            m_host.AddScriptLPS(1);

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

            SceneObjectGroup parentPrim = m_host.ParentGroup;

            if (parentPrim.AttachmentPoint != 0)
                return; // Fail silently if attached
            SceneObjectPart childPrim = null;

            switch (linknum)
            {
                case ScriptBaseClass.LINK_ROOT:
                    break;
                case ScriptBaseClass.LINK_SET:
                case ScriptBaseClass.LINK_ALL_OTHERS:
                case ScriptBaseClass.LINK_ALL_CHILDREN:
                case ScriptBaseClass.LINK_THIS:
                    foreach (SceneObjectPart part in parentPrim.Parts)
                    {
                        if (part.UUID != m_host.UUID)
                        {
                            childPrim = part;
                            break;
                        }
                    }
                    break;
                default:
                    childPrim = parentPrim.GetLinkNumPart(linknum);
                    if (childPrim.UUID == m_host.UUID)
                        childPrim = null;
                    break;
            }

            if (linknum == ScriptBaseClass.LINK_ROOT)
            {
                // Restructuring Multiple Prims.
                List<SceneObjectPart> parts = new List<SceneObjectPart>(parentPrim.Parts);
                parts.Remove(parentPrim.RootPart);
                if (parts.Count > 0)
                {
                    try
                    {
                        foreach (SceneObjectPart part in parts)
                        {
                            parentPrim.DelinkFromGroup(part.LocalId, true);
                        }
                    }
                    finally { }
                 }

                parentPrim.HasGroupChanged = true;
                parentPrim.ScheduleGroupForFullUpdate();
                parentPrim.TriggerScriptChangedEvent(Changed.LINK);

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

                parentPrim.DelinkFromGroup(childPrim.LocalId, true);
                parentPrim.HasGroupChanged = true;
                parentPrim.ScheduleGroupForFullUpdate();
                parentPrim.TriggerScriptChangedEvent(Changed.LINK);
            }
        }

        public void llBreakAllLinks()
        {
            m_host.AddScriptLPS(1);

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

            List<SceneObjectPart> parts = new List<SceneObjectPart>(parentPrim.Parts);
            parts.Remove(parentPrim.RootPart);

            foreach (SceneObjectPart part in parts)
            {
                parentPrim.DelinkFromGroup(part.LocalId, true);
                parentPrim.TriggerScriptChangedEvent(Changed.LINK);
            }
            parentPrim.HasGroupChanged = true;
            parentPrim.ScheduleGroupForFullUpdate();
        }

        public LSL_String llGetLinkKey(int linknum)
        {
            m_host.AddScriptLPS(1);
            SceneObjectPart part = m_host.ParentGroup.GetLinkNumPart(linknum);
            if (part != null)
            {
                return part.UUID.ToString();
            }
            else
            {
                if (linknum > m_host.ParentGroup.PrimCount || (linknum == 1 && m_host.ParentGroup.PrimCount == 1))
                {
                    linknum -= (m_host.ParentGroup.PrimCount) + 1;

                    if (linknum < 0)
                        return UUID.Zero.ToString();

                    List<ScenePresence> avatars = GetLinkAvatars(ScriptBaseClass.LINK_SET);
                    if (avatars.Count > linknum)
                    {
                        return avatars[linknum].UUID.ToString();
                    }
                }
                return UUID.Zero.ToString();
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
        /// The name determined to match the specified link number.
        /// </returns>
        /// <remarks>
        /// The rules governing the returned name are not simple. The only
        /// time a blank name is returned is if the target prim has a blank
        /// name. If no prim with the given link number can be found then
        /// usually NULL_KEY is returned but there are exceptions.
        ///
        /// In a single unlinked prim, A call with 0 returns the name, all
        /// other values for link number return NULL_KEY
        ///
        /// In link sets it is more complicated.
        ///
        /// If the script is in the root prim:-
        ///     A zero link number returns NULL_KEY.
        ///     Positive link numbers return the name of the prim, or NULL_KEY
        ///     if a prim does not exist at that position.
        ///     Negative link numbers return the name of the first child prim.
        ///
        /// If the script is in a child prim:-
        ///     Link numbers 0 or 1 return the name of the root prim.
        ///     Positive link numbers return the name of the prim or NULL_KEY
        ///     if a prim does not exist at that position.
        ///     Negative numbers return the name of the root prim.
        ///
        /// References
        /// http://lslwiki.net/lslwiki/wakka.php?wakka=llGetLinkName
        /// Mentions NULL_KEY being returned
        /// http://wiki.secondlife.com/wiki/LlGetLinkName
        /// Mentions using the LINK_* constants, some of which are negative
        /// </remarks>
        public LSL_String llGetLinkName(int linknum)
        {
            m_host.AddScriptLPS(1);

            ISceneEntity entity = GetLinkEntity(m_host, linknum);

            if (entity != null)
                return entity.Name;
            else
                return ScriptBaseClass.NULL_KEY;
        }

        public LSL_Integer llGetInventoryNumber(int type)
        {
            m_host.AddScriptLPS(1);
            int count = 0;

            m_host.TaskInventory.LockItemsForRead(true);
            foreach (KeyValuePair<UUID, TaskInventoryItem> inv in m_host.TaskInventory)
            {
                if (inv.Value.Type == type || type == -1)
                {
                    count = count + 1;
                }
            }
            
            m_host.TaskInventory.LockItemsForRead(false);
            return count;
        }

        public LSL_String llGetInventoryName(int type, int number)
        {
            m_host.AddScriptLPS(1);
            ArrayList keys = new ArrayList();

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
            m_host.AddScriptLPS(1);
            // TODO: figure out real energy value
            return 1.0f;
        }

        public void llGiveInventory(string destination, string inventory)
        {
            m_host.AddScriptLPS(1);

            UUID destId = UUID.Zero;

            if (!UUID.TryParse(destination, out destId))
            {
                Error("llGiveInventory", "Can't parse destination key '" + destination + "'");
                return;
            }

            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(inventory);

            if (item == null)
            {
                Error("llGiveInventory", "Can't find inventory object '" + inventory + "'");
                return;
            }

            UUID objId = item.ItemID;

            // check if destination is an object
            if (World.GetSceneObjectPart(destId) != null)
            {
                // destination is an object
                World.MoveTaskInventoryItem(destId, m_host, objId);
            }
            else
            {
                ScenePresence presence = World.GetScenePresence(destId);

                if (presence == null)
                {
                    UserAccount account =
                            World.UserAccountService.GetUserAccount(
                            World.RegionInfo.ScopeID,
                            destId);

                    if (account == null)
                    {
                        GridUserInfo info = World.GridUserService.GetGridUserInfo(destId.ToString());
                        if(info == null || info.Online == false)
                        {
                            Error("llGiveInventory", "Can't find destination '" + destId.ToString() + "'");
                            return;
                        }
                    }
                }

                // destination is an avatar
                string message;
                InventoryItemBase agentItem = World.MoveTaskInventoryItem(destId, UUID.Zero, m_host, objId, out message);

                if (agentItem == null)
                {
                    llSay(0, message); 
                    return;
                }

                byte[] bucket = new byte[1];
                bucket[0] = (byte)item.Type;
                //byte[] objBytes = agentItem.ID.GetBytes();
                //Array.Copy(objBytes, 0, bucket, 1, 16);

                GridInstantMessage msg = new GridInstantMessage(World,
                        m_host.OwnerID, m_host.Name, destId,
                        (byte)InstantMessageDialog.TaskInventoryOffered,
                        false, item.Name+". "+m_host.Name+" is located at "+
                        World.RegionInfo.RegionName+" "+
                        m_host.AbsolutePosition.ToString(),
                        agentItem.ID, true, m_host.AbsolutePosition,
                        bucket, true);

                ScenePresence sp;

                if (World.TryGetScenePresence(destId, out sp))
                {
                    sp.ControllingClient.SendInstantMessage(msg);
                }
                else
                {
                    if (m_TransferModule != null)
                        m_TransferModule.SendInstantMessage(msg, delegate(bool success) {});
                }
                
                //This delay should only occur when giving inventory to avatars.
                ScriptSleep(m_sleepMsOnGiveInventory);
            }
        }

        [DebuggerNonUserCode]
        public void llRemoveInventory(string name)
        {
            m_host.AddScriptLPS(1);

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
            m_host.AddScriptLPS(1);
            Vector3 av3 = Util.Clip(color, 0.0f, 1.0f);
            if (text.Length > 254)
                text = text.Remove(254);

            byte[] data;
            do
            {
                data = Util.UTF8.GetBytes(text);
                if (data.Length > 254)
                    text = text.Substring(0, text.Length - 1);
            } while (data.Length > 254);

            m_host.SetText(text, av3, Util.Clip((float)alpha, 0.0f, 1.0f));
            //m_host.ParentGroup.HasGroupChanged = true;
            //m_host.ParentGroup.ScheduleGroupForFullUpdate();
        }

        public LSL_Float llWater(LSL_Vector offset)
        {
            m_host.AddScriptLPS(1);
            return World.RegionInfo.RegionSettings.WaterHeight;
        }

        public void llPassTouches(int pass)
        {
            m_host.AddScriptLPS(1);
            if (pass != 0)
                m_host.PassTouches = true;
            else
                m_host.PassTouches = false;
        }

        public LSL_String llRequestAgentData(string id, int data)
        {
            m_host.AddScriptLPS(1);

            UUID uuid;
            if (UUID.TryParse(id, out uuid))
            {
                PresenceInfo pinfo = null;
                UserAccount account;

                UserInfoCacheEntry ce;
                if (!m_userInfoCache.TryGetValue(uuid, out ce))
                {
                    account = World.UserAccountService.GetUserAccount(World.RegionInfo.ScopeID, uuid);
                    if (account == null)
                    {
                        m_userInfoCache[uuid] = null; // Cache negative
                        return UUID.Zero.ToString();
                    }

                    PresenceInfo[] pinfos = World.PresenceService.GetAgents(new string[] { uuid.ToString() });
                    if (pinfos != null && pinfos.Length > 0)
                    {
                        foreach (PresenceInfo p in pinfos)
                        {
                            if (p.RegionID != UUID.Zero)
                            {
                                pinfo = p;
                            }
                        }
                    }

                    ce = new UserInfoCacheEntry();
                    ce.time = Util.EnvironmentTickCount();
                    ce.account = account;
                    ce.pinfo = pinfo;
                    m_userInfoCache[uuid] = ce;
                }
                else
                {
                    if (ce == null)
                        return UUID.Zero.ToString();

                    account = ce.account;
                    pinfo = ce.pinfo;
                }

                if (Util.EnvironmentTickCount() < ce.time ||
                            (Util.EnvironmentTickCount() - ce.time) >= LlRequestAgentDataCacheTimeoutMs)
                {
                    PresenceInfo[] pinfos = World.PresenceService.GetAgents(new string[] { uuid.ToString() });
                    if (pinfos != null && pinfos.Length > 0)
                    {
                        foreach (PresenceInfo p in pinfos)
                        {
                            if (p.RegionID != UUID.Zero)
                            {
                                pinfo = p;
                            }
                        }
                    }
                    else
                        pinfo = null;

                    ce.time = Util.EnvironmentTickCount();
                    ce.pinfo = pinfo;
                }

                string reply = String.Empty;

                switch (data)
                {
                    case ScriptBaseClass.DATA_ONLINE: // DATA_ONLINE (0|1)
                        if (pinfo != null && pinfo.RegionID != UUID.Zero)
                            reply = "1";
                        else
                            reply = "0";
                        break;
                    case ScriptBaseClass.DATA_NAME: // DATA_NAME (First Last)
                        reply = account.FirstName + " " + account.LastName;
                        break;
                    case ScriptBaseClass.DATA_BORN: // DATA_BORN (YYYY-MM-DD)
                        DateTime born = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                        born = born.AddSeconds(account.Created);
                        reply = born.ToString("yyyy-MM-dd");
                        break;
                    case ScriptBaseClass.DATA_RATING: // DATA_RATING (0,0,0,0,0,0)
                        reply = "0,0,0,0,0,0";
                        break;
                    case 7: // DATA_USERLEVEL (integer).  This is not available in LL and so has no constant.
                        reply = account.UserLevel.ToString();
                        break;
                    case ScriptBaseClass.DATA_PAYINFO: // DATA_PAYINFO (0|1|2|3)
                        reply = "0";
                        break;
                    default:
                        return UUID.Zero.ToString(); // Raise no event
                }

                UUID rq = UUID.Random();

                UUID tid = AsyncCommands.
                    DataserverPlugin.RegisterRequest(m_host.LocalId,
                                                 m_item.ItemID, rq.ToString());

                AsyncCommands.
                DataserverPlugin.DataserverReply(rq.ToString(), reply);

                ScriptSleep(m_sleepMsOnRequestAgentData);
                return tid.ToString();
            }
            else
            {
                Error("llRequestAgentData","Invalid UUID passed to llRequestAgentData.");
            }
            return "";
        }

        public LSL_String llRequestInventoryData(string name)
        {
            m_host.AddScriptLPS(1);

            foreach (TaskInventoryItem item in m_host.Inventory.GetInventoryItems())
            {
                if (item.Type == 3 && item.Name == name)
                {
                    UUID tid = AsyncCommands.
                        DataserverPlugin.RegisterRequest(m_host.LocalId,
                                                     m_item.ItemID, item.AssetID.ToString());

                    Vector3 region = new Vector3(World.RegionInfo.WorldLocX, World.RegionInfo.WorldLocY, 0);

                    World.AssetService.Get(item.AssetID.ToString(), this,
                        delegate(string i, object sender, AssetBase a)
                        {
                            AssetLandmark lm = new AssetLandmark(a);

                            float rx = (uint)(lm.RegionHandle >> 32);
                            float ry = (uint)lm.RegionHandle;
                            region = lm.Position + new Vector3(rx, ry, 0) - region;

                            string reply = region.ToString();
                            AsyncCommands.
                                DataserverPlugin.DataserverReply(i.ToString(),
                                                             reply);
                        });

                    ScriptSleep(m_sleepMsOnRequestInventoryData);
                    return tid.ToString();
                }
            }

            ScriptSleep(m_sleepMsOnRequestInventoryData);
            return String.Empty;
        }

        public void llSetDamage(double damage)
        {
            m_host.AddScriptLPS(1);
            m_host.ParentGroup.Damage = (float)damage;
        }

        public void llTeleportAgentHome(string agent)
        {
            m_host.AddScriptLPS(1);
            UUID agentId = new UUID();
            if (UUID.TryParse(agent, out agentId))
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence != null && presence.PresenceType != PresenceType.Npc)
                {
                    // agent must not be a god
                    if (presence.UserLevel >= 200) return;

                    // agent must be over the owners land
                    if (m_host.OwnerID == World.LandChannel.GetLandObject(presence.AbsolutePosition).LandData.OwnerID)
                    {
                        if (!World.TeleportClientHome(agentId, presence.ControllingClient))
                        {
                            // They can't be teleported home for some reason
                            GridRegion regionInfo = World.GridService.GetRegionByUUID(UUID.Zero, new UUID("2b02daac-e298-42fa-9a75-f488d37896e6"));
                            if (regionInfo != null)
                            {
                                World.RequestTeleportLocation(
                                    presence.ControllingClient, regionInfo.RegionHandle, new Vector3(128, 128, 23), Vector3.Zero,
                                    (uint)(Constants.TeleportFlags.SetLastToTarget | Constants.TeleportFlags.ViaHome));
                            }
                        }
                    }
                }
            }

            ScriptSleep(m_sleepMsOnSetDamage);
        }

        public void llTeleportAgent(string agent, string destination, LSL_Vector targetPos, LSL_Vector targetLookAt)
        {
            m_host.AddScriptLPS(1);
            UUID agentId = new UUID();

            if (UUID.TryParse(agent, out agentId))
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence != null && presence.PresenceType != PresenceType.Npc)
                {
                    if (destination == String.Empty)
                        destination = World.RegionInfo.RegionName;

                    if (m_item.PermsGranter == agentId)
                    {
                        if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TELEPORT) != 0)
                        {
                            DoLLTeleport(presence, destination, targetPos, targetLookAt);
                        }
                    }

                    // agent must be wearing the object
                    if (m_host.ParentGroup.AttachmentPoint != 0 && m_host.OwnerID == presence.UUID)
                    {
                        DoLLTeleport(presence, destination, targetPos, targetLookAt);
                    }
                    else
                    {
                        // agent must not be a god
                        if (presence.GodLevel >= 200) return;

                        // agent must be over the owners land
                        ILandObject agentLand = World.LandChannel.GetLandObject(presence.AbsolutePosition);
                        ILandObject objectLand = World.LandChannel.GetLandObject(m_host.AbsolutePosition);
                        if (m_host.OwnerID == objectLand.LandData.OwnerID && m_host.OwnerID == agentLand.LandData.OwnerID)
                        {
                            DoLLTeleport(presence, destination, targetPos, targetLookAt);
                        }
                    }
                }
            }
        }

        public void llTeleportAgentGlobalCoords(string agent, LSL_Vector global_coords, LSL_Vector targetPos, LSL_Vector targetLookAt)
        {
            m_host.AddScriptLPS(1);
            UUID agentId = new UUID();

            ulong regionHandle = Util.RegionWorldLocToHandle((uint)global_coords.x, (uint)global_coords.y);

            if (UUID.TryParse(agent, out agentId))
            {
                // This function is owner only!
                if (m_host.OwnerID != agentId)
                    return;

                ScenePresence presence = World.GetScenePresence(agentId);

                if (presence == null || presence.PresenceType == PresenceType.Npc)
                    return;

                // Can't TP sitting avatars
                if (presence.ParentID != 0) // Sitting
                    return;
                   				
                if (m_item.PermsGranter == agentId)
                {
                    // If attached using llAttachToAvatarTemp, cowardly refuse
                    if (m_host.ParentGroup.AttachmentPoint != 0 && m_host.ParentGroup.FromItemID == UUID.Zero)
                        return;

                    if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TELEPORT) != 0)
                    {
                       World.RequestTeleportLocation(presence.ControllingClient, regionHandle, targetPos, targetLookAt, (uint)TeleportFlags.ViaLocation);
                    }
                }
            }
        }

        private void DoLLTeleport(ScenePresence sp, string destination, Vector3 targetPos, Vector3 targetLookAt)
        {
            UUID assetID = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, destination);

            // The destinaion is not an asset ID and also doesn't name a landmark.
            // Use it as a sim name
            if (assetID == UUID.Zero)
            {
                World.RequestTeleportLocation(sp.ControllingClient, destination, targetPos, targetLookAt, (uint)TeleportFlags.ViaLocation);
                return;
            }

            AssetBase lma = World.AssetService.Get(assetID.ToString());
            if (lma == null)
                return;

            if (lma.Type != (sbyte)AssetType.Landmark)
                return;

            AssetLandmark lm = new AssetLandmark(lma);

            World.RequestTeleportLocation(sp.ControllingClient, lm.RegionHandle, targetPos, targetLookAt, (uint)TeleportFlags.ViaLocation);
        }

        public void llTextBox(string agent, string message, int chatChannel)
        {
            IDialogModule dm = World.RequestModuleInterface<IDialogModule>();

            if (dm == null)
                return;

            m_host.AddScriptLPS(1);
            UUID av = new UUID();
            if (!UUID.TryParse(agent,out av))
            {
                Error("llTextBox", "First parameter must be a key");
                return;
            }

            if (message == string.Empty)
            {
                Error("llTextBox", "Empty message");
            }
            else if (Encoding.UTF8.GetByteCount(message) > 512)
            {
                Error("llTextBox", "Message longer than 512 bytes");
            }
            else
            {
                dm.SendTextBoxToUser(av, message, chatChannel, m_host.Name, m_host.UUID, m_host.OwnerID);
                ScriptSleep(m_sleepMsOnTextBox);
            }
        }

        public void llModifyLand(int action, int brush)
        {
            m_host.AddScriptLPS(1);
            ITerrainModule tm = m_ScriptEngine.World.RequestModuleInterface<ITerrainModule>();
            if (tm != null)
            {
                tm.ModifyTerrain(m_host.OwnerID, m_host.AbsolutePosition, (byte) brush, (byte) action, m_host.OwnerID);
            }
        }

        public void llCollisionSound(string impact_sound, double impact_volume)
        {
            m_host.AddScriptLPS(1);

            if(impact_sound == "")
            {
                m_host.CollisionSoundVolume = (float)impact_volume;
                m_host.CollisionSound = m_host.invalidCollisionSoundUUID;
                m_host.CollisionSoundType = 0;
                return;
            }
            // TODO: Parameter check logic required.
            m_host.CollisionSound = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, impact_sound, AssetType.Sound);
            m_host.CollisionSoundVolume = (float)impact_volume;
            m_host.CollisionSoundType = 1;
        }

        public LSL_String llGetAnimation(string id)
        {
            // This should only return a value if the avatar is in the same region
            m_host.AddScriptLPS(1);
            UUID avatar = (UUID)id;
            ScenePresence presence = World.GetScenePresence(avatar);
            if (presence == null)
                return "";

            if (m_host.RegionHandle == presence.RegionHandle)
            {
                if (presence != null)
                {
//                    if (presence.SitGround)
//                        return "Sitting on Ground";
//                    if (presence.ParentID != 0 || presence.ParentUUID != UUID.Zero)
//                        return "Sitting";

                    string movementAnimation = presence.Animator.CurrentMovementAnimation;
                    string lslMovementAnimation;
                    
                    if (MovementAnimationsForLSL.TryGetValue(movementAnimation, out lslMovementAnimation))
                        return lslMovementAnimation;
                }
            }

            return String.Empty;
        }

        public void llMessageLinked(int linknumber, int num, string msg, string id)
        {
            m_host.AddScriptLPS(1);

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
                                resobj, new DetectParams[0]));
                    }
                }
            }
        }

        public void llPushObject(string target, LSL_Vector impulse, LSL_Vector ang_impulse, int local)
        {
            m_host.AddScriptLPS(1);
            bool pushrestricted = World.RegionInfo.RegionSettings.RestrictPushing;
            bool pushAllowed = false;

            bool pusheeIsAvatar = false;
            UUID targetID = UUID.Zero;

            if (!UUID.TryParse(target,out targetID))
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
                if (avatar.GodLevel > 0 && m_host.OwnerID != targetID)
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
                    if (m_host.OwnerID == targetlandObj.LandData.OwnerID ||
                        targetlandObj.LandData.IsGroupOwned || m_host.OwnerID == targetID)
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
                            if (m_host.OwnerID == targetlandObj.LandData.OwnerID ||
                                targetlandObj.LandData.IsGroupOwned ||
                                m_host.OwnerID == targetID)
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
                float distance_term = distance * distance * distance; // Script Energy
                // use total object mass and not part
                float pusher_mass = m_host.ParentGroup.GetMass();

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
                    float impulse_length = applied_linear_impulse.Length();

                    float desired_energy = impulse_length * pusher_mass;
                    if (desired_energy > 0f)
                        desired_energy += distance_term;

                    float scaling_factor = 1f;
                    scaling_factor *= distance_attenuation;
                    applied_linear_impulse *= scaling_factor;

                }

                if (pusheeIsAvatar)
                {
                    if (pusheeav != null)
                    {
                        PhysicsActor pa = pusheeav.PhysicsActor;

                        if (pa != null)
                        {
                            if (local != 0)
                            {
//                                applied_linear_impulse *= m_host.GetWorldRotation();
                                applied_linear_impulse *= pusheeav.GetWorldRotation();
                            }

                            pa.AddForce(applied_linear_impulse, true);
                        }
                    }
                }
                else
                {
                    if (pusheeob != null)
                    {
                        if (pusheeob.PhysActor != null)
                        {
                            pusheeob.ApplyImpulse(applied_linear_impulse, local != 0);
                        }
                    }
                }
            }
        }

        public void llPassCollisions(int pass)
        {
            m_host.AddScriptLPS(1);
            if (pass == 0)
            {
                m_host.PassCollisions = false;
            }
            else
            {
                m_host.PassCollisions = true;
            }
        }

        public LSL_String llGetScriptName()
        {
            m_host.AddScriptLPS(1);

            return m_item.Name != null ? m_item.Name : String.Empty;
        }

        public LSL_Integer llGetLinkNumberOfSides(int link)
        {
            m_host.AddScriptLPS(1);

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
            m_host.AddScriptLPS(1);

            return GetNumberOfSides(m_host);
        }

        protected int GetNumberOfSides(SceneObjectPart part)
        {
            int sides = part.GetNumberOfSides();

            if (part.GetPrimType() == PrimType.SPHERE && part.Shape.ProfileHollow > 0)
            {
                // Make up for a bug where LSL shows 4 sides rather than 2
                sides += 2;
            }

            return sides;
        }


        /* The new / changed functions were tested with the following LSL script:

        default
        {
            state_entry()
            {
                rotation rot = llEuler2Rot(<0,70,0> * DEG_TO_RAD);

                llOwnerSay("to get here, we rotate over: "+ (string) llRot2Axis(rot));
                llOwnerSay("and we rotate for: "+ (llRot2Angle(rot) * RAD_TO_DEG));

                // convert back and forth between quaternion <-> vector and angle

                rotation newrot = llAxisAngle2Rot(llRot2Axis(rot),llRot2Angle(rot));

                llOwnerSay("Old rotation was: "+(string) rot);
                llOwnerSay("re-converted rotation is: "+(string) newrot);

                llSetRot(rot);  // to check the parameters in the prim
            }
        }
        */

        // Xantor 29/apr/2008
        // Returns rotation described by rotating angle radians about axis.
        // q = cos(a/2) + i (x * sin(a/2)) + j (y * sin(a/2)) + k (z * sin(a/2))
        public LSL_Rotation llAxisAngle2Rot(LSL_Vector axis, double angle)
        {
            m_host.AddScriptLPS(1);

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
            m_host.AddScriptLPS(1);

            if (Math.Abs(rot.s) > 1) // normalization needed
                rot.Normalize();

            double s = Math.Sqrt(1 - rot.s * rot.s);
            if (s < 0.001)
            {
                return new LSL_Vector(1, 0, 0);
            }
            else
            {
                double invS = 1.0 / s;
                if (rot.s < 0) invS = -invS;
                return new LSL_Vector(rot.x * invS, rot.y * invS, rot.z * invS);
            }
        }


        // Returns the angle of a quaternion (see llRot2Axis for the axis)
        public LSL_Float llRot2Angle(LSL_Rotation rot)
        {
            m_host.AddScriptLPS(1);

            if (Math.Abs(rot.s) > 1) // normalization needed
                rot.Normalize();

            double angle = 2 * Math.Acos(rot.s);
            if (angle > Math.PI) 
                angle = 2 * Math.PI - angle;

            return angle;
        }

        public LSL_Float llAcos(double val)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Acos(val);
        }

        public LSL_Float llAsin(double val)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Asin(val);
        }

        // jcochran 5/jan/2012
        public LSL_Float llAngleBetween(LSL_Rotation a, LSL_Rotation b)
        {
            m_host.AddScriptLPS(1);

            double aa = (a.x * a.x + a.y * a.y + a.z * a.z + a.s * a.s);
            double bb = (b.x * b.x + b.y * b.y + b.z * b.z + b.s * b.s);
            double aa_bb = aa * bb;
            if (aa_bb == 0) return 0.0;
            double ab = (a.x * b.x + a.y * b.y + a.z * b.z + a.s * b.s);
            double quotient = (ab * ab) / aa_bb;
            if (quotient >= 1.0) return 0.0;
            return Math.Acos(2 * quotient - 1);
        }

        public LSL_String llGetInventoryKey(string name)
        {
            m_host.AddScriptLPS(1);

            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(name);

            if (item == null)
                return UUID.Zero.ToString();

            if ((item.CurrentPermissions
                 & (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
                    == (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify))
            {
                return item.AssetID.ToString();
            }

            return UUID.Zero.ToString();
        }

        public void llAllowInventoryDrop(int add)
        {
            m_host.AddScriptLPS(1);

            if (add != 0)
                m_host.ParentGroup.RootPart.AllowedDrop = true;
            else
                m_host.ParentGroup.RootPart.AllowedDrop = false;

            // Update the object flags
            m_host.ParentGroup.RootPart.aggregateScriptEvents();
        }

        public LSL_Vector llGetSunDirection()
        {
            m_host.AddScriptLPS(1);

            LSL_Vector SunDoubleVector3;
            Vector3 SunFloatVector3;

            // sunPosition estate setting is set in OpenSim.Region.CoreModules.SunModule
            // have to convert from Vector3 (float) to LSL_Vector (double)
            SunFloatVector3 = World.RegionInfo.RegionSettings.SunVector;
            SunDoubleVector3.x = (double)SunFloatVector3.X;
            SunDoubleVector3.y = (double)SunFloatVector3.Y;
            SunDoubleVector3.z = (double)SunFloatVector3.Z;

            return SunDoubleVector3;
        }

        public LSL_Vector llGetTextureOffset(int face)
        {
            m_host.AddScriptLPS(1);
            return GetTextureOffset(m_host, face);
        }

        protected LSL_Vector GetTextureOffset(SceneObjectPart part, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            LSL_Vector offset = new LSL_Vector();
            if (face == ScriptBaseClass.ALL_SIDES)
            {
                face = 0;
            }
            if (face >= 0 && face < GetNumberOfSides(part))
            {
                offset.x = tex.GetFace((uint)face).OffsetU;
                offset.y = tex.GetFace((uint)face).OffsetV;
                offset.z = 0.0;
                return offset;
            }
            else
            {
                return offset;
            }
        }

        public LSL_Vector llGetTextureScale(int side)
        {
            m_host.AddScriptLPS(1);
            Primitive.TextureEntry tex = m_host.Shape.Textures;
            LSL_Vector scale;
            if (side == -1)
            {
                side = 0;
            }
            scale.x = tex.GetFace((uint)side).RepeatU;
            scale.y = tex.GetFace((uint)side).RepeatV;
            scale.z = 0.0;
            return scale;
        }

        public LSL_Float llGetTextureRot(int face)
        {
            m_host.AddScriptLPS(1);
            return GetTextureRot(m_host, face);
        }

        protected LSL_Float GetTextureRot(SceneObjectPart part, int face)
        {
            Primitive.TextureEntry tex = part.Shape.Textures;
            if (face == -1)
            {
                face = 0;
            }
            if (face >= 0 && face < GetNumberOfSides(part))
            {
                return tex.GetFace((uint)face).Rotation;
            }
            else
            {
                return 0.0;
            }
        }

        public LSL_Integer llSubStringIndex(string source, string pattern)
        {
            m_host.AddScriptLPS(1);
            return source.IndexOf(pattern);
        }

        public LSL_String llGetOwnerKey(string id)
        {
            m_host.AddScriptLPS(1);
            UUID key = new UUID();
            if (UUID.TryParse(id, out key))
            {
                try
                {
                    SceneObjectPart obj = World.GetSceneObjectPart(key);
                    if (obj == null)
                        return id; // the key is for an agent so just return the key
                    else
                        return obj.OwnerID.ToString();
                }
                catch (KeyNotFoundException)
                {
                    return id; // The Object/Agent not in the region so just return the key
                }
            }
            else
            {
                return UUID.Zero.ToString();
            }
        }

        public LSL_Vector llGetCenterOfMass()
        {
            m_host.AddScriptLPS(1);

            return new LSL_Vector(m_host.GetCenterOfMass());
        }

        public LSL_List llListSort(LSL_List src, int stride, int ascending)
        {
            m_host.AddScriptLPS(1);

            if (stride <= 0)
            {
                stride = 1;
            }
            return src.Sort(stride, ascending);
        }

        public LSL_Integer llGetListLength(LSL_List src)
        {
            m_host.AddScriptLPS(1);

            return src.Length;
        }

        public LSL_Integer llList2Integer(LSL_List src, int index)
        {
            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }
            if (index >= src.Length || index < 0)
            {
                return 0;
            }

            // Vectors & Rotations always return zero in SL, but
            //  keys don't always return zero, it seems to be a bit complex.
            else if (src.Data[index] is LSL_Vector ||
                    src.Data[index] is LSL_Rotation)
            {
                return 0;
            }
            try
            {

                if (src.Data[index] is LSL_Integer)
                    return (LSL_Integer)src.Data[index];
                else if (src.Data[index] is LSL_Float)
                    return Convert.ToInt32(((LSL_Float)src.Data[index]).value);
                return new LSL_Integer(src.Data[index].ToString());
            }
            catch (FormatException)
            {
                return 0;
            }
        }

        public LSL_Float llList2Float(LSL_List src, int index)
        {
            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }
            if (index >= src.Length || index < 0)
            {
                return 0.0;
            }

            // Vectors & Rotations always return zero in SL
            else if (src.Data[index] is LSL_Vector ||
                    src.Data[index] is LSL_Rotation)
            {
                return 0;
            }
            // valid keys seem to get parsed as integers then converted to floats
            else
            {
                UUID uuidt;
                if (src.Data[index] is LSL_Key && UUID.TryParse(src.Data[index].ToString(), out uuidt))
                {
                    return Convert.ToDouble(new LSL_Integer(src.Data[index].ToString()).value);
                }
            }
            try
            {
                if (src.Data[index] is LSL_Integer)
                    return Convert.ToDouble(((LSL_Integer)src.Data[index]).value);
                else if (src.Data[index] is LSL_Float)
                    return Convert.ToDouble(((LSL_Float)src.Data[index]).value);
                else if (src.Data[index] is LSL_String)
                {
                    string str = ((LSL_String) src.Data[index]).m_string;
                    Match m = Regex.Match(str, "^\\s*(-?\\+?[,0-9]+\\.?[0-9]*)");
                    if (m != Match.Empty)
                    {
                        str = m.Value;
                        double d = 0.0;
                        if (!Double.TryParse(str, out d))
                            return 0.0;

                        return d;
                    }
                    return 0.0;
                }
                return Convert.ToDouble(src.Data[index]);
            }
            catch (FormatException)
            {
                return 0.0;
            }
        }

        public LSL_String llList2String(LSL_List src, int index)
        {
            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }
            if (index >= src.Length || index < 0)
            {
                return String.Empty;
            }
            return src.Data[index].ToString();
        }

        public LSL_Key llList2Key(LSL_List src, int index)
        {
            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }

            if (index >= src.Length || index < 0)
            {
                return "";
            }

            // SL spits out an empty string for types other than key & string
            // At the time of patching, LSL_Key is currently LSL_String,
            // so the OR check may be a little redundant, but it's being done
            // for completion and should LSL_Key ever be implemented
            // as it's own struct
            // NOTE: 3rd case is needed because a NULL_KEY comes through as
            // type 'obj' and wrongly returns ""
            else if (!(src.Data[index] is LSL_String ||
                       src.Data[index] is LSL_Key ||
                       src.Data[index].ToString() == "00000000-0000-0000-0000-000000000000"))
            {
                return "";
            }

            return src.Data[index].ToString();
        }

        public LSL_Vector llList2Vector(LSL_List src, int index)
        {
            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }
            if (index >= src.Length || index < 0)
            {
                return new LSL_Vector(0, 0, 0);
            }
            if (src.Data[index].GetType() == typeof(LSL_Vector))
            {
                return (LSL_Vector)src.Data[index];
            }

            // SL spits always out ZERO_VECTOR for anything other than
            // strings or vectors. Although keys always return ZERO_VECTOR,
            // it is currently difficult to make the distinction between
            // a string, a key as string and a string that by coincidence
            // is a string, so we're going to leave that up to the
            // LSL_Vector constructor.
            else if (!(src.Data[index] is LSL_String ||
                    src.Data[index] is LSL_Vector))
            {
                return new LSL_Vector(0, 0, 0);
            }
            else
            {
                return new LSL_Vector(src.Data[index].ToString());
            }
        }

        public LSL_Rotation llList2Rot(LSL_List src, int index)
        {
            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }
            if (index >= src.Length || index < 0)
            {
                return new LSL_Rotation(0, 0, 0, 1);
            }

            // SL spits always out ZERO_ROTATION for anything other than
            // strings or vectors. Although keys always return ZERO_ROTATION,
            // it is currently difficult to make the distinction between
            // a string, a key as string and a string that by coincidence
            // is a string, so we're going to leave that up to the
            // LSL_Rotation constructor.
            else if (!(src.Data[index] is LSL_String ||
                    src.Data[index] is LSL_Rotation))
            {
                return new LSL_Rotation(0, 0, 0, 1);
            }
            else if (src.Data[index].GetType() == typeof(LSL_Rotation))
            {
                return (LSL_Rotation)src.Data[index];
            }
            else
            {
                return new LSL_Rotation(src.Data[index].ToString());
            }
        }

        public LSL_List llList2List(LSL_List src, int start, int end)
        {
            m_host.AddScriptLPS(1);
            return src.GetSublist(start, end);
        }

        public LSL_List llDeleteSubList(LSL_List src, int start, int end)
        {
            return src.DeleteSublist(start, end);
        }

        public LSL_Integer llGetListEntryType(LSL_List src, int index)
        {
            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }
            if (index >= src.Length)
            {
                return 0;
            }

            if (src.Data[index] is LSL_Integer || src.Data[index] is Int32)
                return 1;
            if (src.Data[index] is LSL_Float || src.Data[index] is Single || src.Data[index] is Double)
                return 2;
            if (src.Data[index] is LSL_String || src.Data[index] is String)
            {
                UUID tuuid;
                if (UUID.TryParse(src.Data[index].ToString(), out tuuid))
                {
                    return 4;
                }
                else
                {
                    return 3;
                }
            }
            if (src.Data[index] is LSL_Vector)
                return 5;
            if (src.Data[index] is LSL_Rotation)
                return 6;
            if (src.Data[index] is LSL_List)
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
            m_host.AddScriptLPS(1);

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

            LSL_List result = new LSL_List();
            int parens = 0;
            int start  = 0;
            int length = 0;

            m_host.AddScriptLPS(1);

            for (int i = 0; i < src.Length; i++)
            {
                switch (src[i])
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
            BetterRandom rand = new BetterRandom();

            int   chunkk;
            int[] chunks;

            m_host.AddScriptLPS(1);

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
                    int index = rand.Next(i + 1);

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

        /// <summary>
        /// Elements in the source list starting with 0 and then
        /// every i+stride. If the stride is negative then the scan
        /// is backwards producing an inverted result.
        /// Only those elements that are also in the specified
        /// range are included in the result.
        /// </summary>

        public LSL_List llList2ListStrided(LSL_List src, int start, int end, int stride)
        {

            LSL_List result = new LSL_List();
            int[] si = new int[2];
            int[] ei = new int[2];
            bool twopass = false;

            m_host.AddScriptLPS(1);

            //  First step is always to deal with negative indices

            if (start < 0)
                start = src.Length+start;
            if (end   < 0)
                end   = src.Length+end;

            //  Out of bounds indices are OK, just trim them
            //  accordingly

            if (start > src.Length)
                start = src.Length;

            if (end > src.Length)
                end = src.Length;

            if (stride == 0)
                stride = 1;

            //  There may be one or two ranges to be considered

            if (start != end)
            {

                if (start <= end)
                {
                   si[0] = start;
                   ei[0] = end;
                }
                else
                {
                   si[1] = start;
                   ei[1] = src.Length;
                   si[0] = 0;
                   ei[0] = end;
                   twopass = true;
                }

                //  The scan always starts from the beginning of the
                //  source list, but members are only selected if they
                //  fall within the specified sub-range. The specified
                //  range values are inclusive.
                //  A negative stride reverses the direction of the
                //  scan producing an inverted list as a result.

                if (stride > 0)
                {
                    for (int i = 0; i < src.Length; i += stride)
                    {
                        if (i<=ei[0] && i>=si[0])
                            result.Add(src.Data[i]);
                        if (twopass && i>=si[1] && i<=ei[1])
                            result.Add(src.Data[i]);
                    }
                }
                else if (stride < 0)
                {
                    for (int i = src.Length - 1; i >= 0; i += stride)
                    {
                        if (i <= ei[0] && i >= si[0])
                            result.Add(src.Data[i]);
                        if (twopass && i >= si[1] && i <= ei[1])
                            result.Add(src.Data[i]);
                    }
                }
            }
            else
            {
                if (start%stride == 0)
                {
                    result.Add(src.Data[start]);
                }
            }

            return result;
        }

        public LSL_Integer llGetRegionAgentCount()
        {
            m_host.AddScriptLPS(1);

            int count = 0;
            World.ForEachRootScenePresence(delegate(ScenePresence sp) {
                count++;
            });

            return new LSL_Integer(count);
        }

        public LSL_Vector llGetRegionCorner()
        {
            m_host.AddScriptLPS(1);
            return new LSL_Vector(World.RegionInfo.WorldLocX, World.RegionInfo.WorldLocY, 0);
        }

        public LSL_String llGetEnv(LSL_String name)
        {
            m_host.AddScriptLPS(1);
            if (name == "agent_limit")
            {
                return World.RegionInfo.RegionSettings.AgentLimit.ToString();
            }
            else if (name == "dynamic_pathfinding")
            {
                return "0";
            }
            else if (name == "estate_id")
            {
                return World.RegionInfo.EstateSettings.EstateID.ToString();
            }
            else if (name == "estate_name")
            {
                return World.RegionInfo.EstateSettings.EstateName;
            }
            else if (name == "frame_number")
            {
                return World.Frame.ToString();
            }
            else if (name == "region_cpu_ratio")
            {
                return "1";
            }
            else if (name == "region_idle")
            {
                return "0";
            }
            else if (name == "region_product_name")
            {
                if (World.RegionInfo.RegionType != String.Empty)
                    return World.RegionInfo.RegionType;
                else
                    return "";
            }
            else if (name == "region_product_sku")
            {
                return "OpenSim";
            }
            else if (name == "region_start_time")
            {
                return World.UnixStartTime.ToString();
            }
            else if (name == "sim_channel")
            {
                return "OpenSim";
            }
            else if (name == "sim_version")
            {
                return World.GetSimulatorVersion();
            }
            else if (name == "simulator_hostname")
            {
                IUrlModule UrlModule = World.RequestModuleInterface<IUrlModule>();
                return UrlModule.ExternalHostNameForLSL;
            }
            else
            {
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

            m_host.AddScriptLPS(1);

            if (index < 0)
            {
                index = index+dest.Length;
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
        public LSL_Integer llListFindList(LSL_List src, LSL_List test)
        {
            int index  = -1;
            int length = src.Length - test.Length + 1;

            m_host.AddScriptLPS(1);

            // If either list is empty, do not match
            if (src.Length != 0 && test.Length != 0)
            {
                for (int i = 0; i < length; i++)
                {
                    int needle = llGetListEntryType(test, 0).value;
                    int haystack = llGetListEntryType(src, i).value;

                    // Why this piece of insanity?  This is because most script constants are C# value types (e.g. int)
                    // rather than wrapped LSL types.  Such a script constant does not have int.Equal(LSL_Integer) code
                    // and so the comparison fails even if the LSL_Integer conceptually has the same value.
                    // Therefore, here we test Equals on both the source and destination objects.
                    // However, a future better approach may be use LSL struct script constants (e.g. LSL_Integer(1)).
                    if ((needle == haystack) && (src.Data[i].Equals(test.Data[0]) || test.Data[0].Equals(src.Data[i])))
                    {
                        int j;
                        for (j = 1; j < test.Length; j++)
                        {
                            needle = llGetListEntryType(test, j).value;
                            haystack = llGetListEntryType(src, i+j).value;

                            if ((needle != haystack) || (!(src.Data[i+j].Equals(test.Data[j]) || test.Data[j].Equals(src.Data[i+j]))))
                                break;
                        }

                        if (j == test.Length)
                        {
                            index = i;
                            break;
                        }
                    }
                }
            }

            return index;
        }

        public LSL_String llGetObjectName()
        {
            m_host.AddScriptLPS(1);
            return m_host.Name !=null ? m_host.Name : String.Empty;
        }

        public void llSetObjectName(string name)
        {
            m_host.AddScriptLPS(1);
            m_host.Name = name != null ? name : String.Empty;
        }

        public LSL_String llGetDate()
        {
            m_host.AddScriptLPS(1);
            DateTime date = DateTime.Now.ToUniversalTime();
            string result = date.ToString("yyyy-MM-dd");
            return result;
        }

        public LSL_Integer llEdgeOfWorld(LSL_Vector pos, LSL_Vector dir)
        {
            m_host.AddScriptLPS(1);

            // edge will be used to pass the Region Coordinates offset
            // we want to check for a neighboring sim
            LSL_Vector edge = new LSL_Vector(0, 0, 0);

            if (dir.x == 0)
            {
                if (dir.y == 0)
                {
                    // Direction vector is 0,0 so return
                    // false since we're staying in the sim
                    return 0;
                }
                else
                {
                    // Y is the only valid direction
                    edge.y = dir.y / Math.Abs(dir.y);
                }
            }
            else
            {
                LSL_Float mag;
                if (dir.x > 0)
                {
                    mag = (World.RegionInfo.RegionSizeX - pos.x) / dir.x;
                }
                else
                {
                    mag = (pos.x/dir.x);
                }

                mag = Math.Abs(mag);

                edge.y = pos.y + (dir.y * mag);

                if (edge.y > World.RegionInfo.RegionSizeY || edge.y < 0)
                {
                    // Y goes out of bounds first
                    edge.y = dir.y / Math.Abs(dir.y);
                }
                else
                {
                    // X goes out of bounds first or its a corner exit
                    edge.y = 0;
                    edge.x = dir.x / Math.Abs(dir.x);
                }
            }

            List<GridRegion> neighbors = World.GridService.GetNeighbours(World.RegionInfo.ScopeID, World.RegionInfo.RegionID);

            uint neighborX = World.RegionInfo.RegionLocX + (uint)dir.x;
            uint neighborY = World.RegionInfo.RegionLocY + (uint)dir.y;

            foreach (GridRegion sri in neighbors)
            {
                if (sri.RegionCoordX == neighborX && sri.RegionCoordY == neighborY)
                    return 0;
            }

            return 1;
        }

        /// <summary>
        /// Not fully implemented yet. Still to do:-
        /// AGENT_BUSY
        /// Remove as they are done
        /// </summary>
        public LSL_Integer llGetAgentInfo(string id)
        {
            m_host.AddScriptLPS(1);

            UUID key = new UUID();
            if (!UUID.TryParse(id, out key))
            {
                return 0;
            }

            int flags = 0;

            ScenePresence agent = World.GetScenePresence(key);
            if (agent == null)
            {
                return 0;
            }

            if (agent.IsChildAgent)
                return 0; // Fail if they are not in the same region

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

            UUID busy = new UUID("efcf670c-2d18-8128-973a-034ebc806b67");
            UUID[] anims = agent.Animator.GetAnimationArray();
            if (Array.Exists<UUID>(anims, a => { return a == busy; }))
            {
                flags |= ScriptBaseClass.AGENT_BUSY;
            }

            // seems to get unset, even if in mouselook, when avatar is sitting on a prim???
            if ((agent.AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_MOUSELOOK) != 0)
            {
                flags |= ScriptBaseClass.AGENT_MOUSELOOK;
            }

            if ((agent.State & (byte)AgentState.Typing) != (byte)0)
            {
                flags |= ScriptBaseClass.AGENT_TYPING;
            }

            string agentMovementAnimation = agent.Animator.CurrentMovementAnimation;

            if (agentMovementAnimation == "CROUCH")
            {
                flags |= ScriptBaseClass.AGENT_CROUCHING;
            }

            if (agentMovementAnimation == "WALK" || agentMovementAnimation == "CROUCHWALK")
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

             if (agent.Animator.Animations.ImplicitDefaultAnimation.AnimID
                == DefaultAvatarAnimations.AnimsUUID["SIT_GROUND_CONSTRAINED"])
             {
                 flags |= ScriptBaseClass.AGENT_SITTING;
             }

             if (agent.Appearance.VisualParams[(int)AvatarAppearance.VPElement.SHAPE_MALE] > 0)
             {
                 flags |= ScriptBaseClass.AGENT_MALE;
             }

            return flags;
        }

        public LSL_String llGetAgentLanguage(string id)
        {
            // This should only return a value if the avatar is in the same region, but eh. idc.
            m_host.AddScriptLPS(1);
            if (World.AgentPreferencesService == null)
            {
                Error("llGetAgentLanguage", "No AgentPreferencesService present");
            }
            else
            {
                UUID key = new UUID();
                if (UUID.TryParse(id, out key))
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
        /// </summary>
        public LSL_List llGetAgentList(LSL_Integer scope, LSL_List options)
        {
            m_host.AddScriptLPS(1);

            // the constants are 1, 2 and 4 so bits are being set, but you
            // get an error "INVALID_SCOPE" if it is anything but 1, 2 and 4
            bool regionWide = scope == ScriptBaseClass.AGENT_LIST_REGION;
            bool parcelOwned = scope == ScriptBaseClass.AGENT_LIST_PARCEL_OWNER;
            bool parcel = scope == ScriptBaseClass.AGENT_LIST_PARCEL;

            LSL_List result = new LSL_List();

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
                    // Gods are not listed in SL
                    if (!ssp.IsDeleted && ssp.GodLevel == 0.0 && !ssp.IsChildAgent)
                    {
                        if (!regionWide)
                        {
                            land = World.LandChannel.GetLandObject(ssp.AbsolutePosition);
                            if (land != null)
                            {
                                if (parcelOwned && land.LandData.OwnerID == id ||
                                    parcel && land.LandData.GlobalID == id)
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

        public void llAdjustSoundVolume(double volume)
        {
            m_host.AddScriptLPS(1);
            m_host.AdjustSoundGain(volume);
            ScriptSleep(m_sleepMsOnAdjustSoundVolume);
        }

        public void llSetSoundRadius(double radius)
        {
            m_host.AddScriptLPS(1);
            m_host.SoundRadius = radius;
        }

        public LSL_String llKey2Name(string id)
        {
            m_host.AddScriptLPS(1);
            UUID key = new UUID();
            if (UUID.TryParse(id,out key))
            {
                ScenePresence presence = World.GetScenePresence(key);

                if (presence != null)
                {
                    return presence.ControllingClient.Name;
                    //return presence.Name;
                }

                if (World.GetSceneObjectPart(key) != null)
                {
                    return World.GetSceneObjectPart(key).Name;
                }
            }
            return String.Empty;
        }



        public void llSetTextureAnim(int mode, int face, int sizex, int sizey, double start, double length, double rate)
        {
            m_host.AddScriptLPS(1);

            SetTextureAnim(m_host, mode, face, sizex, sizey, start, length, rate);
        }

        public void llSetLinkTextureAnim(int linknumber, int mode, int face, int sizex, int sizey, double start, double length, double rate)
        {
            m_host.AddScriptLPS(1);

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

        private void SetTextureAnim(SceneObjectPart part, int mode, int face, int sizex, int sizey, double start, double length, double rate)
        {

            Primitive.TextureAnimation pTexAnim = new Primitive.TextureAnimation();
            pTexAnim.Flags = (Primitive.TextureAnimMode)mode;

            //ALL_SIDES
            if (face == ScriptBaseClass.ALL_SIDES)
                face = 255;

            pTexAnim.Face = (uint)face;
            pTexAnim.Length = (float)length;
            pTexAnim.Rate = (float)rate;
            pTexAnim.SizeX = (uint)sizex;
            pTexAnim.SizeY = (uint)sizey;
            pTexAnim.Start = (float)start;

            part.AddTextureAnimation(pTexAnim);
            part.SendFullUpdateToAllClients();
            part.ParentGroup.HasGroupChanged = true;
        }

        public void llTriggerSoundLimited(string sound, double volume, LSL_Vector top_north_east,
                                          LSL_Vector bottom_south_west)
        {
            m_host.AddScriptLPS(1);
            if (m_SoundModule != null)
            {
                m_SoundModule.TriggerSoundLimited(m_host.UUID,
                        ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, sound, AssetType.Sound), volume,
                        bottom_south_west, top_north_east);
            }
        }

        public void llEjectFromLand(string pest)
        {
            m_host.AddScriptLPS(1);
            UUID agentID = new UUID();
            if (UUID.TryParse(pest, out agentID))
            {
                ScenePresence presence = World.GetScenePresence(agentID);
                if (presence != null)
                {
                    // agent must be over the owners land
                    ILandObject land = World.LandChannel.GetLandObject(presence.AbsolutePosition);
                    if (land == null)
                        return;

                    if (m_host.OwnerID == land.LandData.OwnerID)
                    {
                        Vector3 p = World.GetNearestAllowedPosition(presence, land);
                        presence.TeleportWithMomentum(p, null);
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
            m_host.AddScriptLPS(1);
            UUID key = new UUID();
            if (UUID.TryParse(id, out key))
            {
                ScenePresence presence = World.GetScenePresence(key);
                if (presence != null) // object is an avatar
                {
                    if (m_host.OwnerID == World.LandChannel.GetLandObject(presence.AbsolutePosition).LandData.OwnerID)
                        return 1;
                }
                else // object is not an avatar
                {
                    SceneObjectPart obj = World.GetSceneObjectPart(key);

                    if (obj != null)
                    {
                        if (m_host.OwnerID == World.LandChannel.GetLandObject(obj.AbsolutePosition).LandData.OwnerID)
                            return 1;
                    }
                }
            }

            return 0;
        }

        public LSL_String llGetLandOwnerAt(LSL_Vector pos)
        {
            m_host.AddScriptLPS(1);
            ILandObject land = World.LandChannel.GetLandObject((float)pos.x, (float)pos.y);
            if (land == null)
                return UUID.Zero.ToString();
            return land.LandData.OwnerID.ToString();
        }

        /// <summary>
        /// According to http://lslwiki.net/lslwiki/wakka.php?wakka=llGetAgentSize
        /// only the height of avatars vary and that says:
        /// Width (x) and depth (y) are constant. (0.45m and 0.6m respectively).
        /// </summary>
        public LSL_Vector llGetAgentSize(string id)
        {
            m_host.AddScriptLPS(1);
            ScenePresence avatar = World.GetScenePresence((UUID)id);
            LSL_Vector agentSize;
            if (avatar == null || avatar.IsChildAgent) // Fail if not in the same region
            {
                agentSize = ScriptBaseClass.ZERO_VECTOR;
            }
            else
            {
//                agentSize = new LSL_Vector(0.45f, 0.6f, avatar.Appearance.AvatarHeight);
                Vector3 s = avatar.Appearance.AvatarSize;
                agentSize = new LSL_Vector(s.X, s.Y, s.Z);
            }
            return agentSize;
        }

        public LSL_Integer llSameGroup(string id)
        {
            m_host.AddScriptLPS(1);
            UUID uuid = new UUID();
            if (!UUID.TryParse(id, out uuid))
                return new LSL_Integer(0);

            // Check if it's a group key
            if (uuid == m_host.ParentGroup.RootPart.GroupID)
                return new LSL_Integer(1);

            // We got passed a UUID.Zero
            if (uuid == UUID.Zero)
                return new LSL_Integer(0);

            // Handle the case where id names an avatar
            ScenePresence presence = World.GetScenePresence(uuid);
            if (presence != null)
            {
                if (presence.IsChildAgent)
                    return new LSL_Integer(0);

                IClientAPI client = presence.ControllingClient;
                if (m_host.ParentGroup.RootPart.GroupID == client.ActiveGroupId)
                    return new LSL_Integer(1);

                return new LSL_Integer(0);
            }

            // Handle object case
            SceneObjectPart part = World.GetSceneObjectPart(uuid);
            if (part != null)
            {
                // This will handle both deed and non-deed and also the no
                // group case
                if (part.ParentGroup.RootPart.GroupID == m_host.ParentGroup.RootPart.GroupID)
                    return new LSL_Integer(1);

                return new LSL_Integer(0);
            }

            return new LSL_Integer(0);
        }

        public void llUnSit(string id)
        {
            m_host.AddScriptLPS(1);

            UUID key = new UUID();
            if (UUID.TryParse(id, out key))
            {
                ScenePresence av = World.GetScenePresence(key);
                List<ScenePresence> sittingAvatars = m_host.ParentGroup.GetSittingAvatars();

                if (av != null)
                {
                    if (sittingAvatars.Contains(av))
                    {
                        // if the avatar is sitting on this object, then
                        // we can unsit them.  We don't want random scripts unsitting random people
                        // Lets avoid the popcorn avatar scenario.
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
                            if (m_host.OwnerID == parcel.LandData.OwnerID ||
                                (m_host.OwnerID == m_host.GroupID && m_host.GroupID == parcel.LandData.GroupID
                                && parcel.LandData.IsGroupOwned) || World.Permissions.IsGod(m_host.OwnerID))
                            {
                                av.StandUp();
                            }
                        }
                    }
                }
            }
        }

        public LSL_Vector llGroundSlope(LSL_Vector offset)
        {
            m_host.AddScriptLPS(1);

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
            m_host.AddScriptLPS(1);
            Vector3 pos = m_host.GetWorldPosition() + (Vector3)offset;
            // Clamp to valid position
            if (pos.X < 0)
                pos.X = 0;
            else if (pos.X >= World.Heightmap.Width)
                pos.X = World.Heightmap.Width - 1;
            if (pos.Y < 0)
                pos.Y = 0;
            else if (pos.Y >= World.Heightmap.Height)
                pos.Y = World.Heightmap.Height - 1;

            //Find two points in addition to the position to define a plane
            Vector3 p0 = new Vector3(pos.X, pos.Y,
                                     (float)World.Heightmap[(int)pos.X, (int)pos.Y]);
            Vector3 p1 = new Vector3();
            Vector3 p2 = new Vector3();
            if ((pos.X + 1.0f) >= World.Heightmap.Width)
                p1 = new Vector3(pos.X + 1.0f, pos.Y,
                            (float)World.Heightmap[(int)pos.X, (int)pos.Y]);
            else
                p1 = new Vector3(pos.X + 1.0f, pos.Y,
                            (float)World.Heightmap[(int)(pos.X + 1.0f), (int)pos.Y]);
            if ((pos.Y + 1.0f) >= World.Heightmap.Height)
                p2 = new Vector3(pos.X, pos.Y + 1.0f,
                            (float)World.Heightmap[(int)pos.X, (int)pos.Y]);
            else
                p2 = new Vector3(pos.X, pos.Y + 1.0f,
                            (float)World.Heightmap[(int)pos.X, (int)(pos.Y + 1.0f)]);

            //Find normalized vectors from p0 to p1 and p0 to p2
            Vector3 v0 = new Vector3(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
            Vector3 v1 = new Vector3(p2.X - p0.X, p2.Y - p0.Y, p2.Z - p0.Z);
            v0.Normalize();
            v1.Normalize();

            //Find the cross product of the vectors (the slope normal).
            Vector3 vsn = new Vector3();
            vsn.X = (v0.Y * v1.Z) - (v0.Z * v1.Y);
            vsn.Y = (v0.Z * v1.X) - (v0.X * v1.Z);
            vsn.Z = (v0.X * v1.Y) - (v0.Y * v1.X);
            vsn.Normalize();
            //I believe the crossproduct of two normalized vectors is a normalized vector so
            //this normalization may be overkill

            return new LSL_Vector(vsn);
        }

        public LSL_Vector llGroundContour(LSL_Vector offset)
        {
            m_host.AddScriptLPS(1);
            LSL_Vector x = llGroundSlope(offset);
            return new LSL_Vector(-x.y, x.x, 0.0);
        }

        public LSL_Integer llGetAttached()
        {
            m_host.AddScriptLPS(1);
            return m_host.ParentGroup.AttachmentPoint;
        }

        public virtual LSL_Integer llGetFreeMemory()
        {
            m_host.AddScriptLPS(1);
            // Make scripts designed for Mono happy
            return 65536;
        }

        public LSL_Integer llGetFreeURLs()
        {
            m_host.AddScriptLPS(1);
            if (m_UrlModule != null)
                return new LSL_Integer(m_UrlModule.GetFreeUrls());
            return new LSL_Integer(0);
        }


        public LSL_String llGetRegionName()
        {
            m_host.AddScriptLPS(1);
            return World.RegionInfo.RegionName;
        }

        public LSL_Float llGetRegionTimeDilation()
        {
            m_host.AddScriptLPS(1);
            return (double)World.TimeDilation;
        }

        /// <summary>
        /// Returns the value reported in the client Statistics window
        /// </summary>
        public LSL_Float llGetRegionFPS()
        {
            m_host.AddScriptLPS(1);
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

        internal Primitive.ParticleSystem.ParticleDataFlags ConvertUINTtoFlags(uint flags)
        {
            Primitive.ParticleSystem.ParticleDataFlags returnval = Primitive.ParticleSystem.ParticleDataFlags.None;

            return returnval;
        }

        protected Primitive.ParticleSystem getNewParticleSystemWithSLDefaultValues()
        {
            Primitive.ParticleSystem ps = new Primitive.ParticleSystem();

            // TODO find out about the other defaults and add them here
            ps.PartStartColor = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
            ps.PartEndColor = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
            ps.PartStartScaleX = 1.0f;
            ps.PartStartScaleY = 1.0f;
            ps.PartEndScaleX = 1.0f;
            ps.PartEndScaleY = 1.0f;
            ps.BurstSpeedMin = 1.0f;
            ps.BurstSpeedMax = 1.0f;
            ps.BurstRate = 0.1f;
            ps.PartMaxAge = 10.0f;
            ps.BurstPartCount = 1;
            ps.BlendFuncSource = ScriptBaseClass.PSYS_PART_BF_SOURCE_ALPHA;
            ps.BlendFuncDest = ScriptBaseClass.PSYS_PART_BF_ONE_MINUS_SOURCE_ALPHA;
            ps.PartStartGlow = 0.0f;
            ps.PartEndGlow = 0.0f;

            return ps;
        }

        public void llLinkParticleSystem(int linknumber, LSL_List rules)
        {
            m_host.AddScriptLPS(1);

            List<SceneObjectPart> parts = GetLinkParts(linknumber);

            foreach (SceneObjectPart part in parts)
            {
                SetParticleSystem(part, rules, "llLinkParticleSystem");
            }
        }

        public void llParticleSystem(LSL_List rules)
        {
            m_host.AddScriptLPS(1);
            SetParticleSystem(m_host, rules, "llParticleSystem");
        }

        private void SetParticleSystem(SceneObjectPart part, LSL_List rules, string originFunc)
        {
            if (rules.Length == 0)
            {
                part.RemoveParticleSystem();
                part.ParentGroup.HasGroupChanged = true;
            }
            else
            {
                Primitive.ParticleSystem prules = getNewParticleSystemWithSLDefaultValues();
                LSL_Vector tempv = new LSL_Vector();

                float tempf = 0;
                int tmpi = 0;

                for (int i = 0; i < rules.Length; i += 2)
                {
                    int psystype;
                    try
                    {
                        psystype = rules.GetLSLIntegerItem(i);
                    }
                    catch (InvalidCastException)
                    {
                        Error(originFunc, string.Format("Error running particle system params index #{0}: particle system parameter type must be integer", i));
                        return;
                    }
                    switch (psystype)
                    {
                        case (int)ScriptBaseClass.PSYS_PART_FLAGS:
                            try
                            {
                                prules.PartDataFlags = (Primitive.ParticleSystem.ParticleDataFlags)(uint)rules.GetLSLIntegerItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_FLAGS: arg #{0} - parameter 1 must be integer", i + 1));
                                return;
                            }
                            break;

                        case (int)ScriptBaseClass.PSYS_PART_START_COLOR:
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

                        case (int)ScriptBaseClass.PSYS_PART_START_ALPHA:
                            try
                            {
                                tempf = (float)rules.GetLSLFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_START_ALPHA: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.PartStartColor.A = tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_PART_END_COLOR:
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

                        case (int)ScriptBaseClass.PSYS_PART_END_ALPHA:
                            try
                            {
                                tempf = (float)rules.GetLSLFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_END_ALPHA: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.PartEndColor.A = tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_PART_START_SCALE:
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

                        case (int)ScriptBaseClass.PSYS_PART_END_SCALE:
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

                        case (int)ScriptBaseClass.PSYS_PART_MAX_AGE:
                            try
                            {
                                tempf = (float)rules.GetLSLFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_MAX_AGE: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.PartMaxAge = tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_ACCEL:
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

                        case (int)ScriptBaseClass.PSYS_SRC_PATTERN:
                            try
                            {
                                tmpi = (int)rules.GetLSLIntegerItem(i + 1);
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
                        case (int)ScriptBaseClass.PSYS_SRC_INNERANGLE:
                            try
                            {
                                tempf = (float)rules.GetLSLFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_INNERANGLE: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.InnerAngle = (float)tempf;
                            prules.PartFlags &= 0xFFFFFFFD; // Make sure new angle format is off.
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_OUTERANGLE:
                            try
                            {
                                tempf = (float)rules.GetLSLFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_OUTERANGLE: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.OuterAngle = (float)tempf;
                            prules.PartFlags &= 0xFFFFFFFD; // Make sure new angle format is off.
                            break;

                        case (int)ScriptBaseClass.PSYS_PART_BLEND_FUNC_SOURCE:
                            try
                            {
                                tmpi = (int)rules.GetLSLIntegerItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_BLEND_FUNC_SOURCE: arg #{0} - parameter 1 must be integer", i + 1));
                                return;
                            }
                            prules.BlendFuncSource = (byte)tmpi;
                            break;

                        case (int)ScriptBaseClass.PSYS_PART_BLEND_FUNC_DEST:
                            try
                            {
                                tmpi = (int)rules.GetLSLIntegerItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_BLEND_FUNC_DEST: arg #{0} - parameter 1 must be integer", i + 1));
                                return;
                            }
                            prules.BlendFuncDest = (byte)tmpi;
                            break;

                        case (int)ScriptBaseClass.PSYS_PART_START_GLOW:
                            try
                            {
                                tempf = (float)rules.GetLSLFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_START_GLOW: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.PartStartGlow = (float)tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_PART_END_GLOW:
                            try
                            {
                                tempf = (float)rules.GetLSLFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_PART_END_GLOW: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.PartEndGlow = (float)tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_TEXTURE:
                            try
                            {
                                prules.Texture = ScriptUtils.GetAssetIdFromKeyOrItemName(m_host, rules.GetLSLStringItem(i + 1));
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_TEXTURE: arg #{0} - parameter 1 must be string or key", i + 1));
                                return;
                            }
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_BURST_RATE:
                            try
                            {
                                tempf = (float)rules.GetLSLFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_BURST_RATE: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.BurstRate = (float)tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_BURST_PART_COUNT:
                            try
                            {
                                prules.BurstPartCount = (byte)(int)rules.GetLSLIntegerItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_BURST_PART_COUNT: arg #{0} - parameter 1 must be integer", i + 1));
                                return;
                            }
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_BURST_RADIUS:
                            try
                            {
                                tempf = (float)rules.GetLSLFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_BURST_RADIUS: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.BurstRadius = (float)tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_BURST_SPEED_MIN:
                            try
                            {
                                tempf = (float)rules.GetLSLFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_BURST_SPEED_MIN: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.BurstSpeedMin = (float)tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_BURST_SPEED_MAX:
                            try
                            {
                                tempf = (float)rules.GetLSLFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_BURST_SPEED_MAX: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.BurstSpeedMax = (float)tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_MAX_AGE:
                            try
                            {
                                tempf = (float)rules.GetLSLFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_MAX_AGE: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.MaxAge = (float)tempf;
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_TARGET_KEY:
                            UUID key = UUID.Zero;
                            if (UUID.TryParse(rules.Data[i + 1].ToString(), out key))
                            {
                                prules.Target = key;
                            }
                            else
                            {
                                prules.Target = part.UUID;
                            }
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_OMEGA:
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

                        case (int)ScriptBaseClass.PSYS_SRC_ANGLE_BEGIN:
                            try
                            {
                                tempf = (float)rules.GetLSLFloatItem(i + 1);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_ANGLE_BEGIN: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.InnerAngle = (float)tempf;
                            prules.PartFlags |= 0x02; // Set new angle format.
                            break;

                        case (int)ScriptBaseClass.PSYS_SRC_ANGLE_END:
                            try
                            {
                                tempf = (float)rules.GetLSLFloatItem(i + 1);
                            }
                            catch (InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule PSYS_SRC_ANGLE_END: arg #{0} - parameter 1 must be float", i + 1));
                                return;
                            }
                            prules.OuterAngle = (float)tempf;
                            prules.PartFlags |= 0x02; // Set new angle format.
                            break;
                    }

                }
                prules.CRC = 1;

                part.AddNewParticleSystem(prules);
                part.ParentGroup.HasGroupChanged = true;
            }
            part.SendFullUpdateToAllClients();
        }

        private float validParticleScale(float value)
        {
            if (value > 4.0f) return 4.0f;
            return value;
        }

        public void llGroundRepel(double height, int water, double tau)
        {
            m_host.AddScriptLPS(1);
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

        public void llGiveInventoryList(string destination, string category, LSL_List inventory)
        {
            m_host.AddScriptLPS(1);

            UUID destID;
            if (!UUID.TryParse(destination, out destID))
                return;

            List<UUID> itemList = new List<UUID>();

            foreach (Object item in inventory.Data)
            {
                string rawItemString = item.ToString();

                UUID itemID;
                if (UUID.TryParse(rawItemString, out itemID))
                {
                    itemList.Add(itemID);
                }
                else
                {
                    TaskInventoryItem taskItem = m_host.Inventory.GetInventoryItem(rawItemString);

                    if (taskItem != null)
                        itemList.Add(taskItem.ItemID);
                }
            }

            if (itemList.Count == 0)
                return;

            UUID folderID = m_ScriptEngine.World.MoveTaskInventoryItems(destID, category, m_host, itemList);

            if (folderID == UUID.Zero)
                return;

            if (m_TransferModule != null)
            {
                byte[] bucket = new byte[] { (byte)AssetType.Folder };

                Vector3 pos = m_host.AbsolutePosition;

                GridInstantMessage msg = new GridInstantMessage(World,
                        m_host.OwnerID, m_host.Name, destID,
                        (byte)InstantMessageDialog.TaskInventoryOffered,
                        false, string.Format("'{0}'", category),
// We won't go so far as to add a SLURL, but this is the format used by LL as of 2012-10-06
// false, string.Format("'{0}'  ( http://slurl.com/secondlife/{1}/{2}/{3}/{4} )", category, World.Name, (int)pos.X, (int)pos.Y, (int)pos.Z),
                        folderID, false, pos,
                        bucket, false);

                m_TransferModule.SendInstantMessage(msg, delegate(bool success) {});
            }
        }

        public void llSetVehicleType(int type)
        {
            m_host.AddScriptLPS(1);

            if (!m_host.ParentGroup.IsDeleted)
            {
                m_host.ParentGroup.RootPart.SetVehicleType(type);
            }
        }

        //CFK 9/28: Most, but not all of the underlying plumbing between here and the physics modules is in
        //CFK 9/28: so these are not complete yet.
        public void llSetVehicleFloatParam(int param, LSL_Float value)
        {
            m_host.AddScriptLPS(1);

            if (!m_host.ParentGroup.IsDeleted)
            {
                m_host.ParentGroup.RootPart.SetVehicleFloatParam(param, (float)value);
            }
        }

        //CFK 9/28: Most, but not all of the underlying plumbing between here and the physics modules is in
        //CFK 9/28: so these are not complete yet.
        public void llSetVehicleVectorParam(int param, LSL_Vector vec)
        {
            m_host.AddScriptLPS(1);

            if (!m_host.ParentGroup.IsDeleted)
            {
                m_host.ParentGroup.RootPart.SetVehicleVectorParam(param, vec);
            }
        }

        //CFK 9/28: Most, but not all of the underlying plumbing between here and the physics modules is in
        //CFK 9/28: so these are not complete yet.
        public void llSetVehicleRotationParam(int param, LSL_Rotation rot)
        {
            m_host.AddScriptLPS(1);

            if (!m_host.ParentGroup.IsDeleted)
            {
                m_host.ParentGroup.RootPart.SetVehicleRotationParam(param, rot);
            }
        }

        public void llSetVehicleFlags(int flags)
        {
            m_host.AddScriptLPS(1);

            if (!m_host.ParentGroup.IsDeleted)
            {
                m_host.ParentGroup.RootPart.SetVehicleFlags(flags, false);
            }
        }

        public void llRemoveVehicleFlags(int flags)
        {
            m_host.AddScriptLPS(1);

            if (!m_host.ParentGroup.IsDeleted)
            {
                m_host.ParentGroup.RootPart.SetVehicleFlags(flags, true);
            }
        }

        protected void SitTarget(SceneObjectPart part, LSL_Vector offset, LSL_Rotation rot)
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
            m_host.AddScriptLPS(1);
            SitTarget(m_host, offset, rot);
        }

        public void llLinkSitTarget(LSL_Integer link, LSL_Vector offset, LSL_Rotation rot)
        {
            m_host.AddScriptLPS(1);
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

        public LSL_String llAvatarOnSitTarget()
        {
            m_host.AddScriptLPS(1);
            return m_host.SitTargetAvatar.ToString();
        }

        // http://wiki.secondlife.com/wiki/LlAvatarOnLinkSitTarget
        public LSL_String llAvatarOnLinkSitTarget(int linknum)
        {
            m_host.AddScriptLPS(1);
            if(linknum == ScriptBaseClass.LINK_SET ||
                    linknum == ScriptBaseClass.LINK_ALL_CHILDREN ||
                    linknum == ScriptBaseClass.LINK_ALL_OTHERS ||
                    linknum == 0)
                return UUID.Zero.ToString();

            List<SceneObjectPart> parts = GetLinkParts(linknum);
            if (parts.Count == 0)
                return UUID.Zero.ToString();
            return parts[0].SitTargetAvatar.ToString();
        }


        public void llAddToLandPassList(string avatar, double hours)
        {
            m_host.AddScriptLPS(1);
            UUID key;
            ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition);

            if (World.Permissions.CanEditParcelProperties(m_host.OwnerID, land, GroupPowers.LandManageBanned, false))
            {
                int expires = 0;
                if (hours != 0)
                    expires = Util.UnixTimeSinceEpoch() + (int)(3600.0 * hours);

                if (UUID.TryParse(avatar, out key))
                {
                    int idx = land.LandData.ParcelAccessList.FindIndex(
                            delegate(LandAccessEntry e)
                            {
                                if (e.AgentID == key && e.Flags == AccessList.Access)
                                    return true;
                                return false;
                            });

                    if (idx != -1 && (land.LandData.ParcelAccessList[idx].Expires == 0 || (expires != 0 && expires < land.LandData.ParcelAccessList[idx].Expires)))
                        return;

                    if (idx != -1)
                        land.LandData.ParcelAccessList.RemoveAt(idx);

                    LandAccessEntry entry = new LandAccessEntry();

                    entry.AgentID = key;
                    entry.Flags = AccessList.Access;
                    entry.Expires = expires;

                    land.LandData.ParcelAccessList.Add(entry);

                    World.EventManager.TriggerLandObjectUpdated((uint)land.LandData.LocalID, land);
                }
            }
            ScriptSleep(m_sleepMsOnAddToLandPassList);
        }

        public void llSetTouchText(string text)
        {
            m_host.AddScriptLPS(1);
            m_host.TouchName = text;
        }

        public void llSetSitText(string text)
        {
            m_host.AddScriptLPS(1);
            m_host.SitName = text;
        }

        public void llSetCameraEyeOffset(LSL_Vector offset)
        {
            m_host.AddScriptLPS(1);
            m_host.SetCameraEyeOffset(offset);

            if (m_host.ParentGroup.RootPart.GetCameraEyeOffset() == Vector3.Zero)
                m_host.ParentGroup.RootPart.SetCameraEyeOffset(offset);
        }

        public void llSetCameraAtOffset(LSL_Vector offset)
        {
            m_host.AddScriptLPS(1);
            m_host.SetCameraAtOffset(offset);

            if (m_host.ParentGroup.RootPart.GetCameraAtOffset() == Vector3.Zero)
                m_host.ParentGroup.RootPart.SetCameraAtOffset(offset);
        }

        public void llSetLinkCamera(LSL_Integer link, LSL_Vector eye, LSL_Vector at)
        {
            m_host.AddScriptLPS(1);

            if (link == ScriptBaseClass.LINK_SET ||
                link == ScriptBaseClass.LINK_ALL_CHILDREN ||
                link == ScriptBaseClass.LINK_ALL_OTHERS) return;

            SceneObjectPart part = null;

            switch (link)
            {
                case ScriptBaseClass.LINK_ROOT:
                    part = m_host.ParentGroup.RootPart;
                    break;
                case ScriptBaseClass.LINK_THIS:
                    part = m_host;
                    break;
                default:
                    part = m_host.ParentGroup.GetLinkNumPart(link);
                    break;
            }

            if (null != part)
            {
                part.SetCameraEyeOffset(eye);
                part.SetCameraAtOffset(at);
            }
        }

        public LSL_String llDumpList2String(LSL_List src, string seperator)
        {
            m_host.AddScriptLPS(1);
            if (src.Length == 0)
            {
                return String.Empty;
            }
            string ret = String.Empty;
            foreach (object o in src.Data)
            {
                ret = ret + o.ToString() + seperator;
            }
            ret = ret.Substring(0, ret.Length - seperator.Length);
            return ret;
        }

        public LSL_Integer llScriptDanger(LSL_Vector pos)
        {
            m_host.AddScriptLPS(1);
            bool result = World.ScriptDanger(m_host.LocalId, pos);
            if (result)
            {
                return 1;
            }
            else
            {
                return 0;
            }

        }

        public void llDialog(string avatar, string message, LSL_List buttons, int chat_channel)
        {
            IDialogModule dm = World.RequestModuleInterface<IDialogModule>();

            if (dm == null)
                return;

            m_host.AddScriptLPS(1);
            UUID av = new UUID();
            if (!UUID.TryParse(avatar,out av))
            {
                Error("llDialog", "First parameter must be a key");
                return;
            }

            int length = buttons.Length;
            if (length < 1)
            {
                Error("llDialog", "At least 1 button must be shown");
                return;
            }
            if (length > 12)
            {
                Error("llDialog", "No more than 12 buttons can be shown");
                return;
            }

            if (message == string.Empty)
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
                if (buttons.Data[i].ToString() == String.Empty)
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
                av, m_host.Name, m_host.UUID, m_host.OwnerID,
                message, new UUID("00000000-0000-2222-3333-100000001000"), chat_channel, buts);

            ScriptSleep(m_sleepMsOnDialog);
        }

        public void llVolumeDetect(int detect)
        {
            m_host.AddScriptLPS(1);

            if (!m_host.ParentGroup.IsDeleted)
                m_host.ParentGroup.ScriptSetVolumeDetect(detect != 0);
        }

        public void llRemoteLoadScript(string target, string name, int running, int start_param)
        {
            m_host.AddScriptLPS(1);
            Deprecated("llRemoteLoadScript", "Use llRemoteLoadScriptPin instead");
            ScriptSleep(m_sleepMsOnRemoteLoadScript);
        }

        public void llSetRemoteScriptAccessPin(int pin)
        {
            m_host.AddScriptLPS(1);
            m_host.ScriptAccessPin = pin;
        }

        public void llRemoteLoadScriptPin(string target, string name, int pin, int running, int start_param)
        {
            m_host.AddScriptLPS(1);

            UUID destId = UUID.Zero;

            if (!UUID.TryParse(target, out destId))
            {
                Error("llRemoteLoadScriptPin", "Can't parse key '" + target + "'");
                return;
            }

            // target must be a different prim than the one containing the script
            if (m_host.UUID == destId)
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

            SceneObjectPart dest = World.GetSceneObjectPart(destId);
            if (dest != null)
            {
                if ((item.BasePermissions & (uint)PermissionMask.Transfer) != 0 || dest.ParentGroup.RootPart.OwnerID == m_host.ParentGroup.RootPart.OwnerID)
                {
                    // the rest of the permission checks are done in RezScript, so check the pin there as well
                    World.RezScriptFromPrim(item.ItemID, m_host, destId, pin, running, start_param);

                    if ((item.BasePermissions & (uint)PermissionMask.Copy) == 0)
                        m_host.Inventory.RemoveInventoryItem(item.ItemID);
                }
            }
            // this will cause the delay even if the script pin or permissions were wrong - seems ok
            ScriptSleep(m_sleepMsOnRemoteLoadScriptPin);
        }

        public void llOpenRemoteDataChannel()
        {
            m_host.AddScriptLPS(1);
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
                        new LSL_String(UUID.Zero.ToString()),
                        new LSL_String(String.Empty),
                        new LSL_Integer(0),
                        new LSL_String(String.Empty)
                    };
                m_ScriptEngine.PostScriptEvent(m_item.ItemID, new EventParams("remote_data", resobj,
                                                                         new DetectParams[0]));
            }
            ScriptSleep(m_sleepMsOnOpenRemoteDataChannel);
        }

        public LSL_String llSendRemoteData(string channel, string dest, int idata, string sdata)
        {
            m_host.AddScriptLPS(1);
            IXMLRPC xmlrpcMod = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            ScriptSleep(m_sleepMsOnSendRemoteData);
            if (xmlrpcMod == null)
                return "";
            return (xmlrpcMod.SendRemoteData(m_host.LocalId, m_item.ItemID, channel, dest, idata, sdata)).ToString();
        }

        public void llRemoteDataReply(string channel, string message_id, string sdata, int idata)
        {
            m_host.AddScriptLPS(1);
            IXMLRPC xmlrpcMod = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            if (xmlrpcMod != null)
                xmlrpcMod.RemoteDataReply(channel, message_id, sdata, idata);
            ScriptSleep(m_sleepMsOnRemoteDataReply);
        }

        public void llCloseRemoteDataChannel(string channel)
        {
            m_host.AddScriptLPS(1);

            IXmlRpcRouter xmlRpcRouter = m_ScriptEngine.World.RequestModuleInterface<IXmlRpcRouter>();
            if (xmlRpcRouter != null)
            {
                xmlRpcRouter.UnRegisterReceiver(channel, m_item.ItemID);
            }

            IXMLRPC xmlrpcMod = m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            if (xmlrpcMod != null)
                xmlrpcMod.CloseXMLRPCChannel((UUID)channel);
            ScriptSleep(m_sleepMsOnCloseRemoteDataChannel);
        }

        public LSL_String llMD5String(string src, int nonce)
        {
            m_host.AddScriptLPS(1);
            return Util.Md5Hash(String.Format("{0}:{1}", src, nonce.ToString()), Encoding.UTF8);
        }

        public LSL_String llSHA1String(string src)
        {
            m_host.AddScriptLPS(1);
            return Util.SHA1Hash(src, Encoding.UTF8).ToLower();
        }

        protected ObjectShapePacket.ObjectDataBlock SetPrimitiveBlockShapeParams(SceneObjectPart part, int holeshape, LSL_Vector cut, float hollow, LSL_Vector twist, byte profileshape, byte pathcurve)
        {
            float tempFloat;                                    // Use in float expressions below to avoid byte cast precision issues.
            ObjectShapePacket.ObjectDataBlock shapeBlock = new ObjectShapePacket.ObjectDataBlock();
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return shapeBlock;

            if (holeshape != (int)ScriptBaseClass.PRIM_HOLE_DEFAULT &&
                holeshape != (int)ScriptBaseClass.PRIM_HOLE_CIRCLE &&
                holeshape != (int)ScriptBaseClass.PRIM_HOLE_SQUARE &&
                holeshape != (int)ScriptBaseClass.PRIM_HOLE_TRIANGLE)
            {
                holeshape = (int)ScriptBaseClass.PRIM_HOLE_DEFAULT;
            }
            shapeBlock.PathCurve = pathcurve;
            shapeBlock.ProfileCurve = (byte)holeshape;          // Set the hole shape.
            shapeBlock.ProfileCurve += profileshape;            // Add in the profile shape.
            if (cut.x < 0f)
            {
                cut.x = 0f;
            }
            if (cut.x > 1f)
            {
                cut.x = 1f;
            }
            if (cut.y < 0f)
            {
                cut.y = 0f;
            }
            if (cut.y > 1f)
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
                holeshape == (int)ScriptBaseClass.PRIM_HOLE_SQUARE)
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
            if (twist.x > 1.0f)
            {
                twist.x = 1.0f;
            }
            if (twist.y < -1.0f)
            {
                twist.y = -1.0f;
            }
            if (twist.y > 1.0f)
            {
                twist.y = 1.0f;
            }
            // A fairly large precision error occurs for some calculations,
            // if a float or double is directly cast to a byte or sbyte
            // variable, in both .Net and Mono. In .Net, coding
            // "(sbyte)(float)(some expression)" corrects the precision
            // errors. But this does not work for Mono. This longer coding
            // form of creating a tempoary float variable from the
            // expression first, then casting that variable to a byte or
            // sbyte, works for both .Net and Mono. These types of
            // assignments occur in SetPrimtiveBlockShapeParams and
            // SetPrimitiveShapeParams in support of llSetPrimitiveParams.
            tempFloat = (float)(100.0d * twist.x);
            shapeBlock.PathTwistBegin = (sbyte)tempFloat;
            tempFloat = (float)(100.0d * twist.y);
            shapeBlock.PathTwist = (sbyte)tempFloat;

            shapeBlock.ObjectLocalID = part.LocalId;

            part.Shape.SculptEntry = false;
            return shapeBlock;
        }

        // Prim type box, cylinder and prism.
        protected void SetPrimitiveShapeParams(SceneObjectPart part, int holeshape, LSL_Vector cut, float hollow, LSL_Vector twist, LSL_Vector taper_b, LSL_Vector topshear, byte profileshape, byte pathcurve)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            float tempFloat;                                    // Use in float expressions below to avoid byte cast precision issues.
            ObjectShapePacket.ObjectDataBlock shapeBlock;

            shapeBlock = SetPrimitiveBlockShapeParams(part, holeshape, cut, hollow, twist, profileshape, pathcurve);

            if (taper_b.x < 0f)
            {
                taper_b.x = 0f;
            }
            if (taper_b.x > 2f)
            {
                taper_b.x = 2f;
            }
            if (taper_b.y < 0f)
            {
                taper_b.y = 0f;
            }
            if (taper_b.y > 2f)
            {
                taper_b.y = 2f;
            }
            tempFloat = (float)(100.0d * (2.0d - taper_b.x));
            shapeBlock.PathScaleX = (byte)tempFloat;
            tempFloat = (float)(100.0d * (2.0d - taper_b.y));
            shapeBlock.PathScaleY = (byte)tempFloat;
            if (topshear.x < -0.5f)
            {
                topshear.x = -0.5f;
            }
            if (topshear.x > 0.5f)
            {
                topshear.x = 0.5f;
            }
            if (topshear.y < -0.5f)
            {
                topshear.y = -0.5f;
            }
            if (topshear.y > 0.5f)
            {
                topshear.y = 0.5f;
            }
            tempFloat = (float)(100.0d * topshear.x);
            shapeBlock.PathShearX = (byte)tempFloat;
            tempFloat = (float)(100.0d * topshear.y);
            shapeBlock.PathShearY = (byte)tempFloat;

            part.Shape.SculptEntry = false;
            part.UpdateShape(shapeBlock);
        }

        // Prim type sphere.
        protected void SetPrimitiveShapeParams(SceneObjectPart part, int holeshape, LSL_Vector cut, float hollow, LSL_Vector twist, LSL_Vector dimple, byte profileshape, byte pathcurve)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
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
            if (dimple.x > 1f)
            {
                dimple.x = 1f;
            }
            if (dimple.y < 0f)
            {
                dimple.y = 0f;
            }
            if (dimple.y > 1f)
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
        protected void SetPrimitiveShapeParams(SceneObjectPart part, int holeshape, LSL_Vector cut, float hollow, LSL_Vector twist, LSL_Vector holesize, LSL_Vector topshear, LSL_Vector profilecut, LSL_Vector taper_a, float revolutions, float radiusoffset, float skew, byte profileshape, byte pathcurve)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            float tempFloat;                                    // Use in float expressions below to avoid byte cast precision issues.
            ObjectShapePacket.ObjectDataBlock shapeBlock;

            shapeBlock = SetPrimitiveBlockShapeParams(part, holeshape, cut, hollow, twist, profileshape, pathcurve);

            // profile/path swapped for a torrus, tube, ring
            shapeBlock.PathBegin = shapeBlock.ProfileBegin;
            shapeBlock.PathEnd = shapeBlock.ProfileEnd;

            if (holesize.x < 0.01f)
            {
                holesize.x = 0.01f;
            }
            if (holesize.x > 1f)
            {
                holesize.x = 1f;
            }
            if (holesize.y < 0.01f)
            {
                holesize.y = 0.01f;
            }
            if (holesize.y > 0.5f)
            {
                holesize.y = 0.5f;
            }
            tempFloat = (float)(100.0d * (2.0d - holesize.x));
            shapeBlock.PathScaleX = (byte)tempFloat;
            tempFloat = (float)(100.0d * (2.0d - holesize.y));
            shapeBlock.PathScaleY = (byte)tempFloat;
            if (topshear.x < -0.5f)
            {
                topshear.x = -0.5f;
            }
            if (topshear.x > 0.5f)
            {
                topshear.x = 0.5f;
            }
            if (topshear.y < -0.5f)
            {
                topshear.y = -0.5f;
            }
            if (topshear.y > 0.5f)
            {
                topshear.y = 0.5f;
            }
            tempFloat = (float)(100.0d * topshear.x);
            shapeBlock.PathShearX = (byte)tempFloat;
            tempFloat = (float)(100.0d * topshear.y);
            shapeBlock.PathShearY = (byte)tempFloat;
            if (profilecut.x < 0f)
            {
                profilecut.x = 0f;
            }
            if (profilecut.x > 1f)
            {
                profilecut.x = 1f;
            }
            if (profilecut.y < 0f)
            {
                profilecut.y = 0f;
            }
            if (profilecut.y > 1f)
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
            if (taper_a.y < -1f)
            {
                taper_a.y = -1f;
            }
            if (taper_a.y > 1f)
            {
                taper_a.y = 1f;
            }
            tempFloat = (float)(100.0d * taper_a.x);
            shapeBlock.PathTaperX = (sbyte)tempFloat;
            tempFloat = (float)(100.0d * taper_a.y);
            shapeBlock.PathTaperY = (sbyte)tempFloat;
            if (revolutions < 1f)
            {
                revolutions = 1f;
            }
            if (revolutions > 4f)
            {
                revolutions = 4f;
            }
            tempFloat = 66.66667f * (revolutions - 1.0f);
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
            tempFloat = 100.0f * radiusoffset;
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
            shapeBlock.PathSkew = (sbyte)tempFloat;

            part.Shape.SculptEntry = false;
            part.UpdateShape(shapeBlock);
        }

        // Prim type sculpt.
        protected void SetPrimitiveShapeParams(SceneObjectPart part, string map, int type, byte pathcurve)
        {
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return;

            ObjectShapePacket.ObjectDataBlock shapeBlock = new ObjectShapePacket.ObjectDataBlock();
            UUID sculptId;

            if (!UUID.TryParse(map, out sculptId))
                sculptId = ScriptUtils.GetAssetIdFromItemName(m_host, map, (int)AssetType.Texture);

            if (sculptId == UUID.Zero)
                return;

            shapeBlock.PathCurve = pathcurve;
            shapeBlock.ObjectLocalID = part.LocalId;
            shapeBlock.PathScaleX = 100;
            shapeBlock.PathScaleY = 150;

            int flag = type & (ScriptBaseClass.PRIM_SCULPT_FLAG_INVERT | ScriptBaseClass.PRIM_SCULPT_FLAG_MIRROR);

            if (type != (ScriptBaseClass.PRIM_SCULPT_TYPE_CYLINDER | flag) &&
                type != (ScriptBaseClass.PRIM_SCULPT_TYPE_PLANE | flag) &&
                type != (ScriptBaseClass.PRIM_SCULPT_TYPE_SPHERE | flag) &&
                type != (ScriptBaseClass.PRIM_SCULPT_TYPE_TORUS | flag))
            {
                // default
                type = type | (int)ScriptBaseClass.PRIM_SCULPT_TYPE_SPHERE;
            }

            part.Shape.SetSculptProperties((byte)type, sculptId);
            part.Shape.SculptEntry = true;
            part.UpdateShape(shapeBlock);
        }

        public void llSetPrimitiveParams(LSL_List rules)
        {
            m_host.AddScriptLPS(1);

            SetLinkPrimParams(ScriptBaseClass.LINK_THIS, rules, "llSetPrimitiveParams");

            ScriptSleep(m_sleepMsOnSetPrimitiveParams);
        }

        public void llSetLinkPrimitiveParams(int linknumber, LSL_List rules)
        {
            m_host.AddScriptLPS(1);

            SetLinkPrimParams(linknumber, rules, "llSetLinkPrimitiveParams");

            ScriptSleep(m_sleepMsOnSetLinkPrimitiveParams);
        }

        public void llSetLinkPrimitiveParamsFast(int linknumber, LSL_List rules)
        {
            m_host.AddScriptLPS(1);

            SetLinkPrimParams(linknumber, rules, "llSetLinkPrimitiveParamsFast");
        }

        private void SetLinkPrimParams(int linknumber, LSL_List rules, string originFunc)
        {
            List<object> parts = new List<object>();
            List<SceneObjectPart> prims = GetLinkParts(linknumber);
            List<ScenePresence> avatars = GetLinkAvatars(linknumber);
            foreach (SceneObjectPart p in prims)
                parts.Add(p);
            foreach (ScenePresence p in avatars)
                parts.Add(p);

            LSL_List remaining = new LSL_List();
            uint rulesParsed = 0;

            if (parts.Count > 0)
            {
                foreach (object part in parts)
                {
                    if (part is SceneObjectPart)
                        remaining = SetPrimParams((SceneObjectPart)part, rules, originFunc, ref rulesParsed);
                    else
                        remaining = SetPrimParams((ScenePresence)part, rules, originFunc, ref rulesParsed);
                }

                while (remaining.Length > 2)
                {
                    linknumber = remaining.GetLSLIntegerItem(0);
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
                        if (part is SceneObjectPart)
                            remaining = SetPrimParams((SceneObjectPart)part, rules, originFunc, ref rulesParsed);
                        else
                            remaining = SetPrimParams((ScenePresence)part, rules, originFunc, ref rulesParsed);
                    }
                }
            }
        }

        protected void SetEntityParams(List<ISceneEntity> entities, LSL_List rules, string originFunc)
        {
            LSL_List remaining = new LSL_List();
            uint rulesParsed = 0;

            foreach (ISceneEntity entity in entities)
            {
                if (entity is SceneObjectPart)
                    remaining = SetPrimParams((SceneObjectPart)entity, rules, originFunc, ref rulesParsed);
                else
                    remaining = SetAgentParams((ScenePresence)entity, rules, originFunc, ref rulesParsed);
            }

            while (remaining.Length > 2)
            {
                int linknumber;
                try
                {
                    linknumber = remaining.GetLSLIntegerItem(0);
                }
                catch(InvalidCastException)
                {
                    Error(originFunc, string.Format("Error running rule #{0} -> PRIM_LINK_TARGET: parameter 2 must be integer", rulesParsed));
                    return;
                }

                rules = remaining.GetSublist(1, -1);
                entities = GetLinkEntities(linknumber);

                foreach (ISceneEntity entity in entities)
                {
                    if (entity is SceneObjectPart)
                        remaining = SetPrimParams((SceneObjectPart)entity, rules, originFunc, ref rulesParsed);
                    else
                        remaining = SetAgentParams((ScenePresence)entity, rules, originFunc, ref rulesParsed);
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
                if (group.RootPart.KeyframeMotion != null)
                    group.RootPart.KeyframeMotion.Delete();
                group.RootPart.KeyframeMotion = null;

                int idx = 0;

                KeyframeMotion.PlayMode mode = KeyframeMotion.PlayMode.Forward;
                KeyframeMotion.DataFormat data = KeyframeMotion.DataFormat.Translation | KeyframeMotion.DataFormat.Rotation;

                while (idx < options.Data.Length)
                {
                    int option = (int)options.GetLSLIntegerItem(idx++);
                    int remain = options.Data.Length - idx;

                    switch (option)
                    {
                        case ScriptBaseClass.KFM_MODE:
                            if (remain < 1)
                                break;
                            int modeval = (int)options.GetLSLIntegerItem(idx++);
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
                            int dataval = (int)options.GetLSLIntegerItem(idx++);
                            data = (KeyframeMotion.DataFormat)dataval;
                            break;
                    }
                }

                group.RootPart.KeyframeMotion = new KeyframeMotion(group, mode, data);

                idx = 0;

                int elemLength = 2;
                if (data == (KeyframeMotion.DataFormat.Translation | KeyframeMotion.DataFormat.Rotation))
                    elemLength = 3;

                List<KeyframeMotion.Keyframe> keyframes = new List<KeyframeMotion.Keyframe>();
                while (idx < frames.Data.Length)
                {
                    int remain = frames.Data.Length - idx;

                    if (remain < elemLength)
                        break;

                    KeyframeMotion.Keyframe frame = new KeyframeMotion.Keyframe();
                    frame.Position = null;
                    frame.Rotation = null;

                    if ((data & KeyframeMotion.DataFormat.Translation) != 0)
                    {
                        LSL_Types.Vector3 tempv = frames.GetVector3Item(idx++);
                        frame.Position = new Vector3((float)tempv.x, (float)tempv.y, (float)tempv.z);
                    }
                    if ((data & KeyframeMotion.DataFormat.Rotation) != 0)
                    {
                        LSL_Types.Quaternion tempq = frames.GetQuaternionItem(idx++);
                        Quaternion q = new Quaternion((float)tempq.x, (float)tempq.y, (float)tempq.z, (float)tempq.s);
                        q.Normalize();
                        frame.Rotation = q;
                    }

                    float tempf = (float)frames.GetLSLFloatItem(idx++);
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
                    int option = (int)options.GetLSLIntegerItem(idx++);

                    switch (option)
                    {
                        case ScriptBaseClass.KFM_COMMAND:
                            int cmd = (int)options.GetLSLIntegerItem(idx++);
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
            LSL_List result = new LSL_List();

            result.Add(new LSL_Float(m_host.GravityModifier));
            result.Add(new LSL_Float(m_host.Restitution));
            result.Add(new LSL_Float(m_host.Friction));
            result.Add(new LSL_Float(m_host.Density));

            return result;
        }

        private void SetPhysicsMaterial(SceneObjectPart part, int material_bits,
                float material_density, float material_friction,
                float material_restitution, float material_gravity_modifier)
        {
            ExtraPhysicsData physdata = new ExtraPhysicsData();
            physdata.PhysShapeType = (PhysShapeType)part.PhysicsShapeType;
            physdata.Density = part.Density;
            physdata.Friction = part.Friction;
            physdata.Bounce = part.Restitution;
            physdata.GravitationModifier = part.GravityModifier;

            if ((material_bits & (int)ScriptBaseClass.DENSITY) != 0)
                physdata.Density = material_density;
            if ((material_bits & (int)ScriptBaseClass.FRICTION) != 0)
                physdata.Friction = material_friction;
            if ((material_bits & (int)ScriptBaseClass.RESTITUTION) != 0)
                physdata.Bounce = material_restitution;
            if ((material_bits & (int)ScriptBaseClass.GRAVITY_MULTIPLIER) != 0)
                physdata.GravitationModifier = material_gravity_modifier;

            part.UpdateExtraPhysics(physdata);
        }

        public void llSetPhysicsMaterial(int material_bits,
                float material_gravity_modifier, float material_restitution,
                float material_friction, float material_density)
        {
            SetPhysicsMaterial(m_host, material_bits, material_density, material_friction, material_restitution, material_gravity_modifier);
        }

        // vector up using libomv (c&p from sop )
        // vector up rotated by r
        private Vector3 Zrot(Quaternion r)
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
            if (part == null || part.ParentGroup == null || part.ParentGroup.IsDeleted)
                return new LSL_List();

            int idx = 0;
            int idxStart = 0;

            SceneObjectGroup parentgrp = part.ParentGroup;

            bool positionChanged = false;
            LSL_Vector currentPosition = GetPartLocalPos(part);

            try
            {
                while (idx < rules.Length)
                {
                    ++rulesParsed;
                    int code = rules.GetLSLIntegerItem(idx++);

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
                                code = (int)rules.GetLSLIntegerItem(idx++);
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
                                        face = (int)rules.GetLSLIntegerItem(idx++);
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
                                        hollow = (float)rules.GetLSLFloatItem(idx++);
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
                                        face = (int)rules.GetLSLIntegerItem(idx++); // holeshape
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
                                        hollow = (float)rules.GetLSLFloatItem(idx++);
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
                                        face = (int)rules.GetLSLIntegerItem(idx++); // holeshape
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
                                        hollow = (float)rules.GetLSLFloatItem(idx++);
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
                                        face = (int)rules.GetLSLIntegerItem(idx++); // holeshape
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
                                        hollow = (float)rules.GetLSLFloatItem(idx++);
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
                                        face = (int)rules.GetLSLIntegerItem(idx++); // holeshape
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
                                        hollow = (float)rules.GetLSLFloatItem(idx++);
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
                                        revolutions = (float)rules.GetLSLFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TORUS: arg #{1} - parameter 11 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        radiusoffset = (float)rules.GetLSLFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TORUS: arg #{1} - parameter 12 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        skew = (float)rules.GetLSLFloatItem(idx++);
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
                                        face = (int)rules.GetLSLIntegerItem(idx++); // holeshape
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
                                        hollow = (float)rules.GetLSLFloatItem(idx++);
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
                                        revolutions = (float)rules.GetLSLFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TUBE: arg #{1} - parameter 11 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        radiusoffset = (float)rules.GetLSLFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_TUBE: arg #{1} - parameter 12 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        skew = (float)rules.GetLSLFloatItem(idx++);
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
                                        face = (int)rules.GetLSLIntegerItem(idx++); // holeshape
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
                                        hollow = (float)rules.GetLSLFloatItem(idx++);
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
                                        revolutions = (float)rules.GetLSLFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_RING: arg #{1} - parameter 11 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        radiusoffset = (float)rules.GetLSLFloatItem(idx++);
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_RING: arg #{1} - parameter 12 must be float", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    try
                                    {
                                        skew = (float)rules.GetLSLFloatItem(idx++);
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
                                        face = (int)rules.GetLSLIntegerItem(idx++); // type
                                    }
                                    catch(InvalidCastException)
                                    {
                                        Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TYPE, PRIM_TYPE_SCULPT: arg #{1} - parameter 4 must be integer", rulesParsed, idx - idxStart - 1));
                                        return new LSL_List();
                                    }
                                    SetPrimitiveShapeParams(part, map, face, (byte)Extrusion.Curve1);
                                    break;
                            }

                            break;

                        case ScriptBaseClass.PRIM_TEXTURE:
                            if (remain < 5)
                                return new LSL_List();

                            face=(int)rules.GetLSLIntegerItem(idx++);
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
                                rotation = (double)rules.GetLSLFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TEXTURE: arg #{1} - parameter 5 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            SetTexture(part, tex, face);
                            ScaleTexture(part, repeats.x, repeats.y, face);
                            OffsetTexture(part, offsets.x, offsets.y, face);
                            RotateTexture(part, rotation, face);

                            break;

                        case ScriptBaseClass.PRIM_COLOR:
                            if (remain < 3)
                                return new LSL_List();

                            LSL_Vector color;
                            double alpha;

                            try
                            {
                                face = (int)rules.GetLSLIntegerItem(idx++);
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
                                alpha = (double)rules.GetLSLFloatItem(idx++);
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
                                flexi = rules.GetLSLIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_FLEXIBLE: arg #{1} - parameter 2 must be integer", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                softness = rules.GetLSLIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_FLEXIBLE: arg #{1} - parameter 3 must be integer", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                gravity = (float)rules.GetLSLFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_FLEXIBLE: arg #{1} - parameter 4 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                friction = (float)rules.GetLSLFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_FLEXIBLE: arg #{1} - parameter 5 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                wind = (float)rules.GetLSLFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_FLEXIBLE: arg #{1} - parameter 6 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                tension = (float)rules.GetLSLFloatItem(idx++);
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
                                light = rules.GetLSLIntegerItem(idx++);
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
                                intensity = (float)rules.GetLSLFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_POINT_LIGHT: arg #{1} - parameter 4 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                radius = (float)rules.GetLSLFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_POINT_LIGHT: arg #{1} - parameter 5 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                falloff = (float)rules.GetLSLFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_POINT_LIGHT: arg #{1} - parameter 6 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            SetPointLight(part, light, lightcolor, intensity, radius, falloff);

                            break;

                        case ScriptBaseClass.PRIM_GLOW:
                            if (remain < 2)
                                return new LSL_List();

                            float glow;

                            try
                            {
                                face = rules.GetLSLIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_GLOW: arg #{1} - parameter 2 must be integer", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                glow = (float)rules.GetLSLFloatItem(idx++);
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
                                face = (int)rules.GetLSLIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_BUMP_SHINY: arg #{1} - parameter 2 must be integer", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                shiny = (int)rules.GetLSLIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_BUMP_SHINY: arg #{1} - parameter 3 must be integer", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                bump = (Bumpiness)(int)rules.GetLSLIntegerItem(idx++);
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
                                face = rules.GetLSLIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_FULLBRIGHT: arg #{1} - parameter 2 must be integer", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }
                            try
                            {
                                st = rules.GetLSLIntegerItem(idx++);
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
                                mat = rules.GetLSLIntegerItem(idx++);
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
                                shape_type = rules.GetLSLIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_PHYSICS_SHAPE_TYPE: arg #{1} - parameter 2 must be integer", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }

                            ExtraPhysicsData physdata = new ExtraPhysicsData();
                            physdata.Density = part.Density;
                            physdata.Bounce = part.Restitution;
                            physdata.GravitationModifier = part.GravityModifier;
                            physdata.PhysShapeType = (PhysShapeType)shape_type;

                            part.UpdateExtraPhysics(physdata);

                            break;

                        case (int)ScriptBaseClass.PRIM_PHYSICS_MATERIAL:
                            if (remain < 5)
                                return new LSL_List();

                            int material_bits = rules.GetLSLIntegerItem(idx++);
                            float material_density = (float)rules.GetLSLFloatItem(idx++);
                            float material_friction = (float)rules.GetLSLFloatItem(idx++);
                            float material_restitution = (float)rules.GetLSLFloatItem(idx++);
                            float material_gravity_modifier = (float)rules.GetLSLFloatItem(idx++);

                            SetPhysicsMaterial(part, material_bits, material_density, material_friction, material_restitution, material_gravity_modifier);

                            break;

                        case (int)ScriptBaseClass.PRIM_TEMP_ON_REZ:
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
                                face = rules.GetLSLIntegerItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TEXGEN: arg #{1} - parameter 2 must be integer", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }
                            try
                            {
                                style = rules.GetLSLIntegerItem(idx++);
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
                            LSL_Float primTextAlpha;

                            try
                            {
                                primText = rules.GetLSLStringItem(idx++);
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
                                primTextAlpha = rules.GetLSLFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                               Error(originFunc, string.Format("Error running rule #{0} -> PRIM_TEXT: arg #{1} - parameter 4 must be float", rulesParsed, idx - idxStart - 1));
                               return new LSL_List();
                            }
                            Vector3 av3 = Util.Clip(primTextColor, 0.0f, 1.0f);
                            part.SetText(primText, av3, Util.Clip((float)primTextAlpha, 0.0f, 1.0f));

                            break;

                        case ScriptBaseClass.PRIM_NAME:
                            if (remain < 1)
                                return new LSL_List();
                            try
                            {
                                string primName = rules.GetLSLStringItem(idx++);
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
                                string primDesc = rules.GetLSLStringItem(idx++);
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
                            LSL_Float spinrate;
                            LSL_Float gain;

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
                                spinrate = rules.GetLSLFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_OMEGA: arg #{1} - parameter 3 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            try
                            {
                                gain = rules.GetLSLFloatItem(idx++);
                            }
                            catch(InvalidCastException)
                            {
                                Error(originFunc, string.Format("Error running rule #{0} -> PRIM_OMEGA: arg #{1} - parameter 4 must be float", rulesParsed, idx - idxStart - 1));
                                return new LSL_List();
                            }
                            TargetOmega(part, axis, (double)spinrate, (double)gain);
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

                        case ScriptBaseClass.PRIM_LINK_TARGET:
                            if (remain < 3) // setting to 3 on the basis that parsing any usage of PRIM_LINK_TARGET that has nothing following it is pointless.
                                return new LSL_List();

                            return rules.GetSublist(idx, -1);

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
            }

            return new LSL_List();
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
                    int code = rules.GetLSLIntegerItem(idx++);

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
            m_host.AddScriptLPS(1);
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
                return String.Empty;
            }
        }

        public LSL_String llBase64ToString(string str)
        {
            m_host.AddScriptLPS(1);
            try
            {
                byte[] b = Convert.FromBase64String(str);
                return Encoding.UTF8.GetString(b);
            }
            catch
            {
                Error("llBase64ToString", "Error decoding string");
                return String.Empty;
            }
        }

        public LSL_String llXorBase64Strings(string str1, string str2)
        {
            int padding = 0;

            string b64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

            ScriptSleep(300);
            m_host.AddScriptLPS(1);

            if (str1 == String.Empty)
                return String.Empty;
            if (str2 == String.Empty)
                return str1;

            int len = str2.Length;
            if ((len % 4) != 0) // LL is EVIL!!!!
            {
                while (str2.EndsWith("="))
                    str2 = str2.Substring(0, str2.Length - 1);

                len = str2.Length;
                int mod = len % 4;

                if (mod == 1)
                    str2 = str2.Substring(0, str2.Length - 1);
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
                return new LSL_String(String.Empty);
            }

            // For cases where the decoded length of s2 is greater
            // than the decoded length of s1, simply perform a normal
            // decode and XOR
            //
            /*
            if (data2.Length >= data1.Length)
            {
                for (int pos = 0 ; pos < data1.Length ; pos++ )
                    data1[pos] ^= data2[pos];

                return Convert.ToBase64String(data1);
            }
            */

            // Remove padding
            while (str1.EndsWith("="))
            {
                str1 = str1.Substring(0, str1.Length - 1);
                padding++;
            }
            while (str2.EndsWith("="))
                str2 = str2.Substring(0, str2.Length - 1);
            
            byte[] d1 = new byte[str1.Length];
            byte[] d2 = new byte[str2.Length];

            for (int i = 0 ; i < str1.Length ; i++)
            {
                int idx = b64.IndexOf(str1.Substring(i, 1));
                if (idx == -1)
                    idx = 0;
                d1[i] = (byte)idx;
            }

            for (int i = 0 ; i < str2.Length ; i++)
            {
                int idx = b64.IndexOf(str2.Substring(i, 1));
                if (idx == -1)
                    idx = 0;
                d2[i] = (byte)idx;
            }

            string output = String.Empty;

            for (int pos = 0 ; pos < d1.Length ; pos++)
                output += b64[d1[pos] ^ d2[pos % d2.Length]];

            // Here's a funny thing: LL blithely violate the base64
            // standard pretty much everywhere. Here, padding is
            // added only if the first input string had it, rather
            // than when the data actually needs it. This can result
            // in invalid base64 being returned. Go figure.

            while (padding-- > 0)
                output += "=";

            return output;
        }

        public void llRemoteDataSetRegion()
        {
            m_host.AddScriptLPS(1);
            Deprecated("llRemoteDataSetRegion", "Use llOpenRemoteDataChannel instead");
        }

        public LSL_Float llLog10(double val)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Log10(val);
        }

        public LSL_Float llLog(double val)
        {
            m_host.AddScriptLPS(1);
            return (double)Math.Log(val);
        }

        public LSL_List llGetAnimationList(string id)
        {
            m_host.AddScriptLPS(1);

            LSL_List l = new LSL_List();
            ScenePresence av = World.GetScenePresence((UUID)id);
            if (av == null || av.IsChildAgent) // only if in the region
                return l;
            UUID[] anims;
            anims = av.Animator.GetAnimationArray();
            foreach (UUID foo in anims)
                l.Add(new LSL_Key(foo.ToString()));
            return l;
        }

        public void llSetParcelMusicURL(string url)
        {
            m_host.AddScriptLPS(1);

            ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition);

            if (land.LandData.OwnerID != m_host.OwnerID)
                return;

            land.SetMusicUrl(url);

            ScriptSleep(m_sleepMsOnSetParcelMusicURL);
        }

        public LSL_String llGetParcelMusicURL()
        {
            m_host.AddScriptLPS(1);

            ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition);

            if (land.LandData.OwnerID != m_host.OwnerID)
                return String.Empty;

            return land.GetMusicUrl();
        }

        public LSL_Vector llGetRootPosition()
        {
            m_host.AddScriptLPS(1);

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
            m_host.AddScriptLPS(1);
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
            return m_host.Description!=null?m_host.Description:String.Empty;
        }

        public void llSetObjectDesc(string desc)
        {
            m_host.AddScriptLPS(1);
            m_host.Description = desc!=null?desc:String.Empty;
        }

        public LSL_String llGetCreator()
        {
            m_host.AddScriptLPS(1);
            return m_host.CreatorID.ToString();
        }

        public LSL_String llGetTimestamp()
        {
            m_host.AddScriptLPS(1);
            return DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
        }

        public LSL_Integer llGetNumberOfPrims()
        {
            m_host.AddScriptLPS(1);

            return m_host.ParentGroup.PrimCount + m_host.ParentGroup.GetSittingAvatarsCount();
        }

        /// <summary>
        /// Full implementation of llGetBoundingBox according to SL 2015-04-15.
        /// http://wiki.secondlife.com/wiki/LlGetBoundingBox
        /// http://lslwiki.net/lslwiki/wakka.php?wakka=llGetBoundingBox
        /// Returns local bounding box of avatar without attachments
        /// if target is non-seated avatar or prim/mesh in avatar attachment.
        /// Returns local bounding box of object including seated avatars
        /// if target is seated avatar or prim/mesh in object.
        /// Uses meshing of prims for high accuracy
        /// or less accurate box models for speed.
        /// </summary>
        public LSL_List llGetBoundingBox(string obj)
        {
            m_host.AddScriptLPS(1);
            UUID objID = UUID.Zero;
            LSL_List result = new LSL_List();

            // If the ID is not valid, return null result
            if (!UUID.TryParse(obj, out objID))
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
                // As per LSL Wiki, there is no difference between sitting
                // and standing avatar since server 1.36
                LSL_Vector lower;
                LSL_Vector upper;

                Vector3 box = presence.Appearance.AvatarBoxSize * 0.5f;

                if (presence.Animator.Animations.ImplicitDefaultAnimation.AnimID 
                    == DefaultAvatarAnimations.AnimsUUID["SIT_GROUND_CONSTRAINED"])
/*
                {
                    // This is for ground sitting avatars
                    float height = presence.Appearance.AvatarHeight / 2.66666667f;
                    lower = new LSL_Vector(-0.3375f, -0.45f, height * -1.0f);
                    upper = new LSL_Vector(0.3375f, 0.45f, 0.0f);
                }
                else
                {
                    // This is for standing/flying avatars
                    float height = presence.Appearance.AvatarHeight / 2.0f;
                    lower = new LSL_Vector(-0.225f, -0.3f, height * -1.0f);
                    upper = new LSL_Vector(0.225f, 0.3f, height + 0.05f);
                }

                // Adjust to the documented error offsets (see LSL Wiki)
                lower += new LSL_Vector(0.05f, 0.05f, 0.05f);
                upper -= new LSL_Vector(0.05f, 0.05f, 0.05f);
*/
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
            if (part != null)
            {
                float minX;
                float maxX;
                float minY;
                float maxY;
                float minZ;
                float maxZ;

                // This BBox is in sim coordinates, with the offset being
                // a contained point.
                Vector3[] offsets = Scene.GetCombinedBoundingBox(new List<SceneObjectGroup> { part.ParentGroup },
                        out minX, out maxX, out minY, out maxY, out minZ, out maxZ);

                minX -= offsets[0].X;
                maxX -= offsets[0].X;
                minY -= offsets[0].Y;
                maxY -= offsets[0].Y;
                minZ -= offsets[0].Z;
                maxZ -= offsets[0].Z;

                LSL_Vector lower;
                LSL_Vector upper;

                // Adjust to the documented error offsets (see LSL Wiki)
                lower = new LSL_Vector(minX + 0.05f, minY + 0.05f, minZ + 0.05f);
                upper = new LSL_Vector(maxX - 0.05f, maxY - 0.05f, maxZ - 0.05f);

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

            // Not found so return empty values
            result.Add(new LSL_Vector());
            result.Add(new LSL_Vector());
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
            else if (sp.Animator.Animations.ImplicitDefaultAnimation.AnimID == DefaultAvatarAnimations.AnimsUUID["SIT_GROUND_CONSTRAINED"])
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
            m_host.AddScriptLPS(1);

            LSL_List result = new LSL_List();

            LSL_List remaining = GetPrimParams(m_host, rules, ref result);

            while ((object)remaining != null && remaining.Length > 2)
            {
                int linknumber = remaining.GetLSLIntegerItem(0);
                rules = remaining.GetSublist(1, -1);
                List<SceneObjectPart> parts = GetLinkParts(linknumber);

                foreach (SceneObjectPart part in parts)
                    remaining = GetPrimParams(part, rules, ref result);
            }

            return result;
        }

        public LSL_List llGetLinkPrimitiveParams(int linknumber, LSL_List rules)
        {
            m_host.AddScriptLPS(1);

            // acording to SL wiki this must indicate a single link number or link_root or link_this.
            // keep other options as before

            List<SceneObjectPart> parts;
            List<ScenePresence> avatars;
            
            LSL_List res = new LSL_List();
            LSL_List remaining = new LSL_List();

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
                    linknumber = remaining.GetLSLIntegerItem(0);
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
            while (idx < rules.Length)
            {
                int code = (int)rules.GetLSLIntegerItem(idx++);
                int remain = rules.Length - idx;

                switch (code)
                {
                    case (int)ScriptBaseClass.PRIM_MATERIAL:
                        res.Add(new LSL_Integer(part.Material));
                        break;

                    case (int)ScriptBaseClass.PRIM_PHYSICS:
                        if ((part.GetEffectiveObjectFlags() & (uint)PrimFlags.Physics) != 0)
                            res.Add(new LSL_Integer(1));
                        else
                            res.Add(new LSL_Integer(0));
                        break;

                    case (int)ScriptBaseClass.PRIM_TEMP_ON_REZ:
                        if ((part.GetEffectiveObjectFlags() & (uint)PrimFlags.TemporaryOnRez) != 0)
                            res.Add(new LSL_Integer(1));
                        else
                            res.Add(new LSL_Integer(0));
                        break;

                    case (int)ScriptBaseClass.PRIM_PHANTOM:
                        if ((part.GetEffectiveObjectFlags() & (uint)PrimFlags.Phantom) != 0)
                            res.Add(new LSL_Integer(1));
                        else
                            res.Add(new LSL_Integer(0));
                        break;

                    case (int)ScriptBaseClass.PRIM_POSITION:
                        LSL_Vector v = new LSL_Vector(part.AbsolutePosition.X,
                                                      part.AbsolutePosition.Y,
                                                      part.AbsolutePosition.Z);
                        res.Add(v);
                        break;

                    case (int)ScriptBaseClass.PRIM_SIZE:
                        res.Add(new LSL_Vector(part.Scale));
                        break;

                    case (int)ScriptBaseClass.PRIM_ROTATION:
                        res.Add(GetPartRot(part));
                        break;

                    case (int)ScriptBaseClass.PRIM_PHYSICS_SHAPE_TYPE:
                        res.Add(new LSL_Integer((int)part.PhysicsShapeType));
                        break;

                    case (int)ScriptBaseClass.PRIM_TYPE:
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
                                res.Add(new LSL_Integer(Shape.SculptType));
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

                    case (int)ScriptBaseClass.PRIM_TEXTURE:
                        if (remain < 1)
                            return new LSL_List();

                        int face = (int)rules.GetLSLIntegerItem(idx++);
                        Primitive.TextureEntry tex = part.Shape.Textures;
                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < GetNumberOfSides(part); face++)
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
                            if (face >= 0 && face < GetNumberOfSides(part))
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

                    case (int)ScriptBaseClass.PRIM_COLOR:
                        if (remain < 1)
                            return new LSL_List();

                        face = (int)rules.GetLSLIntegerItem(idx++);

                        tex = part.Shape.Textures;
                        Color4 texcolor;
                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < GetNumberOfSides(part); face++)
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

                    case (int)ScriptBaseClass.PRIM_BUMP_SHINY:
                        if (remain < 1)
                            return new LSL_List();

                        face = (int)rules.GetLSLIntegerItem(idx++);

                        tex = part.Shape.Textures;
                        int shiny;
                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < GetNumberOfSides(part); face++)
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

                    case (int)ScriptBaseClass.PRIM_FULLBRIGHT:
                        if (remain < 1)
                            return new LSL_List();

                        face = (int)rules.GetLSLIntegerItem(idx++);

                        tex = part.Shape.Textures;
                        int fullbright;
                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < GetNumberOfSides(part); face++)
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

                    case (int)ScriptBaseClass.PRIM_FLEXIBLE:
                        PrimitiveBaseShape shape = part.Shape;

                        if (shape.FlexiEntry)
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

                    case (int)ScriptBaseClass.PRIM_TEXGEN:
                        // (PRIM_TEXGEN_DEFAULT, PRIM_TEXGEN_PLANAR)
                        if (remain < 1)
                            return new LSL_List();

                        face = (int)rules.GetLSLIntegerItem(idx++);

                        tex = part.Shape.Textures;
                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < GetNumberOfSides(part); face++)
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

                    case (int)ScriptBaseClass.PRIM_POINT_LIGHT:
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

                    case (int)ScriptBaseClass.PRIM_GLOW:
                        if (remain < 1)
                            return new LSL_List();

                        face = (int)rules.GetLSLIntegerItem(idx++);

                        tex = part.Shape.Textures;
                        float primglow;
                        if (face == ScriptBaseClass.ALL_SIDES)
                        {
                            for (face = 0; face < GetNumberOfSides(part); face++)
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

                    case (int)ScriptBaseClass.PRIM_TEXT:
                        Color4 textColor = part.GetTextColor();
                        res.Add(new LSL_String(part.Text));
                        res.Add(new LSL_Vector(textColor.R,
                                               textColor.G,
                                               textColor.B));
                        res.Add(new LSL_Float(textColor.A));
                        break;

                    case (int)ScriptBaseClass.PRIM_NAME:
                        res.Add(new LSL_String(part.Name));
                        break;

                    case (int)ScriptBaseClass.PRIM_DESC:
                        res.Add(new LSL_String(part.Description));
                        break;
                    case (int)ScriptBaseClass.PRIM_ROT_LOCAL:
                        res.Add(new LSL_Rotation(part.RotationOffset));
                        break;

                    case (int)ScriptBaseClass.PRIM_POS_LOCAL:
                        res.Add(new LSL_Vector(GetPartLocalPos(part)));
                        break;
                    case (int)ScriptBaseClass.PRIM_SLICE:
                        PrimType prim_type = part.GetPrimType();
                        bool useProfileBeginEnd = (prim_type == PrimType.SPHERE || prim_type == PrimType.TORUS || prim_type == PrimType.TUBE || prim_type == PrimType.RING);
                        res.Add(new LSL_Vector(
                            (useProfileBeginEnd ? part.Shape.ProfileBegin : part.Shape.PathBegin) / 50000.0,
                            1 - (useProfileBeginEnd ? part.Shape.ProfileEnd : part.Shape.PathEnd) / 50000.0,
                            0
                        ));
                        break;
                    case (int)ScriptBaseClass.PRIM_LINK_TARGET:

                        // TODO: Should be issuing a runtime script warning in this case.
                        if (remain < 2)
                            return new LSL_List();

                        return rules.GetSublist(idx, -1);
                }
            }

            return new LSL_List();
        }


        public LSL_List llGetPrimMediaParams(int face, LSL_List rules)
        {
            m_host.AddScriptLPS(1);
            ScriptSleep(m_sleepMsOnGetPrimMediaParams);
            return GetPrimMediaParams(m_host, face, rules);
        }

        public LSL_List llGetLinkMedia(LSL_Integer link, LSL_Integer face, LSL_List rules)
        {
            m_host.AddScriptLPS(1);
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

            LSL_List res = new LSL_List();

            for (int i = 0; i < rules.Length; i++)
            {
                int code = (int)rules.GetLSLIntegerItem(i);

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
            m_host.AddScriptLPS(1);
            ScriptSleep(m_sleepMsOnSetPrimMediaParams);
            return SetPrimMediaParams(m_host, face, rules);
        }

        public LSL_Integer llSetLinkMedia(LSL_Integer link, LSL_Integer face, LSL_List rules)
        {
            m_host.AddScriptLPS(1);
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
                int code = rules.GetLSLIntegerItem(i++);

                switch (code)
                {
                    case ScriptBaseClass.PRIM_MEDIA_ALT_IMAGE_ENABLE:
                        me.EnableAlterntiveImage = (rules.GetLSLIntegerItem(i++) != 0 ? true : false);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_CONTROLS:
                        int v = rules.GetLSLIntegerItem(i++);
                        if (ScriptBaseClass.PRIM_MEDIA_CONTROLS_STANDARD == v)
                            me.Controls = MediaControls.Standard;
                        else
                            me.Controls = MediaControls.Mini;
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_CURRENT_URL:
                        me.CurrentURL = rules.GetLSLStringItem(i++);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_HOME_URL:
                        me.HomeURL = rules.GetLSLStringItem(i++);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_AUTO_LOOP:
                        me.AutoLoop = (ScriptBaseClass.TRUE == rules.GetLSLIntegerItem(i++) ? true : false);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_AUTO_PLAY:
                        me.AutoPlay = (ScriptBaseClass.TRUE == rules.GetLSLIntegerItem(i++) ? true : false);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_AUTO_SCALE:
                        me.AutoScale = (ScriptBaseClass.TRUE == rules.GetLSLIntegerItem(i++) ? true : false);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_AUTO_ZOOM:
                        me.AutoZoom = (ScriptBaseClass.TRUE == rules.GetLSLIntegerItem(i++) ? true : false);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_FIRST_CLICK_INTERACT:
                        me.InteractOnFirstClick = (ScriptBaseClass.TRUE == rules.GetLSLIntegerItem(i++) ? true : false);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_WIDTH_PIXELS:
                        me.Width = (int)rules.GetLSLIntegerItem(i++);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_HEIGHT_PIXELS:
                        me.Height = (int)rules.GetLSLIntegerItem(i++);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_WHITELIST_ENABLE:
                        me.EnableWhiteList = (ScriptBaseClass.TRUE == rules.GetLSLIntegerItem(i++) ? true : false);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_WHITELIST:
                        string[] rawWhiteListUrls = rules.GetLSLStringItem(i++).ToString().Split(new char[] { ',' });
                        List<string> whiteListUrls = new List<string>();
                        Array.ForEach(
                            rawWhiteListUrls, delegate(string rawUrl) { whiteListUrls.Add(rawUrl.Trim()); });
                        me.WhiteList = whiteListUrls.ToArray();
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_PERMS_INTERACT:
                        me.InteractPermissions = (MediaPermission)(byte)(int)rules.GetLSLIntegerItem(i++);
                        break;

                    case ScriptBaseClass.PRIM_MEDIA_PERMS_CONTROL:
                        me.ControlPermissions = (MediaPermission)(byte)(int)rules.GetLSLIntegerItem(i++);
                        break;

                    default: return ScriptBaseClass.LSL_STATUS_MALFORMED_PARAMS;
                }
            }

            module.SetMediaEntry(part, face, me);

            return ScriptBaseClass.LSL_STATUS_OK;
        }

        public LSL_Integer llClearPrimMedia(LSL_Integer face)
        {
            m_host.AddScriptLPS(1);
            ScriptSleep(m_sleepMsOnClearPrimMedia);
            return ClearPrimMedia(m_host, face);
        }

        public LSL_Integer llClearLinkMedia(LSL_Integer link, LSL_Integer face)
        {
            m_host.AddScriptLPS(1);
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
        //  LSL requires a base64 string to be 8
        //  characters in length. LSL also uses '/'
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

            m_host.AddScriptLPS(1);

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
        //  accumulation.
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
            int number = 0;
            int digit;

            m_host.AddScriptLPS(1);

            //    Require a well-fromed base64 string

            if (str.Length > 8)
                return 0;

            //    The loop is unrolled in the interests
            //    of performance and simple necessity.
            //
            //    MUST find 6 digits to be well formed
            //      -1 == invalid
            //       0 == padding

            if ((digit = c2itable[str[0]]) <= 0)
            {
                return digit < 0 ? (int)0 : number;
            }
            number += --digit<<26;

            if ((digit = c2itable[str[1]]) <= 0)
            {
                return digit < 0 ? (int)0 : number;
            }
            number += --digit<<20;

            if ((digit = c2itable[str[2]]) <= 0)
            {
                return digit < 0 ? (int)0 : number;
            }
            number += --digit<<14;

            if ((digit = c2itable[str[3]]) <= 0)
            {
                return digit < 0 ? (int)0 : number;
            }
            number += --digit<<8;

            if ((digit = c2itable[str[4]]) <= 0)
            {
                return digit < 0 ? (int)0 : number;
            }
            number += --digit<<2;

            if ((digit = c2itable[str[5]]) <= 0)
            {
                return digit < 0 ? (int)0 : number;
            }
            number += --digit>>4;

            // ignore trailing padding

            return number;
        }

        public LSL_Float llGetGMTclock()
        {
            m_host.AddScriptLPS(1);
            return DateTime.UtcNow.TimeOfDay.TotalSeconds;
        }

        public LSL_String llGetHTTPHeader(LSL_Key request_id, string header)
        {
            m_host.AddScriptLPS(1);

           if (m_UrlModule != null)
               return m_UrlModule.GetHttpHeader(new UUID(request_id), header);
           return String.Empty;
        }


        public LSL_String llGetSimulatorHostname()
        {
            m_host.AddScriptLPS(1);
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

        private LSL_List ParseString2List(string src, LSL_List separators, LSL_List spacers, bool keepNulls)
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

            m_host.AddScriptLPS(1);

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
                        int index = src.IndexOf(d, i);
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
                    outarray[outlen++] = src.Substring(i, earliestSrc - i);
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

        public LSL_Integer llGetObjectPermMask(int mask)
        {
            m_host.AddScriptLPS(1);

            int permmask = 0;

            if (mask == ScriptBaseClass.MASK_BASE)//0
            {
                permmask = (int)m_host.BaseMask;
            }

            else if (mask == ScriptBaseClass.MASK_OWNER)//1
            {
                permmask = (int)m_host.OwnerMask;
            }

            else if (mask == ScriptBaseClass.MASK_GROUP)//2
            {
                permmask = (int)m_host.GroupMask;
            }

            else if (mask == ScriptBaseClass.MASK_EVERYONE)//3
            {
                permmask = (int)m_host.EveryoneMask;
            }

            else if (mask == ScriptBaseClass.MASK_NEXT)//4
            {
                permmask = (int)m_host.NextOwnerMask;
            }

            return permmask;
        }

        public void llSetObjectPermMask(int mask, int value)
        {
            m_host.AddScriptLPS(1);

            if (m_ScriptEngine.Config.GetBoolean("AllowGodFunctions", false))
            {
                if (World.Permissions.CanRunConsoleCommand(m_host.OwnerID))
                {
                    if (mask == ScriptBaseClass.MASK_BASE)//0
                    {
                        m_host.BaseMask = (uint)value;
                    }

                    else if (mask == ScriptBaseClass.MASK_OWNER)//1
                    {
                        m_host.OwnerMask = (uint)value;
                    }

                    else if (mask == ScriptBaseClass.MASK_GROUP)//2
                    {
                        m_host.GroupMask = (uint)value;
                    }

                    else if (mask == ScriptBaseClass.MASK_EVERYONE)//3
                    {
                        m_host.EveryoneMask = (uint)value;
                    }

                    else if (mask == ScriptBaseClass.MASK_NEXT)//4
                    {
                        m_host.NextOwnerMask = (uint)value;
                    }
                }
            }
        }

        public LSL_Integer llGetInventoryPermMask(string itemName, int mask)
        {
            m_host.AddScriptLPS(1);

            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(itemName);

            if (item == null)
                return -1;

            switch (mask)
            {
                case 0:
                    return (int)item.BasePermissions;
                case 1:
                    return (int)item.CurrentPermissions;
                case 2:
                    return (int)item.GroupPermissions;
                case 3:
                    return (int)item.EveryonePermissions;
                case 4:
                    return (int)item.NextPermissions;
            }

            return -1;
        }

        public void llSetInventoryPermMask(string itemName, int mask, int value)
        {
            m_host.AddScriptLPS(1);

            if (m_ScriptEngine.Config.GetBoolean("AllowGodFunctions", false))
            {
                if (World.Permissions.CanRunConsoleCommand(m_host.OwnerID))
                {
                    TaskInventoryItem item = m_host.Inventory.GetInventoryItem(itemName);

                    if (item != null)
                    {
                        switch (mask)
                        {
                            case 0:
                                item.BasePermissions = (uint)value;
                                break;
                            case 1:
                                item.CurrentPermissions = (uint)value;
                                break;
                            case 2:
                                item.GroupPermissions = (uint)value;
                                break;
                            case 3:
                                item.EveryonePermissions = (uint)value;
                                break;
                            case 4:
                                item.NextPermissions = (uint)value;
                                break;
                        }
                    }
                }
            }
        }

        public LSL_String llGetInventoryCreator(string itemName)
        {
            m_host.AddScriptLPS(1);

            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(itemName);

            if (item == null)
            {
                Error("llGetInventoryCreator", "Can't find item '" + item + "'");

                return String.Empty;
            }

            return item.CreatorID.ToString();
        }

        public void llOwnerSay(string msg)
        {
            m_host.AddScriptLPS(1);

            World.SimChatBroadcast(Utils.StringToBytes(msg), ChatTypeEnum.Owner, 0,
                                   m_host.AbsolutePosition, m_host.Name, m_host.UUID, false);
//            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
//            wComm.DeliverMessage(ChatTypeEnum.Owner, 0, m_host.Name, m_host.UUID, msg);
        }

        public LSL_String llRequestSecureURL()
        {
            m_host.AddScriptLPS(1);
            if (m_UrlModule != null)
                return m_UrlModule.RequestSecureURL(m_ScriptEngine.ScriptModule, m_host, m_item.ItemID).ToString();
            return UUID.Zero.ToString();
        }

        public LSL_String llRequestSimulatorData(string simulator, int data)
        {
            IOSSL_Api ossl = (IOSSL_Api)m_ScriptEngine.GetApi(m_item.ItemID, "OSSL");

            try
            {
                m_host.AddScriptLPS(1);

                string reply = String.Empty;

                GridRegion info;

                if (World.RegionInfo.RegionName == simulator)
                    info = new GridRegion(World.RegionInfo);
                else
                    info = World.GridService.GetRegionByName(m_ScriptEngine.World.RegionInfo.ScopeID, simulator);

                switch (data)
                {
                    case ScriptBaseClass.DATA_SIM_POS:
                        if (info == null)
                        {
                            ScriptSleep(m_sleepMsOnRequestSimulatorData);
                            return UUID.Zero.ToString();
                        }

                        bool isHypergridRegion = false;

                        if (World.RegionInfo.RegionName != simulator && info.RegionSecret != "")
                        {
                            // Hypergrid is currently placing real destination region co-ords into RegionSecret.
                            // But other code can also use this field for a genuine RegionSecret!  Therefore, if
                            // anything is present we need to disambiguate.
                            //
                            // FIXME: Hypergrid should be storing this data in a different field.
                            RegionFlags regionFlags
                                = (RegionFlags)m_ScriptEngine.World.GridService.GetRegionFlags(
                                    info.ScopeID, info.RegionID);
                            isHypergridRegion = (regionFlags & RegionFlags.Hyperlink) != 0;
                        }

                        if (isHypergridRegion)
                        {
                            uint rx = 0, ry = 0;
                            Utils.LongToUInts(Convert.ToUInt64(info.RegionSecret), out rx, out ry);

                            reply = new LSL_Vector(
                                rx,
                                ry,
                                0).ToString();
                        }
                        else
                        {
                            // Local grid co-oridnates
                            reply = new LSL_Vector(
                                info.RegionLocX,
                                info.RegionLocY,
                                0).ToString();
                        }
                        break;
                    case ScriptBaseClass.DATA_SIM_STATUS:
                        if (info != null)
                            reply = "up"; // Duh!
                        else
                            reply = "unknown";
                        break;
                    case ScriptBaseClass.DATA_SIM_RATING:
                        if (info == null)
                        {
                            ScriptSleep(m_sleepMsOnRequestSimulatorData);
                            return UUID.Zero.ToString();
                        }
                        int access = info.Maturity;
                        if (access == 0)
                            reply = "PG";
                        else if (access == 1)
                            reply = "MATURE";
                        else if (access == 2)
                            reply = "ADULT";
                        else
                            reply = "UNKNOWN";
                        break;
                    case ScriptBaseClass.DATA_SIM_RELEASE:
                        if (ossl != null)
                            ossl.CheckThreatLevel(ThreatLevel.High, "llRequestSimulatorData");
                        reply = "OpenSim";
                        break;
                    default:
                        ScriptSleep(m_sleepMsOnRequestSimulatorData);
                        return UUID.Zero.ToString(); // Raise no event
                }
                UUID rq = UUID.Random();

                UUID tid = AsyncCommands.
                    DataserverPlugin.RegisterRequest(m_host.LocalId, m_item.ItemID, rq.ToString());

                AsyncCommands.
                    DataserverPlugin.DataserverReply(rq.ToString(), reply);

                ScriptSleep(m_sleepMsOnRequestSimulatorData);
                return tid.ToString();
            }
            catch(Exception)
            {
                //m_log.Error("[LSL_API]: llRequestSimulatorData" + e.ToString());
                return UUID.Zero.ToString();
            }
        }

        public LSL_String llRequestURL()
        {
            m_host.AddScriptLPS(1);

            if (m_UrlModule != null)
                return m_UrlModule.RequestURL(m_ScriptEngine.ScriptModule, m_host, m_item.ItemID).ToString();
            return UUID.Zero.ToString();
        }

        public void llForceMouselook(int mouselook)
        {
            m_host.AddScriptLPS(1);
            m_host.SetForceMouselook(mouselook != 0);
        }

        public LSL_Float llGetObjectMass(string id)
        {
            m_host.AddScriptLPS(1);
            UUID key = new UUID();
            if (UUID.TryParse(id, out key))
            {
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
                        return (double)avatar.GetMass();
                    }
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

            m_host.AddScriptLPS(1);

            // Note that although we have normalized, both
            // indices could still be negative.
            if (start < 0)
            {
                start = start+dest.Length;
            }

            if (end < 0)
            {
                end = end+dest.Length;
            }
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
            m_host.AddScriptLPS(1);

            IDialogModule dm = World.RequestModuleInterface<IDialogModule>();
            if (null != dm)
                dm.SendUrlToUser(
                    new UUID(avatar_id), m_host.Name, m_host.UUID, m_host.OwnerID, false, message, url);

            ScriptSleep(m_sleepMsOnLoadURL);
        }

        public void llParcelMediaCommandList(LSL_List commandList)
        {
            // TODO: Not implemented yet (missing in libomv?):
            //  PARCEL_MEDIA_COMMAND_LOOP_SET    float loop      Use this to get or set the parcel's media loop duration. (1.19.1 RC0 or later)

            m_host.AddScriptLPS(1);

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

            ScenePresence presence = null;

            for (int i = 0; i < commandList.Data.Length; i++)
            {
                ParcelMediaCommandEnum command = (ParcelMediaCommandEnum)commandList.Data[i];
                switch (command)
                {
                    case ParcelMediaCommandEnum.Agent:
                        // we send only to one agent
                        if ((i + 1) < commandList.Length)
                        {
                            if (commandList.Data[i + 1] is LSL_String)
                            {
                                UUID agentID;
                                if (UUID.TryParse((LSL_String)commandList.Data[i + 1], out agentID))
                                {
                                    presence = World.GetScenePresence(agentID);
                                }
                            }
                            else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_AGENT must be a key");
                            ++i;
                        }
                        break;

                    case ParcelMediaCommandEnum.Loop:
                        loop = 1;
                        commandToSend = command;
                        update = true; //need to send the media update packet to set looping
                        break;

                    case ParcelMediaCommandEnum.Play:
                        loop = 0;
                        commandToSend = command;
                        update = true; //need to send the media update packet to make sure it doesn't loop
                        break;

                    case ParcelMediaCommandEnum.Pause:
                    case ParcelMediaCommandEnum.Stop:
                    case ParcelMediaCommandEnum.Unload:
                        commandToSend = command;
                        break;

                    case ParcelMediaCommandEnum.Url:
                        if ((i + 1) < commandList.Length)
                        {
                            if (commandList.Data[i + 1] is LSL_String)
                            {
                                url = (LSL_String)commandList.Data[i + 1];
                                update = true;
                            }
                            else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_URL must be a string");
                            ++i;
                        }
                        break;

                    case ParcelMediaCommandEnum.Texture:
                        if ((i + 1) < commandList.Length)
                        {
                            if (commandList.Data[i + 1] is LSL_String)
                            {
                                texture = (LSL_String)commandList.Data[i + 1];
                                update = true;
                            }
                            else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_TEXTURE must be a string or a key");
                            ++i;
                        }
                        break;

                    case ParcelMediaCommandEnum.Time:
                        if ((i + 1) < commandList.Length)
                        {
                            if (commandList.Data[i + 1] is LSL_Float)
                            {
                                time = (float)(LSL_Float)commandList.Data[i + 1];
                            }
                            else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_TIME must be a float");
                            ++i;
                        }
                        break;

                    case ParcelMediaCommandEnum.AutoAlign:
                        if ((i + 1) < commandList.Length)
                        {
                            if (commandList.Data[i + 1] is LSL_Integer)
                            {
                                autoAlign = (LSL_Integer)commandList.Data[i + 1];
                                update = true;
                            }

                            else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_AUTO_ALIGN must be an integer");
                            ++i;
                        }
                        break;

                    case ParcelMediaCommandEnum.Type:
                        if ((i + 1) < commandList.Length)
                        {
                            if (commandList.Data[i + 1] is LSL_String)
                            {
                                mediaType = (LSL_String)commandList.Data[i + 1];
                                update = true;
                            }
                            else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_TYPE must be a string");
                            ++i;
                        }
                        break;

                    case ParcelMediaCommandEnum.Desc:
                        if ((i + 1) < commandList.Length)
                        {
                            if (commandList.Data[i + 1] is LSL_String)
                            {
                                description = (LSL_String)commandList.Data[i + 1];
                                update = true;
                            }
                            else Error("llParcelMediaCommandList", "The argument of PARCEL_MEDIA_COMMAND_DESC must be a string");
                            ++i;
                        }
                        break;

                    case ParcelMediaCommandEnum.Size:
                        if ((i + 2) < commandList.Length)
                        {
                            if (commandList.Data[i + 1] is LSL_Integer)
                            {
                                if (commandList.Data[i + 2] is LSL_Integer)
                                {
                                    width = (LSL_Integer)commandList.Data[i + 1];
                                    height = (LSL_Integer)commandList.Data[i + 2];
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
                        if (sp.currentParcelUUID == landData.GlobalID)
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
                        if (sp.currentParcelUUID == landData.GlobalID)
                        {
                            sp.ControllingClient.SendParcelMediaCommand(0x4, // TODO what is this?
                                            (ParcelMediaCommandEnum)commandToSend, time);
                        }
                    });
                }
                else if (!presence.IsChildAgent)
                {
                    presence.ControllingClient.SendParcelMediaCommand(0x4, // TODO what is this?
                                            (ParcelMediaCommandEnum)commandToSend, time);
                }
            }
            ScriptSleep(m_sleepMsOnParcelMediaCommandList);
        }

        public LSL_List llParcelMediaQuery(LSL_List aList)
        {
            m_host.AddScriptLPS(1);
            LSL_List list = new LSL_List();
            //TO DO: make the implementation for the missing commands
            //PARCEL_MEDIA_COMMAND_LOOP_SET    float loop      Use this to get or set the parcel's media loop duration. (1.19.1 RC0 or later)
            for (int i = 0; i < aList.Data.Length; i++)
            {

                if (aList.Data[i] != null)
                {
                    switch ((ParcelMediaCommandEnum) Convert.ToInt32(aList.Data[i].ToString()))
                    {
                        case ParcelMediaCommandEnum.Url:
                            list.Add(new LSL_String(World.GetLandData(m_host.AbsolutePosition).MediaURL));
                            break;
                        case ParcelMediaCommandEnum.Desc:
                            list.Add(new LSL_String(World.GetLandData(m_host.AbsolutePosition).Description));
                            break;
                        case ParcelMediaCommandEnum.Texture:
                            list.Add(new LSL_String(World.GetLandData(m_host.AbsolutePosition).MediaID.ToString()));
                            break;
                        case ParcelMediaCommandEnum.Type:
                            list.Add(new LSL_String(World.GetLandData(m_host.AbsolutePosition).MediaType));
                            break;
                        case ParcelMediaCommandEnum.Size:
                            list.Add(new LSL_String(World.GetLandData(m_host.AbsolutePosition).MediaWidth));
                            list.Add(new LSL_String(World.GetLandData(m_host.AbsolutePosition).MediaHeight));
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
            m_host.AddScriptLPS(1);
            Int64 tmp = 0;
            Math.DivRem(Convert.ToInt64(Math.Pow(a, b)), c, out tmp);
            ScriptSleep(m_sleepMsOnModPow);
            return Convert.ToInt32(tmp);
        }

        public LSL_Integer llGetInventoryType(string name)
        {
            m_host.AddScriptLPS(1);

            TaskInventoryItem item = m_host.Inventory.GetInventoryItem(name);

            if (item == null)
                return -1;

            return item.Type;
        }

        public void llSetPayPrice(int price, LSL_List quick_pay_buttons)
        {
            m_host.AddScriptLPS(1);

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
            nPrice[1] = quick_pay_buttons.GetLSLIntegerItem(0);
            nPrice[2] = quick_pay_buttons.GetLSLIntegerItem(1);
            nPrice[3] = quick_pay_buttons.GetLSLIntegerItem(2);
            nPrice[4] = quick_pay_buttons.GetLSLIntegerItem(3);
            m_host.ParentGroup.RootPart.PayPrice = nPrice;
            m_host.ParentGroup.HasGroupChanged = true;
        }

        public LSL_Vector llGetCameraPos()
        {
            m_host.AddScriptLPS(1);

            if (m_item.PermsGranter == UUID.Zero)
                return Vector3.Zero;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TRACK_CAMERA) == 0)
            {
                Error("llGetCameraPos", "No permissions to track the camera");
                return Vector3.Zero;
            }

//            ScenePresence presence = World.GetScenePresence(m_host.OwnerID);
            ScenePresence presence = World.GetScenePresence(m_item.PermsGranter);
            if (presence != null)
            {
                LSL_Vector pos = new LSL_Vector(presence.CameraPosition);
                return pos;
            }

            return Vector3.Zero;
        }

        public LSL_Rotation llGetCameraRot()
        {
            m_host.AddScriptLPS(1);

            if (m_item.PermsGranter == UUID.Zero)
                return Quaternion.Identity;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_TRACK_CAMERA) == 0)
            {
                Error("llGetCameraRot", "No permissions to track the camera");
                return Quaternion.Identity;
            }

//            ScenePresence presence = World.GetScenePresence(m_host.OwnerID);
            ScenePresence presence = World.GetScenePresence(m_item.PermsGranter);
            if (presence != null)
            {
                return new LSL_Rotation(presence.CameraRotation);
            }

            return Quaternion.Identity;
        }

        public void llSetPrimURL(string url)
        {
            m_host.AddScriptLPS(1);
            Deprecated("llSetPrimURL", "Use llSetPrimMediaParams instead");
            ScriptSleep(m_sleepMsOnSetPrimURL);
        }

        public void llRefreshPrimURL()
        {
            m_host.AddScriptLPS(1);
            Deprecated("llRefreshPrimURL");
            ScriptSleep(m_sleepMsOnRefreshPrimURL);
        }

        public LSL_String llEscapeURL(string url)
        {
            m_host.AddScriptLPS(1);
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
            m_host.AddScriptLPS(1);
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
            m_host.AddScriptLPS(1);
            DetectParams detectedParams = m_ScriptEngine.GetDetectParams(m_item.ItemID, 0);
            if (detectedParams == null)
            {
                if (m_host.ParentGroup.IsAttachment == true)
                {
                    detectedParams = new DetectParams();
                    detectedParams.Key = m_host.OwnerID;
                }
                else
                {
                    return;
                }
            }
           
            ScenePresence avatar = World.GetScenePresence(detectedParams.Key);
            if (avatar != null)
            {
                avatar.ControllingClient.SendScriptTeleportRequest(m_host.Name,
                    simname, pos, lookAt);
            }
            ScriptSleep(m_sleepMsOnMapDestination);
        }

        public void llAddToLandBanList(string avatar, double hours)
        {
            m_host.AddScriptLPS(1);
            UUID key;
            ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition);
            if (World.Permissions.CanEditParcelProperties(m_host.OwnerID, land, GroupPowers.LandManageBanned, false))
            {
                int expires = 0;
                if (hours != 0)
                    expires = Util.UnixTimeSinceEpoch() + (int)(3600.0 * hours);

                if (UUID.TryParse(avatar, out key))
                {
                    int idx = land.LandData.ParcelAccessList.FindIndex(
                            delegate(LandAccessEntry e)
                            {
                                if (e.AgentID == key && e.Flags == AccessList.Ban)
                                    return true;
                                return false;
                            });

                    if (idx != -1 && (land.LandData.ParcelAccessList[idx].Expires == 0 || (expires != 0 && expires < land.LandData.ParcelAccessList[idx].Expires)))
                        return;

                    if (idx != -1)
                        land.LandData.ParcelAccessList.RemoveAt(idx);

                    LandAccessEntry entry = new LandAccessEntry();

                    entry.AgentID = key;
                    entry.Flags = AccessList.Ban;
                    entry.Expires = expires;

                    land.LandData.ParcelAccessList.Add(entry);

                    World.EventManager.TriggerLandObjectUpdated((uint)land.LandData.LocalID, land);
                }
            }
            ScriptSleep(m_sleepMsOnAddToLandBanList);
        }

        public void llRemoveFromLandPassList(string avatar)
        {
            m_host.AddScriptLPS(1);
            UUID key;
            ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition);
            if (World.Permissions.CanEditParcelProperties(m_host.OwnerID, land, GroupPowers.LandManageAllowed, false))
            {
                if (UUID.TryParse(avatar, out key))
                {
                    int idx = land.LandData.ParcelAccessList.FindIndex(
                            delegate(LandAccessEntry e)
                            {
                                if (e.AgentID == key && e.Flags == AccessList.Access)
                                    return true;
                                return false;
                            });

                    if (idx != -1)
                    {
                        land.LandData.ParcelAccessList.RemoveAt(idx);
                        World.EventManager.TriggerLandObjectUpdated((uint)land.LandData.LocalID, land);
                    }
                }
            }
            ScriptSleep(m_sleepMsOnRemoveFromLandPassList);
        }

        public void llRemoveFromLandBanList(string avatar)
        {
            m_host.AddScriptLPS(1);
            UUID key;
            ILandObject land = World.LandChannel.GetLandObject(m_host.AbsolutePosition);
            if (World.Permissions.CanEditParcelProperties(m_host.OwnerID, land, GroupPowers.LandManageBanned, false))
            {
                if (UUID.TryParse(avatar, out key))
                {
                    int idx = land.LandData.ParcelAccessList.FindIndex(
                            delegate(LandAccessEntry e)
                            {
                                if (e.AgentID == key && e.Flags == AccessList.Ban)
                                    return true;
                                return false;
                            });

                    if (idx != -1)
                    {
                        land.LandData.ParcelAccessList.RemoveAt(idx);
                        World.EventManager.TriggerLandObjectUpdated((uint)land.LandData.LocalID, land);
                    }
                }
            }
            ScriptSleep(m_sleepMsOnRemoveFromLandBanList);
        }

        public void llSetCameraParams(LSL_List rules)
        {
            m_host.AddScriptLPS(1);

            // the object we are in
            UUID objectID = m_host.ParentUUID;
            if (objectID == UUID.Zero)
                return;

            // we need the permission first, to know which avatar we want to set the camera for
            UUID agentID = m_item.PermsGranter;

            if (agentID == UUID.Zero)
                return;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_CONTROL_CAMERA) == 0)
                return;

            ScenePresence presence = World.GetScenePresence(agentID);

            // we are not interested in child-agents
            if (presence.IsChildAgent) return;

            SortedDictionary<int, float> parameters = new SortedDictionary<int, float>();
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
                    if (data[i] is LSL_Float)
                        parameters.Add(type, (float)((LSL_Float)data[i]).value);
                    else if (data[i] is LSL_Integer)
                        parameters.Add(type, (float)((LSL_Integer)data[i]).value);
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
            m_host.AddScriptLPS(1);

            // the object we are in
            UUID objectID = m_host.ParentUUID;
            if (objectID == UUID.Zero)
                return;

            // we need the permission first, to know which avatar we want to clear the camera for
            UUID agentID = m_item.PermsGranter;

            if (agentID == UUID.Zero)
                return;

            if ((m_item.PermsMask & ScriptBaseClass.PERMISSION_CONTROL_CAMERA) == 0)
                return;

            ScenePresence presence = World.GetScenePresence(agentID);

            // we are not interested in child-agents
            if (presence.IsChildAgent)
                return;

            presence.ControllingClient.SendClearFollowCamProperties(objectID);
        }

        public LSL_Float llListStatistics(int operation, LSL_List src)
        {
            m_host.AddScriptLPS(1);
            switch (operation)
            {
                case ScriptBaseClass.LIST_STAT_RANGE:
                    return src.Range();
                case ScriptBaseClass.LIST_STAT_MIN:
                    return src.Min();
                case ScriptBaseClass.LIST_STAT_MAX:
                    return src.Max();
                case ScriptBaseClass.LIST_STAT_MEAN:
                    return src.Mean();
                case ScriptBaseClass.LIST_STAT_MEDIAN:
                    return LSL_List.ToDoubleList(src).Median();
                case ScriptBaseClass.LIST_STAT_NUM_COUNT:
                    return src.NumericLength();
                case ScriptBaseClass.LIST_STAT_STD_DEV:
                    return src.StdDev();
                case ScriptBaseClass.LIST_STAT_SUM:
                    return src.Sum();
                case ScriptBaseClass.LIST_STAT_SUM_SQUARES:
                    return src.SumSqrs();
                case ScriptBaseClass.LIST_STAT_GEOMETRIC_MEAN:
                    return src.GeometricMean();
                case ScriptBaseClass.LIST_STAT_HARMONIC_MEAN:
                    return src.HarmonicMean();
                default:
                    return 0.0;
            }
        }

        public LSL_Integer llGetUnixTime()
        {
            m_host.AddScriptLPS(1);
            return Util.UnixTimeSinceEpoch();
        }

        public LSL_Integer llGetParcelFlags(LSL_Vector pos)
        {
            m_host.AddScriptLPS(1);
            return (int)World.LandChannel.GetLandObject((float)pos.x, (float)pos.y).LandData.Flags;
        }

        public LSL_Integer llGetRegionFlags()
        {
            m_host.AddScriptLPS(1);
            IEstateModule estate = World.RequestModuleInterface<IEstateModule>();
            if (estate == null)
                return 67108864;
            return (int)estate.GetRegionFlags();
        }

        public LSL_String llXorBase64StringsCorrect(string str1, string str2)
        {
            m_host.AddScriptLPS(1);

            if (str1 == String.Empty)
                return String.Empty;
            if (str2 == String.Empty)
                return str1;

            int len = str2.Length;
            if ((len % 4) != 0) // LL is EVIL!!!!
            {
                while (str2.EndsWith("="))
                    str2 = str2.Substring(0, str2.Length - 1);

                len = str2.Length;
                int mod = len % 4;

                if (mod == 1)
                    str2 = str2.Substring(0, str2.Length - 1);
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
                return new LSL_String(String.Empty);
            }

            byte[] d2 = new Byte[data1.Length];
            int pos = 0;
            
            if (data1.Length <= data2.Length)
            {
                Array.Copy(data2, 0, d2, 0, data1.Length);
            }
            else
            {
                while (pos < data1.Length)
                {
                    len = data1.Length - pos;
                    if (len > data2.Length)
                        len = data2.Length;

                    Array.Copy(data2, 0, d2, pos, len);
                    pos += len;
                }
            }

            for (pos = 0 ; pos < data1.Length ; pos++ )
                data1[pos] ^= d2[pos];

            return Convert.ToBase64String(data1);
        }

        public LSL_String llHTTPRequest(string url, LSL_List parameters, string body)
        {
            // Partial implementation: support for parameter flags needed
            //   see http://wiki.secondlife.com/wiki/LlHTTPRequest
            // parameter flags support are implemented in ScriptsHttpRequests.cs
            //   in StartHttpRequest

            m_host.AddScriptLPS(1);
            IHttpRequestModule httpScriptMod =
                m_ScriptEngine.World.RequestModuleInterface<IHttpRequestModule>();
            List<string> param = new List<string>();
            bool  ok;
            Int32 flag;

            for (int i = 0; i < parameters.Data.Length; i += 2)
            {
                ok = Int32.TryParse(parameters.Data[i].ToString(), out flag);
                if (!ok || flag < 0 ||
                    flag > (int)HttpRequestConstants.HTTP_PRAGMA_NO_CACHE)
                {
                    Error("llHTTPRequest", "Parameter " + i.ToString() + " is an invalid flag");
                }

                param.Add(parameters.Data[i].ToString());       //Add parameter flag

                if (flag != (int)HttpRequestConstants.HTTP_CUSTOM_HEADER)
                {
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
                        //Enough parameters remaining for (another) header?
                        if (parameters.Data.Length - i < 2)
                        {
                            //There must be at least one name/value pair for custom header
                            if (count == 1)
                                Error("llHTTPRequest", "Missing name/value for custom header at parameter " + i.ToString());
                            break;
                        }

                        if (HttpStandardHeaders.Contains(parameters.Data[i].ToString(), StringComparer.OrdinalIgnoreCase))
                            Error("llHTTPRequest", "Name is invalid as a custom header at parameter " + i.ToString());

                        param.Add(parameters.Data[i].ToString());
                        param.Add(parameters.Data[i+1].ToString());

                        //Have we reached the end of the list of headers?
                        //End is marked by a string with a single digit.
                        if (i+2 >= parameters.Data.Length ||
                            Char.IsDigit(parameters.Data[i].ToString()[0]))
                        {
                            break;
                        }

                        i += 2;
                    }
                }
            }

            Vector3 position = m_host.AbsolutePosition;
            Vector3 velocity = m_host.Velocity;
            Quaternion rotation = m_host.RotationOffset;
            string ownerName = String.Empty;
            ScenePresence scenePresence = World.GetScenePresence(m_host.OwnerID);
            if (scenePresence == null)
                ownerName = resolveName(m_host.OwnerID);
            else
                ownerName = scenePresence.Name;

            RegionInfo regionInfo = World.RegionInfo;

            Dictionary<string, string> httpHeaders = new Dictionary<string, string>();

            string shard = "OpenSim";
            IConfigSource config = m_ScriptEngine.ConfigSource;
            if (config.Configs["Network"] != null)
            {
                shard = config.Configs["Network"].GetString("shard", shard);
            }

            httpHeaders["X-SecondLife-Shard"] = shard;
            httpHeaders["X-SecondLife-Object-Name"] = m_host.Name;
            httpHeaders["X-SecondLife-Object-Key"] = m_host.UUID.ToString();
            httpHeaders["X-SecondLife-Region"] = string.Format("{0} ({1}, {2})", regionInfo.RegionName, regionInfo.RegionLocX, regionInfo.RegionLocY);
            httpHeaders["X-SecondLife-Local-Position"] = string.Format("({0:0.000000}, {1:0.000000}, {2:0.000000})", position.X, position.Y, position.Z);
            httpHeaders["X-SecondLife-Local-Velocity"] = string.Format("({0:0.000000}, {1:0.000000}, {2:0.000000})", velocity.X, velocity.Y, velocity.Z);
            httpHeaders["X-SecondLife-Local-Rotation"] = string.Format("({0:0.000000}, {1:0.000000}, {2:0.000000}, {3:0.000000})", rotation.X, rotation.Y, rotation.Z, rotation.W);
            httpHeaders["X-SecondLife-Owner-Name"] = ownerName;
            httpHeaders["X-SecondLife-Owner-Key"] = m_host.OwnerID.ToString();
            string userAgent = config.Configs["Network"].GetString("user_agent", null);
            if (userAgent != null)
                httpHeaders["User-Agent"] = userAgent;

            // See if the URL contains any header hacks
            string[] urlParts = url.Split(new char[] {'\n'});
            if (urlParts.Length > 1)
            {
                // Iterate the passed headers and parse them
                for (int i = 1 ; i < urlParts.Length ; i++ )
                {
                    // The rest of those would be added to the body in SL.
                    // Let's not do that.
                    if (urlParts[i] == String.Empty)
                        break;

                    // See if this could be a valid header
                    string[] headerParts = urlParts[i].Split(new char[] {':'}, 2);
                    if (headerParts.Length != 2)
                        continue;

                    string headerName = headerParts[0].Trim();
                    string headerValue = headerParts[1].Trim();

                    // Filter out headers that could be used to abuse
                    // another system or cloak the request
                    if (headerName.ToLower() == "x-secondlife-shard" ||
                        headerName.ToLower() == "x-secondlife-object-name" ||
                        headerName.ToLower() == "x-secondlife-object-key" ||
                        headerName.ToLower() == "x-secondlife-region" ||
                        headerName.ToLower() == "x-secondlife-local-position" ||
                        headerName.ToLower() == "x-secondlife-local-velocity" ||
                        headerName.ToLower() == "x-secondlife-local-rotation" ||
                        headerName.ToLower() == "x-secondlife-owner-name" ||
                        headerName.ToLower() == "x-secondlife-owner-key" ||
                        headerName.ToLower() == "connection" ||
                        headerName.ToLower() == "content-length" ||
                        headerName.ToLower() == "from" ||
                        headerName.ToLower() == "host" ||
                        headerName.ToLower() == "proxy-authorization" ||
                        headerName.ToLower() == "referer" ||
                        headerName.ToLower() == "trailer" ||
                        headerName.ToLower() == "transfer-encoding" ||
                        headerName.ToLower() == "via" ||
                        headerName.ToLower() == "authorization")
                        continue;

                    httpHeaders[headerName] = headerValue;
                }

                // Finally, strip any protocol specifier from the URL
                url = urlParts[0].Trim();
                int idx = url.IndexOf(" HTTP/");
                if (idx != -1)
                    url = url.Substring(0, idx);
            }

            string authregex = @"^(https?:\/\/)(\w+):(\w+)@(.*)$";
            Regex r = new Regex(authregex);
            int[] gnums = r.GetGroupNumbers();
            Match m = r.Match(url);
            if (m.Success)
            {
                for (int i = 1; i < gnums.Length; i++)
                {
                    //System.Text.RegularExpressions.Group g = m.Groups[gnums[i]];
                    //CaptureCollection cc = g.Captures;
                }
                if (m.Groups.Count == 5)
                {
                    httpHeaders["Authorization"] = String.Format("Basic {0}", Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(m.Groups[2].ToString() + ":" + m.Groups[3].ToString())));
                    url = m.Groups[1].ToString() + m.Groups[4].ToString();
                }
            }

            HttpInitialRequestStatus status;
            UUID reqID
                = httpScriptMod.StartHttpRequest(m_host.LocalId, m_item.ItemID, url, param, httpHeaders, body, out status);

            if (status == HttpInitialRequestStatus.DISALLOWED_BY_FILTER)
                Error("llHttpRequest", string.Format("Request to {0} disallowed by filter", url));

            if (reqID != UUID.Zero)
                return reqID.ToString();
            else
                return null;
        }


        public void llHTTPResponse(LSL_Key id, int status, string body)
        {
            // Partial implementation: support for parameter flags needed
            //   see http://wiki.secondlife.com/wiki/llHTTPResponse

            m_host.AddScriptLPS(1);

            if (m_UrlModule != null)
                m_UrlModule.HttpResponse(new UUID(id), status,body);
        }

        public void llResetLandBanList()
        {
            m_host.AddScriptLPS(1);
            LandData land = World.LandChannel.GetLandObject(m_host.AbsolutePosition).LandData;
            if (land.OwnerID == m_host.OwnerID)
            {
                foreach (LandAccessEntry entry in land.ParcelAccessList)
                {
                    if (entry.Flags == AccessList.Ban)
                    {
                        land.ParcelAccessList.Remove(entry);
                    }
                }
            }
            ScriptSleep(m_sleepMsOnResetLandBanList);
        }

        public void llResetLandPassList()
        {
            m_host.AddScriptLPS(1);
            LandData land = World.LandChannel.GetLandObject(m_host.AbsolutePosition).LandData;
            if (land.OwnerID == m_host.OwnerID)
            {
                foreach (LandAccessEntry entry in land.ParcelAccessList)
                {
                    if (entry.Flags == AccessList.Access)
                    {
                        land.ParcelAccessList.Remove(entry);
                    }
                }
            }
            ScriptSleep(m_sleepMsOnResetLandPassList);
        }

        public LSL_Integer llGetParcelPrimCount(LSL_Vector pos, int category, int sim_wide)
        {
            m_host.AddScriptLPS(1);

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
            m_host.AddScriptLPS(1);
            LandObject land = (LandObject)World.LandChannel.GetLandObject((float)pos.x, (float)pos.y);
            LSL_List ret = new LSL_List();
            if (land != null)
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

        public LSL_Integer llGetObjectPrimCount(string object_id)
        {
            m_host.AddScriptLPS(1);
            SceneObjectPart part = World.GetSceneObjectPart(new UUID(object_id));
            if (part == null)
            {
                return 0;
            }
            else
            {
                return part.ParentGroup.PrimCount;
            }
        }

        public LSL_Integer llGetParcelMaxPrims(LSL_Vector pos, int sim_wide)
        {
            m_host.AddScriptLPS(1);

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
            m_host.AddScriptLPS(1);
            LandData land = World.GetLandData(pos);
            if (land == null)
            {
                return new LSL_List(0);
            }
            LSL_List ret = new LSL_List();
            foreach (object o in param.Data)
            {
                switch (o.ToString())
                {
                    case "0":
                        ret.Add(new LSL_String(land.Name));
                        break;
                    case "1":
                        ret.Add(new LSL_String(land.Description));
                        break;
                    case "2":
                        ret.Add(new LSL_Key(land.OwnerID.ToString()));
                        break;
                    case "3":
                        ret.Add(new LSL_Key(land.GroupID.ToString()));
                        break;
                    case "4":
                        ret.Add(new LSL_Integer(land.Area));
                        break;
                    case "5":
                        ret.Add(new LSL_Key(land.GlobalID.ToString()));
                        break;
                    default:
                        ret.Add(new LSL_Integer(0));
                        break;
                }
            }
            return ret;
        }

        public LSL_String llStringTrim(string src, int type)
        {
            m_host.AddScriptLPS(1);
            if (type == (int)ScriptBaseClass.STRING_TRIM_HEAD) { return src.TrimStart(); }
            if (type == (int)ScriptBaseClass.STRING_TRIM_TAIL) { return src.TrimEnd(); }
            if (type == (int)ScriptBaseClass.STRING_TRIM) { return src.Trim(); }
            return src;
        }

        public LSL_List llGetObjectDetails(string id, LSL_List args)
        {
            m_host.AddScriptLPS(1);

            LSL_List ret = new LSL_List();
            UUID key = new UUID();
            

            if (UUID.TryParse(id, out key))
            {
                ScenePresence av = World.GetScenePresence(key);

                if (av != null)
                {
                    foreach (object o in args.Data)
                    {
                        switch (int.Parse(o.ToString()))
                        {
                            case ScriptBaseClass.OBJECT_NAME:
                                ret.Add(new LSL_String(av.Firstname + " " + av.Lastname));
                                break;
                            case ScriptBaseClass.OBJECT_DESC:
                                ret.Add(new LSL_String(""));
                                break;
                            case ScriptBaseClass.OBJECT_POS:
                                Vector3 avpos;

                                if (av.ParentID != 0 && av.ParentPart != null &&
                                    av.ParentPart.ParentGroup != null && av.ParentPart.ParentGroup.RootPart != null )
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

                                ret.Add(new LSL_Vector((double)avpos.X, (double)avpos.Y, (double)avpos.Z));
                                break;
                            case ScriptBaseClass.OBJECT_ROT:
                                Quaternion avrot = av.GetWorldRotation();
                                ret.Add(new LSL_Rotation(avrot));
                                break;
                            case ScriptBaseClass.OBJECT_VELOCITY:
                                Vector3 avvel = av.GetWorldVelocity();
                                ret.Add(new LSL_Vector((double)avvel.X, (double)avvel.Y, (double)avvel.Z));
                                break;
                            case ScriptBaseClass.OBJECT_OWNER:
                                ret.Add(new LSL_String(id));
                                break;
                            case ScriptBaseClass.OBJECT_GROUP:
                                ret.Add(new LSL_String(UUID.Zero.ToString()));
                                break;
                            case ScriptBaseClass.OBJECT_CREATOR:
                                ret.Add(new LSL_String(UUID.Zero.ToString()));
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
                                if (p != null)
                                {
                                    ret.Add(new LSL_String(p.ParentGroup.RootPart.UUID.ToString()));
                                }
                                else
                                {
                                    ret.Add(new LSL_String(id));
                                }
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
                            default:
                                // Invalid or unhandled constant.
                                ret.Add(new LSL_Integer(ScriptBaseClass.OBJECT_UNKNOWN_DETAIL));
                                break;
                        }
                    }

                    return ret;
                }

                SceneObjectPart obj = World.GetSceneObjectPart(key);
                if (obj != null)
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
                                Vector3 opos = obj.AbsolutePosition;
                                ret.Add(new LSL_Vector(opos.X, opos.Y, opos.Z));
                                break;
                            case ScriptBaseClass.OBJECT_ROT:
                                Quaternion rot = Quaternion.Identity;

                                if (obj.ParentGroup.IsAttachment)
                                {
                                    ScenePresence sp = World.GetScenePresence(obj.ParentGroup.AttachedAvatar);

                                    if (sp != null)
                                        rot = sp.GetWorldRotation();
                                }
                                else
                                {
                                    if (obj.ParentGroup.RootPart == obj)
                                        rot = obj.ParentGroup.GroupRotation;
                                    else
                                        rot = obj.GetWorldRotation();
                                }

                                LSL_Rotation objrot = new LSL_Rotation(rot);
                                ret.Add(objrot);

                                break;
                            case ScriptBaseClass.OBJECT_VELOCITY:
                                Vector3 vel = Vector3.Zero;

                                if (obj.ParentGroup.IsAttachment)
                                {
                                    ScenePresence sp = World.GetScenePresence(obj.ParentGroup.AttachedAvatar);

                                    if (sp != null)
                                        vel = sp.GetWorldVelocity();
                                }
                                else
                                {
                                    vel = obj.Velocity; 
                                }

                                ret.Add(vel);
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
                                {
                                    ret.Add(new LSL_Integer(0)); // Always false if attached
                                }
                                else
                                {
                                    ret.Add(new LSL_Integer(obj.ParentGroup.UsesPhysics ? 1 : 0));
                                }
                                break;
                            case ScriptBaseClass.OBJECT_PHANTOM:
                                if (obj.ParentGroup.AttachmentPoint != 0)
                                {
                                    ret.Add(new LSL_Integer(0)); // Always false if attached
                                }
                                else
                                {
                                    ret.Add(new LSL_Integer(obj.ParentGroup.IsPhantom ? 1 : 0));
                                }
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
                            default:
                                // Invalid or unhandled constant.
                                ret.Add(new LSL_Integer(ScriptBaseClass.OBJECT_UNKNOWN_DETAIL));
                                break;
                        }
                    }

                    return ret;
                }
            }

            return new LSL_List();
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
            {
                text = text.Substring(0, 1023);
            }

            World.SimChat(Utils.StringToBytes(text), ChatTypeEnum.DebugChannel, ScriptBaseClass.DEBUG_CHANNEL,
                m_host.ParentGroup.RootPart.AbsolutePosition, m_host.Name, m_host.UUID, false);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            if (wComm != null)
            {
                wComm.DeliverMessage(ChatTypeEnum.Shout, ScriptBaseClass.DEBUG_CHANNEL, m_host.Name, m_host.UUID, text);
            }
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
                    UUID uuid = UUID.Zero;
                    UUID.TryParse(i, out uuid);
                    cb(uuid, a);
                });
        }

        public LSL_String llGetNumberOfNotecardLines(string name)
        {
            m_host.AddScriptLPS(1);

            UUID assetID = UUID.Zero;

            if (!UUID.TryParse(name, out assetID))
            {
                TaskInventoryItem item = m_host.Inventory.GetInventoryItem(name);

                if (item != null && item.Type == 7)
                    assetID = item.AssetID;
            }

            if (assetID == UUID.Zero)
            {
                // => complain loudly, as specified by the LSL docs
                Error("llGetNumberOfNotecardLines", "Can't find notecard '" + name + "'");

                return UUID.Zero.ToString();
            }

            string reqIdentifier = UUID.Random().ToString();

            // was: UUID tid = tid = AsyncCommands.
            UUID tid = AsyncCommands.DataserverPlugin.RegisterRequest(m_host.LocalId, m_item.ItemID, reqIdentifier);

            if (NotecardCache.IsCached(assetID))
            {
                AsyncCommands.DataserverPlugin.DataserverReply(reqIdentifier, NotecardCache.GetLines(assetID).ToString());

                ScriptSleep(m_sleepMsOnGetNumberOfNotecardLines);
                return tid.ToString();
            }

            WithNotecard(assetID, delegate (UUID id, AssetBase a)
            {
                if (a == null || a.Type != 7)
                {
                    Error("llGetNumberOfNotecardLines", "Can't find notecard '" + name + "'");
                    return;
                }

                NotecardCache.Cache(id, a.Data);
                AsyncCommands.DataserverPlugin.DataserverReply(reqIdentifier, NotecardCache.GetLines(id).ToString());
            });

            ScriptSleep(m_sleepMsOnGetNumberOfNotecardLines);
            return tid.ToString();
        }

        public LSL_String llGetNotecardLine(string name, int line)
        {
            m_host.AddScriptLPS(1);

            UUID assetID = UUID.Zero;

            if (!UUID.TryParse(name, out assetID))
            {
                TaskInventoryItem item = m_host.Inventory.GetInventoryItem(name);

                if (item != null && item.Type == 7)
                    assetID = item.AssetID;
            }

            if (assetID == UUID.Zero)
            {
                // => complain loudly, as specified by the LSL docs
                Error("llGetNotecardLine", "Can't find notecard '" + name + "'");

                return UUID.Zero.ToString();
            }

            string reqIdentifier = UUID.Random().ToString();

            // was: UUID tid = tid = AsyncCommands.
            UUID tid = AsyncCommands.DataserverPlugin.RegisterRequest(m_host.LocalId, m_item.ItemID, reqIdentifier);

            if (NotecardCache.IsCached(assetID))
            {
                AsyncCommands.DataserverPlugin.DataserverReply(
                    reqIdentifier, NotecardCache.GetLine(assetID, line, m_notecardLineReadCharsMax));

                ScriptSleep(m_sleepMsOnGetNotecardLine);
                return tid.ToString();
            }

            WithNotecard(assetID, delegate (UUID id, AssetBase a)
                         {
                             if (a == null || a.Type != 7)
                             {
                                 Error("llGetNotecardLine", "Can't find notecard '" + name + "'");
                                 return;
                             }

                             string data = Encoding.UTF8.GetString(a.Data);
                             //m_log.Debug(data);
                             NotecardCache.Cache(id, a.Data);
                             AsyncCommands.DataserverPlugin.DataserverReply(
                                reqIdentifier, NotecardCache.GetLine(assetID, line, m_notecardLineReadCharsMax));
                         });

            ScriptSleep(m_sleepMsOnGetNotecardLine);
            return tid.ToString();
        }

        public void SetPrimitiveParamsEx(LSL_Key prim, LSL_List rules, string originFunc)
        {
            SceneObjectPart obj = World.GetSceneObjectPart(new UUID(prim));
            if (obj == null)
                return;

            if (obj.OwnerID != m_host.OwnerID)
                return;

            SetEntityParams(new List<ISceneEntity>() { obj }, rules, originFunc);
        }

        public LSL_List GetPrimitiveParamsEx(LSL_Key prim, LSL_List rules)
        {
           SceneObjectPart obj = World.GetSceneObjectPart(new UUID(prim));

            LSL_List result = new LSL_List();

            if (obj != null && obj.OwnerID == m_host.OwnerID)
            {
                LSL_List remaining = GetPrimParams(obj, rules, ref result);

                while (remaining.Length > 2)
                {
                    int linknumber = remaining.GetLSLIntegerItem(0);
                    rules = remaining.GetSublist(1, -1);
                    List<SceneObjectPart> parts = GetLinkParts(linknumber);

                    foreach (SceneObjectPart part in parts)
                        remaining = GetPrimParams(part, rules, ref result);
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

        private string Name2Username(string name)
        {
            string[] parts = name.Split(new char[] {' '});
            if (parts.Length < 2)
                return name.ToLower();
            if (parts[1] == "Resident")
                return parts[0].ToLower();

            return name.Replace(" ", ".").ToLower();
        }

        public LSL_String llGetUsername(string id)
        {
            return Name2Username(llKey2Name(id));
        }

        public LSL_String llRequestUsername(string id)
        {
            UUID rq = UUID.Random();

            AsyncCommands.DataserverPlugin.RegisterRequest(m_host.LocalId, m_item.ItemID, rq.ToString());

            AsyncCommands.DataserverPlugin.DataserverReply(rq.ToString(), Name2Username(llKey2Name(id)));

            return rq.ToString();
        }

        public LSL_String llGetDisplayName(string id)
        {
            return llKey2Name(id);
        }

        public LSL_String llRequestDisplayName(string id)
        {
            UUID rq = UUID.Random();

            AsyncCommands.DataserverPlugin.RegisterRequest(m_host.LocalId, m_item.ItemID, rq.ToString());

            AsyncCommands.DataserverPlugin.DataserverReply(rq.ToString(), llKey2Name(id));

            return rq.ToString();
        }
/*
        private void SayShoutTimerElapsed(Object sender, ElapsedEventArgs args)
        {
            m_SayShoutCount = 0;
        }
*/
        private struct Tri
        {
            public Vector3 p1;
            public Vector3 p2;
            public Vector3 p3;
        }

        private bool InBoundingBox(ScenePresence avatar, Vector3 point)
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

        private ContactResult[] AvatarIntersection(Vector3 rayStart, Vector3 rayEnd)
        {
            List<ContactResult> contacts = new List<ContactResult>();

            Vector3 ab = rayEnd - rayStart;

            World.ForEachScenePresence(delegate(ScenePresence sp)
            {
                Vector3 ac = sp.AbsolutePosition - rayStart;
//                Vector3 bc = sp.AbsolutePosition - rayEnd;

                double d = Math.Abs(Vector3.Mag(Vector3.Cross(ab, ac)) / Vector3.Distance(rayStart, rayEnd));

                if (d > 1.5)
                    return;

                double d2 = Vector3.Dot(Vector3.Negate(ab), ac);

                if (d2 > 0)
                    return;

                double dp = Math.Sqrt(Vector3.Mag(ac) * Vector3.Mag(ac) - d * d);
                Vector3 p = rayStart + Vector3.Divide(Vector3.Multiply(ab, (float)dp), (float)Vector3.Mag(ab));

                if (!InBoundingBox(sp, p))
                    return;

                ContactResult result = new ContactResult ();
                result.ConsumerID = sp.LocalId;
                result.Depth = Vector3.Distance(rayStart, p);
                result.Normal = Vector3.Zero;
                result.Pos = p;

                contacts.Add(result);
            });

            return contacts.ToArray();
        }

        private ContactResult[] ObjectIntersection(Vector3 rayStart, Vector3 rayEnd, bool includePhysical, bool includeNonPhysical, bool includePhantom)
        {
            Ray ray = new Ray(rayStart, Vector3.Normalize(rayEnd - rayStart));
            List<ContactResult> contacts = new List<ContactResult>();

            Vector3 ab = rayEnd - rayStart;

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
                float minX;
                float maxX;
                float minY;
                float maxY;
                float minZ;
                float maxZ;

                float radius = 0.0f;

                group.GetAxisAlignedBoundingBoxRaw(out minX, out maxX, out minY, out maxY, out minZ, out maxZ);

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
                radius = radius*1.413f;
                Vector3 ac = group.AbsolutePosition - rayStart;
//                Vector3 bc = group.AbsolutePosition - rayEnd;

                double d = Math.Abs(Vector3.Mag(Vector3.Cross(ab, ac)) / Vector3.Distance(rayStart, rayEnd));

                // Too far off ray, don't bother
                if (d > radius)
                    return;

                // Behind ray, drop
                double d2 = Vector3.Dot(Vector3.Negate(ab), ac);
                if (d2 > 0)
                    return;

                ray = new Ray(rayStart, Vector3.Normalize(rayEnd - rayStart));
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

                ContactResult result = new ContactResult ();
                result.ConsumerID = group.LocalId;
//                result.Depth = intersection.distance;
                result.Normal = intersection.normal;
                result.Pos = intersection.ipoint;
                result.Depth = Vector3.Mag(rayStart - result.Pos);

                contacts.Add(result);
            });

            return contacts.ToArray();
        }

        private ContactResult? GroundIntersection(Vector3 rayStart, Vector3 rayEnd)
        {
            double[,] heightfield = World.Heightmap.GetDoubles();
            List<ContactResult> contacts = new List<ContactResult>();

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

            List<Tri> trilist = new List<Tri>();

            // Create our triangle list
            for (int x = 1 ; x < World.Heightmap.Width ; x++)
            {
                for (int y = 1 ; y < World.Heightmap.Height ; y++)
                {
                    Tri t1 = new Tri();
                    Tri t2 = new Tri();

                    Vector3 p1 = new Vector3(x-1, y-1, (float)heightfield[x-1, y-1]);
                    Vector3 p2 = new Vector3(x, y-1, (float)heightfield[x, y-1]);
                    Vector3 p3 = new Vector3(x, y, (float)heightfield[x, y]);
                    Vector3 p4 = new Vector3(x-1, y, (float)heightfield[x-1, y]);

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

                if (n == Vector3.Zero)
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
                ContactResult result = new ContactResult ();
                result.ConsumerID = 0;
                result.Depth = Vector3.Distance(rayStart, ip);
                result.Normal = n;
                result.Pos = ip;

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
            // Use llCastRay V3 if configured
            if (m_useCastRayV3)
                return llCastRayV3(start, end, options);

            LSL_List list = new LSL_List();

            m_host.AddScriptLPS(1);

            Vector3 rayStart = start;
            Vector3 rayEnd = end;
            Vector3 dir = rayEnd - rayStart;

            float dist = Vector3.Mag(dir);

            int count = 1;
            bool detectPhantom = false;
            int dataFlags = 0;
            int rejectTypes = 0;

            for (int i = 0; i < options.Length; i += 2)
            {
                if (options.GetLSLIntegerItem(i) == ScriptBaseClass.RC_MAX_HITS)
                    count = options.GetLSLIntegerItem(i + 1);
                else if (options.GetLSLIntegerItem(i) == ScriptBaseClass.RC_DETECT_PHANTOM)
                    detectPhantom = (options.GetLSLIntegerItem(i + 1) > 0);
                else if (options.GetLSLIntegerItem(i) == ScriptBaseClass.RC_DATA_FLAGS)
                    dataFlags = options.GetLSLIntegerItem(i + 1);
                else if (options.GetLSLIntegerItem(i) == ScriptBaseClass.RC_REJECT_TYPES)
                    rejectTypes = options.GetLSLIntegerItem(i + 1);
            }

            if (count > 16)
                count = 16;

            List<ContactResult> results = new List<ContactResult>();

            bool checkTerrain = !((rejectTypes & ScriptBaseClass.RC_REJECT_LAND) == ScriptBaseClass.RC_REJECT_LAND);
            bool checkAgents = !((rejectTypes & ScriptBaseClass.RC_REJECT_AGENTS) == ScriptBaseClass.RC_REJECT_AGENTS);
            bool checkNonPhysical = !((rejectTypes & ScriptBaseClass.RC_REJECT_NONPHYSICAL) == ScriptBaseClass.RC_REJECT_NONPHYSICAL);
            bool checkPhysical = !((rejectTypes & ScriptBaseClass.RC_REJECT_PHYSICAL) == ScriptBaseClass.RC_REJECT_PHYSICAL);


            if (World.SupportsRayCastFiltered())
            {
                if (dist == 0)
                    return list;

                RayFilterFlags rayfilter = RayFilterFlags.BackFaceCull;
                if (checkTerrain)
                    rayfilter |= RayFilterFlags.land;
//                if (checkAgents)
//                    rayfilter |= RayFilterFlags.agent;
                if (checkPhysical)
                    rayfilter |= RayFilterFlags.physical;
                if (checkNonPhysical)
                    rayfilter |= RayFilterFlags.nonphysical;
                if (detectPhantom)
                    rayfilter |= RayFilterFlags.LSLPhantom;

                Vector3 direction = dir * ( 1/dist);

                if(rayfilter == 0)
                {
                    list.Add(new LSL_Integer(0));
                    return list;
                }

                // get some more contacts to sort ???
                int physcount = 4 * count;
                if (physcount > 20)
                    physcount = 20;

                object physresults;
                physresults = World.RayCastFiltered(rayStart, direction, dist, physcount, rayfilter);

                if (physresults == null)
                {
                    list.Add(new LSL_Integer(-3)); // timeout error
                    return list;
                }

                results = (List<ContactResult>)physresults;

                // for now physics doesn't detect sitted avatars so do it outside physics
                if (checkAgents)
                {
                    ContactResult[] agentHits = AvatarIntersection(rayStart, rayEnd);
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
            }
            else
            {
                if (checkAgents)
                {
                    ContactResult[] agentHits = AvatarIntersection(rayStart, rayEnd);
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
            }

            // Double check this
            if (checkTerrain)
            {
                bool skipGroundCheck = false;

                foreach (ContactResult c in results)
                {
                    if (c.ConsumerID == 0) // Physics gave us a ground collision
                        skipGroundCheck = true;
                }

                if (!skipGroundCheck)
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
                if (m_host.LocalId == result.ConsumerID)
                    continue;

                UUID itemID = UUID.Zero;
                int linkNum = 0;

                SceneObjectPart part = World.GetSceneObjectPart(result.ConsumerID);
                // It's a prim!
                if (part != null)
                {
                    // dont detect members of same object ???
                    if (part.ParentGroup == thisgrp)
                        continue;

                    if ((dataFlags & ScriptBaseClass.RC_GET_ROOT_KEY) == ScriptBaseClass.RC_GET_ROOT_KEY)
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

                if ((dataFlags & ScriptBaseClass.RC_GET_LINK_NUM) == ScriptBaseClass.RC_GET_LINK_NUM)
                    list.Add(new LSL_Integer(linkNum));

                if ((dataFlags & ScriptBaseClass.RC_GET_NORMAL) == ScriptBaseClass.RC_GET_NORMAL)
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
            m_host.AddScriptLPS(1);
            LSL_List result = new LSL_List();

            // Prepare throttle data
            int calledMs = Environment.TickCount;
            Stopwatch stopWatch = new Stopwatch();
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
                    else if (m_castRayCalls[i].RegionId == regionId)
                    {
                        // Reduce available time with recent calls
                        if (m_castRayCalls[i].UserId == userId)
                            msAvailable -= m_castRayCalls[i].UsedMs;
                    }
                }
            }

            // Return failure if not enough available time
            if (msAvailable < m_msMinInCastRay)
            {
                result.Add(new LSL_Integer(ScriptBaseClass.RCERR_CAST_TIME_EXCEEDED));
                return result;
            }

            // Initialize
            List<RayHit> rayHits = new List<RayHit>();
            float tol = m_floatToleranceInCastRay;
            Vector3 pos1Ray = start;
            Vector3 pos2Ray = end;

            // Get input options
            int rejectTypes = 0;
            int dataFlags = 0;
            int maxHits = 1;
            bool detectPhantom = false;
            for (int i = 0; i < options.Length; i += 2)
            {
                if (options.GetLSLIntegerItem(i) == ScriptBaseClass.RC_REJECT_TYPES)
                    rejectTypes = options.GetLSLIntegerItem(i + 1);
                else if (options.GetLSLIntegerItem(i) == ScriptBaseClass.RC_DATA_FLAGS)
                    dataFlags = options.GetLSLIntegerItem(i + 1);
                else if (options.GetLSLIntegerItem(i) == ScriptBaseClass.RC_MAX_HITS)
                    maxHits = options.GetLSLIntegerItem(i + 1);
                else if (options.GetLSLIntegerItem(i) == ScriptBaseClass.RC_DETECT_PHANTOM)
                    detectPhantom = (options.GetLSLIntegerItem(i + 1) != 0);
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
                    // Check group filters unless part filters are configured
                    bool isPhysical = (group.RootPart != null && group.RootPart.PhysActor != null && group.RootPart.PhysActor.IsPhysical);
                    bool isNonphysical = !isPhysical;
                    bool isPhantom = group.IsPhantom || group.IsVolumeDetect;
                    bool isAttachment = group.IsAttachment;
                    bool doGroup = true;
                    if (isPhysical && rejectPhysical)
                        doGroup = false;
                    if (isNonphysical && rejectNonphysical)
                        doGroup = false;
                    if (isPhantom && detectPhantom)
                        doGroup = true;
                    if (m_filterPartsInCastRay)
                        doGroup = true;
                    if (isAttachment && !m_doAttachmentsInCastRay)
                        doGroup = false;
                    // Parse object/group if passed filters
                    if (doGroup)
                    {
                        // Iterate over all prims/parts in object/group
                        foreach(SceneObjectPart part in group.Parts)
                        {
                            // Check part filters if configured
                            if (m_filterPartsInCastRay)
                            {
                                isPhysical = (part.PhysActor != null && part.PhysActor.IsPhysical);
                                isNonphysical = !isPhysical;
                                isPhantom = ((part.Flags & PrimFlags.Phantom) != 0) || (part.VolumeDetectActive);
                                bool doPart = true;
                                if (isPhysical && rejectPhysical)
                                    doPart = false;
                                if (isNonphysical && rejectNonphysical)
                                    doPart = false;
                                if (isPhantom && detectPhantom)
                                    doPart = true;
                                if (!doPart)
                                    continue;
                            }

                            // Parse prim/part and project ray if passed filters
                            Vector3 scalePart = part.Scale;
                            Vector3 posPart = part.GetWorldPosition();
                            Quaternion rotPart = part.GetWorldRotation();
                            Quaternion rotPartInv = Quaternion.Inverse(rotPart);
                            Vector3 pos1RayProj = ((pos1Ray - posPart) * rotPartInv) / scalePart;
                            Vector3 pos2RayProj = ((pos2Ray - posPart) * rotPartInv) / scalePart;

                            // Filter parts by shape bounding boxes
                            Vector3 shapeBoxMax = new Vector3(0.5f, 0.5f, 0.5f);
                            if (!part.Shape.SculptEntry)
                                shapeBoxMax = shapeBoxMax * (new Vector3(m_primSafetyCoeffX, m_primSafetyCoeffY, m_primSafetyCoeffZ));
                            shapeBoxMax = shapeBoxMax + (new Vector3(tol, tol, tol));
                            if (RayIntersectsShapeBox(pos1RayProj, pos2RayProj, shapeBoxMax))
                            {
                                // Prepare data needed to check for ray hits
                                RayTrans rayTrans = new RayTrans();
                                rayTrans.PartId = part.UUID;
                                rayTrans.GroupId = part.ParentGroup.UUID;
                                rayTrans.Link = group.PrimCount > 1 ? part.LinkNum : 0;
                                rayTrans.ScalePart = scalePart;
                                rayTrans.PositionPart = posPart;
                                rayTrans.RotationPart = rotPart;
                                rayTrans.ShapeNeedsEnds = true;
                                rayTrans.Position1Ray = pos1Ray;
                                rayTrans.Position1RayProj = pos1RayProj;
                                rayTrans.VectorRayProj = pos2RayProj - pos1RayProj;

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
                                        AssetMesh meshAsset = new AssetMesh(omvPrim.Sculpt.SculptTexture, sculptAsset);
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
                        Vector3 lower;
                        Vector3 upper;
                        BoundingBoxOfScenePresence(sp, out lower, out upper);
                        // Parse avatar
                        Vector3 scalePart = upper - lower;
                        Vector3 posPart = sp.AbsolutePosition;
                        Quaternion rotPart = sp.GetWorldRotation();
                        Quaternion rotPartInv = Quaternion.Inverse(rotPart);
                        posPart = posPart + (lower + upper) * 0.5f * rotPart;
                        // Project ray
                        Vector3 pos1RayProj = ((pos1Ray - posPart) * rotPartInv) / scalePart;
                        Vector3 pos2RayProj = ((pos2Ray - posPart) * rotPartInv) / scalePart;

                        // Filter avatars by shape bounding boxes
                        Vector3 shapeBoxMax = new Vector3(0.5f + tol, 0.5f + tol, 0.5f + tol);
                        if (RayIntersectsShapeBox(pos1RayProj, pos2RayProj, shapeBoxMax))
                        {
                            // Prepare data needed to check for ray hits
                            RayTrans rayTrans = new RayTrans();
                            rayTrans.PartId = sp.UUID;
                            rayTrans.GroupId = sp.ParentPart != null ? sp.ParentPart.ParentGroup.UUID : sp.UUID;
                            rayTrans.Link = sp.ParentPart != null ? UUID2LinkNumber(sp.ParentPart, sp.UUID) : 0;
                            rayTrans.ScalePart = scalePart;
                            rayTrans.PositionPart = posPart;
                            rayTrans.RotationPart = rotPart;
                            rayTrans.ShapeNeedsEnds = false;
                            rayTrans.Position1Ray = pos1Ray;
                            rayTrans.Position1RayProj = pos1RayProj;
                            rayTrans.VectorRayProj = pos2RayProj - pos1RayProj;

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
                Vector3 lower;
                Vector3 upper;
                List<Tri> triangles = TrisFromHeightmapUnderRay(pos1Ray, pos2Ray, out lower, out upper);
                lower.Z -= tol;
                upper.Z += tol;
                if ((pos1Ray.Z >= lower.Z || pos2Ray.Z >= lower.Z) && (pos1Ray.Z <= upper.Z || pos2Ray.Z <= upper.Z))
                {
                    // Prepare data needed to check for ray hits
                    RayTrans rayTrans = new RayTrans();
                    rayTrans.PartId = UUID.Zero;
                    rayTrans.GroupId = UUID.Zero;
                    rayTrans.Link = 0;
                    rayTrans.ScalePart = new Vector3 (1.0f, 1.0f, 1.0f);
                    rayTrans.PositionPart = Vector3.Zero;
                    rayTrans.RotationPart = Quaternion.Identity;
                    rayTrans.ShapeNeedsEnds = true;
                    rayTrans.Position1Ray = pos1Ray;
                    rayTrans.Position1RayProj = pos1Ray;
                    rayTrans.VectorRayProj = vecRay;

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
                    Hashtable hits = new Hashtable();
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
            CastRayCall castRayCall = new CastRayCall();
            castRayCall.RegionId = regionId;
            castRayCall.UserId = userId;
            castRayCall.CalledMs = calledMs;
            castRayCall.UsedMs = (int)stopWatch.ElapsedMilliseconds;
            lock (m_castRayCalls)
            {
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
            int sign = 0;
            float dist = 0.0f;
            Vector3 posProj = Vector3.Zero;
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
                        Tri triangle = new Tri();
                        triangle.p1 = face.Vertices[face.Indices[i]].Position;
                        triangle.p2 = face.Vertices[face.Indices[i + 1]].Position;
                        triangle.p3 = face.Vertices[face.Indices[i + 2]].Position;
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
            Vector3 posHitProj;
            Vector3 normalProj;
            if (HitRayInTri(triProj, rayTrans.Position1RayProj, rayTrans.VectorRayProj, out posHitProj, out normalProj))
            {
                // Hack to circumvent ghost face bug in PrimMesher by removing hits in (ghost) face plane through shape center
                if (Math.Abs(Vector3.Dot(posHitProj, normalProj)) < m_floatToleranceInCastRay && !rayTrans.ShapeNeedsEnds)
                    return;

                // Transform hit and normal to region coordinate system
                Vector3 posHit = rayTrans.PositionPart + (posHitProj * rayTrans.ScalePart) * rayTrans.RotationPart;
                Vector3 normal = Vector3.Normalize((normalProj * rayTrans.ScalePart) * rayTrans.RotationPart);

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
                RayHit rayHit = new RayHit();
                rayHit.PartId = rayTrans.PartId;
                rayHit.GroupId = rayTrans.GroupId;
                rayHit.Link = rayTrans.Link;
                rayHit.Position = posHit;
                rayHit.Normal = normal;
                rayHit.Distance = distance;
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
            List<Tri> triangles = new List<Tri>();

            // Set parsing lane direction to major ray X-Y axis
            Vector3 vec = posEnd - posStart;
            float xAbs = Math.Abs(vec.X);
            float yAbs = Math.Abs(vec.Y);
            bool bigX = true;
            if (yAbs > xAbs)
            {
                bigX = false;
                vec = vec / yAbs;
            }
            else if (xAbs > yAbs || xAbs > 0.0f)
                vec = vec / xAbs;
            else
                vec = new Vector3(1.0f, 1.0f, 0.0f);

            // Simplify by start parsing in lower end of lane
            if ((bigX && vec.X < 0.0f) || (!bigX && vec.Y < 0.0f))
            {
                Vector3 posTemp = posStart;
                posStart = posEnd;
                posEnd = posTemp;
                vec = vec * -1.0f;
            }

            // First 1x1 rectangle under ray
            float xFloorOld = 0.0f;
            float yFloorOld = 0.0f;
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
                pos = pos + vec;

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
            int x = Util.Clamp<int>(xInt+1, 0, World.Heightmap.Width - 1);
            int y = Util.Clamp<int>(yInt+1, 0, World.Heightmap.Height - 1);
            Vector3 pos1 = new Vector3(x, y, (float)World.Heightmap[x, y]);
            // Adjust bounding box
            zLower = Math.Min(zLower, pos1.Z);
            zUpper = Math.Max(zUpper, pos1.Z);

            // Corner 2 of 1x1 rectangle
            x = Util.Clamp<int>(xInt, 0, World.Heightmap.Width - 1);
            y = Util.Clamp<int>(yInt+1, 0, World.Heightmap.Height - 1);
            Vector3 pos2 = new Vector3(x, y, (float)World.Heightmap[x, y]);
            // Adjust bounding box
            zLower = Math.Min(zLower, pos2.Z);
            zUpper = Math.Max(zUpper, pos2.Z);

            // Corner 3 of 1x1 rectangle
            x = Util.Clamp<int>(xInt, 0, World.Heightmap.Width - 1);
            y = Util.Clamp<int>(yInt, 0, World.Heightmap.Height - 1);
            Vector3 pos3 = new Vector3(x, y, (float)World.Heightmap[x, y]);
            // Adjust bounding box
            zLower = Math.Min(zLower, pos3.Z);
            zUpper = Math.Max(zUpper, pos3.Z);

            // Corner 4 of 1x1 rectangle
            x = Util.Clamp<int>(xInt+1, 0, World.Heightmap.Width - 1);
            y = Util.Clamp<int>(yInt, 0, World.Heightmap.Height - 1);
            Vector3 pos4 = new Vector3(x, y, (float)World.Heightmap[x, y]);
            // Adjust bounding box
            zLower = Math.Min(zLower, pos4.Z);
            zUpper = Math.Max(zUpper, pos4.Z);

            // Add triangle 1
            Tri triangle1 = new Tri();
            triangle1.p1 = pos1;
            triangle1.p2 = pos2;
            triangle1.p3 = pos3;
            triangles.Add(triangle1);

            // Add triangle 2
            Tri triangle2 = new Tri();
            triangle2.p1 = pos3;
            triangle2.p2 = pos4;
            triangle2.p3 = pos1;
            triangles.Add(triangle2);
        }

        /// <summary>
        /// Helper to get link number for a UUID.
        /// </summary>
        private int UUID2LinkNumber(SceneObjectPart part, UUID id)
        {
            SceneObjectGroup group = part.ParentGroup;
            if (group != null)
            {
                // Parse every link for UUID
                int linkCount = group.PrimCount + group.GetSittingAvatarsCount();
                for (int link = linkCount; link > 0; link--)
                {
                    ISceneEntity entity = GetLinkEntity(part, link);
                    // Return link number if UUID match
                    if (entity != null && entity.UUID == id)
                        return link;
                }
            }
            // Return link number 0 if no links or UUID matches
            return 0;
        }

        public LSL_Integer llManageEstateAccess(int action, string avatar)
        {
            m_host.AddScriptLPS(1);
            EstateSettings estate = World.RegionInfo.EstateSettings;
            bool isAccount = false;
            bool isGroup = false;

            if (!estate.IsEstateOwner(m_host.OwnerID) || !estate.IsEstateManagerOrOwner(m_host.OwnerID))
                return 0;

            UUID id = new UUID();
            if (!UUID.TryParse(avatar, out id))
                return 0;

            UserAccount account = World.UserAccountService.GetUserAccount(World.RegionInfo.ScopeID, id);
            isAccount = account != null ? true : false;
            if (!isAccount)
            {
                IGroupsModule groups = World.RequestModuleInterface<IGroupsModule>();
                if (groups != null)
                {
                    GroupRecord group = groups.GetGroupRecord(id);
                    isGroup = group != null ? true : false;
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
                    if (estate.IsBanned(id, World.GetUserFlags(id))) return 1;
                    EstateBan ban = new EstateBan();
                    ban.EstateID = estate.EstateID;
                    ban.BannedUserID = id;
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
            m_host.AddScriptLPS(1);
            // The value returned for Mono scripts in SL
            return 65536;
        }

        public LSL_Integer llSetMemoryLimit(LSL_Integer limit)
        {
            m_host.AddScriptLPS(1);
            // Treat as an LSO script
            return ScriptBaseClass.FALSE;
        }

        public LSL_Integer llGetSPMaxMemory()
        {
            m_host.AddScriptLPS(1);
            // The value returned for Mono scripts in SL
            return 65536;
        }

        public virtual LSL_Integer llGetUsedMemory()
        {
            m_host.AddScriptLPS(1);
            // The value returned for Mono scripts in SL
            return 65536;
        }

        public void llScriptProfiler(LSL_Integer flags)
        {
            m_host.AddScriptLPS(1);
            // This does nothing for LSO scripts in SL
        }

        #region Not Implemented
        //
        // Listing the unimplemented lsl functions here, please move
        // them from this region as they are completed
        //

        public void llSetSoundQueueing(int queue)
        {
            m_host.AddScriptLPS(1);

            if (m_SoundModule != null)
                m_SoundModule.SetSoundQueueing(m_host.UUID, queue == ScriptBaseClass.TRUE.value);
        }

        public void llCollisionSprite(string impact_sprite)
        {
            m_host.AddScriptLPS(1);
            // Viewer 2.0 broke this and it's likely LL has no intention
            // of fixing it. Therefore, letting this be a NOP seems appropriate.
        }

        public void llGodLikeRezObject(string inventory, LSL_Vector pos)
        {
            m_host.AddScriptLPS(1);

            if (!World.Permissions.IsGod(m_host.OwnerID))
                NotImplemented("llGodLikeRezObject");

            AssetBase rezAsset = World.AssetService.Get(inventory);
            if (rezAsset == null)
            {
                llSay(0, "Asset not found");
                return;
            }

            SceneObjectGroup group = null;

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

            group.RootPart.AttachPoint = group.RootPart.Shape.State;
            group.RootPart.AttachedPos = group.AbsolutePosition;

            group.ResetIDs();

            Vector3 llpos = new Vector3((float)pos.x, (float)pos.y, (float)pos.z);
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
                    new DetectParams[0]));
        }

        public LSL_String llTransferLindenDollars(string destination, int amount)
        {
            UUID txn = UUID.Random();

            Util.FireAndForget(delegate(object x)
            {
                int replycode = 0;
                string replydata = destination + "," + amount.ToString();

                try
                {
                    TaskInventoryItem item = m_item;
                    if (item == null)
                    {
                        replydata = "SERVICE_ERROR";
                        return;
                    }

                    m_host.AddScriptLPS(1);

                    if (item.PermsGranter == UUID.Zero)
                    {
                        replydata = "MISSING_PERMISSION_DEBIT";
                        return;
                    }

                    if ((item.PermsMask & ScriptBaseClass.PERMISSION_DEBIT) == 0)
                    {
                        replydata = "MISSING_PERMISSION_DEBIT";
                        return;
                    }

                    UUID toID = new UUID();

                    if (!UUID.TryParse(destination, out toID))
                    {
                        replydata = "INVALID_AGENT";
                        return;
                    }

                    IMoneyModule money = World.RequestModuleInterface<IMoneyModule>();

                    if (money == null)
                    {
                        replydata = "TRANSFERS_DISABLED";
                        return;
                    }

                    string reason;
                    bool result = money.ObjectGiveMoney(
                        m_host.ParentGroup.RootPart.UUID, m_host.ParentGroup.RootPart.OwnerID, toID, amount, txn, out reason);

                    if (result)
                    {
                        replycode = 1;
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
                            new DetectParams[0]));
                }
            }, null, "LSL_Api.llTransferLindenDollars");

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
                    int code = rules.GetLSLIntegerItem(idx++);

                    int remain = rules.Length - idx;
                    idxStart = idx;

                    switch (code)
                    {
                        case (int)ScriptBaseClass.PRIM_POSITION:
                        case (int)ScriptBaseClass.PRIM_POS_LOCAL:
                            {
                                if (remain < 1)
                                    return new LSL_List();

                                LSL_Vector v;
                                v = rules.GetVector3Item(idx++);

                                if(!av.LegacySitOffsets)
                                {
                                    LSL_Vector sitOffset = (llRot2Up(new LSL_Rotation(av.Rotation.X, av.Rotation.Y, av.Rotation.Z, av.Rotation.W)) * av.Appearance.AvatarHeight * 0.02638f);

                                    v = v + 2 * sitOffset;
                                }

                                av.OffsetPosition = new Vector3((float)v.x, (float)v.y, (float)v.z);
                                positionChanged = true;
                            }
                            break;

                        case (int)ScriptBaseClass.PRIM_ROTATION:
                            {
                                if (remain < 1)
                                    return new LSL_List();

                                Quaternion r;
                                r = rules.GetQuaternionItem(idx++);

                                av.Rotation = m_host.GetWorldRotation() * r;
                                positionChanged = true;
                            }
                            break;

                        case (int)ScriptBaseClass.PRIM_ROT_LOCAL:
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
                        case (int)ScriptBaseClass.PRIM_SIZE:
                        case (int)ScriptBaseClass.PRIM_MATERIAL:
                        case (int)ScriptBaseClass.PRIM_PHANTOM:
                        case (int)ScriptBaseClass.PRIM_PHYSICS:
                        case (int)ScriptBaseClass.PRIM_PHYSICS_SHAPE_TYPE:
                        case (int)ScriptBaseClass.PRIM_TEMP_ON_REZ:
                        case (int)ScriptBaseClass.PRIM_NAME:
                        case (int)ScriptBaseClass.PRIM_DESC:
                            if (remain < 1)
                                return new LSL_List();
                            idx++;
                            break;

                        case (int)ScriptBaseClass.PRIM_GLOW:
                        case (int)ScriptBaseClass.PRIM_FULLBRIGHT:
                        case (int)ScriptBaseClass.PRIM_TEXGEN:
                            if (remain < 2)
                                return new LSL_List();
                            idx += 2;
                            break;

                        case (int)ScriptBaseClass.PRIM_TYPE:
                            if (remain < 3)
                                return new LSL_List();
                            code = (int)rules.GetLSLIntegerItem(idx++);
                            remain = rules.Length - idx;
                            switch (code)
                            {
                                case (int)ScriptBaseClass.PRIM_TYPE_BOX:
                                case (int)ScriptBaseClass.PRIM_TYPE_CYLINDER:
                                case (int)ScriptBaseClass.PRIM_TYPE_PRISM:
                                    if (remain < 6)
                                        return new LSL_List();
                                    idx += 6;
                                    break;

                                case (int)ScriptBaseClass.PRIM_TYPE_SPHERE:
                                    if (remain < 5)
                                        return new LSL_List();
                                    idx += 5;
                                    break;

                                case (int)ScriptBaseClass.PRIM_TYPE_TORUS:
                                case (int)ScriptBaseClass.PRIM_TYPE_TUBE:
                                case (int)ScriptBaseClass.PRIM_TYPE_RING:
                                    if (remain < 11)
                                        return new LSL_List();
                                    idx += 11;
                                    break;

                                case (int)ScriptBaseClass.PRIM_TYPE_SCULPT:
                                    if (remain < 2)
                                        return new LSL_List();
                                    idx += 2;
                                    break;
                            }
                            break;

                        case (int)ScriptBaseClass.PRIM_COLOR:
                        case (int)ScriptBaseClass.PRIM_TEXT:
                        case (int)ScriptBaseClass.PRIM_BUMP_SHINY:
                        case (int)ScriptBaseClass.PRIM_OMEGA:
                            if (remain < 3)
                                return new LSL_List();
                            idx += 3;
                            break;

                        case (int)ScriptBaseClass.PRIM_TEXTURE:
                        case (int)ScriptBaseClass.PRIM_POINT_LIGHT:
                        case (int)ScriptBaseClass.PRIM_PHYSICS_MATERIAL:
                            if (remain < 5)
                                return new LSL_List();
                            idx += 5;
                            break;

                        case (int)ScriptBaseClass.PRIM_FLEXIBLE:
                            if (remain < 7)
                                return new LSL_List();

                            idx += 7;
                            break;

                        case (int)ScriptBaseClass.PRIM_LINK_TARGET:
                            if (remain < 3) // setting to 3 on the basis that parsing any usage of PRIM_LINK_TARGET that has nothing following it is pointless.
                                return new LSL_List();

                            return rules.GetSublist(idx, -1);
                    }
                }
            }
            catch (InvalidCastException e)
            {
                Error(originFunc,string.Format(
                        " error running rule #{1}: arg #{2} ",
                         rulesParsed, idx - idxStart) + e.Message);
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
                int code = (int)rules.GetLSLIntegerItem(idx++);
                int remain = rules.Length - idx;

                switch (code)
                {
                    case (int)ScriptBaseClass.PRIM_MATERIAL:
                        res.Add(new LSL_Integer((int)SOPMaterialData.SopMaterial.Flesh));
                        break;

                    case (int)ScriptBaseClass.PRIM_PHYSICS:
                            res.Add(new LSL_Integer(0));
                        break;

                    case (int)ScriptBaseClass.PRIM_TEMP_ON_REZ:
                            res.Add(new LSL_Integer(0));
                        break;

                    case (int)ScriptBaseClass.PRIM_PHANTOM:
                            res.Add(new LSL_Integer(0));
                        break;

                    case (int)ScriptBaseClass.PRIM_POSITION:
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

                    case (int)ScriptBaseClass.PRIM_SIZE:
                        Vector3 s = avatar.Appearance.AvatarSize;
                        res.Add(new LSL_Vector(s.X, s.Y, s.Z));

                        break;

                    case (int)ScriptBaseClass.PRIM_ROTATION:
                        res.Add(new LSL_Rotation(avatar.GetWorldRotation()));
                        break;

                    case (int)ScriptBaseClass.PRIM_TYPE:
                        res.Add(new LSL_Integer(ScriptBaseClass.PRIM_TYPE_BOX));
                        res.Add(new LSL_Integer(ScriptBaseClass.PRIM_HOLE_DEFAULT));
                        res.Add(new LSL_Vector(0f,1.0f,0f));
                        res.Add(new LSL_Float(0.0f));
                        res.Add(new LSL_Vector(0, 0, 0));
                        res.Add(new LSL_Vector(1.0f,1.0f,0f));
                        res.Add(new LSL_Vector(0, 0, 0));
                        break;

                    case (int)ScriptBaseClass.PRIM_TEXTURE:
                        if (remain < 1)
                            return new LSL_List();

                        int face = (int)rules.GetLSLIntegerItem(idx++);
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

                    case (int)ScriptBaseClass.PRIM_COLOR:
                        if (remain < 1)
                            return new LSL_List();

                        face = (int)rules.GetLSLIntegerItem(idx++);

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

                    case (int)ScriptBaseClass.PRIM_BUMP_SHINY:
                        if (remain < 1)
                            return new LSL_List();
                        face = (int)rules.GetLSLIntegerItem(idx++);

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

                    case (int)ScriptBaseClass.PRIM_FULLBRIGHT:
                        if (remain < 1)
                            return new LSL_List();
                        face = (int)rules.GetLSLIntegerItem(idx++);

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

                    case (int)ScriptBaseClass.PRIM_FLEXIBLE:
                        res.Add(new LSL_Integer(0));
                        res.Add(new LSL_Integer(0));// softness
                        res.Add(new LSL_Float(0.0f));   // gravity
                        res.Add(new LSL_Float(0.0f));      // friction
                        res.Add(new LSL_Float(0.0f));      // wind
                        res.Add(new LSL_Float(0.0f));   // tension
                        res.Add(new LSL_Vector(0f,0f,0f));
                        break;

                    case (int)ScriptBaseClass.PRIM_TEXGEN:
                        // (PRIM_TEXGEN_DEFAULT, PRIM_TEXGEN_PLANAR)
                        if (remain < 1)
                            return new LSL_List();
                        face = (int)rules.GetLSLIntegerItem(idx++);

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

                    case (int)ScriptBaseClass.PRIM_POINT_LIGHT:
                        res.Add(new LSL_Integer(0));
                        res.Add(new LSL_Vector(0f,0f,0f));
                        res.Add(new LSL_Float(0f)); // intensity
                        res.Add(new LSL_Float(0f));    // radius
                        res.Add(new LSL_Float(0f));   // falloff
                        break;

                    case (int)ScriptBaseClass.PRIM_GLOW:
                        if (remain < 1)
                            return new LSL_List();
                        face = (int)rules.GetLSLIntegerItem(idx++);

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

                    case (int)ScriptBaseClass.PRIM_TEXT:
                        res.Add(new LSL_String(""));
                        res.Add(new LSL_Vector(0f,0f,0f));
                        res.Add(new LSL_Float(1.0f));
                        break;

                    case (int)ScriptBaseClass.PRIM_NAME:
                        res.Add(new LSL_String(avatar.Name));
                        break;

                    case (int)ScriptBaseClass.PRIM_DESC:
                        res.Add(new LSL_String(""));
                        break;

                    case (int)ScriptBaseClass.PRIM_ROT_LOCAL:                      
                        Quaternion lrot = avatar.Rotation;
                        res.Add(new LSL_Rotation(lrot.X, lrot.Y, lrot.Z, lrot.W));
                        break;

                    case (int)ScriptBaseClass.PRIM_POS_LOCAL:
                        Vector3 lpos = avatar.OffsetPosition;

                        if(!avatar.LegacySitOffsets)
                        {
                            Vector3 lsitOffset = (Zrot(avatar.Rotation)) * (avatar.Appearance.AvatarHeight * 0.02638f * 2.0f);
                            lpos -= lsitOffset;
                        }

                        res.Add(new LSL_Vector(lpos.X,lpos.Y,lpos.Z));
                        break;

                    case (int)ScriptBaseClass.PRIM_LINK_TARGET:
                        if (remain < 3) // setting to 3 on the basis that parsing any usage of PRIM_LINK_TARGET that has nothing following it is pointless.
                            return new LSL_List();

                        return rules.GetSublist(idx, -1);
                }
            }

            return new LSL_List();
        }

        public void llSetAnimationOverride(LSL_String animState, LSL_String anim)
        {
            string state = String.Empty;

            foreach (KeyValuePair<string, string> kvp in MovementAnimationsForLSL)
            {
                if (kvp.Value.ToLower() == ((string)animState).ToLower())
                {
                    state = kvp.Key;
                    break;
                }
            }

            if (state == String.Empty)
            {
                llShout(ScriptBaseClass.DEBUG_CHANNEL, "Invalid animation state " + animState);
                return;
            }

            if (m_item.PermsGranter == UUID.Zero)
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

            UUID animID;

            animID = ScriptUtils.GetAssetIdFromItemName(m_host, anim, (int)AssetType.Animation);

            if (animID == UUID.Zero)
            {
                String animupper = ((string)anim).ToUpperInvariant();
                DefaultAvatarAnimations.AnimsUUID.TryGetValue(animupper, out animID);
            }

            if (animID == UUID.Zero)
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

            if (m_item.PermsGranter == UUID.Zero)
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

            if (state == String.Empty)
            {
                return;
            }

            presence.SetAnimationOverride(state, UUID.Zero);
        }

        public LSL_String llGetAnimationOverride(LSL_String animState)
        {
            ScenePresence presence = World.GetScenePresence(m_item.PermsGranter);
            if (presence == null)
                return String.Empty;

            if (m_item.PermsGranter == UUID.Zero)
            {
                llShout(ScriptBaseClass.DEBUG_CHANNEL, "No permission to override animations");
                return String.Empty;
            }

            if ((m_item.PermsMask & (ScriptBaseClass.PERMISSION_OVERRIDE_ANIMATIONS | ScriptBaseClass.PERMISSION_TRIGGER_ANIMATION)) == 0)
            {
                llShout(ScriptBaseClass.DEBUG_CHANNEL, "No permission to override animations");
                return String.Empty;
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

            if (state == String.Empty)
            {
                return String.Empty;
            }

            UUID animID = presence.GetAnimationOverride(state);
            if (animID == UUID.Zero)
                return animState;

            foreach (KeyValuePair<string, UUID> kvp in DefaultAvatarAnimations.AnimsUUID)
            {
                if (kvp.Value == animID)
                    return kvp.Key.ToLower();
            }

            foreach (TaskInventoryItem item in m_host.Inventory.GetInventoryItems())
            {
                if (item.AssetID == animID)
                    return item.Name;
            }

            return String.Empty;
        }

        public LSL_String llJsonGetValue(LSL_String json, LSL_List specifiers)
        {
            OSD o = OSDParser.DeserializeJson(json);
            OSD specVal = JsonGetSpecific(o, specifiers, 0);

            return specVal.AsString();
        }

        public LSL_List llJson2List(LSL_String json)
        {
            try
            {
                OSD o = OSDParser.DeserializeJson(json);
                return (LSL_List)ParseJsonNode(o);
            }
            catch (Exception)
            {
                return new LSL_List(ScriptBaseClass.JSON_INVALID);
            }
        }

        private object ParseJsonNode(OSD node)
        {
            if (node.Type == OSDType.Integer)
                return new LSL_Integer(node.AsInteger());
            if (node.Type == OSDType.Boolean)
                return new LSL_Integer(node.AsBoolean() ? 1 : 0);
            if (node.Type == OSDType.Real)
                return new LSL_Float(node.AsReal());
            if (node.Type == OSDType.UUID || node.Type == OSDType.String)
                return new LSL_String(node.AsString());
            if (node.Type == OSDType.Array)
            {
                LSL_List resp = new LSL_List();
                OSDArray ar = node as OSDArray;
                foreach (OSD o in ar)
                    resp.Add(ParseJsonNode(o));
                return resp;
            }
            if (node.Type == OSDType.Map)
            {
                LSL_List resp = new LSL_List();
                OSDMap ar = node as OSDMap;
                foreach (KeyValuePair<string, OSD> o in ar)
                {
                    resp.Add(new LSL_String(o.Key));
                    resp.Add(ParseJsonNode(o.Value));
                }
                return resp;
            }
            throw new Exception(ScriptBaseClass.JSON_INVALID);
        }

        public LSL_String llList2Json(LSL_String type, LSL_List values)
        {
            try
            {
                if (type == ScriptBaseClass.JSON_ARRAY)
                {
                    OSDArray array = new OSDArray();
                    foreach (object o in values.Data)
                    {
                        array.Add(ListToJson(o));
                    }
                    return OSDParser.SerializeJsonString(array);
                }
                else if (type == ScriptBaseClass.JSON_OBJECT)
                {
                    OSDMap map = new OSDMap();
                    for (int i = 0; i < values.Data.Length; i += 2)
                    {
                        if (!(values.Data[i] is LSL_String))
                            return ScriptBaseClass.JSON_INVALID;
                        map.Add(((LSL_String)values.Data[i]).m_string, ListToJson(values.Data[i + 1]));
                    }
                    return OSDParser.SerializeJsonString(map);
                }
                return ScriptBaseClass.JSON_INVALID;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private OSD ListToJson(object o)
        {
            if (o is LSL_Float)
                return OSD.FromReal(((LSL_Float)o).value);
            if (o is LSL_Integer)
            {
                int i = ((LSL_Integer)o).value;
                if (i == 0)
                    return OSD.FromBoolean(false);
                else if (i == 1)
                    return OSD.FromBoolean(true);
                return OSD.FromInteger(i);
            }
            if (o is LSL_Rotation)
                return OSD.FromString(((LSL_Rotation)o).ToString());
            if (o is LSL_Vector)
                return OSD.FromString(((LSL_Vector)o).ToString());
            if (o is LSL_String)
            {
                string str = ((LSL_String)o).m_string;
                if (str == ScriptBaseClass.JSON_NULL)
                    return new OSD();
                return OSD.FromString(str);
            }
            throw new Exception(ScriptBaseClass.JSON_INVALID);
        }

        private OSD JsonGetSpecific(OSD o, LSL_List specifiers, int i)
        {
            object spec = specifiers.Data[i];
            OSD nextVal = null;
            if (o is OSDArray)
            {
                if (spec is LSL_Integer)
                    nextVal = ((OSDArray)o)[((LSL_Integer)spec).value];
            }
            if (o is OSDMap)
            {
                if (spec is LSL_String)
                    nextVal = ((OSDMap)o)[((LSL_String)spec).m_string];
            }
            if (nextVal != null)
            {
                if (specifiers.Data.Length - 1 > i)
                    return JsonGetSpecific(nextVal, specifiers, i + 1);
            }
            return nextVal;
        }

        public LSL_String llJsonSetValue(LSL_String json, LSL_List specifiers, LSL_String value)
        {
            try
            {
                OSD o = OSDParser.DeserializeJson(json);
                JsonSetSpecific(o, specifiers, 0, value);
                return OSDParser.SerializeJsonString(o);
            }
            catch (Exception)
            {
            }
            return ScriptBaseClass.JSON_INVALID;
        }

        private void JsonSetSpecific(OSD o, LSL_List specifiers, int i, LSL_String val)
        {
            object spec = specifiers.Data[i];
            // 20131224 not used            object specNext = i+1 == specifiers.Data.Length ? null : specifiers.Data[i+1];
            OSD nextVal = null;
            if (o is OSDArray)
            {
                OSDArray array = ((OSDArray)o);
                if (spec is LSL_Integer)
                {
                    int v = ((LSL_Integer)spec).value;
                    if (v >= array.Count)
                        array.Add(JsonBuildRestOfSpec(specifiers, i + 1, val));
                    else
                        nextVal = ((OSDArray)o)[v];
                }
                else if (spec is LSL_String && ((LSL_String)spec) == ScriptBaseClass.JSON_APPEND)
                    array.Add(JsonBuildRestOfSpec(specifiers, i + 1, val));
            }
            if (o is OSDMap)
            {
                if (spec is LSL_String)
                {
                    OSDMap map = ((OSDMap)o);
                    if (map.ContainsKey(((LSL_String)spec).m_string))
                        nextVal = map[((LSL_String)spec).m_string];
                    else
                        map.Add(((LSL_String)spec).m_string, JsonBuildRestOfSpec(specifiers, i + 1, val));
                }
            }
            if (nextVal != null)
            {
                if (specifiers.Data.Length - 1 > i)
                {
                    JsonSetSpecific(nextVal, specifiers, i + 1, val);
                    return;
                }
            }
        }

        private OSD JsonBuildRestOfSpec(LSL_List specifiers, int i, LSL_String val)
        {
            object spec = i >= specifiers.Data.Length ? null : specifiers.Data[i];
            // 20131224 not used            object specNext = i+1 >= specifiers.Data.Length ? null : specifiers.Data[i+1];

            if (spec == null)
                return OSD.FromString(val);

            if (spec is LSL_Integer ||
                (spec is LSL_String && ((LSL_String)spec) == ScriptBaseClass.JSON_APPEND))
            {
                OSDArray array = new OSDArray();
                array.Add(JsonBuildRestOfSpec(specifiers, i + 1, val));
                return array;
            }
            else if (spec is LSL_String)
            {
                OSDMap map = new OSDMap();
                map.Add((LSL_String)spec, JsonBuildRestOfSpec(specifiers, i + 1, val));
                return map;
            }
            return new OSD();
        }

        public LSL_String llJsonValueType(LSL_String json, LSL_List specifiers)
        {
            OSD o = OSDParser.DeserializeJson(json);
            OSD specVal = JsonGetSpecific(o, specifiers, 0);
            if (specVal == null)
                return ScriptBaseClass.JSON_INVALID;
            switch (specVal.Type)
            {
                case OSDType.Array:
                    return ScriptBaseClass.JSON_ARRAY;
                case OSDType.Boolean:
                    return specVal.AsBoolean() ? ScriptBaseClass.JSON_TRUE : ScriptBaseClass.JSON_FALSE;
                case OSDType.Integer:
                case OSDType.Real:
                    return ScriptBaseClass.JSON_NUMBER;
                case OSDType.Map:
                    return ScriptBaseClass.JSON_OBJECT;
                case OSDType.String:
                case OSDType.UUID:
                    return ScriptBaseClass.JSON_STRING;
                case OSDType.Unknown:
                    return ScriptBaseClass.JSON_NULL;
            }
            return ScriptBaseClass.JSON_INVALID;
        }
    }

    public class NotecardCache
    {
        protected class Notecard
        {
            public string[] text;
            public DateTime lastRef;
        }

        private static Dictionary<UUID, Notecard> m_Notecards =
            new Dictionary<UUID, Notecard>();

        public static void Cache(UUID assetID, byte[] text)
        {
            CheckCache();

            lock (m_Notecards)
            {
                if (m_Notecards.ContainsKey(assetID))
                    return;

                Notecard nc = new Notecard();
                nc.lastRef = DateTime.Now;
                try
                {
                    nc.text = SLUtil.ParseNotecardToArray(text);
                }
                catch(SLUtil.NotANotecardFormatException)
                {
                    nc.text = new string[0];
                }
                m_Notecards[assetID] = nc;
            }
        }

        public static bool IsCached(UUID assetID)
        {
            lock (m_Notecards)
            {
                return m_Notecards.ContainsKey(assetID);
            }
        }

        public static int GetLines(UUID assetID)
        {
            if (!IsCached(assetID))
                return -1;

            lock (m_Notecards)
            {
                m_Notecards[assetID].lastRef = DateTime.Now;
                return m_Notecards[assetID].text.Length;
            }
        }

        /// <summary>
        /// Get a notecard line.
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="lineNumber">Lines start at index 0</param>
        /// <returns></returns>
        public static string GetLine(UUID assetID, int lineNumber)
        {
            if (lineNumber < 0)
                return "";

            string data;

            if (!IsCached(assetID))
                return "";

            lock (m_Notecards)
            {
                m_Notecards[assetID].lastRef = DateTime.Now;

                if (lineNumber >= m_Notecards[assetID].text.Length)
                    return "\n\n\n";

                data = m_Notecards[assetID].text[lineNumber];

                return data;
            }
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
                line = line.Substring(0, maxLength);

            return line;
        }

        public static void CheckCache()
        {
            lock (m_Notecards)
            {
                foreach (UUID key in new List<UUID>(m_Notecards.Keys))
                {
                    Notecard nc = m_Notecards[key];
                    if (nc.lastRef.AddSeconds(30) < DateTime.Now)
                        m_Notecards.Remove(key);
                }
            }
        }
    }
}
