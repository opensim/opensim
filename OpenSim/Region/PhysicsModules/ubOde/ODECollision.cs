using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenMetaverse;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using OpenSim.Region.PhysicsModules.SharedBase;

namespace OpenSim.Region.PhysicsModule.ubOde
{
    public partial class ODEScene
    {
        internal bool CollideSpheres(Vector3 p1, float r1, Vector3 p2, float r2,
                ref SafeNativeMethods.ContactGeom c)
        {
            float rsum = r1 + r2;
            Vector3 delta = p1 - p2;
            float d = delta.LengthSquared();
            if (d >= rsum * rsum)
                return false;
            if (d < 1e-6f)
            {
                c.pos = Unsafe.As<Vector3, SafeNativeMethods.Vector3>(ref p1);
                c.normal.X = 1;
                c.normal.Y = 0;
                c.normal.Z = 0;
                c.depth = rsum;
                return true;
            }

            d = MathF.Sqrt(d);
            c.depth = rsum - d;
            float k = r1 - 0.5f * c.depth;

            delta *= 1.0f / d;
            c.normal = Unsafe.As<Vector3, SafeNativeMethods.Vector3>(ref delta);

            delta *= k;
            delta = p1 - delta;
            c.pos = Unsafe.As<Vector3, SafeNativeMethods.Vector3>(ref delta);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool CollideVerticalChars(OdeCharacter p1, OdeCharacter p2, ref ContactPoint cp, ref int feet)
        {
            Vector3 tt;
            tt.Z = p1._position.Z - p2._position.Z;
            float absdz = Math.Abs(tt.Z);

            float r = p1.CapsuleSizeZ + p2.CapsuleSizeZ;
            if (absdz >= r)
                return false;

            tt.X = p1._position.X - p2._position.X;
            tt.Y = p1._position.Y - p2._position.Y;

            Vector3 v;
            float r1, r2, rhsum, d;

            float rsum = p1.CapsuleRadius + p2.CapsuleRadius;
            r -= rsum;
            if (absdz < r)
            {
                cp.Position = p1._position;
                cp.Position.Z -= 0.5f * tt.Z;
                cp.SurfaceNormal.Z = 0;

                float hd = tt.X * tt.X + tt.Y * tt.Y;
                if (hd < 1e-6f)
                {
                    cp.SurfaceNormal.X = 1;
                    cp.SurfaceNormal.Y = 0;
                    cp.PenetrationDepth = rsum;
                    return true;
                }

                hd = MathF.Sqrt(hd);
                d = hd;

                hd = 1.0f / hd;
                tt.X *= hd;
                tt.Y *= hd;

                v = Vector3.InverseRotateByShortQZ(tt, p1.Orientation2D);
                r1 = 1.0f / MathF.Sqrt(v.X * v.X / p1.AvaSizeXsq + v.Y * v.Y / p1.AvaSizeYsq);
                v = Vector3.InverseRotateByShortQZ(tt, p2.Orientation2D);
                r2 = 1.0f / MathF.Sqrt(v.X * v.X / p2.AvaSizeXsq + v.Y * v.Y / p2.AvaSizeYsq);

                rhsum = r1 + r2 - d;
                if (rhsum < 0.5e-3f)
                    return false;

                cp.SurfaceNormal.X = tt.X;
                cp.SurfaceNormal.Y = tt.Y;

                float hk = r1 - 0.5f * rhsum;
                if (hk <= 0)
                    cp.PenetrationDepth = r1;
                else
                {
                    cp.PenetrationDepth = rhsum;
                    cp.Position.X -= hk * tt.X;
                    cp.Position.Y -= hk * tt.Y;
                }
                return true;
            }

            if (tt.Z > 0)
            {
                tt.Z -= r;
                feet = 1;
            }
            else
            {
                tt.Z += r;
                feet = -1;
            }

            d = tt.LengthSquared();
            if (d < 1e-6f)
            {
                cp.Position = p1._position;
                cp.SurfaceNormal.X = 0;
                cp.SurfaceNormal.Y = 0;
                cp.SurfaceNormal.Z = tt.Z > 0 ? 1.0f : -1.0f;
                cp.PenetrationDepth = rsum;
                return true;
            }

            d = MathF.Sqrt(d);
            float k = rsum - d;
            if (k < 0.5e-3f)
                return false;

            tt *= 1.0f / d;

            v = Vector3.InverseRotateByShortQZ(tt, p1.Orientation2D);
            r1 = v.X * v.X / p1.AvaSizeXsq + v.Y * v.Y / p1.AvaSizeYsq;
            v = Vector3.InverseRotateByShortQZ(tt, p2.Orientation2D);
            r2 = v.X * v.X / p2.AvaSizeXsq + v.Y * v.Y / p2.AvaSizeYsq;

            if (d > 1.0f / MathF.Sqrt(r1) + 1.0f / MathF.Sqrt(r2))
                return false;

            cp.SurfaceNormal = tt;
            cp.PenetrationDepth = k;

            k = p1.CapsuleRadius - 0.5f * k; 
            tt *= k;
            cp.Position = p1._position - tt;
            return true;
        }

        internal ContactPoint SharedChrContact = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CollideCharChar(OdeCharacter p1, OdeCharacter p2)
        {
            if (ContactJointCount >= maxContactJoints)
                return;
            int feetcollision = 0;
            if (!CollideVerticalChars(p1, p2, ref SharedChrContact, ref feetcollision))
                return;

            p1.CollidingObj = true;
            p2.CollidingObj = true;

            if (feetcollision > 0)
            {
                p1.CollideNormal = SharedChrContact.SurfaceNormal;
                p1.IsColliding = true;
            }
            else if (feetcollision < 0)
            {
                p2.CollideNormal = -SharedChrContact.SurfaceNormal;
                p2.IsColliding = true;
                SharedChrContact.CharacterFeet = true;
            }

            contactSharedForJoints.surface.mu = 0;
            contactSharedForJoints.surface.bounce = 0;
            contactSharedForJoints.geom.pos = Unsafe.As<Vector3, SafeNativeMethods.Vector3>(ref SharedChrContact.Position);
            contactSharedForJoints.geom.normal = Unsafe.As<Vector3, SafeNativeMethods.Vector3>(ref SharedChrContact.SurfaceNormal);
            contactSharedForJoints.geom.depth = SharedChrContact.PenetrationDepth;
            IntPtr Joint = CreateCharContacJoint();
            if (Joint == IntPtr.Zero)
                return;
            SafeNativeMethods.JointAttach(Joint, p1.Body, p2.Body);

            if (p1.CollisionScore < float.MaxValue)
                p1.CollisionScore += 1.0f;
            if (p2.CollisionScore < float.MaxValue)
                p2.CollisionScore += 1.0f;

            bool p1events = p1.SubscribedEvents();
            bool p2events = p2.SubscribedEvents();
            if (!p2events && !p1events)
                return;

            Vector3 vel = Vector3.Zero;
            if (p2.IsPhysical)
                vel = p2.rootVelocity;
            if (p1.IsPhysical)
                vel -= p1.rootVelocity;

            SharedChrContact.RelativeSpeed = Vector3.Dot(vel, SharedChrContact.SurfaceNormal);
            if (p2events)
            {
                p2.AddCollisionEvent(p1.ParentActor.m_baseLocalID, SharedChrContact);
            }
            if (p1events)
            {
                SharedChrContact.SurfaceNormal = -SharedChrContact.SurfaceNormal;
                SharedChrContact.RelativeSpeed = -SharedChrContact.RelativeSpeed;
                SharedChrContact.CharacterFeet = feetcollision > 0;
                p1.AddCollisionEvent(p2.ParentActor.m_baseLocalID, SharedChrContact);
            }
        }
    }
}

