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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Data;
using NHibernate;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;
using OpenMetaverse;

namespace OpenSim.Data.NHibernate
{
    [Serializable]
    public class UUIDUserType: IUserType
    {
        public object Assemble(object cached, object owner)
        {
            return cached;
        }

        bool IUserType.Equals(object uuid1, object uuid2)
        {
            return uuid1.Equals(uuid2);
        }

        public object DeepCopy(object uuid)
        {
            return uuid;
        }

        public object Disassemble(object uuid)
        {
            return uuid;
        }

        public int GetHashCode(object uuid)
        {
            return (uuid == null) ? 0 : uuid.GetHashCode();
        }

        public bool IsMutable
        {
            get { return false; }
        }

        public object NullSafeGet(IDataReader rs, string[] names, object owner)
        {
            object uuid = null;

            int ord = rs.GetOrdinal(names[0]);
            if (!rs.IsDBNull(ord))
            {
                string first = (string)rs.GetString(ord);
                uuid = new UUID(first);
            }

            return uuid;
        }

        public void NullSafeSet(IDbCommand cmd, object obj, int index)
        {
            UUID uuid = (UUID)obj;
            ((IDataParameter)cmd.Parameters[index]).Value = uuid.ToString();
        }

        public object Replace(object original, object target, object owner)
        {
            return original;
        }

        public Type ReturnedType
        {
            get { return typeof(UUID); }
        }

        public SqlType[] SqlTypes
        {
            get { return new SqlType [] { NHibernateUtil.String.SqlType }; }
        }
    }
}
