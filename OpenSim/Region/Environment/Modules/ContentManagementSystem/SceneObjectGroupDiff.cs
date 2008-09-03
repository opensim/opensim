// SceneObjectGroupDiff.cs
// User: bongiojp

using System;
using System.Collections.Generic;
using System.Drawing;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using log4net;
using OpenSim.Region.Physics.Manager;
using Axiom.Math;
using System.Diagnostics;

namespace OpenSim.Region.Environment.Modules.ContentManagement
{
	[Flags]
	public enum Diff
	{
		NONE = 0,
		FACECOLOR = 1,
		SHAPE = 1<<1,
		MATERIAL = 1<<2,
		TEXTURE = 1<<3,
		SCALE = 1<<4,
		POSITION = 1<<5,
		OFFSETPOSITION = 1<<6,
		ROTATIONOFFSET = 1<<7,
		ROTATIONALVELOCITY = 1<<8,
		ACCELERATION = 1<<9,
		ANGULARVELOCITY = 1<<10,
		VELOCITY = 1<<11,
		OBJECTOWNER = 1<<12,
		PERMISSIONS = 1<<13,
		DESCRIPTION = 1<<14,
		NAME = 1<<15,
		SCRIPT = 1<<16,
		CLICKACTION = 1<<17,
		PARTICLESYSTEM = 1<<18,
		GLOW = 1<<19,
		SALEPRICE = 1<<20,
		SITNAME = 1<<21,
		SITTARGETORIENTATION = 1<<22,
		SITTARGETPOSITION = 1<<23,
		TEXT = 1<<24,
		TOUCHNAME = 1<<25
	};
	
	public static class Difference
	{
		static float TimeToDiff = 0;
		
		private static readonly ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
			
		private static int TruncateSignificant(float num, int digits)
		{
			return (int) Math.Ceiling((Math.Truncate(num * 10 * digits)/10*digits));
		//	return (int) ((num * (10*digits))/10*digits);
		}
		
		private static bool AreVectorsEquivalent(LLVector3 first, LLVector3 second)
		{
			if(TruncateSignificant(first.X, 2) == TruncateSignificant(second.X, 2)
			   && TruncateSignificant(first.Y, 2) == TruncateSignificant(second.Y, 2)
			   && TruncateSignificant(first.Z, 2) == TruncateSignificant(second.Z, 2)
			   )
				return true;
			else
				return false;
		}
		
		private static bool AreQuaternionsEquivalent(LLQuaternion first, LLQuaternion second)
		{
			LLVector3 firstVector = llRot2Euler(first);
			LLVector3 secondVector = llRot2Euler(second);
			return AreVectorsEquivalent(firstVector, secondVector);
		}
	
		// Taken from Region/ScriptEngine/Common/LSL_BuiltIn_Commands.cs
		// Also changed the original function from LSL_Types to LL types
        private static LLVector3 llRot2Euler(LLQuaternion r)
        {                                                                      
            LLQuaternion t = new LLQuaternion(r.X * r.X, r.Y * r.Y, r.Z * r.Z, r.W * r.W);
            double m = (t.X + t.Y + t.Z + t.W);
            if (m == 0) return new LLVector3();
            double n = 2 * (r.Y * r.W + r.X * r.Z);
            double p = m * m - n * n;
            if (p > 0)
                return new LLVector3((float)NormalizeAngle(Math.Atan2(2.0 * (r.X * r.W - r.Y * r.Z), (-t.X - t.Y + t.Z + t.W))),
                                             (float)NormalizeAngle(Math.Atan2(n, Math.Sqrt(p))),
                                             (float)NormalizeAngle(Math.Atan2(2.0 * (r.Z * r.W - r.X * r.Y), (t.X - t.Y - t.Z + t.W))));
            else if (n > 0)
                return new LLVector3(0.0f, (float)(Math.PI / 2), (float)NormalizeAngle(Math.Atan2((r.Z * r.W + r.X * r.Y), 0.5 - t.X - t.Z)));
            else
                return new LLVector3(0.0f, (float)(-Math.PI / 2), (float)NormalizeAngle(Math.Atan2((r.Z * r.W + r.X * r.Y), 0.5 - t.X - t.Z)));
        }
		
		// Taken from Region/ScriptEngine/Common/LSL_BuiltIn_Commands.cs
		private static double NormalizeAngle(double angle)
		{
			angle = angle % (Math.PI * 2);
            if (angle < 0) angle = angle + Math.PI * 2;
            return angle;
        }
		
		/// <summary>
		/// Compares the attributes (Vectors, Quaternions, Strings, etc.) between two scene object parts 
		/// and returns a Diff bitmask which details what the differences are.
		/// </summary>
		public static Diff FindDifferences(SceneObjectPart first, SceneObjectPart second)
		{
			Stopwatch x = new Stopwatch();
			x.Start();
			
			Diff result = 0;
			
			// VECTOR COMPARISONS
			if(! AreVectorsEquivalent(first.Acceleration, second.Acceleration))
				result |= Diff.ACCELERATION;
			if(! AreVectorsEquivalent(first.AbsolutePosition, second.AbsolutePosition))
				result |= Diff.POSITION;
			if(! AreVectorsEquivalent(first.AngularVelocity, second.AngularVelocity))
				result |= Diff.ANGULARVELOCITY;
			if(! AreVectorsEquivalent(first.OffsetPosition, second.OffsetPosition))
				result |= Diff.OFFSETPOSITION;
			if(! AreVectorsEquivalent(first.RotationalVelocity, second.RotationalVelocity))
				result |= Diff.ROTATIONALVELOCITY;
			if(! AreVectorsEquivalent(first.Scale, second.Scale))
				result |= Diff.SCALE;
			if(! AreVectorsEquivalent(first.Velocity, second.Velocity))
				result |= Diff.VELOCITY;

			
			// QUATERNION COMPARISONS
			if(! AreQuaternionsEquivalent(first.RotationOffset, second.RotationOffset))
				result |= Diff.ROTATIONOFFSET;			
			
			
			// MISC COMPARISONS (LLUUID, Byte)
			if(first.ClickAction != second.ClickAction)
				result |= Diff.CLICKACTION;
			if(first.ObjectOwner != second.ObjectOwner)
				result |= Diff.OBJECTOWNER;
			
			
			// STRING COMPARISONS
			if(first.Description != second.Description)
				result |= Diff.DESCRIPTION;
			if(first.Material != second.Material)
				result |= Diff.MATERIAL;
			if(first.Name != second.Name)
				result |= Diff.NAME;
			if(first.SitName != second.SitName)
				result |= Diff.SITNAME;
			if(first.Text != second.Text)
				result |= Diff.TEXT;
			if(first.TouchName != second.TouchName)
				result |= Diff.TOUCHNAME;

			x.Stop();
			TimeToDiff += x.ElapsedMilliseconds;
			//m_log.Info("[DIFFERENCES] Time spent diffing objects so far" + TimeToDiff);
			
			return result;		
		}
	}
}
