CREATE TABLE `land`
   (
   `UUID` varchar (255) NOT NULL,
   `RegionUUID` varchar (255) DEFAULT NULL ,
   `LocalLandID` int (11) DEFAULT NULL ,
   `Bitmap` longblob,
   `Name` varchar (255) DEFAULT NULL ,
   `Description` varchar (255) DEFAULT NULL ,
   `OwnerUUID` varchar (255) DEFAULT NULL ,
   `IsGroupOwned` int (11) DEFAULT NULL ,
   `Area` int (11) DEFAULT NULL ,
   `AuctionID` int (11) DEFAULT NULL ,
   `Category` int (11) DEFAULT NULL ,
   `ClaimDate` int (11) DEFAULT NULL ,
   `ClaimPrice` int (11) DEFAULT NULL ,
   `GroupUUID` varchar (255) DEFAULT NULL ,
   `SalePrice` int (11) DEFAULT NULL ,
   `LandStatus` int (11) DEFAULT NULL ,
   `LandFlags` int (11) DEFAULT NULL ,
   `LandingType` int (11) DEFAULT NULL ,
   `MediaAutoScale` int (11) DEFAULT NULL ,
   `MediaTextureUUID` varchar (255) DEFAULT NULL ,
   `MediaURL` varchar (255) DEFAULT NULL ,
   `MusicURL` varchar (255) DEFAULT NULL ,
   `PassHours` float DEFAULT NULL ,
   `PassPrice` int (11) DEFAULT NULL ,
   `SnapshotUUID` varchar (255) DEFAULT NULL ,
   `UserLocationX` float DEFAULT NULL ,
   `UserLocationY` float DEFAULT NULL ,
   `UserLocationZ` float DEFAULT NULL ,
   `UserLookAtX` float DEFAULT NULL ,
   `UserLookAtY` float DEFAULT NULL ,
   `UserLookAtZ` float DEFAULT NULL ,
   `AuthbuyerID` varchar(36) default '00000000-0000-0000-0000-000000000000' not null,
   
   PRIMARY KEY (`UUID`)
   )
   ENGINE=INNODB
   DEFAULT CHARSET=utf8 COMMENT='Rev. 2';