namespace OpenSim.Framework
{
    public enum ThrottleOutPacketType : int
    {
        Resend = 0,
        Land = 1,
        Wind = 2,
        Cloud = 3,
        Task = 4,
        Texture = 5,
        Asset = 6,
        Unknown = 7, // Also doubles as 'do not throttle'
        Back = 8,
        LowpriorityTask = 9
    }
}
