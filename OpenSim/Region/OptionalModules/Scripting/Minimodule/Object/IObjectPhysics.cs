using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule.Object
{
    interface IObjectPhysics
    {
        bool Enabled { get; set; }
        bool Phantom { get; set; }
        bool PhantomCollisions { get; set; }

        double Density { get; set; }
        double Mass { get; set; }

        double Buoyancy { get; set; }

        Vector3 GeometricCenter { get; }
        Vector3 CenterOfMass { get; }
        Vector3 Velocity { get; set; }
        Vector3 Torque { get; set; }

        Vector3 Acceleration { get; }
        Quaternion Orientation { get; set; }
        Vector3 RotationalVelocity { get; set; }

        void AddForce(Vector3 force, bool pushforce);
        void AddAngularForce(Vector3 force, bool pushforce);
        void SetMomentum(Vector3 momentum);
    }
}
