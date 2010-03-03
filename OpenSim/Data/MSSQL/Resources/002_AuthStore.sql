BEGIN TRANSACTION

INSERT INTO auth (UUID, passwordHash, passwordSalt, webLoginKey, accountType) SELECT [UUID] AS UUID, [passwordHash] AS passwordHash, [passwordSalt] AS passwordSalt, [webLoginKey] AS webLoginKey, 'UserAccount' as [accountType] FROM users;


COMMIT