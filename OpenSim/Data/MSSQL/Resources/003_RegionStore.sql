BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_prims
	(
	UUID varchar(36) NOT NULL,
	RegionUUID varchar(36) NULL,
	ParentID int NULL,
	CreationDate int NULL,
	Name varchar(255) NULL,
	SceneGroupID varchar(36) NULL,
	Text varchar(255) NULL,
	Description varchar(255) NULL,
	SitName varchar(255) NULL,
	TouchName varchar(255) NULL,
	ObjectFlags int NULL,
	CreatorID varchar(36) NULL,
	OwnerID varchar(36) NULL,
	GroupID varchar(36) NULL,
	LastOwnerID varchar(36) NULL,
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
	SitTargetOrientZ float(53) NULL
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.prims)
	 EXEC('INSERT INTO dbo.Tmp_prims (UUID, RegionUUID, ParentID, CreationDate, Name, SceneGroupID, Text, Description, SitName, TouchName, ObjectFlags, CreatorID, OwnerID, GroupID, LastOwnerID, OwnerMask, NextOwnerMask, GroupMask, EveryoneMask, BaseMask, PositionX, PositionY, PositionZ, GroupPositionX, GroupPositionY, GroupPositionZ, VelocityX, VelocityY, VelocityZ, AngularVelocityX, AngularVelocityY, AngularVelocityZ, AccelerationX, AccelerationY, AccelerationZ, RotationX, RotationY, RotationZ, RotationW, SitTargetOffsetX, SitTargetOffsetY, SitTargetOffsetZ, SitTargetOrientW, SitTargetOrientX, SitTargetOrientY, SitTargetOrientZ)
		SELECT CONVERT(varchar(36), UUID), CONVERT(varchar(36), RegionUUID), ParentID, CreationDate, Name, CONVERT(varchar(36), SceneGroupID), Text, Description, SitName, TouchName, ObjectFlags, CONVERT(varchar(36), CreatorID), CONVERT(varchar(36), OwnerID), CONVERT(varchar(36), GroupID), CONVERT(varchar(36), LastOwnerID), OwnerMask, NextOwnerMask, GroupMask, EveryoneMask, BaseMask, PositionX, PositionY, PositionZ, GroupPositionX, GroupPositionY, GroupPositionZ, VelocityX, VelocityY, VelocityZ, AngularVelocityX, AngularVelocityY, AngularVelocityZ, AccelerationX, AccelerationY, AccelerationZ, RotationX, RotationY, RotationZ, RotationW, SitTargetOffsetX, SitTargetOffsetY, SitTargetOffsetZ, SitTargetOrientW, SitTargetOrientX, SitTargetOrientY, SitTargetOrientZ FROM dbo.prims WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.prims

EXECUTE sp_rename N'dbo.Tmp_prims', N'prims', 'OBJECT' 

ALTER TABLE dbo.prims ADD CONSTRAINT
	PK__prims__10566F31 PRIMARY KEY CLUSTERED 
	(
	UUID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT