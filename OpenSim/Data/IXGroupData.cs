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
using OpenSim.Framework;

namespace OpenSim.Data
{
    public class XGroup
    {
        public UUID groupID;
        public UUID ownerRoleID;
        public string name;
        public string charter;
        public bool showInList;
        public UUID insigniaID;
        public int membershipFee;
        public bool openEnrollment;
        public bool allowPublish;
        public bool maturePublish;
        public UUID founderID;
        public ulong everyonePowers;
        public ulong ownersPowers;

        public Dictionary<UUID, XGroupMember> members = new Dictionary<UUID, XGroupMember>();
        public Dictionary<UUID, XGroupNotice> notices = new Dictionary<UUID, XGroupNotice>();

        public XGroup Clone()
        {
            XGroup clone = (XGroup)MemberwiseClone();
            clone.members = new Dictionary<UUID, XGroupMember>();
            clone.notices = new Dictionary<UUID, XGroupNotice>();

            foreach (KeyValuePair<UUID, XGroupMember> kvp in members)
                clone.members[kvp.Key] = kvp.Value.Clone();

            foreach (KeyValuePair<UUID, XGroupNotice> kvp in notices)
                clone.notices[kvp.Key] = kvp.Value.Clone();

            return clone;
        }
    }

    public class XGroupMember
    {
        public UUID agentID;
        public UUID groupID;
        public UUID roleID;
        public bool acceptNotices = true;
        public bool listInProfile = true;

        public XGroupMember Clone()
        {
            return (XGroupMember)MemberwiseClone();
        }
    }

    public class XGroupNotice
    {
        public UUID groupID;
        public UUID noticeID;
        public uint timestamp;
        public string fromName;
        public string subject;
        public string message;
        public byte[] binaryBucket;
        public bool hasAttachment;
        public int assetType;

        public XGroupNotice Clone()
        {
            XGroupNotice clone = (XGroupNotice)MemberwiseClone();
            clone.binaryBucket = (byte[])binaryBucket.Clone();

            return clone;
        }
    }

    /// <summary>
    /// Early stub interface for groups data, not final.
    /// </summary>
    /// <remarks>
    /// Currently in-use only for regression test purposes.
    /// </remarks>
    public interface IXGroupData
    {
        bool StoreGroup(XGroup group);
        XGroup GetGroup(UUID groupID);
        Dictionary<UUID, XGroup> GetGroups();
        bool DeleteGroup(UUID groupID);
    }
}