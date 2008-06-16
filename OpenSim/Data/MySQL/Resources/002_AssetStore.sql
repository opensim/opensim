BEGIN;

ALTER TABLE assets change id oldid binary(16);
ALTER TABLE assets add id varchar(36) not null default '';
UPDATE assets set id = concat(substr(hex(oldid),1,8),"-",substr(hex(oldid),9,4),"-",substr(hex(oldid),13,4),"-",substr(hex(oldid),17,4),"-",substr(hex(oldid),21,12));
ALTER TABLE assets drop oldid;
ALTER TABLE assets add constraint primary key(id);

COMMIT;