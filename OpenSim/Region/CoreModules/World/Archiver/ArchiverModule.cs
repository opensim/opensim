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
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Archiver
{
    /// <summary>
    /// This module loads and saves OpenSimulator region archives
    /// </summary>
    public class ArchiverModule : INonSharedRegionModule, IRegionArchiverModule
    {
        private static readonly ILog m_log = 
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Scene Scene { get; private set; }
        public IRegionCombinerModule RegionCombinerModule { get; private set; }

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
            RegionCombinerModule = scene.RequestModuleInterface<IRegionCombinerModule>();
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
            bool skipAssets = false;
            
            OptionSet options = new OptionSet().Add("m|merge", delegate (string v) { mergeOar = v != null; });
            options.Add("s|skip-assets", delegate (string v) { skipAssets = v != null; });

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

            if (mainParams.Count > 2)
            {
                DearchiveRegion(mainParams[2], mergeOar, skipAssets, Guid.Empty);
            }
            else
            {
                DearchiveRegion(DEFAULT_OAR_BACKUP_FILENAME, mergeOar, skipAssets, Guid.Empty);
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

            if (!ConsoleUtil.CheckFileDoesNotExist(MainConsole.Instance, path))
                return;

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
            DearchiveRegion(loadPath, false, false, Guid.Empty);
        }
        
        public void DearchiveRegion(string loadPath, bool merge, bool skipAssets, Guid requestId)
        {
            m_log.InfoFormat(
                "[ARCHIVER]: Loading archive to region {0} from {1}", Scene.RegionInfo.RegionName, loadPath);
            
            new ArchiveReadRequest(Scene, loadPath, merge, skipAssets, requestId).DearchiveRegion();
        }
        
        public void DearchiveRegion(Stream loadStream)
        {
            DearchiveRegion(loadStream, false, false, Guid.Empty);
        }
        
        public void DearchiveRegion(Stream loadStream, bool merge, bool skipAssets, Guid requestId)
        {
            new ArchiveReadRequest(Scene, loadStream, merge, skipAssets, requestId).DearchiveRegion();
        }
    }
}
