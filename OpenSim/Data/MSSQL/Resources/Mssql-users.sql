SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

SET ANSI_PADDING ON

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
