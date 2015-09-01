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
 * 
 * The design of this map service is based on SimianGrid's PHP-based 
 * map service. See this URL for the original PHP version:
 * https://github.com/openmetaversefoundation/simiangrid/
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

using Nini.Config;
using log4net;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Services.Interfaces;


namespace OpenSim.Services.MapImageService
{
    public class MapImageService : IMapImageService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);
#pragma warning disable 414
        private string LogHeader = "[MAP IMAGE SERVICE]";
#pragma warning restore 414

        private const int ZOOM_LEVELS = 8;
        private const int IMAGE_WIDTH = 256;
        private const int HALF_WIDTH = 128;
        private const int JPEG_QUALITY = 80;

        private static string m_TilesStoragePath = "maptiles";

        private static object m_Sync = new object();
        private static bool m_Initialized = false;
        private static string m_WaterTileFile = string.Empty;
        private static Color m_Watercolor = Color.FromArgb(29, 71, 95);

        public MapImageService(IConfigSource config)
        {
            if (!m_Initialized)
            {
                m_Initialized = true;
                m_log.Debug("[MAP IMAGE SERVICE]: Starting MapImage service");

                IConfig serviceConfig = config.Configs["MapImageService"];
                if (serviceConfig != null)
                {
                    m_TilesStoragePath = serviceConfig.GetString("TilesStoragePath", m_TilesStoragePath);
                    if (!Directory.Exists(m_TilesStoragePath))
                        Directory.CreateDirectory(m_TilesStoragePath);


                    m_WaterTileFile = Path.Combine(m_TilesStoragePath, "water.jpg");
                    if (!File.Exists(m_WaterTileFile))
                    {
                        Bitmap waterTile = new Bitmap(IMAGE_WIDTH, IMAGE_WIDTH);
                        FillImage(waterTile, m_Watercolor);
                        waterTile.Save(m_WaterTileFile, ImageFormat.Jpeg);
                    }
                }
            }
        }

        #region IMapImageService

        public bool AddMapTile(int x, int y, byte[] imageData, out string reason)
        {
            reason = string.Empty;
            string fileName = GetFileName(1, x, y);

            lock (m_Sync)
            {
                try
                {
                    using (FileStream f = File.Open(fileName, FileMode.OpenOrCreate, FileAccess.Write))
                        f.Write(imageData, 0, imageData.Length);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("[MAP IMAGE SERVICE]: Unable to save image file {0}: {1}", fileName, e);
                    reason = e.Message;
                    return false;
                }
            }

            return UpdateMultiResolutionFilesAsync(x, y, out reason);
        }

        public bool RemoveMapTile(int x, int y, out string reason)
        {
            reason = String.Empty;
            string fileName = GetFileName(1, x, y);

            lock (m_Sync)
            {
                try
                {
                    File.Delete(fileName);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("[MAP IMAGE SERVICE]: Unable to save delete file {0}: {1}", fileName, e);
                    reason = e.Message;
                    return false;
                }
            }

            return UpdateMultiResolutionFilesAsync(x, y, out reason);
        }

        // When large varregions start up, they can send piles of new map tiles. This causes
        //    this multi-resolution routine to be called a zillion times an causes much CPU
        //    time to be spent creating multi-resolution tiles that will be replaced when
        //    the next maptile arrives.
        private class mapToMultiRez
        {
            public int xx;
            public int yy;
            public mapToMultiRez(int pX, int pY)
            {
                xx = pX;
                yy = pY;
            }
        };
        private Queue<mapToMultiRez> multiRezToBuild = new Queue<mapToMultiRez>();
        private bool UpdateMultiResolutionFilesAsync(int x, int y, out string reason)
        {
            reason = String.Empty;
            lock (multiRezToBuild)
            {
                // m_log.DebugFormat("{0} UpdateMultiResolutionFilesAsync: scheduling update for <{1},{2}>", LogHeader, x, y);
                multiRezToBuild.Enqueue(new mapToMultiRez(x, y));
                if (multiRezToBuild.Count == 1)
                    Util.FireAndForget(
                        DoUpdateMultiResolutionFilesAsync, null, "MapImageService.DoUpdateMultiResolutionFilesAsync");
            }

            return true;
        }

        private void DoUpdateMultiResolutionFilesAsync(object o)
        {
            // This sleep causes the FireAndForget thread to be different than the invocation thread.
            // It also allows other tiles to be uploaded so the multi-rez images are more likely
            //     to be correct.
            Thread.Sleep(1 * 1000);

            while (multiRezToBuild.Count > 0)
            {
                mapToMultiRez toMultiRez = null;
                lock (multiRezToBuild)
                {
                    if (multiRezToBuild.Count > 0)
                        toMultiRez = multiRezToBuild.Dequeue();
                }
                if (toMultiRez != null)
                {
                    int x = toMultiRez.xx;
                    int y = toMultiRez.yy;
                    // m_log.DebugFormat("{0} DoUpdateMultiResolutionFilesAsync: doing build for <{1},{2}>", LogHeader, x, y);

                    // Stitch seven more aggregate tiles together
                    for (uint zoomLevel = 2; zoomLevel <= ZOOM_LEVELS; zoomLevel++)
                    {
                        // Calculate the width (in full resolution tiles) and bottom-left
                        // corner of the current zoom level
                        int width = (int)Math.Pow(2, (double)(zoomLevel - 1));
                        int x1 = x - (x % width);
                        int y1 = y - (y % width);

                        lock (m_Sync)   // must lock the reading and writing of the maptile files
                        {
                            if (!CreateTile(zoomLevel, x1, y1))
                            {
                                m_log.WarnFormat("[MAP IMAGE SERVICE]: Unable to create tile for {0},{1} at zoom level {1}", x, y, zoomLevel);
                                return;
                            }
                        }
                    }
                }
            }

            return;
        }

        public byte[] GetMapTile(string fileName, out string format)
        {
//            m_log.DebugFormat("[MAP IMAGE SERVICE]: Getting map tile {0}", fileName);

            format = ".jpg";
            string fullName = Path.Combine(m_TilesStoragePath, fileName);
            if (File.Exists(fullName))
            {
                format = Path.GetExtension(fileName).ToLower();
                //m_log.DebugFormat("[MAP IMAGE SERVICE]: Found file {0}, extension {1}", fileName, format);
                return File.ReadAllBytes(fullName);
            }
            else if (File.Exists(m_WaterTileFile))
            {
                return File.ReadAllBytes(m_WaterTileFile);
            }
            else
            {
                m_log.DebugFormat("[MAP IMAGE SERVICE]: unable to get file {0}", fileName);
                return new byte[0];
            }
        }

        #endregion


        private string GetFileName(uint zoomLevel, int x, int y)
        {
            string extension = "jpg";
            return Path.Combine(m_TilesStoragePath, string.Format("map-{0}-{1}-{2}-objects.{3}", zoomLevel, x, y, extension));
        }

        private Bitmap GetInputTileImage(string fileName)
        {
            try
            {
                if (File.Exists(fileName))
                    return new Bitmap(fileName);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[MAP IMAGE SERVICE]: Unable to read image data from {0}: {1}", fileName, e);
            }

            return null;
        }

        private Bitmap GetOutputTileImage(string fileName)
        {
            try
            {
                if (File.Exists(fileName))                    
                    return new Bitmap(fileName);

                else
                {
                    // Create a new output tile with a transparent background
                    Bitmap bm = new Bitmap(IMAGE_WIDTH, IMAGE_WIDTH, PixelFormat.Format24bppRgb);
                    bm.MakeTransparent();
                    return bm;
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[MAP IMAGE SERVICE]: Unable to read image data from {0}: {1}", fileName, e);
            }

            return null;
        }

        private bool CreateTile(uint zoomLevel, int x, int y)
        {
//            m_log.DebugFormat("[MAP IMAGE SERVICE]: Create tile for {0} {1}, zoom {2}", x, y, zoomLevel);
            int prevWidth = (int)Math.Pow(2, (double)zoomLevel - 2);
            int thisWidth = (int)Math.Pow(2, (double)zoomLevel - 1);

            // Convert x and y to the bottom left tile for this zoom level
            int xIn = x - (x % prevWidth);
            int yIn = y - (y % prevWidth);

            // Convert x and y to the bottom left tile for the next zoom level
            int xOut = x - (x % thisWidth);
            int yOut = y - (y % thisWidth);

            // Try to open the four input tiles from the previous zoom level
            Bitmap inputBL = GetInputTileImage(GetFileName(zoomLevel - 1, xIn, yIn));
            Bitmap inputBR = GetInputTileImage(GetFileName(zoomLevel - 1, xIn + prevWidth, yIn));
            Bitmap inputTL = GetInputTileImage(GetFileName(zoomLevel - 1, xIn, yIn + prevWidth));
            Bitmap inputTR = GetInputTileImage(GetFileName(zoomLevel - 1, xIn + prevWidth, yIn + prevWidth));

            // Open the output tile (current zoom level)
            string outputFile = GetFileName(zoomLevel, xOut, yOut);
            Bitmap output = GetOutputTileImage(outputFile);
            if (output == null)
                return false;
            FillImage(output, m_Watercolor);

            if (inputBL != null)
            {
                ImageCopyResampled(output, inputBL, 0, HALF_WIDTH, 0, 0);
                inputBL.Dispose();
            }
            if (inputBR != null)
            {
                ImageCopyResampled(output, inputBR, HALF_WIDTH, HALF_WIDTH, 0, 0);
                inputBR.Dispose();
            }
            if (inputTL != null)
            {
                ImageCopyResampled(output, inputTL, 0, 0, 0, 0);
                inputTL.Dispose();
            }
            if (inputTR != null)
            {
                ImageCopyResampled(output, inputTR, HALF_WIDTH, 0, 0, 0);
                inputTR.Dispose();
            }

            // Write the modified output
            try
            {
                using (Bitmap final = new Bitmap(output))
                {
                    output.Dispose();
                    final.Save(outputFile, ImageFormat.Jpeg);
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[MAP IMAGE SERVICE]: Oops on saving {0} {1}", outputFile, e);
            }

            // Save also as png?

            return true;
        }

        #region Image utilities

        private void FillImage(Bitmap bm, Color c)
        {
            for (int x = 0; x < bm.Width; x++)
                for (int y = 0; y < bm.Height; y++)
                    bm.SetPixel(x, y, c);
        }

        private void ImageCopyResampled(Bitmap output, Bitmap input, int destX, int destY, int srcX, int srcY)
        {
            int resamplingRateX = 2; // (input.Width - srcX) / (output.Width - destX);
            int resamplingRateY = 2; //  (input.Height - srcY) / (output.Height - destY);

            for (int x = destX; x < destX + HALF_WIDTH; x++)
                for (int y = destY; y < destY + HALF_WIDTH; y++)
                {
                    Color p = input.GetPixel(srcX + (x - destX) * resamplingRateX, srcY + (y - destY) * resamplingRateY);
                    output.SetPixel(x, y, p);
                }
        }

        #endregion
    }
}
