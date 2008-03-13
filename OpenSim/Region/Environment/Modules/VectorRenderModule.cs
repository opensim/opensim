
using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Globalization;
using libsecondlife;
using Nini.Config;
using OpenJPEGNet;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
//using Cairo;

namespace OpenSim.Region.Environment.Modules
{
    public class VectorRenderModule : IRegionModule, IDynamicTextureRender
    {
        private Scene m_scene;
        private string m_name = "VectorRenderModule";
        private IDynamicTextureManager m_textureManager;

        public VectorRenderModule()
        {
        }

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (m_scene == null)
            {
                m_scene = scene;
            }
        }

        public void PostInitialise()
        {
            m_textureManager = m_scene.RequestModuleInterface<IDynamicTextureManager>();
            if (m_textureManager != null)
            {
                m_textureManager.RegisterRender(GetContentType(), this);
            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return m_name; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        private void Draw(string data, LLUUID id, string extraParams)
        {
            // TODO: this is a brutal hack.  extraParams should actually be parsed reasonably.
            int size = 256;
            try {
                size = Convert.ToInt32(extraParams);
            } catch (Exception e) {

            }
            
            if ((size < 128) || (size > 1024))
                size = 256;
            
            Bitmap bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            System.Drawing.Graphics graph = System.Drawing.Graphics.FromImage(bitmap);

            extraParams = extraParams.ToLower();
            int alpha = 255;
            if (extraParams == "setalpha")
            {
                alpha = 0;
            }
            else
            {
                graph.FillRectangle(new SolidBrush(Color.White), 0, 0, size, size);
            }

            for (int w = 0; w < bitmap.Width; w++)
            {
                for (int h = 0; h < bitmap.Height; h++)
                {
                    bitmap.SetPixel(w, h, Color.FromArgb(alpha, bitmap.GetPixel(w, h)));
                }
            }

           

            GDIDraw(data, graph);

            byte[] imageJ2000 = OpenJPEG.EncodeFromImage(bitmap, true);
            m_textureManager.ReturnData(id, imageJ2000);

        }

        /* private void CairoDraw(string data, System.Drawing.Graphics graph)
         {
             using (Win32Surface draw = new Win32Surface(graph.GetHdc()))
             {
                 Context contex = new Context(draw);

                 contex.Antialias = Antialias.None;	//fastest method but low quality
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
         }*/

        private void GDIDraw(string data, System.Drawing.Graphics graph)
        {
            System.Drawing.Point startPoint = new System.Drawing.Point(0, 0);
            System.Drawing.Point endPoint = new System.Drawing.Point(0, 0);
            System.Drawing.Pen drawPen = new Pen(System.Drawing.Color.Black, 7);
            Font myFont = new Font("Times New Roman", 14);
            SolidBrush myBrush = new SolidBrush(Color.Black);
            char[] lineDelimiter = { ';' };
            char[] partsDelimiter = { ',' };
            string[] lines = data.Split(lineDelimiter);

             
            foreach (string line in lines)
            {
                string nextLine = line.Trim();
                //replace with switch, or even better, do some proper parsing 
                if (nextLine.StartsWith("MoveTo"))
                {
                    float x = 0;
                    float y = 0;
                    GetParams(partsDelimiter, ref nextLine, 6, ref x, ref y);
                    startPoint.X = (int)x;
                    startPoint.Y = (int)y;
                }
                else if (nextLine.StartsWith("LineTo"))
                {
                    float x = 0;
                    float y = 0;
                    GetParams(partsDelimiter, ref nextLine, 6, ref x, ref y);
                    endPoint.X = (int)x;
                    endPoint.Y = (int)y;
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
                    float x = 0;
                    float y = 0;
                    GetParams(partsDelimiter, ref nextLine, 5, ref x, ref y);
                    endPoint.X = (int)x;
                    endPoint.Y = (int)y;
                    System.Drawing.Image image = ImageHttpRequest(nextLine);
                    graph.DrawImage(image, (float)startPoint.X, (float)startPoint.Y, x, y);
                    startPoint.X += endPoint.X;
                    startPoint.Y += endPoint.Y;
                }
                else if (nextLine.StartsWith("Rectangle"))
                {
                    float x = 0;
                    float y = 0;
                    GetParams(partsDelimiter, ref nextLine, 9, ref x, ref y);
                    endPoint.X = (int)x;
                    endPoint.Y = (int)y;
                    graph.DrawRectangle(drawPen, startPoint.X, startPoint.Y, endPoint.X, endPoint.Y);
                    startPoint.X += endPoint.X;
                    startPoint.Y += endPoint.Y;
                }
                else if (nextLine.StartsWith("FillRectangle"))
                {
                    float x = 0;
                    float y = 0;
                    GetParams(partsDelimiter, ref nextLine, 13, ref x, ref y);
                    endPoint.X = (int)x;
                    endPoint.Y = (int)y;
                    graph.FillRectangle(myBrush, startPoint.X, startPoint.Y, endPoint.X, endPoint.Y);
                    startPoint.X += endPoint.X;
                    startPoint.Y += endPoint.Y;
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
                    float size = Convert.ToSingle(nextLine, CultureInfo.InvariantCulture);
                    myFont = new Font("Times New Roman", size);
                }
                else if (nextLine.StartsWith("PenSize"))
                {
                    nextLine = nextLine.Remove(0, 8);
                    nextLine = nextLine.Trim();
                    float size = Convert.ToSingle(nextLine, CultureInfo.InvariantCulture);
                    drawPen.Width = size;
                }
                else if (nextLine.StartsWith("PenColour"))
                {
                    nextLine = nextLine.Remove(0, 9);
                    nextLine = nextLine.Trim();
                    
                    // Linux Compile error if Color is compared against null
                    // Documentation for System.Drawing.Color says it returns 
                    // a Color with all Components as 0 if the String passed 
                    // isn't a known color
                    Color newColour = Color.FromName(nextLine);
                    //if (newColour != null)
                    //{
                        myBrush.Color = newColour;
                        drawPen.Color = newColour;
                    //}
                }
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

        private Bitmap ImageHttpRequest(string url)
        {
            WebRequest request = HttpWebRequest.Create(url);
            Stream str = null;
            HttpWebResponse response = (HttpWebResponse)(request).GetResponse();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Bitmap image = new Bitmap(response.GetResponseStream());
                return image;
            }

            return null;
        }

        public string GetContentType()
        {
            return ("vector");
        }

        public string GetName()
        {
            return m_name;
        }

        public bool SupportsAsynchronous()
        {
            return true;
        }

        public byte[] ConvertUrl(string url, string extraParams)
        {
            return null;
        }

        public byte[] ConvertStream(Stream data, string extraParams)
        {
            return null;
        }

        public bool AsyncConvertUrl(LLUUID id, string url, string extraParams)
        {
            return false;
        }

        public bool AsyncConvertData(LLUUID id, string bodyData, string extraParams)
        {
            Draw(bodyData, id, extraParams);
            return true;
        }
    }
}