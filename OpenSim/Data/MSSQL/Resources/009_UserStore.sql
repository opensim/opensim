BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_avatarappearance
	(
	Owner uniqueidentifier NOT NULL,
	Serial int NOT NULL,
	Visual_Params image NOT NULL,
	Texture image NOT NULL,
	Avatar_Height float(53) NOT NULL,
	Body_Item uniqueidentifier NOT NULL,
	Body_Asset uniqueidentifier NOT NULL,
	Skin_Item uniqueidentifier NOT NULL,
	Skin_Asset uniqueidentifier NOT NULL,
	Hair_Item uniqueidentifier NOT NULL,
	Hair_Asset uniqueidentifier NOT NULL,
	Eyes_Item uniqueidentifier NOT NULL,
	Eyes_Asset uniqueidentifier NOT NULL,
	Shirt_Item uniqueidentifier NOT NULL,
	Shirt_Asset uniqueidentifier NOT NULL,
	Pants_Item uniqueidentifier NOT NULL,
	Pants_Asset uniqueidentifier NOT NULL,
	Shoes_Item uniqueidentifier NOT NULL,
	Shoes_Asset uniqueidentifier NOT NULL,
	Socks_Item uniqueidentifier NOT NULL,
	Socks_Asset uniqueidentifier NOT NULL,
	Jacket_Item uniqueidentifier NOT NULL,
	Jacket_Asset uniqueidentifier NOT NULL,
	Gloves_Item uniqueidentifier NOT NULL,
	Gloves_Asset uniqueidentifier NOT NULL,
	Undershirt_Item uniqueidentifier NOT NULL,
	Undershirt_Asset uniqueidentifier NOT NULL,
	Underpants_Item uniqueidentifier NOT NULL,
	Underpants_Asset uniqueidentifier NOT NULL,
	Skirt_Item uniqueidentifier NOT NULL,
	Skirt_Asset uniqueidentifier NOT NULL
	)  ON [PRIMARY]
	 TEXTIMAGE_ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.avatarappearance)
	 EXEC('INSERT INTO dbo.Tmp_avatarappearance (Owner, Serial, Visual_Params, Texture, Avatar_Height, Body_Item, Body_Asset, Skin_Item, Skin_Asset, Hair_Item, Hair_Asset, Eyes_Item, Eyes_Asset, Shirt_Item, Shirt_Asset, Pants_Item, Pants_Asset, Shoes_Item, Shoes_Asset, Socks_Item, Socks_Asset, Jacket_Item, Jacket_Asset, Gloves_Item, Gloves_Asset, Undershirt_Item, Undershirt_Asset, Underpants_Item, Underpants_Asset, Skirt_Item, Skirt_Asset)
		SELECT CONVERT(uniqueidentifier, Owner), Serial, Visual_Params, Texture, Avatar_Height, CONVERT(uniqueidentifier, Body_Item), CONVERT(uniqueidentifier, Body_Asset), CONVERT(uniqueidentifier, Skin_Item), CONVERT(uniqueidentifier, Skin_Asset), CONVERT(uniqueidentifier, Hair_Item), CONVERT(uniqueidentifier, Hair_Asset), CONVERT(uniqueidentifier, Eyes_Item), CONVERT(uniqueidentifier, Eyes_Asset), CONVERT(uniqueidentifier, Shirt_Item), CONVERT(uniqueidentifier, Shirt_Asset), CONVERT(uniqueidentifier, Pants_Item), CONVERT(uniqueidentifier, Pants_Asset), CONVERT(uniqueidentifier, Shoes_Item), CONVERT(uniqueidentifier, Shoes_Asset), CONVERT(uniqueidentifier, Socks_Item), CONVERT(uniqueidentifier, Socks_Asset), CONVERT(uniqueidentifier, Jacket_Item), CONVERT(uniqueidentifier, Jacket_Asset), CONVERT(uniqueidentifier, Gloves_Item), CONVERT(uniqueidentifier, Gloves_Asset), CONVERT(uniqueidentifier, Undershirt_Item), CONVERT(uniqueidentifier, Undershirt_Asset), CONVERT(uniqueidentifier, Underpants_Item), CONVERT(uniqueidentifier, Underpants_Asset), CONVERT(uniqueidentifier, Skirt_Item), CONVERT(uniqueidentifier, Skirt_Asset) FROM dbo.avatarappearance WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.avatarappearance

EXECUTE sp_rename N'dbo.Tmp_avatarappearance', N'avatarappearance', 'OBJECT' 

ALTER TABLE dbo.avatarappearance ADD CONSTRAINT
	PK__avatarap__7DD115CC4E88ABD4 PRIMARY KEY CLUSTERED 
	(
	Owner
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
