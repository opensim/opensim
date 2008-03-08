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
* 
*/

using System;
using System.Collections.Generic;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Terrain
{
    public interface ITerrainPaintableEffect
    {
        void PaintEffect(ITerrainChannel map, double x, double y, double strength, double duration);
    }

    public interface ITerrainFloodEffect
    {
        void FloodEffect(ITerrainChannel map, Boolean[,] fillArea, double strength);
    }

    public interface ITerrainEffect
    {
        void RunEffect(ITerrainChannel map, double strength);
    }

    public interface ITerrainLoader 
    {
        ITerrainChannel LoadFile(string filename);
        void SaveFile(string filename, ITerrainChannel map);
    }

    /// <summary>
    /// A new version of the old Channel class, simplified
    /// </summary>
    public class TerrainChannel : ITerrainChannel
    {
        private double[,] map;
        private bool[,] taint;

        public int Width
        {
            get { return map.GetLength(0); }
        }

        public int Height
        {
            get { return map.GetLength(1); }
        }

        public TerrainChannel Copy()
        {
            TerrainChannel copy = new TerrainChannel(false);
            copy.map = (double[,])this.map.Clone();

            return copy;
        }

        public float[] GetFloatsSerialised()
        {
            float[] heights = new float[Width * Height];
            int i;

            for (i = 0; i < Width * Height; i++)
            {
                heights[i] = (float)map[i % Width, i / Width];
            }

            return heights;
        }

        public double[,] GetDoubles()
        {
            return map;
        }

        public double this[int x, int y]
        {
            get
            {
                return map[x, y];
            }
            set
            {
                if (map[x, y] != value)
                {
                    taint[x / 16, y / 16] = true;
                    map[x, y] = value;
                }
            }
        }

        public bool Tainted(int x, int y)
        {
            if (taint[x / 16, y / 16] != false)
            {
                taint[x / 16, y / 16] = false;
                return true;
            }
            else
            {
                return false;
            }
        }

        public TerrainChannel()
        {
            map = new double[Constants.RegionSize, Constants.RegionSize];
            taint = new bool[Constants.RegionSize / 16, Constants.RegionSize / 16];

            int x, y;
            for (x = 0; x < Constants.RegionSize; x++)
            {
                for (y = 0; y < Constants.RegionSize; y++)
                {
                    map[x, y] = 60.0 - // 60 = Sphere Radius
                        ((x - (Constants.RegionSize / 2)) * (x - (Constants.RegionSize / 2)) +
                        (y - (Constants.RegionSize / 2)) * (y - (Constants.RegionSize / 2)));
                }
            }
        }

        public TerrainChannel(double[,] import)
        {
            map = import;
            taint = new bool[import.GetLength(0), import.GetLength(1)];
        }

        public TerrainChannel(bool createMap)
        {
            if (createMap)
            {
                map = new double[Constants.RegionSize, Constants.RegionSize];
                taint = new bool[Constants.RegionSize / 16, Constants.RegionSize / 16];
            }
        }

        public TerrainChannel(int w, int h)
        {
            map = new double[w, h];
            taint = new bool[w / 16, h / 16];
        }
    }

    public enum StandardTerrainEffects : byte
    {
        Flatten = 0,
        Raise = 1,
        Lower = 2,
        Smooth = 3,
        Noise = 4,
        Revert = 5
    }

    public class TerrainModule : IRegionModule
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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
                        ITerrainChannel channel = loader.Value.LoadFile(filename);
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

        void EventManager_OnPluginConsole(string[] args)
        {
            if (args[0] == "terrain")
            {
                string command = args[1];
                string param = args[2];

                int x, y;

                switch (command)
                {
                    case "load":
                        LoadFromFile(param);
                        SendUpdatedLayerData();
                        break;
                    case "save":
                        SaveToFile(param);
                        break;
                    case "fill":
                        for (x = 0; x < m_channel.Width; x++)
                            for (y = 0; y < m_channel.Height; y++)
                                m_channel[x, y] = Double.Parse(param);
                        SendUpdatedLayerData();
                        break;
                    case "newbrushes":
                        if (Boolean.Parse(param))
                        {
                            m_painteffects[StandardTerrainEffects.Revert] = new PaintBrushes.WeatherSphere();
                        }
                        else
                        {
                            InstallDefaultEffects();
                        }
                        break;
                    default:
                        m_log.Warn("Unknown terrain command.");
                        break;
                }
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
                            m_channel, west, south, Math.Pow(size, 2.0), seconds);

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
                                    if (y < south && y > north)
                                    {
                                        fillArea[x, y] = true;
                                    }
                                }
                            }
                        }

                        m_floodeffects[(StandardTerrainEffects)action].FloodEffect(
                            m_channel, fillArea, Math.Pow(size, 2.0));
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

        public void PostInitialise()
        {
            InstallDefaultEffects();
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
    }
}
