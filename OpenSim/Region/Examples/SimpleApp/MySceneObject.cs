using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Environment.Scenes;
using libsecondlife;
using OpenSim.Framework.Types;
using System.Timers;
using System.Diagnostics;

namespace SimpleApp
{
    public class MySceneObject : SceneObject
    {
        private PerformanceCounter m_counter;
        
        public MySceneObject(Scene world, EventManager eventManager, LLUUID ownerID, uint localID, LLVector3 pos, PrimitiveBaseShape shape)
            : base(world, eventManager, ownerID, localID, pos, shape )
        {
            String objectName = "Processor";
            String counterName = "% Processor Time";
            String instanceName = "_Total";

            m_counter = new PerformanceCounter(objectName, counterName, instanceName);

            Timer timer = new Timer();
            timer.Enabled = true;
            timer.Interval = 100;
            timer.Elapsed += new ElapsedEventHandler(this.Heartbeat);

        }

        public void Heartbeat(object sender, EventArgs e)
        {
            float cpu = m_counter.NextValue() / 40f;
            LLVector3 size = new LLVector3(cpu, cpu, cpu);            
            rootPrimitive.ResizeGoup( size );
            update();
        }
    }
}
