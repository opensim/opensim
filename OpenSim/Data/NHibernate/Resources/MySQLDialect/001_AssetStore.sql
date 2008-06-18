START TRANSACTION;

CREATE TABLE `Assets` (
  `ID` char(36) NOT NULL,
  `Type` smallint(6) default NULL,
  `InvType` smallint(6) default NULL,
  `Name` varchar(64) default NULL,
  `Description` varchar(64) default NULL,
  `Local` tinyint(1) default NULL,
  `Temporary` tinyint(1) default NULL,
  `Data` longblob,
  PRIMARY KEY  (`ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

COMMIT;