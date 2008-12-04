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
   primary key (ProfileID)
);
create table UserProfiles (
  ID TEXT not null,
   FirstName TEXT,
   SurName TEXT,
   PasswordHash TEXT,
   PasswordSalt TEXT,
   WebLoginKey TEXT,
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
   RootInventoryFolderID TEXT,
   UserInventoryURI TEXT,
   UserAssetURI TEXT,
   Image TEXT,
   FirstLifeImage TEXT,
   AboutText TEXT,
   FirstLifeAboutText TEXT,
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
   primary key (Owner)
);
create index user_firstname on UserProfiles (FirstName);
create index user_surname on UserProfiles (SurName);

COMMIT;