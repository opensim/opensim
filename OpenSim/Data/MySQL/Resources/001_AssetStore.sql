BEGIN;

CREATE TABLE `assets` (
  `id` binary(16) NOT NULL,
  `name` varchar(64) NOT NULL,
  `description` varchar(64) NOT NULL,
  `assetType` tinyint(4) NOT NULL,
  `invType` tinyint(4) NOT NULL,
  `local` tinyint(1) NOT NULL,
  `temporary` tinyint(1) NOT NULL,
  `data` longblob NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Rev. 1';

COMMIT;