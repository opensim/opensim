using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework;
using TribalMedia.Framework.Data;

namespace OpenSim.Region.Environment.Modules
{
    public class AppearanceRowMapper : BaseRowMapper<AvatarAppearance>
    {

        public AppearanceRowMapper(BaseSchema schema, AvatarAppearance obj)
            : base(schema, obj)
        {
        }
    }

    public class AppearanceTableMapper : BaseTableMapper<AppearanceRowMapper, Guid>
    {
        public AppearanceTableMapper(BaseDatabaseConnector database, string tableName)
            : base(database, tableName)
        {
            BaseSchema<AppearanceRowMapper> rowMapperSchema = new BaseSchema<AppearanceRowMapper>(this);
            m_schema = rowMapperSchema;

            m_keyFieldMapper = rowMapperSchema.AddMapping<Guid>("UUID",
       delegate(AppearanceRowMapper mapper) { return mapper.Object.ScenePresenceID.UUID; },
       delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.ScenePresenceID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<uint>("Serial",
                  delegate(AppearanceRowMapper mapper) { return (uint)mapper.Object.WearablesSerial; },
       delegate(AppearanceRowMapper mapper, uint value) { mapper.Object.WearablesSerial = (int)value; });

            rowMapperSchema.AddMapping<Guid>("WearableItem0",
                 delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[0].ItemID.UUID; },
      delegate(AppearanceRowMapper mapper, Guid value)
      {
          if (mapper.Object.Wearables == null)
          {
              mapper.Object.Wearables = new OpenSim.Framework.AvatarWearable[13];
              for (int i = 0; i < 13; i++)
              {
                  mapper.Object.Wearables[i] = new AvatarWearable();
              }
          }
          mapper.Object.Wearables[0].ItemID = new libsecondlife.LLUUID(value.ToString());
      });

            rowMapperSchema.AddMapping<Guid>("WearableAsset0",
                delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[0].AssetID.UUID; },
                delegate(AppearanceRowMapper mapper, Guid value)
                { mapper.Object.Wearables[0].AssetID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem1",
                 delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[1].ItemID.UUID; },
      delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[1].ItemID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset1",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[1].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[1].AssetID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem2",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[2].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[2].ItemID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset2",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[2].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[2].AssetID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem3",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[3].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[3].ItemID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset3",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[3].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[3].AssetID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem4",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[4].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[4].ItemID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset4",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[4].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[4].AssetID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem5",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[5].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[5].ItemID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset5",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[5].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[5].AssetID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem6",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[6].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[6].ItemID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset6",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[6].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[6].AssetID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem7",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[7].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[7].ItemID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset7",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[7].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[7].AssetID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem8",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[8].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[8].ItemID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset8",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[8].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[8].AssetID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem9",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[9].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[9].ItemID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset9",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[9].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[9].AssetID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem10",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[10].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[10].ItemID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset10",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[10].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[10].AssetID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem11",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[11].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[11].ItemID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset11",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[11].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[11].AssetID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem12",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[12].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[12].ItemID = new libsecondlife.LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset12",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[12].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[12].AssetID = new libsecondlife.LLUUID(value.ToString()); });

        }

        public bool Add(Guid userID, AvatarAppearance appearance)
        {
            AppearanceRowMapper mapper = CreateRowMapper(appearance);
            return Add(mapper);
        }

        public bool Update(Guid userID, AvatarAppearance appearance)
        {
            AppearanceRowMapper mapper = CreateRowMapper(appearance);
            return Update(appearance.ScenePresenceID.UUID, mapper);
        }

        protected AppearanceRowMapper CreateRowMapper(AvatarAppearance appearance)
        {
            return new AppearanceRowMapper(m_schema, appearance);
        }

        protected AppearanceRowMapper CreateRowMapper()
        {
            return CreateRowMapper(new AvatarAppearance());
        }

        protected AppearanceRowMapper FromReader(BaseDataReader reader, AvatarAppearance appearance)
        {
            AppearanceRowMapper mapper = CreateRowMapper(appearance);
            mapper.FillObject(reader);
            return mapper;
        }

        public override AppearanceRowMapper FromReader(BaseDataReader reader)
        {
            AppearanceRowMapper mapper = CreateRowMapper();
            mapper.FillObject(reader);
            return mapper;
        }

        public bool TryGetValue(Guid presenceID, out AvatarAppearance val)
        {
            AppearanceRowMapper mapper;
            if (TryGetValue(presenceID, out mapper))
            {
                val = mapper.Object;
                return true;
            }
            else
            {
                val = null;
                return false;
            }
        }
    }
}
