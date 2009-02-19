BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_userfriends
	(
	ownerID uniqueidentifier NOT NULL,
	friendID uniqueidentifier NOT NULL,
	friendPerms int NOT NULL,
	datetimestamp int NOT NULL
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.userfriends)
	 EXEC('INSERT INTO dbo.Tmp_userfriends (ownerID, friendID, friendPerms, datetimestamp)
		SELECT CONVERT(uniqueidentifier, ownerID), CONVERT(uniqueidentifier, friendID), friendPerms, datetimestamp FROM dbo.userfriends WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.userfriends

EXECUTE sp_rename N'dbo.Tmp_userfriends', N'userfriends', 'OBJECT' 

CREATE NONCLUSTERED INDEX IX_userfriends_ownerID ON dbo.userfriends
	(
	ownerID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

CREATE NONCLUSTERED INDEX IX_userfriends_friendID ON dbo.userfriends
	(
	friendID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
