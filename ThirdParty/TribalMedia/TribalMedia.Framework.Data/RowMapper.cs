/*
* Copyright (c) Tribal Media AB, http://tribalmedia.se/
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * The name of Tribal Media AB may not be used to endorse or promote products
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
* 
*/

using TribalMedia.Framework.Data;

namespace TribalMedia.Framework.Data
{
    public abstract class RowMapper
    {
        public abstract void FillObject(DataReader reader);
    }

    public class ObjectMapper<TObj> : RowMapper
    {
        private readonly Schema m_schema;
        private readonly TObj m_obj;

        public TObj Object
        {
            get { return m_obj; }
        }

        public ObjectMapper(Schema schema, TObj obj)
        {
            m_schema = schema;
            m_obj = obj;
        }

        public override void FillObject(DataReader reader)
        {
            foreach (FieldMapper fieldMapper in m_schema.Fields.Values)
            {
                fieldMapper.SetPropertyFromReader(m_obj, reader);
            }
        }
    }

    public class RowMapper<TObj> : RowMapper
    {
        private readonly Schema m_schema;
        private readonly TObj m_obj;

        public TObj Object
        {
            get { return m_obj; }
        }

        public RowMapper(Schema schema, TObj obj)
        {
            m_schema = schema;
            m_obj = obj;
        }

        public override void FillObject(DataReader reader)
        {
            foreach (FieldMapper fieldMapper in m_schema.Fields.Values)
            {
                fieldMapper.SetPropertyFromReader(this, reader);
            }
        }
    }
}