/* To prevent any potential data loss issues, you should review this script in detail before running it outside the context of the database designer.*/
BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_regions
	(
	uuid uniqueidentifier NOT NULL,
	regionHandle bigint NULL,
	regionName varchar(20) NULL,
	regionRecvKey varchar(128) NULL,
	regionSendKey varchar(128) NULL,
	regionSecret varchar(128) NULL,
	regionDataURI varchar(128) NULL,
	serverIP varchar(64) NULL,
	serverPort int NULL,
	serverURI varchar(255) NULL,
	locX int NULL,
	locY int NULL,
	locZ int NULL,
	eastOverrideHandle bigint NULL,
	westOverrideHandle bigint NULL,
	southOverrideHandle bigint NULL,
	northOverrideHandle bigint NULL,
	regionAssetURI varchar(255) NULL,
	regionAssetRecvKey varchar(128) NULL,
	regionAssetSendKey varchar(128) NULL,
	regionUserURI varchar(255) NULL,
	regionUserRecvKey varchar(128) NULL,
	regionUserSendKey varchar(128) NULL,
	regionMapTexture uniqueidentifier NULL,
	serverHttpPort int NULL,
	serverRemotingPort int NULL,
	owner_uuid uniqueidentifier NOT NULL,
	originUUID uniqueidentifier NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000')
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.regions)
	 EXEC('INSERT INTO dbo.Tmp_regions (uuid, regionHandle, regionName, regionRecvKey, regionSendKey, regionSecret, regionDataURI, serverIP, serverPort, serverURI, locX, locY, locZ, eastOverrideHandle, westOverrideHandle, southOverrideHandle, northOverrideHandle, regionAssetURI, regionAssetRecvKey, regionAssetSendKey, regionUserURI, regionUserRecvKey, regionUserSendKey, regionMapTexture, serverHttpPort, serverRemotingPort, owner_uuid, originUUID)
		SELECT CONVERT(uniqueidentifier, uuid), regionHandle, regionName, regionRecvKey, regionSendKey, regionSecret, regionDataURI, serverIP, serverPort, serverURI, locX, locY, locZ, eastOverrideHandle, westOverrideHandle, southOverrideHandle, northOverrideHandle, regionAssetURI, regionAssetRecvKey, regionAssetSendKey, regionUserURI, regionUserRecvKey, regionUserSendKey, CONVERT(uniqueidentifier, regionMapTexture), serverHttpPort, serverRemotingPort, CONVERT(uniqueidentifier, owner_uuid), CONVERT(uniqueidentifier, originUUID) FROM dbo.regions WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.regions

EXECUTE sp_rename N'dbo.Tmp_regions', N'regions', 'OBJECT' 

ALTER TABLE dbo.regions ADD CONSTRAINT
	PK__regions__uuid PRIMARY KEY CLUSTERED 
	(
	uuid
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

CREATE NONCLUSTERED INDEX IX_regions_name ON dbo.regions
	(
	regionName
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

CREATE NONCLUSTERED INDEX IX_regions_handle ON dbo.regions
	(
	regionHandle
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

CREATE NONCLUSTERED INDEX IX_regions_override ON dbo.regions
	(
	eastOverrideHandle,
	westOverrideHandle,
	southOverrideHandle,
	northOverrideHandle
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
