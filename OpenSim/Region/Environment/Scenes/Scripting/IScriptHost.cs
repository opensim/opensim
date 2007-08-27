using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Region.Environment.Scenes.Scripting
{
    public interface IScriptHost
    {
        string Name { get; set;}
        LLUUID UUID { get; }
        LLVector3 AbsolutePosition { get; }
        void SetText(string text, Axiom.Math.Vector3 color, double alpha);
    }
}
