BEGIN TRANSACTION

CREATE TABLE [Friends] (
[PrincipalID] uniqueidentifier NOT NULL, 
[Friend] varchar(255) NOT NULL, 
[Flags] char(16) NOT NULL DEFAULT '0',
[Offered] varchar(32) NOT NULL DEFAULT 0)
 ON [PRIMARY]


COMMIT