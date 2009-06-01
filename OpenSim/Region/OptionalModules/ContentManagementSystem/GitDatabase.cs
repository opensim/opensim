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

#region Header

// GitDatabase.cs 
//
//
//

#endregion Header

using System;
using System.Collections.Generic;
using System.IO;
using Slash = System.IO.Path;
using System.Reflection;
using System.Xml;

using OpenMetaverse;

using Nini.Config;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.Physics.Manager;

using log4net;

namespace OpenSim.Region.OptionalModules.ContentManagement
{
    /// <summary>
    /// Just a stub :-(
    /// </summary>
    public class GitDatabase : IContentDatabase
    {
        #region Constructors

        public GitDatabase()
        {
        }

        #endregion Constructors

        #region Public Methods

        public SceneObjectGroup GetMostRecentObjectRevision(UUID id)
        {
            return null;
        }

        public int GetMostRecentRevision(UUID regionid)
        {
            return 0;
        }

        public SceneObjectGroup GetObjectRevision(UUID id, int revision)
        {
            return null;
        }

        public System.Collections.ArrayList GetObjectsFromRegion(UUID regionid, int revision)
        {
            return null;
        }

        public string GetRegionObjectHeightMap(UUID regionid)
        {
            return null;
        }

        public string GetRegionObjectHeightMap(UUID regionid, int revision)
        {
            return null;
        }

        public string GetRegionObjectXML(UUID regionid)
        {
            return null;
        }

        public string GetRegionObjectXML(UUID regionid, int revision)
        {
            return null;
        }

        public System.Collections.ArrayList GetRegionObjectXMLList(UUID regionid)
        {
            return null;
        }

        public System.Collections.ArrayList GetRegionObjectXMLList(UUID regionid, int revision)
        {
            return null;
        }

        public bool InRepository(UUID id)
        {
            return false;
        }

        public void Initialise(Scene scene, String dir)
        {
        }

        public System.Collections.Generic.SortedDictionary<string, string> ListOfObjectRevisions(UUID id)
        {
            return null;
        }

        public System.Collections.Generic.SortedDictionary<string, string> ListOfRegionRevisions(UUID id)
        {
            return null;
        }

        public int NumOfObjectRev(UUID id)
        {
            return 0;
        }

        public int NumOfRegionRev(UUID regionid)
        {
            return 0;
        }

        public void PostInitialise()
        {
        }

        public void SaveObject(SceneObjectGroup entity)
        {
        }

        public void SaveRegion(UUID regionid, string regionName, string logMessage)
        {
        }

        #endregion Public Methods
    }
}
