BEGIN TRANSACTION

CREATE TABLE Tmp_regions
	(
	uuid varchar(36) COLLATE Latin1_General_CI_AS NOT NULL,
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
	regionMapTexture varchar(36) NULL,
	serverHttpPort int NULL,
	serverRemotingPort int NULL,
	owner_uuid varchar(36) NULL,
	originUUID varchar(36) NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000')
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM regions)
	 EXEC('INSERT INTO Tmp_regions (uuid, regionHandle, regionName, regionRecvKey, regionSendKey, regionSecret, regionDataURI, serverIP, serverPort, serverURI, locX, locY, locZ, eastOverrideHandle, westOverrideHandle, southOverrideHandle, northOverrideHandle, regionAssetURI, regionAssetRecvKey, regionAssetSendKey, regionUserURI, regionUserRecvKey, regionUserSendKey, regionMapTexture, serverHttpPort, serverRemotingPort, owner_uuid)
		SELECT CONVERT(varchar(36), uuid), CONVERT(bigint, regionHandle), CONVERT(varchar(20), regionName), CONVERT(varchar(128), regionRecvKey), CONVERT(varchar(128), regionSendKey), CONVERT(varchar(128), regionSecret), CONVERT(varchar(128), regionDataURI), CONVERT(varchar(64), serverIP), CONVERT(int, serverPort), serverURI, CONVERT(int, locX), CONVERT(int, locY), CONVERT(int, locZ), CONVERT(bigint, eastOverrideHandle), CONVERT(bigint, westOverrideHandle), CONVERT(bigint, southOverrideHandle), CONVERT(bigint, northOverrideHandle), regionAssetURI, CONVERT(varchar(128), regionAssetRecvKey), CONVERT(varchar(128), regionAssetSendKey), regionUserURI, CONVERT(varchar(128), regionUserRecvKey), CONVERT(varchar(128), regionUserSendKey), CONVERT(varchar(36), regionMapTexture), CONVERT(int, serverHttpPort), CONVERT(int, serverRemotingPort), owner_uuid FROM regions')

DROP TABLE regions

EXECUTE sp_rename N'Tmp_regions', N'regions', 'OBJECT' 

ALTER TABLE regions ADD CONSTRAINT
	PK__regions__uuid PRIMARY KEY CLUSTERED 
	(
	uuid
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT