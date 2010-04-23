BEGIN TRANSACTION;

ALTER TABLE useragents add currentLookAtX float not null default 128;
ALTER TABLE useragents add currentLookAtY float not null default 128;
ALTER TABLE useragents add currentLookAtZ float not null default 70;

COMMIT;
