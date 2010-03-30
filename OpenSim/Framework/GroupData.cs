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

namespace OpenSim.Framework
{
    public class GroupRecord
    {
        public UUID GroupID;
        public string GroupName;
        public bool AllowPublish = true;
        public bool MaturePublish = true;
        public string Charter;
        public UUID FounderID = UUID.Zero;
        public UUID GroupPicture = UUID.Zero;
        public int MembershipFee = 0;
        public bool OpenEnrollment = true;
        public UUID OwnerRoleID = UUID.Zero;
        public bool ShowInList = false;
    }

    public class GroupMembershipData
    {
        // Group base data
        public UUID GroupID;
        public string GroupName;
        public bool AllowPublish = true;
        public bool MaturePublish = true;
        public string Charter;
        public UUID FounderID = UUID.Zero;
        public UUID GroupPicture = UUID.Zero;
        public int MembershipFee = 0;
        public bool OpenEnrollment = true;
        public bool ShowInList = true;

        // Per user data
        public bool AcceptNotices = true;
        public int Contribution = 0;
        public ulong GroupPowers = 0;
        public bool Active = false;
        public UUID ActiveRole = UUID.Zero;
        public bool ListInProfile = false;
        public string GroupTitle;
    }

    public struct GroupTitlesData
    {
        public string Name;
        public UUID UUID;
        public bool Selected;
    }

    public struct GroupProfileData
    {
        public UUID GroupID;
        public string Name;
        public string Charter;
        public bool ShowInList;
        public string MemberTitle;
        public ulong PowersMask;
        public UUID InsigniaID;
        public UUID FounderID;
        public int MembershipFee;
        public bool OpenEnrollment;
        public int Money;
        public int GroupMembershipCount;
        public int GroupRolesCount;
        public bool AllowPublish;
        public bool MaturePublish;
        public UUID OwnerRole;
    }

    public struct GroupMembersData
    {
        public UUID AgentID;
        public int Contribution;
        public string OnlineStatus;
        public ulong AgentPowers;
        public string Title;
        public bool IsOwner;
        public bool ListInProfile;
        public bool AcceptNotices;
    }

    public struct GroupRolesData
    {
        public UUID RoleID;
        public string Name;
        public string Title;
        public string Description;
        public ulong Powers;
        public int Members;
    }

    public struct GroupRoleMembersData
    {
        public UUID RoleID;
        public UUID MemberID;
    }

    public struct GroupNoticeData
    {
        public UUID NoticeID;
        public uint Timestamp;
        public string FromName;
        public string Subject;
        public bool HasAttachment;
        public byte AssetType;
    }

    public struct GroupVoteHistory
    {
        public string VoteID;
        public string VoteInitiator;
        public string Majority;
        public string Quorum;
        public string TerseDateID;
        public string StartDateTime;
        public string EndDateTime;
        public string VoteType;
        public string VoteResult;
        public string ProposalText;
    }

    public struct GroupActiveProposals
    {
        public string VoteID;
        public string VoteInitiator;
        public string Majority;
        public string Quorum;
        public string TerseDateID;
        public string StartDateTime;
        public string EndDateTime;
        public string ProposalText;
    }
}
