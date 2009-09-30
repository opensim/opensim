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

using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface ILandChannel
    {
        List<ILandObject> ParcelsNearPoint(Vector3 position);
        List<ILandObject> AllParcels();
             
        /// <summary>
        /// Get the land object at the specified point
        /// </summary>
        /// <param name="x">Value between 0 - 256 on the x axis of the point</param>
        /// <param name="y">Value between 0 - 256 on the y axis of the point</param>
        /// <returns>Land object at the point supplied</returns>
        ILandObject GetLandObject(int x, int y);

        ILandObject GetLandObject(int localID);

        /// <summary>
        /// Get the land object at the specified point
        /// </summary>
        /// <param name="x">Value between 0 - 256 on the x axis of the point</param>
        /// <param name="y">Value between 0 - 256 on the y axis of the point</param>
        /// <returns>Land object at the point supplied</returns>
        ILandObject GetLandObject(float x, float y);
        
        bool IsLandPrimCountTainted();
        bool IsForcefulBansAllowed();
        void UpdateLandObject(int localID, LandData data);
        void ReturnObjectsInParcel(int localID, uint returnType, UUID[] agentIDs, UUID[] taskIDs, IClientAPI remoteClient);
        void setParcelObjectMaxOverride(overrideParcelMaxPrimCountDelegate overrideDel);
        void setSimulatorObjectMaxOverride(overrideSimulatorMaxPrimCountDelegate overrideDel);
        void SetParcelOtherCleanTime(IClientAPI remoteClient, int localID, int otherCleanTime);
    }
}
