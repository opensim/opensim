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
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Data
{

    public static class DBGuid
    {
        /// <summary>This function converts a value returned from the database in one of the
        /// supported formats into a UUID.  This function is not actually DBMS-specific right
        /// now
        ///
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static UUID FromDB(object id)
        {
            if ((id == null) || (id == DBNull.Value))
                return UUID.Zero;

            if (id.GetType() == typeof(Guid))
                return new UUID((Guid)id);

            if (id.GetType() == typeof(byte[]))
            {
                if (((byte[])id).Length == 0)
                    return UUID.Zero;
                else if (((byte[])id).Length == 16)
                    return new UUID((byte[])id, 0);
            }
            else if (id.GetType() == typeof(string))
            {
                if (((string)id).Length == 0)
                    return UUID.Zero;
                else if (((string)id).Length == 36)
                    return new UUID((string)id);
            }

            throw new Exception("Failed to convert db value to UUID: " + id.ToString());
        }
    }
}
