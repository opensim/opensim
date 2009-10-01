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
using OpenSim.Framework.Console;
using Nini.Config;

namespace OpenSim.Framework
{
    public delegate void restart(RegionInfo thisRegion);

    //public delegate void regionup (RegionInfo thisRegion);

    public enum RegionStatus : int
    {
        Down = 0,
        Up = 1,
        Crashed = 2,
        Starting = 3,
        SlaveScene = 4
    };
            
    /// <value>
    /// Indicate what action to take on an object derez request
    /// </value>
    public enum DeRezAction : byte
    {
        SaveToExistingUserInventoryItem = 0,
        TakeCopy = 1,
        Take = 4,
        GodTakeCopy = 5,
        Delete = 6,
        Return = 9
    };

    public interface IScene
    {
        RegionInfo RegionInfo { get; }
        RegionStatus RegionStatus { get; set; }

        ClientManager ClientManager { get; }
        IConfigSource Config { get; }

        float TimeDilation { get; }

        event restart OnRestart;

        void AddNewClient(IClientAPI client);
        void RemoveClient(UUID agentID);
        void CloseAllAgents(uint circuitcode);

        void Restart(int seconds);
        //RegionInfo OtherRegionUp(RegionInfo thisRegion);

        string GetSimulatorVersion();

        /// <summary>
        /// Is the agent denoted by the given agentID a child presence in this scene?
        /// </summary>
        /// 
        /// Used by ClientView when a 'kick everyone' or 'estate message' occurs
        /// 
        /// <param name="avatarID">AvatarID to lookup</param>
        /// <returns>true if the presence is a child agent, false if the presence is a root exception</returns>
        /// <exception cref="System.NullReferenceException">
        /// Thrown if the agent does not exist.
        /// </exception>
        bool PresenceChildStatus(UUID agentId);

        T RequestModuleInterface<T>();
        T[] RequestModuleInterfaces<T>();

        void RegisterModuleInterface<M>(M mod);
        void StackModuleInterface<M>(M mod);

        void AddCommand(object module, string command, string shorthelp, string longhelp, CommandDelegate callback);
    }
}
