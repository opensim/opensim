BEGIN TRANSACTION

CREATE TABLE [avatarattachments] (
	[UUID] varchar(36) NOT NULL
	, [attachpoint] int NOT NULL
	, [item] varchar(36) NOT NULL
	, [asset] varchar(36) NOT NULL)

CREATE NONCLUSTERED INDEX IX_avatarattachments ON dbo.avatarattachments
	(
	UUID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]


COMMIT
