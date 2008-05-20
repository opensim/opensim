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

namespace OpenSim.Framework
{
    #region Args Classes
    public class ICA2_ConnectionArgs : EventArgs
    {

    }

    public class ICA2_DisconnectionArgs : EventArgs
    {
        public bool Forced;

        // Static Constructor
        // Allows us to recycle these classes later more easily from a pool.
        public static ICA2_DisconnectionArgs Create(bool forced)
        {
            ICA2_DisconnectionArgs tmp = new ICA2_DisconnectionArgs();
            tmp.Forced = forced;

            return tmp;
        }
    }

    public class ICA2_PingArgs : EventArgs
    {
    }

    public class ICA2_AvatarAppearanceArgs : EventArgs
    {
    }

    public class ICA2_TerraformArgs : EventArgs
    {
        public double XMin;
        public double XMax;
        public double YMin;
        public double YMax;
        public Guid Action;
        public double Strength; // 0 .. 1
        public double Radius;
    }
    #endregion

    public delegate void ICA2_OnTerraformDelegate(IClientAPI2 sender, ICA2_TerraformArgs e);

    public interface IClientAPI2
    {
        // Connect / Disconnect
        void Connect(ICA2_ConnectionArgs e);
        void Disconnect(ICA2_DisconnectionArgs e);
        void Ping(ICA2_PingArgs e);

        void SendAvatarAppearance(ICA2_AvatarAppearanceArgs e);

        event ICA2_OnTerraformDelegate OnTerraform;
    }
}
