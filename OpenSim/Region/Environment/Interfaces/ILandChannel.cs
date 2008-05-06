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

using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface ILandChannel
    {
        bool AllowedForcefulBans { get; set; }
        void IncomingLandObjectsFromStorage(List<LandData> data);
        void IncomingLandObjectFromStorage(LandData data);

        void NoLandDataFromStorage();
        ILandObject GetLandObject(int x, int y);
        ILandObject GetLandObject(float x, float y);
        void SetPrimsTainted();
        bool IsLandPrimCountTainted();
        void SendLandUpdate(ScenePresence avatar, bool force);
        void SendLandUpdate(ScenePresence avatar);
        void ResetAllLandPrimCounts();
        void AddPrimToLandPrimCounts(SceneObjectGroup obj);
        void RemovePrimFromLandPrimCounts(SceneObjectGroup obj);
        void FinalizeLandPrimCountUpdate();
        void UpdateLandPrimCounts();
        void PerformParcelPrimCountUpdate();
        void UpdateLandObject(int local_id, LandData newData);

        void SendParcelOverlay(IClientAPI remote_client);

        void ResetSimLandObjects();
        List<ILandObject> ParcelsNearPoint(LLVector3 position);
        void SendYouAreBannedNotice(ScenePresence avatar);
        void handleAvatarChangingParcel(ScenePresence avatar, int localLandID, LLUUID regionID);
        void SendOutNearestBanLine(IClientAPI avatar);
    }
}
