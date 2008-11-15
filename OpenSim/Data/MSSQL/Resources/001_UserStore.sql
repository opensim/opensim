CREATE TABLE [users] (
  [UUID] [varchar](36) NOT NULL default '',
  [username] [varchar](32) NOT NULL,
  [lastname] [varchar](32) NOT NULL,
  [passwordHash] [varchar](32) NOT NULL,
  [passwordSalt] [varchar](32) NOT NULL,
  [homeRegion] [bigint] default NULL,
  [homeLocationX] [float] default NULL,
  [homeLocationY] [float] default NULL,
  [homeLocationZ] [float] default NULL,
  [homeLookAtX] [float] default NULL,
  [homeLookAtY] [float] default NULL,
  [homeLookAtZ] [float] default NULL,
  [created] [int] NOT NULL,
  [lastLogin] [int] NOT NULL,
  [userInventoryURI] [varchar](255) default NULL,
  [userAssetURI] [varchar](255) default NULL,
  [profileCanDoMask] [int] default NULL,
  [profileWantDoMask] [int] default NULL,
  [profileAboutText] [ntext],
  [profileFirstText] [ntext],
  [profileImage] [varchar](36) default NULL,
  [profileFirstImage] [varchar](36) default NULL,
  [webLoginKey] [varchar](36) default NULL,
  PRIMARY KEY CLUSTERED
(
	[UUID] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]


CREATE NONCLUSTERED INDEX [usernames] ON [users]
(
	[username] ASC,
	[lastname] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]


CREATE TABLE [agents] (
  [UUID] [varchar](36) NOT NULL,
  [sessionID] [varchar](36) NOT NULL,
  [secureSessionID] [varchar](36) NOT NULL,
  [agentIP] [varchar](16) NOT NULL,
  [agentPort] [int] NOT NULL,
  [agentOnline] [tinyint] NOT NULL,
  [loginTime] [int] NOT NULL,
  [logoutTime] [int] NOT NULL,
  [currentRegion] [varchar](36) NOT NULL,
  [currentHandle] [bigint] NOT NULL,
  [currentPos] [varchar](64) NOT NULL,
  PRIMARY KEY CLUSTERED
(
	[UUID] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]


CREATE NONCLUSTERED INDEX [session] ON [agents]
(
	[sessionID] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]

CREATE NONCLUSTERED INDEX [ssession] ON [agents]
(
	[secureSessionID] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]


CREATE TABLE [dbo].[userfriends](
	[ownerID] [varchar](50) COLLATE Latin1_General_CI_AS NOT NULL,
	[friendID] [varchar](50) COLLATE Latin1_General_CI_AS NOT NULL,
	[friendPerms] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[datetimestamp] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL
) ON [PRIMARY]

CREATE TABLE [avatarappearance]  (
  [Owner]  [varchar](36) NOT NULL,
  [Serial]  int NOT NULL,
  [Visual_Params] [image] NOT NULL,
  [Texture] [image] NOT NULL,
  [Avatar_Height] float NOT NULL,
  [Body_Item] [varchar](36) NOT NULL,
  [Body_Asset] [varchar](36) NOT NULL,
  [Skin_Item] [varchar](36) NOT NULL,
  [Skin_Asset] [varchar](36) NOT NULL,
  [Hair_Item] [varchar](36) NOT NULL,
  [Hair_Asset] [varchar](36) NOT NULL,
  [Eyes_Item] [varchar](36) NOT NULL,
  [Eyes_Asset] [varchar](36) NOT NULL,
  [Shirt_Item] [varchar](36) NOT NULL,
  [Shirt_Asset] [varchar](36) NOT NULL,
  [Pants_Item] [varchar](36) NOT NULL,
  [Pants_Asset] [varchar](36) NOT NULL,
  [Shoes_Item] [varchar](36) NOT NULL,
  [Shoes_Asset] [varchar](36) NOT NULL,
  [Socks_Item] [varchar](36) NOT NULL,
  [Socks_Asset] [varchar](36) NOT NULL,
  [Jacket_Item] [varchar](36) NOT NULL,
  [Jacket_Asset] [varchar](36) NOT NULL,
  [Gloves_Item] [varchar](36) NOT NULL,
  [Gloves_Asset] [varchar](36) NOT NULL,
  [Undershirt_Item] [varchar](36) NOT NULL,
  [Undershirt_Asset] [varchar](36) NOT NULL,
  [Underpants_Item] [varchar](36) NOT NULL,
  [Underpants_Asset] [varchar](36) NOT NULL,
  [Skirt_Item] [varchar](36) NOT NULL,
  [Skirt_Asset] [varchar](36) NOT NULL,

  PRIMARY KEY  CLUSTERED (
  [Owner]
  ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
