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
    public class CpuCounterObject : SceneObject
    {
        private PerformanceCounter m_counter;
        
        public CpuCounterObject(Scene world, EventManager eventManager, LLUUID ownerID, uint localID, LLVector3 pos, PrimitiveBaseShape shape)
            : base(world, eventManager, ownerID, localID, pos, shape )
        {
            String objectName = "Processor";
            String counterName = "% Processor Time";
            String instanceName = "_Total";

            m_counter = new PerformanceCounter(objectName, counterName, instanceName);
        }

        public override void Update( )
        {
            float cpu = m_counter.NextValue() / 40f;
            LLVector3 size = new LLVector3(cpu, cpu, cpu);            
            rootPrimitive.ResizeGoup( size );
            
            base.Update();
        }
    }
}
