BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_estate_settings
	(
	EstateID int NOT NULL IDENTITY (1, 100),
	EstateName varchar(64) NULL DEFAULT (NULL),
	AbuseEmailToEstateOwner bit NOT NULL,
	DenyAnonymous bit NOT NULL,
	ResetHomeOnTeleport bit NOT NULL,
	FixedSun bit NOT NULL,
	DenyTransacted bit NOT NULL,
	BlockDwell bit NOT NULL,
	DenyIdentified bit NOT NULL,
	AllowVoice bit NOT NULL,
	UseGlobalTime bit NOT NULL,
	PricePerMeter int NOT NULL,
	TaxFree bit NOT NULL,
	AllowDirectTeleport bit NOT NULL,
	RedirectGridX int NOT NULL,
	RedirectGridY int NOT NULL,
	ParentEstateID int NOT NULL,
	SunPosition float(53) NOT NULL,
	EstateSkipScripts bit NOT NULL,
	BillableFactor float(53) NOT NULL,
	PublicAccess bit NOT NULL,
	AbuseEmail varchar(255) NOT NULL,
	EstateOwner uniqueidentifier NOT NULL,
	DenyMinors bit NOT NULL
	)  ON [PRIMARY]

SET IDENTITY_INSERT dbo.Tmp_estate_settings ON

IF EXISTS(SELECT * FROM dbo.estate_settings)
	 EXEC('INSERT INTO dbo.Tmp_estate_settings (EstateID, EstateName, AbuseEmailToEstateOwner, DenyAnonymous, ResetHomeOnTeleport, FixedSun, DenyTransacted, BlockDwell, DenyIdentified, AllowVoice, UseGlobalTime, PricePerMeter, TaxFree, AllowDirectTeleport, RedirectGridX, RedirectGridY, ParentEstateID, SunPosition, EstateSkipScripts, BillableFactor, PublicAccess, AbuseEmail, EstateOwner, DenyMinors)
		SELECT EstateID, EstateName, AbuseEmailToEstateOwner, DenyAnonymous, ResetHomeOnTeleport, FixedSun, DenyTransacted, BlockDwell, DenyIdentified, AllowVoice, UseGlobalTime, PricePerMeter, TaxFree, AllowDirectTeleport, RedirectGridX, RedirectGridY, ParentEstateID, SunPosition, EstateSkipScripts, BillableFactor, PublicAccess, AbuseEmail, CONVERT(uniqueidentifier, EstateOwner), DenyMinors FROM dbo.estate_settings WITH (HOLDLOCK TABLOCKX)')

SET IDENTITY_INSERT dbo.Tmp_estate_settings OFF

DROP TABLE dbo.estate_settings

EXECUTE sp_rename N'dbo.Tmp_estate_settings', N'estate_settings', 'OBJECT' 

ALTER TABLE dbo.estate_settings ADD CONSTRAINT
	PK_estate_settings PRIMARY KEY CLUSTERED 
	(
	EstateID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
