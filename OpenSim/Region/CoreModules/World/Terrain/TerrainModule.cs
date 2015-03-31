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

using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.CoreModules.Framework.InterfaceCommander;
using OpenSim.Region.CoreModules.World.Terrain.FileLoaders;
using OpenSim.Region.CoreModules.World.Terrain.Features;
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

        /// <summary>
        /// Terrain Features
        /// </summary>
        public enum TerrainFeatures: byte
        {
            Rectangle = 1,
        }

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

#pragma warning disable 414
        private static readonly string LogHeader = "[TERRAIN MODULE]";
#pragma warning restore 414

        private readonly Commander m_commander = new Commander("terrain");

        private readonly Dictionary<StandardTerrainEffects, ITerrainFloodEffect> m_floodeffects =
            new Dictionary<StandardTerrainEffects, ITerrainFloodEffect>();

        private readonly Dictionary<string, ITerrainLoader> m_loaders = new Dictionary<string, ITerrainLoader>();

        private readonly Dictionary<StandardTerrainEffects, ITerrainPaintableEffect> m_painteffects =
            new Dictionary<StandardTerrainEffects, ITerrainPaintableEffect>();

        private Dictionary<string, ITerrainEffect> m_plugineffects;

        private Dictionary<string, ITerrainFeature> m_featureEffects =
            new Dictionary<string, ITerrainFeature>();

        private ITerrainChannel m_channel;
        private ITerrainChannel m_revert;
        private Scene m_scene;
        private volatile bool m_tainted;
        private readonly Stack<LandUndoState> m_undo = new Stack<LandUndoState>(5);
        
        private String m_InitialTerrain = "pinhead-island";

        // If true, send terrain patch updates to clients based on their view distance
        private bool m_sendTerrainUpdatesByViewDistance = true;

        // Class to keep the per client collection of terrain patches that must be sent.
        // A patch is set to 'true' meaning it should be sent to the client. Once the
        //    patch packet is queued to the client, the bit for that patch is set to 'false'.
        private class PatchUpdates
        {
            private bool[,] updated;    // for each patch, whether it needs to be sent to this client
            private int updateCount;    // number of patches that need to be sent
            public ScenePresence Presence;   // a reference to the client to send to
            public PatchUpdates(TerrainData terrData, ScenePresence pPresence)
            {
                updated = new bool[terrData.SizeX / Constants.TerrainPatchSize, terrData.SizeY / Constants.TerrainPatchSize];
                updateCount = 0;
                Presence = pPresence;
                // Initially, send all patches to the client
                SetAll(true);
            }
            // Returns 'true' if there are any patches marked for sending
            public bool HasUpdates()
            {
                return (updateCount > 0);
            }
            public void SetByXY(int x, int y, bool state)
            {
                this.SetByPatch(x / Constants.TerrainPatchSize, y / Constants.TerrainPatchSize, state);
            }
            public bool GetByPatch(int patchX, int patchY)
            {
                return updated[patchX, patchY];
            }
            public void SetByPatch(int patchX, int patchY, bool state)
            {
                bool prevState = updated[patchX, patchY];
                if (!prevState && state)
                    updateCount++;
                if (prevState && !state)
                    updateCount--;
                updated[patchX, patchY] = state;
            }
            public void SetAll(bool state)
            {
                updateCount = 0;
                for (int xx = 0; xx < updated.GetLength(0); xx++)
                    for (int yy = 0; yy < updated.GetLength(1); yy++)
                        updated[xx, yy] = state;
                if (state)
                    updateCount = updated.GetLength(0) * updated.GetLength(1);
            }
            // Logically OR's the terrain data's patch taint map into this client's update map.
            public void SetAll(TerrainData terrData)
            {
                if (updated.GetLength(0) != (terrData.SizeX / Constants.TerrainPatchSize)
                    || updated.GetLength(1) != (terrData.SizeY / Constants.TerrainPatchSize))
                {
                    throw new Exception(
                        String.Format("{0} PatchUpdates.SetAll: patch array not same size as terrain. arr=<{1},{2}>, terr=<{3},{4}>",
                                LogHeader, updated.GetLength(0), updated.GetLength(1),
                                terrData.SizeX / Constants.TerrainPatchSize, terrData.SizeY / Constants.TerrainPatchSize)
                    );
                }
                for (int xx = 0; xx < terrData.SizeX; xx += Constants.TerrainPatchSize)
                {
                    for (int yy = 0; yy < terrData.SizeY; yy += Constants.TerrainPatchSize)
                    {
                        // Only set tainted. The patch bit may be set if the patch was to be sent later.
                        if (terrData.IsTaintedAt(xx, yy, false))
                        {
                            this.SetByXY(xx, yy, true);
                        }
                    }
                }
            }
        }

        // The flags of which terrain patches to send for each of the ScenePresence's
        private Dictionary<UUID, PatchUpdates> m_perClientPatchUpdates = new Dictionary<UUID, PatchUpdates>();

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
            {
                m_InitialTerrain = terrainConfig.GetString("InitialTerrain", m_InitialTerrain);
                m_sendTerrainUpdatesByViewDistance = terrainConfig.GetBoolean("SendTerrainUpdatesByViewDistance", m_sendTerrainUpdatesByViewDistance);
            }
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;

            // Install terrain module in the simulator
            lock (m_scene)
            {
                if (m_scene.Heightmap == null)
                {
                    m_channel = new TerrainChannel(m_InitialTerrain, (int)m_scene.RegionInfo.RegionSizeX,
                                                                     (int)m_scene.RegionInfo.RegionSizeY,
                                                                     (int)m_scene.RegionInfo.RegionSizeZ);
                    m_scene.Heightmap = m_channel;
                    UpdateRevertMap();
                }
                else
                {
                    m_channel = m_scene.Heightmap;
                    UpdateRevertMap();
                }

                m_scene.RegisterModuleInterface<ITerrainModule>(this);
                m_scene.EventManager.OnNewClient += EventManager_OnNewClient;
                m_scene.EventManager.OnClientClosed += EventManager_OnClientClosed;
                m_scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
                m_scene.EventManager.OnTerrainTick += EventManager_OnTerrainTick;
                m_scene.EventManager.OnFrame += EventManager_OnFrame;
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
                m_scene.EventManager.OnFrame -= EventManager_OnFrame;
                m_scene.EventManager.OnTerrainTick -= EventManager_OnTerrainTick;
                m_scene.EventManager.OnPluginConsole -= EventManager_OnPluginConsole;
                m_scene.EventManager.OnClientClosed -= EventManager_OnClientClosed;
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
                            if (channel.Width != m_scene.RegionInfo.RegionSizeX || channel.Height != m_scene.RegionInfo.RegionSizeY)
                            {
                                // TerrainChannel expects a RegionSize x RegionSize map, currently
                                throw new ArgumentException(String.Format("wrong size, use a file with size {0} x {1}",
                                                                          m_scene.RegionInfo.RegionSizeX, m_scene.RegionInfo.RegionSizeY));
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

        public void LoadFromStream(string filename, Stream stream)
        {
            LoadFromStream(filename, Vector3.Zero, 0f, Vector2.Zero, stream);
        }

        /// <summary>
        /// Loads a terrain file from a stream and installs it in the scene.
        /// </summary>
        /// <param name="filename">Filename to terrain file. Type is determined by extension.</param>
        /// <param name="stream"></param>
        public void LoadFromStream(string filename, Vector3 displacement,
                                float radianRotation, Vector2 rotationDisplacement, Stream stream)
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
                            m_channel.Merge(channel, displacement, radianRotation, rotationDisplacement);
                            UpdateRevertMap();
                        }
                        catch (NotImplementedException)
                        {
                            m_log.Error("[TERRAIN]: Unable to load heightmap, the " + loader.Value +
                                        " parser does not support file loading. (May be save only)");
                            throw new TerrainException(String.Format("unable to load heightmap: parser {0} does not support loading", loader.Value));
                        }
                    }

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

        // Someone diddled terrain outside the normal code paths. Set the taintedness for all clients.
        // ITerrainModule.TaintTerrain()
        public void TaintTerrain ()
        {
            lock (m_perClientPatchUpdates)
            {
                // Set the flags for all clients so the tainted patches will be sent out
                foreach (PatchUpdates pups in m_perClientPatchUpdates.Values)
                {
                    pups.SetAll(m_scene.Heightmap.GetTerrainData());
                }
            }
        }

        // ITerrainModule.PushTerrain()
        public void PushTerrain(IClientAPI pClient)
        {
            if (m_sendTerrainUpdatesByViewDistance)
            {
                ScenePresence presence = m_scene.GetScenePresence(pClient.AgentId);
                if (presence != null)
                {
                    lock (m_perClientPatchUpdates)
                    {
                        PatchUpdates pups;
                        if (!m_perClientPatchUpdates.TryGetValue(pClient.AgentId, out pups))
                        {
                            // There is a ScenePresence without a send patch map. Create one.
                            pups = new PatchUpdates(m_scene.Heightmap.GetTerrainData(), presence);
                            m_perClientPatchUpdates.Add(presence.UUID, pups);
                        }
                        pups.SetAll(true);
                    }
                }
            }
            else
            {
                // The traditional way is to call into the protocol stack to send them all.
                pClient.SendLayerData(new float[10]);
            }
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

            // Terrain Feature effects
            m_featureEffects["rectangle"] = new RectangleFeature(this);

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
            /*
            int x;
            for (x = 0; x < m_channel.Width; x++)
            {
                int y;
                for (y = 0; y < m_channel.Height; y++)
                {
                    m_revert[x, y] = m_channel[x, y];
                }
            }
             */
            m_revert = m_channel.MakeCopy();
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
                                                                            (int) m_scene.RegionInfo.RegionSizeX,
                                                                            (int) m_scene.RegionInfo.RegionSizeY);
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
                                              (int)m_scene.RegionInfo.RegionSizeX,
                                              (int)m_scene.RegionInfo.RegionSizeY);

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
        /// Called before processing of every simulation frame.
        /// This is used to check to see of any of the terrain is tainted and, if so, schedule
        /// updates for all the presences.
        /// This also checks to see if there are updates that need to be sent for each presence.
        /// This is where the logic is to send terrain updates to clients.
        /// </summary>
        private void EventManager_OnFrame()
        {
            TerrainData terrData = m_channel.GetTerrainData();

            bool shouldTaint = false;
            for (int x = 0; x < terrData.SizeX; x += Constants.TerrainPatchSize)
            {
                for (int y = 0; y < terrData.SizeY; y += Constants.TerrainPatchSize)
                {
                    if (terrData.IsTaintedAt(x, y))
                    {
                        // Found a patch that was modified. Push this flag into the clients.
                        SendToClients(terrData, x, y);
                        shouldTaint = true;
                    }
                }
            }

            // This event also causes changes to be sent to the clients
            CheckSendingPatchesToClients();

            // If things changes, generate some events
            if (shouldTaint)
            {
                m_scene.EventManager.TriggerTerrainTainted();
                m_tainted = true;
            }
        }

        /// <summary>
        /// Performs updates to the region periodically, synchronising physics and other heightmap aware sections
        /// Called infrequently (like every 5 seconds or so). Best used for storing terrain.
        /// </summary>
        private void EventManager_OnTerrainTick()
        {
            if (m_tainted)
            {
                m_tainted = false;
                m_scene.PhysicsScene.SetTerrain(m_channel.GetFloatsSerialised());
                m_scene.SaveTerrain();

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
        /// Installs terrain brush hook to IClientAPI
        /// </summary>
        /// <param name="client"></param>
        private void EventManager_OnClientClosed(UUID client, Scene scene)
        {
            ScenePresence presence = scene.GetScenePresence(client);
            if (presence != null)
            {
                presence.ControllingClient.OnModifyTerrain -= client_OnModifyTerrain;
                presence.ControllingClient.OnBakeTerrain -= client_OnBakeTerrain;
                presence.ControllingClient.OnLandUndo -= client_OnLandUndo;
                presence.ControllingClient.OnUnackedTerrain -= client_OnUnackedTerrain;
            }

            lock (m_perClientPatchUpdates)
                m_perClientPatchUpdates.Remove(client);
        }
        
        /// <summary>
        /// Scan over changes in the terrain and limit height changes. This enforces the
        ///     non-estate owner limits on rate of terrain editting.
        /// Returns 'true' if any heights were limited.
        /// </summary>
        private bool EnforceEstateLimits()
        {
            TerrainData terrData = m_channel.GetTerrainData();

            bool wasLimited = false;
            for (int x = 0; x < terrData.SizeX; x += Constants.TerrainPatchSize)
            {
                for (int y = 0; y < terrData.SizeY; y += Constants.TerrainPatchSize)
                {
                    if (terrData.IsTaintedAt(x, y, false /* clearOnTest */))
                   {
                        // If we should respect the estate settings then
                        //     fixup and height deltas that don't respect them.
                        // Note that LimitChannelChanges() modifies the TerrainChannel with the limited height values.
                        wasLimited |= LimitChannelChanges(terrData, x, y);
                    }
                }
            }
            return wasLimited;
        }

        /// <summary>
        /// Checks to see height deltas in the tainted terrain patch at xStart ,yStart
        /// are all within the current estate limits
        /// <returns>true if changes were limited, false otherwise</returns>
        /// </summary>
        private bool LimitChannelChanges(TerrainData terrData, int xStart, int yStart)
        {
            bool changesLimited = false;
            float minDelta = (float)m_scene.RegionInfo.RegionSettings.TerrainLowerLimit;
            float maxDelta = (float)m_scene.RegionInfo.RegionSettings.TerrainRaiseLimit;

            // loop through the height map for this patch and compare it against
            // the revert map
            for (int x = xStart; x < xStart + Constants.TerrainPatchSize; x++)
            {
                for (int y = yStart; y < yStart + Constants.TerrainPatchSize; y++)
                {
                    float requestedHeight = terrData[x, y];
                    float bakedHeight = (float)m_revert[x, y];
                    float requestedDelta = requestedHeight - bakedHeight;

                    if (requestedDelta > maxDelta)
                    {
                        terrData[x, y] = bakedHeight + maxDelta;
                        changesLimited = true;
                    }
                    else if (requestedDelta < minDelta)
                    {
                        terrData[x, y] = bakedHeight + minDelta; //as lower is a -ve delta
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
        private void SendToClients(TerrainData terrData, int x, int y)
        {
            if (m_sendTerrainUpdatesByViewDistance)
            {
                // Add that this patch needs to be sent to the accounting for each client.
                lock (m_perClientPatchUpdates)
                {
                    m_scene.ForEachScenePresence(presence =>
                        {
                            PatchUpdates thisClientUpdates;
                            if (!m_perClientPatchUpdates.TryGetValue(presence.UUID, out thisClientUpdates))
                            {
                                // There is a ScenePresence without a send patch map. Create one.
                                thisClientUpdates = new PatchUpdates(terrData, presence);
                                m_perClientPatchUpdates.Add(presence.UUID, thisClientUpdates);
                            }
                            thisClientUpdates.SetByXY(x, y, true);
                        }
                    );
                }
            }
            else
            {
                // Legacy update sending where the update is sent out as soon as noticed
                // We know the actual terrain data passed is ignored. This kludge saves changing IClientAPI.
                //float[] heightMap = terrData.GetFloatsSerialized();
                float[] heightMap = new float[10];
                m_scene.ForEachClient(
                    delegate(IClientAPI controller)
                    {
                        controller.SendLayerData(x / Constants.TerrainPatchSize,
                                                 y / Constants.TerrainPatchSize,
                                                 heightMap);
                    }
                );
            }
        }

        private class PatchesToSend : IComparable<PatchesToSend>
        {
            public int PatchX;
            public int PatchY;
            public float Dist;
            public PatchesToSend(int pX, int pY, float pDist)
            {
                PatchX = pX;
                PatchY = pY;
                Dist = pDist;
            }
            public int CompareTo(PatchesToSend other)
            {
                return Dist.CompareTo(other.Dist);
            }
        }

        // Called each frame time to see if there are any patches to send to any of the
        //    ScenePresences.
        // Loop through all the per-client info and send any patches necessary.
        private void CheckSendingPatchesToClients()
        {
            lock (m_perClientPatchUpdates)
            {
                foreach (PatchUpdates pups in m_perClientPatchUpdates.Values)
                {
                    if (pups.HasUpdates())
                    {
                        // There is something that could be sent to this client.
                        List<PatchesToSend> toSend = GetModifiedPatchesInViewDistance(pups);
                        if (toSend.Count > 0)
                        {
                            // m_log.DebugFormat("{0} CheckSendingPatchesToClient: sending {1} patches to {2} in region {3}",
                            //                     LogHeader, toSend.Count, pups.Presence.Name, m_scene.RegionInfo.RegionName);
                            // Sort the patches to send by the distance from the presence
                            toSend.Sort();
                            /*
                            foreach (PatchesToSend pts in toSend)
                            {
                                pups.Presence.ControllingClient.SendLayerData(pts.PatchX, pts.PatchY, null);
                                // presence.ControllingClient.SendLayerData(xs.ToArray(), ys.ToArray(), null, TerrainPatch.LayerType.Land);
                            }
                            */

                            int[] xPieces = new int[toSend.Count];
                            int[] yPieces = new int[toSend.Count];
                            float[] patchPieces = new float[toSend.Count * 2];
                            int pieceIndex = 0;
                            foreach (PatchesToSend pts in toSend)
                            {
                                patchPieces[pieceIndex++] = pts.PatchX;
                                patchPieces[pieceIndex++] = pts.PatchY;
                            }
                            pups.Presence.ControllingClient.SendLayerData(-toSend.Count, 0, patchPieces);
                        }
                    }
                }
            }
        }

        private List<PatchesToSend> GetModifiedPatchesInViewDistance(PatchUpdates pups)
        {
            List<PatchesToSend> ret = new List<PatchesToSend>();

            ScenePresence presence = pups.Presence;
            if (presence == null)
                return ret;

            // Compute the area of patches within our draw distance
            int startX = (((int) (presence.AbsolutePosition.X - presence.DrawDistance))/Constants.TerrainPatchSize) - 2;
            startX = Math.Max(startX, 0);
            startX = Math.Min(startX, (int)m_scene.RegionInfo.RegionSizeX/Constants.TerrainPatchSize);
            int startY = (((int) (presence.AbsolutePosition.Y - presence.DrawDistance))/Constants.TerrainPatchSize) - 2;
            startY = Math.Max(startY, 0);
            startY = Math.Min(startY, (int)m_scene.RegionInfo.RegionSizeY/Constants.TerrainPatchSize);
            int endX = (((int) (presence.AbsolutePosition.X + presence.DrawDistance))/Constants.TerrainPatchSize) + 2;
            endX = Math.Max(endX, 0);
            endX = Math.Min(endX, (int)m_scene.RegionInfo.RegionSizeX/Constants.TerrainPatchSize);
            int endY = (((int) (presence.AbsolutePosition.Y + presence.DrawDistance))/Constants.TerrainPatchSize) + 2;
            endY = Math.Max(endY, 0);
            endY = Math.Min(endY, (int)m_scene.RegionInfo.RegionSizeY/Constants.TerrainPatchSize);
            // m_log.DebugFormat("{0} GetModifiedPatchesInViewDistance. rName={1}, ddist={2}, apos={3}, start=<{4},{5}>, end=<{6},{7}>",
            //                                     LogHeader, m_scene.RegionInfo.RegionName,
            //                                     presence.DrawDistance, presence.AbsolutePosition,
            //                                     startX, startY, endX, endY);
            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    //Need to make sure we don't send the same ones over and over
                    Vector3 presencePos = presence.AbsolutePosition;
                    Vector3 patchPos = new Vector3(x * Constants.TerrainPatchSize, y * Constants.TerrainPatchSize, presencePos.Z);
                    if (pups.GetByPatch(x, y))
                    {
                        //Check which has less distance, camera or avatar position, both have to be done.
                        //Its not a radius, its a diameter and we add 50 so that it doesn't look like it cuts off
                        if (Util.DistanceLessThan(presencePos, patchPos, presence.DrawDistance + 50)
                            || Util.DistanceLessThan(presence.CameraPosition, patchPos, presence.DrawDistance + 50))
                        {
                            //They can see it, send it to them
                            pups.SetByPatch(x, y, false);
                            float dist = Vector3.DistanceSquared(presencePos, patchPos);
                            ret.Add(new PatchesToSend(x, y, dist));
                            //Wait and send them all at once
                            // pups.client.SendLayerData(x, y, null);
                        }
                    }
                }
            }
            return ret;
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

                        //revert changes outside estate limits
                        if (!god)
                            EnforceEstateLimits();
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
                        m_floodeffects[(StandardTerrainEffects) action].FloodEffect(m_channel, fillArea, size);

                        //revert changes outside estate limits
                        if (!god)
                            EnforceEstateLimits();
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
            // SendLayerData does not use the heightmap parameter. This kludge is so as to not change IClientAPI.
            float[] heightMap = new float[10];
            client.SendLayerData(patchX, patchY, heightMap);
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
        }

        private void InterfaceLoadTileFile(Object[] args)
        {
            LoadFromFile((string) args[0],
                         (int) args[1],
                         (int) args[2],
                         (int) args[3],
                         (int) args[4]);
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

        }

        private void InterfaceFlipTerrain(Object[] args)
        {
            String direction = (String)args[0];

            if (direction.ToLower().StartsWith("y"))
            {
                for (int x = 0; x < m_channel.Width; x++)
                {
                    for (int y = 0; y < m_channel.Height / 2; y++)
                    {
                        double height = m_channel[x, y];
                        double flippedHeight = m_channel[x, (int)m_channel.Height - 1 - y];
                        m_channel[x, y] = flippedHeight;
                        m_channel[x, (int)m_channel.Height - 1 - y] = height;

                    }
                }
            }
            else if (direction.ToLower().StartsWith("x"))
            {
                for (int y = 0; y < m_channel.Height; y++)
                {
                    for (int x = 0; x < m_channel.Width / 2; x++)
                    {
                        double height = m_channel[x, y];
                        double flippedHeight = m_channel[(int)m_channel.Width - 1 - x, y];
                        m_channel[x, y] = flippedHeight;
                        m_channel[(int)m_channel.Width - 1 - x, y] = height;

                    }
                }
            }
            else
            {
                MainConsole.Instance.OutputFormat("ERROR: Unrecognised direction {0} - need x or y", direction);
            }
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

            }

        }

        private void InterfaceElevateTerrain(Object[] args)
        {
            int x, y;
            for (x = 0; x < m_channel.Width; x++)
                for (y = 0; y < m_channel.Height; y++)
                    m_channel[x, y] += (double) args[0];
        }

        private void InterfaceMultiplyTerrain(Object[] args)
        {
            int x, y;
            for (x = 0; x < m_channel.Width; x++)
                for (y = 0; y < m_channel.Height; y++)
                    m_channel[x, y] *= (double) args[0];
        }

        private void InterfaceLowerTerrain(Object[] args)
        {
            int x, y;
            for (x = 0; x < m_channel.Width; x++)
                for (y = 0; y < m_channel.Height; y++)
                    m_channel[x, y] -= (double) args[0];
        }

        public void InterfaceFillTerrain(Object[] args)
        {
            int x, y;

            for (x = 0; x < m_channel.Width; x++)
                for (y = 0; y < m_channel.Height; y++)
                    m_channel[x, y] = (double) args[0];
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
        }

        private void InterfaceShow(Object[] args)
        {
            Vector2 point;

            if (!ConsoleUtil.TryParseConsole2DVector((string)args[0], null, out point))
            {
                Console.WriteLine("ERROR: {0} is not a valid vector", args[0]);
                return;
            }

            double height = m_channel[(int)point.X, (int)point.Y];

            Console.WriteLine("Terrain height at {0} is {1}", point, height);
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

            MainConsole.Instance.OutputFormat("Channel {0}x{1}", m_channel.Width, m_channel.Height);
            MainConsole.Instance.OutputFormat("max/min/avg/sum: {0}/{1}/{2}/{3}", max, min, avg, sum);
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
                MainConsole.Instance.Output("List of loaded plugins");
                foreach (KeyValuePair<string, ITerrainEffect> kvp in m_plugineffects)
                {
                    MainConsole.Instance.Output(kvp.Key);
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
            }
            else
            {
                MainConsole.Instance.Output("WARNING: No such plugin effect {0} loaded.", firstArg);
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

            Command showCommand =
                new Command("show", CommandIntentions.COMMAND_NON_HAZARDOUS, InterfaceShow,
                            "Shows terrain height at a given co-ordinate.");
            showCommand.AddArgument("point", "point in <x>,<y> format with no spaces (e.g. 45,45)", "String");

            Command experimentalBrushesCommand =
                new Command("newbrushes", CommandIntentions.COMMAND_HAZARDOUS, InterfaceEnableExperimentalBrushes,
                            "Enables experimental brushes which replace the standard terrain brushes. WARNING: This is a debug setting and may be removed at any time.");
            experimentalBrushesCommand.AddArgument("Enabled?", "true / false - Enable new brushes", "Boolean");

            // Plugins
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
            m_commander.RegisterCommand("show", showCommand);
            m_commander.RegisterCommand("stats", showDebugStatsCommand);
            m_commander.RegisterCommand("effect", pluginRunCommand);
            m_commander.RegisterCommand("flip", flipCommand);
            m_commander.RegisterCommand("rescale", rescaleCommand);
            m_commander.RegisterCommand("min", minCommand);
            m_commander.RegisterCommand("max", maxCommand);

            // Add this to our scene so scripts can call these functions
            m_scene.RegisterModuleCommander(m_commander);

            // Add Feature command to Scene, since Command object requires fixed-length arglists
            m_scene.AddCommand("Terrain", this, "terrain feature",
                               "terrain feature <type> <parameters...>", "Constructs a feature of the requested type.", FeatureCommand);

        }

        public void FeatureCommand(string module, string[] cmd)
        {
            string result;
            if (cmd.Length > 2)
            {
                string featureType = cmd[2];

                ITerrainFeature feature;
                if (!m_featureEffects.TryGetValue(featureType, out feature))
                {
                    result = String.Format("Terrain Feature \"{0}\" not found.", featureType);
                }
                else if ((cmd.Length > 3) &&  (cmd[3] == "usage"))
                {
                    result = "Usage: " + feature.GetUsage();
                }
                else
                {
                    result = feature.CreateFeature(m_channel, cmd);
                }

                if(result == String.Empty)
                {
                    result = "Created Feature";
                    m_log.DebugFormat("Created terrain feature {0}", featureType);
                }
            }
            else
            {
                result = "Usage: <feature-name> <arg1> <arg2>...";
            }
            MainConsole.Instance.Output(result);
        }
        #endregion

    }
}
