BEGIN;

CREATE TABLE `Terrain` (
       `RegionID` char(36) not null,
       `Map` longblob,
        PRIMARY KEY  (`RegionID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

COMMIT;