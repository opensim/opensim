BEGIN;

alter table regionsettings change column `object_bonus` `object_bonus` double NOT NULL;
alter table regionsettings change column `elevation_1_nw` `elevation_1_nw` double NOT NULL;
alter table regionsettings change column `elevation_2_nw` `elevation_2_nw` double NOT NULL;
alter table regionsettings change column `elevation_1_ne` `elevation_1_ne` double NOT NULL;
alter table regionsettings change column `elevation_2_ne` `elevation_2_ne` double NOT NULL;
alter table regionsettings change column `elevation_1_se` `elevation_1_se` double NOT NULL;
alter table regionsettings change column `elevation_2_se` `elevation_2_se` double NOT NULL;
alter table regionsettings change column `elevation_1_sw` `elevation_1_sw` double NOT NULL;
alter table regionsettings change column `elevation_2_sw` `elevation_2_sw` double NOT NULL;
alter table regionsettings change column `water_height` `water_height` double NOT NULL;
alter table regionsettings change column `terrain_raise_limit` `terrain_raise_limit` double NOT NULL;
alter table regionsettings change column `terrain_lower_limit` `terrain_lower_limit` double NOT NULL;
alter table regionsettings change column `sun_position` `sun_position` double NOT NULL;

COMMIT;

