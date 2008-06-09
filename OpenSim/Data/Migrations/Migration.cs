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
using System.Data;
using System.IO;
using System.Reflection;

namespace OpenSim.Data.Migrations
{
    /// <summary> 
    ///
    /// The Migration theory is based on the ruby on rails concept.
    /// Each database driver is going to be allowed to have files in
    /// Resources that specify the database migrations.  They will be
    /// of the form:
    ///
    ///    001_Users.sql
    ///    002_Users.sql
    ///    003_Users.sql
    ///    001_Prims.sql
    ///    002_Prims.sql
    ///    ...etc...
    ///
    /// When a database driver starts up, it specifies a resource that
    /// needs to be brought up to the current revision.  For instance:
    ///
    ///    Migration um = new Migration("Users");
    ///    um.Upgrade(dbconnection);
    ///
    /// This works out which version Users is at, and applies all the
    /// revisions past it to it.  If there is no users table, all
    /// revisions are applied in order.  Consider each future
    /// migration to be an incremental roll forward of the tables in
    /// question.
    ///
    /// </summary>

    public class Migration
    {
        private string _type;

        public Migration(string type)
        {
            _type = type;
        }


    }

    
}