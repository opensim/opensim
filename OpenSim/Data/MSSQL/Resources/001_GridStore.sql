BEGIN TRANSACTION

CREATE TABLE [dbo].[regions](
 [regionHandle] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [regionName] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [uuid] [varchar](255) COLLATE Latin1_General_CI_AS NOT NULL,
 [regionRecvKey] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [regionSecret] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [regionSendKey] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [regionDataURI] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [serverIP] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [serverPort] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [serverURI] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [locX] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [locY] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [locZ] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [eastOverrideHandle] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [westOverrideHandle] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [southOverrideHandle] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [northOverrideHandle] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [regionAssetURI] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [regionAssetRecvKey] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [regionAssetSendKey] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [regionUserURI] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [regionUserRecvKey] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [regionUserSendKey] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [regionMapTexture] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [serverHttpPort] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [serverRemotingPort] [varchar](255) COLLATE Latin1_General_CI_AS NULL,
 [owner_uuid] [varchar](36) COLLATE Latin1_General_CI_AS NULL,
PRIMARY KEY CLUSTERED 
(
 [uuid] ASC
)WITH (PAD_INDEX  = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]

COMMIT
