BEGIN;

CREATE TABLE `avatarattachments` (`UUID` char(36) NOT NULL, `attachpoint` int(11) NOT NULL, `item` char(36) NOT NULL, `asset` char(36) NOT NULL) ENGINE=InnoDB;

COMMIT;
