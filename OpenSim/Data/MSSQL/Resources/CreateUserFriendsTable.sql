SET ANSI_NULLS ON

SET QUOTED_IDENTIFIER ON

SET ANSI_PADDING ON

CREATE TABLE [dbo].[userfriends](
[ownerID] [varchar](50) COLLATE Latin1_General_CI_AS NOT NULL,
[friendID] [varchar](50) COLLATE Latin1_General_CI_AS NOT NULL,
[friendPerms] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
[datetimestamp] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL
) ON [PRIMARY]

SET ANSI_PADDING OFF
