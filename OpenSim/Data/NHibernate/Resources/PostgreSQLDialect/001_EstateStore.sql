CREATE TABLE EstateSettings (
  EstateID INT NOT NULL,
  ParentEstateID INT DEFAULT NULL,
  EstateOwnerID VARCHAR(36) DEFAULT NULL,
  Name VARCHAR(64) DEFAULT NULL,
  RedirectGridX INT DEFAULT NULL,
  RedirectGridY INT DEFAULT NULL,
  BillableFactor DOUBLE PRECISION DEFAULT NULL,
  PricePerMeter INT DEFAULT NULL,
  SunPosition DOUBLE PRECISION DEFAULT NULL,
  
  UseGlobalTime BOOLEAN DEFAULT NULL,
  FixedSun BOOLEAN DEFAULT NULL,
  AllowVoice BOOLEAN DEFAULT NULL,
  AllowDirectTeleport BOOLEAN DEFAULT NULL,
  ResetHomeOnTeleport BOOLEAN DEFAULT NULL,
  PublicAccess BOOLEAN DEFAULT NULL,
  DenyAnonymous BOOLEAN DEFAULT NULL,
  DenyIdentified BOOLEAN DEFAULT NULL,
  DenyTransacted BOOLEAN DEFAULT NULL,
  DenyMinors BOOLEAN DEFAULT NULL,
  BlockDwell BOOLEAN DEFAULT NULL,
  EstateSkipScripts BOOLEAN DEFAULT NULL,
  TaxFree BOOLEAN DEFAULT NULL,
  AbuseEmailToEstateOwner BOOLEAN DEFAULT NULL,
  
  AbuseEmail VARCHAR(255) DEFAULT NULL,

  PRIMARY KEY (EstateID)
);

CREATE TABLE EstateRegionLink (
  EstateRegionLinkID VARCHAR(36) NOT NULL,
  EstateID INT DEFAULT NULL,
  RegionID VARCHAR(36) DEFAULT NULL,
  PRIMARY KEY (EstateRegionLinkID)
);

CREATE INDEX EstateRegionLinkEstateIDIndex ON EstateRegionLink (EstateID);
CREATE INDEX EstateRegionLinkERegionIDIndex ON EstateRegionLink (RegionID);


CREATE TABLE EstateManagers (
  EstateID INT NOT NULL,
  ManagerID VARCHAR(36) NOT NULL,
  ArrayIndex INT NOT NULL,
  PRIMARY KEY (EstateID,ArrayIndex)
);

CREATE TABLE EstateUsers (
  EstateID INT NOT NULL,
  UserID VARCHAR(36) NOT NULL,
  ArrayIndex INT NOT NULL,
  PRIMARY KEY (EstateID,ArrayIndex)
);

CREATE TABLE EstateGroups (
  EstateID INT NOT NULL,
  GroupID VARCHAR(36) NOT NULL,
  ArrayIndex INT NOT NULL,
  PRIMARY KEY (EstateID,ArrayIndex)
);

CREATE TABLE EstateBans (
  EstateID INT NOT NULL,
  ArrayIndex INT NOT NULL,
  BannedUserID VARCHAR(36) NOT NULL,
  BannedHostAddress VARCHAR(16) NOT NULL,
  BannedHostIPMask VARCHAR(16) NOT NULL,
  BannedHostNameMask VARCHAR(16) NOT NULL,
  PRIMARY KEY (EstateID,ArrayIndex)
);