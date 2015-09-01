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

using OpenMetaverse;

namespace OpenSim.Framework
{
    public enum ObjectChangeType : uint
    {
        // bits definitions
        Position = 0x01,
        Rotation = 0x02,
        Scale   = 0x04,
        Group = 0x08,
        UniformScale = 0x10,

        // macros from above
        // single prim
        primP = 0x01,
        primR = 0x02,
        primPR = 0x03,
        primS = 0x04,
        primPS = 0x05,
        primRS = 0x06,
        primPSR = 0x07,

        primUS = 0x14,
        primPUS = 0x15,
        primRUS = 0x16,
        primPUSR = 0x17,

        // group
        groupP = 0x09,
        groupR = 0x0A,
        groupPR = 0x0B,
        groupS = 0x0C,
        groupPS = 0x0D,
        groupRS = 0x0E,
        groupPSR = 0x0F,

        groupUS = 0x1C,
        groupPUS = 0x1D,
        groupRUS = 0x1E,
        groupPUSR = 0x1F,

        PRSmask = 0x07
    }

    public struct ObjectChangeData
    {
        public Quaternion rotation;
        public Vector3 position;
        public Vector3 scale;
        public ObjectChangeType change;
    }
}
