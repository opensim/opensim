#region License
/*
MIT License
Copyright © 2006 The Mono.Xna Team

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
#endregion License

using System;
using System.ComponentModel;

namespace MonoXnaCompactMaths
{
    [Serializable]
    //[TypeConverter(typeof(QuaternionConverter))]
    public struct Quaternion : IEquatable<Quaternion>
    {
        public float X;
        public float Y;
        public float Z;
        public float W;
        static Quaternion identity = new Quaternion(0, 0, 0, 1);

        
        public Quaternion(float x, float y, float z, float w)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.W = w;
        }
        
        
        public Quaternion(Vector3 vectorPart, float scalarPart)
        {
            this.X = vectorPart.X;
            this.Y = vectorPart.Y;
            this.Z = vectorPart.Z;
            this.W = scalarPart;
        }

        public static Quaternion Identity
        {
            get{ return identity; }
        }


        public static Quaternion Add(Quaternion quaternion1, Quaternion quaternion2)
        {
            throw new NotImplementedException();
        }


        public static void Add(ref Quaternion quaternion1, ref Quaternion quaternion2, out Quaternion result)
        {
            throw new NotImplementedException();
        }


        public static Quaternion CreateFromAxisAngle(Vector3 axis, float angle)
        {
            throw new NotImplementedException();
        }


        public static void CreateFromAxisAngle(ref Vector3 axis, float angle, out Quaternion result)
        {
            throw new NotImplementedException();
        }


        public static Quaternion CreateFromRotationMatrix(Matrix matrix)
        {
            float Omega2 = matrix.M44;
            if (!isAprox(Omega2, 1f))
            {
                //"Normalize" the Rotation matrix. Norma = M44 = Omega2
                matrix = matrix / Omega2;
            }
            //Deducted from: public static Matrix CreateFromQuaternion(Quaternion quaternion)
            float lambda1pos, lambda2pos, lambda3pos, lambda1neg, lambda2neg, lambda3neg;
            lambda1pos = (1f - matrix.M11 + matrix.M23 + matrix.M32) / 2f;
            lambda2pos = (1f - matrix.M22 + matrix.M13 + matrix.M31) / 2f;
            lambda3pos = (1f - matrix.M33 + matrix.M12 + matrix.M21) / 2f;
            lambda1neg = (1f - matrix.M11 - matrix.M23 - matrix.M32) / 2f;
            lambda2neg = (1f - matrix.M22 - matrix.M13 - matrix.M31) / 2f;
            lambda3neg = (1f - matrix.M33 - matrix.M12 - matrix.M21) / 2f;

            //lambadIS = (qJ + s*qK)^2
            //q0 = w | q1 = x | q2 = y, q3 = z
            //Every value of qI (I=1,2,3) has 4 possible values cause the sqrt
            float[] x = new float[4]; float[] y = new float[4]; float[] z = new float[4];
            float[] sig1 = {1f, 1f, -1f, -1f};
            float[] sig2 = {1f, -1f, 1f, -1f};
            for (int i = 0; i < 4; i++)
            {
                x[i] = (sig1[i] * (float)Math.Sqrt(lambda1pos) + sig2[i] * (float)Math.Sqrt(lambda1neg)) / 2f;
                y[i] = (sig1[i] * (float)Math.Sqrt(lambda2pos) + sig2[i] * (float)Math.Sqrt(lambda2neg)) / 2f;
                z[i] = (sig1[i] * (float)Math.Sqrt(lambda3pos) + sig2[i] * (float)Math.Sqrt(lambda3neg)) / 2f;
            }

            //Only a set of x, y, z are the corrects values. So it requires testing
            int li_i=0, li_j=0, li_k=0;
            bool lb_testL1P, lb_testL2P, lb_testL3P, lb_testL1N, lb_testL2N, lb_testL3N;
            bool lb_superLambda = false;
            while((li_i<4)&&(!lb_superLambda))
            {
                while ((li_j < 4) && (!lb_superLambda))
                {
                    while ((li_k < 4) && (!lb_superLambda))
                    {
                        lb_testL1P = isAprox((float)(
                            Math.Pow((double)(y[li_j] + z[li_k]), 2.0)), lambda1pos);
                        lb_testL2P = isAprox((float)(
                            Math.Pow((double)(x[li_i] + z[li_k]), 2.0)), lambda2pos);
                        lb_testL3P = isAprox((float)(
                            Math.Pow((double)(x[li_i] + y[li_j]), 2.0)), lambda3pos);
                        lb_testL1N = isAprox((float)(
                            Math.Pow((double)(y[li_j] - z[li_k]), 2.0)), lambda1neg);
                        lb_testL2N = isAprox((float)(
                            Math.Pow((double)(x[li_i] - z[li_k]), 2.0)), lambda2neg);
                        lb_testL3N = isAprox((float)(
                            Math.Pow((double)(x[li_i] - y[li_j]), 2.0)), lambda3neg);

                        lb_superLambda = (lb_testL1P && lb_testL2P && lb_testL3P
                            && lb_testL1N && lb_testL2N && lb_testL3N);

                        if (!lb_superLambda) li_k++;
                    }
                    if (!lb_superLambda) li_j++;
                }
                if (!lb_superLambda) li_i++;
            }

            Quaternion q = new Quaternion();

            if (lb_superLambda)
            {
                q.X = x[li_i]; q.Y = y[li_j]; q.Z = z[li_k];
                q.W = (matrix.M12 - 2f * q.X * q.Y) / (2f * q.Z);

                if (!isAprox(Omega2, 1f))
                {
                    if (Omega2 < 0) throw new Exception("Quaternion.CreateFromRotationMatrix: Omega2 is negative!");
                    q = q * (float)Math.Sqrt(Omega2);//2 possibles values (+/-). For now only 1.
                }
            }
            else
            {
                q = Quaternion.identity;
            }

            return q;
        }
        private static float floatError = 0.000001f;
        private static bool isAprox(float test, float realValue)
        {
            return (((realValue * (1f - floatError)) <= test) && (test <= (realValue * (1f + floatError))));
        }


        public static void CreateFromRotationMatrix(ref Matrix matrix, out Quaternion result)
        {
            throw new NotImplementedException();
        }


        public static Quaternion Divide(Quaternion quaternion1, Quaternion quaternion2)
        {
            throw new NotImplementedException();
        }


        public static void Divide(ref Quaternion quaternion1, ref Quaternion quaternion2, out Quaternion result)
        {
            throw new NotImplementedException();
        }


        public static float Dot(Quaternion quaternion1, Quaternion quaternion2)
        {
            throw new NotImplementedException();
        }


        public static void Dot(ref Quaternion quaternion1, ref Quaternion quaternion2, out float result)
        {
            throw new NotImplementedException();
        }


        public override bool Equals(object obj)
        {
            throw new NotImplementedException();
        }


        public bool Equals(Quaternion other)
        {
            throw new NotImplementedException();
        }


        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }


        public static Quaternion Inverse(Quaternion quaternion)
        {
            throw new NotImplementedException();
        }


        public static void Inverse(ref Quaternion quaternion, out Quaternion result)
        {
            throw new NotImplementedException();
        }


        public float Length()
        {
            //---
            return (float)Math.Sqrt(Math.Pow(this.W, 2.0) + Math.Pow(this.X, 2.0) + Math.Pow(this.Y, 2.0) + Math.Pow(this.Z, 2.0));
            //---
            //throw new NotImplementedException();
        }


        public float LengthSquared()
        {
            //---
            return (float)(Math.Pow(this.W, 2.0) + Math.Pow(this.X, 2.0) + Math.Pow(this.Y, 2.0) + Math.Pow(this.Z, 2.0));
            //---
            //throw new NotImplementedException();
        }


        public static Quaternion Lerp(Quaternion quaternion1, Quaternion quaternion2, float amount)
        {
            throw new NotImplementedException();
        }


        public static void Lerp(ref Quaternion quaternion1, ref Quaternion quaternion2, float amount, out Quaternion result)
        {
            throw new NotImplementedException();
        }


        public static Quaternion Slerp(Quaternion quaternion1, Quaternion quaternion2, float amount)
        {
            throw new NotImplementedException();
        }


        public static void Slerp(ref Quaternion quaternion1, ref Quaternion quaternion2, float amount, out Quaternion result)
        {
            throw new NotImplementedException();
        }


        public static Quaternion Subtract(Quaternion quaternion1, Quaternion quaternion2)
        {
            throw new NotImplementedException();
        }


        public static void Subtract(ref Quaternion quaternion1, ref Quaternion quaternion2, out Quaternion result)
        {
            throw new NotImplementedException();
        }


        public static Quaternion Multiply(Quaternion quaternion1, Quaternion quaternion2)
        {
            throw new NotImplementedException();
        }


        public static Quaternion Multiply(Quaternion quaternion1, float scaleFactor)
        {
            throw new NotImplementedException();
        }


        public static void Multiply(ref Quaternion quaternion1, float scaleFactor, out Quaternion result)
        {
            throw new NotImplementedException();
        }


        public static void Multiply(ref Quaternion quaternion1, ref Quaternion quaternion2, out Quaternion result)
        {
            throw new NotImplementedException();
        }


        public static Quaternion Negate(Quaternion quaternion)
        {
            throw new NotImplementedException();
        }


        public static void Negate(ref Quaternion quaternion, out Quaternion result)
        {
            throw new NotImplementedException();
        }


        public void Normalize()
        {
            //---
            this = Normalize(this);
            //---
            //throw new NotImplementedException();
        }


        public static Quaternion Normalize(Quaternion quaternion)
        {
            //---
            return quaternion / quaternion.Length();
            //---
            //throw new NotImplementedException();
        }


        public static void Normalize(ref Quaternion quaternion, out Quaternion result)
        {
            throw new NotImplementedException();
        }


        public static Quaternion operator +(Quaternion quaternion1, Quaternion quaternion2)
        {
            throw new NotImplementedException();
        }


        public static Quaternion operator /(Quaternion quaternion1, Quaternion quaternion2)
        {
            throw new NotImplementedException();
        }
        public static Quaternion operator /(Quaternion quaternion, float factor)
        {
            quaternion.W /= factor;
            quaternion.X /= factor;
            quaternion.Y /= factor;
            quaternion.Z /= factor;
            return quaternion;
        }

        public static bool operator ==(Quaternion quaternion1, Quaternion quaternion2)
        {
            throw new NotImplementedException();
        }


        public static bool operator !=(Quaternion quaternion1, Quaternion quaternion2)
        {
            throw new NotImplementedException();
        }


        public static Quaternion operator *(Quaternion quaternion1, Quaternion quaternion2)
        {
            //---
            //Grassmann product
            Quaternion quaternionProduct = new Quaternion();

            quaternionProduct.W = quaternion1.W * quaternion2.W - quaternion1.X * quaternion2.X - quaternion1.Y * quaternion2.Y - quaternion1.Z * quaternion2.Z;
            quaternionProduct.X = quaternion1.W * quaternion2.X + quaternion1.X * quaternion2.W + quaternion1.Y * quaternion2.Z - quaternion1.Z * quaternion2.Y;
            quaternionProduct.Y = quaternion1.W * quaternion2.Y - quaternion1.X * quaternion2.Z + quaternion1.Y * quaternion2.W + quaternion1.Z * quaternion2.X;
            quaternionProduct.Z = quaternion1.W * quaternion2.Z + quaternion1.X * quaternion2.Y - quaternion1.Y * quaternion2.X + quaternion1.Z * quaternion2.W;
            return quaternionProduct;
            //---
            //throw new NotImplementedException();
        }


        public static Quaternion operator *(Quaternion quaternion1, float scaleFactor)
        {
            return new Quaternion(quaternion1.X / scaleFactor, quaternion1.Y / scaleFactor,
                quaternion1.Z / scaleFactor, quaternion1.W / scaleFactor);
        }


        public static Quaternion operator -(Quaternion quaternion1, Quaternion quaternion2)
        {
            throw new NotImplementedException();
        }


        public static Quaternion operator -(Quaternion quaternion)
        {
            throw new NotImplementedException();
        }


        public override string ToString()
        {
            return "(" + this.X + ", " + this.Y + ", " + this.Z + ", " + this.W + ")";
        }

        private static void Conjugate(ref Quaternion quaternion, out Quaternion result)
        {
            throw new NotImplementedException();
        }
    }
}
