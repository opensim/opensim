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

using System;
using System.Collections.Generic;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class LandData
    {
        private Vector3 _AABBMax = new Vector3();
        private Vector3 _AABBMin = new Vector3();
        private int _area = 0;
        private uint _auctionID = 0; //Unemplemented. If set to 0, not being auctioned
        private UUID _authBuyerID = UUID.Zero; //Unemplemented. Authorized Buyer's UUID
        private Parcel.ParcelCategory _category = new Parcel.ParcelCategory(); //Unemplemented. Parcel's chosen category
        private int _claimDate = 0;
        private int _claimPrice = 0; //Unemplemented
        private UUID _globalID = UUID.Zero;
        private UUID _groupID = UUID.Zero;
        private int _groupPrims = 0;
        private bool _isGroupOwned = false;
        private byte[] _bitmap = new byte[512];
        private string _description = String.Empty;


        private uint _flags = (uint) Parcel.ParcelFlags.AllowFly | (uint) Parcel.ParcelFlags.AllowLandmark |
                                (uint) Parcel.ParcelFlags.AllowAPrimitiveEntry |
                                (uint) Parcel.ParcelFlags.AllowDeedToGroup | (uint) Parcel.ParcelFlags.AllowTerraform |
                                (uint) Parcel.ParcelFlags.CreateObjects | (uint) Parcel.ParcelFlags.AllowOtherScripts |
                                (uint) Parcel.ParcelFlags.SoundLocal;

        private byte _landingType = 0;
        private string _name = "Your Parcel";
        private Parcel.ParcelStatus _status = Parcel.ParcelStatus.Leased;
        private int _localID = 0;
        private byte _mediaAutoScale = 0;
        private UUID _mediaID = UUID.Zero;

        private string _mediaURL = String.Empty;
        private string _musicURL = String.Empty;
        private int _otherPrims = 0;
        private UUID _ownerID = UUID.Zero;
        private int _ownerPrims = 0;
        private List<ParcelManager.ParcelAccessEntry> _parcelAccessList = new List<ParcelManager.ParcelAccessEntry>();
        private float _passHours = 0;
        private int _passPrice = 0;
        private int _salePrice = 0; //Unemeplemented. Parcels price.
        private int _selectedPrims = 0;
        private int _simwideArea = 0;
        private int _simwidePrims = 0;
        private UUID _snapshotID = UUID.Zero;
        private Vector3 _userLocation = new Vector3();
        private Vector3 _userLookAt = new Vector3();
        private int _dwell = 0;
        private int _otherCleanTime = 0;

        public Vector3 AABBMax {
            get {
                return _AABBMax;
            }
            set {
                _AABBMax = value;
            }
        }

        public Vector3 AABBMin {
            get {
                return _AABBMin;
            }
            set {
                _AABBMin = value;
            }
        }

        public int Area {
            get {
                return _area;
            }
            set {
                _area = value;
            }
        }

        public uint AuctionID {
            get {
                return _auctionID;
            }
            set {
                _auctionID = value;
            }
        }

        public UUID AuthBuyerID {
            get {
                return _authBuyerID;
            }
            set {
                _authBuyerID = value;
            }
        }

        public Parcel.ParcelCategory Category {
            get {
                return _category;
            }
            set {
                _category = value;
            }
        }

        public int ClaimDate {
            get {
                return _claimDate;
            }
            set {
                _claimDate = value;
            }
        }

        public int ClaimPrice {
            get {
                return _claimPrice;
            }
            set {
                _claimPrice = value;
            }
        }

        public UUID GlobalID {
            get {
                return _globalID;
            }
            set {
                _globalID = value;
            }
        }

        public UUID GroupID {
            get {
                return _groupID;
            }
            set {
                _groupID = value;
            }
        }

        public int GroupPrims {
            get {
                return _groupPrims;
            }
            set {
                _groupPrims = value;
            }
        }

        public bool IsGroupOwned {
            get {
                return _isGroupOwned;
            }
            set {
                _isGroupOwned = value;
            }
        }

        public byte[] Bitmap {
            get {
                return _bitmap;
            }
            set {
                _bitmap = value;
            }
        }

        public string Description {
            get {
                return _description;
            }
            set {
                _description = value;
            }
        }

        public uint Flags {
            get {
                return _flags;
            }
            set {
                _flags = value;
            }
        }

        public byte LandingType {
            get {
                return _landingType;
            }
            set {
                _landingType = value;
            }
        }

        public string Name {
            get {
                return _name;
            }
            set {
                _name = value;
            }
        }

        public Parcel.ParcelStatus Status {
            get {
                return _status;
            }
            set {
                _status = value;
            }
        }

        public int LocalID {
            get {
                return _localID;
            }
            set {
                _localID = value;
            }
        }

        public byte MediaAutoScale {
            get {
                return _mediaAutoScale;
            }
            set {
                _mediaAutoScale = value;
            }
        }

        public UUID MediaID {
            get {
                return _mediaID;
            }
            set {
                _mediaID = value;
            }
        }

        public string MediaURL {
            get {
                return _mediaURL;
            }
            set {
                _mediaURL = value;
            }
        }

        public string MusicURL {
            get {
                return _musicURL;
            }
            set {
                _musicURL = value;
            }
        }

        public int OtherPrims {
            get {
                return _otherPrims;
            }
            set {
                _otherPrims = value;
            }
        }

        public UUID OwnerID {
            get {
                return _ownerID;
            }
            set {
                _ownerID = value;
            }
        }

        public int OwnerPrims {
            get {
                return _ownerPrims;
            }
            set {
                _ownerPrims = value;
            }
        }

        public List<ParcelManager.ParcelAccessEntry> ParcelAccessList {
            get {
                return _parcelAccessList;
            }
            set {
                _parcelAccessList = value;
            }
        }

        public float PassHours {
            get {
                return _passHours;
            }
            set {
                _passHours = value;
            }
        }

        public int PassPrice {
            get {
                return _passPrice;
            }
            set {
                _passPrice = value;
            }
        }

        public int SalePrice {
            get {
                return _salePrice;
            }
            set {
                _salePrice = value;
            }
        }

        public int SelectedPrims {
            get {
                return _selectedPrims;
            }
            set {
                _selectedPrims = value;
            }
        }

        public int SimwideArea {
            get {
                return _simwideArea;
            }
            set {
                _simwideArea = value;
            }
        }

        public int SimwidePrims {
            get {
                return _simwidePrims;
            }
            set {
                _simwidePrims = value;
            }
        }

        public UUID SnapshotID {
            get {
                return _snapshotID;
            }
            set {
                _snapshotID = value;
            }
        }

        public Vector3 UserLocation {
            get {
                return _userLocation;
            }
            set {
                _userLocation = value;
            }
        }

        public Vector3 UserLookAt {
            get {
                return _userLookAt;
            }
            set {
                _userLookAt = value;
            }
        }

        public int Dwell {
            get {
                return _dwell;
            }
            set {
                _dwell = value;
            }
        }

        public int OtherCleanTime {
            get {
                return _otherCleanTime;
            }
            set {
                _otherCleanTime = value;
            }
        }

        public LandData()
        {
            _globalID = UUID.Random();
        }

        public LandData Copy()
        {
            LandData landData = new LandData();

            landData._AABBMax = _AABBMax;
            landData._AABBMin = _AABBMin;
            landData._area = _area;
            landData._auctionID = _auctionID;
            landData._authBuyerID = _authBuyerID;
            landData._category = _category;
            landData._claimDate = _claimDate;
            landData._claimPrice = _claimPrice;
            landData._globalID = _globalID;
            landData._groupID = _groupID;
            landData._groupPrims = _groupPrims;
            landData._otherPrims = _otherPrims;
            landData._ownerPrims = _ownerPrims;
            landData._selectedPrims = _selectedPrims;
            landData._isGroupOwned = _isGroupOwned;
            landData._localID = _localID;
            landData._landingType = _landingType;
            landData._mediaAutoScale = _mediaAutoScale;
            landData._mediaID = _mediaID;
            landData._mediaURL = _mediaURL;
            landData._musicURL = _musicURL;
            landData._ownerID = _ownerID;
            landData._bitmap = (byte[]) _bitmap.Clone();
            landData._description = _description;
            landData._flags = _flags;
            landData._name = _name;
            landData._status = _status;
            landData._passHours = _passHours;
            landData._passPrice = _passPrice;
            landData._salePrice = _salePrice;
            landData._snapshotID = _snapshotID;
            landData._userLocation = _userLocation;
            landData._userLookAt = _userLookAt;
            landData._otherCleanTime = _otherCleanTime;
            landData._dwell = _dwell;

            landData._parcelAccessList.Clear();
            foreach (ParcelManager.ParcelAccessEntry entry in _parcelAccessList)
            {
                ParcelManager.ParcelAccessEntry newEntry = new ParcelManager.ParcelAccessEntry();
                newEntry.AgentID = entry.AgentID;
                newEntry.Flags = entry.Flags;
                newEntry.Time = entry.Time;

                landData._parcelAccessList.Add(newEntry);
            }

            return landData;
        }
    }
}
