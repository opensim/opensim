/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using libsecondlife;
using OpenSim.Data.Base;
using OpenSim.Framework;

namespace OpenSim.Data.Base
{
    /// <summary>
    /// 
    /// </summary>
    public class AppearanceRowMapper : BaseRowMapper<AvatarAppearance>
    {
        public AppearanceRowMapper(BaseSchema schema, AvatarAppearance obj)
            : base(schema, obj)
        {
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class AppearanceTableMapper : BaseTableMapper<AppearanceRowMapper, Guid>
    {
        public AppearanceTableMapper(BaseDatabaseConnector database, string tableName)
            : base(database, tableName)
        {
            BaseSchema<AppearanceRowMapper> rowMapperSchema = new BaseSchema<AppearanceRowMapper>(this);
            m_schema = rowMapperSchema;

            m_keyFieldMapper = rowMapperSchema.AddMapping<Guid>("UUID",
       delegate(AppearanceRowMapper mapper) { return mapper.Object.Owner.UUID; },
       delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Owner = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<uint>("Serial",
                  delegate(AppearanceRowMapper mapper) { return (uint)mapper.Object.Serial; },
       delegate(AppearanceRowMapper mapper, uint value) { mapper.Object.Serial = (int)value; });

            rowMapperSchema.AddMapping<Guid>("WearableItem0",
                 delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[0].ItemID.UUID; },
      delegate(AppearanceRowMapper mapper, Guid value)
      {
          if (mapper.Object.Wearables == null)
          {
              mapper.Object.Wearables = new AvatarWearable[13];
              for (int i = 0; i < 13; i++)
              {
                  mapper.Object.Wearables[i] = new AvatarWearable();
              }
          }
          mapper.Object.Wearables[0].ItemID = new LLUUID(value.ToString());
      });

            rowMapperSchema.AddMapping<Guid>("WearableAsset0",
                delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[0].AssetID.UUID; },
                delegate(AppearanceRowMapper mapper, Guid value)
                { mapper.Object.Wearables[0].AssetID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem1",
                 delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[1].ItemID.UUID; },
      delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[1].ItemID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset1",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[1].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[1].AssetID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem2",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[2].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[2].ItemID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset2",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[2].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[2].AssetID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem3",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[3].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[3].ItemID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset3",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[3].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[3].AssetID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem4",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[4].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[4].ItemID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset4",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[4].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[4].AssetID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem5",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[5].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[5].ItemID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset5",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[5].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[5].AssetID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem6",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[6].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[6].ItemID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset6",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[6].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[6].AssetID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem7",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[7].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[7].ItemID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset7",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[7].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[7].AssetID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem8",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[8].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[8].ItemID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset8",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[8].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[8].AssetID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem9",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[9].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[9].ItemID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset9",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[9].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[9].AssetID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem10",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[10].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[10].ItemID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset10",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[10].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[10].AssetID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem11",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[11].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[11].ItemID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset11",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[11].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[11].AssetID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableItem12",
              delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[12].ItemID.UUID; },
   delegate(AppearanceRowMapper mapper, Guid value) { mapper.Object.Wearables[12].ItemID = new LLUUID(value.ToString()); });

            rowMapperSchema.AddMapping<Guid>("WearableAsset12",
               delegate(AppearanceRowMapper mapper) { return mapper.Object.Wearables[12].AssetID.UUID; },
               delegate(AppearanceRowMapper mapper, Guid value)
               { mapper.Object.Wearables[12].AssetID = new LLUUID(value.ToString()); });

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="appearance"></param>
        /// <returns></returns>
        public bool Add(Guid userID, AvatarAppearance appearance)
        {
            AppearanceRowMapper mapper = CreateRowMapper(appearance);
            return Add(mapper);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="appearance"></param>
        /// <returns></returns>
        public bool Update(Guid userID, AvatarAppearance appearance)
        {
            AppearanceRowMapper mapper = CreateRowMapper(appearance);
            return Update(appearance.Owner.UUID, mapper);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="appearance"></param>
        /// <returns></returns>
        protected AppearanceRowMapper CreateRowMapper(AvatarAppearance appearance)
        {
            return new AppearanceRowMapper(m_schema, appearance);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected AppearanceRowMapper CreateRowMapper()
        {
            return CreateRowMapper(new AvatarAppearance());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="appearance"></param>
        /// <returns></returns>
        protected AppearanceRowMapper FromReader(BaseDataReader reader, AvatarAppearance appearance)
        {
            AppearanceRowMapper mapper = CreateRowMapper(appearance);
            mapper.FillObject(reader);
            return mapper;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public override AppearanceRowMapper FromReader(BaseDataReader reader)
        {
            AppearanceRowMapper mapper = CreateRowMapper();
            mapper.FillObject(reader);
            return mapper;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="presenceID"></param>
        /// <param name="val"></param>
        /// <returns></returns>
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
