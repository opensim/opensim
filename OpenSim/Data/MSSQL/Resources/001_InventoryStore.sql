BEGIN TRANSACTION

CREATE TABLE [inventoryfolders] (
  [folderID] [varchar](36) NOT NULL default '',
  [agentID] [varchar](36) default NULL,
  [parentFolderID] [varchar](36) default NULL,
  [folderName] [varchar](64) default NULL,
  [type] [smallint] NOT NULL default 0,
  [version] [int] NOT NULL default 0,  
  PRIMARY KEY CLUSTERED
(
	[folderID] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

CREATE NONCLUSTERED INDEX [owner] ON [inventoryfolders]
(
	[agentID] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]

CREATE NONCLUSTERED INDEX [parent] ON [inventoryfolders]
(
	[parentFolderID] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]


CREATE TABLE [inventoryitems] (
  [inventoryID] [varchar](36) NOT NULL default '',
  [assetID] [varchar](36) default NULL,
  [assetType] [int] default NULL,
  [parentFolderID] [varchar](36) default NULL,
  [avatarID] [varchar](36) default NULL,
  [inventoryName] [varchar](64) default NULL,
  [inventoryDescription] [varchar](128) default NULL,
  [inventoryNextPermissions] [int] default NULL,
  [inventoryCurrentPermissions] [int] default NULL,
  [invType] [int] default NULL,
  [creatorID] [varchar](36) default NULL,
  [inventoryBasePermissions] [int] NOT NULL default 0,
  [inventoryEveryOnePermissions] [int] NOT NULL default 0,
  [salePrice] [int] default NULL,
  [saleType] [tinyint] default NULL,
  [creationDate] [int] default NULL,
  [groupID] [varchar](36) default NULL,
  [groupOwned] [bit] default NULL,
  [flags] [int] default NULL,
  PRIMARY KEY CLUSTERED
(
	[inventoryID] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]


CREATE NONCLUSTERED INDEX [owner] ON [inventoryitems]
(
	[avatarID] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]

CREATE NONCLUSTERED INDEX [folder] ON [inventoryitems]
(
	[parentFolderID] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]

COMMIT
