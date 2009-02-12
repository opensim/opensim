using OpenMetaverse;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface ISceneObject
    {
        UUID UUID { get; }
        ISceneObject CloneForNewScene();
        string ToXmlString2();
        string ExtraToXmlString();
        void ExtraFromXmlString(string xmlstr);
        string GetStateSnapshot();
        void SetState(string xmlstr, UUID regionID);
    }
}
