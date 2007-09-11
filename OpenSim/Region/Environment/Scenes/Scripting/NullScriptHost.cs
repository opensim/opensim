using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Region.Environment.Scenes.Scripting
{
    public class NullScriptHost : IScriptHost
    {

        LLVector3 m_pos = new LLVector3( 128, 128, 30 );
        public string Name
        {
            get { return "Object"; }
            set { }
        }

        public string SitName
        {
            get { return ""; }
            set { }
        }

        public string TouchName
        {
            get { return ""; }
            set { }
        }

        public string Description
        {
            get { return ""; }
            set { }
        }

        public LLUUID UUID
        {
            get { return LLUUID.Zero; }
        }

        public LLUUID ObjectOwner
        { get { return LLUUID.Zero; } }

        public LLUUID ObjectCreator { get { return LLUUID.Zero; } }

        public LLVector3 AbsolutePosition
        {
            get { return m_pos; }
        }

        public void SetText(string text, Axiom.Math.Vector3 color, double alpha)
        {
            Console.WriteLine("Tried to SetText [{0}] on NullScriptHost", text);
        }
    }
}
