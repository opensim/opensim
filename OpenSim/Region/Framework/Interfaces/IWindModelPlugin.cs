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

using Nini.Config;
using OpenSim.Framework;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

using Mono.Addins;

namespace OpenSim.Region.Framework.Interfaces
{
    [TypeExtensionPoint(Path = "/OpenSim/WindModule", NodeName = "WindModel")]
    public interface IWindModelPlugin : IPlugin
    {
        /// <summary>
        /// Brief description of this plugin's wind model
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Provides access to the wind configuration, if any.
        /// </summary>
        void WindConfig(Scene scene, IConfig windConfig);

        /// <summary>
        /// Update wind.
        /// </summary>
        void WindUpdate(uint frame);

        /// <summary>
        /// Returns the wind vector at the given local region coordinates.
        /// </summary>
        Vector3 WindSpeed(float x, float y, float z);

        /// <summary>
        /// Generate a 16 x 16 Vector2 array of wind speeds for LL* based viewers
        /// </summary>
        /// <returns>Must return a Vector2[256]</returns>
        Vector2[] WindLLClientArray();

        /// <summary>
        /// Retrieve a list of parameter/description pairs.
        /// </summary>
        /// <returns></returns>
        Dictionary<string, string> WindParams();

        /// <summary>
        /// Set the specified parameter
        /// </summary>
        void WindParamSet(string param, float value);

        /// <summary>
        /// Get the specified parameter
        /// </summary>
        float WindParamGet(string param);

    }
}
