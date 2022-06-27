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
using OpenMetaverse;
using System.Collections.Generic;


namespace OpenSim.Framework
{
    public class UserClassifiedAdd
    {
        public UUID ClassifiedId;
        public UUID CreatorId;
        public int CreationDate;
        public int ExpirationDate;
        public int Category;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public UUID ParcelId;
        public int ParentEstate = 0;
        public UUID SnapshotId;
        public string SimName = string.Empty;
        public string GlobalPos = "<0,0,0>";
        public string ParcelName = string.Empty;
        public byte Flags = 0;
        public int Price = 0;
    }

    public class UserProfileProperties
    {
        public UUID UserId;
        public UUID PartnerId;
        public bool PublishProfile;
        public bool PublishMature;
        public string WebUrl = string.Empty;
        public int WantToMask = 0;
        public string WantToText = string.Empty;
        public int SkillsMask = 0;
        public string SkillsText = string.Empty;
        public string Language = string.Empty;
        public UUID ImageId;
        public string AboutText = string.Empty;
        public UUID FirstLifeImageId;
        public string FirstLifeText = string.Empty;
    }

    public class UserProfilePick
    {
        public UUID PickId;
        public UUID CreatorId;
        public bool TopPick;
        public string Name = string.Empty;
        public string OriginalName = string.Empty;
        public string Desc = string.Empty;
        public UUID ParcelId;
        public UUID SnapshotId;
        public string ParcelName = string.Empty;
        public string SimName = string.Empty;
        public string GlobalPos = "<0,0,0>";
        public string Gatekeeper = string.Empty;
        public int SortOrder = 0;
        public bool Enabled = false;
    }

    public class UserProfileNotes
    {
        public UUID UserId;
        public UUID TargetId;
        public string Notes;
    }

    public class UserPreferences
    {
        public UUID UserId;
        public bool IMViaEmail;
        public bool Visible;
        public string EMail = string.Empty;
    }

    public class UserAccountProperties
    {
        public string EmailAddress = string.Empty;
        public string Firstname = string.Empty;
        public string LastName = string.Empty;
        public string Password = string.Empty;
        public string UserId = string.Empty;
    }

    public class UserAccountAuth
    {
        public string UserId = UUID.Zero.ToString();
        public string Password = string.Empty;
    }

    public class UserAppData
    {
        public string TagId = string.Empty;
        public string DataKey = string.Empty;
        public string UserId = UUID.Zero.ToString();
        public string DataVal = string.Empty;
    }

    public class UserProfileCacheEntry
    {
        public Dictionary<UUID, string> picksList;
        public Dictionary<UUID, UserProfilePick> picks;
        public Dictionary<UUID, string> classifiedsLists;
        public Dictionary<UUID, UserClassifiedAdd> classifieds;
        public UserProfileProperties props;
        public GroupMembershipData[] avatarGroups;
        public string born;
        public byte[] membershipType;
        public uint flags;
        public HashSet<IClientAPI> ClientsWaitingProps;
    }
}

