CREATE TABLE EstateSettings (
  EstateID INT NOT NULL,
  ParentEstateID INT NULL,
  EstateOwnerID NVARCHAR(36) NULL,
  Name NVARCHAR(64) NULL,
  RedirectGridX INT NULL,
  RedirectGridY INT NULL,
  BillableFactor REAL NULL,
  PricePerMeter INT NULL,
  SunPosition FLOAT NULL,
  
  UseGlobalTime BIT NULL,
  FixedSun BIT NULL,
  AllowVoice BIT NULL,
  AllowDirectTeleport BIT NULL,
  ResetHomeOnTeleport BIT NULL,
  PublicAccess BIT NULL,
  DenyAnonymous BIT NULL,
  DenyIdentified BIT NULL,
  DenyTransacted BIT NULL,
  DenyMinors BIT NULL,
  BlockDwell BIT NULL,
  EstateSkipScripts BIT NULL,
  TaxFree BIT NULL,
  AbuseEmailToEstateOwner BIT NULL,
  
  AbuseEmail NVARCHAR(255) NULL,

  PRIMARY KEY (EstateID)
);

CREATE TABLE EstateRegionLink (
  EstateRegionLinkID NVARCHAR(36) NOT NULL,
  EstateID INT NULL,
  RegionID NVARCHAR(36) NULL,
  PRIMARY KEY (EstateRegionLinkID)
);

CREATE INDEX EstateRegionLinkEstateIDIndex ON EstateRegionLink (EstateID);
CREATE INDEX EstateRegionLinkERegionIDIndex ON EstateRegionLink (RegionID);


CREATE TABLE EstateManagers (
  EstateID INT NOT NULL,
  ManagerID NVARCHAR(36) NOT NULL,
  ArrayIndex INT NOT NULL,
  PRIMARY KEY (EstateID,ArrayIndex)
);

CREATE TABLE EstateUsers (
  EstateID INT NOT NULL,
  UserID NVARCHAR(36) NOT NULL,
  ArrayIndex INT NOT NULL,
  PRIMARY KEY (EstateID,ArrayIndex)
);

CREATE TABLE EstateGroups (
  EstateID INT NOT NULL,
  GroupID NVARCHAR(36) NOT NULL,
  ArrayIndex INT NOT NULL,
  PRIMARY KEY (EstateID,ArrayIndex)
);

CREATE TABLE EstateBans (
  EstateID INT NOT NULL,
  ArrayIndex INT NOT NULL,
  BannedUserID NVARCHAR(36) NOT NULL,
  BannedHostAddress NVARCHAR(16) NOT NULL,
  BannedHostIPMask NVARCHAR(16) NOT NULL,
  BannedHostNameMask NVARCHAR(16) NOT NULL,
  PRIMARY KEY (EstateID,ArrayIndex)
);