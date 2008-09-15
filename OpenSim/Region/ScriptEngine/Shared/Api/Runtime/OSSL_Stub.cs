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
using System.Threading;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;
using integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using key = System.String;
using vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;
using rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;

namespace OpenSim.Region.ScriptEngine.Shared.ScriptBase
{
    public partial class ScriptBaseClass : MarshalByRefObject
    {
        public IOSSL_Api m_OSSL_Functions;

        public void ApiTypeOSSL(IScriptApi api)
        {
            if (!(api is IOSSL_Api))
                return;

            m_OSSL_Functions = (IOSSL_Api)api;
        }

        public void osSetRegionWaterHeight(double height)
        {
            m_OSSL_Functions.osSetRegionWaterHeight(height);
        }

        public double osList2Double(LSL_Types.list src, int index)
        {
            return m_OSSL_Functions.osList2Double(src, index);
        }

        public string osSetDynamicTextureURL(string dynamicID, string contentType, string url, string extraParams,
                                             int timer)
        {
            return m_OSSL_Functions.osSetDynamicTextureURL(dynamicID, contentType, url, extraParams, timer);
        }

        public string osSetDynamicTextureData(string dynamicID, string contentType, string data, string extraParams,
                                             int timer)
        {
            return m_OSSL_Functions.osSetDynamicTextureData(dynamicID, contentType, data, extraParams, timer);
        }

        public string osSetDynamicTextureURLBlend(string dynamicID, string contentType, string url, string extraParams,
                                           int timer, int alpha)
        {
            return m_OSSL_Functions.osSetDynamicTextureURLBlend(dynamicID, contentType, url, extraParams, timer, alpha);
        }

        public string osSetDynamicTextureDataBlend(string dynamicID, string contentType, string data, string extraParams,
                                             int timer, int alpha)
        {
            return m_OSSL_Functions.osSetDynamicTextureDataBlend(dynamicID, contentType, data, extraParams, timer, alpha);
        }

        public double osTerrainGetHeight(int x, int y)
        {
            return m_OSSL_Functions.osTerrainGetHeight(x, y);
        }

        public int osTerrainSetHeight(int x, int y, double val)
        {
            return m_OSSL_Functions.osTerrainSetHeight(x, y, val);
        }

        public int osRegionRestart(double seconds)
        {
            return m_OSSL_Functions.osRegionRestart(seconds);
        }

        public void osRegionNotice(string msg)
        {
            m_OSSL_Functions.osRegionNotice(msg);
        }

        public bool osConsoleCommand(string Command)
        {
            return m_OSSL_Functions.osConsoleCommand(Command);
        }

        public void osSetParcelMediaURL(string url)
        {
            m_OSSL_Functions.osSetParcelMediaURL(url);
        }

        public void osSetPrimFloatOnWater(int floatYN)
        {
            m_OSSL_Functions.osSetPrimFloatOnWater(floatYN);
        }

        // Teleport Functions

        public void osTeleportAgent(string agent, string regionName, vector position, vector lookat)
        {
            m_OSSL_Functions.osTeleportAgent(agent, regionName, position, lookat);
        }

        public void osTeleportAgent(string agent, vector position, vector lookat)
        {
            m_OSSL_Functions.osTeleportAgent(agent, position, lookat);
        }

        // Animation Functions

        public void osAvatarPlayAnimation(string avatar, string animation)
        {
            m_OSSL_Functions.osAvatarPlayAnimation(avatar, animation);
        }

        public void osAvatarStopAnimation(string avatar, string animation)
        {
            m_OSSL_Functions.osAvatarStopAnimation(avatar, animation);
        }


        //Texture Draw functions

        public string osMovePen(string drawList, int x, int y)
        {
            return m_OSSL_Functions.osMovePen(drawList, x, y);
        }

        public string osDrawLine(string drawList, int startX, int startY, int endX, int endY)
        {
            return m_OSSL_Functions.osDrawLine(drawList, startX, startY, endX, endY);
        }

        public string osDrawLine(string drawList, int endX, int endY)
        {
            return m_OSSL_Functions.osDrawLine(drawList, endX, endY);
        }

        public string osDrawText(string drawList, string text)
        {
            return m_OSSL_Functions.osDrawText(drawList, text);
        }

        public string osDrawEllipse(string drawList, int width, int height)
        {
            return m_OSSL_Functions.osDrawEllipse(drawList, width, height);
        }

        public string osDrawRectangle(string drawList, int width, int height)
        {
            return m_OSSL_Functions.osDrawRectangle(drawList, width, height);
        }

        public string osDrawFilledRectangle(string drawList, int width, int height)
        {
            return m_OSSL_Functions.osDrawFilledRectangle(drawList, width, height);
        }

        public string osSetFontSize(string drawList, int fontSize)
        {
            return m_OSSL_Functions.osSetFontSize(drawList, fontSize);
        }

        public string osSetPenSize(string drawList, int penSize)
        {
            return m_OSSL_Functions.osSetPenSize(drawList, penSize);
        }

        public string osSetPenColour(string drawList, string colour)
        {
            return m_OSSL_Functions.osSetPenColour(drawList, colour);
        }

        public string osDrawImage(string drawList, int width, int height, string imageUrl)
        {
            return m_OSSL_Functions.osDrawImage(drawList, width, height, imageUrl);
        }
        public void osSetStateEvents(int events)
        {
            m_OSSL_Functions.osSetStateEvents(events);
        }

        public string osGetScriptEngineName()
        {
            return m_OSSL_Functions.osGetScriptEngineName();
        }


        //for testing purposes only
        public void osSetParcelMediaTime(double time)
        {
            m_OSSL_Functions.osSetParcelMediaTime(time);
        }
    }
}
