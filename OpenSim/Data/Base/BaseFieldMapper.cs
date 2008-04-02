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
using System.Collections.Generic;
using System.Data.Common;

namespace OpenSim.Data.Base
{  
    public delegate TField ObjectGetAccessor<TObj, TField>(TObj obj);
    public delegate void ObjectSetAccessor<TObj, TField>(TObj obj, TField value);

    public abstract class BaseFieldMapper
    {
        private readonly BaseTableMapper m_tableMapper;
        private readonly string m_fieldName;

        public string FieldName
        {
            get { return m_fieldName; }
        }

        protected Type m_valueType;

        public Type ValueType
        {
            get { return m_valueType; }
        }

        public abstract object GetParamValue(object obj);

        public BaseFieldMapper(BaseTableMapper tableMapper, string fieldName, Type valueType)
        {
            m_fieldName = fieldName;
            m_valueType = valueType;
            m_tableMapper = tableMapper;
        }

        public abstract void SetPropertyFromReader(object mapper, BaseDataReader reader);

        public void RawAddParam(DbCommand command, List<string> fieldNames, string fieldName, object value)
        {
            string paramName = m_tableMapper.CreateParamName(fieldName);
            fieldNames.Add(fieldName);

            DbParameter param = command.CreateParameter();
            param.ParameterName = paramName;
            param.Value = value;

            command.Parameters.Add(param);
        }

        public virtual void ExpandField<TObj>(TObj obj, DbCommand command, List<string> fieldNames)
        {
            string fieldName = FieldName;
            object value = GetParamValue(obj);

            RawAddParam(command, fieldNames, fieldName, m_tableMapper.ConvertToDbType(value));
        }

        protected virtual object GetValue(BaseDataReader reader)
        {
            object value;

            if (ValueType == typeof(Guid))
            {
                value = reader.GetGuid(m_fieldName);
            }
            else if (ValueType == typeof(bool))
            {
                uint boolVal = reader.GetUShort(m_fieldName);
                value = (boolVal == 1);
            }
            else
                if (ValueType == typeof(byte))
                {
                    value = reader.GetByte(m_fieldName);
                }
                else if (ValueType == typeof(sbyte))
                {
                    value = reader.GetSByte(m_fieldName);
                }
                else if (ValueType == typeof(ushort))
                {
                    value = reader.GetUShort(m_fieldName);
                }
                else if (ValueType == typeof(uint))
                {
                    value = reader.GetUInt32(m_fieldName);
                }
                else if (ValueType == typeof(byte[]))
                {
                    value = reader.GetBytes(m_fieldName);
                }
                else
                {
                    value = reader.Get(m_fieldName);
                }

            if (value is DBNull)
            {
                value = default(ValueType);
            }

            return value;
        }
    }

    public class ObjectField<TObject, TField> : BaseFieldMapper
    {
        private readonly ObjectGetAccessor<TObject, TField> m_fieldGetAccessor;
        private readonly ObjectSetAccessor<TObject, TField> m_fieldSetAccessor;

        public override object GetParamValue(object obj)
        {
            return m_fieldGetAccessor((TObject)obj);
        }

        public override void SetPropertyFromReader(object obj, BaseDataReader reader)
        {
            object value;

            value = GetValue(reader);

            if (value == null)
            {
                m_fieldSetAccessor((TObject)obj, default(TField));
            }
            else
            {
                m_fieldSetAccessor((TObject)obj, (TField)value);
            }
        }


        public ObjectField(BaseTableMapper tableMapper, string fieldName, ObjectGetAccessor<TObject, TField> rowMapperGetAccessor,
                           ObjectSetAccessor<TObject, TField> rowMapperSetAccessor)
            : base(tableMapper, fieldName, typeof(TField))
        {
            m_fieldGetAccessor = rowMapperGetAccessor;
            m_fieldSetAccessor = rowMapperSetAccessor;
        }
    }
}
