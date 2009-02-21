CREATE TABLE EstateSettings (
  EstateID INT NOT NULL,
  ParentEstateID INT DEFAULT NULL,
  EstateOwnerID VARCHAR(36) DEFAULT NULL,
  Name VARCHAR(64) DEFAULT NULL,
  RedirectGridX INT DEFAULT NULL,
  RedirectGridY INT DEFAULT NULL,
  BillableFactor DOUBLE DEFAULT NULL,
  PricePerMeter INT DEFAULT NULL,
  SunPosition DOUBLE DEFAULT NULL,
  
  UseGlobalTime BIT DEFAULT NULL,
  FixedSun BIT DEFAULT NULL,
  AllowVoice BIT DEFAULT NULL,
  AllowDirectTeleport BIT DEFAULT NULL,
  ResetHomeOnTeleport BIT DEFAULT NULL,
  PublicAccess BIT DEFAULT NULL,
  DenyAnonymous BIT DEFAULT NULL,
  DenyIdentified BIT DEFAULT NULL,
  DenyTransacted BIT DEFAULT NULL,
  DenyMinors BIT DEFAULT NULL,
  BlockDwell BIT DEFAULT NULL,
  EstateSkipScripts BIT DEFAULT NULL,
  TaxFree BIT DEFAULT NULL,
  AbuseEmailToEstateOwner BIT DEFAULT NULL,
  
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
