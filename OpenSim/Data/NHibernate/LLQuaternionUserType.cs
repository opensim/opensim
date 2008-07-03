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
using System.Data;
using libsecondlife;
using NHibernate;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;

namespace OpenSim.Data.NHibernate
{
    [Serializable]
    public class LLQuaternionUserType: IUserType
    {
        public object Assemble(object cached, object owner)
        {
            return cached;
        }

        bool IUserType.Equals(object quat1, object quat2)
        {
            return quat1.Equals(quat2);
        }

        public object DeepCopy(object quat)
        {
            // silly libsecondlife having no copy constructor
            LLQuaternion q = (LLQuaternion) quat;
            return new LLQuaternion(q.X, q.Y, q.Z, q.W);
        }

        public object Disassemble(object quat)
        {
            return quat;
        }

        public int GetHashCode(object quat)
        {
            return (quat == null) ? 0 : quat.GetHashCode();
        }

        public bool IsMutable
        {
            get { return false; }
        }

        public object NullSafeGet(IDataReader rs, string[] names, object owner)
        {
            object quat = null;
                        
            int x = rs.GetOrdinal(names[0]);
            int y = rs.GetOrdinal(names[1]);
            int z = rs.GetOrdinal(names[2]);
            int w = rs.GetOrdinal(names[3]);
            if (!rs.IsDBNull(x))
            {
                quat = new LLQuaternion((Single)rs[x], (Single)rs[y], (Single)rs[z], (Single)rs[w]);
            }
            return quat;
        }

        public void NullSafeSet(IDbCommand cmd, object obj, int index)
        {
            LLQuaternion quat = (LLQuaternion)obj;
            ((IDataParameter)cmd.Parameters[index]).Value = quat.X;
            ((IDataParameter)cmd.Parameters[index + 1]).Value = quat.Y;
            ((IDataParameter)cmd.Parameters[index + 2]).Value = quat.Z;
            ((IDataParameter)cmd.Parameters[index + 3]).Value = quat.W;
        }

        public object Replace(object original, object target, object owner)
        {
            return original;
        }

        public Type ReturnedType
        {
            get { return typeof(LLQuaternion); }
        }

        public SqlType[] SqlTypes
        {
            get { return new SqlType [] { 
                    NHibernateUtil.Single.SqlType, 
                    NHibernateUtil.Single.SqlType, 
                    NHibernateUtil.Single.SqlType, 
                    NHibernateUtil.Single.SqlType
                }; }
        }
    }
}
