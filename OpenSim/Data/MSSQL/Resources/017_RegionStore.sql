BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_land
	(
	UUID uniqueidentifier NOT NULL,
	RegionUUID uniqueidentifier NULL,
	LocalLandID int NULL,
	Bitmap image NULL,
	Name varchar(255) NULL,
	Description varchar(255) NULL,
	OwnerUUID uniqueidentifier NULL,
	IsGroupOwned int NULL,
	Area int NULL,
	AuctionID int NULL,
	Category int NULL,
	ClaimDate int NULL,
	ClaimPrice int NULL,
	GroupUUID uniqueidentifier NULL,
	SalePrice int NULL,
	LandStatus int NULL,
	LandFlags int NULL,
	LandingType int NULL,
	MediaAutoScale int NULL,
	MediaTextureUUID uniqueidentifier NULL,
	MediaURL varchar(255) NULL,
	MusicURL varchar(255) NULL,
	PassHours float(53) NULL,
	PassPrice int NULL,
	SnapshotUUID uniqueidentifier NULL,
	UserLocationX float(53) NULL,
	UserLocationY float(53) NULL,
	UserLocationZ float(53) NULL,
	UserLookAtX float(53) NULL,
	UserLookAtY float(53) NULL,
	UserLookAtZ float(53) NULL,
	AuthbuyerID uniqueidentifier NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000'),
	OtherCleanTime int NOT NULL DEFAULT ((0)),
	Dwell int NOT NULL DEFAULT ((0))
	)  ON [PRIMARY]
	 TEXTIMAGE_ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.land)
	 EXEC('INSERT INTO dbo.Tmp_land (UUID, RegionUUID, LocalLandID, Bitmap, Name, Description, OwnerUUID, IsGroupOwned, Area, AuctionID, Category, ClaimDate, ClaimPrice, GroupUUID, SalePrice, LandStatus, LandFlags, LandingType, MediaAutoScale, MediaTextureUUID, MediaURL, MusicURL, PassHours, PassPrice, SnapshotUUID, UserLocationX, UserLocationY, UserLocationZ, UserLookAtX, UserLookAtY, UserLookAtZ, AuthbuyerID, OtherCleanTime, Dwell)
		SELECT CONVERT(uniqueidentifier, UUID), CONVERT(uniqueidentifier, RegionUUID), LocalLandID, Bitmap, Name, Description, CONVERT(uniqueidentifier, OwnerUUID), IsGroupOwned, Area, AuctionID, Category, ClaimDate, ClaimPrice, CONVERT(uniqueidentifier, GroupUUID), SalePrice, LandStatus, LandFlags, LandingType, MediaAutoScale, CONVERT(uniqueidentifier, MediaTextureUUID), MediaURL, MusicURL, PassHours, PassPrice, CONVERT(uniqueidentifier, SnapshotUUID), UserLocationX, UserLocationY, UserLocationZ, UserLookAtX, UserLookAtY, UserLookAtZ, CONVERT(uniqueidentifier, AuthbuyerID), OtherCleanTime, Dwell FROM dbo.land WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.land

EXECUTE sp_rename N'dbo.Tmp_land', N'land', 'OBJECT' 

ALTER TABLE dbo.land ADD CONSTRAINT
	PK__land__65A475E71BFD2C07 PRIMARY KEY CLUSTERED 
	(
	UUID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
