BEGIN TRANSACTION;

CREATE TEMPORARY TABLE assets_backup(UUID,Name,Description,Type,Local,Temporary,Data);
INSERT INTO assets_backup SELECT UUID,Name,Description,Type,Local,Temporary,Data FROM assets;
DROP TABLE assets;
CREATE TABLE assets(UUID,Name,Description,Type,Local,Temporary,Data);
INSERT INTO assets SELECT UUID,Name,Description,Type,Local,Temporary,Data FROM assets_backup;
DROP TABLE assets_backup;

COMMIT;
