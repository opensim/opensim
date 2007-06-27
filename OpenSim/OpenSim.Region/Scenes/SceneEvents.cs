using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.Scenes
{
    /// <summary>
    /// A class for triggering remote scene events.
    /// </summary>
    class SceneEvents
    {
        public delegate void OnFrameDelegate();
        public event OnFrameDelegate OnFrame;

        public delegate void OnNewViewerDelegate();
        public event OnNewViewerDelegate OnNewViewer;

        public delegate void OnNewPrimitiveDelegate();
        public event OnNewPrimitiveDelegate OnNewPrimitive;

        public void TriggerOnFrame()
        {
            if (OnFrame != null)
            {
                OnFrame();
            }
        }
    }
}
