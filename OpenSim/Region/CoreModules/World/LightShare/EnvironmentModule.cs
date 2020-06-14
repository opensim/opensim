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
using System.Net;
using System.Reflection;
using System.Text;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using log4net;
using Nini.Config;
using Mono.Addins;

using Caps = OpenSim.Framework.Capabilities.Caps;
using OSDArray = OpenMetaverse.StructuredData.OSDArray;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;

using MethodImplOptions = System.Runtime.CompilerServices.MethodImplOptions;

namespace OpenSim.Region.CoreModules.World.LightShare
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "EnvironmentModule")]

    public class EnvironmentModule : INonSharedRegionModule, IEnvironmentModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene = null;
        private UUID regionID = UUID.Zero;
        private bool Enabled = false;
        private IEstateModule m_estateModule;
        private IEventQueue m_eventQueue;
        private IAssetService m_assetService;

        private static ViewerEnviroment m_DefaultEnv = null;
        private static readonly string m_defaultDayAssetID = "5646d39e-d3d7-6aff-ed71-30fc87d64a91";

        private int m_regionEnvVersion = -1;

        private double m_framets;

        #region INonSharedRegionModule
        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["ClientStack.LindenCaps"];

            if (null == config)
                return;

            if (config.GetString("Cap_EnvironmentSettings", String.Empty) != "localhost")
            {
                m_log.InfoFormat("[{0}]: Module is disabled.", Name);
                return;
            }

            Enabled = true;


            m_log.InfoFormat("[{0}]: Module is enabled.", Name);
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "EnvironmentModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void AddRegion(Scene scene)
        {
            if (!Enabled)
                return;

            scene.RegisterModuleInterface<IEnvironmentModule>(this);
            m_scene = scene;
            regionID = scene.RegionInfo.RegionID;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!Enabled)
                return;

            m_estateModule = scene.RequestModuleInterface<IEstateModule>();
            if (m_estateModule == null)
            {
                Enabled = false;
                return;
            }

            m_eventQueue = m_scene.RequestModuleInterface<IEventQueue>();
            if (m_eventQueue == null)
            {
                Enabled = false;
                return;
            }

            m_assetService = m_scene.AssetService;
            if (m_assetService == null)
            {
                Enabled = false;
                return;
            }

            if (m_DefaultEnv == null)
            {
                AssetBase defEnv = m_assetService.Get(m_defaultDayAssetID);
                if(defEnv != null)
                {
                    byte[] envData = defEnv.Data;
                    try
                    {
                        OSD oenv = OSDParser.Deserialize(envData);
                        m_DefaultEnv = new ViewerEnviroment();
                        m_DefaultEnv.CycleFromOSD(oenv);
                    }
                    catch ( Exception e)
                    {
                        m_DefaultEnv = null;
                        m_log.WarnFormat("[Enviroment {0}]: failed to decode default enviroment asset: {1}", m_scene.Name, e.Message);
                    }
                }
            }
            if (m_DefaultEnv == null)
                m_DefaultEnv = new ViewerEnviroment();

            string senv = scene.SimulationDataService.LoadRegionEnvironmentSettings(scene.RegionInfo.RegionID);
            if(!string.IsNullOrEmpty(senv))
            {
                try
                {
                    OSD oenv = OSDParser.Deserialize(senv);
                    ViewerEnviroment VEnv = new ViewerEnviroment();
                    if(oenv is OSDArray)
                        VEnv.FromWLOSD(oenv);
                    else
                        VEnv.FromOSD(oenv);
                    scene.RegionEnviroment = VEnv;
                    m_regionEnvVersion = VEnv.version;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[Enviroment {0}] failed to load initial enviroment {1}", m_scene.Name, e.Message);
                    scene.RegionEnviroment = null;
                    m_regionEnvVersion = -1;
                }
            }
            else
            {
                scene.RegionEnviroment = null;
                m_regionEnvVersion = -1;
            }

            m_framets = 0;
            UpdateEnvTime();
            scene.EventManager.OnRegisterCaps += OnRegisterCaps;
            scene.EventManager.OnFrame += UpdateEnvTime;
        }

        public void RemoveRegion(Scene scene)
        {
            if (!Enabled)
                return;

            scene.EventManager.OnRegisterCaps -= OnRegisterCaps;
            m_scene = null;
        }
        #endregion

        #region IEnvironmentModule
        private void StoreOnRegion(ViewerEnviroment VEnv)
        {
            try
            {
                if (VEnv == null)
                {
                    m_scene.SimulationDataService.RemoveRegionEnvironmentSettings(regionID);
                    m_scene.RegionEnviroment = null;
                    m_regionEnvVersion = -1;
                }
                else
                {
                    m_regionEnvVersion++;
                    VEnv.version = m_regionEnvVersion;
                    OSD env = VEnv.ToOSD();
                    //m_scene.SimulationDataService.StoreRegionEnvironmentSettings(regionID, OSDParser.SerializeLLSDXmlString(env));
                    m_scene.SimulationDataService.StoreRegionEnvironmentSettings(regionID, OSDParser.SerializeLLSDNotationFull(env));
                    m_scene.RegionEnviroment = VEnv;
                }
                m_framets = 0;
                UpdateEnvTime();
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[Enviroment {0}] failed to store enviroment {1}", m_scene.Name, e.Message);
            }
        }

        public void ResetEnvironmentSettings(UUID regionUUID)
        {
            if (!Enabled)
                return;

            StoreOnRegion(null);
            WindlightRefresh(0);
        }

        public void WindlightRefresh(int interpolate)
        {
            List<byte[]> ls = null;
            m_scene.ForEachClient(delegate (IClientAPI client)
            {
                if(!client.IsActive)
                    return;

                uint vflags = client.GetViewerCaps();

                if ((vflags & 0x8000) != 0)
                    m_estateModule.HandleRegionInfoRequest(client);

                else if ((vflags & 0x4000) != 0)
                    m_eventQueue.WindlightRefreshEvent(interpolate, client.AgentId);

                else
                {
                    if(ls == null)
                        ls = MakeLightShareData();
                    SendLightShare(client, ls);
                }
            });
        }

        public void FromLightShare(RegionLightShareData ls)
        {
            if (!Enabled)
                return;

            ViewerEnviroment VEnv = new ViewerEnviroment();
            VEnv.FromLightShare(ls);

            StoreOnRegion(VEnv);
            WindlightRefresh(0);
        }

        public RegionLightShareData ToLightShare()
        {
            if (!Enabled)
                return new RegionLightShareData();

            RegionLightShareData ls = null;
            try
            {
                ViewerEnviroment VEnv = m_scene.RegionEnviroment;
                if(VEnv == null)
                    return new RegionLightShareData();
                ls = VEnv.ToLightShare();
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[{0}]: Unable to convert environment to lightShare, Exception: {1} - {2}",
                    Name, e.Message, e.StackTrace);
            }
            if(ls == null)
                return new RegionLightShareData();
            return ls;
        }
        #endregion

        #region Events
        private void OnRegisterCaps(UUID agentID, Caps caps)
        {
            // m_log.DebugFormat("[{0}]: Register capability for agentID {1} in region {2}",
            //       Name, agentID, caps.RegionName);

            caps.RegisterSimpleHandler("EnvironmentSettings",
                new SimpleStreamHandler("/" + UUID.Random(), delegate (IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
                {
                    processEnv(httpRequest, httpResponse, agentID);
                }));

            //Extended
            caps.RegisterSimpleHandler("ExtEnvironment",
                new SimpleStreamHandler("/" + UUID.Random(), delegate (IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
                {
                    processExtEnv(httpRequest, httpResponse, agentID, caps);
                }));
        }
        #endregion

        private void processEnv(IOSHttpRequest request, IOSHttpResponse response, UUID agentID)
        {
            switch (request.HttpMethod)
            {
                case "POST":
                    SetEnvironmentSettings(request, response, agentID);
                    return;
                case "GET":
                    GetEnvironmentSettings(response, agentID);
                    return;
                default:
                {
                    response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    return;
                }
            }
        }


        private void processExtEnv(IOSHttpRequest request, IOSHttpResponse response, UUID agentID, Caps caps)
        {
            switch(request.HttpMethod)
            {
                case "PUT":
                case "POST":
                    SetExtEnvironmentSettings(request, response, agentID, caps);
                    return;
                case "GET":
                    GetExtEnvironmentSettings(request, response, agentID);
                    return;
                case "DELETE":
                    DeleteExtEnvironmentSettings(request, response, agentID);
                    return;
                default:
                {
                    response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    return;
                }
            }
        }

        private void DeleteExtEnvironmentSettings(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, UUID agentID)
        {
            int parcel = -1;

            if (httpRequest.Query.Count > 0)
            {
                if (httpRequest.Query.ContainsKey("parcelid"))
                {
                    Int32.TryParse((string)httpRequest.Query["parcelid"], out parcel);
                }
            }

            if(parcel == -1)
                StoreOnRegion(null);

            WindlightRefresh(0);

            StringBuilder sb = LLSDxmlEncode.Start();
            LLSDxmlEncode.AddMap(sb);
            LLSDxmlEncode.AddElem("messageID", UUID.Zero, sb);
            LLSDxmlEncode.AddElem("regionID", regionID, sb);
            LLSDxmlEncode.AddElem("success", true, sb);
            LLSDxmlEncode.AddEndMap(sb);
            httpResponse.RawBuffer = Util.UTF8.GetBytes(LLSDxmlEncode.End(sb));
            httpResponse.StatusCode = (int)HttpStatusCode.OK;
        }

        private void GetExtEnvironmentSettings(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, UUID agentID)
        {
            if (httpRequest.Query.Count > 0)
            {
                int parcel = -1;
                if (httpRequest.Query.ContainsKey("parcelid"))
                {
                    Int32.TryParse((string)httpRequest.Query["parcelid"], out parcel);
                }
                OSD oenv = ViewerEnviroment.DefaultToOSD(regionID, parcel);
                httpResponse.RawBuffer = Util.UTF8NBGetbytes(OSDParser.SerializeLLSDXmlString(oenv));
                httpResponse.StatusCode = (int)HttpStatusCode.OK;
            }

            ViewerEnviroment VEnv = GetRegionEnviroment();

            OSDMap map = new OSDMap();
            map["environment"] = VEnv.ToOSD();

            string env = OSDParser.SerializeLLSDXmlString(map);

            if (String.IsNullOrEmpty(env))
            {
                StringBuilder sb = LLSDxmlEncode.Start();
                LLSDxmlEncode.AddArray(sb);
                LLSDxmlEncode.AddMap(sb);
                LLSDxmlEncode.AddElem("messageID", UUID.Zero, sb);
                LLSDxmlEncode.AddElem("regionID", regionID, sb);
                LLSDxmlEncode.AddEndMap(sb);
                LLSDxmlEncode.AddEndArray(sb);
                env = LLSDxmlEncode.End(sb);
            }

            httpResponse.RawBuffer = Util.UTF8NBGetbytes(env);
            httpResponse.StatusCode = (int)HttpStatusCode.OK;
        }

        private void SetExtEnvironmentSettings(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, UUID agentID, Caps caps)
        {
            bool success = false;
            string message = "Could not process request";
            int parcel = -1;
            int track = -1;

            StringBuilder sb = LLSDxmlEncode.Start();

            if (httpRequest.Query.Count > 0)
            {
                if (httpRequest.Query.ContainsKey("parcelid"))
                {
                    Int32.TryParse((string)httpRequest.Query["parcelid"], out parcel);
                }
                if (httpRequest.Query.ContainsKey("trackno"))
                {
                    Int32.TryParse((string)httpRequest.Query["trackno"], out track);
                }

                message = "Parcel Enviroment not supported";
                goto skiped;
            }

            if(parcel == -1)
            {
                if (!m_scene.Permissions.CanIssueEstateCommand(agentID, false))
                {
                    message = "Insufficient estate permissions, settings has not been saved.";
                    goto skiped;
                }
            }

            if(track == -1)
            {
                try
                {
                    OSD req = OSDParser.Deserialize(httpRequest.InputStream);
                    if(req is OpenMetaverse.StructuredData.OSDMap)
                    {
                        OpenMetaverse.StructuredData.OSDMap map = req as OpenMetaverse.StructuredData.OSDMap;
                        if(map.TryGetValue("environment", out OSD env))
                        {
                            ViewerEnviroment VEnv = m_scene.RegionEnviroment;
                            if (VEnv == null)
                            {
                                // need a proper clone
                                VEnv = new ViewerEnviroment();
                                OSD otmp = m_DefaultEnv.ToOSD();
                                string tmpstr = OSDParser.SerializeLLSDXmlString(otmp);
                                otmp = OSDParser.DeserializeLLSDXml(tmpstr);
                                VEnv.FromOSD(otmp);
                            }
                            OSDMap evmap = (OSDMap)env;
                            if(evmap.TryGetValue("day_asset", out OSD tmp) && !evmap.ContainsKey("day_cycle"))
                            {
                                string id = tmp.AsString();
                                AssetBase asset = m_assetService.Get(id);
                                if(asset == null || asset.Data == null || asset.Data.Length == 0)
                                {
                                    httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                                    return;
                                }
                                try
                                {
                                    OSD oenv = OSDParser.Deserialize(asset.Data);
                                    VEnv.CycleFromOSD(oenv);
                                }
                                catch
                                {
                                    httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                                    return;
                                }
                            }
                            VEnv.FromOSD(env);
                            StoreOnRegion(VEnv);

                            WindlightRefresh(0);

                            success = true;
                            m_log.InfoFormat("[{0}]: ExtEnviromet settings saved from agentID {1} in region {2}",
                                Name, agentID, caps.RegionName);
                        }
                    }
                    else if (req is OSDArray)
                    {
                        ViewerEnviroment VEnv = new ViewerEnviroment();
                        VEnv.FromWLOSD(req);
                        StoreOnRegion(VEnv);
                        success = true;

                        WindlightRefresh(0);

                        m_log.InfoFormat("[{0}]: New Environment settings has been saved from agentID {1} in region {2}",
                            Name, agentID, caps.RegionName);

                        LLSDxmlEncode.AddMap(sb);
                        LLSDxmlEncode.AddElem("messageID", UUID.Zero, sb);
                        LLSDxmlEncode.AddElem("regionID", regionID, sb);
                        LLSDxmlEncode.AddElem("success", success, sb);
                        LLSDxmlEncode.AddEndMap(sb);
                        httpResponse.RawBuffer = Util.UTF8NBGetbytes(LLSDxmlEncode.End(sb));
                        httpResponse.StatusCode = (int)HttpStatusCode.OK;
                        return;
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[{0}]: ExtEnvironment settings not saved for region {1}, Exception: {2} - {3}",
                        Name, caps.RegionName, e.Message, e.StackTrace);

                    success = false;
                    message = String.Format("ExtEnvironment Set for region {0} has failed, settings not saved.", caps.RegionName);
                }
            }

        skiped:
            string response;

            LLSDxmlEncode.AddMap(sb);
                LLSDxmlEncode.AddElem("success", success, sb);
                if(!success)
                    LLSDxmlEncode.AddElem("message", message, sb);
            LLSDxmlEncode.AddEndMap(sb);
            response = LLSDxmlEncode.End(sb);

            httpResponse.RawBuffer = Util.UTF8NBGetbytes(response);
            httpResponse.StatusCode = (int)HttpStatusCode.OK;
        }

        private void GetEnvironmentSettings(IOSHttpResponse response, UUID agentID)
        {
            // m_log.DebugFormat("[{0}]: Environment GET handle for agentID {1} in region {2}",
            //      Name, agentID, caps.RegionName);

            ViewerEnviroment VEnv = GetRegionEnviroment();

            OSD d = VEnv.ToWLOSD(UUID.Zero, regionID);
            string env = OSDParser.SerializeLLSDXmlString(d);

            if (String.IsNullOrEmpty(env))
            {
                StringBuilder sb = LLSDxmlEncode.Start();
                    LLSDxmlEncode.AddArray(sb);
                        LLSDxmlEncode.AddMap(sb);
                            LLSDxmlEncode.AddElem("messageID", UUID.Zero, sb);
                            LLSDxmlEncode.AddElem("regionID", regionID, sb);
                        LLSDxmlEncode.AddEndMap(sb);
                LLSDxmlEncode.AddEndArray(sb);
                env = LLSDxmlEncode.End(sb);
            }

            response.RawBuffer = Util.UTF8NBGetbytes(env);
            response.StatusCode = (int)HttpStatusCode.OK;
        }


        private void SetEnvironmentSettings(IOSHttpRequest request, IOSHttpResponse response, UUID agentID)
        {
            // m_log.DebugFormat("[{0}]: Environment SET handle from agentID {1} in region {2}",
            //       Name, agentID, caps.RegionName);

            bool success = false;
            string fail_reason = "";

            if (!m_scene.Permissions.CanIssueEstateCommand(agentID, false))
            {
                fail_reason = "Insufficient estate permissions, settings has not been saved.";
            }
            else
            {
                try
                {
                    ViewerEnviroment VEnv = new ViewerEnviroment();
                    OSD env = OSDParser.Deserialize(request.InputStream);
                    VEnv.FromWLOSD(env);
                    StoreOnRegion(VEnv);
                    success = true;

                    WindlightRefresh(0);

                    m_log.InfoFormat("[{0}]: New Environment settings has been saved from agentID {1} in region {2}",
                        Name, agentID, m_scene.Name);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[{0}]: Environment settings has not been saved for region {1}, Exception: {2} - {3}",
                        Name, m_scene.Name, e.Message, e.StackTrace);

                    success = false;
                    fail_reason = String.Format("Environment Set for region {0} has failed, settings not saved.", m_scene.Name);
                }
            }

            StringBuilder sb = LLSDxmlEncode.Start();
                LLSDxmlEncode.AddMap(sb);
                    LLSDxmlEncode.AddElem("messageID", UUID.Zero, sb);
                    LLSDxmlEncode.AddElem("regionID", regionID, sb);
                    LLSDxmlEncode.AddElem("success", success, sb);
                    if(!success)
                        LLSDxmlEncode.AddElem("fail_reason", fail_reason, sb);
                LLSDxmlEncode.AddEndMap(sb);
            response.RawBuffer = Util.UTF8NBGetbytes(LLSDxmlEncode.End(sb));
            response.StatusCode = (int)HttpStatusCode.OK;
        }

        public byte[] GetDefaultAssetData(int type)
        {
            OSD osddata;
            switch(type)
            {
                case 0:
                    SkyData sky = new SkyData();
                    sky.Name = "DefaultSky";
                    osddata = sky.ToOSD();
                    break;
                case 1:
                    WaterData water = new WaterData();
                    water.Name = "DefaultWater";
                    osddata = water.ToOSD();
                    break;
                case 2:
                    DayCycle day = new DayCycle();
                    day.Name="New Daycycle";
                    DayCycle.TrackEntry te = new DayCycle.TrackEntry();

                    WaterData dwater = new WaterData();
                    dwater.Name = "DefaultWater";
                    day.waterframes["DefaultWater"] = dwater;
                    te.time = 0;
                    te.frameName = "DefaultWater";
                    day.waterTrack.Add(te);

                    SkyData dsky = new SkyData();
                    dsky.Name = "DefaultSky";
                    day.skyframes["DefaultSky"] = dsky;
                    te.time = 0;
                    te.frameName = "DefaultSky";
                    day.skyTrack0.Add(te);

                    osddata = day.ToOSD();
                    break;
                default:
                    return null;
            }
            return OSDParser.SerializeLLSDNotationToBytes(osddata,true);
        }

        public List<byte[]> MakeLightShareData()
        {
            if(m_scene.RegionEnviroment == null)
                return null;

            RegionLightShareData wl = ToLightShare();
            byte[] mBlock = new Byte[249];
            int pos = 0;

            wl.waterColor.ToBytes(mBlock, 0); pos += 12;
            Utils.FloatToBytes(wl.waterFogDensityExponent).CopyTo(mBlock, pos); pos += 4;
            Utils.FloatToBytes(wl.underwaterFogModifier).CopyTo(mBlock, pos); pos += 4;
            wl.reflectionWaveletScale.ToBytes(mBlock, pos); pos += 12;
            Utils.FloatToBytes(wl.fresnelScale).CopyTo(mBlock, pos); pos += 4;
            Utils.FloatToBytes(wl.fresnelOffset).CopyTo(mBlock, pos); pos += 4;
            Utils.FloatToBytes(wl.refractScaleAbove).CopyTo(mBlock, pos); pos += 4;
            Utils.FloatToBytes(wl.refractScaleBelow).CopyTo(mBlock, pos); pos += 4;
            Utils.FloatToBytes(wl.blurMultiplier).CopyTo(mBlock, pos); pos += 4;
            wl.bigWaveDirection.ToBytes(mBlock, pos); pos += 8;
            wl.littleWaveDirection.ToBytes(mBlock, pos); pos += 8;
            wl.normalMapTexture.ToBytes(mBlock, pos); pos += 16;
            wl.horizon.ToBytes(mBlock, pos); pos += 16;
            Utils.FloatToBytes(wl.hazeHorizon).CopyTo(mBlock, pos); pos += 4;
            wl.blueDensity.ToBytes(mBlock, pos); pos += 16;
            Utils.FloatToBytes(wl.hazeDensity).CopyTo(mBlock, pos); pos += 4;
            Utils.FloatToBytes(wl.densityMultiplier).CopyTo(mBlock, pos); pos += 4;
            Utils.FloatToBytes(wl.distanceMultiplier).CopyTo(mBlock, pos); pos += 4;
            wl.sunMoonColor.ToBytes(mBlock, pos); pos += 16;
            Utils.FloatToBytes(wl.sunMoonPosition).CopyTo(mBlock, pos); pos += 4;
            wl.ambient.ToBytes(mBlock, pos); pos += 16;
            Utils.FloatToBytes(wl.eastAngle).CopyTo(mBlock, pos); pos += 4;
            Utils.FloatToBytes(wl.sunGlowFocus).CopyTo(mBlock, pos); pos += 4;
            Utils.FloatToBytes(wl.sunGlowSize).CopyTo(mBlock, pos); pos += 4;
            Utils.FloatToBytes(wl.sceneGamma).CopyTo(mBlock, pos); pos += 4;
            Utils.FloatToBytes(wl.starBrightness).CopyTo(mBlock, pos); pos += 4;
            wl.cloudColor.ToBytes(mBlock, pos); pos += 16;
            wl.cloudXYDensity.ToBytes(mBlock, pos); pos += 12;
            Utils.FloatToBytes(wl.cloudCoverage).CopyTo(mBlock, pos); pos += 4;
            Utils.FloatToBytes(wl.cloudScale).CopyTo(mBlock, pos); pos += 4;
            wl.cloudDetailXYDensity.ToBytes(mBlock, pos); pos += 12;
            Utils.FloatToBytes(wl.cloudScrollX).CopyTo(mBlock, pos); pos += 4;
            Utils.FloatToBytes(wl.cloudScrollY).CopyTo(mBlock, pos); pos += 4;
            Utils.UInt16ToBytes(wl.maxAltitude).CopyTo(mBlock, pos); pos += 2;
            mBlock[pos] = Convert.ToByte(wl.cloudScrollXLock); pos++;
            mBlock[pos] = Convert.ToByte(wl.cloudScrollYLock); pos++;
            mBlock[pos] = Convert.ToByte(wl.drawClassicClouds); pos++;

            List<byte[]> param = new List<byte[]>();
            param.Add(mBlock);
            return param;
        }

        public void SendLightShare(IClientAPI client, List<byte[]> param)
        {
            if(param == null || param.Count == 0)
                client.SendGenericMessage("WindlightReset", UUID.Random(), new List<byte[]>());
            else
                client.SendGenericMessage("Windlight", UUID.Random(), param);
        }

        private void UpdateEnvTime()
        {
            double now = Util.GetTimeStamp();
            if (now - m_framets < 10.0)
                return;

            m_framets = now;
            UpdateClientsSunTime();
        }

        private void UpdateClientsSunTime()
        {
            if(m_scene.GetNumberOfClients() == 0)
                return;

            ViewerEnviroment env = GetRegionEnviroment();
            float dayFrac = GetDayFractionTime(env);

            float wldayFrac = dayFrac;

            if (wldayFrac <= 0.25f)
                wldayFrac += 1.5f;
            else if (wldayFrac > 0.75f)
                wldayFrac += 0.5f;
            else if (wldayFrac >= 0.333333f)
                wldayFrac = 3f * wldayFrac - 1f;
            else
                wldayFrac = 3f * wldayFrac + 1f;

            wldayFrac = Utils.Clamp(wldayFrac, 0, 2f);
            wldayFrac *= Utils.PI;

            float eepDayFrac = dayFrac * Utils.TWO_PI;

            m_scene.ForEachRootScenePresence(delegate (ScenePresence sp)
            {
                if(sp.IsDeleted || sp.IsInTransit || sp.IsNPC)
                    return;

                IClientAPI client = sp.ControllingClient;
                uint vflags = client.GetViewerCaps();

                if ((vflags & 0x8000) != 0)
                {
                    client.SendViewerTime(Vector3.Zero, eepDayFrac);
                    return;
                }

                env.getWLPositions(sp.AbsolutePosition.Z, dayFrac, out Vector3 m_sunDir);
                client.SendViewerTime(m_sunDir, wldayFrac);
            });
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ViewerEnviroment GetRegionEnviroment()
        {
            return m_scene.RegionEnviroment == null ? m_DefaultEnv : m_scene.RegionEnviroment;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public float GetDayFractionTime(ViewerEnviroment env)
        {
            double dayfrac = env.DayLength;
            dayfrac = ((Util.UnixTimeSinceEpochSecs() + env.DayOffset) % dayfrac) / dayfrac;
            return (float)Utils.Clamp(dayfrac, 0, 1);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public float GetRegionDayFractionTime()
        {
            return GetDayFractionTime(GetRegionEnviroment());
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public int GetDayLength(ViewerEnviroment env)
        {
            return env.DayLength;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public int GetDayOffset(ViewerEnviroment env)
        {
            return env.DayOffset;
        }

        public Vector3 GetSunDir(ViewerEnviroment env, float altitude)
        {
            env.getPositions(altitude, GetDayFractionTime(env), out Vector3 sundir, out Vector3 moondir,
                out Quaternion sunrot, out Quaternion moonrot);
            return sundir;
        }

        public Quaternion GetSunRot(ViewerEnviroment env, float altitude)
        {
            env.getPositions(altitude, GetDayFractionTime(env), out Vector3 sundir, out Vector3 moondir,
                out Quaternion sunrot, out Quaternion moonrot);
            return sunrot;
        }

        public Vector3 GetMoonDir(ViewerEnviroment env, float altitude)
        {
            env.getPositions(altitude, GetDayFractionTime(env), out Vector3 sundir, out Vector3 moondir,
                out Quaternion sunrot, out Quaternion moonrot);
            return moondir;
        }

        public Quaternion GetMoonRot(ViewerEnviroment env, float altitude)
        {
            env.getPositions(altitude, GetDayFractionTime(env), out Vector3 sundir, out Vector3 moondir,
                out Quaternion sunrot, out Quaternion moonrot);
            return moonrot;
        }

        [System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetRegionDayLength()
        {
            return GetRegionEnviroment().DayLength;
        }

        [System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetRegionDayOffset()
        {
            return GetRegionEnviroment().DayOffset;
        }

        [System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetRegionSunDir(float altitude)
        {
            return GetSunDir(GetRegionEnviroment(), altitude);
        }

        [System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Quaternion GetRegionSunRot(float altitude)
        {
            return GetSunRot(GetRegionEnviroment(), altitude);
        }

        [System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetRegionMoonDir(float altitude)
        {
            return GetMoonDir(GetRegionEnviroment(), altitude);
        }

        [System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Quaternion GetRegionMoonRot(float altitude)
        {
            return GetMoonRot(GetRegionEnviroment(), altitude);
        }
    }
}

