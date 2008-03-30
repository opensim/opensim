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
using System.Drawing;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Modules.ModuleFramework;


namespace OpenSim.Region.Environment.Modules.Terrain
{
    public class TerrainModule : IRegionModule , ITerrainTemp, ICommandableModule
    {
        public enum StandardTerrainEffects : byte
        {
            Flatten = 0,
            Raise = 1,
            Lower = 2,
            Smooth = 3,
            Noise = 4,
            Revert = 5
        }

        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Commander m_commander = new Commander("Terrain");

        private Dictionary<StandardTerrainEffects, ITerrainPaintableEffect> m_painteffects =
            new Dictionary<StandardTerrainEffects, ITerrainPaintableEffect>();
        private Dictionary<StandardTerrainEffects, ITerrainFloodEffect> m_floodeffects =
            new Dictionary<StandardTerrainEffects, ITerrainFloodEffect>();
        private Dictionary<string, ITerrainLoader> m_loaders = new Dictionary<string, ITerrainLoader>();
        Scene m_scene;
        ITerrainChannel m_channel;
        ITerrainChannel m_revert;
        bool m_tainted = false;
        private IConfigSource m_gConfig;

        private void InstallDefaultEffects()
        {
            // Draggable Paint Brush Effects
            m_painteffects[StandardTerrainEffects.Raise]    = new PaintBrushes.RaiseSphere();
            m_painteffects[StandardTerrainEffects.Lower]    = new PaintBrushes.LowerSphere();
            m_painteffects[StandardTerrainEffects.Smooth]   = new PaintBrushes.SmoothSphere();
            m_painteffects[StandardTerrainEffects.Noise]    = new PaintBrushes.NoiseSphere();
            m_painteffects[StandardTerrainEffects.Flatten]  = new PaintBrushes.FlattenSphere();
            m_painteffects[StandardTerrainEffects.Revert]   = new PaintBrushes.RevertSphere(m_revert);

            // Area of effect selection effects
            m_floodeffects[StandardTerrainEffects.Raise]    = new FloodBrushes.RaiseArea();
            m_floodeffects[StandardTerrainEffects.Lower]    = new FloodBrushes.LowerArea();
            m_floodeffects[StandardTerrainEffects.Smooth]   = new FloodBrushes.SmoothArea();
            m_floodeffects[StandardTerrainEffects.Noise]    = new FloodBrushes.NoiseArea();
            m_floodeffects[StandardTerrainEffects.Flatten]  = new FloodBrushes.FlattenArea();
            m_floodeffects[StandardTerrainEffects.Revert]   = new FloodBrushes.RevertArea(m_revert);

            // Filesystem load/save loaders
            m_loaders[".r32"] = new FileLoaders.RAW32();
            m_loaders[".f32"] = m_loaders[".r32"];
            m_loaders[".ter"] = new FileLoaders.Terragen();
            m_loaders[".raw"] = new FileLoaders.LLRAW();
            m_loaders[".jpg"] = new FileLoaders.JPEG();
            m_loaders[".jpeg"] = m_loaders[".jpg"];
        }

        public void UpdateRevertMap()
        {
            int x, y;
            for (x = 0; x < m_channel.Width; x++)
            {
                for (y = 0; y < m_channel.Height; y++)
                {
                    m_revert[x, y] = m_channel[x, y];
                }
            }
        }

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
                            m_log.Error("[TERRAIN]: Unable to load heightmap, the " + loader.Value.ToString() + " parser does not support file loading. (May be save only)");
                            return;
                        }
                        catch (System.IO.FileNotFoundException)
                        {
                            m_log.Error("[TERRAIN]: Unable to load heightmap, file not found. (A directory permissions error may also cause this)");
                            return;
                        }
                    }
                    m_log.Info("[TERRAIN]: File (" + filename + ") loaded successfully");
                    return;
                }
            }
            m_log.Error("[TERRAIN]: Unable to load heightmap, no file loader availible for that format.");
        }

        public void LoadFromFile(string filename, int fileWidth, int fileHeight, int fileStartX, int fileStartY)
        {
            fileStartX -= (int)m_scene.RegionInfo.RegionLocX;
            fileStartY -= (int)m_scene.RegionInfo.RegionLocY;

            foreach (KeyValuePair<string, ITerrainLoader> loader in m_loaders)
            {
                if (filename.EndsWith(loader.Key))
                {
                    lock (m_scene)
                    {
                        ITerrainChannel channel = loader.Value.LoadFile(filename, fileStartX, fileStartY,
                            fileWidth, fileHeight, (int)Constants.RegionSize, (int)Constants.RegionSize);
                        m_scene.Heightmap = channel;
                        m_channel = channel;
                        UpdateRevertMap();
                    }
                    return;
                }
            }
        }

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
            }
        }

        public void Initialise(Scene scene, IConfigSource config)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<ITerrainTemp>(this);
            m_gConfig = config;

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

            m_scene.EventManager.OnNewClient += EventManager_OnNewClient;
            m_scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
            m_scene.EventManager.OnTerrainTick += EventManager_OnTerrainTick;
        }

        void EventManager_OnTerrainTick()
        {
            if (m_tainted)
            {
                m_tainted = false;
                m_scene.PhysicsScene.SetTerrain(m_channel.GetFloatsSerialised());
                m_scene.SaveTerrain();
            }
        }

        #region Console Commands

        private void InterfaceLoadFile(Object[] args)
        {
            LoadFromFile((string)args[0]);
        }

        private void InterfaceLoadTileFile(Object[] args)
        {
            LoadFromFile((string)args[0],
                (int)args[1],
                (int)args[2],
                (int)args[3],
                (int)args[4]);
        }

        private void InterfaceSaveFile(Object[] args)
        {
            SaveToFile((string)args[0]);
        }

        private void InterfaceFillTerrain(Object[] args)
        {
            int x, y;

            for (x = 0; x < m_channel.Width; x++)
                for (y = 0; y < m_channel.Height; y++)
                    m_channel[x, y] = (double)args[0];
            SendUpdatedLayerData();
        }

        private void InterfaceEnableExperimentalBrushes(Object[] args)
        {
            if ((bool)args[0])
            {
                m_painteffects[StandardTerrainEffects.Revert] = new PaintBrushes.WeatherSphere();
                m_painteffects[StandardTerrainEffects.Flatten] = new PaintBrushes.OlsenSphere();
                m_painteffects[StandardTerrainEffects.Smooth] = new PaintBrushes.ErodeSphere();
            }
            else
            {
                InstallDefaultEffects();
            }
        }

        private void InstallInterfaces()
        {
            // Load / Save
            string supportedFileExtensions = "";
            foreach (KeyValuePair<string,ITerrainLoader> loader in m_loaders)
                supportedFileExtensions += " " + loader.Key + " (" + loader.Value.ToString() + ")";

            Command loadFromFileCommand = new Command("load", InterfaceLoadFile, "Loads a terrain from a specified file.");
            loadFromFileCommand.AddArgument("filename", "The file you wish to load from, the file extension determines the loader to be used. Supported extensions include: " + supportedFileExtensions, "String");

            Command saveToFileCommand = new Command("save", InterfaceSaveFile, "Saves the current heightmap to a specified file.");
            saveToFileCommand.AddArgument("filename", "The destination filename for your heightmap, the file extension determines the format to save in. Supported extensions include: " + supportedFileExtensions, "String");

            Command loadFromTileCommand = new Command("load-tile", InterfaceLoadTileFile, "Loads a terrain from a section of a larger file.");
            loadFromTileCommand.AddArgument("filename", "The file you wish to load from, the file extension determines the loader to be used. Supported extensions include: " + supportedFileExtensions, "String");
            loadFromTileCommand.AddArgument("file width", "The width of the file in tiles", "Integer");
            loadFromTileCommand.AddArgument("file height", "The height of the file in tiles", "Integer");
            loadFromTileCommand.AddArgument("minimum X tile", "The X region coordinate of the first section on the file", "Integer");
            loadFromTileCommand.AddArgument("minimum Y tile", "The Y region coordinate of the first section on the file", "Integer");

            // Terrain adjustments
            Command fillRegionCommand = new Command("fill", InterfaceFillTerrain, "Fills the current heightmap with a specified value.");
            fillRegionCommand.AddArgument("value", "The numeric value of the height you wish to set your region to.", "Double");

            // Brushes
            Command experimentalBrushesCommand = new Command("newbrushes", InterfaceEnableExperimentalBrushes, "Enables experimental brushes which replace the standard terrain brushes. WARNING: This is a debug setting and may be removed at any time.");
            experimentalBrushesCommand.AddArgument("Enabled?", "true / false - Enable new brushes", "Boolean");

            m_commander.RegisterCommand("load", loadFromFileCommand);
            m_commander.RegisterCommand("load-tile", loadFromTileCommand);
            m_commander.RegisterCommand("save", saveToFileCommand);
            m_commander.RegisterCommand("fill", fillRegionCommand);
            m_commander.RegisterCommand("newbrushes", experimentalBrushesCommand);

            // Add this to our scene so scripts can call these functions
            m_scene.RegisterModuleCommander("Terrain", m_commander);
        }

        #endregion

        void EventManager_OnPluginConsole(string[] args)
        {
            if (args[0] == "terrain")
            {
                string[] tmpArgs = new string[args.Length - 2];
                int i = 0;
                for (i = 2; i < args.Length; i++)
                    tmpArgs[i - 2] = args[i];
                
                m_commander.ProcessConsoleCommand(args[1], tmpArgs);
            }
        }

        void EventManager_OnNewClient(IClientAPI client)
        {
            client.OnModifyTerrain += client_OnModifyTerrain;
        }

        void SendUpdatedLayerData()
        {
            bool shouldTaint = false;
            float[] serialised = m_channel.GetFloatsSerialised();
            int x, y;
            for (x = 0; x < m_channel.Width; x += Constants.TerrainPatchSize)
            {
                for (y = 0; y < m_channel.Height; y += Constants.TerrainPatchSize)
                {
                    if (m_channel.Tainted(x, y))
                    {
                        m_scene.ForEachClient(delegate(IClientAPI controller)
                        {
                            controller.SendLayerData(x / Constants.TerrainPatchSize, y / Constants.TerrainPatchSize, serialised);
                        });
                        shouldTaint = true;
                    }
                }
            }
            if (shouldTaint)
            {
                m_tainted = true;
            }
        }

        void client_OnModifyTerrain(float height, float seconds, byte size, byte action, float north, float west, float south, float east, IClientAPI remoteClient)
        {
            // Not a good permissions check, if in area mode, need to check the entire area.
            if (m_scene.PermissionsMngr.CanTerraform(remoteClient.AgentId, new LLVector3(north, west, 0)))
            {

                if (north == south && east == west)
                {
                    if (m_painteffects.ContainsKey((StandardTerrainEffects)action))
                    {
                        m_painteffects[(StandardTerrainEffects)action].PaintEffect(
                            m_channel, west, south, size, seconds);

                        bool usingTerrainModule = true;

                        if (usingTerrainModule)
                        {
                            SendUpdatedLayerData();
                        }
                    }
                    else
                    {
                        m_log.Debug("Unknown terrain brush type " + action.ToString());
                    }
                }
                else
                {
                    if (m_floodeffects.ContainsKey((StandardTerrainEffects)action))
                    {
                        bool[,] fillArea = new bool[m_channel.Width, m_channel.Height];
                        fillArea.Initialize();

                        int x, y;
    
                        for (x = 0; x < m_channel.Width; x++)
                        {
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

                        m_floodeffects[(StandardTerrainEffects)action].FloodEffect(
                            m_channel, fillArea, size);
                        bool usingTerrainModule = true;

                        if (usingTerrainModule)
                        {
                            SendUpdatedLayerData();
                        }
                    }
                    else
                    {
                        m_log.Debug("Unknown terrain flood type " + action.ToString());
                    }
                }
            }
        }

        public byte[] WriteJpegImage(string gradientmap)
        {
            byte[] imageData = null;
            try
            {
                Bitmap bmp = TerrainToBitmap(gradientmap);

                imageData = OpenJPEGNet.OpenJPEG.EncodeFromImage(bmp, true);
                
            }
            catch (Exception e) // LEGIT: Catching problems caused by OpenJPEG p/invoke
            {
                Console.WriteLine("Failed generating terrain map: " + e.ToString());
            }

            return imageData;
        }

        private Bitmap TerrainToBitmap(string gradientmap)
        {
            Bitmap gradientmapLd = new Bitmap(gradientmap);

            int pallete = gradientmapLd.Height;

            Bitmap bmp = new Bitmap(m_channel.Width, m_channel.Height);
            Color[] colours = new Color[pallete];

            for (int i = 0; i < pallete; i++)
            {
                colours[i] = gradientmapLd.GetPixel(0, i);
            }

            TerrainChannel copy =(TerrainChannel) m_channel.MakeCopy();
            for (int y = 0; y < copy.Height; y++)
            {
                for (int x = 0; x < copy.Width; x++)
                {
                    // 512 is the largest possible height before colours clamp
                    int colorindex = (int)(Math.Max(Math.Min(1.0, copy[x, y] / 512.0), 0.0) * (pallete - 1));
                    bmp.SetPixel(x, copy.Height - y - 1, colours[colorindex]);
                }
            }
            return bmp;
        }

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

        #region ICommandable Members

        public ICommander CommandInterface
        {
            get { return m_commander; }
        }

        #endregion
    }
}
