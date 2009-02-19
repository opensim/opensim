BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_agents
	(
	UUID uniqueidentifier NOT NULL,
	sessionID uniqueidentifier NOT NULL,
	secureSessionID uniqueidentifier NOT NULL,
	agentIP varchar(16) NOT NULL,
	agentPort int NOT NULL,
	agentOnline tinyint NOT NULL,
	loginTime int NOT NULL,
	logoutTime int NOT NULL,
	currentRegion uniqueidentifier NOT NULL,
	currentHandle bigint NOT NULL,
	currentPos varchar(64) NOT NULL
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.agents)
	 EXEC('INSERT INTO dbo.Tmp_agents (UUID, sessionID, secureSessionID, agentIP, agentPort, agentOnline, loginTime, logoutTime, currentRegion, currentHandle, currentPos)
		SELECT CONVERT(uniqueidentifier, UUID), CONVERT(uniqueidentifier, sessionID), CONVERT(uniqueidentifier, secureSessionID), agentIP, agentPort, agentOnline, loginTime, logoutTime, CONVERT(uniqueidentifier, currentRegion), currentHandle, currentPos FROM dbo.agents WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.agents

EXECUTE sp_rename N'dbo.Tmp_agents', N'agents', 'OBJECT' 

ALTER TABLE dbo.agents ADD CONSTRAINT
	PK__agents__65A475E749C3F6B7 PRIMARY KEY CLUSTERED 
	(
	UUID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

CREATE NONCLUSTERED INDEX session ON dbo.agents
	(
	sessionID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

CREATE NONCLUSTERED INDEX ssession ON dbo.agents
	(
	secureSessionID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
