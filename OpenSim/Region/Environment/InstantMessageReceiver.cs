using System;

namespace OpenSim.Region.Environment
{
    /// <summary>
    /// Bit Vector for Which Modules to send an instant message to from the Scene or an Associated Module
    /// </summary>
    
    // This prevents the Modules from sending Instant messages to other modules through the scene
    // and then receiving the same messages

    // This is mostly here because on LLSL and the SecondLife Client, IMs,Groups and friends are linked 
    // inseparably

    [Flags]
    public enum InstantMessageReceiver : uint
    {
        /// <summary>None of them..   here for posterity and amusement</summary>
        None = 0,
        /// <summary>The IM Module</summary>
        IMModule = 0x00000001,
        /// <summary>The Friends Module</summary>
        FriendsModule = 0x00000002,
        /// <summary>The Groups Module</summary>
        GroupsModule = 0x00000004

    }
}
