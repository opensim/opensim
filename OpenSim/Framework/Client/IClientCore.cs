namespace OpenSim.Framework.Client
{
    public interface IClientCore
    {
        bool TryGet<T>(out T iface);
        T Get<T>();
    }
}