using OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.Bepu
{
    /// <summary>
    /// Conversion helpers between OpenMetaverse types (used by OpenSim)
    /// and System.Numerics types (used by BepuPhysics v2).
    /// </summary>
    public static class BepuUtil
    {
        #region OpenMetaverse → System.Numerics

        public static System.Numerics.Vector3 ToSN(Vector3 v)
            => new(v.X, v.Y, v.Z);

        public static System.Numerics.Quaternion ToSN(Quaternion q)
            => new(q.X, q.Y, q.Z, q.W);

        public static System.Numerics.Vector3 ToSN(Vector3d v)
            => new((float)v.X, (float)v.Y, (float)v.Z);

        #endregion

        #region System.Numerics → OpenMetaverse

        public static Vector3 ToOM(System.Numerics.Vector3 v)
            => new(v.X, v.Y, v.Z);

        public static Quaternion ToOM(System.Numerics.Quaternion q)
            => new(q.X, q.Y, q.Z, q.W);

        public static Vector3d ToOMd(System.Numerics.Vector3 v)
            => new(v.X, v.Y, v.Z);

        #endregion

        #region Bepu-specific helpers

        /// <summary>
        /// Convert an OM Vector3 to a Bepu RigidPose at the given rotation.
        /// </summary>
        public static BepuPhysics.RigidPose ToRigidPose(Vector3 position, Quaternion rotation)
            => new(ToSN(position), ToSN(rotation));

        /// <summary>
        /// Convert an OM Vector3 to a Bepu RigidPose with identity rotation.
        /// </summary>
        public static BepuPhysics.RigidPose ToRigidPose(Vector3 position)
            => new(ToSN(position), System.Numerics.Quaternion.Identity);

        /// <summary>
        /// Extract OM position from a Bepu RigidPose.
        /// </summary>
        public static Vector3 PositionFromPose(in BepuPhysics.RigidPose pose)
            => ToOM(pose.Position);

        /// <summary>
        /// Extract OM rotation from a Bepu RigidPose.
        /// </summary>
        public static Quaternion RotationFromPose(in BepuPhysics.RigidPose pose)
            => ToOM(pose.Orientation);

        #endregion
    }
}
