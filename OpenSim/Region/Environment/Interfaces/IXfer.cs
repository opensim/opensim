namespace OpenSim.Region.Environment.Interfaces
{
    public interface IXfer
    {
        bool AddNewFile(string fileName, byte[] data);
    }
}