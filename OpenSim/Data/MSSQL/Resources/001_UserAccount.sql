CREATE TABLE [UserAccounts] (
  [PrincipalID] uniqueidentifier NOT NULL,
  [ScopeID] uniqueidentifier NOT NULL,
  [FirstName] [varchar](64) NOT NULL,
  [LastName] [varchar](64) NOT NULL,
  [Email] [varchar](64) NULL,
  [ServiceURLs] [text] NULL,
  [Created] [int] default NULL,
  
  PRIMARY KEY CLUSTERED
(
	[PrincipalID] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
