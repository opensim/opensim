using System;

namespace OpenSim.Framework
{
    #region Args Classes
    public class ICA2_ConnectionArgs : EventArgs
    {
        
    }

    public class ICA2_DisconnectionArgs : EventArgs
    {
        public bool Forced;

        // Static Constructor 
        // Allows us to recycle these classes later more easily from a pool.
        public static ICA2_DisconnectionArgs Create(bool forced)
        {
            ICA2_DisconnectionArgs tmp = new ICA2_DisconnectionArgs();
            tmp.Forced = forced;

            return tmp;
        }
    }

    public class ICA2_PingArgs : EventArgs
    {
    }

    public class ICA2_AvatarAppearanceArgs : EventArgs
    {
    }

    public class ICA2_TerraformArgs : EventArgs
    {
        public double XMin;
        public double XMax;
        public double YMin;
        public double YMax;
        public Guid Action;
        public double Strength; // 0 .. 1
        public double Radius;
    }
    #endregion

    public delegate void ICA2_OnTerraformDelegate(IClientAPI2 sender, ICA2_TerraformArgs e);

    public interface IClientAPI2
    {
        // Connect / Disconnect
        void Connect(ICA2_ConnectionArgs e);
        void Disconnect(ICA2_DisconnectionArgs e);
        void Ping(ICA2_PingArgs e);

        void SendAvatarAppearance(ICA2_AvatarAppearanceArgs e);

        event ICA2_OnTerraformDelegate OnTerraform;
    }
}
