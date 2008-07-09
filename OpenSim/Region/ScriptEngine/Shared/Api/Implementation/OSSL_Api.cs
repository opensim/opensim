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
using Axiom.Math;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.ScriptEngine.Shared; 
using OpenSim.Region.ScriptEngine.Shared.Api.Plugins; 
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;

namespace OpenSim.Region.ScriptEngine.Shared.Api
{
    [Serializable]
    public class OSSL_Api : MarshalByRefObject, IOSSL_Api, IScriptApi
    {
        internal IScriptEngine m_ScriptEngine;
        internal SceneObjectPart m_host;
        internal uint m_localID;
        internal LLUUID m_itemID;

        public void Initialize(IScriptEngine ScriptEngine, SceneObjectPart host, uint localID, LLUUID itemID)
        {
            m_ScriptEngine = ScriptEngine;
            m_host = host;
            m_localID = localID;
            m_itemID = itemID;
        }


        //
        // OpenSim functions
        //

        public int osTerrainSetHeight(int x, int y, double val)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osTerrainSetHeight: permission denied");
                return 0;
            }

            m_host.AddScriptLPS(1);
            if (x > 255 || x < 0 || y > 255 || y < 0)
                OSSLError("osTerrainSetHeight: Coordinate out of bounds");

            if (World.ExternalChecks.ExternalChecksCanTerraformLand(m_host.OwnerID, new LLVector3(x, y, 0)))
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
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osTerrainGetHeight: permission denied");
                return 0.0;
            }

            m_host.AddScriptLPS(1);
            if (x > 255 || x < 0 || y > 255 || y < 0)
                OSSLError("osTerrainGetHeight: Coordinate out of bounds");

            return World.Heightmap[x, y];
        }

        public int osRegionRestart(double seconds)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osRegionRestart: permission denied");
                return 0;
            }

            m_host.AddScriptLPS(1);
            if (World.ExternalChecks.ExternalChecksCanIssueEstateCommand(m_host.OwnerID))
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
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osRegionNotice: permission denied");
                return;
            }

            m_host.AddScriptLPS(1);
            World.SendGeneralAlert(msg);
        }

        public void osSetRot(LLUUID target, Quaternion rotation)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osSetRot: permission denied");
                return;
            }

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
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osSetDynamicTextureURL: permission denied");
                return String.Empty;
            }

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                LLUUID createdTexture =
                    textureManager.AddDynamicTextureURL(World.RegionInfo.RegionID, m_host.UUID, contentType, url,
                                                        extraParams, timer);
                return createdTexture.ToString();
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return LLUUID.Zero.ToString();
        }

        public string osSetDynamicTextureURLBlend(string dynamicID, string contentType, string url, string extraParams,
                                             int timer, int alpha)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osSetDynamicTextureURLBlend: permission denied");
                return String.Empty;
            }

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                LLUUID createdTexture =
                    textureManager.AddDynamicTextureURL(World.RegionInfo.RegionID, m_host.UUID, contentType, url,
                                                        extraParams, timer, true, (byte) alpha);
                return createdTexture.ToString();
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return LLUUID.Zero.ToString();
        }

        public string osSetDynamicTextureData(string dynamicID, string contentType, string data, string extraParams,
                                           int timer)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osSetDynamicTextureData: permission denied");
                return String.Empty;
            }

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                if (textureManager != null)
                {
                    LLUUID createdTexture =
                        textureManager.AddDynamicTextureData(World.RegionInfo.RegionID, m_host.UUID, contentType, data,
                                                            extraParams, timer);
                    return createdTexture.ToString();
                }
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return LLUUID.Zero.ToString();
        }

        public string osSetDynamicTextureDataBlend(string dynamicID, string contentType, string data, string extraParams,
                                          int timer, int alpha)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osSetDynamicTextureDataBlend: permission denied");
                return String.Empty;
            }

            m_host.AddScriptLPS(1);
            if (dynamicID == String.Empty)
            {
                IDynamicTextureManager textureManager = World.RequestModuleInterface<IDynamicTextureManager>();
                if (textureManager != null)
                {
                    LLUUID createdTexture =
                        textureManager.AddDynamicTextureData(World.RegionInfo.RegionID, m_host.UUID, contentType, data,
                                                            extraParams, timer, true, (byte) alpha);
                    return createdTexture.ToString();
                }
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return LLUUID.Zero.ToString();
        }

        public bool osConsoleCommand(string command)
        {
            m_host.AddScriptLPS(1);
            if (m_ScriptEngine.Config.GetBoolean("AllowosConsoleCommand", false))
            {
                if (World.ExternalChecks.ExternalChecksCanRunConsoleCommand(m_host.OwnerID))
                {
                    MainConsole.Instance.RunCommand(command);
                    return true;
                }
                return false;
            }
            return false;
        }
        public void osSetPrimFloatOnWater(int floatYN)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osSetPrimFloatOnWater: permission denied");
                return;
            }

            m_host.AddScriptLPS(1);
            if (m_host.ParentGroup != null)
            {
                if (m_host.ParentGroup.RootPart != null)
                {
                    m_host.ParentGroup.RootPart.SetFloatOnWater(floatYN);
                }
            }
        }

        // Adam's super super custom animation functions
        public void osAvatarPlayAnimation(string avatar, string animation)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osAvatarPlayAnimation: permission denied");
                return;
            }

            m_host.AddScriptLPS(1);
            if (World.Entities.ContainsKey(avatar) && World.Entities[avatar] is ScenePresence)
            {
                ScenePresence target = (ScenePresence)World.Entities[avatar];
                target.AddAnimation(avatar);
            }
        }

        public void osAvatarStopAnimation(string avatar, string animation)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osAvatarStopAnimation: permission denied");
                return;
            }

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
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osMovePen: permission denied");
                return String.Empty;
            }

            m_host.AddScriptLPS(1);
            drawList += "MoveTo " + x + "," + y + ";";
            return drawList;
        }

        public string osDrawLine(string drawList, int startX, int startY, int endX, int endY)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osDrawLine: permission denied");
                return String.Empty;
            }

            m_host.AddScriptLPS(1);
            drawList += "MoveTo "+ startX+","+ startY +"; LineTo "+endX +","+endY +"; ";
            return drawList;
        }

        public string osDrawLine(string drawList, int endX, int endY)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osDrawLine: permission denied");
                return String.Empty;
            }

            m_host.AddScriptLPS(1);
            drawList += "LineTo " + endX + "," + endY + "; ";
            return drawList;
        }

        public string osDrawText(string drawList, string text)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osDrawText: permission denied");
                return String.Empty;
            }

            m_host.AddScriptLPS(1);
            drawList += "Text " + text + "; ";
            return drawList;
        }

        public string osDrawEllipse(string drawList, int width, int height)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osDrawEllipse: permission denied");
                return String.Empty;
            }

            m_host.AddScriptLPS(1);
            drawList += "Ellipse " + width + "," + height + "; ";
            return drawList;
        }

        public string osDrawRectangle(string drawList, int width, int height)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osDrawRectangle: permission denied");
                return String.Empty;
            }

            m_host.AddScriptLPS(1);
            drawList += "Rectangle " + width + "," + height + "; ";
            return drawList;
        }

        public string osDrawFilledRectangle(string drawList, int width, int height)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osDrawFilledRectangle: permission denied");
                return String.Empty;
            }

            m_host.AddScriptLPS(1);
            drawList += "FillRectangle " + width + "," + height + "; ";
            return drawList;
        }

        public string osSetFontSize(string drawList, int fontSize)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osSetFontSize: permission denied");
                return String.Empty;
            }

            m_host.AddScriptLPS(1);
            drawList += "FontSize "+ fontSize +"; ";
            return drawList;
        }

        public string osSetPenSize(string drawList, int penSize)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osSetPenSize: permission denied");
                return String.Empty;
            }

            m_host.AddScriptLPS(1);
            drawList += "PenSize " + penSize + "; ";
            return drawList;
        }

        public string osSetPenColour(string drawList, string colour)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osSetPenColour: permission denied");
                return String.Empty;
            }

            m_host.AddScriptLPS(1);
            drawList += "PenColour " + colour + "; ";
            return drawList;
        }

        public string osDrawImage(string drawList, int width, int height, string imageUrl)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osDrawImage: permission denied");
                return String.Empty;
            }

           m_host.AddScriptLPS(1);
           drawList +="Image " +width + "," + height+ ","+ imageUrl +"; " ;
           return drawList;
        }

        public void osSetStateEvents(int events)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osSetStateEvents: permission denied");
                return;
            }

            m_host.SetScriptEvents(m_itemID, events);
        }

        public void osSetRegionWaterHeight(double height)
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osSetRegionWaterHeight: permission denied");
                return;
            }

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
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osList2Double: permission denied");
                return 0.0;
            }

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
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osSetParcelMediaURL: permission denied");
                return;
            }

            m_host.AddScriptLPS(1);
            LLUUID landowner = World.GetLandOwner(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y);

            if (landowner == LLUUID.Zero)
            {
                return;
            }

            if (landowner != m_host.ObjectOwner)
            {
                return;
            }

            World.SetLandMediaURL(m_host.AbsolutePosition.X, m_host.AbsolutePosition.Y, url);
        }

        public Scene World
        {
            get { return m_ScriptEngine.World; }
        }

        internal void OSSLError(string msg)
        {
            throw new Exception("OSSL Runtime Error: " + msg);
        }

        public string osGetScriptEngineName()
        {
            if (!m_ScriptEngine.Config.GetBoolean("AllowOSFunctions", false))
            {
                OSSLError("osGetScriptEngineName: permission denied");
                return "";
            }

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
            World.ParcelMediaSetTime((float)time);
        }
    }
}
