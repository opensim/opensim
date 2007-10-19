using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using System.Drawing;
using libsecondlife;
using OpenJPEGNet;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Interfaces;
using Nini.Config;

namespace OpenSim.Region.Environment.Modules
{
    public class LoadImageURLModule : IRegionModule , IDynamicTextureRender
    {
        private string m_name = "LoadImageURL";
        private IDynamicTextureManager m_textureManager;
        private Scene m_scene;

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
            m_textureManager.RegisterRender(GetContentType(), this);
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

        public string GetName()
        {
            return m_name;
        }

        public string GetContentType()
        {
            return ("image");
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
            MakeHttpRequest(url, id);
            return true;
        }

        public bool AsyncConvertData(LLUUID id, string bodyData, string extraParams)
        {
            return false;
        }

        private void MakeHttpRequest(string url, LLUUID requestID)
        {
            WebRequest request = HttpWebRequest.Create(url);
            RequestState state = new RequestState((HttpWebRequest)request, requestID);
            IAsyncResult result = request.BeginGetResponse(new AsyncCallback(HttpRequestReturn), state);

            TimeSpan t = (DateTime.UtcNow - new DateTime(1970, 1, 1));
            state.TimeOfRequest = (int)t.TotalSeconds;
        }

        private void HttpRequestReturn(IAsyncResult result)
        {
            RequestState state = (RequestState)result.AsyncState;
            WebRequest request = (WebRequest)state.Request;
            HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(result);
            if (response.StatusCode == HttpStatusCode.OK)
            {
               Bitmap image = new Bitmap(response.GetResponseStream());
               Bitmap resize = new Bitmap(image, new Size(512, 512));
               byte[] imageJ2000 = OpenJPEG.EncodeFromImage(resize, true);

               m_textureManager.ReturnData(state.RequestID, imageJ2000);
            }
        }

        public class RequestState
        {
            public HttpWebRequest Request = null;
            public LLUUID RequestID = LLUUID.Zero;
            public int TimeOfRequest = 0;

            public RequestState(HttpWebRequest request, LLUUID requestID)
            {
                Request = request;
                RequestID = requestID;
            }
        }

    }
}
