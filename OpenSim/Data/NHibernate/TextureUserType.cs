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
using OpenSim.Framework;

namespace OpenSim.Data.NHibernate
{
    [Serializable]
    public class TextureUserType: IUserType
    {
        public object Assemble(object cached, object owner)
        {
            return cached;
        }

        bool IUserType.Equals(object texture1, object texture2)
        {
            return texture1.Equals(texture2);
        }

        public object DeepCopy(object texture)
        {
            if (texture == null) 
            {
                // TODO: should parametrize this texture out
                return new Primitive.TextureEntry(new UUID(Constants.DefaultTexture));
            }
            else 
            {
                byte[] bytes = ((Primitive.TextureEntry)texture).GetBytes();
                return new Primitive.TextureEntry(bytes, 0, bytes.Length);
            }
        }

        public object Disassemble(object texture)
        {
            return texture;
        }

        public int GetHashCode(object texture)
        {
            return (texture == null) ? 0 : texture.GetHashCode();
        }

        public bool IsMutable
        {
            get { return false; }
        }

        public object NullSafeGet(IDataReader rs, string[] names, object owner)
        {
            object texture = null;

            int ord = rs.GetOrdinal(names[0]);
            if (!rs.IsDBNull(ord))
            {
                byte[] bytes = (byte[])rs[ord];
                texture = new Primitive.TextureEntry(bytes, 0, bytes.Length);
            }

            return texture;
        }

        public void NullSafeSet(IDbCommand cmd, object obj, int index)
        {
            Primitive.TextureEntry texture = (Primitive.TextureEntry)obj;
            ((IDataParameter)cmd.Parameters[index]).Value = texture.GetBytes();
        }

        public object Replace(object original, object target, object owner)
        {
            return original;
        }

        public Type ReturnedType
        {
            get { return typeof(Primitive.TextureEntry); }
        }

        public SqlType[] SqlTypes
        {
            get { return new SqlType [] { NHibernateUtil.Binary.SqlType }; }
        }
    }
}
