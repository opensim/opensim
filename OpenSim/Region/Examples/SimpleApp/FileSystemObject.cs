using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Environment.Scenes;
using libsecondlife;
using OpenSim.Framework.Types;
using System.Timers;
using System.Diagnostics;
using System.IO;

namespace SimpleApp
{
    public class FileSystemObject : SceneObjectGroup
    {
        public FileSystemObject(Scene world, FileInfo fileInfo, LLVector3 pos)
            : base(world, world.RegionInfo.RegionHandle, LLUUID.Zero, world.NextLocalId, pos, BoxShape.Default)
        {


            float size = (float)Math.Pow((double)fileInfo.Length, (double)1 / 3) / 5;
            // rootPrimitive.ResizeGoup(new LLVector3(size, size, size));
            Text = fileInfo.Name;
            ScheduleGroupForFullUpdate();
        }

        public override void Update()
        {
            base.Update();
        }
    }
}
