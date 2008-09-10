begin;

ALTER TABLE prims ADD COLUMN ClickAction tinyint NOT NULL default 0;

commit;

