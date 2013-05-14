using System;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class UserClassifiedAdd
    {
        public UUID ClassifiedId = UUID.Zero;
        public UUID CreatorId = UUID.Zero;
        public int CreationDate = 0;
        public int ExpirationDate = 0;
        public int Category = 0;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public UUID ParcelId = UUID.Zero;
        public int ParentEstate = 0;
        public UUID SnapshotId = UUID.Zero;
        public string SimName = string.Empty;
        public string GlobalPos = "<0,0,0>";
        public string ParcelName = string.Empty;
        public byte Flags = 0;
        public int Price = 0;
    }
    
    public class UserProfileProperties
    {
        public UUID UserId = UUID.Zero;
        public UUID PartnerId = UUID.Zero;
        public bool PublishProfile = false;
        public bool PublishMature = false;
        public string WebUrl = string.Empty;
        public int WantToMask = 0;
        public string WantToText = string.Empty;
        public int SkillsMask = 0;
        public string SkillsText = string.Empty;
        public string Language = string.Empty;
        public UUID ImageId = UUID.Zero;
        public string AboutText = string.Empty;
        public UUID FirstLifeImageId = UUID.Zero;
        public string FirstLifeText = string.Empty;
    }
    
    public class UserProfilePick
    {
        public UUID PickId = UUID.Zero;
        public UUID CreatorId = UUID.Zero;
        public bool TopPick = false;
        public string Name = string.Empty;
        public string OriginalName = string.Empty;
        public string Desc = string.Empty;
        public UUID ParcelId = UUID.Zero;
        public UUID SnapshotId = UUID.Zero;
        public string User = string.Empty;
        public string SimName = string.Empty;
        public string GlobalPos = "<0,0,0>";
        public int SortOrder = 0;
        public bool Enabled = false;
    }
    
    public class UserProfileNotes
    {
        public UUID UserId;
        public UUID TargetId;
        public string Notes;
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
}

