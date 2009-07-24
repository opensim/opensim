ALTER TABLE UserAgents ADD PositionX REAL null;
ALTER TABLE UserAgents ADD PositionY REAL null;
ALTER TABLE UserAgents ADD PositionZ REAL null;
ALTER TABLE UserAgents ADD LookAtX REAL null;
ALTER TABLE UserAgents ADD LookAtY REAL null;
ALTER TABLE UserAgents ADD LookAtZ REAL null;

ALTER TABLE UserProfiles ADD Email NVARCHAR(250) null;
ALTER TABLE UserProfiles ADD HomeRegionID NVARCHAR(36) null;
ALTER TABLE UserProfiles ADD CanDoMask INT null;
ALTER TABLE UserProfiles ADD WantDoMask INT null;
ALTER TABLE UserProfiles ADD UserFlags INT null;
ALTER TABLE UserProfiles ADD GodLevel INT null; 
ALTER TABLE UserProfiles ADD CustomType NVARCHAR(32) null;
ALTER TABLE UserProfiles ADD Partner NVARCHAR(36) null;

ALTER TABLE UserAppearances ADD AvatarHeight FLOAT null;

CREATE TABLE UserFriends (
  UserFriendID NVARCHAR(36) NOT NULL,
  OwnerID NVARCHAR(36) NULL,
  FriendID NVARCHAR(36) NULL,
  FriendPermissions INT NULL,
  PRIMARY KEY (UserFriendID)  
);

CREATE INDEX UserFriendsOwnerIdFriendIdIndex ON UserFriends (OwnerID,FriendID);  
