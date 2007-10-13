CREATE TABLE `assets` (
  `id` binary(16) NOT NULL,
  `name` varchar(64) NOT NULL,
  `description` varchar(64) NOT NULL,
  `assetType` TINYINT(4) NOT NULL,
  `invType` TINYINT(4) NOT NULL,
  `local` tinyint(1) NOT NULL,
  `temporary` tinyint(1) NOT NULL,
  `data` longblob NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Rev. 1';