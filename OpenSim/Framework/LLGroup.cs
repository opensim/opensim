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
using System.Collections;
using OpenMetaverse;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework
{
    public class GroupData
    {
        public string ActiveGroupTitle;
        public UUID GroupID;
        public List<UUID> GroupMembers;
        public string groupName;
        public uint groupPowers = (uint)(GroupPowers.LandAllowLandmark | GroupPowers.LandAllowSetHome);
        public List<string> GroupTitles;
        public bool AcceptNotices = true;
        public bool AllowPublish = true;
        public string Charter = "Cool Group Yeah!";
        public int contribution = 0;
        public UUID FounderID = UUID.Zero;
        public int groupMembershipCost = 0;
        public int groupRollsCount = 1;
        public UUID GroupPicture = UUID.Zero;
        public bool MaturePublish = true;
        public int MembershipFee = 0;
        public bool OpenEnrollment = true;
        public bool ShowInList = true;

        public GroupData()
        {
            GroupTitles = new List<string>();
            GroupMembers = new List<UUID>();
        }

        public GroupPowers ActiveGroupPowers
        {
            set { groupPowers = (uint)value; }
            get { return (GroupPowers)groupPowers; }
        }
    }

    public class GroupList
    {
        public List<UUID> m_GroupList;

        public GroupList()
        {
            m_GroupList = new List<UUID>();
        }
    }
}
