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
using System.IO;
using System.Reflection;
using libsecondlife;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules.ModuleFramework;
using OpenSim.Region.Environment.Modules.Terrain.Effects;
using OpenSim.Region.Environment.Modules.Terrain.FileLoaders;
using OpenSim.Region.Environment.Modules.Terrain.FloodBrushes;
using OpenSim.Region.Environment.Modules.Terrain.PaintBrushes;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Terrain
{
    public class TerrainModule : IRegionModule, ICommandableModule, ITerrainModule
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

        private readonly Commander m_commander = new Commander("Terrain");

        private readonly Dictionary<StandardTerrainEffects, ITerrainFloodEffect> m_floodeffects =
            new Dictionary<StandardTerrainEffects, ITerrainFloodEffect>();

        private readonly Dictionary<string, ITerrainLoader> m_loaders = new Dictionary<string, ITerrainLoader>();

        private readonly Dictionary<StandardTerrainEffects, ITerrainPaintableEffect> m_painteffects =
            new Dictionary<StandardTerrainEffects, ITerrainPaintableEffect>();

        private ITerrainChannel m_channel;
        private ITerrainChannel m_revert;
        private Scene m_scene;
        private bool m_tainted = false;

        #region ICommandableModule Members

        public ICommander CommandInterface
        {
            get { return m_commander; }
        }

        #endregion

        #region IRegionModule Members

        /// <summary>
        /// Creates and initialises a terrain module for a region
        /// </summary>
        /// <param name="scene">Region initialising</param>
        /// <param name="config">Config for the region</param>
        public void Initialise(Scene scene, IConfigSource config)
        {
            m_scene = scene;

            // Install terrain module in the simulator
            if (m_scene.Heightmap == null)
            {
                lock (m_scene)
                {
                    m_channel = new TerrainChannel();
                    m_scene.Heightmap = m_channel;
                    m_revert = new TerrainChannel();
                    UpdateRevertMap();
                }
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

        /// <summary>
        /// Enables terrain module when called
        /// </summary>
        public void PostInitialise()
        {
            InstallDefaultEffects();
            InstallInterfaces();
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "TerrainModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion

        #region ITerrainModule Members

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
                            m_scene.Heightmap = channel;
                            m_channel = channel;
                            UpdateRevertMap();
                        }
                        catch (NotImplementedException)
                        {
                            m_log.Error("[TERRAIN]: Unable to load heightmap, the " + loader.Value +
                                        " parser does not support file loading. (May be save only)");
                            throw new Exception(String.Format("unable to load heightmap: parser {0} does not support loading", loader.Value));
                        }
                        catch (FileNotFoundException)
                        {
                            m_log.Error(
                                "[TERRAIN]: Unable to load heightmap, file not found. (A directory permissions error may also cause this)");
                            throw new Exception(String.Format("unable to load heightmap: file {0} not found (or permissions do not allow access", 
                                                              filename));
                        }
                    }
                    CheckForTerrainUpdates();
                    m_log.Info("[TERRAIN]: File (" + filename + ") loaded successfully");
                    return;
                }
            }
            m_log.Error("[TERRAIN]: Unable to load heightmap, no file loader availible for that format.");
            throw new Exception(String.Format("unable to load heightmap from file {0}: no loader available for that format", 
                                              filename));
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
                        return;
                    }
                }
            }
            catch (NotImplementedException)
            {
                m_log.Error("Unable to save to " + filename + ", saving of this file format has not been implemented.");
                throw new Exception(String.Format("unable to save heightmap: {0}: saving of this file format not implemented"));
            }
        }

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
        /// Performs updates to the region periodically, synchronising physics and other heightmap aware sections
        /// </summary>
        private void EventManager_OnTerrainTick()
        {
            if (m_tainted)
            {
                m_tainted = false;
                m_scene.PhysicsScene.SetTerrain(m_channel.GetFloatsSerialised());
                m_scene.SaveTerrain();
                m_scene.CreateTerrainTexture(true);
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
        }

        /// <summary>
        /// Checks to see if the terrain has been modified since last check
        /// </summary>
        private void CheckForTerrainUpdates()
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
                        SendToClients(serialised, x, y);
                        shouldTaint = true;
                    }
                }
            }
            if (shouldTaint)
            {
                m_tainted = true;
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
                delegate(IClientAPI controller) { controller.SendLayerData(x / Constants.TerrainPatchSize, y / Constants.TerrainPatchSize, serialised); });
        }

        private void client_OnModifyTerrain(float height, float seconds, byte size, byte action, float north, float west,
                                            float south, float east, IClientAPI remoteClient)
        {
            // Not a good permissions check, if in area mode, need to check the entire area.
            if (m_scene.PermissionsMngr.CanTerraform(remoteClient.AgentId, new LLVector3(north, west, 0)))
            {
                if (north == south && east == west)
                {
                    if (m_painteffects.ContainsKey((StandardTerrainEffects) action))
                    {
                        m_painteffects[(StandardTerrainEffects) action].PaintEffect(
                            m_channel, west, south, size, seconds);

                        CheckForTerrainUpdates();
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
                                        fillArea[x, y] = true;
                                    }
                                }
                            }
                        }

                        m_floodeffects[(StandardTerrainEffects) action].FloodEffect(
                            m_channel, fillArea, size);

                        CheckForTerrainUpdates();
                    }
                    else
                    {
                        m_log.Debug("Unknown terrain flood type " + action);
                    }
                }
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

        private void InterfaceShowDebugStats(Object[] args)
        {
            double max = Double.MinValue;
            double min = double.MaxValue;
            double avg;
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

            avg = sum / (m_channel.Height * m_channel.Width);

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

        private void InterfacePerformEffectTest(Object[] args)
        {
            CookieCutter cookie = new CookieCutter();
            cookie.RunEffect(m_channel);
        }

        private void InstallInterfaces()
        {
            // Load / Save
            string supportedFileExtensions = "";
            foreach (KeyValuePair<string, ITerrainLoader> loader in m_loaders)
                supportedFileExtensions += " " + loader.Key + " (" + loader.Value + ")";

            Command loadFromFileCommand =
                new Command("load", InterfaceLoadFile, "Loads a terrain from a specified file.");
            loadFromFileCommand.AddArgument("filename",
                                            "The file you wish to load from, the file extension determines the loader to be used. Supported extensions include: " +
                                            supportedFileExtensions, "String");

            Command saveToFileCommand =
                new Command("save", InterfaceSaveFile, "Saves the current heightmap to a specified file.");
            saveToFileCommand.AddArgument("filename",
                                          "The destination filename for your heightmap, the file extension determines the format to save in. Supported extensions include: " +
                                          supportedFileExtensions, "String");

            Command loadFromTileCommand =
                new Command("load-tile", InterfaceLoadTileFile, "Loads a terrain from a section of a larger file.");
            loadFromTileCommand.AddArgument("filename",
                                            "The file you wish to load from, the file extension determines the loader to be used. Supported extensions include: " +
                                            supportedFileExtensions, "String");
            loadFromTileCommand.AddArgument("file width", "The width of the file in tiles", "Integer");
            loadFromTileCommand.AddArgument("file height", "The height of the file in tiles", "Integer");
            loadFromTileCommand.AddArgument("minimum X tile", "The X region coordinate of the first section on the file",
                                            "Integer");
            loadFromTileCommand.AddArgument("minimum Y tile", "The Y region coordinate of the first section on the file",
                                            "Integer");

            // Terrain adjustments
            Command fillRegionCommand =
                new Command("fill", InterfaceFillTerrain, "Fills the current heightmap with a specified value.");
            fillRegionCommand.AddArgument("value", "The numeric value of the height you wish to set your region to.",
                                          "Double");

            Command elevateCommand =
                new Command("elevate", InterfaceElevateTerrain, "Raises the current heightmap by the specified amount.");
            elevateCommand.AddArgument("amount", "The amount of height to add to the terrain in meters.", "Double");

            Command lowerCommand =
                new Command("lower", InterfaceLowerTerrain, "Lowers the current heightmap by the specified amount.");
            lowerCommand.AddArgument("amount", "The amount of height to remove from the terrain in meters.", "Double");

            Command multiplyCommand =
                new Command("multiply", InterfaceMultiplyTerrain, "Multiplies the heightmap by the value specified.");
            multiplyCommand.AddArgument("value", "The value to multiply the heightmap by.", "Double");

            Command bakeRegionCommand =
                new Command("bake", InterfaceBakeTerrain, "Saves the current terrain into the regions revert map.");
            Command revertRegionCommand =
                new Command("revert", InterfaceRevertTerrain, "Loads the revert map terrain into the regions heightmap.");

            // Debug
            Command showDebugStatsCommand =
                new Command("stats", InterfaceShowDebugStats,
                            "Shows some information about the regions heightmap for debugging purposes.");

            Command experimentalBrushesCommand =
                new Command("newbrushes", InterfaceEnableExperimentalBrushes,
                            "Enables experimental brushes which replace the standard terrain brushes. WARNING: This is a debug setting and may be removed at any time.");
            experimentalBrushesCommand.AddArgument("Enabled?", "true / false - Enable new brushes", "Boolean");

            // Effects
            Command effectsTestCommand =
                new Command("test", InterfacePerformEffectTest, "Performs an effects module test");

            m_commander.RegisterCommand("load", loadFromFileCommand);
            m_commander.RegisterCommand("load-tile", loadFromTileCommand);
            m_commander.RegisterCommand("save", saveToFileCommand);
            m_commander.RegisterCommand("fill", fillRegionCommand);
            m_commander.RegisterCommand("elevate", elevateCommand);
            m_commander.RegisterCommand("lower", lowerCommand);
            m_commander.RegisterCommand("multiply", multiplyCommand);
            m_commander.RegisterCommand("bake", bakeRegionCommand);
            m_commander.RegisterCommand("revert", revertRegionCommand);
            m_commander.RegisterCommand("newbrushes", experimentalBrushesCommand);
            m_commander.RegisterCommand("test", effectsTestCommand);
            m_commander.RegisterCommand("stats", showDebugStatsCommand);

            // Add this to our scene so scripts can call these functions
            m_scene.RegisterModuleCommander("Terrain", m_commander);
        }

        #endregion
    }
}