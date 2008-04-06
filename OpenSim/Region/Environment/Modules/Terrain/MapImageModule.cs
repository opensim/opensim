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
    class MapImageModule : ITerrainTemp, IRegionModule
    {
        private Scene m_scene;
        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<ITerrainTemp>(this);
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "MapImageModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
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

            Bitmap bmp = new Bitmap(m_scene.Heightmap.Width, m_scene.Heightmap.Height);
            Color[] colours = new Color[pallete];

            for (int i = 0; i < pallete; i++)
            {
                colours[i] = gradientmapLd.GetPixel(0, i);
            }

            lock (m_scene.Heightmap)
            {
                ITerrainChannel copy = m_scene.Heightmap;
                for (int y = 0; y < copy.Height; y++)
                {
                    for (int x = 0; x < copy.Width; x++)
                    {
                        // 512 is the largest possible height before colours clamp
                        int colorindex = (int)(Math.Max(Math.Min(1.0, copy[x, y] / 512.0), 0.0) * (pallete - 1));
                        bmp.SetPixel(x, copy.Height - y - 1, colours[colorindex]);
                    }
                }
            }
            return bmp;
        }


        #endregion
    }
}
