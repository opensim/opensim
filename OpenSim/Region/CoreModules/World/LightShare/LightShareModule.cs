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
using System.Reflection;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Framework.InterfaceCommander;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using log4net;
using Nini.Config;
using Mono.Addins;

namespace OpenSim.Region.CoreModules.World.LightShare
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LightShareModule")]
    public class LightShareModule : INonSharedRegionModule, ILightShareModule, ICommandableModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Commander m_commander = new Commander("windlight");
        private Scene m_scene;
        private static bool m_enableWindlight;

        #region ICommandableModule Members

        public ICommander CommandInterface
        {
            get { return m_commander; }
        }

        #endregion

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
            try
            {
                m_enableWindlight = config.Configs["LightShare"].GetBoolean("enable_windlight", false);
            }
            catch (Exception)
            {
                m_log.Debug("[WINDLIGHT]: ini failure for enable_windlight - using default");
            }

            m_log.DebugFormat("[WINDLIGHT]: windlight module {0}", (m_enableWindlight ? "enabled" : "disabled"));
        }

        public void AddRegion(Scene scene)
        {
            if (!m_enableWindlight)
                return;

            m_scene = scene;
            m_scene.RegisterModuleInterface<ILightShareModule>(this);
            m_scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;

            m_scene.EventManager.OnMakeRootAgent += EventManager_OnMakeRootAgent;
            m_scene.EventManager.OnSaveNewWindlightProfile += EventManager_OnSaveNewWindlightProfile;
            m_scene.EventManager.OnSendNewWindlightProfileTargeted += EventManager_OnSendNewWindlightProfileTargeted;
            m_scene.LoadWindlightProfile();

            InstallCommands();
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_enableWindlight)
                return;

            m_scene.EventManager.OnPluginConsole -= EventManager_OnPluginConsole;

            m_scene.EventManager.OnMakeRootAgent -= EventManager_OnMakeRootAgent;
            m_scene.EventManager.OnSaveNewWindlightProfile -= EventManager_OnSaveNewWindlightProfile;
            m_scene.EventManager.OnSendNewWindlightProfileTargeted -= EventManager_OnSendNewWindlightProfileTargeted;

            m_scene = null;
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "LightShareModule"; }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        public static bool EnableWindlight
        {
            get
            {
                return m_enableWindlight;
            }
            set
            {
            }
        }

        #region events

        private List<byte[]> compileWindlightSettings(RegionLightShareData wl)
        {
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

        public void SendProfileToClient(IClientAPI client)
        {
            SendProfileToClient(client, m_scene.RegionInfo.WindlightSettings);
        }

        public void SendProfileToClient(IClientAPI client, RegionLightShareData wl)
        {
            if (client == null)
                return;

            if (m_enableWindlight)
            {
                if (m_scene.RegionInfo.WindlightSettings.valid)
                {
                    List<byte[]> param = compileWindlightSettings(wl);
                    client.SendGenericMessage("Windlight", param);
                }
                else
                {
                    List<byte[]> param = new List<byte[]>();
                    client.SendGenericMessage("WindlightReset", param);
                }
            }
        }

        private void EventManager_OnMakeRootAgent(ScenePresence presence)
        {
            if (m_enableWindlight && m_scene.RegionInfo.WindlightSettings.valid)
                m_log.Debug("[WINDLIGHT]: Sending windlight scene to new client");
            SendProfileToClient(presence.ControllingClient);
        }

        private void EventManager_OnSendNewWindlightProfileTargeted(RegionLightShareData wl, UUID pUUID)
        {
            IClientAPI client;
            m_scene.TryGetClient(pUUID, out client);
            SendProfileToClient(client, wl);
        }

        private void EventManager_OnSaveNewWindlightProfile()
        {
            m_scene.ForEachRootClient(SendProfileToClient);
        }

        #endregion

        #region ICommandableModule Members

        private void InstallCommands()
        {
            Command wlload = new Command("load", CommandIntentions.COMMAND_NON_HAZARDOUS, HandleLoad, "Load windlight profile from the database and broadcast");
            Command wlenable = new Command("enable", CommandIntentions.COMMAND_NON_HAZARDOUS, HandleEnable, "Enable the windlight plugin");
            Command wldisable = new Command("disable", CommandIntentions.COMMAND_NON_HAZARDOUS, HandleDisable, "Disable the windlight plugin");

            m_commander.RegisterCommand("load", wlload);
            m_commander.RegisterCommand("enable", wlenable);
            m_commander.RegisterCommand("disable", wldisable);

            m_scene.RegisterModuleCommander(m_commander);
        }

        private void HandleLoad(Object[] args)
        {
            if (!m_enableWindlight)
            {
                m_log.InfoFormat("[WINDLIGHT]: Cannot load windlight profile, module disabled. Use 'windlight enable' first.");
            }
            else
            {
                m_log.InfoFormat("[WINDLIGHT]: Loading Windlight profile from database");
                m_scene.LoadWindlightProfile();
                m_log.InfoFormat("[WINDLIGHT]: Load complete");
            }
        }

        private void HandleDisable(Object[] args)
        {
            m_log.InfoFormat("[WINDLIGHT]: Plugin now disabled");
            m_enableWindlight = false;
        }

        private void HandleEnable(Object[] args)
        {
            m_log.InfoFormat("[WINDLIGHT]: Plugin now enabled");
            m_enableWindlight = true;
        }

        /// <summary>
        /// Processes commandline input. Do not call directly.
        /// </summary>
        /// <param name="args">Commandline arguments</param>
        private void EventManager_OnPluginConsole(string[] args)
        {
            if (args[0] == "windlight")
            {
                if (args.Length == 1)
                {
                    m_commander.ProcessConsoleCommand("add", new string[0]);
                    return;
                }

                string[] tmpArgs = new string[args.Length - 2];
                int i;
                for (i = 2; i < args.Length; i++)
                {
                    tmpArgs[i - 2] = args[i];
                }

                m_commander.ProcessConsoleCommand(args[1], tmpArgs);
            }
        }
        #endregion

    }
}

