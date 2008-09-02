CREATE TABLE [dbo].[prims](
	[UUID] [varchar](255) NOT NULL,
	[RegionUUID] [varchar](255) NULL,
	[ParentID] [int] NULL,
	[CreationDate] [int] NULL,
	[Name] [varchar](255) NULL,
	[SceneGroupID] [varchar](255) NULL,
	[Text] [varchar](255) NULL,
	[Description] [varchar](255) NULL,
	[SitName] [varchar](255) NULL,
	[TouchName] [varchar](255) NULL,
	[ObjectFlags] [int] NULL,
	[CreatorID] [varchar](255) NULL,
	[OwnerID] [varchar](255) NULL,
	[GroupID] [varchar](255) NULL,
	[LastOwnerID] [varchar](255) NULL,
	[OwnerMask] [int] NULL,
	[NextOwnerMask] [int] NULL,
	[GroupMask] [int] NULL,
	[EveryoneMask] [int] NULL,
	[BaseMask] [int] NULL,
	[PositionX] [float] NULL,
	[PositionY] [float] NULL,
	[PositionZ] [float] NULL,
	[GroupPositionX] [float] NULL,
	[GroupPositionY] [float] NULL,
	[GroupPositionZ] [float] NULL,
	[VelocityX] [float] NULL,
	[VelocityY] [float] NULL,
	[VelocityZ] [float] NULL,
	[AngularVelocityX] [float] NULL,
	[AngularVelocityY] [float] NULL,
	[AngularVelocityZ] [float] NULL,
	[AccelerationX] [float] NULL,
	[AccelerationY] [float] NULL,
	[AccelerationZ] [float] NULL,
	[RotationX] [float] NULL,
	[RotationY] [float] NULL,
	[RotationZ] [float] NULL,
	[RotationW] [float] NULL,
	[SitTargetOffsetX] [float] NULL,
	[SitTargetOffsetY] [float] NULL,
	[SitTargetOffsetZ] [float] NULL,
	[SitTargetOrientW] [float] NULL,
	[SitTargetOrientX] [float] NULL,
	[SitTargetOrientY] [float] NULL,
	[SitTargetOrientZ] [float] NULL,
PRIMARY KEY CLUSTERED 
(
	[UUID] ASC
)WITH (PAD_INDEX  = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

CREATE TABLE [dbo].[primshapes](
	[UUID] [varchar](255) NOT NULL,
	[Shape] [int] NULL,
	[ScaleX] [float] NULL,
	[ScaleY] [float] NULL,
	[ScaleZ] [float] NULL,
	[PCode] [int] NULL,
	[PathBegin] [int] NULL,
	[PathEnd] [int] NULL,
	[PathScaleX] [int] NULL,
	[PathScaleY] [int] NULL,
	[PathShearX] [int] NULL,
	[PathShearY] [int] NULL,
	[PathSkew] [int] NULL,
	[PathCurve] [int] NULL,
	[PathRadiusOffset] [int] NULL,
	[PathRevolutions] [int] NULL,
	[PathTaperX] [int] NULL,
	[PathTaperY] [int] NULL,
	[PathTwist] [int] NULL,
	[PathTwistBegin] [int] NULL,
	[ProfileBegin] [int] NULL,
	[ProfileEnd] [int] NULL,
	[ProfileCurve] [int] NULL,
	[ProfileHollow] [int] NULL,
	[State] [int] NULL,
	[Texture] [image] NULL,
	[ExtraParams] [image] NULL,
PRIMARY KEY CLUSTERED 
(
	[UUID] ASC
)WITH (PAD_INDEX  = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

CREATE TABLE [dbo].[primitems](
	[itemID] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[primID] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[assetID] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[parentFolderID] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[invType] [int] NULL,
	[assetType] [int] NULL,
	[name] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[description] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[creationDate] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[creatorID] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ownerID] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[lastOwnerID] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[groupID] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[nextPermissions] [int] NULL,
	[currentPermissions] [int] NULL,
	[basePermissions] [int] NULL,
	[everyonePermissions] [int] NULL,
	[groupPermissions] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[itemID] ASC
)WITH (PAD_INDEX  = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

CREATE TABLE [dbo].[terrain](
	[RegionUUID] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Revision] [int] NULL,
	[Heightfield] [image] NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

CREATE TABLE [dbo].[land](
	[UUID] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RegionUUID] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[LocalLandID] [int] NULL,
	[Bitmap] [image] NULL,
	[Name] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Description] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[OwnerUUID] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[IsGroupOwned] [int] NULL,
	[Area] [int] NULL,
	[AuctionID] [int] NULL,
	[Category] [int] NULL,
	[ClaimDate] [int] NULL,
	[ClaimPrice] [int] NULL,
	[GroupUUID] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[SalePrice] [int] NULL,
	[LandStatus] [int] NULL,
	[LandFlags] [int] NULL,
	[LandingType] [int] NULL,
	[MediaAutoScale] [int] NULL,
	[MediaTextureUUID] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[MediaURL] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[MusicURL] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[PassHours] [float] NULL,
	[PassPrice] [int] NULL,
	[SnapshotUUID] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UserLocationX] [float] NULL,
	[UserLocationY] [float] NULL,
	[UserLocationZ] [float] NULL,
	[UserLookAtX] [float] NULL,
	[UserLookAtY] [float] NULL,
	[UserLookAtZ] [float] NULL,
PRIMARY KEY CLUSTERED 
(
	[UUID] ASC
)WITH (PAD_INDEX  = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

CREATE TABLE [dbo].[landaccesslist](
	[LandUUID] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AccessUUID] [varchar](255) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Flags] [int] NULL
) ON [PRIMARY]