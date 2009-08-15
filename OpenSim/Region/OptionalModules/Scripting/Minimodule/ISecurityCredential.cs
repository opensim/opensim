namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public interface ISecurityCredential
    {
        ISocialEntity owner { get; }
    }
}