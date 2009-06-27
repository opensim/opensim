using System;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface ISceneViewer
    {
        void Reset();
        void Close();
        int MaxPrimsPerFrame { get; set; }
        void QueuePartForUpdate(SceneObjectPart part);
        void SendPrimUpdates();
    }
}
