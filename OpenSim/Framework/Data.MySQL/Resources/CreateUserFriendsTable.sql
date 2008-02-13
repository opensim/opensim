SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for users
-- ----------------------------
CREATE TABLE `userfriends` (
   `ownerID` VARCHAR(37) NOT NULL,
   `friendID` VARCHAR(37) NOT NULL,
   `friendPerms` INT NOT NULL,
   `datetimestamp` INT NOT NULL,
	 UNIQUE KEY  (`ownerID`, `friendID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Rev.1';