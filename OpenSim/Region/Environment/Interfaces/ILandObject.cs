using System;
using System.Collections.Generic;
using System.Text;

using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Region.Environment.Scenes;

using OpenSim.Framework;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface ILandObject
    {

        LandData landData { get; set; }
        bool[,] landBitmap { get; set; }
        LLUUID regionUUID { get; }
        bool containsPoint(int x, int y);
        ILandObject Copy();
        

        void sendLandUpdateToAvatarsOverMe();

        void sendLandProperties(int sequence_id, bool snap_selection, int request_result, IClientAPI remote_client);
        void updateLandProperties(ParcelPropertiesUpdatePacket packet, IClientAPI remote_client);
        bool isEitherBannedOrRestricted(LLUUID avatar);
        bool isBannedFromLand(LLUUID avatar);
        bool isRestrictedFromLand(LLUUID avatar);
        void sendLandUpdateToClient(IClientAPI remote_client);
        ParcelAccessListReplyPacket.ListBlock[] createAccessListArrayByFlag(ParcelManager.AccessList flag);
        void sendAccessList(LLUUID agentID, LLUUID sessionID, uint flags, int sequenceID, IClientAPI remote_client);
        void updateAccessList(uint flags, List<ParcelManager.ParcelAccessEntry> entries, IClientAPI remote_client);
        void updateLandBitmapByteArray();
        void setLandBitmapFromByteArray();
        bool[,] getLandBitmap();
        void forceUpdateLandInfo();
        void setLandBitmap(bool[,] bitmap);

        bool[,] basicFullRegionLandBitmap();
        bool[,] getSquareLandBitmap(int start_x, int start_y, int end_x, int end_y);
        bool[,] modifyLandBitmapSquare(bool[,] land_bitmap, int start_x, int start_y, int end_x, int end_y, bool set_value);
        bool[,] mergeLandBitmaps(bool[,] bitmap_base, bool[,] bitmap_add);
        void sendForceObjectSelect(int local_id, int request_type, IClientAPI remote_client);
        void sendLandObjectOwners(IClientAPI remote_client);
        void returnObject(SceneObjectGroup obj);
        void returnLandObjects(int type, LLUUID owner);
        void resetLandPrimCounts();
        void addPrimToCount(SceneObjectGroup obj);
        void removePrimFromCount(SceneObjectGroup obj);


    }
}
