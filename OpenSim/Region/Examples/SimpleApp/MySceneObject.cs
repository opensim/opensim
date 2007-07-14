using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Environment.Scenes;
using libsecondlife;
using OpenSim.Framework.Types;
using System.Timers;

namespace SimpleApp
{
    public class MySceneObject : SceneObject
    {
        LLVector3 delta = new LLVector3(0.1f, 0.1f, 0.1f);
        
        public MySceneObject(Scene world, LLUUID ownerID, uint localID, LLVector3 pos, PrimitiveBaseShape shape)
            : base(world, ownerID, localID, pos, shape )
        {
            Timer timer = new Timer();
            timer.Enabled = true;
            timer.Interval = 100;
            timer.Elapsed += new ElapsedEventHandler(this.Heartbeat);
        }

        public void Heartbeat(object sender, EventArgs e)
        {
            if (rootPrimitive.Scale.X > 1)
            {
                delta = new LLVector3(-0.1f, -0.1f, -0.1f);
            }

            if (rootPrimitive.Scale.X < 0.2f)
            {
                delta = new LLVector3(0.1f, 0.1f, 0.1f);
            }

            rootPrimitive.ResizeGoup(rootPrimitive.Scale + delta);
            update();
        }
    }
}
