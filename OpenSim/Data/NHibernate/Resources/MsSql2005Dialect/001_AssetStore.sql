create table Assets (
   ID NVARCHAR(36) not null,
   Type SMALLINT null,
   Name NVARCHAR(64) null,
   Description NVARCHAR(64) null,
   Local BIT null,
   Temporary BIT null,
   Data VARBINARY(max) null,
   primary key (ID)
)
