namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public interface ISecurityCredential
    {
        ISocialEntity owner { get; }
        bool CanEditObject(IObject target);
        bool CanEditTerrain(int x, int y);
    }
}