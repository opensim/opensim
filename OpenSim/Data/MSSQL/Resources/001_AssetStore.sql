CREATE TABLE [assets] (
  [id] [varchar](36) NOT NULL,
  [name] [varchar](64) NOT NULL,
  [description] [varchar](64) NOT NULL,
  [assetType] [tinyint] NOT NULL,
  [local] [tinyint] NOT NULL,
  [temporary] [tinyint] NOT NULL,
  [data] [image] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
