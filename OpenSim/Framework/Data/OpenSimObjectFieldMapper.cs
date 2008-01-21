using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using libsecondlife;
/*
using TribalMedia.Framework.Data;

namespace OpenSim.Framework.Data
{
    public class OpenSimObjectFieldMapper<TObject, TField> : ObjectField<TObject, TField>
    {
        public OpenSimObjectFieldMapper(BaseTableMapper tableMapper, string fieldName,
                                        ObjectGetAccessor<TObject, TField> rowMapperGetAccessor,
                                        ObjectSetAccessor<TObject, TField> rowMapperSetAccessor)
            : base(tableMapper, fieldName, rowMapperGetAccessor, rowMapperSetAccessor)
        {
        }

        public override void ExpandField<TObj>(TObj obj, DbCommand command, List<string> fieldNames)
        {
            string fieldName = FieldName;
            object value = GetParamValue(obj);

            if (ValueType == typeof(LLVector3))
            {
                LLVector3 vector = (LLVector3)value;

                RawAddParam(command, fieldNames, fieldName + "X", vector.X);
                RawAddParam(command, fieldNames, fieldName + "Y", vector.Y);
                RawAddParam(command, fieldNames, fieldName + "Z", vector.Z);
            }
            else if (ValueType == typeof(LLQuaternion))
            {
                LLQuaternion quaternion = (LLQuaternion)value;

                RawAddParam(command, fieldNames, fieldName + "X", quaternion.X);
                RawAddParam(command, fieldNames, fieldName + "Y", quaternion.Y);
                RawAddParam(command, fieldNames, fieldName + "Z", quaternion.Z);
                RawAddParam(command, fieldNames, fieldName + "W", quaternion.W);
            }
            else
            {
                base.ExpandField(obj, command, fieldNames);
            }
        }

        protected override object GetValue(BaseDataReader reader)
        {
            object value;

            OpenSimDataReader osreader = (OpenSimDataReader) reader;

            if (ValueType == typeof(LLVector3))
            {
                value = osreader.GetVector(FieldName);
            }
            else if (ValueType == typeof(LLQuaternion))
            {
                value = osreader.GetQuaternion(FieldName);
            }
            else if (ValueType == typeof(LLUUID))
            {
                Guid guid = reader.GetGuid(FieldName);
                value = new LLUUID(guid);
            }
            else
            {
                value = base.GetValue(reader);
            }

            return value;
        }      
    }
}
*/
