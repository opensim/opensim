using System;
using System.Collections;
using libsecondlife;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework
{
    public class GroupData
    {
        public string ActiveGroupTitle;
        public LLUUID GroupID;
        public List<LLUUID> GroupMembers;
        public string groupName;
        public uint groupPowers = (uint)(GroupPowers.LandAllowLandmark | GroupPowers.LandAllowSetHome);
        public List<string> GroupTitles;
        public bool AcceptNotices = true;
        public bool AllowPublish = true;
        public string Charter = "Cool Group Yeah!";
        public int contribution = 0;
        public LLUUID FounderID = LLUUID.Zero;
        public int groupMembershipCost = 0;
        public int groupRollsCount = 1;
        public LLUUID GroupPicture = LLUUID.Zero;
        public bool MaturePublish = true;
        public int MembershipFee = 0;
        public bool OpenEnrollment = true;
        public bool ShowInList = true;

        public GroupData()
        {
            GroupTitles = new List<string>();
            GroupMembers = new List<LLUUID>();
        }

        public GroupPowers ActiveGroupPowers
        {
            set { groupPowers = (uint)value; }
            get { return (GroupPowers)groupPowers; }
        }
    }

    public class GroupList
    {
        public List<LLUUID> m_GroupList;

        public GroupList()
        {
            m_GroupList = new List<LLUUID>();
        }
    }
}
