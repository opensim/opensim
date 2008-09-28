BEGIN TRANSACTION

CREATE TABLE Tmp_userfriends
	(
	ownerID varchar(36) NOT NULL,
	friendID varchar(36) NOT NULL,
	friendPerms int NOT NULL,
	datetimestamp int NOT NULL
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM userfriends)
	 EXEC('INSERT INTO dbo.Tmp_userfriends (ownerID, friendID, friendPerms, datetimestamp)
		SELECT CONVERT(varchar(36), ownerID), CONVERT(varchar(36), friendID), CONVERT(int, friendPerms), CONVERT(int, datetimestamp) FROM dbo.userfriends WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.userfriends

EXECUTE sp_rename N'Tmp_userfriends', N'userfriends', 'OBJECT' 

CREATE NONCLUSTERED INDEX IX_userfriends_ownerID ON userfriends
	(
	ownerID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

CREATE NONCLUSTERED INDEX IX_userfriends_friendID ON userfriends
	(
	friendID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
