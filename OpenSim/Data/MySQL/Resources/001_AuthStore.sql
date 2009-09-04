begin;

CREATE TABLE `auth` (
  `UUID` char(36) NOT NULL,
  `passwordHash` char(32) NOT NULL default '',
  `passwordSalt` char(32) NOT NULL default '',
  `webLoginKey` varchar(255) NOT NULL default '',
  PRIMARY KEY  (`UUID`)
) ENGINE=InnoDB;

CREATE TABLE `tokens` (
  `UUID` char(36) NOT NULL,
  `token` varchar(255) NOT NULL,
  `validity` datetime NOT NULL,
  UNIQUE KEY `uuid_token` (`UUID`,`token`),
  KEY `UUID` (`UUID`),
  KEY `token` (`token`),
  KEY `validity` (`validity`)
) ENGINE=InnoDB;

commit;
