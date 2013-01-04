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
using System.Net;
using log4net;
using Nini.Config;
using OpenMetaverse;
using Mono.Addins;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Framework.InterfaceCommander;
using OpenSim.Region.CoreModules.World.Terrain.FileLoaders;
using OpenSim.Region.CoreModules.World.Terrain.FloodBrushes;
using OpenSim.Region.CoreModules.World.Terrain.PaintBrushes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Terrain
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "TerrainModule")]
    public class TerrainModule : INonSharedRegionModule, ICommandableModule, ITerrainModule
    {
        #region StandardTerrainEffects enum

        /// <summary>
        /// A standard set of terrain brushes and effects recognised by viewers
        /// </summary>
        public enum StandardTerrainEffects : byte
        {
            Flatten = 0,
            Raise = 1,
            Lower = 2,
            Smooth = 3,
            Noise = 4,
            Revert = 5,

            // Extended brushes
            Erode = 255,
            Weather = 254,
            Olsen = 253
        }

        #endregion

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        private readonly Commander m_commander = new Commander("terrain");

        private readonly Dictionary<StandardTerrainEffects, ITerrainFloodEffect> m_floodeffects =
            new Dictionary<StandardTerrainEffects, ITerrainFloodEffect>();

        private readonly Dictionary<string, ITerrainLoader> m_loaders = new Dictionary<string, ITerrainLoader>();

        private readonly Dictionary<StandardTerrainEffects, ITerrainPaintableEffect> m_painteffects =
            new Dictionary<StandardTerrainEffects, ITerrainPaintableEffect>();

        private ITerrainChannel m_channel;
        private Dictionary<string, ITerrainEffect> m_plugineffects;
        private ITerrainChannel m_revert;
        private Scene m_scene;
        private volatile bool m_tainted;
        private readonly Stack<LandUndoState> m_undo = new Stack<LandUndoState>(5);

        private String m_InitialTerrain = "pinhead-island";

        /// <summary>
        /// Human readable list of terrain file extensions that are supported.
        /// </summary>
        private string m_supportedFileExtensions = "";

        //For terrain save-tile file extensions
        private string m_supportFileExtensionsForTileSave = "";

        #region ICommandableModule Members

        public ICommander CommandInterface
        {
            get { return m_commander; }
        }

        #endregion

        #region INonSharedRegionModule Members

        /// <summary>
        /// Creates and initialises a terrain module for a region
        /// </summary>
        /// <param name="scene">Region initialising</param>
        /// <param name="config">Config for the region</param>
        public void Initialise(IConfigSource config)
        {
            IConfig terrainConfig = config.Configs["Terrain"];
            if (terrainConfig != null)
                m_InitialTerrain = terrainConfig.GetString("InitialTerrain", m_InitialTerrain);
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;

            // Install terrain module in the simulator
            lock (m_scene)
            {
                if (m_scene.Heightmap == null)
                {
                    m_channel = new TerrainChannel(m_InitialTerrain);
                    m_scene.Heightmap = m_channel;
                    m_revert = new TerrainChannel();
                    UpdateRevertMap();
                }
                else
                {
                    m_channel = m_scene.Heightmap;
                    m_revert = new TerrainChannel();
                    UpdateRevertMap();
                }

                m_scene.RegisterModuleInterface<ITerrainModule>(this);
                m_scene.EventManager.OnNewClient += EventManager_OnNewClient;
                m_scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
                m_scene.EventManager.OnTerrainTick += EventManager_OnTerrainTick;
            }

            InstallDefaultEffects();
            LoadPlugins();

            // Generate user-readable extensions list
            string supportedFilesSeparator = "";
            string supportedFilesSeparatorForTileSave = "";

            m_supportFileExtensionsForTileSave = "";
            foreach (KeyValuePair<string, ITerrainLoader> loader in m_loaders)
            {
                m_supportedFileExtensions += supportedFilesSeparator + loader.Key + " (" + loader.Value + ")";
                supportedFilesSeparator = ", ";

                //For terrain save-tile file extensions
                if (loader.Value.SupportsTileSave() == true)
                {
                    m_supportFileExtensionsForTileSave += supportedFilesSeparatorForTileSave + loader.Key + " (" + loader.Value + ")";
                    supportedFilesSeparatorForTileSave = ", ";
                }
            }
        }

        public void RegionLoaded(Scene scene)
        {
            //Do this here to give file loaders time to initialize and
            //register their supported file extensions and file formats.
            InstallInterfaces();
        }

        public void RemoveRegion(Scene scene)
        {
            lock (m_scene)
            {
                // remove the commands
                m_scene.UnregisterModuleCommander(m_commander.Name);
                // remove the event-handlers
                m_scene.EventManager.OnTerrainTick -= EventManager_OnTerrainTick;
                m_scene.EventManager.OnPluginConsole -= EventManager_OnPluginConsole;
                m_scene.EventManager.OnNewClient -= EventManager_OnNewClient;
                // remove the interface
                m_scene.UnregisterModuleInterface<ITerrainModule>(this);
            }
        }

        public void Close()
        {
        }

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        public string Name
        {
            get { return "TerrainModule"; }
        }

        #endregion

        #region ITerrainModule Members

        public void UndoTerrain(ITerrainChannel channel)
        {
            m_channel = channel;
        }

        /// <summary>
        /// Loads a terrain file from disk and installs it in the scene.
        /// </summary>
        /// <param name="filename">Filename to terrain file. Type is determined by extension.</param>
        public void LoadFromFile(string filename)
        {
            foreach (KeyValuePair<string, ITerrainLoader> loader in m_loaders)
            {
                if (filename.EndsWith(loader.Key))
                {
                    lock (m_scene)
                    {
                        try
                        {
                            ITerrainChannel channel = loader.Value.LoadFile(filename);
                            if (channel.Width != Constants.RegionSize || channel.Height != Constants.RegionSize)
                            {
                                // TerrainChannel expects a RegionSize x RegionSize map, currently
                                throw new ArgumentException(String.Format("wrong size, use a file with size {0} x {1}",
                                                                          Constants.RegionSize, Constants.RegionSize));
                            }
                            m_log.DebugFormat("[TERRAIN]: Loaded terrain, wd/ht: {0}/{1}", channel.Width, channel.Height);
                            m_scene.Heightmap = channel;
                            m_channel = channel;
                            UpdateRevertMap();
                        }
                        catch (NotImplementedException)
                        {
                            m_log.Error("[TERRAIN]: Unable to load heightmap, the " + loader.Value +
                                        " parser does not support file loading. (May be save only)");
                            throw new TerrainException(String.Format("unable to load heightmap: parser {0} does not support loading", loader.Value));
                        }
                        catch (FileNotFoundException)
                        {
                            m_log.Error(
                                "[TERRAIN]: Unable to load heightmap, file not found. (A directory permissions error may also cause this)");
                            throw new TerrainException(
                                String.Format("unable to load heightmap: file {0} not found (or permissions do not allow access", filename));
                        }
                        catch (ArgumentException e)
                        {
                            m_log.ErrorFormat("[TERRAIN]: Unable to load heightmap: {0}", e.Message);
                            throw new TerrainException(
                                String.Format("Unable to load heightmap: {0}", e.Message));
                        }
                    }
                    CheckForTerrainUpdates();
                    m_log.Info("[TERRAIN]: File (" + filename + ") loaded successfully");
                    return;
                }
            }

            m_log.Error("[TERRAIN]: Unable to load heightmap, no file loader available for that format.");
            throw new TerrainException(String.Format("unable to load heightmap from file {0}: no loader available for that format", filename));
        }

        /// <summary>
        /// Saves the current heightmap to a specified file.
        /// </summary>
        /// <param name="filename">The destination filename</param>
        public void SaveToFile(string filename)
        {
            try
            {
                foreach (KeyValuePair<string, ITerrainLoader> loader in m_loaders)
                {
                    if (filename.EndsWith(loader.Key))
                    {
                        loader.Value.SaveFile(filename, m_channel);
                        m_log.InfoFormat("[TERRAIN]: Saved terrain from {0} to {1}", m_scene.RegionInfo.RegionName, filename);
                        return;
                    }
                }
            }
            catch (IOException ioe)
            {
                m_log.Error(String.Format("[TERRAIN]: Unable to save to {0}, {1}", filename, ioe.Message));
            }

            m_log.ErrorFormat(
                "[TERRAIN]: Could not save terrain from {0} to {1}.  Valid file extensions are {2}",
                m_scene.RegionInfo.RegionName, filename, m_supportedFileExtensions);
        }

        /// <summary>
        /// Loads a terrain file from the specified URI
        /// </summary>
        /// <param name="filename">The name of the terrain to load</param>
        /// <param name="pathToTerrainHeightmap">The URI to the terrain height map</param>
        public void LoadFromStream(string filename, Uri pathToTerrainHeightmap)
        {
            LoadFromStream(filename, URIFetch(pathToTerrainHeightmap));
        }

        /// <summary>
        /// Loads a terrain file from a stream and installs it in the scene.
        /// </summary>
        /// <param name="filename">Filename to terrain file. Type is determined by extension.</param>
        /// <param name="stream"></param>
        public void LoadFromStream(string filename, Stream stream)
        {
            foreach (KeyValuePair<string, ITerrainLoader> loader in m_loaders)
            {
                if (filename.EndsWith(loader.Key))
                {
                    lock (m_scene)
                    {
                        try
                        {
                            ITerrainChannel channel = loader.Value.LoadStream(stream);
                            m_scene.Heightmap = channel;
                            m_channel = channel;
                            UpdateRevertMap();
                        }
                        catch (NotImplementedException)
                        {
                            m_log.Error("[TERRAIN]: Unable to load heightmap, the " + loader.Value +
                                        " parser does not support file loading. (May be save only)");
                            throw new TerrainException(String.Format("unable to load heightmap: parser {0} does not support loading", loader.Value));
                        }
                    }

                    CheckForTerrainUpdates();
                    m_log.Info("[TERRAIN]: File (" + filename + ") loaded successfully");
                    return;
                }
            }
            m_log.Error("[TERRAIN]: Unable to load heightmap, no file loader available for that format.");
            throw new TerrainException(String.Format("unable to load heightmap from file {0}: no loader available for that format", filename));
        }

        private static Stream URIFetch(Uri uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);

            // request.Credentials = credentials;

            request.ContentLength = 0;
            request.KeepAlive = false;

            WebResponse response = request.GetResponse();
            Stream file = response.GetResponseStream();

            if (response.ContentLength == 0)
                throw new Exception(String.Format("{0} returned an empty file", uri.ToString()));

            // return new BufferedStream(file, (int) response.ContentLength);
            return new BufferedStream(file, 1000000);
        }

        /// <summary>
        /// Modify Land
        /// </summary>
        /// <param name="pos">Land-position (X,Y,0)</param>
        /// <param name="size">The size of the brush (0=small, 1=medium, 2=large)</param>
        /// <param name="action">0=LAND_LEVEL, 1=LAND_RAISE, 2=LAND_LOWER, 3=LAND_SMOOTH, 4=LAND_NOISE, 5=LAND_REVERT</param>
        /// <param name="agentId">UUID of script-owner</param>
        public void ModifyTerrain(UUID user, Vector3 pos, byte size, byte action, UUID agentId)
        {
            float duration = 0.25f;
            if (action == 0)
                duration = 4.0f;

            client_OnModifyTerrain(user, (float)pos.Z, duration, size, action, pos.Y, pos.X, pos.Y, pos.X, agentId);
        }

        /// <summary>
        /// Saves the current heightmap to a specified stream.
        /// </summary>
        /// <param name="filename">The destination filename.  Used here only to identify the image type</param>
        /// <param name="stream"></param>
        public void SaveToStream(string filename, Stream stream)
        {
            try
            {
                foreach (KeyValuePair<string, ITerrainLoader> loader in m_loaders)
                {
                    if (filename.EndsWith(loader.Key))
                    {
                        loader.Value.SaveStream(stream, m_channel);
                        return;
                    }
                }
            }
            catch (NotImplementedException)
            {
                m_log.Error("Unable to save to " + filename + ", saving of this file format has not been implemented.");
                throw new TerrainException(String.Format("Unable to save heightmap: saving of this file format not implemented"));
            }
        }

        public void TaintTerrain ()
        {
            CheckForTerrainUpdates();
        }

        #region Plugin Loading Methods

        private void LoadPlugins()
        {
            m_plugineffects = new Dictionary<string, ITerrainEffect>();
            LoadPlugins(Assembly.GetCallingAssembly());
            string plugineffectsPath = "Terrain";
            
            // Load the files in the Terrain/ dir
            if (!Directory.Exists(plugineffectsPath))
                return;
            
            string[] files = Directory.GetFiles(plugineffectsPath);
            foreach (string file in files)
            {
                m_log.Info("Loading effects in " + file);
                try
                {
                    Assembly library = Assembly.LoadFrom(file);
                    LoadPlugins(library);
                }
                catch (BadImageFormatException)
                {
                }
            }
        }

        private void LoadPlugins(Assembly library)
        {
            foreach (Type pluginType in library.GetTypes())
            {
                try
                {
                    if (pluginType.IsAbstract || pluginType.IsNotPublic)
                        continue;

                    string typeName = pluginType.Name;

                    if (pluginType.GetInterface("ITerrainEffect", false) != null)
                    {
                        ITerrainEffect terEffect = (ITerrainEffect)Activator.CreateInstance(library.GetType(pluginType.ToString()));

                        InstallPlugin(typeName, terEffect);
                    }
                    else if (pluginType.GetInterface("ITerrainLoader", false) != null)
                    {
                        ITerrainLoader terLoader = (ITerrainLoader)Activator.CreateInstance(library.GetType(pluginType.ToString()));
                        m_loaders[terLoader.FileExtension] = terLoader;
                        m_log.Info("L ... " + typeName);
                    }
                }
                catch (AmbiguousMatchException)
                {
                }
            }
        }

        public void InstallPlugin(string pluginName, ITerrainEffect effect)
        {
            lock (m_plugineffects)
            {
                if (!m_plugineffects.ContainsKey(pluginName))
                {
                    m_plugineffects.Add(pluginName, effect);
                    m_log.Info("E ... " + pluginName);
                }
                else
                {
                    m_plugineffects[pluginName] = effect;
                    m_log.Info("E ... " + pluginName + " (Replaced)");
                }
            }
        }

        #endregion

        #endregion

        /// <summary>
        /// Installs into terrain module the standard suite of brushes
        /// </summary>
        private void InstallDefaultEffects()
        {
            // Draggable Paint Brush Effects
            m_painteffects[StandardTerrainEffects.Raise] = new RaiseSphere();
            m_painteffects[StandardTerrainEffects.Lower] = new LowerSphere();
            m_painteffects[StandardTerrainEffects.Smooth] = new SmoothSphere();
            m_painteffects[StandardTerrainEffects.Noise] = new NoiseSphere();
            m_painteffects[StandardTerrainEffects.Flatten] = new FlattenSphere();
            m_painteffects[StandardTerrainEffects.Revert] = new RevertSphere(m_revert);
            m_painteffects[StandardTerrainEffects.Erode] = new ErodeSphere();
            m_painteffects[StandardTerrainEffects.Weather] = new WeatherSphere();
            m_painteffects[StandardTerrainEffects.Olsen] = new OlsenSphere();

            // Area of effect selection effects
            m_floodeffects[StandardTerrainEffects.Raise] = new RaiseArea();
            m_floodeffects[StandardTerrainEffects.Lower] = new LowerArea();
            m_floodeffects[StandardTerrainEffects.Smooth] = new SmoothArea();
            m_floodeffects[StandardTerrainEffects.Noise] = new NoiseArea();
            m_floodeffects[StandardTerrainEffects.Flatten] = new FlattenArea();
            m_floodeffects[StandardTerrainEffects.Revert] = new RevertArea(m_revert);

            // Filesystem load/save loaders
            m_loaders[".r32"] = new RAW32();
            m_loaders[".f32"] = m_loaders[".r32"];
            m_loaders[".ter"] = new Terragen();
            m_loaders[".raw"] = new LLRAW();
            m_loaders[".jpg"] = new JPEG();
            m_loaders[".jpeg"] = m_loaders[".jpg"];
            m_loaders[".bmp"] = new BMP();
            m_loaders[".png"] = new PNG();
            m_loaders[".gif"] = new GIF();
            m_loaders[".tif"] = new TIFF();
            m_loaders[".tiff"] = m_loaders[".tif"];
        }

        /// <summary>
        /// Saves the current state of the region into the revert map buffer.
        /// </summary>
        public void UpdateRevertMap()
        {
            int x;
            for (x = 0; x < m_channel.Width; x++)
            {
                int y;
                for (y = 0; y < m_channel.Height; y++)
                {
                    m_revert[x, y] = m_channel[x, y];
                }
            }
        }

        /// <summary>
        /// Loads a tile from a larger terrain file and installs it into the region.
        /// </summary>
        /// <param name="filename">The terrain file to load</param>
        /// <param name="fileWidth">The width of the file in units</param>
        /// <param name="fileHeight">The height of the file in units</param>
        /// <param name="fileStartX">Where to begin our slice</param>
        /// <param name="fileStartY">Where to begin our slice</param>
        public void LoadFromFile(string filename, int fileWidth, int fileHeight, int fileStartX, int fileStartY)
        {
            int offsetX = (int) m_scene.RegionInfo.RegionLocX - fileStartX;
            int offsetY = (int) m_scene.RegionInfo.RegionLocY - fileStartY;

            if (offsetX >= 0 && offsetX < fileWidth && offsetY >= 0 && offsetY < fileHeight)
            {
                // this region is included in the tile request
                foreach (KeyValuePair<string, ITerrainLoader> loader in m_loaders)
                {
                    if (filename.EndsWith(loader.Key))
                    {
                        lock (m_scene)
                        {
                            ITerrainChannel channel = loader.Value.LoadFile(filename, offsetX, offsetY,
                                                                            fileWidth, fileHeight,
                                                                            (int) Constants.RegionSize,
                                                                            (int) Constants.RegionSize);
                            m_scene.Heightmap = channel;
                            m_channel = channel;
                            UpdateRevertMap();
                        }

                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Save a number of map tiles to a single big image file.
        /// </summary>
        /// <remarks>
        /// If the image file already exists then the tiles saved will replace those already in the file - other tiles
        /// will be untouched.
        /// </remarks>
        /// <param name="filename">The terrain file to save</param>
        /// <param name="fileWidth">The number of tiles to save along the X axis.</param>
        /// <param name="fileHeight">The number of tiles to save along the Y axis.</param>
        /// <param name="fileStartX">The map x co-ordinate at which to begin the save.</param>
        /// <param name="fileStartY">The may y co-ordinate at which to begin the save.</param>
        public void SaveToFile(string filename, int fileWidth, int fileHeight, int fileStartX, int fileStartY)
        {
            int offsetX = (int)m_scene.RegionInfo.RegionLocX - fileStartX;
            int offsetY = (int)m_scene.RegionInfo.RegionLocY - fileStartY;

            if (offsetX < 0 || offsetX >= fileWidth || offsetY < 0 || offsetY >= fileHeight)
            {
                MainConsole.Instance.OutputFormat(
                    "ERROR: file width + minimum X tile and file height + minimum Y tile must incorporate the current region at ({0},{1}).  File width {2} from {3} and file height {4} from {5} does not.",
                    m_scene.RegionInfo.RegionLocX, m_scene.RegionInfo.RegionLocY, fileWidth, fileStartX, fileHeight, fileStartY);

                return;
            }

            // this region is included in the tile request
            foreach (KeyValuePair<string, ITerrainLoader> loader in m_loaders)
            {
                if (filename.EndsWith(loader.Key) && loader.Value.SupportsTileSave())
                {
                    lock (m_scene)
                    {
                        loader.Value.SaveFile(m_channel, filename, offsetX, offsetY,
                                              fileWidth, fileHeight,
                                              (int)Constants.RegionSize,
                                              (int)Constants.RegionSize);

                        MainConsole.Instance.OutputFormat(
                            "Saved terrain from ({0},{1}) to ({2},{3}) from {4} to {5}",
                            fileStartX, fileStartY, fileStartX + fileWidth - 1, fileStartY + fileHeight - 1,
                            m_scene.RegionInfo.RegionName, filename);
                    }
                    
                    return;
                }
            }

            MainConsole.Instance.OutputFormat(
                "ERROR: Could not save terrain from {0} to {1}.  Valid file extensions are {2}",
                m_scene.RegionInfo.RegionName, filename, m_supportFileExtensionsForTileSave);
        }

        /// <summary>
        /// Performs updates to the region periodically, synchronising physics and other heightmap aware sections
        /// </summary>
        private void EventManager_OnTerrainTick()
        {
            if (m_tainted)
            {
                m_tainted = false;
                m_scene.PhysicsScene.SetTerrain(m_channel.GetFloatsSerialised());
                m_scene.SaveTerrain();

                m_scene.EventManager.TriggerTerrainUpdate();

                // Clients who look at the map will never see changes after they looked at the map, so i've commented this out.
                //m_scene.CreateTerrainTexture(true);
            }
        }

        /// <summary>
        /// Processes commandline input. Do not call directly.
        /// </summary>
        /// <param name="args">Commandline arguments</param>
        private void EventManager_OnPluginConsole(string[] args)
        {
            if (args[0] == "terrain")
            {
                if (args.Length == 1)
                {
                    m_commander.ProcessConsoleCommand("help", new string[0]);
                    return;
                }

                string[] tmpArgs = new string[args.Length - 2];
                int i;
                for (i = 2; i < args.Length; i++)
                    tmpArgs[i - 2] = args[i];

                m_commander.ProcessConsoleCommand(args[1], tmpArgs);
            }
        }

        /// <summary>
        /// Installs terrain brush hook to IClientAPI
        /// </summary>
        /// <param name="client"></param>
        private void EventManager_OnNewClient(IClientAPI client)
        {
            client.OnModifyTerrain += client_OnModifyTerrain;
            client.OnBakeTerrain += client_OnBakeTerrain;
            client.OnLandUndo += client_OnLandUndo;
            client.OnUnackedTerrain += client_OnUnackedTerrain;
        }
        
        /// <summary>
        /// Checks to see if the terrain has been modified since last check
        /// but won't attempt to limit those changes to the limits specified in the estate settings
        /// currently invoked by the command line operations in the region server only
        /// </summary>
        private void CheckForTerrainUpdates()
        {
            CheckForTerrainUpdates(false);
        }

        /// <summary>
        /// Checks to see if the terrain has been modified since last check.
        /// If it has been modified, every all the terrain patches are sent to the client.
        /// If the call is asked to respect the estate settings for terrain_raise_limit and
        /// terrain_lower_limit, it will clamp terrain updates between these values
        /// currently invoked by client_OnModifyTerrain only and not the Commander interfaces
        /// <param name="respectEstateSettings">should height map deltas be limited to the estate settings limits</param>
        /// </summary>
        private void CheckForTerrainUpdates(bool respectEstateSettings)
        {
            bool shouldTaint = false;
            float[] serialised = m_channel.GetFloatsSerialised();
            int x;
            for (x = 0; x < m_channel.Width; x += Constants.TerrainPatchSize)
            {
                int y;
                for (y = 0; y < m_channel.Height; y += Constants.TerrainPatchSize)
                {
                    if (m_channel.Tainted(x, y))
                    {
                        // if we should respect the estate settings then
                        // fixup and height deltas that don't respect them
                        if (respectEstateSettings && LimitChannelChanges(x, y))
                        {
                            // this has been vetoed, so update
                            // what we are going to send to the client
                            serialised = m_channel.GetFloatsSerialised();
                        }

                        SendToClients(serialised, x, y);
                        shouldTaint = true;
                    }
                }
            }
            if (shouldTaint)
            {
                m_scene.EventManager.TriggerTerrainTainted();
                m_tainted = true;
            }
        }

        /// <summary>
        /// Checks to see height deltas in the tainted terrain patch at xStart ,yStart
        /// are all within the current estate limits
        /// <returns>true if changes were limited, false otherwise</returns>
        /// </summary>
        private bool LimitChannelChanges(int xStart, int yStart)
        {
            bool changesLimited = false;
            double minDelta = m_scene.RegionInfo.RegionSettings.TerrainLowerLimit;
            double maxDelta = m_scene.RegionInfo.RegionSettings.TerrainRaiseLimit;

            // loop through the height map for this patch and compare it against
            // the revert map
            for (int x = xStart; x < xStart + Constants.TerrainPatchSize; x++)
            {
                for (int y = yStart; y < yStart + Constants.TerrainPatchSize; y++)
                {

                    double requestedHeight = m_channel[x, y];
                    double bakedHeight = m_revert[x, y];
                    double requestedDelta = requestedHeight - bakedHeight;

                    if (requestedDelta > maxDelta)
                    {
                        m_channel[x, y] = bakedHeight + maxDelta;
                        changesLimited = true;
                    }
                    else if (requestedDelta < minDelta)
                    {
                        m_channel[x, y] = bakedHeight + minDelta; //as lower is a -ve delta
                        changesLimited = true;
                    }
                }
            }

            return changesLimited;
        }

        private void client_OnLandUndo(IClientAPI client)
        {
            lock (m_undo)
            {
                if (m_undo.Count > 0)
                {
                    LandUndoState goback = m_undo.Pop();
                    if (goback != null)
                        goback.PlaybackState();
                }
            }
        }

        /// <summary>
        /// Sends a copy of the current terrain to the scenes clients
        /// </summary>
        /// <param name="serialised">A copy of the terrain as a 1D float array of size w*h</param>
        /// <param name="x">The patch corner to send</param>
        /// <param name="y">The patch corner to send</param>
        private void SendToClients(float[] serialised, int x, int y)
        {
            m_scene.ForEachClient(
                delegate(IClientAPI controller)
                    { controller.SendLayerData(
                        x / Constants.TerrainPatchSize, y / Constants.TerrainPatchSize, serialised);
                    }
            );
        }

        private void client_OnModifyTerrain(UUID user, float height, float seconds, byte size, byte action,
                                            float north, float west, float south, float east, UUID agentId)
        {
            bool god = m_scene.Permissions.IsGod(user);
            bool allowed = false;
            if (north == south && east == west)
            {
                if (m_painteffects.ContainsKey((StandardTerrainEffects) action))
                {
                    bool[,] allowMask = new bool[m_channel.Width,m_channel.Height];
                    allowMask.Initialize();
                    int n = size + 1;
                    if (n > 2)
                        n = 4;

                    int zx = (int) (west + 0.5);
                    int zy = (int) (north + 0.5);

                    int dx;
                    for (dx=-n; dx<=n; dx++)
                    {
                        int dy;
                        for (dy=-n; dy<=n; dy++)
                        {
                            int x = zx + dx;
                            int y = zy + dy;
                            if (x>=0 && y>=0 && x<m_channel.Width && y<m_channel.Height)
                            {
                                if (m_scene.Permissions.CanTerraformLand(agentId, new Vector3(x,y,0)))
                                {
                                    allowMask[x, y] = true;
                                    allowed = true;
                                }
                            }
                        }
                    }
                    if (allowed)
                    {
                        StoreUndoState();
                        m_painteffects[(StandardTerrainEffects) action].PaintEffect(
                            m_channel, allowMask, west, south, height, size, seconds);

                        CheckForTerrainUpdates(!god); //revert changes outside estate limits
                    }
                }
                else
                {
                    m_log.Debug("Unknown terrain brush type " + action);
                }
            }
            else
            {
                if (m_floodeffects.ContainsKey((StandardTerrainEffects) action))
                {
                    bool[,] fillArea = new bool[m_channel.Width,m_channel.Height];
                    fillArea.Initialize();

                    int x;
                    for (x = 0; x < m_channel.Width; x++)
                    {
                        int y;
                        for (y = 0; y < m_channel.Height; y++)
                        {
                            if (x < east && x > west)
                            {
                                if (y < north && y > south)
                                {
                                    if (m_scene.Permissions.CanTerraformLand(agentId, new Vector3(x,y,0)))
                                    {
                                        fillArea[x, y] = true;
                                        allowed = true;
                                    }
                                }
                            }
                        }
                    }

                    if (allowed)
                    {
                        StoreUndoState();
                        m_floodeffects[(StandardTerrainEffects) action].FloodEffect(
                            m_channel, fillArea, size);

                        CheckForTerrainUpdates(!god); //revert changes outside estate limits
                    }
                }
                else
                {
                    m_log.Debug("Unknown terrain flood type " + action);
                }
            }
        }

        private void client_OnBakeTerrain(IClientAPI remoteClient)
        {
            // Not a good permissions check (see client_OnModifyTerrain above), need to check the entire area.
            // for now check a point in the centre of the region

            if (m_scene.Permissions.CanIssueEstateCommand(remoteClient.AgentId, true))
            {
                InterfaceBakeTerrain(null); //bake terrain does not use the passed in parameter
            }
        }
        
        protected void client_OnUnackedTerrain(IClientAPI client, int patchX, int patchY)
        {
            //m_log.Debug("Terrain packet unacked, resending patch: " + patchX + " , " + patchY);
             client.SendLayerData(patchX, patchY, m_scene.Heightmap.GetFloatsSerialised());
        }

        private void StoreUndoState()
        {
            lock (m_undo)
            {
                if (m_undo.Count > 0)
                {
                    LandUndoState last = m_undo.Peek();
                    if (last != null)
                    {
                        if (last.Compare(m_channel))
                            return;
                    }
                }

                LandUndoState nUndo = new LandUndoState(this, m_channel);
                m_undo.Push(nUndo);
            }
        }

        #region Console Commands

        private void InterfaceLoadFile(Object[] args)
        {
            LoadFromFile((string) args[0]);
            CheckForTerrainUpdates();
        }

        private void InterfaceLoadTileFile(Object[] args)
        {
            LoadFromFile((string) args[0],
                         (int) args[1],
                         (int) args[2],
                         (int) args[3],
                         (int) args[4]);
            CheckForTerrainUpdates();
        }

        private void InterfaceSaveFile(Object[] args)
        {
            SaveToFile((string) args[0]);
        }

        private void InterfaceSaveTileFile(Object[] args)
        {
            SaveToFile((string)args[0],
                         (int)args[1],
                         (int)args[2],
                         (int)args[3],
                         (int)args[4]);
        }

        private void InterfaceBakeTerrain(Object[] args)
        {
            UpdateRevertMap();
        }

        private void InterfaceRevertTerrain(Object[] args)
        {
            int x, y;
            for (x = 0; x < m_channel.Width; x++)
                for (y = 0; y < m_channel.Height; y++)
                    m_channel[x, y] = m_revert[x, y];

            CheckForTerrainUpdates();
        }

        private void InterfaceFlipTerrain(Object[] args)
        {
            String direction = (String)args[0];

            if (direction.ToLower().StartsWith("y"))
            {
                for (int x = 0; x < Constants.RegionSize; x++)
                {
                    for (int y = 0; y < Constants.RegionSize / 2; y++)
                    {
                        double height = m_channel[x, y];
                        double flippedHeight = m_channel[x, (int)Constants.RegionSize - 1 - y];
                        m_channel[x, y] = flippedHeight;
                        m_channel[x, (int)Constants.RegionSize - 1 - y] = height;

                    }
                }
            }
            else if (direction.ToLower().StartsWith("x"))
            {
                for (int y = 0; y < Constants.RegionSize; y++)
                {
                    for (int x = 0; x < Constants.RegionSize / 2; x++)
                    {
                        double height = m_channel[x, y];
                        double flippedHeight = m_channel[(int)Constants.RegionSize - 1 - x, y];
                        m_channel[x, y] = flippedHeight;
                        m_channel[(int)Constants.RegionSize - 1 - x, y] = height;

                    }
                }
            }
            else
            {
                m_log.Error("Unrecognised direction - need x or y");
            }


            CheckForTerrainUpdates();
        }

        private void InterfaceRescaleTerrain(Object[] args)
        {
            double desiredMin = (double)args[0];
            double desiredMax = (double)args[1];

            // determine desired scaling factor
            double desiredRange = desiredMax - desiredMin;
            //m_log.InfoFormat("Desired {0}, {1} = {2}", new Object[] { desiredMin, desiredMax, desiredRange });

            if (desiredRange == 0d)
            {
                // delta is zero so flatten at requested height
                InterfaceFillTerrain(new Object[] { args[1] });
            }
            else
            {
                //work out current heightmap range
                double currMin = double.MaxValue;
                double currMax = double.MinValue;

                int width = m_channel.Width;
                int height = m_channel.Height;

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        double currHeight = m_channel[x, y];
                        if (currHeight < currMin)
                        {
                            currMin = currHeight;
                        }
                        else if (currHeight > currMax)
                        {
                            currMax = currHeight;
                        }
                    }
                }

                double currRange = currMax - currMin;
                double scale = desiredRange / currRange;

                //m_log.InfoFormat("Current {0}, {1} = {2}", new Object[] { currMin, currMax, currRange });
                //m_log.InfoFormat("Scale = {0}", scale);

                // scale the heightmap accordingly
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                            double currHeight = m_channel[x, y] - currMin;
                            m_channel[x, y] = desiredMin + (currHeight * scale);
                    }
                }

                CheckForTerrainUpdates();
            }

        }

        private void InterfaceElevateTerrain(Object[] args)
        {
            int x, y;
            for (x = 0; x < m_channel.Width; x++)
                for (y = 0; y < m_channel.Height; y++)
                    m_channel[x, y] += (double) args[0];
            CheckForTerrainUpdates();
        }

        private void InterfaceMultiplyTerrain(Object[] args)
        {
            int x, y;
            for (x = 0; x < m_channel.Width; x++)
                for (y = 0; y < m_channel.Height; y++)
                    m_channel[x, y] *= (double) args[0];
            CheckForTerrainUpdates();
        }

        private void InterfaceLowerTerrain(Object[] args)
        {
            int x, y;
            for (x = 0; x < m_channel.Width; x++)
                for (y = 0; y < m_channel.Height; y++)
                    m_channel[x, y] -= (double) args[0];
            CheckForTerrainUpdates();
        }

        private void InterfaceFillTerrain(Object[] args)
        {
            int x, y;

            for (x = 0; x < m_channel.Width; x++)
                for (y = 0; y < m_channel.Height; y++)
                    m_channel[x, y] = (double) args[0];
            CheckForTerrainUpdates();
        }

        private void InterfaceMinTerrain(Object[] args)
        {
            int x, y;
            for (x = 0; x < m_channel.Width; x++)
            {
                for (y = 0; y < m_channel.Height; y++)
                {
                    m_channel[x, y] = Math.Max((double)args[0], m_channel[x, y]);
                }
            }
            CheckForTerrainUpdates();
        }

        private void InterfaceMaxTerrain(Object[] args)
        {
            int x, y;
            for (x = 0; x < m_channel.Width; x++)
            {
                for (y = 0; y < m_channel.Height; y++)
                {
                    m_channel[x, y] = Math.Min((double)args[0], m_channel[x, y]);
                }
            }
            CheckForTerrainUpdates();
        }

        private void InterfaceShowDebugStats(Object[] args)
        {
            double max = Double.MinValue;
            double min = double.MaxValue;
            double sum = 0;

            int x;
            for (x = 0; x < m_channel.Width; x++)
            {
                int y;
                for (y = 0; y < m_channel.Height; y++)
                {
                    sum += m_channel[x, y];
                    if (max < m_channel[x, y])
                        max = m_channel[x, y];
                    if (min > m_channel[x, y])
                        min = m_channel[x, y];
                }
            }

            double avg = sum / (m_channel.Height * m_channel.Width);

            m_log.Info("Channel " + m_channel.Width + "x" + m_channel.Height);
            m_log.Info("max/min/avg/sum: " + max + "/" + min + "/" + avg + "/" + sum);
        }

        private void InterfaceEnableExperimentalBrushes(Object[] args)
        {
            if ((bool) args[0])
            {
                m_painteffects[StandardTerrainEffects.Revert] = new WeatherSphere();
                m_painteffects[StandardTerrainEffects.Flatten] = new OlsenSphere();
                m_painteffects[StandardTerrainEffects.Smooth] = new ErodeSphere();
            }
            else
            {
                InstallDefaultEffects();
            }
        }

        private void InterfaceRunPluginEffect(Object[] args)
        {
            string firstArg = (string)args[0];
            if (firstArg == "list")
            {
                m_log.Info("List of loaded plugins");
                foreach (KeyValuePair<string, ITerrainEffect> kvp in m_plugineffects)
                {
                    m_log.Info(kvp.Key);
                }
                return;
            }
            if (firstArg == "reload")
            {
                LoadPlugins();
                return;
            }
            if (m_plugineffects.ContainsKey(firstArg))
            {
                m_plugineffects[firstArg].RunEffect(m_channel);
                CheckForTerrainUpdates();
            }
            else
            {
                m_log.Warn("No such plugin effect loaded.");
            }
        }

        private void InstallInterfaces()
        {
            Command loadFromFileCommand =
                new Command("load", CommandIntentions.COMMAND_HAZARDOUS, InterfaceLoadFile, "Loads a terrain from a specified file.");
            loadFromFileCommand.AddArgument("filename",
                                            "The file you wish to load from, the file extension determines the loader to be used. Supported extensions include: " +
                                            m_supportedFileExtensions, "String");

            Command saveToFileCommand =
                new Command("save", CommandIntentions.COMMAND_NON_HAZARDOUS, InterfaceSaveFile, "Saves the current heightmap to a specified file.");
            saveToFileCommand.AddArgument("filename",
                                          "The destination filename for your heightmap, the file extension determines the format to save in. Supported extensions include: " +
                                          m_supportedFileExtensions, "String");

            Command loadFromTileCommand =
                new Command("load-tile", CommandIntentions.COMMAND_HAZARDOUS, InterfaceLoadTileFile, "Loads a terrain from a section of a larger file.");
            loadFromTileCommand.AddArgument("filename",
                                            "The file you wish to load from, the file extension determines the loader to be used. Supported extensions include: " +
                                            m_supportedFileExtensions, "String");
            loadFromTileCommand.AddArgument("file width", "The width of the file in tiles", "Integer");
            loadFromTileCommand.AddArgument("file height", "The height of the file in tiles", "Integer");
            loadFromTileCommand.AddArgument("minimum X tile", "The X region coordinate of the first section on the file",
                                            "Integer");
            loadFromTileCommand.AddArgument("minimum Y tile", "The Y region coordinate of the first section on the file",
                                            "Integer");

            Command saveToTileCommand =
                new Command("save-tile", CommandIntentions.COMMAND_HAZARDOUS, InterfaceSaveTileFile, "Saves the current heightmap to the larger file.");
            saveToTileCommand.AddArgument("filename",
                                            "The file you wish to save to, the file extension determines the loader to be used. Supported extensions include: " +
                                            m_supportFileExtensionsForTileSave, "String");
            saveToTileCommand.AddArgument("file width", "The width of the file in tiles", "Integer");
            saveToTileCommand.AddArgument("file height", "The height of the file in tiles", "Integer");
            saveToTileCommand.AddArgument("minimum X tile", "The X region coordinate of the first section on the file",
                                            "Integer");
            saveToTileCommand.AddArgument("minimum Y tile", "The Y region coordinate of the first tile on the file\n"
                                          + "= Example =\n"
                                          + "To save a PNG file for a set of map tiles 2 regions wide and 3 regions high from map co-ordinate (9910,10234)\n"
                                          + "        # terrain save-tile ST06.png 2 3 9910 10234\n",
                                          "Integer");

            // Terrain adjustments
            Command fillRegionCommand =
                new Command("fill", CommandIntentions.COMMAND_HAZARDOUS, InterfaceFillTerrain, "Fills the current heightmap with a specified value.");
            fillRegionCommand.AddArgument("value", "The numeric value of the height you wish to set your region to.",
                                          "Double");

            Command elevateCommand =
                new Command("elevate", CommandIntentions.COMMAND_HAZARDOUS, InterfaceElevateTerrain, "Raises the current heightmap by the specified amount.");
            elevateCommand.AddArgument("amount", "The amount of height to add to the terrain in meters.", "Double");

            Command lowerCommand =
                new Command("lower", CommandIntentions.COMMAND_HAZARDOUS, InterfaceLowerTerrain, "Lowers the current heightmap by the specified amount.");
            lowerCommand.AddArgument("amount", "The amount of height to remove from the terrain in meters.", "Double");

            Command multiplyCommand =
                new Command("multiply", CommandIntentions.COMMAND_HAZARDOUS, InterfaceMultiplyTerrain, "Multiplies the heightmap by the value specified.");
            multiplyCommand.AddArgument("value", "The value to multiply the heightmap by.", "Double");

            Command bakeRegionCommand =
                new Command("bake", CommandIntentions.COMMAND_HAZARDOUS, InterfaceBakeTerrain, "Saves the current terrain into the regions revert map.");
            Command revertRegionCommand =
                new Command("revert", CommandIntentions.COMMAND_HAZARDOUS, InterfaceRevertTerrain, "Loads the revert map terrain into the regions heightmap.");

            Command flipCommand =
                new Command("flip", CommandIntentions.COMMAND_HAZARDOUS, InterfaceFlipTerrain, "Flips the current terrain about the X or Y axis");
            flipCommand.AddArgument("direction", "[x|y] the direction to flip the terrain in", "String");

            Command rescaleCommand =
                new Command("rescale", CommandIntentions.COMMAND_HAZARDOUS, InterfaceRescaleTerrain, "Rescales the current terrain to fit between the given min and max heights");
            rescaleCommand.AddArgument("min", "min terrain height after rescaling", "Double");
            rescaleCommand.AddArgument("max", "max terrain height after rescaling", "Double");

            Command minCommand = new Command("min", CommandIntentions.COMMAND_HAZARDOUS, InterfaceMinTerrain, "Sets the minimum terrain height to the specified value.");
            minCommand.AddArgument("min", "terrain height to use as minimum", "Double");

            Command maxCommand = new Command("max", CommandIntentions.COMMAND_HAZARDOUS, InterfaceMaxTerrain, "Sets the maximum terrain height to the specified value.");
            maxCommand.AddArgument("min", "terrain height to use as maximum", "Double");


            // Debug
            Command showDebugStatsCommand =
                new Command("stats", CommandIntentions.COMMAND_STATISTICAL, InterfaceShowDebugStats,
                            "Shows some information about the regions heightmap for debugging purposes.");

            Command experimentalBrushesCommand =
                new Command("newbrushes", CommandIntentions.COMMAND_HAZARDOUS, InterfaceEnableExperimentalBrushes,
                            "Enables experimental brushes which replace the standard terrain brushes. WARNING: This is a debug setting and may be removed at any time.");
            experimentalBrushesCommand.AddArgument("Enabled?", "true / false - Enable new brushes", "Boolean");

            //Plugins
            Command pluginRunCommand =
                new Command("effect", CommandIntentions.COMMAND_HAZARDOUS, InterfaceRunPluginEffect, "Runs a specified plugin effect");
            pluginRunCommand.AddArgument("name", "The plugin effect you wish to run, or 'list' to see all plugins", "String");

            m_commander.RegisterCommand("load", loadFromFileCommand);
            m_commander.RegisterCommand("load-tile", loadFromTileCommand);
            m_commander.RegisterCommand("save", saveToFileCommand);
            m_commander.RegisterCommand("save-tile", saveToTileCommand);
            m_commander.RegisterCommand("fill", fillRegionCommand);
            m_commander.RegisterCommand("elevate", elevateCommand);
            m_commander.RegisterCommand("lower", lowerCommand);
            m_commander.RegisterCommand("multiply", multiplyCommand);
            m_commander.RegisterCommand("bake", bakeRegionCommand);
            m_commander.RegisterCommand("revert", revertRegionCommand);
            m_commander.RegisterCommand("newbrushes", experimentalBrushesCommand);
            m_commander.RegisterCommand("stats", showDebugStatsCommand);
            m_commander.RegisterCommand("effect", pluginRunCommand);
            m_commander.RegisterCommand("flip", flipCommand);
            m_commander.RegisterCommand("rescale", rescaleCommand);
            m_commander.RegisterCommand("min", minCommand);
            m_commander.RegisterCommand("max", maxCommand);

            // Add this to our scene so scripts can call these functions
            m_scene.RegisterModuleCommander(m_commander);
        }


        #endregion

    }
}
