--
-- Create schema avatar_appearance
--

SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON
SET ANSI_PADDING ON

CREATE TABLE [avatarappearance]  (
  [UUID]  uniqueidentifier NOT NULL,
  [Serial]  int NOT NULL,
  [WearableItem0]  uniqueidentifier NOT NULL,
  [WearableAsset0]  uniqueidentifier NOT NULL,
  [WearableItem1]  uniqueidentifier NOT NULL,
  [WearableAsset1]  uniqueidentifier NOT NULL,
  [WearableItem2]  uniqueidentifier NOT NULL,
  [WearableAsset2]  uniqueidentifier NOT NULL,
  [WearableItem3]  uniqueidentifier NOT NULL,
  [WearableAsset3]  uniqueidentifier NOT NULL,
  [WearableItem4]  uniqueidentifier NOT NULL,
  [WearableAsset4]  uniqueidentifier NOT NULL,
  [WearableItem5]  uniqueidentifier NOT NULL,
  [WearableAsset5]  uniqueidentifier NOT NULL,
  [WearableItem6]  uniqueidentifier NOT NULL,
  [WearableAsset6]  uniqueidentifier NOT NULL,
  [WearableItem7]  uniqueidentifier NOT NULL,
  [WearableAsset7]  uniqueidentifier NOT NULL,
  [WearableItem8]  uniqueidentifier NOT NULL,
  [WearableAsset8]  uniqueidentifier NOT NULL,
  [WearableItem9]  uniqueidentifier NOT NULL,
  [WearableAsset9]  uniqueidentifier NOT NULL,
  [WearableItem10]  uniqueidentifier NOT NULL,
  [WearableAsset10]  uniqueidentifier NOT NULL,
  [WearableItem11]  uniqueidentifier NOT NULL,
  [WearableAsset11]  uniqueidentifier NOT NULL,
  [WearableItem12]  uniqueidentifier NOT NULL,
  [WearableAsset12]  uniqueidentifier NOT NULL

  PRIMARY KEY  CLUSTERED (
  [UUID]
  ) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

SET ANSI_PADDING OFF
