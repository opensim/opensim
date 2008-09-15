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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Runtime.Remoting.Lifetime;
using OpenMetaverse;
using Nini.Config;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.ScriptEngine.Shared; 
using OpenSim.Region.ScriptEngine.Shared.Api.Plugins; 
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;
using TPFlags = OpenSim.Framework.Constants.TeleportFlags;

namespace OpenSim.Region.ScriptEngine.Shared.Api
{
    //////////////////////////////////////////////////////////////
    //
    // Level description
    //
    // None     - Function is no threat at all. It doesn't constitute
    //            an threat to either users or the system and has no
    //            known side effects
    //
    // Nuisance - Abuse of this command can cause a nuisance to the
    //            region operator, such as log message spew
    //
    // VeryLow  - Extreme levels ob abuse of this function can cause
    //            impaired functioning of the region, or very gullible
    //            users can be tricked into experiencing harmless effects
    //
    // Low      - Intentional abuse can cause crashes or malfunction
    //            under certain circumstances, which can easily be rectified,
    //            or certain users can be tricked into certain situations
    //            in an avoidable manner.
    //
    // Moderate - Intentional abuse can cause denial of service and crashes
    //            with potential of data or state loss, or trusting users
    //            can be tricked into embarrassing or uncomfortable
    //            situationsa.
    //
    // High     - Casual abuse can cause impaired functionality or temporary
    //            denial of service conditions. Intentional abuse can easily
    //            cause crashes with potential data loss, or can be used to
    //            trick experienced and cautious users into unwanted situations,
    //            or changes global data permanently and without undo ability
    //
    // VeryHigh - Even normal use may, depending on the number of instances,
    //            or frequency of use, result in severe service impairment
    //            or crash with loss of data, or can be used to cause
    //            unwanted or harmful effects on users without giving the
    //            user a means to avoid it.
    //
    // Severe   - Even casual use is a danger to region stability, or function
    //            allows console or OS command execution, or function allows
    //            taking money without consent, or allows deletion or
    //            modification of user data, or allows the compromise of
    //            sensitive data by design.

    public enum ThreatLevel
    {
        None = 0,
        Nuisance = 1,
        VeryLow = 2,
        Low = 3,
        Moderate = 4,
        High = 5,
        VeryHigh = 6,
        Severe = 7
    };
        
    [Serializable]
    public class OSSL_Api : MarshalByRefObject, IOSSL_Api, IScriptApi
    {
        internal IScriptEngine m_ScriptEngine;
        internal SceneObjectPart m_host;
        internal uint m_localID;
        internal UUID m_itemID;
        internal bool m_OSFunctionsEnabled = false;
        internal ThreatLevel m_MaxThreatLevel = ThreatLevel.VeryLow;
        internal float m_ScriptDelayFactor = 1.0f;
        internal float m_ScriptDistanceFactor = 1.0f;

        public void Initialize(IScriptEngine ScriptEngine, SceneObjectPart host, uint localID, UUID itemID)
        {
            m_ScriptEngine = ScriptEngine;
            m_host = host;
            m_localID = localID;
            m_itemID = itemID;

            if (m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
                m_OSFunctionsEnabled = true;

            m_ScriptDelayFactor =
                    m_ScriptEngine.Config.GetFloat("ScriptDelayFactor", 1.0f);
            m_ScriptDistanceFactor =
                    m_ScriptEngine.Config.GetFloat("ScriptDistanceLimitFactor", 1.0f);

            string risk = m_ScriptEngine.Config.GetString("OSFunctionThreatLevel", "VeryLow");
            switch (risk)
            {
            case "None":
                m_MaxThreatLevel = ThreatLevel.None;
                break;
            case "VeryLow":
                m_MaxThreatLevel = ThreatLevel.VeryLow;
                break;
            case "Low":
                m_MaxThreatLevel = ThreatLevel.Low;
                break;
            case "Moderate":
                m_MaxThreatLevel = ThreatLevel.Moderate;
                break;
            case "High":
                m_MaxThreatLevel = ThreatLevel.High;
                break;
            case "VeryHigh":
                m_MaxThreatLevel = ThreatLevel.VeryHigh;
                break;
            case "Severe":
                m_MaxThreatLevel = ThreatLevel.Severe;
                break;
            default:
                break;
            }
        }

        //
        // Never expire this object
        //
        public override Object InitializeLifetimeService()
        {
            ILease lease = (ILease)base.InitializeLifetimeService();

            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.Zero;
            }
            return lease;
        }

        public Scene World
        {
            get { return m_ScriptEngine.World; }
        }

        internal void OSSLError(string msg)
        {
            throw new Exception("OSSL Runtime Error: " + msg);
        }

        protected void CheckThreatLevel(ThreatLevel level, string function)
        {
            if (level > m_MaxThreatLevel)
                throw new Exception("Threat level too high - "+function);
        }

        protected void ScriptSleep(int delay)
        {
            delay = (int)((float)delay * m_ScriptDelayFactor);
            if (delay == 0)
                return;
            System.Threading.Thread.Sleep(delay);
        }

        //
        // OpenSim functions
        //

        public int osTerrainSetHeight(int x, int y, double val)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osTerrainSetHeight: permission denied");
                return 0;
            }
            CheckThreatLevel(ThreatLevel.High, "osTerrainSetHeight");

            m_host.AddScriptLPS(1);
            if (x > 255 || x < 0 || y > 255 || y < 0)
                OSSLError("osTerrainSetHeight: Coordinate out of bounds");

            if (World.ExternalChecks.ExternalChecksCanTerraformLand(m_host.OwnerID, new Vector3(x, y, 0)))
            {
                World.Heightmap[x, y] = val;
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public double osTerrainGetHeight(int x, int y)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osTerrainGetHeight: permission denied");
                return 0.0;
            }
            CheckThreatLevel(ThreatLevel.None, "osTerrainGetHeight");

            m_host.AddScriptLPS(1);
            if (x > 255 || x < 0 || y > 255 || y < 0)
                OSSLError("osTerrainGetHeight: Coordinate out of bounds");

            return World.Heightmap[x, y];
        }

        public int osRegionRestart(double seconds)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osRegionRestart: permission denied");
                return 0;
            }
            // This is High here because region restart is not reliable
            // it may result in the region staying down or becoming
            // unstable. This should be changed to Low or VeryLow once
            // The underlying functionality is fixed, since the security
            // as such is sound
            //
            CheckThreatLevel(ThreatLevel.High, "osRegionRestart");

            m_host.AddScriptLPS(1);
            if (World.ExternalChecks.ExternalChecksCanIssueEstateCommand(m_host.OwnerID, false))
            {
                World.Restart((float)seconds);
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public void osRegionNotice(string msg)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osRegionNotice: permission denied");
                return;
            }

            // This implementation provides absolutely no security
            // It's high griefing potential makes this classification
            // necessary
            //
            CheckThreatLevel(ThreatLevel.VeryHigh, "osRegionNotice");

            m_host.AddScriptLPS(1);
            World.SendGeneralAlert(msg);
        }

        public void osSetRot(UUID target, Quaternion rotation)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osSetRot: permission denied");
                return;
            }

            // This function has no security. It can be used to destroy
            // arbitrary builds the user would normally have no rights to
            //
            CheckThreatLevel(ThreatLevel.VeryHigh, "osSetRot");

            m_host.AddScriptLPS(1);
            if (World.Entities.ContainsKey(target))
            {
                World.Entities[target].Rotation = rotation;
            }
            else
            {
                OSSLError("osSetRot: Invalid target");
            }
        }

        public string osSetDynamicTextureURL(string dynamicID, string contentType, string url, string extraParams,
                                             int timer)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osSetDynamicTextureURL: permission denied");
                return String.Empty;
            }

            // This may be upgraded depending on the griefing or DOS
            // potential, or guarded with a delay
            //
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetDynamicTextureURL");

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                UUID createdTexture =
                    textureManager.AddDynamicTextureURL(World.RegionInfo.RegionID, m_host.UUID, contentType, url,
                                                        extraParams, timer);
                return createdTexture.ToString();
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return UUID.Zero.ToString();
        }

        public string osSetDynamicTextureURLBlend(string dynamicID, string contentType, string url, string extraParams,
                                             int timer, int alpha)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osSetDynamicTextureURLBlend: permission denied");
                return String.Empty;
            }

            CheckThreatLevel(ThreatLevel.VeryLow, "osSetDynamicTextureURLBlend");

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                UUID createdTexture =
                    textureManager.AddDynamicTextureURL(World.RegionInfo.RegionID, m_host.UUID, contentType, url,
                                                        extraParams, timer, true, (byte) alpha);
                return createdTexture.ToString();
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return UUID.Zero.ToString();
        }

        public string osSetDynamicTextureData(string dynamicID, string contentType, string data, string extraParams,
                                           int timer)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osSetDynamicTextureData: permission denied");
                return String.Empty;
            }

            CheckThreatLevel(ThreatLevel.VeryLow, "osSetDynamicTextureData");

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                if (textureManager != null)
                {
                    if (extraParams == String.Empty)
                    {
                        extraParams = "256";
                    }
                    UUID createdTexture =
                        textureManager.AddDynamicTextureData(World.RegionInfo.RegionID, m_host.UUID, contentType, data,
                                                            extraParams, timer);
                    return createdTexture.ToString();
                }
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return UUID.Zero.ToString();
        }

        public string osSetDynamicTextureDataBlend(string dynamicID, string contentType, string data, string extraParams,
                                          int timer, int alpha)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osSetDynamicTextureDataBlend: permission denied");
                return String.Empty;
            }

            CheckThreatLevel(ThreatLevel.VeryLow, "osSetDynamicTextureDataBlend");

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                if (textureManager != null)
                {
                    if (extraParams == String.Empty)
                    {
                        extraParams = "256";
                    }
                    UUID createdTexture =
                        textureManager.AddDynamicTextureData(World.RegionInfo.RegionID, m_host.UUID, contentType, data,
                                                            extraParams, timer, true, (byte) alpha);
                    return createdTexture.ToString();
                }
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return UUID.Zero.ToString();
        }

        public bool osConsoleCommand(string command)
        {
            m_host.AddScriptLPS(1);
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osConsoleCommand: permission denied");
                return false;
            }

            CheckThreatLevel(ThreatLevel.Severe, "osConsoleCommand");

            if (World.ExternalChecks.ExternalChecksCanRunConsoleCommand(m_host.OwnerID))
            {
                MainConsole.Instance.RunCommand(command);
                return true;
            }
            return false;
        }

        public void osSetPrimFloatOnWater(int floatYN)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osSetPrimFloatOnWater: permission denied");
                return;
            }

            CheckThreatLevel(ThreatLevel.VeryLow, "osSetPrimFloatOnWater");

            m_host.AddScriptLPS(1);
            if (m_host.ParentGroup != null)
            {
                if (m_host.ParentGroup.RootPart != null)
                {
                    m_host.ParentGroup.RootPart.SetFloatOnWater(floatYN);
                }
            }
        }

        // Teleport functions
        public void osTeleportAgent(string agent, string regionName, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osTeleportAgent: permission denied");
                return;
            }

            // High because there is no security check. High griefer potential
            //
            CheckThreatLevel(ThreatLevel.High, "osTeleportAgent");

            m_host.AddScriptLPS(1);
            UUID agentId = new UUID();
            if (UUID.TryParse(agent, out agentId))
            {
                ScenePresence presence = World.GetScenePresence(agentId);
                if (presence != null)
                {
                    // agent must be over owners land to avoid abuse
                    if (m_host.OwnerID == World.GetLandOwner(presence.AbsolutePosition.X, presence.AbsolutePosition.Y))
                    {
                        World.RequestTeleportLocation(presence.ControllingClient, regionName,
                            new Vector3((float)position.x, (float)position.y, (float)position.z),
                            new Vector3((float)lookat.x, (float)lookat.y, (float)lookat.z), (uint)TPFlags.ViaLocation);
                        ScriptSleep(5000);
                    }
                }
            }
        }

        public void osTeleportAgent(string agent, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat)
        {
            osTeleportAgent(agent, World.RegionInfo.RegionName, position, lookat);
        }

        // Adam's super super custom animation functions
        public void osAvatarPlayAnimation(string avatar, string animation)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osAvatarPlayAnimation: permission denied");
                return;
            }

            CheckThreatLevel(ThreatLevel.VeryHigh, "osAvatarPlayAnimation");

            m_host.AddScriptLPS(1);
            if (World.Entities.ContainsKey(avatar) && World.Entities[avatar] is ScenePresence)
            {
                ScenePresence target = (ScenePresence)World.Entities[avatar];
                target.AddAnimation(animation);
            }
        }

        public void osAvatarStopAnimation(string avatar, string animation)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osAvatarStopAnimation: permission denied");
                return;
            }

            CheckThreatLevel(ThreatLevel.VeryHigh, "osAvatarStopAnimation");

            m_host.AddScriptLPS(1);
            if (World.Entities.ContainsKey(avatar) && World.Entities[avatar] is ScenePresence)
            {
                ScenePresence target = (ScenePresence)World.Entities[avatar];
                target.RemoveAnimation(animation);
            }
        }

        //Texture draw functions
        public string osMovePen(string drawList, int x, int y)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osMovePen: permission denied");
                return String.Empty;
            }

            CheckThreatLevel(ThreatLevel.None, "osMovePen");

            m_host.AddScriptLPS(1);
            drawList += "MoveTo " + x + "," + y + ";";
            return drawList;
        }

        public string osDrawLine(string drawList, int startX, int startY, int endX, int endY)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osDrawLine: permission denied");
                return String.Empty;
            }

            CheckThreatLevel(ThreatLevel.None, "osDrawLine");

            m_host.AddScriptLPS(1);
            drawList += "MoveTo "+ startX+","+ startY +"; LineTo "+endX +","+endY +"; ";
            return drawList;
        }

        public string osDrawLine(string drawList, int endX, int endY)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osDrawLine: permission denied");
                return String.Empty;
            }

            CheckThreatLevel(ThreatLevel.None, "osDrawLine");

            m_host.AddScriptLPS(1);
            drawList += "LineTo " + endX + "," + endY + "; ";
            return drawList;
        }

        public string osDrawText(string drawList, string text)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osDrawText: permission denied");
                return String.Empty;
            }

            CheckThreatLevel(ThreatLevel.None, "osDrawText");

            m_host.AddScriptLPS(1);
            drawList += "Text " + text + "; ";
            return drawList;
        }

        public string osDrawEllipse(string drawList, int width, int height)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osDrawEllipse: permission denied");
                return String.Empty;
            }

            CheckThreatLevel(ThreatLevel.None, "osDrawEllipse");

            m_host.AddScriptLPS(1);
            drawList += "Ellipse " + width + "," + height + "; ";
            return drawList;
        }

        public string osDrawRectangle(string drawList, int width, int height)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osDrawRectangle: permission denied");
                return String.Empty;
            }

            CheckThreatLevel(ThreatLevel.None, "osDrawRectangle");

            m_host.AddScriptLPS(1);
            drawList += "Rectangle " + width + "," + height + "; ";
            return drawList;
        }

        public string osDrawFilledRectangle(string drawList, int width, int height)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osDrawFilledRectangle: permission denied");
                return String.Empty;
            }

            CheckThreatLevel(ThreatLevel.None, "osDrawFilledRectangle");

            m_host.AddScriptLPS(1);
            drawList += "FillRectangle " + width + "," + height + "; ";
            return drawList;
        }

        public string osSetFontSize(string drawList, int fontSize)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osSetFontSize: permission denied");
                return String.Empty;
            }

            CheckThreatLevel(ThreatLevel.None, "osSetFontSize");

            m_host.AddScriptLPS(1);
            drawList += "FontSize "+ fontSize +"; ";
            return drawList;
        }

        public string osSetPenSize(string drawList, int penSize)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osSetPenSize: permission denied");
                return String.Empty;
            }

            CheckThreatLevel(ThreatLevel.None, "osSetPenSize");

            m_host.AddScriptLPS(1);
            drawList += "PenSize " + penSize + "; ";
            return drawList;
        }

        public string osSetPenColour(string drawList, string colour)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osSetPenColour: permission denied");
                return String.Empty;
            }

            CheckThreatLevel(ThreatLevel.None, "osSetPenColour");

            m_host.AddScriptLPS(1);
            drawList += "PenColour " + colour + "; ";
            return drawList;
        }

        public string osDrawImage(string drawList, int width, int height, string imageUrl)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osDrawImage: permission denied");
                return String.Empty;
            }

            CheckThreatLevel(ThreatLevel.None, "osDrawImage");

            m_host.AddScriptLPS(1);
            drawList +="Image " +width + "," + height+ ","+ imageUrl +"; " ;
            return drawList;
        }

        public void osSetStateEvents(int events)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osSetStateEvents: permission denied");
                return;
            }

            // This function is a hack. There is no reason for it's existence
            // anymore, since state events now work properly.
            // It was probably added as a crutch or debugging aid, and
            // should be removed
            //
            CheckThreatLevel(ThreatLevel.High, "osSetStateEvents");

            m_host.SetScriptEvents(m_itemID, events);
        }

        public void osSetRegionWaterHeight(double height)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osSetRegionWaterHeight: permission denied");
                return;
            }

            CheckThreatLevel(ThreatLevel.High, "osSetRegionWaterHeight");

            m_host.AddScriptLPS(1);
            //Check to make sure that the script's owner is the estate manager/master
            //World.Permissions.GenericEstatePermission(
            if (World.ExternalChecks.ExternalChecksCanBeGodLike(m_host.OwnerID))
            {
                World.EventManager.TriggerRequestChangeWaterHeight((float)height);
            }
        }

        public double osList2Double(LSL_Types.list src, int index)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osList2Double: permission denied");
                return 0.0;
            }

            // There is really no double type in OSSL. C# and other
            // have one, but the current implementation of LSL_Types.list
            // is not allowed to contain any.
            // This really should be removed.
            //
            CheckThreatLevel(ThreatLevel.None, "osList2Double");

            m_host.AddScriptLPS(1);
            if (index < 0)
            {
                index = src.Length + index;
            }
            if (index >= src.Length)
            {
                return 0.0;
            }
            return Convert.ToDouble(src.Data[index]);
        }

        public void osSetParcelMediaURL(string url)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osSetParcelMediaURL: permission denied");
                return;
            }

            // What actually is the difference to the LL function?
            //
            CheckThreatLevel(ThreatLevel.VeryLow, "osSetParcelMediaURL");

            m_host.AddScriptLPS(1);
            UUID landowner = World.GetLandOwner(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y);

            if (landowner == UUID.Zero)
            {
                return;
            }

            if (landowner != m_host.ObjectOwner)
            {
                return;
            }

            World.SetLandMediaURL(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y, url);
        }

        public string osGetScriptEngineName()
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osGetScriptEngineName: permission denied");
                return "";
            }

            // This gets a "high" because knowing the engine may be used
            // to exploit engine-specific bugs or induce usage patterns
            // that trigger engine-specific failures.
            // Besides, public grid users aren't supposed to know.
            //
            CheckThreatLevel(ThreatLevel.High, "osGetScriptEngineName");

            m_host.AddScriptLPS(1);

            int scriptEngineNameIndex = 0;

            if (!String.IsNullOrEmpty(m_ScriptEngine.ScriptEngineName))
            {
                // parse off the "ScriptEngine."
                scriptEngineNameIndex = m_ScriptEngine.ScriptEngineName.IndexOf(".", scriptEngineNameIndex);
                scriptEngineNameIndex++; // get past delimiter

                int scriptEngineNameLength = m_ScriptEngine.ScriptEngineName.Length - scriptEngineNameIndex;

                // create char array then a string that is only the script engine name
                Char[] scriptEngineNameCharArray = m_ScriptEngine.ScriptEngineName.ToCharArray(scriptEngineNameIndex, scriptEngineNameLength);
                String scriptEngineName = new String(scriptEngineNameCharArray);

                return scriptEngineName;
            }
            else
            {
                return String.Empty;
            }
        }


        //for testing purposes only
        public void osSetParcelMediaTime(double time)
        {
            if (!m_OSFunctionsEnabled)
            {
                OSSLError("osSetParcelMediaTime: permission denied");
                return;
            }

            // This gets very high because I have no idea what it does.
            // If someone knows, please adjust. If it;s no longer needed,
            // please remove.
            //
            CheckThreatLevel(ThreatLevel.VeryHigh, "osSetParcelMediaTime");

            m_host.AddScriptLPS(1);

            World.ParcelMediaSetTime((float)time);
        }
    }
}
