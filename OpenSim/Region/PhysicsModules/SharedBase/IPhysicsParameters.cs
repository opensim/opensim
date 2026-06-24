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
using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Region.PhysicsModules.SharedBase
{
    public struct PhysParameterEntry
    {
        // flags to say to apply to all or no instances (I wish one could put consts into interfaces)
        public const uint APPLY_TO_ALL = 0xfffffff3;
        public const uint APPLY_TO_NONE = 0xfffffff4;

        // values that denote true and false values
        public const float NUMERIC_TRUE = 1f;
        public const float NUMERIC_FALSE = 0f;

        public string name;
        public string desc;

        public PhysParameterEntry(string n, string d)
        {
            name = n;
            desc = d;
        }
    }

    // Interface for a physics scene that implements the runtime setting and getting of physics parameters
    public interface IPhysicsParameters
    {
        // Get the list of parameters this physics engine supports
        PhysParameterEntry[] GetParameterList();

        // Set parameter on a specific or all instances.
        // Return 'false' if not able to set the parameter.
        bool SetPhysicsParameter(string parm, string value, uint localID);

        // Get parameter.
        // Return 'false' if not able to get the parameter.
        bool GetPhysicsParameter(string parm, out string value);

        // Get parameter from a particular object
        // TODO:
        // bool GetPhysicsParameter(string parm, out string value, uint localID);
    }
}
