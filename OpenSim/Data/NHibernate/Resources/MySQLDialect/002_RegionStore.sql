BEGIN;

CREATE TABLE `Terrain` (
       `RegionID` char(36) not null,
       `Map` blob,
        PRIMARY KEY  (`RegionID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

COMMIT;