BEGIN;

INSERT INTO auth (UUID, passwordHash, passwordSalt, webLoginKey) SELECT `UUID` AS UUID, `passwordHash` AS passwordHash, `passwordSalt` AS passwordSalt, `webLoginKey` AS webLoginKey FROM users;

COMMIT;
