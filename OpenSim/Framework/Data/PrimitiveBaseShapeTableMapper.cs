using System;
using OpenSim.Framework;
using TribalMedia.Framework.Data;
using libsecondlife;

namespace OpenSim.Framework.Data
{
    public class PrimitiveBaseShapeRowMapper : BaseRowMapper<PrimitiveBaseShape>
    {
        public Guid SceneObjectPartId;

        public PrimitiveBaseShapeRowMapper(BaseSchema schema, PrimitiveBaseShape obj) : base(schema, obj)
        {
        }
    }

    public class PrimitiveBaseShapeTableMapper : OpenSimTableMapper<PrimitiveBaseShapeRowMapper, Guid>
    {
        public PrimitiveBaseShapeTableMapper(BaseDatabaseConnector connection, string tableName)
            : base(connection, tableName)
        {
            BaseSchema<PrimitiveBaseShapeRowMapper> rowMapperSchema = new BaseSchema<PrimitiveBaseShapeRowMapper>(this);
            m_schema = rowMapperSchema;

            m_keyFieldMapper = rowMapperSchema.AddMapping<Guid>("SceneObjectPartId",
                                                                delegate(PrimitiveBaseShapeRowMapper shape) { return shape.SceneObjectPartId; },
                                                                delegate(PrimitiveBaseShapeRowMapper shape, Guid value) { shape.SceneObjectPartId = value; });

            rowMapperSchema.AddMapping<byte>("PCode",
                                             delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.PCode; },
                                             delegate(PrimitiveBaseShapeRowMapper shape, byte value) { shape.Object.PCode = value; });

            rowMapperSchema.AddMapping<ushort>("PathBegin",
                                               delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.PathBegin; },
                                               delegate(PrimitiveBaseShapeRowMapper shape, ushort value) { shape.Object.PathBegin = value; });

            rowMapperSchema.AddMapping<ushort>("PathEnd",
                                               delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.PathEnd; },
                                               delegate(PrimitiveBaseShapeRowMapper shape, ushort value) { shape.Object.PathEnd = value; });

            rowMapperSchema.AddMapping<byte>("PathScaleX",
                                             delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.PathScaleX; },
                                             delegate(PrimitiveBaseShapeRowMapper shape, byte value) { shape.Object.PathScaleX = value; });

            rowMapperSchema.AddMapping<byte>("PathScaleY",
                                             delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.PathScaleY; },
                                             delegate(PrimitiveBaseShapeRowMapper shape, byte value) { shape.Object.PathScaleY = value; });

            rowMapperSchema.AddMapping<byte>("PathShearX",
                                             delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.PathShearX; },
                                             delegate(PrimitiveBaseShapeRowMapper shape, byte value) { shape.Object.PathShearX = value; });

            rowMapperSchema.AddMapping<byte>("PathShearY",
                                             delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.PathShearY; },
                                             delegate(PrimitiveBaseShapeRowMapper shape, byte value) { shape.Object.PathShearY = value; });

            rowMapperSchema.AddMapping<ushort>("ProfileBegin",
                                               delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.ProfileBegin; },
                                               delegate(PrimitiveBaseShapeRowMapper shape, ushort value) { shape.Object.ProfileBegin = value; });

            rowMapperSchema.AddMapping<ushort>("ProfileEnd",
                                               delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.ProfileEnd; },
                                               delegate(PrimitiveBaseShapeRowMapper shape, ushort value) { shape.Object.ProfileEnd = value; });

            rowMapperSchema.AddMapping<LLVector3>("Scale",
                                                       delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.Scale; },
                                                       delegate(PrimitiveBaseShapeRowMapper shape, LLVector3 value) { shape.Object.Scale = value; });

            rowMapperSchema.AddMapping<sbyte>("PathTaperX",
                                              delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.PathTaperX; },
                                              delegate(PrimitiveBaseShapeRowMapper shape, sbyte value) { shape.Object.PathTaperX = value; });

            rowMapperSchema.AddMapping<sbyte>("PathTaperY",
                                              delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.PathTaperY; },
                                              delegate(PrimitiveBaseShapeRowMapper shape, sbyte value) { shape.Object.PathTaperY = value; });

            rowMapperSchema.AddMapping<sbyte>("PathTwist",
                                              delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.PathTwist; },
                                              delegate(PrimitiveBaseShapeRowMapper shape, sbyte value) { shape.Object.PathTwist = value; });

            rowMapperSchema.AddMapping<sbyte>("PathRadiusOffset",
                                              delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.PathRadiusOffset; },
                                              delegate(PrimitiveBaseShapeRowMapper shape, sbyte value) { shape.Object.PathRadiusOffset = value; });

            rowMapperSchema.AddMapping<byte>("PathRevolutions",
                                             delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.PathRevolutions; },
                                             delegate(PrimitiveBaseShapeRowMapper shape, byte value) { shape.Object.PathRevolutions = value; });

            rowMapperSchema.AddMapping<sbyte>("PathTwistBegin",
                                              delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.PathTwistBegin; },
                                              delegate(PrimitiveBaseShapeRowMapper shape, sbyte value) { shape.Object.PathTwistBegin = value; });

            rowMapperSchema.AddMapping<byte>("PathCurve",
                                             delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.PathCurve; },
                                             delegate(PrimitiveBaseShapeRowMapper shape, byte value) { shape.Object.PathCurve = value; });

            rowMapperSchema.AddMapping<byte>("ProfileCurve",
                                             delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.ProfileCurve; },
                                             delegate(PrimitiveBaseShapeRowMapper shape, byte value) { shape.Object.ProfileCurve = value; });

            rowMapperSchema.AddMapping<ushort>("ProfileHollow",
                                               delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.ProfileHollow; },
                                               delegate(PrimitiveBaseShapeRowMapper shape, ushort value) { shape.Object.ProfileHollow = value; });

            rowMapperSchema.AddMapping<byte[]>("TextureEntry",
                                               delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.TextureEntry; },
                                               delegate(PrimitiveBaseShapeRowMapper shape, byte[] value) { shape.Object.TextureEntry = value; });

            rowMapperSchema.AddMapping<byte[]>("ExtraParams",
                                               delegate(PrimitiveBaseShapeRowMapper shape) { return shape.Object.ExtraParams; },
                                               delegate(PrimitiveBaseShapeRowMapper shape, byte[] value) { shape.Object.ExtraParams = value; });            
        }

        public override PrimitiveBaseShapeRowMapper FromReader(BaseDataReader reader)
        {
            PrimitiveBaseShape shape = new PrimitiveBaseShape();

            PrimitiveBaseShapeRowMapper mapper = new PrimitiveBaseShapeRowMapper(m_schema, shape);
            mapper.FillObject( reader );

            return mapper;
        }

        public bool Update(Guid sceneObjectPartId, PrimitiveBaseShape primitiveBaseShape)
        {
            PrimitiveBaseShapeRowMapper mapper = CreateRowMapper(sceneObjectPartId, primitiveBaseShape);
            return Update(sceneObjectPartId, mapper);
        }

        public bool Add(Guid sceneObjectPartId, PrimitiveBaseShape primitiveBaseShape)
        {
            PrimitiveBaseShapeRowMapper mapper = CreateRowMapper(sceneObjectPartId, primitiveBaseShape);
            return Add(mapper);
        }

        private PrimitiveBaseShapeRowMapper CreateRowMapper(Guid sceneObjectPartId, PrimitiveBaseShape primitiveBaseShape)
        {
            PrimitiveBaseShapeRowMapper mapper = new PrimitiveBaseShapeRowMapper( m_schema, primitiveBaseShape );
            mapper.SceneObjectPartId = sceneObjectPartId;
            return mapper;
        }
    }
}
