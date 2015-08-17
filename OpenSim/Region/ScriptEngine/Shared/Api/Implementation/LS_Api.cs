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
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Lifetime;
using System.Threading;
using OpenMetaverse;
using Nini.Config;
using OpenSim;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.World.LightShare;
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
    public class LS_Api : MarshalByRefObject, ILS_Api, IScriptApi
    {
        internal IScriptEngine m_ScriptEngine;
        internal SceneObjectPart m_host;
        internal bool m_LSFunctionsEnabled = false;
        internal IScriptModuleComms m_comms = null;

        public void Initialize(
            IScriptEngine scriptEngine, SceneObjectPart host, TaskInventoryItem item)
        {
            m_ScriptEngine = scriptEngine;
            m_host = host;

            if (m_ScriptEngine.Config.GetBoolean("AllowLightShareFunctions", false))
                m_LSFunctionsEnabled = true;

            m_comms = m_ScriptEngine.World.RequestModuleInterface<IScriptModuleComms>();
            if (m_comms == null)
                m_LSFunctionsEnabled = false;
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

        /// <summary>
        /// Dumps an error message on the debug console.
        /// </summary>
        internal void LSShoutError(string message)
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
        public LSL_List lsGetWindlightScene(LSL_List rules)
        {
            if (!m_LSFunctionsEnabled)
            {
                LSShoutError("LightShare functions are not enabled.");
                return new LSL_List();
            }
            m_host.AddScriptLPS(1);
            RegionLightShareData wl = m_host.ParentGroup.Scene.RegionInfo.WindlightSettings;

            LSL_List values = new LSL_List();
            int idx = 0;
            while (idx < rules.Length)
            {
                LSL_Integer ruleInt = rules.GetLSLIntegerItem(idx);
                uint rule = (uint)ruleInt;
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
                    case (int)ScriptBaseClass.WL_SUN_MOON_POSITION:
                         toadd.Add(new LSL_Float(wl.sunMoonPosition));
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
                    values.Add(ruleInt);
                    values.Add(toadd.Data[0]);
                }
                idx++;
            }

            return values;
        }

        private RegionLightShareData getWindlightProfileFromRules(LSL_List rules)
        {
            RegionLightShareData wl = (RegionLightShareData)m_host.ParentGroup.Scene.RegionInfo.WindlightSettings.Clone();

//            LSL_List values = new LSL_List();
            int idx = 0;
            while (idx < rules.Length)
            {
                uint rule;

                try
                {
                    rule = (uint)rules.GetLSLIntegerItem(idx);
                }
                catch (InvalidCastException)
                {
                    throw new InvalidCastException(string.Format("Error running rule type: arg #{0} - parameter type must be integer", idx));
                }

                LSL_Types.Quaternion iQ;
                LSL_Types.Vector3 iV;
                switch (rule)
                {
                    case (int)ScriptBaseClass.WL_SUN_MOON_POSITION:
                        idx++;
                        try
                        {
                            wl.sunMoonPosition = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_SUN_MOON_POSITION: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_AMBIENT:
                        idx++;
                        try
                        {
                            iQ = rules.GetQuaternionItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_AMBIENT: arg #{0} - parameter 1 must be rotation", idx));
                        }
                        wl.ambient = new Vector4((float)iQ.x, (float)iQ.y, (float)iQ.z, (float)iQ.s);
                        break;
                    case (int)ScriptBaseClass.WL_BIG_WAVE_DIRECTION:
                        idx++;
                        try
                        {
                            iV = rules.GetVector3Item(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_BIG_WAVE_DIRECTION: arg #{0} - parameter 1 must be vector", idx));
                        }
                        wl.bigWaveDirection = new Vector2((float)iV.x, (float)iV.y);
                        break;
                    case (int)ScriptBaseClass.WL_BLUE_DENSITY:
                        idx++;
                        try
                        {
                            iQ = rules.GetQuaternionItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_BLUE_DENSITY: arg #{0} - parameter 1 must be rotation", idx));
                        }
                        wl.blueDensity = new Vector4((float)iQ.x, (float)iQ.y, (float)iQ.z, (float)iQ.s);
                        break;
                    case (int)ScriptBaseClass.WL_BLUR_MULTIPLIER:
                        idx++;
                        try
                        {
                            wl.blurMultiplier = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_BLUR_MULTIPLIER: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_COLOR:
                        idx++;
                        try
                        {
                            iQ = rules.GetQuaternionItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_CLOUD_COLOR: arg #{0} - parameter 1 must be rotation", idx));
                        }
                        wl.cloudColor = new Vector4((float)iQ.x, (float)iQ.y, (float)iQ.z, (float)iQ.s);
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_COVERAGE:
                        idx++;
                        try
                        {
                            wl.cloudCoverage = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_CLOUD_COVERAGE: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_DETAIL_XY_DENSITY:
                        idx++;
                        try
                        {
                            iV = rules.GetVector3Item(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_CLOUD_DETAIL_XY_DENSITY: arg #{0} - parameter 1 must be vector", idx));
                        }
                        wl.cloudDetailXYDensity = iV;
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_SCALE:
                        idx++;
                        try
                        {
                            wl.cloudScale = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_CLOUD_SCALE: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_SCROLL_X:
                        idx++;
                        try
                        {
                            wl.cloudScrollX = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_CLOUD_SCROLL_X: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_SCROLL_X_LOCK:
                        idx++;
                        try
                        {
                            wl.cloudScrollXLock = rules.GetLSLIntegerItem(idx).value == 1 ? true : false;
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_CLOUD_SCROLL_Y_LOCK: arg #{0} - parameter 1 must be integer", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_SCROLL_Y:
                        idx++;
                        try
                        {
                            wl.cloudScrollY = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_CLOUD_SCROLL_Y: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_SCROLL_Y_LOCK:
                        idx++;
                        try
                        {
                            wl.cloudScrollYLock = rules.GetLSLIntegerItem(idx).value == 1 ? true : false;
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_CLOUD_SCROLL_Y_LOCK: arg #{0} - parameter 1 must be integer", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_CLOUD_XY_DENSITY:
                        idx++;
                        try
                        {
                            iV = rules.GetVector3Item(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_CLOUD_XY_DENSITY: arg #{0} - parameter 1 must be vector", idx));
                        }
                        wl.cloudXYDensity = iV;
                        break;
                    case (int)ScriptBaseClass.WL_DENSITY_MULTIPLIER:
                        idx++;
                        try
                        {
                            wl.densityMultiplier = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_DENSITY_MULTIPLIER: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_DISTANCE_MULTIPLIER:
                        idx++;
                        try
                        {
                            wl.distanceMultiplier = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_DISTANCE_MULTIPLIER: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_DRAW_CLASSIC_CLOUDS:
                        idx++;
                        try
                        {
                            wl.drawClassicClouds = rules.GetLSLIntegerItem(idx).value == 1 ? true : false;
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_DRAW_CLASSIC_CLOUDS: arg #{0} - parameter 1 must be integer", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_EAST_ANGLE:
                        idx++;
                        try
                        {
                            wl.eastAngle = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_EAST_ANGLE: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_FRESNEL_OFFSET:
                        idx++;
                        try
                        {
                            wl.fresnelOffset = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_FRESNEL_OFFSET: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_FRESNEL_SCALE:
                        idx++;
                        try
                        {
                            wl.fresnelScale = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_FRESNEL_SCALE: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_HAZE_DENSITY:
                        idx++;
                        try
                        {
                            wl.hazeDensity = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_HAZE_DENSITY: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_HAZE_HORIZON:
                        idx++;
                        try
                        {
                            wl.hazeHorizon = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_HAZE_HORIZON: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_HORIZON:
                        idx++;
                        try
                        {
                            iQ = rules.GetQuaternionItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_HORIZON: arg #{0} - parameter 1 must be rotation", idx));
                        }
                        wl.horizon = new Vector4((float)iQ.x, (float)iQ.y, (float)iQ.z, (float)iQ.s);
                        break;
                    case (int)ScriptBaseClass.WL_LITTLE_WAVE_DIRECTION:
                        idx++;
                        try
                        {
                            iV = rules.GetVector3Item(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_LITTLE_WAVE_DIRECTION: arg #{0} - parameter 1 must be vector", idx));
                        }
                        wl.littleWaveDirection = new Vector2((float)iV.x, (float)iV.y);
                        break;
                    case (int)ScriptBaseClass.WL_MAX_ALTITUDE:
                        idx++;
                        try
                        {
                            wl.maxAltitude = (ushort)rules.GetLSLIntegerItem(idx).value;
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_MAX_ALTITUDE: arg #{0} - parameter 1 must be integer", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_NORMAL_MAP_TEXTURE:
                        idx++;
                        try
                        {
                            wl.normalMapTexture = new UUID(rules.GetLSLStringItem(idx).m_string);
                        }
                        catch (ArgumentException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_NORMAL_MAP_TEXTURE: arg #{0} - parameter 1 must be key", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_REFLECTION_WAVELET_SCALE:
                        idx++;
                        try
                        {
                            iV = rules.GetVector3Item(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_REFLECTION_WAVELET_SCALE: arg #{0} - parameter 1 must be vector", idx));
                        }
                        wl.reflectionWaveletScale = iV;
                        break;
                    case (int)ScriptBaseClass.WL_REFRACT_SCALE_ABOVE:
                        idx++;
                        try
                        {
                            wl.refractScaleAbove = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_REFRACT_SCALE_ABOVE: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_REFRACT_SCALE_BELOW:
                        idx++;
                        try
                        {
                            wl.refractScaleBelow = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_REFRACT_SCALE_BELOW: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_SCENE_GAMMA:
                        idx++;
                        try
                        {
                            wl.sceneGamma = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_SCENE_GAMMA: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_STAR_BRIGHTNESS:
                        idx++;
                        try
                        {
                            wl.starBrightness = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_STAR_BRIGHTNESS: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_SUN_GLOW_FOCUS:
                        idx++;
                        try
                        {
                            wl.sunGlowFocus = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_SUN_GLOW_FOCUS: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_SUN_GLOW_SIZE:
                        idx++;
                        try
                        {
                            wl.sunGlowSize = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_SUN_GLOW_SIZE: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_SUN_MOON_COLOR:
                        idx++;
                        iQ = rules.GetQuaternionItem(idx);
                        try
                        {
                            wl.sunMoonColor = new Vector4((float)iQ.x, (float)iQ.y, (float)iQ.z, (float)iQ.s);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_SUN_MOON_COLOR: arg #{0} - parameter 1 must be rotation", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_UNDERWATER_FOG_MODIFIER:
                        idx++;
                        try
                        {
                            wl.underwaterFogModifier = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_UNDERWATER_FOG_MODIFIER: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                    case (int)ScriptBaseClass.WL_WATER_COLOR:
                        idx++;
                        try
                        {
                            iV = rules.GetVector3Item(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_WATER_COLOR: arg #{0} - parameter 1 must be vector", idx));
                        }
                        wl.waterColor = iV;
                        break;
                    case (int)ScriptBaseClass.WL_WATER_FOG_DENSITY_EXPONENT:
                        idx++;
                        try
                        {
                            wl.waterFogDensityExponent = (float)rules.GetLSLFloatItem(idx);
                        }
                        catch (InvalidCastException)
                        {
                            throw new InvalidCastException(string.Format("Error running rule WL_WATER_FOG_DENSITY_EXPONENT: arg #{0} - parameter 1 must be float", idx));
                        }
                        break;
                }
                idx++;
            }
            return wl;
        }

        /// <summary>
        /// Set the current Windlight scene
        /// </summary>
        /// <param name="rules"></param>
        /// <returns>success: true or false</returns>
        public int lsSetWindlightScene(LSL_List rules)
        {
            if (!m_LSFunctionsEnabled)
            {
                LSShoutError("LightShare functions are not enabled.");
                return 0;
            }

            if (!World.RegionInfo.EstateSettings.IsEstateManagerOrOwner(m_host.OwnerID))
            {
                ScenePresence sp = World.GetScenePresence(m_host.OwnerID);

                if (sp == null || sp.GodLevel < 200)
                {
                    LSShoutError("lsSetWindlightScene can only be used by estate managers or owners.");
                    return 0;
                }
            }

            int success = 0;
            m_host.AddScriptLPS(1);

            if (LightShareModule.EnableWindlight)
            {
                RegionLightShareData wl;
                try
                {
                    wl = getWindlightProfileFromRules(rules);
                }
                catch(InvalidCastException e)
                {
                    LSShoutError(e.Message);
                    return 0;
                }
                wl.valid = true;
                m_host.ParentGroup.Scene.StoreWindlightProfile(wl);
                success = 1;
            }
            else
            {
                LSShoutError("Windlight module is disabled");
                return 0;
            }

            return success;
        }

        public void lsClearWindlightScene()
        {
            if (!m_LSFunctionsEnabled)
            {
                LSShoutError("LightShare functions are not enabled.");
                return;
            }

            if (!World.RegionInfo.EstateSettings.IsEstateManagerOrOwner(m_host.OwnerID))
            {
                ScenePresence sp = World.GetScenePresence(m_host.OwnerID);

                if (sp == null || sp.GodLevel < 200)
                {
                    LSShoutError("lsSetWindlightScene can only be used by estate managers or owners.");
                    return;
                }
            }

            m_host.ParentGroup.Scene.RegionInfo.WindlightSettings.valid = false;
            if (m_host.ParentGroup.Scene.SimulationDataService != null)
                m_host.ParentGroup.Scene.SimulationDataService.RemoveRegionWindlightSettings(m_host.ParentGroup.Scene.RegionInfo.RegionID);

            m_host.ParentGroup.Scene.EventManager.TriggerOnSaveNewWindlightProfile();
        }

        /// <summary>
        /// Set the current Windlight scene to a target avatar
        /// </summary>
        /// <param name="rules"></param>
        /// <returns>success: true or false</returns>
        public int lsSetWindlightSceneTargeted(LSL_List rules, LSL_Key target)
        {
            if (!m_LSFunctionsEnabled)
            {
                LSShoutError("LightShare functions are not enabled.");
                return 0;
            }

            if (!World.RegionInfo.EstateSettings.IsEstateManagerOrOwner(m_host.OwnerID))
            {
                ScenePresence sp = World.GetScenePresence(m_host.OwnerID);

                if (sp == null || sp.GodLevel < 200)
                {
                    LSShoutError("lsSetWindlightSceneTargeted can only be used by estate managers or owners.");
                    return 0;
                }
            }

            int success = 0;
            m_host.AddScriptLPS(1);

            if (LightShareModule.EnableWindlight)
            {
                RegionLightShareData wl;
                try
                {
                    wl = getWindlightProfileFromRules(rules);
                }
                catch(InvalidCastException e)
                {
                    LSShoutError(e.Message);
                    return 0;
                }
                World.EventManager.TriggerOnSendNewWindlightProfileTargeted(wl, new UUID(target.m_string));
                success = 1;
            }
            else
            {
                LSShoutError("Windlight module is disabled");
                return 0;
            }

            return success;
        }        
    }
}
