using System;

namespace OpenSim.Region.ScriptEngine.Common
{
    [Serializable]
    public class LSL_Types
    {
        [Serializable]
        public struct Vector3
        {
            public double X;
            public double Y;
            public double Z;

            public Vector3(Vector3 vector)
		    {
			    X = (float)vector.X;
			    Y = (float)vector.Y;
			    Z = (float)vector.Z;
    		}
            public Vector3(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }
        [Serializable]
        public struct Quaternion
        {
            public double X;
            public double Y;
            public double Z;
            public double R;

            public Quaternion(Quaternion Quat)
		    {
                X = (float)Quat.X;
                Y = (float)Quat.Y;
                Z = (float)Quat.Z;
                R = (float)Quat.R;
            }
            public Quaternion(double x, double y, double z, double r)
            {
                X = x;
                Y = y;
                Z = z;
                R = r;
            }

        }
    }
}
