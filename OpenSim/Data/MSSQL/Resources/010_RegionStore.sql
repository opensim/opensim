BEGIN TRANSACTION

ALTER TABLE regionsettings ADD sunvectorx float NOT NULL default 0;
ALTER TABLE regionsettings ADD sunvectory float NOT NULL default 0;
ALTER TABLE regionsettings ADD sunvectorz float NOT NULL default 0;

COMMIT
