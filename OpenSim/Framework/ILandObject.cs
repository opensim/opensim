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
        void UpdateLandProperties(LandUpdateArgs args, IClientAPI remote_client);
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
        /// specify start_x = 0, start_y = 0, end_x = 252 (or anything up to 255), end_y = 256.
        /// 
        /// At the moment, the smallest parcel of land is 4m x 4m, so if the 
        /// region is 256 x 256m (the SL size), the bitmap returned will start at (0,0) and end at (63,63).
        /// </remarks>
        /// <param name="start_x"></param>
        /// <param name="start_y"></param>
        /// <param name="end_x"></param>
        /// <param name="end_y"></param>
        /// <returns>The bitmap created.</returns>
        bool[,] GetSquareLandBitmap(int start_x, int start_y, int end_x, int end_y);
        
        bool[,] ModifyLandBitmapSquare(bool[,] land_bitmap, int start_x, int start_y, int end_x, int end_y, bool set_value);
        bool[,] MergeLandBitmaps(bool[,] bitmap_base, bool[,] bitmap_add);
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
