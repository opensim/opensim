BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_prims
	(
	UUID uniqueidentifier NOT NULL,
	RegionUUID uniqueidentifier NULL,
	ParentID int NULL,
	CreationDate int NULL,
	Name varchar(255) NULL,
	SceneGroupID uniqueidentifier NULL,
	Text varchar(255) NULL,
	Description varchar(255) NULL,
	SitName varchar(255) NULL,
	TouchName varchar(255) NULL,
	ObjectFlags int NULL,
	CreatorID uniqueidentifier NULL,
	OwnerID uniqueidentifier NULL,
	GroupID uniqueidentifier NULL,
	LastOwnerID uniqueidentifier NULL,
	OwnerMask int NULL,
	NextOwnerMask int NULL,
	GroupMask int NULL,
	EveryoneMask int NULL,
	BaseMask int NULL,
	PositionX float(53) NULL,
	PositionY float(53) NULL,
	PositionZ float(53) NULL,
	GroupPositionX float(53) NULL,
	GroupPositionY float(53) NULL,
	GroupPositionZ float(53) NULL,
	VelocityX float(53) NULL,
	VelocityY float(53) NULL,
	VelocityZ float(53) NULL,
	AngularVelocityX float(53) NULL,
	AngularVelocityY float(53) NULL,
	AngularVelocityZ float(53) NULL,
	AccelerationX float(53) NULL,
	AccelerationY float(53) NULL,
	AccelerationZ float(53) NULL,
	RotationX float(53) NULL,
	RotationY float(53) NULL,
	RotationZ float(53) NULL,
	RotationW float(53) NULL,
	SitTargetOffsetX float(53) NULL,
	SitTargetOffsetY float(53) NULL,
	SitTargetOffsetZ float(53) NULL,
	SitTargetOrientW float(53) NULL,
	SitTargetOrientX float(53) NULL,
	SitTargetOrientY float(53) NULL,
	SitTargetOrientZ float(53) NULL,
	PayPrice int NOT NULL DEFAULT ((0)),
	PayButton1 int NOT NULL DEFAULT ((0)),
	PayButton2 int NOT NULL DEFAULT ((0)),
	PayButton3 int NOT NULL DEFAULT ((0)),
	PayButton4 int NOT NULL DEFAULT ((0)),
	LoopedSound uniqueidentifier NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000'),
	LoopedSoundGain float(53) NOT NULL DEFAULT ((0.0)),
	TextureAnimation image NULL,
	OmegaX float(53) NOT NULL DEFAULT ((0.0)),
	OmegaY float(53) NOT NULL DEFAULT ((0.0)),
	OmegaZ float(53) NOT NULL DEFAULT ((0.0)),
	CameraEyeOffsetX float(53) NOT NULL DEFAULT ((0.0)),
	CameraEyeOffsetY float(53) NOT NULL DEFAULT ((0.0)),
	CameraEyeOffsetZ float(53) NOT NULL DEFAULT ((0.0)),
	CameraAtOffsetX float(53) NOT NULL DEFAULT ((0.0)),
	CameraAtOffsetY float(53) NOT NULL DEFAULT ((0.0)),
	CameraAtOffsetZ float(53) NOT NULL DEFAULT ((0.0)),
	ForceMouselook tinyint NOT NULL DEFAULT ((0)),
	ScriptAccessPin int NOT NULL DEFAULT ((0)),
	AllowedDrop tinyint NOT NULL DEFAULT ((0)),
	DieAtEdge tinyint NOT NULL DEFAULT ((0)),
	SalePrice int NOT NULL DEFAULT ((10)),
	SaleType tinyint NOT NULL DEFAULT ((0)),
	ColorR int NOT NULL DEFAULT ((0)),
	ColorG int NOT NULL DEFAULT ((0)),
	ColorB int NOT NULL DEFAULT ((0)),
	ColorA int NOT NULL DEFAULT ((0)),
	ParticleSystem image NULL,
	ClickAction tinyint NOT NULL DEFAULT ((0)),
	Material tinyint NOT NULL DEFAULT ((3)),
	CollisionSound uniqueidentifier NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000'),
	CollisionSoundVolume float(53) NOT NULL DEFAULT ((0.0)),
	LinkNumber int NOT NULL DEFAULT ((0))
	)  ON [PRIMARY]
	 TEXTIMAGE_ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.prims)
	 EXEC('INSERT INTO dbo.Tmp_prims (UUID, RegionUUID, ParentID, CreationDate, Name, SceneGroupID, Text, Description, SitName, TouchName, ObjectFlags, CreatorID, OwnerID, GroupID, LastOwnerID, OwnerMask, NextOwnerMask, GroupMask, EveryoneMask, BaseMask, PositionX, PositionY, PositionZ, GroupPositionX, GroupPositionY, GroupPositionZ, VelocityX, VelocityY, VelocityZ, AngularVelocityX, AngularVelocityY, AngularVelocityZ, AccelerationX, AccelerationY, AccelerationZ, RotationX, RotationY, RotationZ, RotationW, SitTargetOffsetX, SitTargetOffsetY, SitTargetOffsetZ, SitTargetOrientW, SitTargetOrientX, SitTargetOrientY, SitTargetOrientZ, PayPrice, PayButton1, PayButton2, PayButton3, PayButton4, LoopedSound, LoopedSoundGain, TextureAnimation, OmegaX, OmegaY, OmegaZ, CameraEyeOffsetX, CameraEyeOffsetY, CameraEyeOffsetZ, CameraAtOffsetX, CameraAtOffsetY, CameraAtOffsetZ, ForceMouselook, ScriptAccessPin, AllowedDrop, DieAtEdge, SalePrice, SaleType, ColorR, ColorG, ColorB, ColorA, ParticleSystem, ClickAction, Material, CollisionSound, CollisionSoundVolume, LinkNumber)
		SELECT CONVERT(uniqueidentifier, UUID), CONVERT(uniqueidentifier, RegionUUID), ParentID, CreationDate, Name, CONVERT(uniqueidentifier, SceneGroupID), Text, Description, SitName, TouchName, ObjectFlags, CONVERT(uniqueidentifier, CreatorID), CONVERT(uniqueidentifier, OwnerID), CONVERT(uniqueidentifier, GroupID), CONVERT(uniqueidentifier, LastOwnerID), OwnerMask, NextOwnerMask, GroupMask, EveryoneMask, BaseMask, PositionX, PositionY, PositionZ, GroupPositionX, GroupPositionY, GroupPositionZ, VelocityX, VelocityY, VelocityZ, AngularVelocityX, AngularVelocityY, AngularVelocityZ, AccelerationX, AccelerationY, AccelerationZ, RotationX, RotationY, RotationZ, RotationW, SitTargetOffsetX, SitTargetOffsetY, SitTargetOffsetZ, SitTargetOrientW, SitTargetOrientX, SitTargetOrientY, SitTargetOrientZ, PayPrice, PayButton1, PayButton2, PayButton3, PayButton4, CONVERT(uniqueidentifier, LoopedSound), LoopedSoundGain, TextureAnimation, OmegaX, OmegaY, OmegaZ, CameraEyeOffsetX, CameraEyeOffsetY, CameraEyeOffsetZ, CameraAtOffsetX, CameraAtOffsetY, CameraAtOffsetZ, ForceMouselook, ScriptAccessPin, AllowedDrop, DieAtEdge, SalePrice, SaleType, ColorR, ColorG, ColorB, ColorA, ParticleSystem, ClickAction, Material, CONVERT(uniqueidentifier, CollisionSound), CollisionSoundVolume, LinkNumber FROM dbo.prims WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.prims

EXECUTE sp_rename N'dbo.Tmp_prims', N'prims', 'OBJECT' 

ALTER TABLE dbo.prims ADD CONSTRAINT
	PK__prims__10566F31 PRIMARY KEY CLUSTERED 
	(
	UUID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]


CREATE NONCLUSTERED INDEX prims_regionuuid ON dbo.prims
	(
	RegionUUID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

CREATE NONCLUSTERED INDEX prims_parentid ON dbo.prims
	(
	ParentID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
