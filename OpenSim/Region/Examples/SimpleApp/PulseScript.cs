using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Scripting;
using OpenSim.Region.Environment.Scenes;
namespace SimpleApp
{
    public class PulseScript : IScript
    {
        ScriptInfo script;

        private libsecondlife.LLVector3 pulse = new libsecondlife.LLVector3(0.1f, 0.1f, 0.1f);
        public string getName()
        {
            return "pulseScript 0.1";
        }

        public void Initialise(ScriptInfo scriptInfo)
        {
            script = scriptInfo;
            script.events.OnFrame += new EventManager.OnFrameDelegate(events_OnFrame);
            script.events.OnNewPresence += new EventManager.OnNewPresenceDelegate(events_OnNewPresence);
        }

        void events_OnNewPresence(ScenePresence presence)
        {
            script.logger.Verbose("Hello " + presence.firstname.ToString() + "!");
        }

        void events_OnFrame()
        {
            foreach (EntityBase ent in this.script.world.Entities.Values)
            {
                if (ent is SceneObject)
                {
                    SceneObject prim = (SceneObject)ent;
                    if ((prim.rootPrimitive.Scale.X > 1) && (prim.rootPrimitive.Scale.Y > 1) && (prim.rootPrimitive.Scale.Z > 1))
                    {
                        this.pulse = new libsecondlife.LLVector3(-0.1f, -0.1f, -0.1f);
                    }
                    else if ((prim.rootPrimitive.Scale.X < 0.2f) && (prim.rootPrimitive.Scale.Y < 0.2f) && (prim.rootPrimitive.Scale.Z < 0.2f))
                    {
                        pulse = new libsecondlife.LLVector3(0.1f, 0.1f, 0.1f);
                    }

                    prim.rootPrimitive.ResizeGoup(prim.rootPrimitive.Scale + pulse);
                }
            }
        }

    }
}
