using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Lifetime;
using OpenMetaverse;
using Nini.Config;
using OpenSim;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.World.Meta7Windlight;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api.Plugins;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

namespace OpenSim.Region.ScriptEngine.Shared.Api
{
    [Serializable]
    public class CM_Api : MarshalByRefObject, ICM_Api, IScriptApi
    {
        internal IScriptEngine m_ScriptEngine;
        internal SceneObjectPart m_host;
        internal uint m_localID;
        internal UUID m_itemID;
        internal bool m_CMFunctionsEnabled = false;
        internal IScriptModuleComms m_comms = null;

        public void Initialize(IScriptEngine ScriptEngine, SceneObjectPart host, uint localID, UUID itemID)
        {
            m_ScriptEngine = ScriptEngine;
            m_host = host;
            m_localID = localID;
            m_itemID = itemID;

            if (m_ScriptEngine.Config.GetBoolean("AllowCareminsterFunctions", false))
                m_CMFunctionsEnabled = true;

            m_comms = m_ScriptEngine.World.RequestModuleInterface<IScriptModuleComms>();
            if (m_comms == null)
                m_CMFunctionsEnabled = false;
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

        public Scene World
        {
            get { return m_ScriptEngine.World; }
        }

        //
        //Dumps an error message on the debug console.
        //

        internal void CMShoutError(string message)
        {
            if (message.Length > 1023)
                message = message.Substring(0, 1023);

            World.SimChat(Utils.StringToBytes(message),
                          ChatTypeEnum.Shout, ScriptBaseClass.DEBUG_CHANNEL, m_host.ParentGroup.RootPart.AbsolutePosition, m_host.Name, m_host.UUID, true);

            IWorldComm wComm = m_ScriptEngine.World.RequestModuleInterface<IWorldComm>();
            wComm.DeliverMessage(ChatTypeEnum.Shout, ScriptBaseClass.DEBUG_CHANNEL, m_host.Name, m_host.UUID, message);
        }

        /// <summary>
        /// Get the current Windlight scene
        /// </summary>
        /// <returns>List of windlight parameters</returns>
        public LSL_List cmGetWindlightScene(LSL_List rules)
        {
            if (!m_CMFunctionsEnabled)
            {
                CMShoutError("Careminster functions are not enabled.");
                return new LSL_List();
            }
            m_host.AddScriptLPS(1);
            RegionMeta7WindlightData wl = m_host.ParentGroup.Scene.RegionInfo.WindlightSettings;

            LSL_List values = new LSL_List();
            int idx = 0;
            while (idx < rules.Length)
            {
                uint rule = (uint)rules.GetLSLIntegerItem(idx);
                LSL_List toadd = new LSL_List();

                switch (rule)
                {
                    case (int)ScriptBaseClass.WL_AMBIENT:
                        toadd.Add(new LSL_Rotation(wl.ambient.X, wl.ambient.Y, wl.ambient.Z, wl.ambient.W));
                        break;
                    case (int)ScriptBaseClass.WL_BIG_WAVE_DIRECTION:
                        toadd.Add(new LSL_Vector(wl.bigWaveDirection.X, wl.bigWaveDirection.Y, 0.0f));
                        break;
                    case (int)ScriptBaseClass.WL_BLUE_DENSITY:
                        toadd.Add(new LSL_Rotation(wl.blueDensity.X, wl.blueDensity.Y, wl.blueDensity.Z, wl.blueDensity.W));
                        break;
                    case (int)ScriptBaseClass.WL_BLUR_MULTIPLIER:
                        toadd.Add(new LSL_Float(wl.blurMultiplier));
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_COLOR:
                        toadd.Add(new LSL_Rotation(wl.cloudColor.X, wl.cloudColor.Y, wl.cloudColor.Z, wl.cloudColor.W));
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_COVERAGE:
                        toadd.Add(new LSL_Float(wl.cloudCoverage));
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_DETAIL_XY_DENSITY:
                        toadd.Add(new LSL_Vector(wl.cloudDetailXYDensity.X, wl.cloudDetailXYDensity.Y, wl.cloudDetailXYDensity.Z));
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_SCALE:
                        toadd.Add(new LSL_Float(wl.cloudScale));
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_SCROLL_X:
                        toadd.Add(new LSL_Float(wl.cloudScrollX));
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_SCROLL_X_LOCK:
                        toadd.Add(new LSL_Integer(wl.cloudScrollXLock ? 1 : 0));
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_SCROLL_Y:
                        toadd.Add(new LSL_Float(wl.cloudScrollY));
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_SCROLL_Y_LOCK:
                        toadd.Add(new LSL_Integer(wl.cloudScrollYLock ? 1 : 0));
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_XY_DENSITY:
                        toadd.Add(new LSL_Vector(wl.cloudXYDensity.X, wl.cloudXYDensity.Y, wl.cloudXYDensity.Z));
                        break;
                    case (int)ScriptBaseClass.WL_DENSITY_MULTIPLIER:
                        toadd.Add(new LSL_Float(wl.densityMultiplier));
                        break;
                    case (int)ScriptBaseClass.WL_DISTANCE_MULTIPLIER:
                        toadd.Add(new LSL_Float(wl.distanceMultiplier));
                        break;
                    case (int)ScriptBaseClass.WL_DRAW_CLASSIC_CLOUDS:
                        toadd.Add(new LSL_Integer(wl.drawClassicClouds ? 1 : 0));
                        break;
                    case (int)ScriptBaseClass.WL_EAST_ANGLE:
                        toadd.Add(new LSL_Float(wl.eastAngle));
                        break;
                    case (int)ScriptBaseClass.WL_FRESNEL_OFFSET:
                        toadd.Add(new LSL_Float(wl.fresnelOffset));
                        break;
                    case (int)ScriptBaseClass.WL_FRESNEL_SCALE:
                        toadd.Add(new LSL_Float(wl.fresnelScale));
                        break;
                    case (int)ScriptBaseClass.WL_HAZE_DENSITY:
                        toadd.Add(new LSL_Float(wl.hazeDensity));
                        break;
                    case (int)ScriptBaseClass.WL_HAZE_HORIZON:
                        toadd.Add(new LSL_Float(wl.hazeHorizon));
                        break;
                    case (int)ScriptBaseClass.WL_HORIZON:
                        toadd.Add(new LSL_Rotation(wl.horizon.X, wl.horizon.Y, wl.horizon.Z, wl.horizon.W));
                        break;
                    case (int)ScriptBaseClass.WL_LITTLE_WAVE_DIRECTION:
                        toadd.Add(new LSL_Vector(wl.littleWaveDirection.X, wl.littleWaveDirection.Y, 0.0f));
                        break;
                    case (int)ScriptBaseClass.WL_MAX_ALTITUDE:
                        toadd.Add(new LSL_Integer(wl.maxAltitude));
                        break;
                    case (int)ScriptBaseClass.WL_NORMAL_MAP_TEXTURE:
                        toadd.Add(new LSL_Key(wl.normalMapTexture.ToString()));
                        break;
                    case (int)ScriptBaseClass.WL_REFLECTION_WAVELET_SCALE:
                        toadd.Add(new LSL_Vector(wl.reflectionWaveletScale.X, wl.reflectionWaveletScale.Y, wl.reflectionWaveletScale.Z));
                        break;
                    case (int)ScriptBaseClass.WL_REFRACT_SCALE_ABOVE:
                        toadd.Add(new LSL_Float(wl.refractScaleAbove));
                        break;
                    case (int)ScriptBaseClass.WL_REFRACT_SCALE_BELOW:
                        toadd.Add(new LSL_Float(wl.refractScaleBelow));
                        break;
                    case (int)ScriptBaseClass.WL_SCENE_GAMMA:
                        toadd.Add(new LSL_Float(wl.sceneGamma));
                        break;
                    case (int)ScriptBaseClass.WL_STAR_BRIGHTNESS:
                        toadd.Add(new LSL_Float(wl.starBrightness));
                        break;
                    case (int)ScriptBaseClass.WL_SUN_GLOW_FOCUS:
                        toadd.Add(new LSL_Float(wl.sunGlowFocus));
                        break;
                    case (int)ScriptBaseClass.WL_SUN_GLOW_SIZE:
                        toadd.Add(new LSL_Float(wl.sunGlowSize));
                        break;
                    case (int)ScriptBaseClass.WL_SUN_MOON_COLOR:
                        toadd.Add(new LSL_Rotation(wl.sunMoonColor.X, wl.sunMoonColor.Y, wl.sunMoonColor.Z, wl.sunMoonColor.W));
                        break;
                    case (int)ScriptBaseClass.WL_UNDERWATER_FOG_MODIFIER:
                        toadd.Add(new LSL_Float(wl.underwaterFogModifier));
                        break;
                    case (int)ScriptBaseClass.WL_WATER_COLOR:
                        toadd.Add(new LSL_Vector(wl.waterColor.X, wl.waterColor.Y, wl.waterColor.Z));
                        break;
                    case (int)ScriptBaseClass.WL_WATER_FOG_DENSITY_EXPONENT:
                        toadd.Add(new LSL_Float(wl.waterFogDensityExponent));
                        break;
                }

                if (toadd.Length > 0)
                {
                    values.Add(rule);
                    values.Add(toadd.Data[0]);
                }
                idx++;
            }


            return values;

        }

        /// <summary>
        /// Set the current Windlight scene
        /// </summary>
        /// <param name="rules"></param>
        /// <returns>success: true or false</returns>
        public int cmSetWindlightScene(LSL_List rules)
        {
            if (!m_CMFunctionsEnabled)
            {
                CMShoutError("Careminster functions are not enabled.");
                return 0;
            }
            int success = 0;
            m_host.AddScriptLPS(1);
            if (Meta7WindlightModule.EnableWindlight)
            {
                RegionMeta7WindlightData wl = m_host.ParentGroup.Scene.RegionInfo.WindlightSettings;

                LSL_List values = new LSL_List();
                int idx = 0;
                success = 1;
                while (idx < rules.Length)
                {
                    uint rule = (uint)rules.GetLSLIntegerItem(idx);
                    LSL_Types.Quaternion iQ;
                    LSL_Types.Vector3 iV;
                    switch (rule)
                    {
                        case (int)ScriptBaseClass.WL_AMBIENT:
                            idx++;
                            iQ = rules.GetQuaternionItem(idx);
                            wl.ambient = new Vector4((float)iQ.x, (float)iQ.y, (float)iQ.z, (float)iQ.s);
                            break;
                        case (int)ScriptBaseClass.WL_BIG_WAVE_DIRECTION:
                            idx++;
                            iV = rules.GetVector3Item(idx);
                            wl.bigWaveDirection = new Vector2((float)iV.x, (float)iV.y);
                            break;
                        case (int)ScriptBaseClass.WL_BLUE_DENSITY:
                            idx++;
                            iQ = rules.GetQuaternionItem(idx);
                            wl.blueDensity = new Vector4((float)iQ.x, (float)iQ.y, (float)iQ.z, (float)iQ.s);
                            break;
                        case (int)ScriptBaseClass.WL_BLUR_MULTIPLIER:
                            idx++;
                            wl.blurMultiplier = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_CLOUD_COLOR:
                            idx++;
                            iQ = rules.GetQuaternionItem(idx);
                            wl.cloudColor = new Vector4((float)iQ.x, (float)iQ.y, (float)iQ.z, (float)iQ.s);
                            break;
                        case (int)ScriptBaseClass.WL_CLOUD_COVERAGE:
                            idx++;
                            wl.cloudCoverage = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_CLOUD_DETAIL_XY_DENSITY:
                            idx++;
                            iV = rules.GetVector3Item(idx);
                            wl.cloudDetailXYDensity = new Vector3((float)iV.x, (float)iV.y, (float)iV.z);
                            break;
                        case (int)ScriptBaseClass.WL_CLOUD_SCALE:
                            idx++;
                            wl.cloudScale = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_CLOUD_SCROLL_X:
                            idx++;
                            wl.cloudScrollX = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_CLOUD_SCROLL_X_LOCK:
                            idx++;
                            wl.cloudScrollXLock = rules.GetLSLIntegerItem(idx).value == 1 ? true : false;
                            break;
                        case (int)ScriptBaseClass.WL_CLOUD_SCROLL_Y:
                            idx++;
                            wl.cloudScrollY = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_CLOUD_SCROLL_Y_LOCK:
                            idx++;
                            wl.cloudScrollYLock = rules.GetLSLIntegerItem(idx).value == 1 ? true : false;
                            break;
                        case (int)ScriptBaseClass.WL_CLOUD_XY_DENSITY:
                            idx++;
                            iV = rules.GetVector3Item(idx);
                            wl.cloudDetailXYDensity = new Vector3((float)iV.x, (float)iV.y, (float)iV.z);
                            break;
                        case (int)ScriptBaseClass.WL_DENSITY_MULTIPLIER:
                            idx++;
                            wl.densityMultiplier = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_DISTANCE_MULTIPLIER:
                            idx++;
                            wl.distanceMultiplier = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_DRAW_CLASSIC_CLOUDS:
                            idx++;
                            wl.drawClassicClouds = rules.GetLSLIntegerItem(idx).value == 1 ? true : false;
                            break;
                        case (int)ScriptBaseClass.WL_EAST_ANGLE:
                            idx++;
                            wl.eastAngle = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_FRESNEL_OFFSET:
                            idx++;
                            wl.fresnelOffset = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_FRESNEL_SCALE:
                            idx++;
                            wl.fresnelScale = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_HAZE_DENSITY:
                            idx++;
                            wl.hazeDensity = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_HAZE_HORIZON:
                            idx++;
                            wl.hazeHorizon = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_HORIZON:
                            idx++;
                            iQ = rules.GetQuaternionItem(idx);
                            wl.horizon = new Vector4((float)iQ.x, (float)iQ.y, (float)iQ.z, (float)iQ.s);
                            break;
                        case (int)ScriptBaseClass.WL_LITTLE_WAVE_DIRECTION:
                            idx++;
                            iV = rules.GetVector3Item(idx);
                            wl.littleWaveDirection = new Vector2((float)iV.x, (float)iV.y);
                            break;
                        case (int)ScriptBaseClass.WL_MAX_ALTITUDE:
                            idx++;
                            wl.maxAltitude = (ushort)rules.GetLSLIntegerItem(idx).value;
                            break;
                        case (int)ScriptBaseClass.WL_NORMAL_MAP_TEXTURE:
                            idx++;
                            wl.normalMapTexture = new UUID(rules.GetLSLStringItem(idx).m_string);
                            break;
                        case (int)ScriptBaseClass.WL_REFLECTION_WAVELET_SCALE:
                            idx++;
                            iV = rules.GetVector3Item(idx);
                            wl.reflectionWaveletScale = new Vector3((float)iV.x, (float)iV.y, (float)iV.z);
                            break;
                        case (int)ScriptBaseClass.WL_REFRACT_SCALE_ABOVE:
                            idx++;
                            wl.refractScaleAbove = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_REFRACT_SCALE_BELOW:
                            idx++;
                            wl.refractScaleBelow = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_SCENE_GAMMA:
                            idx++;
                            wl.sceneGamma = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_STAR_BRIGHTNESS:
                            idx++;
                            wl.starBrightness = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_SUN_GLOW_FOCUS:
                            idx++;
                            wl.sunGlowFocus = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_SUN_GLOW_SIZE:
                            idx++;
                            wl.sunGlowSize = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_SUN_MOON_COLOR:
                            idx++;
                            iQ = rules.GetQuaternionItem(idx);
                            wl.sunMoonColor = new Vector4((float)iQ.x, (float)iQ.y, (float)iQ.z, (float)iQ.s);
                            break;
                        case (int)ScriptBaseClass.WL_UNDERWATER_FOG_MODIFIER:
                            idx++;
                            wl.underwaterFogModifier = (float)rules.GetLSLFloatItem(idx);
                            break;
                        case (int)ScriptBaseClass.WL_WATER_COLOR:
                            idx++;
                            iV = rules.GetVector3Item(idx);
                            wl.waterColor = new Vector3((float)iV.x, (float)iV.y, (float)iV.z);
                            break;
                        case (int)ScriptBaseClass.WL_WATER_FOG_DENSITY_EXPONENT:
                            idx++;
                            wl.waterFogDensityExponent = (float)rules.GetLSLFloatItem(idx);
                            break;
                        default:
                            success = 0;
                            break;
                    }
                    idx++;
                }
                m_host.ParentGroup.Scene.StoreWindlightProfile(wl);

            }
            return success;
        }
        
    }
}
