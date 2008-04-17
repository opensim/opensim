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
using System.Collections.Generic;
using System.Text;

using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface ILandChannel
    {
        bool allowedForcefulBans { get; set; }
        void IncomingLandObjectsFromStorage(List<LandData> data);
        void IncomingLandObjectFromStorage(LandData data);

        void NoLandDataFromStorage();
        ILandObject getLandObject(int x, int y);
        ILandObject getLandObject(float x, float y);
        void setPrimsTainted();
        bool isLandPrimCountTainted();
        void sendLandUpdate(ScenePresence avatar, bool force);
        void sendLandUpdate(ScenePresence avatar);
        void resetAllLandPrimCounts();
        void addPrimToLandPrimCounts(SceneObjectGroup obj);
        void removePrimFromLandPrimCounts(SceneObjectGroup obj);
        void finalizeLandPrimCountUpdate();
        void updateLandPrimCounts();
        void performParcelPrimCountUpdate();
        void updateLandObject(int local_id, LandData newData);

        void sendParcelOverlay(IClientAPI remote_client);
        void handleParcelPropertiesRequest(int start_x, int start_y, int end_x, int end_y, int sequence_id, bool snap_selection, IClientAPI remote_client);
        void handleParcelPropertiesUpdateRequest(ParcelPropertiesUpdatePacket packet, IClientAPI remote_client);
        void handleParcelDivideRequest(int west, int south, int east, int north, IClientAPI remote_client);
        void handleParcelJoinRequest(int west, int south, int east, int north, IClientAPI remote_client);
        void handleParcelSelectObjectsRequest(int local_id, int request_type, IClientAPI remote_client);
        void handleParcelObjectOwnersRequest(int local_id, IClientAPI remote_client);

        void resetSimLandObjects();
        List<ILandObject> parcelsNearPoint(LLVector3 position);
        void sendYouAreBannedNotice(ScenePresence avatar);
        void handleAvatarChangingParcel(ScenePresence avatar, int localLandID, LLUUID regionID);
        void sendOutNearestBanLine(IClientAPI avatar);
        void handleSignificantClientMovement(IClientAPI remote_client);
        void handleAnyClientMovement(ScenePresence avatar);
        void handleParcelAccessRequest(LLUUID agentID, LLUUID sessionID, uint flags, int sequenceID, int landLocalID, IClientAPI remote_client);
        void handleParcelAccessUpdateRequest(LLUUID agentID, LLUUID sessionID, uint flags, int landLocalID, List<ParcelManager.ParcelAccessEntry> entries, IClientAPI remote_client);

    }
}
