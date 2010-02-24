BEGIN TRANSACTION

CREATE TABLE [auth] (
  [uuid] [uniqueidentifier] NOT NULL default '00000000-0000-0000-0000-000000000000',
  [passwordHash] [varchar](32) NOT NULL,
  [passwordSalt] [varchar](32) NOT NULL,
  [webLoginKey] [varchar](255) NOT NULL,
  [accountType] VARCHAR(32) NOT NULL DEFAULT 'UserAccount',
) ON [PRIMARY]

CREATE TABLE [tokens] (
  [uuid] [uniqueidentifier] NOT NULL default '00000000-0000-0000-0000-000000000000',
  [token] [varchar](255) NOT NULL,
  [validity] [datetime] NOT NULL )
  ON [PRIMARY]

COMMIT