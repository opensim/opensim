using System;
namespace PBJ
{

    public class IMessage
    {
        public virtual Google.ProtocolBuffers.IMessage _PBJISuper { get { return null; } }
        protected virtual bool _HasAllPBJFields { get { return true; } }
        public void WriteTo(Google.ProtocolBuffers.CodedOutputStream output)
        {
            _PBJISuper.WriteTo(output);
        }
        public override bool Equals(object other)
        {
            return _PBJISuper.Equals(other);
        }
        public int SerializedSize { get { return _PBJISuper.SerializedSize; } }

        public override int GetHashCode() { return _PBJISuper.GetHashCode(); }

        public override string ToString()
        {
            return _PBJISuper.ToString();
        }
        public virtual IBuilder WeakCreateBuilderForType() { return null; }
        public Google.ProtocolBuffers.ByteString ToByteString()
        {
            return _PBJISuper.ToByteString();
        }
        public byte[] ToByteArray()
        {
            return _PBJISuper.ToByteArray();
        }
        public void WriteTo(global::System.IO.Stream output)
        {
            _PBJISuper.WriteTo(output);
        }
        //    Google.ProtocolBuffers.MessageDescriptor DescriptorForType { get {return _PBJISuper.DescriptorForType;} }
        public Google.ProtocolBuffers.UnknownFieldSet UnknownFields { get { return _PBJISuper.UnknownFields; } }
        public class IBuilder
        {
            public virtual Google.ProtocolBuffers.IBuilder _PBJISuper { get { return null; } }
            protected virtual bool _HasAllPBJFields { get { return true; } }
        }
    }

    public struct Vector2f
    {

        public float x;
        public float y;


        public Vector2f(float _x, float _y)
        {
            x = _x; y = _y;
        }

        public Vector2f(Vector2f cpy)
        {
            x = cpy.x; y = cpy.y;
        }


        public Vector2f Negate()
        {
            return new Vector2f(-x, -y);
        }

        public Vector2f Add(Vector2f rhs)
        {
            return new Vector2f(x + rhs.x, y + rhs.y);
        }

        public Vector2f Subtract(Vector2f rhs)
        {
            return new Vector2f(x - rhs.x, y - rhs.y);
        }

        public Vector2f Multiply(Vector2f rhs)
        {
            return new Vector2f(x * rhs.x, y * rhs.y);
        }

        public Vector2f Multiply(float s)
        {
            return new Vector2f(x * s, y * s);
        }

        public Vector2f Divide(Vector2f rhs)
        {
            return new Vector2f(x / rhs.x, y / rhs.y);
        }

        public Vector2f Divide(float s)
        {
            return new Vector2f(x / s, y / s);
        }

        public float Dot(Vector2f rhs)
        {
            return (x * rhs.x + y * rhs.y);
        }

        public void Normalize()
        {
            float len = Length;
            if (len != 0.0)
            {
                x /= len; y /= len;
            }
        }

        public Vector2f Normalized
        {
            get
            {
                Vector2f normed = new Vector2f(this);
                normed.Normalize();
                return normed;
            }
        }

        public float SquaredLength
        {
            get
            {
                return (x * x + y * y);
            }
        }
        public float Length
        {
            get
            {
                return (float)Math.Sqrt(SquaredLength);
            }
        }


        public override string ToString()
        {
            return String.Format("<{0}, {1}>", x, y);
        }


        public static Vector2f operator -(Vector2f uo)
        {
            return uo.Negate();
        }

        public static Vector2f operator +(Vector2f lhs, Vector2f rhs)
        {
            return lhs.Add(rhs);
        }

        public static Vector2f operator -(Vector2f lhs, Vector2f rhs)
        {
            return lhs.Subtract(rhs);
        }

        public static Vector2f operator *(Vector2f lhs, Vector2f rhs)
        {
            return lhs.Multiply(rhs);
        }

        public static Vector2f operator *(Vector2f lhs, float rhs)
        {
            return lhs.Multiply(rhs);
        }
        public static Vector2f operator *(float lhs, Vector2f rhs)
        {
            return rhs.Multiply(lhs);
        }

        public static Vector2f operator /(Vector2f lhs, Vector2f rhs)
        {
            return lhs.Divide(rhs);
        }

        public static Vector2f operator /(Vector2f lhs, float rhs)
        {
            return lhs.Divide(rhs);
        }

    } // struct Vector2f

    public struct Vector2d
    {

        public double x;
        public double y;


        public Vector2d(double _x, double _y)
        {
            x = _x; y = _y;
        }

        public Vector2d(Vector2d cpy)
        {
            x = cpy.x; y = cpy.y;
        }


        public Vector2d Negate()
        {
            return new Vector2d(-x, -y);
        }

        public Vector2d Add(Vector2d rhs)
        {
            return new Vector2d(x + rhs.x, y + rhs.y);
        }

        public Vector2d Subtract(Vector2d rhs)
        {
            return new Vector2d(x - rhs.x, y - rhs.y);
        }

        public Vector2d Multiply(Vector2d rhs)
        {
            return new Vector2d(x * rhs.x, y * rhs.y);
        }

        public Vector2d Multiply(double s)
        {
            return new Vector2d(x * s, y * s);
        }

        public Vector2d Divide(Vector2d rhs)
        {
            return new Vector2d(x / rhs.x, y / rhs.y);
        }

        public Vector2d Divide(double s)
        {
            return new Vector2d(x / s, y / s);
        }

        public double Dot(Vector2d rhs)
        {
            return (x * rhs.x + y * rhs.y);
        }

        public void Normalize()
        {
            double len = Length;
            if (len != 0.0)
            {
                x /= len; y /= len;
            }
        }

        public Vector2d Normalized
        {
            get
            {
                Vector2d normed = new Vector2d(this);
                normed.Normalize();
                return normed;
            }
        }

        public double SquaredLength
        {
            get
            {
                return (x * x + y * y);
            }
        }
        public double Length
        {
            get
            {
                return Math.Sqrt(SquaredLength);
            }
        }


        public override string ToString()
        {
            return String.Format("<{0}, {1}>", x, y);
        }


        public static Vector2d operator -(Vector2d uo)
        {
            return uo.Negate();
        }

        public static Vector2d operator +(Vector2d lhs, Vector2d rhs)
        {
            return lhs.Add(rhs);
        }

        public static Vector2d operator -(Vector2d lhs, Vector2d rhs)
        {
            return lhs.Subtract(rhs);
        }

        public static Vector2d operator *(Vector2d lhs, Vector2d rhs)
        {
            return lhs.Multiply(rhs);
        }

        public static Vector2d operator *(Vector2d lhs, double rhs)
        {
            return lhs.Multiply(rhs);
        }
        public static Vector2d operator *(double lhs, Vector2d rhs)
        {
            return rhs.Multiply(lhs);
        }

        public static Vector2d operator /(Vector2d lhs, Vector2d rhs)
        {
            return lhs.Divide(rhs);
        }

        public static Vector2d operator /(Vector2d lhs, double rhs)
        {
            return lhs.Divide(rhs);
        }

    } // struct Vector2d

    public struct Vector3f
    {

        public float x;
        public float y;
        public float z;


        public Vector3f(float _x, float _y, float _z)
        {
            x = _x; y = _y; z = _z;
        }

        public Vector3f(Vector3f cpy)
        {
            x = cpy.x; y = cpy.y; z = cpy.z;
        }


        public Vector3f Negate()
        {
            return new Vector3f(-x, -y, -z);
        }

        public Vector3f Add(Vector3f rhs)
        {
            return new Vector3f(x + rhs.x, y + rhs.y, z + rhs.z);
        }

        public Vector3f Subtract(Vector3f rhs)
        {
            return new Vector3f(x - rhs.x, y - rhs.y, z - rhs.z);
        }

        public Vector3f Multiply(Vector3f rhs)
        {
            return new Vector3f(x * rhs.x, y * rhs.y, z * rhs.z);
        }

        public Vector3f Multiply(float s)
        {
            return new Vector3f(x * s, y * s, z * s);
        }

        public Vector3f Divide(Vector3f rhs)
        {
            return new Vector3f(x / rhs.x, y / rhs.y, z / rhs.z);
        }

        public Vector3f Divide(float s)
        {
            return new Vector3f(x / s, y / s, z / s);
        }

        public float Dot(Vector3f rhs)
        {
            return (x * rhs.x + y * rhs.y + z * rhs.z);
        }

        public Vector3f Cross(Vector3f rhs)
        {
            return new Vector3f(y * rhs.z - z * rhs.y, z * rhs.x - x * rhs.z, x * rhs.y - y * rhs.x);
        }

        public void Normalize()
        {
            float len = Length;
            if (len != 0.0)
            {
                x /= len; y /= len; z /= len;
            }
        }

        public Vector3f Normalized
        {
            get
            {
                Vector3f normed = new Vector3f(this);
                normed.Normalize();
                return normed;
            }
        }

        public float SquaredLength
        {
            get
            {
                return (x * x + y * y + z * z);
            }
        }
        public float Length
        {
            get
            {
                return (float)Math.Sqrt(SquaredLength);
            }
        }


        public override string ToString()
        {
            return String.Format("<{0}, {1}, {2}>", x, y, z);
        }


        public static Vector3f operator -(Vector3f uo)
        {
            return uo.Negate();
        }

        public static Vector3f operator +(Vector3f lhs, Vector3f rhs)
        {
            return lhs.Add(rhs);
        }

        public static Vector3f operator -(Vector3f lhs, Vector3f rhs)
        {
            return lhs.Subtract(rhs);
        }

        public static Vector3f operator *(Vector3f lhs, Vector3f rhs)
        {
            return lhs.Multiply(rhs);
        }

        public static Vector3f operator *(Vector3f lhs, float rhs)
        {
            return lhs.Multiply(rhs);
        }
        public static Vector3f operator *(float lhs, Vector3f rhs)
        {
            return rhs.Multiply(lhs);
        }

        public static Vector3f operator /(Vector3f lhs, Vector3f rhs)
        {
            return lhs.Divide(rhs);
        }

        public static Vector3f operator /(Vector3f lhs, float rhs)
        {
            return lhs.Divide(rhs);
        }

    } // struct Vector3f

    public struct Vector3d
    {

        public double x;
        public double y;
        public double z;


        public Vector3d(double _x, double _y, double _z)
        {
            x = _x; y = _y; z = _z;
        }

        public Vector3d(Vector3d cpy)
        {
            x = cpy.x; y = cpy.y; z = cpy.z;
        }


        public Vector3d Negate()
        {
            return new Vector3d(-x, -y, -z);
        }

        public Vector3d Add(Vector3d rhs)
        {
            return new Vector3d(x + rhs.x, y + rhs.y, z + rhs.z);
        }

        public Vector3d Subtract(Vector3d rhs)
        {
            return new Vector3d(x - rhs.x, y - rhs.y, z - rhs.z);
        }

        public Vector3d Multiply(Vector3d rhs)
        {
            return new Vector3d(x * rhs.x, y * rhs.y, z * rhs.z);
        }

        public Vector3d Multiply(double s)
        {
            return new Vector3d(x * s, y * s, z * s);
        }

        public Vector3d Divide(Vector3d rhs)
        {
            return new Vector3d(x / rhs.x, y / rhs.y, z / rhs.z);
        }

        public Vector3d Divide(double s)
        {
            return new Vector3d(x / s, y / s, z / s);
        }

        public double Dot(Vector3d rhs)
        {
            return (x * rhs.x + y * rhs.y + z * rhs.z);
        }

        public Vector3d Cross(Vector3d rhs)
        {
            return new Vector3d(y * rhs.z - z * rhs.y, z * rhs.x - x * rhs.z, x * rhs.y - y * rhs.x);
        }

        public void Normalize()
        {
            double len = Length;
            if (len != 0.0)
            {
                x /= len; y /= len; z /= len;
            }
        }

        public Vector3d Normalized
        {
            get
            {
                Vector3d normed = new Vector3d(this);
                normed.Normalize();
                return normed;
            }
        }

        public double SquaredLength
        {
            get
            {
                return (x * x + y * y + z * z);
            }
        }
        public double Length
        {
            get
            {
                return Math.Sqrt(SquaredLength);
            }
        }


        public override string ToString()
        {
            return String.Format("<{0}, {1}, {2}>", x, y, z);
        }


        public static Vector3d operator -(Vector3d uo)
        {
            return uo.Negate();
        }

        public static Vector3d operator +(Vector3d lhs, Vector3d rhs)
        {
            return lhs.Add(rhs);
        }

        public static Vector3d operator -(Vector3d lhs, Vector3d rhs)
        {
            return lhs.Subtract(rhs);
        }

        public static Vector3d operator *(Vector3d lhs, Vector3d rhs)
        {
            return lhs.Multiply(rhs);
        }

        public static Vector3d operator *(Vector3d lhs, double rhs)
        {
            return lhs.Multiply(rhs);
        }
        public static Vector3d operator *(double lhs, Vector3d rhs)
        {
            return rhs.Multiply(lhs);
        }

        public static Vector3d operator /(Vector3d lhs, Vector3d rhs)
        {
            return lhs.Divide(rhs);
        }

        public static Vector3d operator /(Vector3d lhs, double rhs)
        {
            return lhs.Divide(rhs);
        }

    } // struct Vector3d

    public struct Quaternion
    {

        public float w;
        public float x;
        public float y;
        public float z;


        public Quaternion(float _w, float _x, float _y, float _z)
        {
            w = _w; x = _x; y = _y; z = _z;
        }

        public Quaternion(Quaternion cpy)
        {
            w = cpy.w; x = cpy.x; y = cpy.y; z = cpy.z;
        }

        public static readonly Quaternion Identity = new Quaternion((float)1.0, (float)0.0, (float)0.0, (float)0.0);

        public static Quaternion FromAxisAngle(Vector3f axis, float rads)
        {
            float halfAngle = rads * 0.5f;
            float sinHalf = (float)Math.Sin(halfAngle);
            float w = (float)Math.Cos(halfAngle);
            float x = sinHalf * axis.x;
            float y = sinHalf * axis.y;
            float z = sinHalf * axis.z;
            return new Quaternion(w, x, y, z);
        }

        public static Quaternion FromAxisAngle(Vector3d axis, float rads)
        {
            float halfAngle = rads * 0.5f;
            float sinHalf = (float)Math.Sin(halfAngle);
            float w = (float)Math.Cos(halfAngle);
            float x = (float)(sinHalf * axis.x);
            float y = (float)(sinHalf * axis.y);
            float z = (float)(sinHalf * axis.z);
            return new Quaternion(w, x, y, z);
        }


        public Quaternion Add(Quaternion rhs)
        {
            return new Quaternion(w + rhs.w, x + rhs.x, y + rhs.y, z + rhs.z);
        }

        public Quaternion Subtract(Quaternion rhs)
        {
            return new Quaternion(w - rhs.w, x - rhs.x, y - rhs.y, z - rhs.z);
        }

        public Quaternion Multiply(Quaternion rhs)
        {
            return new Quaternion(
                    w * rhs.w - x * rhs.x - y * rhs.y - z * rhs.z,
                    w * rhs.x + x * rhs.w + y * rhs.z - z * rhs.y,
                    w * rhs.y + y * rhs.w + z * rhs.x - x * rhs.z,
                    w * rhs.z + z * rhs.w + x * rhs.y - y * rhs.x
            );
        }

        public Vector3f Multiply(Vector3f rhs)
        {
            Vector3f qvec = new Vector3f(x, y, z);
            Vector3f uv = qvec.Cross(rhs);
            Vector3f uuv = qvec.Cross(uv);
            uv *= 2.0f * w;
            uuv *= 2.0f;

            return rhs + uv + uuv;
        }

        public Vector3d Multiply(Vector3d rhs)
        {
            Vector3d qvec = new Vector3d(x, y, z);
            Vector3d uv = qvec.Cross(rhs);
            Vector3d uuv = qvec.Cross(uv);
            uv *= 2.0f * w;
            uuv *= 2.0f;

            return rhs + uv + uuv;
        }

        public Quaternion Multiply(float rhs)
        {
            return new Quaternion(w * rhs, x * rhs, y * rhs, z * rhs);
        }

        public Quaternion Negate()
        {
            return new Quaternion(-w, -x, -y, -z);
        }

        public float Dot(Quaternion rhs)
        {
            return (w * rhs.w + x * rhs.x + y * rhs.y + z * rhs.z);
        }

        public float Norm
        {
            get
            {
                return (float)Math.Sqrt(w * w + x * x + y * y + z * z);
            }
        }

        public float SquareNorm
        {
            get
            {
                return (w * w + x * x + y * y + z * z);
            }
        }

        public void Normalize()
        {
            float len = SquareNorm;
            if (len == 0.0) return;
            float factor = 1.0f / (float)Math.Sqrt(len);
            this *= factor;
        }

        public Quaternion Normalized
        {
            get
            {
                Quaternion q = new Quaternion(this);
                q.Normalize();
                return q;
            }
        }

        public Quaternion Inverse
        {
            get
            {
                float norm = SquareNorm;
                if (norm > 0.0)
                {
                    double invnorm = 1.0 / norm;
                    return new Quaternion((float)(w * invnorm), (float)(-x * invnorm), (float)(-y * invnorm), (float)(-z * invnorm));
                }
                else
                    return new Quaternion((float)0.0, 0.0f, 0.0f, 0.0f);
            }
        }


        public override string ToString()
        {
            return String.Format("<{0}, {1}, {2}, {3}>", w, x, y, z);
        }



        public static Quaternion operator -(Quaternion uo)
        {
            return uo.Negate();
        }

        public static Quaternion operator +(Quaternion lhs, Quaternion rhs)
        {
            return lhs.Add(rhs);
        }

        public static Quaternion operator -(Quaternion lhs, Quaternion rhs)
        {
            return lhs.Subtract(rhs);
        }

        public static Vector3f operator *(Quaternion lhs, Vector3f rhs)
        {
            return lhs.Multiply(rhs);
        }

        public static Vector3d operator *(Quaternion lhs, Vector3d rhs)
        {
            return lhs.Multiply(rhs);
        }

        public static Quaternion operator *(Quaternion lhs, Quaternion rhs)
        {
            return lhs.Multiply(rhs);
        }

        public static Quaternion operator *(Quaternion lhs, float rhs)
        {
            return lhs.Multiply(rhs);
        }

        public static Quaternion operator *(float lhs, Quaternion rhs)
        {
            return rhs.Multiply(lhs);
        }

    } // struct Quaternion


    public struct Vector4f
    {

        public float x;
        public float y;
        public float z;
        public float w;


        public Vector4f(float _x, float _y, float _z, float _w)
        {
            x = _x; y = _y; z = _z; w = _w;
        }

        public Vector4f(Vector4f cpy)
        {
            x = cpy.x; y = cpy.y; z = cpy.z; w = cpy.w;
        }


        public Vector4f Negate()
        {
            return new Vector4f(-x, -y, -z, -w);
        }

        public Vector4f Add(Vector4f rhs)
        {
            return new Vector4f(x + rhs.x, y + rhs.y, z + rhs.z, w + rhs.w);
        }

        public Vector4f Subtract(Vector4f rhs)
        {
            return new Vector4f(x - rhs.x, y - rhs.y, z - rhs.z, w - rhs.w);
        }

        public Vector4f Multiply(Vector4f rhs)
        {
            return new Vector4f(x * rhs.x, y * rhs.y, z * rhs.z, w * rhs.w);
        }

        public Vector4f Multiply(float s)
        {
            return new Vector4f(x * s, y * s, z * s, w * s);
        }

        public Vector4f Divide(Vector4f rhs)
        {
            return new Vector4f(x / rhs.x, y / rhs.y, z / rhs.z, w / rhs.w);
        }

        public Vector4f Divide(float s)
        {
            return new Vector4f(x / s, y / s, z / s, w / s);
        }

        public float Dot(Vector4f rhs)
        {
            return (x * rhs.x + y * rhs.y + z * rhs.z + w * rhs.w);
        }

        public void Normalize()
        {
            float len = Length;
            if (len != 0.0)
            {
                x /= len; y /= len; z /= len; w /= len;
            }
        }

        public Vector4f Normalized
        {
            get
            {
                Vector4f normed = new Vector4f(this);
                normed.Normalize();
                return normed;
            }
        }

        public float SquaredLength
        {
            get
            {
                return (x * x + y * y + z * z + w * w);
            }
        }
        public float Length
        {
            get
            {
                return (float)Math.Sqrt(SquaredLength);
            }
        }


        public override string ToString()
        {
            return String.Format("<{0}, {1}, {2}, {3}>", x, y, z, w);
        }


        public static Vector4f operator -(Vector4f uo)
        {
            return uo.Negate();
        }

        public static Vector4f operator +(Vector4f lhs, Vector4f rhs)
        {
            return lhs.Add(rhs);
        }

        public static Vector4f operator -(Vector4f lhs, Vector4f rhs)
        {
            return lhs.Subtract(rhs);
        }

        public static Vector4f operator *(Vector4f lhs, Vector4f rhs)
        {
            return lhs.Multiply(rhs);
        }

        public static Vector4f operator *(Vector4f lhs, float rhs)
        {
            return lhs.Multiply(rhs);
        }
        public static Vector4f operator *(float lhs, Vector4f rhs)
        {
            return rhs.Multiply(lhs);
        }

        public static Vector4f operator /(Vector4f lhs, Vector4f rhs)
        {
            return lhs.Divide(rhs);
        }

        public static Vector4f operator /(Vector4f lhs, float rhs)
        {
            return lhs.Divide(rhs);
        }

    } // struct Vector4f



    public struct Vector4d
    {

        public double x;
        public double y;
        public double z;
        public double w;


        public Vector4d(double _x, double _y, double _z, double _w)
        {
            x = _x; y = _y; z = _z; w = _w;
        }

        public Vector4d(Vector4d cpy)
        {
            x = cpy.x; y = cpy.y; z = cpy.z; w = cpy.w;
        }


        public Vector4d Negate()
        {
            return new Vector4d(-x, -y, -z, -w);
        }

        public Vector4d Add(Vector4d rhs)
        {
            return new Vector4d(x + rhs.x, y + rhs.y, z + rhs.z, w + rhs.w);
        }

        public Vector4d Subtract(Vector4d rhs)
        {
            return new Vector4d(x - rhs.x, y - rhs.y, z - rhs.z, w - rhs.w);
        }

        public Vector4d Multiply(Vector4d rhs)
        {
            return new Vector4d(x * rhs.x, y * rhs.y, z * rhs.z, w * rhs.w);
        }

        public Vector4d Multiply(double s)
        {
            return new Vector4d(x * s, y * s, z * s, w * s);
        }

        public Vector4d Divide(Vector4d rhs)
        {
            return new Vector4d(x / rhs.x, y / rhs.y, z / rhs.z, w / rhs.w);
        }

        public Vector4d Divide(double s)
        {
            return new Vector4d(x / s, y / s, z / s, w / s);
        }

        public double Dot(Vector4d rhs)
        {
            return (x * rhs.x + y * rhs.y + z * rhs.z + w * rhs.w);
        }

        public void Normalize()
        {
            double len = Length;
            if (len != 0.0)
            {
                x /= len; y /= len; z /= len; w /= len;
            }
        }

        public Vector4d Normalized
        {
            get
            {
                Vector4d normed = new Vector4d(this);
                normed.Normalize();
                return normed;
            }
        }

        public double SquaredLength
        {
            get
            {
                return (x * x + y * y + z * z + w * w);
            }
        }
        public double Length
        {
            get
            {
                return Math.Sqrt(SquaredLength);
            }
        }


        public override string ToString()
        {
            return String.Format("<{0}, {1}, {2}, {3}>", x, y, z, w);
        }


        public static Vector4d operator -(Vector4d uo)
        {
            return uo.Negate();
        }

        public static Vector4d operator +(Vector4d lhs, Vector4d rhs)
        {
            return lhs.Add(rhs);
        }

        public static Vector4d operator -(Vector4d lhs, Vector4d rhs)
        {
            return lhs.Subtract(rhs);
        }

        public static Vector4d operator *(Vector4d lhs, Vector4d rhs)
        {
            return lhs.Multiply(rhs);
        }

        public static Vector4d operator *(Vector4d lhs, double rhs)
        {
            return lhs.Multiply(rhs);
        }
        public static Vector4d operator *(double lhs, Vector4d rhs)
        {
            return rhs.Multiply(lhs);
        }

        public static Vector4d operator /(Vector4d lhs, Vector4d rhs)
        {
            return lhs.Divide(rhs);
        }

        public static Vector4d operator /(Vector4d lhs, double rhs)
        {
            return lhs.Divide(rhs);
        }

    } // struct Vector4d



    public struct BoundingBox3f3f
    {
        Vector3f mMin;
        Vector3f mDiag;
        public BoundingBox3f3f(float minx, float miny, float minz, float diagx, float diagy, float diagz)
        {
            mMin.x = minx;
            mMin.y = miny;
            mMin.z = minz;

            mDiag.x = diagx;
            mDiag.y = diagy;
            mDiag.z = diagz;
        }
        public BoundingBox3f3f(Vector3f min, Vector3f max)
        {
            mMin = min;
            mDiag = (max - min);
        }
        public BoundingBox3f3f(BoundingBox3f3f cpy, Vector3f scale)
        {
            mMin.x = (float)(cpy.mMin.x * scale.x);
            mMin.y = (float)(cpy.mMin.y * scale.y);
            mMin.z = (float)(cpy.mMin.z * scale.z);

            mDiag.x = (float)(cpy.mDiag.x * scale.x);
            mDiag.y = (float)(cpy.mDiag.y * scale.y);
            mDiag.z = (float)(cpy.mDiag.z * scale.z);
        }
        public Vector3f Min
        {
            get
            {
                return new Vector3f(mMin.x, mMin.y, mMin.z);
            }
        }
        public Vector3f Max
        {
            get
            {
                return new Vector3f(mMin.x + mDiag.x, mMin.y + mDiag.y, mMin.z + mDiag.z);
            }
        }

        public Vector3f Diag
        {
            get
            {
                return new Vector3f(mDiag.x, mDiag.y, mDiag.z);
            }
        }


        public override string ToString()
        {
            return "[" + this.Min.ToString() + " - " + this.Max.ToString() + "]";
        }
        public BoundingBox3f3f Merge(BoundingBox3f3f other)
        {
            Vector3f thisMax = Max;
            Vector3f otherMax = other.Max;
            bool xless = other.mMin.x > mMin.x;
            bool yless = other.mMin.y > mMin.y;
            bool zless = other.mMin.z > mMin.z;

            bool xmore = otherMax.x < thisMax.x;
            bool ymore = otherMax.y < thisMax.y;
            bool zmore = otherMax.z < thisMax.z;
            return new BoundingBox3f3f(xless ? mMin.x : other.mMin.x,
                                   yless ? mMin.y : other.mMin.y,
                                   zless ? mMin.z : other.mMin.z,
                                   xmore ? (xless ? mDiag.x : otherMax.x - mMin.x) : (xless ? thisMax.x - other.mMin.x : other.mDiag.x),
                                   ymore ? (yless ? mDiag.y : otherMax.y - mMin.y) : (yless ? thisMax.y - other.mMin.y : other.mDiag.y),
                                   zmore ? (zless ? mDiag.z : otherMax.z - mMin.z) : (zless ? thisMax.z - other.mMin.z : other.mDiag.z));
        }

    } // struct BoundingBox

    public struct BoundingBox3d3f
    {
        Vector3d mMin;
        Vector3f mDiag;
        public BoundingBox3d3f(double minx, double miny, double minz, float diagx, float diagy, float diagz)
        {
            mMin.x = minx;
            mMin.y = miny;
            mMin.z = minz;

            mDiag.x = diagx;
            mDiag.y = diagy;
            mDiag.z = diagz;
        }
        public BoundingBox3d3f(Vector3d min, Vector3f max)
        {
            mMin = min;

            mDiag = new Vector3f((float)(max.x - min.x),
                               (float)(max.y - min.y),
                               (float)(max.z - min.z));
        }
        public BoundingBox3d3f(BoundingBox3d3f cpy, Vector3d scale)
        {
            mMin.x = (double)(cpy.mMin.x * scale.x);
            mMin.y = (double)(cpy.mMin.y * scale.y);
            mMin.z = (double)(cpy.mMin.z * scale.z);

            mDiag.x = (float)(cpy.mDiag.x * scale.x);
            mDiag.y = (float)(cpy.mDiag.y * scale.y);
            mDiag.z = (float)(cpy.mDiag.z * scale.z);
        }
        public Vector3d Min
        {
            get
            {
                return new Vector3d(mMin.x, mMin.y, mMin.z);
            }
        }
        public Vector3d Max
        {
            get
            {
                return new Vector3d(mMin.x + mDiag.x, mMin.y + mDiag.y, mMin.z + mDiag.z);
            }
        }

        public Vector3d Diag
        {
            get
            {
                return new Vector3d(mDiag.x, mDiag.y, mDiag.z);
            }
        }


        public override string ToString()
        {
            return "[" + this.Min.ToString() + " - " + this.Max.ToString() + "]";
        }
        public BoundingBox3d3f Merge(BoundingBox3d3f other)
        {
            Vector3d thisMax = Max;
            Vector3d otherMax = other.Max;
            bool xless = other.mMin.x > mMin.x;
            bool yless = other.mMin.y > mMin.y;
            bool zless = other.mMin.z > mMin.z;

            bool xmore = otherMax.x < thisMax.x;
            bool ymore = otherMax.y < thisMax.y;
            bool zmore = otherMax.z < thisMax.z;
            return new BoundingBox3d3f(xless ? mMin.x : other.mMin.x,
                                       yless ? mMin.y : other.mMin.y,
                                       zless ? mMin.z : other.mMin.z,
                                       (float)(xmore ? (xless ? mDiag.x : otherMax.x - mMin.x) : (xless ? thisMax.x - other.mMin.x : other.mDiag.x)),
                                       (float)(ymore ? (yless ? mDiag.y : otherMax.y - mMin.y) : (yless ? thisMax.y - other.mMin.y : other.mDiag.y)),
                                       (float)(zmore ? (zless ? mDiag.z : otherMax.z - mMin.z) : (zless ? thisMax.z - other.mMin.z : other.mDiag.z)));
        }

    } // struct BoundingBox




    public struct BoundingSphere3f
    {
        Vector3f mCenter;
        float mRadius;
        public BoundingSphere3f(float x, float y, float z, float r)
        {
            mCenter = new Vector3f(x, y, z);
            mRadius = r;
        }
        public BoundingSphere3f(Vector3f center, float radius)
        {
            mCenter = center;
            mRadius = radius;
        }
        public BoundingSphere3f(BoundingSphere3f cpy, float scale)
        {
            mCenter = cpy.mCenter;
            mRadius = cpy.mRadius * scale;
        }
        public Vector3f Center
        {
            get
            {
                return new Vector3f(mCenter.x, mCenter.y, mCenter.z);
            }
        }
        public float Radius
        {
            get
            {
                return mRadius;
            }
        }

        public override string ToString()
        {
            return "[" + this.Center.ToString() + " : " + this.Radius.ToString() + "]";
        }
    } // struct BoundingSphere3f



    public struct BoundingSphere3d
    {
        Vector3d mCenter;
        float mRadius;
        public BoundingSphere3d(double x, double y, double z, float r)
        {
            mCenter.x = x;
            mCenter.y = y;
            mCenter.z = z;
            mRadius = r;
        }
        public BoundingSphere3d(Vector3d center, float radius)
        {
            mCenter = center;
            mRadius = radius;
        }
        public BoundingSphere3d(BoundingSphere3d cpy, float scale)
        {
            mCenter = cpy.mCenter;
            mRadius = cpy.mRadius * scale;
        }
        public Vector3d Center
        {
            get
            {
                return new Vector3d(mCenter.x, mCenter.y, mCenter.z);
            }
        }
        public float Radius
        {
            get
            {
                return mRadius;
            }
        }

        public override string ToString()
        {
            return "[" + this.Center.ToString() + " : " + this.Radius.ToString() + "]";
        }
    } // struct BoundingSphere3f

    public struct UUID
    {
        ulong mLowOrderBytes;
        ulong mHighOrderBytes;


        static ulong SetUUIDlow(Google.ProtocolBuffers.ByteString data, int offset)
        {
            ulong LowOrderBytes = 0;
            int shiftVal = 0;
            for (int i = 0; i < 8; ++i)
            {
                ulong temp = data[i];
                LowOrderBytes |= (temp << shiftVal);
                shiftVal += 8;
            }
            return LowOrderBytes;
        }
        static ulong SetUUIDhigh(Google.ProtocolBuffers.ByteString data)
        {
            return SetUUIDlow(data, 8);
        }
        static ulong SetUUIDlow(byte[] data, int offset)
        {
            ulong LowOrderBytes = 0;
            int shiftVal = 0;
            for (int i = 0; i < 8; ++i)
            {
                ulong temp = data[i];
                LowOrderBytes |= (temp << shiftVal);
                shiftVal += 8;
            }
            return LowOrderBytes;
        }
        static ulong SetUUIDhigh(byte[] data)
        {
            return SetUUIDlow(data, 8);
        }
        public bool SetUUID(byte[] data)
        {
            if (data.Length == 16)
            {
                mLowOrderBytes = 0;
                mHighOrderBytes = 0;
                mLowOrderBytes = SetUUIDlow(data, 0);
                mHighOrderBytes = SetUUIDlow(data, 8);
                return true;
            }
            else
            {
                return false;
            }
        }
        public byte[] GetUUID()
        {
            byte[] data = new byte[16];
            int shiftVal = 0;
            for (int i = 0; i < 8; ++i)
            {
                ulong temp = 0xff;
                temp = (mLowOrderBytes & (temp << shiftVal));
                temp = (temp >> shiftVal);
                data[i] = (byte)temp;
                shiftVal += 8;
            }
            shiftVal = 0;
            for (int i = 8; i < 16; ++i)
            {
                ulong temp = 0xff;
                temp = (mHighOrderBytes & (temp << shiftVal));
                temp = (temp >> shiftVal);
                data[i] = (byte)temp;
                shiftVal += 8;
            }
            return data;
        }

        public static UUID Empty = new UUID(new byte[16]);
        public UUID(byte[] data)
        {
            if (data.Length != 16)
            {
                throw new System.ArgumentException("UUIDs must be provided 16 bytes");
            }
            mLowOrderBytes = SetUUIDlow(data, 0);
            mHighOrderBytes = SetUUIDhigh(data);
        }
        public UUID(Google.ProtocolBuffers.ByteString data)
        {
            if (data.Length != 16)
            {
                throw new System.ArgumentException("UUIDs must be provided 16 bytes");
            }
            mLowOrderBytes = SetUUIDlow(data, 0);
            mHighOrderBytes = SetUUIDhigh(data);
        }

    }


    public struct SHA256
    {
        ulong mLowOrderBytes;
        ulong mLowMediumOrderBytes;
        ulong mMediumHighOrderBytes;
        ulong mHighOrderBytes;


        static ulong SetLMH(Google.ProtocolBuffers.ByteString data, int offset)
        {
            ulong LowOrderBytes = 0;
            int shiftVal = 0;
            for (int i = 0; i < 8; ++i)
            {
                ulong temp = data[i];
                LowOrderBytes |= (temp << shiftVal);
                shiftVal += 8;
            }
            return LowOrderBytes;
        }
        static ulong SetLow(Google.ProtocolBuffers.ByteString data)
        {
            return SetLMH(data, 0);
        }
        static ulong SetLowMedium(Google.ProtocolBuffers.ByteString data)
        {
            return SetLMH(data, 8);
        }
        static ulong SetMediumHigh(Google.ProtocolBuffers.ByteString data)
        {
            return SetLMH(data, 16);
        }
        static ulong SetHigh(Google.ProtocolBuffers.ByteString data)
        {
            return SetLMH(data, 24);
        }
        static ulong SetLMH(byte[] data, int offset)
        {
            ulong LowOrderBytes = 0;
            int shiftVal = 0;
            for (int i = 0; i < 8; ++i)
            {
                ulong temp = data[i];
                LowOrderBytes |= (temp << shiftVal);
                shiftVal += 8;
            }
            return LowOrderBytes;
        }
        static ulong SetLow(byte[] data)
        {
            return SetLMH(data, 0);
        }
        static ulong SetLowMedium(byte[] data)
        {
            return SetLMH(data, 8);
        }
        static ulong SetMediumHigh(byte[] data)
        {
            return SetLMH(data, 16);
        }
        static ulong SetHigh(byte[] data)
        {
            return SetLMH(data, 24);
        }
        public bool SetSHA256(byte[] data)
        {
            if (data.Length == 32)
            {
                mLowOrderBytes = SetLow(data);
                mLowMediumOrderBytes = SetLowMedium(data);
                mMediumHighOrderBytes = SetMediumHigh(data);
                mHighOrderBytes = SetHigh(data);
                return true;
            }
            else
            {
                return false;
            }
        }
        public byte[] GetBinaryData()
        {
            byte[] data = new byte[32];
            int shiftVal = 0;
            for (int i = 0; i < 8; ++i)
            {
                ulong temp = 0xff;
                temp = (mLowOrderBytes & (temp << shiftVal));
                temp = (temp >> shiftVal);
                data[i] = (byte)temp;
                shiftVal += 8;
            }
            shiftVal = 0;
            for (int i = 8; i < 16; ++i)
            {
                ulong temp = 0xff;
                temp = (mLowMediumOrderBytes & (temp << shiftVal));
                temp = (temp >> shiftVal);
                data[i] = (byte)temp;
                shiftVal += 8;
            }
            shiftVal = 0;
            for (int i = 16; i < 24; ++i)
            {
                ulong temp = 0xff;
                temp = (mMediumHighOrderBytes & (temp << shiftVal));
                temp = (temp >> shiftVal);
                data[i] = (byte)temp;
                shiftVal += 8;
            }
            shiftVal = 0;
            for (int i = 24; i < 32; ++i)
            {
                ulong temp = 0xff;
                temp = (mHighOrderBytes & (temp << shiftVal));
                temp = (temp >> shiftVal);
                data[i] = (byte)temp;
                shiftVal += 8;
            }
            return data;
        }

        public static SHA256 Empty = new SHA256(new byte[32]);
        public SHA256(byte[] data)
        {
            if (data.Length != 32)
            {
                throw new System.ArgumentException("SHA256s must be provided 32 bytes");
            }
            mLowOrderBytes = SetLow(data);
            mLowMediumOrderBytes = SetLowMedium(data);
            mMediumHighOrderBytes = SetMediumHigh(data);
            mHighOrderBytes = SetHigh(data);
        }
        public SHA256(Google.ProtocolBuffers.ByteString data)
        {
            if (data.Length != 32)
            {
                throw new System.ArgumentException("SHA256s must be provided 32 bytes");
            }
            mLowOrderBytes = SetLow(data);
            mLowMediumOrderBytes = SetLowMedium(data);
            mMediumHighOrderBytes = SetMediumHigh(data);
            mHighOrderBytes = SetHigh(data);
        }

    }




    public struct Time
    {
        ulong usec;
        public Time(ulong usec_since_epoch)
        {
            usec = usec_since_epoch;
        }
        public ulong toMicro()
        {
            return usec;
        }
    }
    public class Duration
    {
        long usec;
        public Duration(long time_since)
        {
            usec = time_since;
        }
        public long toMicro()
        {
            return usec;
        }
    }

    class _PBJ
    {

        public static bool ValidateBool(bool d)
        {
            return true;
        }
        public static bool ValidateDouble(double d)
        {
            return true;
        }
        public static bool ValidateFloat(float d)
        {
            return true;
        }
        public static bool ValidateUint64(ulong d)
        {
            return true;
        }
        public static bool ValidateUint32(uint d)
        {
            return true;
        }
        public static bool ValidateUint16(ushort d)
        {
            return true;
        }
        public static bool ValidateUint8(byte d)
        {
            return true;
        }
        public static bool ValidateInt64(long d)
        {
            return true;
        }
        public static bool ValidateInt32(int d)
        {
            return true;
        }
        public static bool ValidateInt16(short d)
        {
            return true;
        }
        public static bool ValidateInt8(sbyte d)
        {
            return true;
        }
        public static bool ValidateString<S>(S input)
        {
            return true;
        }
        public static bool ValidateBytes<B>(B input)
        {
            return true;
        }
        public static bool ValidateUuid(Google.ProtocolBuffers.ByteString input)
        {
            return input.Length == 16;
        }
        public static bool ValidateSha256(Google.ProtocolBuffers.ByteString input)
        {
            return input.Length == 32;
        }
        public static bool ValidateAngle(float input)
        {
            return input >= 0 && input <= 3.1415926536 * 2.0;
        }
        public static bool ValidateTime(ulong input)
        {
            return true;
        }
        public static bool ValidateDuration(long input)
        {
            return true;
        }
        public static bool ValidateFlags(ulong input, ulong verification)
        {
            return (input & verification) == input;
        }




        public static bool CastBool(bool d)
        {
            return d;
        }
        public static double CastDouble(double d)
        {
            return d;
        }
        public static float CastFloat(float d)
        {
            return d;
        }
        public static ulong CastUint64(ulong d)
        {
            return d;
        }
        public static uint CastUint32(uint d)
        {
            return d;
        }
        public static ushort CastUint16(ushort d)
        {
            return d;
        }
        public static byte CastUint8(byte d)
        {
            return d;
        }
        public static long CastInt64(long d)
        {
            return d;
        }
        public static int CastInt32(int d)
        {
            return d;
        }
        public static short CastInt16(short d)
        {
            return d;
        }
        public static sbyte CastInt8(sbyte d)
        {
            return d;
        }
        public static S CastString<S>(S input)
        {
            return input;
        }
        public static B CastBytes<B>(B input)
        {
            return input;
        }



        public static bool CastBool()
        {
            return false;
        }
        public static double CastDouble()
        {
            return 0;
        }
        public static float CastFloat()
        {
            return 0;
        }
        public static ulong CastUint64()
        {
            return 0;
        }
        public static uint CastUint32()
        {
            return 0;
        }
        public static ushort CastUint16()
        {
            return 0;
        }
        public static byte CastUint8()
        {
            return 0;
        }
        public static long CastInt64()
        {
            return 0;
        }
        public static int CastInt32()
        {
            return 0;
        }
        public static short CastInt16()
        {
            return 0;
        }
        public static sbyte CastInt8()
        {
            return 0;
        }
        public static string CastString()
        {
            return "";
        }
        public static Google.ProtocolBuffers.ByteString CastBytes()
        {
            return Google.ProtocolBuffers.ByteString.Empty;
        }


        public static ulong CastFlags(ulong data, ulong allFlagsOn)
        {
            return allFlagsOn & data;
        }
        public static ulong CastFlags(ulong allFlagsOn)
        {
            return 0;
        }

        public static Vector3f CastNormal(float x, float y)
        {
            float neg = (x > 1.5f || y > 1.5f) ? -1.0f : 1.0f;
            if (x > 1.5)
                x -= 3;
            if (y > 1.5)
                y -= 3;
            return new Vector3f(x, y, neg - neg * (float)Math.Sqrt(x * x + y * y));
        }
        public static Vector3f CastNormal()
        {
            return new Vector3f(0, 1, 0);
        }


        public static Vector2f CastVector2f(float x, float y)
        {
            return new Vector2f(x, y);
        }
        public static Vector2f CastVector2f()
        {
            return new Vector2f(0, 0);
        }

        public static Vector3f CastVector3f(float x, float y, float z)
        {
            return new Vector3f(x, y, z);
        }
        public static Vector3f CastVector3f()
        {
            return new Vector3f(0, 0, 0);
        }

        public static Vector4f CastVector4f(float x, float y, float z, float w)
        {
            return new Vector4f(x, y, z, w);
        }
        public static Vector4f CastVector4f()
        {
            return new Vector4f(0, 0, 0, 0);
        }
        public static Vector2d CastVector2d(double x, double y)
        {
            return new Vector2d(x, y);
        }
        public static Vector2d CastVector2d()
        {
            return new Vector2d(0, 0);
        }

        public static Vector3d CastVector3d(double x, double y, double z)
        {
            return new Vector3d(x, y, z);
        }
        public static Vector3d CastVector3d()
        {
            return new Vector3d(0, 0, 0);
        }

        public static Vector4d CastVector4d(double x, double y, double z, double w)
        {
            return new Vector4d(x, y, z, w);
        }
        public static Vector4d CastVector4d()
        {
            return new Vector4d(0, 0, 0, 0);
        }

        public static BoundingSphere3f CastBoundingsphere3f(float x, float y, float z, float r)
        {
            return new BoundingSphere3f(new Vector3f(x, y, z), r);
        }
        public static BoundingSphere3d CastBoundingsphere3d(double x, double y, double z, double r)
        {
            return new BoundingSphere3d(new Vector3d(x, y, z), (float)r);
        }

        public static BoundingSphere3f CastBoundingsphere3f()
        {
            return new BoundingSphere3f(new Vector3f(0, 0, 0), 0);
        }
        public static BoundingSphere3d CastBoundingsphere3d()
        {
            return new BoundingSphere3d(new Vector3d(0, 0, 0), (float)0);
        }


        public static BoundingBox3f3f CastBoundingbox3f3f(float x, float y, float z, float dx, float dy, float dz)
        {
            return new BoundingBox3f3f(x, y, z, dx, dy, dz);
        }
        public static BoundingBox3d3f CastBoundingbox3d3f(double x, double y, double z, double dx, double dy, double dz)
        {
            return new BoundingBox3d3f(x, y, z, (float)dx, (float)dy, (float)dz);
        }

        public static BoundingBox3f3f CastBoundingbox3f3f()
        {
            return new BoundingBox3f3f(new Vector3f(0, 0, 0), new Vector3f(0, 0, 0));
        }
        public static BoundingBox3d3f CastBoundingbox3d3f()
        {
            return new BoundingBox3d3f(0, 0, 0, 0, 0, 0);
        }



        public static Quaternion CastQuaternion(float x, float y, float z)
        {
            float neg = (x > 1.5 || y > 1.5 || z > 1.5) ? -1.0f : 1.0f;
            if (x > 1.5)
                x -= 3.0f;
            if (y > 1.5)
                y -= 3.0f;
            if (z > 1.5)
                z -= 3.0f;
            return new Quaternion(neg - neg * (float)Math.Sqrt(x * x + y * y + z * z), x, y, z);
        }
        public static Quaternion CastQuaternion()
        {
            return new Quaternion(1, 0, 0, 0);
        }

        public static UUID CastUuid(Google.ProtocolBuffers.ByteString input)
        {
            return new UUID(input);
        }
        public static SHA256 CastSha256(Google.ProtocolBuffers.ByteString input)
        {
            return new SHA256(input);
        }
        public static SHA256 CastSha256()
        {
            return SHA256.Empty;
        }
        public static UUID CastUuid()
        {
            return UUID.Empty;
        }

        public static float CastAngle(float d)
        {
            return d;
        }
        public static float CastAngle()
        {
            return 0;
        }

        public static Time CastTime(ulong t)
        {
            return new Time(t);
        }
        public static Time CastTime()
        {
            return new Time(0);
        }
        public static Duration CastDuration(long t)
        {
            return new Duration(t);
        }
        public static Duration CastDuration()
        {
            return new Duration(0);
        }

        public static T Construct<T>(T retval)
        {
            return retval;
        }
        public static long Construct(Duration d)
        {
            return d.toMicro();
        }
        public static ulong Construct(Time t)
        {
            return t.toMicro();
        }
        public static Google.ProtocolBuffers.ByteString Construct(UUID u)
        {
            byte[] data = u.GetUUID();
            Google.ProtocolBuffers.ByteString retval = Google.ProtocolBuffers.ByteString.CopyFrom(data, 0, 16);
            return retval;
        }
        public static Google.ProtocolBuffers.ByteString Construct(SHA256 u)
        {
            byte[] data = u.GetBinaryData();
            Google.ProtocolBuffers.ByteString retval = Google.ProtocolBuffers.ByteString.CopyFrom(data, 0, 16);
            return retval;
        }
        public static float[] ConstructNormal(Vector3f d)
        {
            return new float[] { d.x + (d.z < 0 ? 3.0f : 0.0f), d.y };
        }
        public static float[] ConstructQuaternion(Quaternion d)
        {
            return new float[] { d.x + (d.w < 0 ? 3.0f : 0.0f), d.y, d.z };
        }

        public static float[] ConstructVector2f(Vector2f d)
        {
            return new float[] { d.x, d.y };
        }
        public static double[] ConstructVector2d(Vector2d d)
        {
            return new double[] { d.x, d.y };
        }

        public static float[] ConstructVector3f(Vector3f d)
        {
            return new float[] { d.x, d.y, d.z };
        }
        public static double[] ConstructVector3d(Vector3d d)
        {
            return new double[] { d.x, d.y, d.z };
        }
        public static float[] ConstructVector4f(Vector4f d)
        {
            return new float[] { d.x, d.y, d.z, d.w };
        }
        public static double[] ConstructVector4d(Vector4d d)
        {
            return new double[] { d.x, d.y, d.z, d.w };
        }


        public static float[] ConstructBoundingsphere3f(BoundingSphere3f d)
        {
            return new float[] { d.Center.x, d.Center.y, d.Center.z, d.Radius };
        }
        public static double[] ConstructBoundingsphere3d(BoundingSphere3d d)
        {
            return new double[] { d.Center.x, d.Center.y, d.Center.z, d.Radius };
        }

        public static float[] ConstructBoundingbox3f3f(BoundingBox3f3f d)
        {
            return new float[] { d.Min.x, d.Min.y, d.Min.z, d.Diag.x, d.Diag.y, d.Diag.z };
        }
        public static double[] ConstructBoundingbox3d3f(BoundingBox3d3f d)
        {
            return new double[] { d.Min.x, d.Min.y, d.Min.z, d.Diag.x, d.Diag.y, d.Diag.z };
        }


    }

}
