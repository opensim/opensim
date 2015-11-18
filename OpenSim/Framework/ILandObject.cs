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

namespace OpenSim.Framework
{
    public delegate int overrideParcelMaxPrimCountDelegate(ILandObject obj);
    public delegate int overrideSimulatorMaxPrimCountDelegate(ILandObject obj);

    public interface ILandObject
    {
        int GetParcelMaxPrimCount();
        int GetSimulatorMaxPrimCount();
        int GetPrimsFree();
        Dictionary<UUID, int> GetLandObjectOwners();

        LandData LandData { get; set; }
        bool[,] LandBitmap { get; set; }
        UUID RegionUUID { get; }
        
        /// <summary>
        /// Prim counts for this land object.
        /// </summary>
        IPrimCounts PrimCounts { get; set; }
        
        /// <summary>
        /// The start point for the land object.  This is the western-most point as one scans land working from 
        /// north to south.
        /// </summary>
        Vector3 StartPoint { get; }
        
        /// <summary>
        /// The end point for the land object.  This is the eastern-most point as one scans land working from 
        /// south to north.
        /// </summary>        
        Vector3 EndPoint { get; }
        
        bool ContainsPoint(int x, int y);
        
        ILandObject Copy();

        void SendLandUpdateToAvatarsOverMe();

        void SendLandProperties(int sequence_id, bool snap_selection, int request_result, IClientAPI remote_client);
        bool UpdateLandProperties(LandUpdateArgs args, IClientAPI remote_client, out bool snap_selection, out bool needOverlay);
        bool IsEitherBannedOrRestricted(UUID avatar);
        bool IsBannedFromLand(UUID avatar);
        bool CanBeOnThisLand(UUID avatar, float posHeight);
        bool IsRestrictedFromLand(UUID avatar);
        bool IsInLandAccessList(UUID avatar);
        void SendLandUpdateToClient(IClientAPI remote_client);
        void SendLandUpdateToClient(bool snap_selection, IClientAPI remote_client);
        List<LandAccessEntry> CreateAccessListArrayByFlag(AccessList flag);
        void SendAccessList(UUID agentID, UUID sessionID, uint flags, int sequenceID, IClientAPI remote_client);
        void UpdateAccessList(uint flags, UUID transactionID, int sequenceID, int sections, List<LandAccessEntry> entries, IClientAPI remote_client);
        void UpdateLandBitmapByteArray();
        void SetLandBitmapFromByteArray();
        bool[,] GetLandBitmap();
        void ForceUpdateLandInfo();
        void SetLandBitmap(bool[,] bitmap);

        /// <summary>
        /// Get a land bitmap that would cover an entire region.
        /// </summary>
        /// <returns>The bitmap created.</returns>
        bool[,] BasicFullRegionLandBitmap();
        
        /// <summary>
        /// Create a square land bitmap.
        /// </summary>
        /// <remarks>
        /// Land co-ordinates are zero indexed.  The inputs are treated as points.  So if you want to create a bitmap
        /// that covers an entire 256 x 256m region apart from a strip of land on the east, then you would need to 
        /// specify start_x = 0, start_y = 0, end_x = 252 (or anything up to 255), end_y = 255.
        /// 
        /// At the moment, the smallest parcel of land is 4m x 4m, so if the 
        /// region is 256 x 256m (the SL size), the bitmap returned will start at (0,0) and end at (63,63).
        /// The value of the set_value needs to be true to define an active parcel of the given size.
        /// </remarks>
        /// <param name="start_x"></param>
        /// <param name="start_y"></param>
        /// <param name="end_x"></param>
        /// <param name="end_y"></param>
        /// <param name="set_value"></param>
        /// <returns>The bitmap created.</returns>
        bool[,] GetSquareLandBitmap(int start_x, int start_y, int end_x, int end_y, bool set_value = true);
        
        bool[,] ModifyLandBitmapSquare(bool[,] land_bitmap, int start_x, int start_y, int end_x, int end_y, bool set_value);

        /// <summary>
        /// Merge two (same size) land bitmaps.
        /// </summary>
        /// <param name="bitmap_base"></param>
        /// <param name="bitmap_add"></param>
        /// <returns>The modified bitmap.</returns>
        bool[,] MergeLandBitmaps(bool[,] bitmap_base, bool[,] bitmap_add);

        /// <summary>
        /// Remap a land bitmap. Takes the supplied land bitmap and rotates it, crops it and finally offsets it into
        /// a final land bitmap of the target region size.
        /// </summary>
        /// <param name="bitmap_base">The original parcel bitmap</param>
        /// <param name="rotationDegrees"></param>
        /// <param name="displacement">&lt;x,y,?&gt;</param>
        /// <param name="boundingOrigin">&lt;x,y,?&gt;</param>
        /// <param name="boundingSize">&lt;x,y,?&gt;</param>
        /// <param name="regionSize">&lt;x,y,?&gt;</param>
        /// <param name="isEmptyNow">out: This is set if the resultant bitmap is now empty</param>
        /// <param name="AABBMin">out: parcel.AABBMin &lt;x,y,0&gt</param>
        /// <param name="AABBMax">out: parcel.AABBMax &lt;x,y,0&gt</param>
        /// <returns>New parcel bitmap</returns>
        bool[,] RemapLandBitmap(bool[,] bitmap_base, Vector2 displacement, float rotationDegrees, Vector2 boundingOrigin, Vector2 boundingSize, Vector2 regionSize, out bool isEmptyNow, out Vector3 AABBMin, out Vector3 AABBMax);

        /// <summary>
        /// Clears any parcel data in bitmap_base where there exists parcel data in bitmap_new. In other words the parcel data
        /// in bitmap_new takes over the space of the parcel data in bitmap_base.
        /// </summary>
        /// <param name="bitmap_base"></param>
        /// <param name="bitmap_new"></param>
        /// <param name="isEmptyNow">out: This is set if the resultant bitmap is now empty</param>
        /// <param name="AABBMin">out: parcel.AABBMin &lt;x,y,0&gt;</param>
        /// <param name="AABBMax">out: parcel.AABBMax &lt;x,y,0&gt</param>
        /// <returns>New parcel bitmap</returns>       
        bool[,] RemoveFromLandBitmap(bool[,] bitmap_base, bool[,] bitmap_new, out bool isEmptyNow, out Vector3 AABBMin, out Vector3 AABBMax);

        byte[] ConvertLandBitmapToBytes();
        bool[,] ConvertBytesToLandBitmap(bool overrideRegionSize = false);
        bool IsLandBitmapEmpty(bool[,] landBitmap);
        void DebugLandBitmap(bool[,] landBitmap);

        void SendForceObjectSelect(int local_id, int request_type, List<UUID> returnIDs, IClientAPI remote_client);
        void SendLandObjectOwners(IClientAPI remote_client);
        void ReturnLandObjects(uint type, UUID[] owners, UUID[] tasks, IClientAPI remote_client);
        void ResetOverMeRecord();
        void UpdateLandSold(UUID avatarID, UUID groupID, bool groupOwned, uint AuctionID, int claimprice, int area);

        void DeedToGroup(UUID groupID);

        void SetParcelObjectMaxOverride(overrideParcelMaxPrimCountDelegate overrideDel);
        void SetSimulatorObjectMaxOverride(overrideSimulatorMaxPrimCountDelegate overrideDel);

        /// <summary>
        /// Set the media url for this land parcel
        /// </summary>
        /// <param name="url"></param>
        void SetMediaUrl(string url);
        
        /// <summary>
        /// Set the music url for this land parcel
        /// </summary>
        /// <param name="url"></param>
        void SetMusicUrl(string url);

        /// <summary>
        /// Get the music url for this land parcel
        /// </summary>
        /// <returns>The music url.</returns>
        string GetMusicUrl();
    }
}
