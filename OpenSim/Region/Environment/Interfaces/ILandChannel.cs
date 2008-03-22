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
