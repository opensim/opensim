BEGIN TRANSACTION;

create table UserAgents (
  ProfileID TEXT not null,
   AgentIP TEXT,
   AgentPort INTEGER,
   AgentOnline INTEGER,
   SessionID TEXT,
   SecureSessionID TEXT,
   InitialRegion TEXT,
   Region TEXT,
   LoginTime INTEGER,
   LogoutTime INTEGER,
   Handle INTEGER,
   PositionX NUMERIC,
   PositionY NUMERIC,
   PositionZ NUMERIC,
   LookAtX NUMERIC,
   LookAtY NUMERIC,
   LookAtZ NUMERIC,     
   primary key (ProfileID)
);

create table UserProfiles (
  ID TEXT not null,
   WebLoginKey TEXT,
   FirstName TEXT,
   SurName TEXT,
   Email TEXT,
   PasswordHash TEXT,
   PasswordSalt TEXT,
   HomeRegionID TEXT,
   HomeRegionX INTEGER,
   HomeRegionY INTEGER,
   HomeLocationX NUMERIC,
   HomeLocationY NUMERIC,
   HomeLocationZ NUMERIC,
   HomeLookAtX NUMERIC,
   HomeLookAtY NUMERIC,
   HomeLookAtZ NUMERIC,
   Created INTEGER,
   LastLogin INTEGER,
   UserInventoryURI TEXT,
   UserAssetURI TEXT,
   Image TEXT,
   FirstLifeImage TEXT,
   AboutText TEXT,
   FirstLifeAboutText TEXT,
   RootInventoryFolderID TEXT,
  `CanDoMask` INTEGER,
  `WantDoMask` INTEGER,
  `UserFlags` INTEGER,
  `GodLevel` INTEGER,  
  `CustomType` TEXT,
  `Partner` TEXT,   
   primary key (ID)
);

create table UserAppearances (
  Owner TEXT not null,
   BodyItem TEXT,
   BodyAsset TEXT,
   SkinItem TEXT,
   SkinAsset TEXT,
   HairItem TEXT,
   HairAsset TEXT,
   EyesItem TEXT,
   EyesAsset TEXT,
   ShirtItem TEXT,
   ShirtAsset TEXT,
   PantsItem TEXT,
   PantsAsset TEXT,
   ShoesItem TEXT,
   ShoesAsset TEXT,
   SocksItem TEXT,
   SocksAsset TEXT,
   JacketItem TEXT,
   JacketAsset TEXT,
   GlovesItem TEXT,
   GlovesAsset TEXT,
   UnderShirtItem TEXT,
   UnderShirtAsset TEXT,
   UnderPantsItem TEXT,
   UnderPantsAsset TEXT,
   SkirtItem TEXT,
   SkirtAsset TEXT,
   Texture BLOB,
   VisualParams BLOB,
   Serial INTEGER,
   AvatarHeight NUMERIC,     
   primary key (Owner)
);


CREATE TABLE UserFriends (
   UserFriendID TEXT,
   OwnerID TEXT,
   FriendID TEXT,
   FriendPermissions INTEGER,
   primary key (UserFriendID) 
);

create index UserFirstNameIndex on UserProfiles (FirstName);
create index UserSurnameIndex on UserProfiles (SurName);
create unique index UserFullNameIndex on UserProfiles (FirstName,SurName);
create unique index UserFriendsOwnerFriendIndex on UserFriends (OwnerID,FriendID);

COMMIT;
