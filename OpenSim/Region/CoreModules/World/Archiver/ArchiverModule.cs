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
using System.IO;
using System.Reflection;
using log4net;
using NDesk.Options;
using Nini.Config;
using Mono.Addins;

using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using OpenMetaverse;

namespace OpenSim.Region.CoreModules.World.Archiver
{
    /// <summary>
    /// This module loads and saves OpenSimulator region archives
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ArchiverModule")]
    public class ArchiverModule : INonSharedRegionModule, IRegionArchiverModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Scene Scene { get; private set; }

        /// <value>
        /// The file used to load and save an opensimulator archive if no filename has been specified
        /// </value>
        protected const string DEFAULT_OAR_BACKUP_FILENAME = "region.oar";

        public string Name
        {
            get { return "RegionArchiverModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }


        public void Initialise(IConfigSource source)
        {
            //m_log.Debug("[ARCHIVER] Initialising");
        }

        public void AddRegion(Scene scene)
        {
            Scene = scene;
            Scene.RegisterModuleInterface<IRegionArchiverModule>(this);
            //m_log.DebugFormat("[ARCHIVER]: Enabled for region {0}", scene.RegionInfo.RegionName);
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void Close()
        {
        }

        /// <summary>
        /// Load a whole region from an opensimulator archive.
        /// </summary>
        /// <param name="cmdparams"></param>
        public void HandleLoadOarConsoleCommand(string module, string[] cmdparams)
        {
            bool mergeOar = false;
            bool mergeReplaceObjects = false;
            bool skipAssets = false;
            bool mergeTerrain = false;
            bool mergeParcels = false;
            bool noObjects = false;
            Vector3 displacement = new Vector3(0f, 0f, 0f);
            String defaultUser = "";
            float rotation = 0f;
            Vector3 rotationCenter = new Vector3(Scene.RegionInfo.RegionSizeX / 2f, Scene.RegionInfo.RegionSizeY / 2f, 0);
            Vector3 boundingOrigin = new Vector3(0f, 0f, 0f);
            Vector3 boundingSize = new Vector3(Scene.RegionInfo.RegionSizeX, Scene.RegionInfo.RegionSizeY, float.MaxValue);
            bool debug = false;
            
            OptionSet options = new OptionSet();
            options.Add("m|merge", delegate(string v) { mergeOar = (v != null); });
            options.Add("mergeReplaceObjects", delegate (string v) { mergeReplaceObjects = (v != null); });
            options.Add("s|skip-assets", delegate(string v) { skipAssets = (v != null); });
            options.Add("merge-terrain", delegate(string v) { mergeTerrain = (v != null); });
            options.Add("force-terrain", delegate (string v) { mergeTerrain = (v != null); });   // downward compatibility
            options.Add("forceterrain", delegate (string v) { mergeTerrain = (v != null); });   // downward compatibility
            options.Add("merge-parcels", delegate(string v) { mergeParcels = (v != null); });
            options.Add("force-parcels", delegate (string v) { mergeParcels = (v != null); });   // downward compatibility
            options.Add("forceparcels", delegate (string v) { mergeParcels = (v != null); });   // downward compatibility
            options.Add("no-objects", delegate(string v) { noObjects = (v != null); });
            options.Add("default-user=", delegate(string v) { defaultUser = (v == null) ? "" : v; });
            options.Add("displacement=", delegate(string v)
            {
                try
                {
                    displacement = v == null ? Vector3.Zero : Vector3.Parse(v);
                }
                catch
                {
                    m_log.ErrorFormat("[ARCHIVER MODULE] failure parsing displacement");
                    m_log.ErrorFormat("[ARCHIVER MODULE]    Must be represented as vector3: --displacement \"<128,128,0>\"");
                    return;
                }
            });
            options.Add("rotation=", delegate(string v)
            {
                try
                {
                    rotation = v == null ? 0f : float.Parse(v);
                }
                catch
                {
                    m_log.ErrorFormat("[ARCHIVER MODULE] failure parsing rotation");
                    m_log.ErrorFormat("[ARCHIVER MODULE]    Must be an angle in degrees between -360 and +360: --rotation 45");
                    return;
                }
                //pass this in as degrees now, convert to radians later during actual work phase
                rotation = Util.Clamp<float>(rotation, -359f, 359f);
            });
            options.Add("rotation-center=", delegate(string v)
            {
                try
                {
                    m_log.Info("[ARCHIVER MODULE] Warning: --rotation-center no longer does anything and will be removed soon!");
                    rotationCenter = v == null ? Vector3.Zero : Vector3.Parse(v);
                }
                catch
                {
                    m_log.ErrorFormat("[ARCHIVER MODULE] failure parsing rotation displacement");
                    m_log.ErrorFormat("[ARCHIVER MODULE]    Must be represented as vector3: --rotation-center \"<128,128,0>\"");
                    return;
                }
            });
            options.Add("bounding-origin=", delegate(string v)
            {
                try
                {
                    boundingOrigin = v == null ? Vector3.Zero : Vector3.Parse(v);
                }
                catch
                {
                    m_log.ErrorFormat("[ARCHIVER MODULE] failure parsing bounding cube origin");
                    m_log.ErrorFormat("[ARCHIVER MODULE]    Must be represented as vector3: --bounding-origin \"<128,128,0>\"");
                    return;
                }
            });
            options.Add("bounding-size=", delegate(string v)
            {
                try
                {
                    boundingSize = v == null ? new Vector3(Scene.RegionInfo.RegionSizeX, Scene.RegionInfo.RegionSizeY, float.MaxValue) : Vector3.Parse(v);
                }
                catch
                {
                    m_log.ErrorFormat("[ARCHIVER MODULE] failure parsing bounding cube size");
                    m_log.ErrorFormat("[ARCHIVER MODULE]    Must be represented as a positive vector3: --bounding-size \"<256,256,4096>\"");
                    return;
                }
            });
            options.Add("d|debug", delegate(string v) { debug = (v != null); });

            // Send a message to the region ready module
            /* bluewall* Disable this for the time being
            IRegionReadyModule rready = m_scene.RequestModuleInterface<IRegionReadyModule>();

            if (rready != null)
            {
                rready.OarLoadingAlert("load");
            }
            */

            List<string> mainParams = options.Parse(cmdparams);

//            m_log.DebugFormat("MERGE OAR IS [{0}]", mergeOar);
//
//            foreach (string param in mainParams)
//                m_log.DebugFormat("GOT PARAM [{0}]", param);

            Dictionary<string, object> archiveOptions = new Dictionary<string, object>();
            if (mergeOar) archiveOptions.Add("merge", null);
            if (skipAssets) archiveOptions.Add("skipAssets", null);
            if (mergeReplaceObjects) archiveOptions.Add("mReplaceObjects", null);
            if (mergeTerrain) archiveOptions.Add("merge-terrain", null);
            if (mergeParcels) archiveOptions.Add("merge-parcels", null);
            if (noObjects) archiveOptions.Add("no-objects", null);
            if (defaultUser != "")
            {
                UUID defaultUserUUID = UUID.Zero;
                try
                {
                    defaultUserUUID = Scene.UserManagementModule.GetUserIdByName(defaultUser);
                }
                catch
                {
                    m_log.ErrorFormat("[ARCHIVER MODULE] default user must be in format \"First Last\"", defaultUser);
                }
                if (defaultUserUUID == UUID.Zero)
                {
                    m_log.ErrorFormat("[ARCHIVER MODULE] cannot find specified default user {0}", defaultUser);
                    return;
                }
                else
                {
                    archiveOptions.Add("default-user", defaultUserUUID);
                }
            }
            archiveOptions.Add("displacement", displacement);
            archiveOptions.Add("rotation", rotation);
            archiveOptions.Add("rotation-center", rotationCenter);
            archiveOptions.Add("bounding-origin", boundingOrigin);
            archiveOptions.Add("bounding-size", boundingSize);
            if (debug) archiveOptions.Add("debug", null);

            if (mainParams.Count > 2)
            {
                DearchiveRegion(mainParams[2], Guid.Empty, archiveOptions);
            }
            else
            {
                DearchiveRegion(DEFAULT_OAR_BACKUP_FILENAME, Guid.Empty, archiveOptions);
            }
        }

        /// <summary>
        /// Save a region to a file, including all the assets needed to restore it.
        /// </summary>
        /// <param name="cmdparams"></param>
        public void HandleSaveOarConsoleCommand(string module, string[] cmdparams)
        {
            Dictionary<string, object> options = new Dictionary<string, object>();

            OptionSet ops = new OptionSet();

            // legacy argument [obsolete]
            ops.Add("p|profile=", delegate(string v) { Console.WriteLine("\n WARNING: -profile option is obsolete and it will not work. Use -home instead.\n"); });
            // preferred
            ops.Add("h|home=", delegate(string v) { options["home"] = v; });

            ops.Add("noassets", delegate(string v) { options["noassets"] = v != null; });
            ops.Add("publish", v => options["wipe-owners"] = v != null);
            ops.Add("perm=", delegate(string v) { options["checkPermissions"] = v; });
            ops.Add("all", delegate(string v) { options["all"] = v != null; });

            List<string> mainParams = ops.Parse(cmdparams);

            string path;
            if (mainParams.Count > 2)
                path = mainParams[2];
            else
                path = DEFAULT_OAR_BACKUP_FILENAME;

            // Not doing this right now as this causes some problems with auto-backup systems.  Maybe a force flag is
            // needed
//            if (!ConsoleUtil.CheckFileDoesNotExist(MainConsole.Instance, path))
//                return;

            ArchiveRegion(path, options);
        }

        public void ArchiveRegion(string savePath, Dictionary<string, object> options)
        {
            ArchiveRegion(savePath, Guid.Empty, options);
        }

        public void ArchiveRegion(string savePath, Guid requestId, Dictionary<string, object> options)
        {
            m_log.InfoFormat(
                "[ARCHIVER]: Writing archive for region {0} to {1}", Scene.RegionInfo.RegionName, savePath);

            new ArchiveWriteRequest(Scene, savePath, requestId).ArchiveRegion(options);
        }

        public void ArchiveRegion(Stream saveStream)
        {
            ArchiveRegion(saveStream, Guid.Empty);
        }

        public void ArchiveRegion(Stream saveStream, Guid requestId)
        {
            ArchiveRegion(saveStream, requestId, new Dictionary<string, object>());
        }

        public void ArchiveRegion(Stream saveStream, Guid requestId, Dictionary<string, object> options)
        {
            new ArchiveWriteRequest(Scene, saveStream, requestId).ArchiveRegion(options);
        }

        public void DearchiveRegion(string loadPath)
        {
            Dictionary<string, object> archiveOptions = new Dictionary<string, object>();
            DearchiveRegion(loadPath, Guid.Empty, archiveOptions);
        }

        public void DearchiveRegion(string loadPath, Guid requestId, Dictionary<string, object> options)
        {
            m_log.InfoFormat(
                "[ARCHIVER]: Loading archive to region {0} from {1}", Scene.RegionInfo.RegionName, loadPath);

            new ArchiveReadRequest(Scene, loadPath, requestId, options).DearchiveRegion();
        }

        public void DearchiveRegion(Stream loadStream)
        {
            Dictionary<string, object> archiveOptions = new Dictionary<string, object>();
            DearchiveRegion(loadStream, Guid.Empty, archiveOptions);
        }
        public void DearchiveRegion(Stream loadStream, Guid requestId, Dictionary<string, object> options)
        {
            new ArchiveReadRequest(Scene, loadStream, requestId, options).DearchiveRegion();
        }
    }
}
