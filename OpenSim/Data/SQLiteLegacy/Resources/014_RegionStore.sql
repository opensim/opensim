begin;

ALTER TABLE regionsettings ADD COLUMN sunvectorx double NOT NULL default 0;
ALTER TABLE regionsettings ADD COLUMN sunvectory double NOT NULL default 0;
ALTER TABLE regionsettings ADD COLUMN sunvectorz double NOT NULL default 0;

commit;

