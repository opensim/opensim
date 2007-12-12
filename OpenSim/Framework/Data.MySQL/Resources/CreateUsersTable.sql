SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for users
-- ----------------------------
CREATE TABLE `users` (
  `UUID` varchar(36) NOT NULL default '',
  `username` varchar(32) NOT NULL,
  `lastname` varchar(32) NOT NULL,
  `passwordHash` varchar(32) NOT NULL,
  `passwordSalt` varchar(32) NOT NULL,
  `homeRegion` bigint(20) unsigned default NULL,
  `homeLocationX` float default NULL,
  `homeLocationY` float default NULL,
  `homeLocationZ` float default NULL,
  `homeLookAtX` float default NULL,
  `homeLookAtY` float default NULL,
  `homeLookAtZ` float default NULL,
  `created` int(11) NOT NULL,
  `lastLogin` int(11) NOT NULL,
  `userInventoryURI` varchar(255) default NULL,
  `userAssetURI` varchar(255) default NULL,
  `profileCanDoMask` int(10) unsigned default NULL,
  `profileWantDoMask` int(10) unsigned default NULL,
  `profileAboutText` text,
  `profileFirstText` text,
  `profileImage` varchar(36) default NULL,
  `profileFirstImage` varchar(36) default NULL,
  PRIMARY KEY  (`UUID`),
  UNIQUE KEY `usernames` (`username`,`lastname`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Rev. 1';

-- ----------------------------
-- Records 
-- ----------------------------
