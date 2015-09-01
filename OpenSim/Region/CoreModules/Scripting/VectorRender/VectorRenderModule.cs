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
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenSim.Region.CoreModules.Scripting.DynamicTexture;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using log4net;
using System.Reflection;
using Mono.Addins;

//using Cairo;

namespace OpenSim.Region.CoreModules.Scripting.VectorRender
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "VectorRenderModule")]
    public class VectorRenderModule : ISharedRegionModule, IDynamicTextureRender
    {
        // These fields exist for testing purposes, please do not remove.
//        private static bool s_flipper;
//        private static byte[] s_asset1Data;
//        private static byte[] s_asset2Data;

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private IDynamicTextureManager m_textureManager;

        private Graphics m_graph;
        private string m_fontName = "Arial";

        public VectorRenderModule()
        {
        }

        #region IDynamicTextureRender Members

        public string GetContentType()
        {
            return "vector";
        }

        public string GetName()
        {
            return Name;
        }

        public bool SupportsAsynchronous()
        {
            return true;
        }

//        public bool AlwaysIdenticalConversion(string bodyData, string extraParams)
//        {
//            string[] lines = GetLines(bodyData);
//            return lines.Any((str, r) => str.StartsWith("Image"));
//        }

        public IDynamicTexture ConvertUrl(string url, string extraParams)
        {
            return null;
        }

        public IDynamicTexture ConvertData(string bodyData, string extraParams)
        {
            return Draw(bodyData, extraParams);
        }

        public bool AsyncConvertUrl(UUID id, string url, string extraParams)
        {
            return false;
        }

        public bool AsyncConvertData(UUID id, string bodyData, string extraParams)
        {
            if (m_textureManager == null)
            {
                m_log.Warn("[VECTORRENDERMODULE]: No texture manager. Can't function");
                return false;
            }
            // XXX: This isn't actually being done asynchronously!
            m_textureManager.ReturnData(id, ConvertData(bodyData, extraParams));

            return true;
        }

        public void GetDrawStringSize(string text, string fontName, int fontSize, 
                                      out double xSize, out double ySize)
        {
            lock (this)
            {
                using (Font myFont = new Font(fontName, fontSize))
                {
                    SizeF stringSize = new SizeF();

                    // XXX: This lock may be unnecessary.
                    lock (m_graph)
                    {
                        stringSize = m_graph.MeasureString(text, myFont);
                        xSize = stringSize.Width;
                        ySize = stringSize.Height;
                    }
                }
            }
        }

        #endregion

        #region ISharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
            IConfig cfg = config.Configs["VectorRender"];
            if (null != cfg)
            {
                m_fontName = cfg.GetString("font_name", m_fontName);
            }
            m_log.DebugFormat("[VECTORRENDERMODULE]: using font \"{0}\" for text rendering.", m_fontName);

            // We won't dispose of these explicitly since this module is only removed when the entire simulator
            // is shut down.
            Bitmap bitmap = new Bitmap(1024, 1024, PixelFormat.Format32bppArgb);
            m_graph = Graphics.FromImage(bitmap);
        }

        public void PostInitialise()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (m_scene == null)
            {
                m_scene = scene;
            }
        }

        public void RegionLoaded(Scene scene)
        {
            if (m_textureManager == null && m_scene == scene)
            {
                m_textureManager = m_scene.RequestModuleInterface<IDynamicTextureManager>();
                if (m_textureManager != null)
                {
                    m_textureManager.RegisterRender(GetContentType(), this);
                }
            }
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "VectorRenderModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        private IDynamicTexture Draw(string data, string extraParams)
        {
            // We need to cater for old scripts that didnt use extraParams neatly, they use either an integer size which represents both width and height, or setalpha
            // we will now support multiple comma seperated params in the form  width:256,height:512,alpha:255
            int width = 256;
            int height = 256;
            int alpha = 255; // 0 is transparent
            Color bgColor = Color.White;  // Default background color
            char altDataDelim = ';';
            
            char[] paramDelimiter = { ',' };
            char[] nvpDelimiter = { ':' };
           
            extraParams = extraParams.Trim();
            extraParams = extraParams.ToLower();
            
            string[] nvps = extraParams.Split(paramDelimiter);
            
            int temp = -1;
            foreach (string pair in nvps)
            {
                string[] nvp = pair.Split(nvpDelimiter);
                string name = "";
                string value = "";
                
                if (nvp[0] != null)
                {
                    name = nvp[0].Trim();
                }
                
                if (nvp.Length == 2)
                {
                    value = nvp[1].Trim();
                }
                
                switch (name)
                {
                    case "width":
                        temp = parseIntParam(value);
                        if (temp != -1)
                        {
                            if (temp < 1)
                            {
                                width = 1;
                            }
                            else if (temp > 2048)
                            {
                                width = 2048;
                            }
                            else
                            {
                                width = temp;
                            }
                        }
                        break;
                    case "height":
                        temp = parseIntParam(value);
                        if (temp != -1)
                        {
                            if (temp < 1)
                            {
                                height = 1;
                            }
                            else if (temp > 2048)
                            {
                                height = 2048;
                            }
                            else
                            {
                                height = temp;
                            }
                        }
                        break;
                     case "alpha":
                          temp = parseIntParam(value);
                          if (temp != -1)
                          {
                              if (temp < 0)
                              {
                                  alpha = 0;
                              }
                              else if (temp > 255)
                              {
                                  alpha = 255;
                              }
                              else
                              {
                                  alpha = temp;
                              }
                          }
                          // Allow a bitmap w/o the alpha component to be created
                          else if (value.ToLower() == "false") {
                               alpha = 256;
                          }
                          break;
                     case "bgcolor":
                     case "bgcolour":
                          int hex = 0;
                         if (Int32.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hex))
                         {
                             bgColor = Color.FromArgb(hex);
                         } 
                         else
                         {
                             bgColor = Color.FromName(value);
                         }
                         break;
                     case "altdatadelim":
                         altDataDelim = value.ToCharArray()[0];
                         break;
                     case "":
                         // blank string has been passed do nothing just use defaults
                     break;
                     default: // this is all for backwards compat, all a bit ugly hopfully can be removed in future
                         // could be either set alpha or just an int
                         if (name == "setalpha")
                         {
                             alpha = 0; // set the texture to have transparent background (maintains backwards compat)
                         }
                         else
                         {
                             // this function used to accept an int on its own that represented both 
                             // width and height, this is to maintain backwards compat, could be removed
                             // but would break existing scripts
                             temp = parseIntParam(name);
                             if (temp != -1)
                             {
                                 if (temp > 1024)
                                    temp = 1024;
                                    
                                 if (temp < 128)
                                     temp = 128;
                                  
                                 width = temp;
                                 height = temp;
                             }
                         }
                     break;
                }
            }

            Bitmap bitmap = null;
            Graphics graph = null;
            bool reuseable = false;

            try
            {
                // XXX: In testing, it appears that if multiple threads dispose of separate GDI+ objects simultaneously,
                // the native malloc heap can become corrupted, possibly due to a double free().  This may be due to
                // bugs in the underlying libcairo used by mono's libgdiplus.dll on Linux/OSX.  These problems were
                // seen with both libcario 1.10.2-6.1ubuntu3 and 1.8.10-2ubuntu1.  They go away if disposal is perfomed
                // under lock.
                lock (this)
                {
                    if (alpha == 256)
                        bitmap = new Bitmap(width, height, PixelFormat.Format32bppRgb);
                    else
                        bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
    
                    graph = Graphics.FromImage(bitmap);
        
                    // this is really just to save people filling the 
                    // background color in their scripts, only do when fully opaque
                    if (alpha >= 255)
                    {
                        using (SolidBrush bgFillBrush = new SolidBrush(bgColor))
                        {
                            graph.FillRectangle(bgFillBrush, 0, 0, width, height);
                        }
                    }
        
                    for (int w = 0; w < bitmap.Width; w++)
                    {
                        if (alpha <= 255) 
                        {
                            for (int h = 0; h < bitmap.Height; h++)
                            {
                                bitmap.SetPixel(w, h, Color.FromArgb(alpha, bitmap.GetPixel(w, h)));
                            }
                        }
                    }
        
                    GDIDraw(data, graph, altDataDelim, out reuseable);
                }
    
                byte[] imageJ2000 = new byte[0];

                // This code exists for testing purposes, please do not remove.
//                if (s_flipper)
//                    imageJ2000 = s_asset1Data;
//                else
//                    imageJ2000 = s_asset2Data;
//
//                s_flipper = !s_flipper;
    
                try
                {
                    imageJ2000 = OpenJPEG.EncodeFromImage(bitmap, true);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[VECTORRENDERMODULE]: OpenJpeg Encode Failed.  Exception {0}{1}",
                        e.Message, e.StackTrace);
                }

                return new OpenSim.Region.CoreModules.Scripting.DynamicTexture.DynamicTexture(
                    data, extraParams, imageJ2000, new Size(width, height), reuseable);
            }
            finally
            {
                // XXX: In testing, it appears that if multiple threads dispose of separate GDI+ objects simultaneously,
                // the native malloc heap can become corrupted, possibly due to a double free().  This may be due to
                // bugs in the underlying libcairo used by mono's libgdiplus.dll on Linux/OSX.  These problems were
                // seen with both libcario 1.10.2-6.1ubuntu3 and 1.8.10-2ubuntu1.  They go away if disposal is perfomed
                // under lock.
                lock (this)
                {
                    if (graph != null)
                        graph.Dispose();
    
                    if (bitmap != null)
                        bitmap.Dispose();
                }
            }
        }
        
        private int parseIntParam(string strInt)
        {
            int parsed;
            try
            {
                parsed = Convert.ToInt32(strInt);
            }
            catch (Exception)
            {
                //Ckrinke: Add a WriteLine to remove the warning about 'e' defined but not used
                // m_log.Debug("Problem with Draw. Please verify parameters." + e.ToString());
                parsed = -1;
            }
            
            return parsed;
        }

/*
        private void CairoDraw(string data, System.Drawing.Graphics graph)
        {
            using (Win32Surface draw = new Win32Surface(graph.GetHdc()))
            {
                Context contex = new Context(draw);

                contex.Antialias = Antialias.None;    //fastest method but low quality
                contex.LineWidth = 7;
                char[] lineDelimiter = { ';' };
                char[] partsDelimiter = { ',' };
                string[] lines = data.Split(lineDelimiter);

                foreach (string line in lines)
                {
                    string nextLine = line.Trim();

                    if (nextLine.StartsWith("MoveTO"))
                    {
                        float x = 0;
                        float y = 0;
                        GetParams(partsDelimiter, ref nextLine, ref x, ref y);
                        contex.MoveTo(x, y);
                    }
                    else if (nextLine.StartsWith("LineTo"))
                    {
                        float x = 0;
                        float y = 0;
                        GetParams(partsDelimiter, ref nextLine, ref x, ref y);
                        contex.LineTo(x, y);
                        contex.Stroke();
                    }
                }
            }
            graph.ReleaseHdc();
        }
*/

        /// <summary>
        /// Split input data into discrete command lines.
        /// </summary>
        /// <returns></returns>
        /// <param name='data'></param>
        /// <param name='dataDelim'></param>
        private string[] GetLines(string data, char dataDelim)
        {
            char[] lineDelimiter = { dataDelim };
            return data.Split(lineDelimiter);
        }

        private void GDIDraw(string data, Graphics graph, char dataDelim, out bool reuseable)
        {
            reuseable = true;
            Point startPoint = new Point(0, 0);
            Point endPoint = new Point(0, 0);
            Pen drawPen = null;
            Font myFont = null;
            SolidBrush myBrush = null;

            try
            {
                drawPen = new Pen(Color.Black, 7);
                string fontName = m_fontName;
                float fontSize = 14;
                myFont = new Font(fontName, fontSize);
                myBrush = new SolidBrush(Color.Black);

                char[] partsDelimiter = {','};

                foreach (string line in GetLines(data, dataDelim))
                {
                    string nextLine = line.Trim();

//                    m_log.DebugFormat("[VECTOR RENDER MODULE]: Processing line '{0}'", nextLine);

                    //replace with switch, or even better, do some proper parsing
                    if (nextLine.StartsWith("MoveTo"))
                    {
                        float x = 0;
                        float y = 0;
                        GetParams(partsDelimiter, ref nextLine, 6, ref x, ref y);
                        startPoint.X = (int) x;
                        startPoint.Y = (int) y;
                    }
                    else if (nextLine.StartsWith("LineTo"))
                    {
                        float x = 0;
                        float y = 0;
                        GetParams(partsDelimiter, ref nextLine, 6, ref x, ref y);
                        endPoint.X = (int) x;
                        endPoint.Y = (int) y;
                        graph.DrawLine(drawPen, startPoint, endPoint);
                        startPoint.X = endPoint.X;
                        startPoint.Y = endPoint.Y;
                    }
                    else if (nextLine.StartsWith("Text"))
                    {
                        nextLine = nextLine.Remove(0, 4);
                        nextLine = nextLine.Trim();
                        graph.DrawString(nextLine, myFont, myBrush, startPoint);
                    }
                    else if (nextLine.StartsWith("Image"))
                    {
                        // We cannot reuse any generated texture involving fetching an image via HTTP since that image
                        // can change.
                        reuseable = false;

                        float x = 0;
                        float y = 0;
                        GetParams(partsDelimiter, ref nextLine, 5, ref x, ref y);
                        endPoint.X = (int) x;
                        endPoint.Y = (int) y;

                        using (Image image = ImageHttpRequest(nextLine))
                        {
                            if (image != null)
                            {
                                graph.DrawImage(image, (float)startPoint.X, (float)startPoint.Y, x, y);
                            }
                            else
                            {
                                using (Font errorFont = new Font(m_fontName,6))
                                {
                                    graph.DrawString("URL couldn't be resolved or is", errorFont,
                                                     myBrush, startPoint);
                                    graph.DrawString("not an image. Please check URL.", errorFont,
                                                     myBrush, new Point(startPoint.X, 12 + startPoint.Y));
                                }
    
                                graph.DrawRectangle(drawPen, startPoint.X, startPoint.Y, endPoint.X, endPoint.Y);
                            }
                        }

                        startPoint.X += endPoint.X;
                        startPoint.Y += endPoint.Y;
                    }
                    else if (nextLine.StartsWith("Rectangle"))
                    {
                        float x = 0;
                        float y = 0;
                        GetParams(partsDelimiter, ref nextLine, 9, ref x, ref y);
                        endPoint.X = (int) x;
                        endPoint.Y = (int) y;
                        graph.DrawRectangle(drawPen, startPoint.X, startPoint.Y, endPoint.X, endPoint.Y);
                        startPoint.X += endPoint.X;
                        startPoint.Y += endPoint.Y;
                    }
                    else if (nextLine.StartsWith("FillRectangle"))
                    {
                        float x = 0;
                        float y = 0;
                        GetParams(partsDelimiter, ref nextLine, 13, ref x, ref y);
                        endPoint.X = (int) x;
                        endPoint.Y = (int) y;
                        graph.FillRectangle(myBrush, startPoint.X, startPoint.Y, endPoint.X, endPoint.Y);
                        startPoint.X += endPoint.X;
                        startPoint.Y += endPoint.Y;
                    }
                    else if (nextLine.StartsWith("FillPolygon"))
                    {
                        PointF[] points = null;
                        GetParams(partsDelimiter, ref nextLine, 11, ref points);
                        graph.FillPolygon(myBrush, points);
                    }
                    else if (nextLine.StartsWith("Polygon"))
                    {
                        PointF[] points = null;
                        GetParams(partsDelimiter, ref nextLine, 7, ref points);
                        graph.DrawPolygon(drawPen, points);
                    }
                    else if (nextLine.StartsWith("Ellipse"))
                    {
                        float x = 0;
                        float y = 0;
                        GetParams(partsDelimiter, ref nextLine, 7, ref x, ref y);
                        endPoint.X = (int)x;
                        endPoint.Y = (int)y;
                        graph.DrawEllipse(drawPen, startPoint.X, startPoint.Y, endPoint.X, endPoint.Y);
                        startPoint.X += endPoint.X;
                        startPoint.Y += endPoint.Y;
                    }
                    else if (nextLine.StartsWith("FontSize"))
                    {
                        nextLine = nextLine.Remove(0, 8);
                        nextLine = nextLine.Trim();
                        fontSize = Convert.ToSingle(nextLine, CultureInfo.InvariantCulture);

                        myFont.Dispose();
                        myFont = new Font(fontName, fontSize);
                    }
                    else if (nextLine.StartsWith("FontProp"))
                    {
                        nextLine = nextLine.Remove(0, 8);
                        nextLine = nextLine.Trim();
    
                        string[] fprops = nextLine.Split(partsDelimiter);
                        foreach (string prop in fprops)
                        {
    
                            switch (prop)
                            {
                                case "B":
                                    if (!(myFont.Bold))
                                    {
                                        Font newFont = new Font(myFont, myFont.Style | FontStyle.Bold);
                                        myFont.Dispose();
                                        myFont = newFont;
                                    }
                                    break;
                                case "I":
                                    if (!(myFont.Italic))
                                    {
                                        Font newFont = new Font(myFont, myFont.Style | FontStyle.Italic);
                                        myFont.Dispose();
                                        myFont = newFont;
                                    }
                                    break;
                                case "U":
                                    if (!(myFont.Underline))
                                    {
                                        Font newFont = new Font(myFont, myFont.Style | FontStyle.Underline);
                                        myFont.Dispose();
                                        myFont = newFont;
                                    }
                                    break;
                                case "S":
                                    if (!(myFont.Strikeout))
                                    {
                                        Font newFont = new Font(myFont, myFont.Style | FontStyle.Strikeout);
                                        myFont.Dispose();
                                        myFont = newFont;
                                    }
                                    break;
                                case "R":
                                    // We need to place this newFont inside its own context so that the .NET compiler
                                    // doesn't complain about a redefinition of an existing newFont, even though there is none
                                    // The mono compiler doesn't produce this error.
                                    {
                                        Font newFont = new Font(myFont, FontStyle.Regular);
                                        myFont.Dispose();
                                        myFont = newFont;
                                    }
                                    break;
                            }
                        }
                    }
                    else if (nextLine.StartsWith("FontName"))
                    {
                        nextLine = nextLine.Remove(0, 8);
                        fontName = nextLine.Trim();
                        myFont.Dispose();
                        myFont = new Font(fontName, fontSize);
                    }
                    else if (nextLine.StartsWith("PenSize"))
                    {
                        nextLine = nextLine.Remove(0, 7);
                        nextLine = nextLine.Trim();
                        float size = Convert.ToSingle(nextLine, CultureInfo.InvariantCulture);
                        drawPen.Width = size;
                    }
                    else if (nextLine.StartsWith("PenCap"))
                    {
                        bool start = true, end = true;
                        nextLine = nextLine.Remove(0, 6);
                        nextLine = nextLine.Trim();
                        string[] cap = nextLine.Split(partsDelimiter);
                        if (cap[0].ToLower() == "start")
                            end = false;
                        else if (cap[0].ToLower() == "end")
                            start = false;
                        else if (cap[0].ToLower() != "both")
                            return;
                        string type = cap[1].ToLower();
                        
                        if (end)
                        {
                            switch (type)
                            {
                                case "arrow":
                                    drawPen.EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor;
                                    break;
                                case "round":
                                    drawPen.EndCap = System.Drawing.Drawing2D.LineCap.RoundAnchor;
                                    break;
                                case "diamond":
                                    drawPen.EndCap = System.Drawing.Drawing2D.LineCap.DiamondAnchor;
                                    break;
                                case "flat":
                                    drawPen.EndCap = System.Drawing.Drawing2D.LineCap.Flat;
                                    break;
                            }
                        }
                        if (start)
                        {
                            switch (type)
                            {
                                case "arrow":
                                    drawPen.StartCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor;
                                    break;
                                case "round":
                                    drawPen.StartCap = System.Drawing.Drawing2D.LineCap.RoundAnchor;
                                    break;
                                case "diamond":
                                    drawPen.StartCap = System.Drawing.Drawing2D.LineCap.DiamondAnchor;
                                    break;
                                case "flat":
                                    drawPen.StartCap = System.Drawing.Drawing2D.LineCap.Flat;
                                    break;
                            }
                        }
                    }
                    else if (nextLine.StartsWith("PenColour") || nextLine.StartsWith("PenColor"))
                    {
                        nextLine = nextLine.Remove(0, 9);
                        nextLine = nextLine.Trim();
                        int hex = 0;
    
                        Color newColor;
                        if (Int32.TryParse(nextLine, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hex))
                        {
                            newColor = Color.FromArgb(hex);
                        }
                        else
                        {
                            // this doesn't fail, it just returns black if nothing is found
                            newColor = Color.FromName(nextLine);
                        }

                        myBrush.Color = newColor;
                        drawPen.Color = newColor;
                    }
                }
            }
            finally
            {
                if (drawPen != null)
                    drawPen.Dispose();

                if (myFont != null)
                    myFont.Dispose();

                if (myBrush != null)
                    myBrush.Dispose();
            }
        }

        private static void GetParams(char[] partsDelimiter, ref string line, int startLength, ref float x, ref float y)
        {
            line = line.Remove(0, startLength);
            string[] parts = line.Split(partsDelimiter);
            if (parts.Length == 2)
            {
                string xVal = parts[0].Trim();
                string yVal = parts[1].Trim();
                x = Convert.ToSingle(xVal, CultureInfo.InvariantCulture);
                y = Convert.ToSingle(yVal, CultureInfo.InvariantCulture);
            }
            else if (parts.Length > 2)
            {
                string xVal = parts[0].Trim();
                string yVal = parts[1].Trim();
                x = Convert.ToSingle(xVal, CultureInfo.InvariantCulture);
                y = Convert.ToSingle(yVal, CultureInfo.InvariantCulture);

                line = "";
                for (int i = 2; i < parts.Length; i++)
                {
                    line = line + parts[i].Trim();
                    line = line + " ";
                }
            }
        }

        private static void GetParams(char[] partsDelimiter, ref string line, int startLength, ref PointF[] points)
        {
            line = line.Remove(0, startLength);
            string[] parts = line.Split(partsDelimiter);
            if (parts.Length > 1 && parts.Length % 2 == 0)
            {
                points = new PointF[parts.Length / 2];
                for (int i = 0; i < parts.Length; i = i + 2)
                {
                    string xVal = parts[i].Trim();
                    string yVal = parts[i+1].Trim();
                    float x = Convert.ToSingle(xVal, CultureInfo.InvariantCulture);
                    float y = Convert.ToSingle(yVal, CultureInfo.InvariantCulture);
                    PointF point = new PointF(x, y);
                    points[i / 2] = point;

//                    m_log.DebugFormat("[VECTOR RENDER MODULE]: Got point {0}", points[i / 2]);
                }
            }
        }

        private Bitmap ImageHttpRequest(string url)
        {
            try
            {
                WebRequest request = HttpWebRequest.Create(url);

                using (HttpWebResponse response = (HttpWebResponse)(request).GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        using (Stream s = response.GetResponseStream())
                        {
                            Bitmap image = new Bitmap(s);
                            return image;
                        }
                    }
                }
            }
            catch { }

            return null;
        }
    }
}
