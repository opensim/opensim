using Axiom.Math;
using libsecondlife;

namespace OpenSim.Region.Environment.Scenes.Scripting
{
    public interface IScriptHost
    {
        string Name { get; set; }
        string SitName { get; set; }
        string TouchName { get; set; }
        string Description { get; set; }
        LLUUID UUID { get; }
        LLUUID ObjectOwner { get; }
        LLUUID ObjectCreator { get; }
        LLVector3 AbsolutePosition { get; }
        void SetText(string text, Vector3 color, double alpha);
    }
}