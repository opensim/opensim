--
-- Create schema avatar_appearance
--

SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
SET ANSI_PADDING ON

CREATE TABLE [avatarappearance]  (
  [Owner]  [varchar](36) NOT NULL,
  [Serial]  int NOT NULL,
  [Visual_Params] [image] NOT NULL,
  [Texture] [image] NOT NULL,
  [Avatar_Height] float NOT NULL,
  [Body_Item] [varchar](36) NOT NULL,
  [Body_Asset] [varchar](36) NOT NULL,
  [Skin_Item] [varchar](36) NOT NULL,
  [Skin_Asset] [varchar](36) NOT NULL,
  [Hair_Item] [varchar](36) NOT NULL,
  [Hair_Asset] [varchar](36) NOT NULL,
  [Eyes_Item] [varchar](36) NOT NULL,
  [Eyes_Asset] [varchar](36) NOT NULL,
  [Shirt_Item] [varchar](36) NOT NULL,
  [Shirt_Asset] [varchar](36) NOT NULL,
  [Pants_Item] [varchar](36) NOT NULL,
  [Pants_Asset] [varchar](36) NOT NULL,
  [Shoes_Item] [varchar](36) NOT NULL,
  [Shoes_Asset] [varchar](36) NOT NULL,
  [Socks_Item] [varchar](36) NOT NULL,
  [Socks_Asset] [varchar](36) NOT NULL,
  [Jacket_Item] [varchar](36) NOT NULL,
  [Jacket_Asset] [varchar](36) NOT NULL,
  [Gloves_Item] [varchar](36) NOT NULL,
  [Gloves_Asset] [varchar](36) NOT NULL,
  [Undershirt_Item] [varchar](36) NOT NULL,
  [Undershirt_Asset] [varchar](36) NOT NULL,
  [Underpants_Item] [varchar](36) NOT NULL,
  [Underpants_Asset] [varchar](36) NOT NULL,
  [Skirt_Item] [varchar](36) NOT NULL,
  [Skirt_Asset] [varchar](36) NOT NULL,

  PRIMARY KEY  CLUSTERED (
  [Owner]
  ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

SET ANSI_PADDING OFF
