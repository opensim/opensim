SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for agents
-- ----------------------------
CREATE TABLE `agents` (
  `UUID` varchar(36) NOT NULL,
  `sessionID` varchar(36) NOT NULL,
  `secureSessionID` varchar(36) NOT NULL,
  `agentIP` varchar(16) NOT NULL,
  `agentPort` int(11) NOT NULL,
  `agentOnline` tinyint(4) NOT NULL,
  `loginTime` int(11) NOT NULL,
  `logoutTime` int(11) NOT NULL,
  `currentRegion` varchar(36) NOT NULL,
  `currentHandle` bigint(20) unsigned NOT NULL,
  `currentPos` varchar(64) NOT NULL,
  PRIMARY KEY  (`UUID`),
  UNIQUE KEY `session` (`sessionID`),
  UNIQUE KEY `ssession` (`secureSessionID`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records 
-- ----------------------------
