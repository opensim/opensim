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
using System.Collections;
using OpenSim.Region.ScriptEngine.Interfaces;

using key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

namespace OpenSim.Region.ScriptEngine.Shared.Api.Interfaces
{
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

    public interface IOSSL_Api
    {
        void CheckThreatLevel(ThreatLevel level, string function);

        //OpenSim functions
        string osSetDynamicTextureURL(string dynamicID, string contentType, string url, string extraParams, int timer);
        string osSetDynamicTextureURLBlend(string dynamicID, string contentType, string url, string extraParams,
                                           int timer, int alpha);
        string osSetDynamicTextureData(string dynamicID, string contentType, string data, string extraParams, int timer);
        string osSetDynamicTextureDataBlend(string dynamicID, string contentType, string data, string extraParams,
                                            int timer, int alpha);
        double osTerrainGetHeight(int x, int y);
        int osTerrainSetHeight(int x, int y, double val);
        int osRegionRestart(double seconds);
        void osRegionNotice(string msg);
        bool osConsoleCommand(string Command);
        void osSetParcelMediaURL(string url);
        void osSetPrimFloatOnWater(int floatYN);

        // Teleport commands
        void osTeleportAgent(string agent, string regionName, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat);
        void osTeleportAgent(string agent, LSL_Types.Vector3 position, LSL_Types.Vector3 lookat);

        // Animation commands
        void osAvatarPlayAnimation(string avatar, string animation);
        void osAvatarStopAnimation(string avatar, string animation);

        //texture draw functions
        string osMovePen(string drawList, int x, int y);
        string osDrawLine(string drawList, int startX, int startY, int endX, int endY);
        string osDrawLine(string drawList, int endX, int endY);
        string osDrawText(string drawList, string text);
        string osDrawEllipse(string drawList, int width, int height);
        string osDrawRectangle(string drawList, int width, int height);
        string osDrawFilledRectangle(string drawList, int width, int height);
        string osSetFontSize(string drawList, int fontSize);
        string osSetPenSize(string drawList, int penSize);
        string osSetPenColour(string drawList, string colour);
        string osDrawImage(string drawList, int width, int height, string imageUrl);
        void osSetStateEvents(int events);

        double osList2Double(LSL_Types.list src, int index);
        void osSetRegionWaterHeight(double height);

        string osGetScriptEngineName();
        string osGetSimulatorVersion();
        void osSetParcelMediaTime(double time);
        Hashtable osParseJSON(string JSON);
        
        void osMessageObject(key objectUUID,string message);

	void osMakeNotecard(string notecardName, LSL_Types.list contents);
    }
}
