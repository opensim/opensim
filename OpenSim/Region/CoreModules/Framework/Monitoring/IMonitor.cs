namespace OpenSim.Region.CoreModules.Framework.Monitoring
{
    interface IMonitor
    {
        double GetValue();
        string GetName();
        string GetFriendlyValue(); // Convert to readable numbers
    }
}
