using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Environment.Scenes;
using libsecondlife;
using OpenSim.Framework.Types;
using System.Timers;
using System.Diagnostics;
using System.IO;
using Primitive=OpenSim.Region.Environment.Scenes.Primitive;

namespace SimpleApp
{
    public class FileSystemObject : SceneObject
    {
        public FileSystemObject(Scene world, FileInfo fileInfo, LLVector3 pos)
            : base( world, world.EventManager, LLUUID.Zero, world.NextLocalId, pos, BoxShape.Default )
        {
            
            
            float size = (float)Math.Pow((double)fileInfo.Length, (double) 1 / 3) / 5;
            rootPrimitive.ResizeGoup(new LLVector3(size, size, size));
            rootPrimitive.Text = fileInfo.Name;
        }

        public override void Update()
        {
            base.Update();
        }
    }
}
