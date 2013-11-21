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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;

namespace OpenSim.Data.Null
{
    public class NullXGroupData : NullGenericDataHandler, IXGroupData
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<UUID, XGroup> m_groups = new Dictionary<UUID, XGroup>();

        public NullXGroupData(string connectionString, string realm) {}

        public bool StoreGroup(XGroup group)
        {
            lock (m_groups)
            {
                m_groups[group.groupID] = group.Clone();
            }

            return true;
        }

        public XGroup[] GetGroups(string field, string val)
        {
            return GetGroups(new string[] { field }, new string[] { val });
        }

        public XGroup[] GetGroups(string[] fields, string[] vals)
        {
            lock (m_groups)
            {
                List<XGroup> origGroups = Get<XGroup>(fields, vals, m_groups.Values.ToList());

                return origGroups.Select(g => g.Clone()).ToArray();
            }
        }

        public bool DeleteGroups(string field, string val)
        {
            return DeleteGroups(new string[] { field }, new string[] { val });
        }

        public bool DeleteGroups(string[] fields, string[] vals)
        {
            lock (m_groups)
            {
                XGroup[] groupsToDelete = GetGroups(fields, vals);
                Array.ForEach(groupsToDelete, g => m_groups.Remove(g.groupID));
            }

            return true;
        }
    }
}