BEGIN TRANSACTION

CREATE TABLE [dbo].[estate_managers](
	[EstateID] [int] NOT NULL,
	[uuid] [varchar](36) NOT NULL,
 CONSTRAINT [PK_estate_managers] PRIMARY KEY CLUSTERED 
(
	[EstateID] ASC
)WITH (PAD_INDEX  = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];

CREATE TABLE [dbo].[estate_groups](
	[EstateID] [int] NOT NULL,
	[uuid] [varchar](36) NOT NULL,
 CONSTRAINT [PK_estate_groups] PRIMARY KEY CLUSTERED 
(
	[EstateID] ASC
)WITH (PAD_INDEX  = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];


CREATE TABLE [dbo].[estate_users](
	[EstateID] [int] NOT NULL,
	[uuid] [varchar](36) NOT NULL,
 CONSTRAINT [PK_estate_users] PRIMARY KEY CLUSTERED 
(
	[EstateID] ASC
)WITH (PAD_INDEX  = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];


CREATE TABLE [dbo].[estateban](
	[EstateID] [int] NOT NULL,
	[bannedUUID] [varchar](36) NOT NULL,
	[bannedIp] [varchar](16) NOT NULL,
	[bannedIpHostMask] [varchar](16) NOT NULL,
	[bannedNameMask] [varchar](64) NULL DEFAULT (NULL),
 CONSTRAINT [PK_estateban] PRIMARY KEY CLUSTERED 
(
	[EstateID] ASC
)WITH (PAD_INDEX  = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];

CREATE TABLE [dbo].[estate_settings](
	[EstateID] [int] IDENTITY(1,100) NOT NULL,
	[EstateName] [varchar](64) NULL DEFAULT (NULL),
	[AbuseEmailToEstateOwner] [bit] NOT NULL,
	[DenyAnonymous] [bit] NOT NULL,
	[ResetHomeOnTeleport] [bit] NOT NULL,
	[FixedSun] [bit] NOT NULL,
	[DenyTransacted] [bit] NOT NULL,
	[BlockDwell] [bit] NOT NULL,
	[DenyIdentified] [bit] NOT NULL,
	[AllowVoice] [bit] NOT NULL,
	[UseGlobalTime] [bit] NOT NULL,
	[PricePerMeter] [int] NOT NULL,
	[TaxFree] [bit] NOT NULL,
	[AllowDirectTeleport] [bit] NOT NULL,
	[RedirectGridX] [int] NOT NULL,
	[RedirectGridY] [int] NOT NULL,
	[ParentEstateID] [int] NOT NULL,
	[SunPosition] [float] NOT NULL,
	[EstateSkipScripts] [bit] NOT NULL,
	[BillableFactor] [float] NOT NULL,
	[PublicAccess] [bit] NOT NULL,
	[AbuseEmail] [varchar](255) NOT NULL,
	[EstateOwner] [varchar](36) NOT NULL,
	[DenyMinors] [bit] NOT NULL,
 CONSTRAINT [PK_estate_settings] PRIMARY KEY CLUSTERED 
(
	[EstateID] ASC
)WITH (PAD_INDEX  = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];


CREATE TABLE [dbo].[estate_map](
	[RegionID] [varchar](36) NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000'),
	[EstateID] [int] NOT NULL,
 CONSTRAINT [PK_estate_map] PRIMARY KEY CLUSTERED 
(
	[RegionID] ASC
)WITH (PAD_INDEX  = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];

COMMIT