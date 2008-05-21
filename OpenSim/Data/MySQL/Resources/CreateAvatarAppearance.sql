--
-- Create schema avatar_appearance
--

DROP TABLE IF EXISTS `avatarappearance`;
CREATE TABLE `avatarappearance` (
  Owner char(36) NOT NULL,
  Serial int(10) unsigned NOT NULL,
  Visual_Params blob NOT NULL,
  Texture blob NOT NULL,
  Avatar_Height float NOT NULL,
  Body_Item char(36) NOT NULL,
  Body_Asset char(36) NOT NULL,
  Skin_Item char(36) NOT NULL,
  Skin_Asset char(36) NOT NULL,
  Hair_Item char(36) NOT NULL,
  Hair_Asset char(36) NOT NULL,
  Eyes_Item char(36) NOT NULL,
  Eyes_Asset char(36) NOT NULL,
  Shirt_Item char(36) NOT NULL,
  Shirt_Asset char(36) NOT NULL,
  Pants_Item char(36) NOT NULL,
  Pants_Asset char(36) NOT NULL,
  Shoes_Item char(36) NOT NULL,
  Shoes_Asset char(36) NOT NULL,
  Socks_Item char(36) NOT NULL,
  Socks_Asset char(36) NOT NULL,
  Jacket_Item char(36) NOT NULL,
  Jacket_Asset char(36) NOT NULL,
  Gloves_Item char(36) NOT NULL,
  Gloves_Asset char(36) NOT NULL,
  Undershirt_Item char(36) NOT NULL,
  Undershirt_Asset char(36) NOT NULL,
  Underpants_Item char(36) NOT NULL,
  Underpants_Asset char(36) NOT NULL,
  Skirt_Item char(36) NOT NULL,
  Skirt_Asset char(36) NOT NULL,


  PRIMARY KEY  (`Owner`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Rev.2';

