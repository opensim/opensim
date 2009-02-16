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
