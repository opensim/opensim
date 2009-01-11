BEGIN;

CREATE TABLE `PrimItems` (
  `ItemID` char(36) NOT NULL default '',
  `GroupID` char(36) default NULL,
  `PrimID` char(36) default NULL,
  `ParentFolderID` char(36) default NULL,
  `AssetID` char(36) default NULL,
  `OwnerID` char(36) default NULL,
  `LastOwnerID` char(36) default NULL,
  `CreatorID` char(36) default NULL,
  `CreationDate` bigint(20) default NULL,
  `InvType` int(11) default NULL,
  `Name` varchar(255) default NULL,
  `Description` varchar(255) default NULL,
  `NextPermissions` int(11) default NULL,
  `CurrentPermissions` int(11) default NULL,
  `BasePermissions` int(11) default NULL,
  `EveryonePermissions` int(11) default NULL,
  `GroupPermissions` int(11) default NULL,
  `Flags` int(11) NOT NULL default '0',
  PRIMARY KEY  (`ItemID`),
  KEY `primitems_primid` (`PrimID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE RegionSettings (
  `RegionID` char(36) default NULL,
  
  `BlockTerraform` bit(1) default NULL,
  `BlockFly` bit(1) default NULL,
  `AllowDamage` bit(1) default NULL,
  `RestrictPushing` bit(1) default NULL,
  `AllowLandResell` bit(1) default NULL,
  `AllowLandJoinDivide` bit(1) default NULL,
  `BlockShowInSearch` bit(1) default NULL,
  
  `AgentLimit` int(11) default NULL,
  `ObjectBonus` double default NULL,
  `Maturity` int(11) default NULL,
  
  `DisableScripts` bit(1) default NULL,
  `DisableCollisions` bit(1) default NULL,
  `DisablePhysics` bit(1) default NULL,

  `TerrainTexture1` char(36) default NULL,
  `TerrainTexture2` char(36) default NULL,
  `TerrainTexture3` char(36) default NULL,
  `TerrainTexture4` char(36) default NULL,

  `Elevation1NW` double default NULL,
  `Elevation2NW` double default NULL,
  `Elevation1NE` double default NULL,
  `Elevation2NE` double default NULL,
  `Elevation1SE` double default NULL,
  `Elevation2SE` double default NULL,
  `Elevation1SW` double default NULL,
  `Elevation2SW` double default NULL,
  
  `WaterHeight` double default NULL,
  `TerrainRaiseLimit` double default NULL,
  `TerrainLowerLimit` double default NULL,    

  `UseEstateSun` bit(1) default NULL,
  `Sandbox` bit(1) default NULL,

  `SunVectorX` double default NULL,
  `SunVectorY` double default NULL,
  `SunVectorZ` double default NULL,
 
  `FixedSun` bit(1) default NULL, 
  `SunPosition` double default NULL, 
 
  `Covenant` char(36) default NULL,
   
  PRIMARY KEY  (RegionID)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

COMMIT;
