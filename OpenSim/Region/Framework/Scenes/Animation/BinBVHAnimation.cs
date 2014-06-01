/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.IO;
using OpenMetaverse;

namespace OpenSim.Region.Framework.Scenes.Animation
{
    /// <summary>
    /// Written to decode and encode a binary animation asset.
    /// The SecondLife Client reads in a BVH file and converts 
    /// it to the format described here.  This isn't 
    /// </summary>
    public class BinBVHAnimation
    {
        /// <summary>
        /// Rotation Keyframe count (used internally)
        /// Don't use this, use the rotationkeys.Length on each joint
        /// </summary>
        private int rotationkeys;

        /// <summary>
        /// Position Keyframe count (used internally)
        /// Don't use this, use the positionkeys.Length on each joint
        /// </summary>
        private int positionkeys;

        public UInt16 unknown0; // Always 1
        public UInt16 unknown1; // Always 0

        /// <summary>
        /// Animation Priority
        /// </summary>
        public int Priority;

        /// <summary>
        /// The animation length in seconds.
        /// </summary>
        public Single Length;

        /// <summary>
        /// Expression set in the client.  Null if [None] is selected
        /// </summary>
        public string ExpressionName; // "" (null)

        /// <summary>
        /// The time in seconds to start the animation
        /// </summary>
        public Single InPoint;

        /// <summary>
        /// The time in seconds to end the animation
        /// </summary>
        public Single OutPoint;

        /// <summary>
        /// Loop the animation
        /// </summary>
        public bool Loop;

        /// <summary>
        /// Meta data. Ease in Seconds.
        /// </summary>
        public Single EaseInTime;

        /// <summary>
        /// Meta data. Ease out seconds.
        /// </summary>
        public Single EaseOutTime;

        /// <summary>
        /// Meta Data for the Hand Pose
        /// </summary>
        public uint HandPose;

        /// <summary>
        /// Number of joints defined in the animation
        /// Don't use this..  use joints.Length
        /// </summary>
        private uint m_jointCount;


        /// <summary>
        /// Contains an array of joints
        /// </summary>
        public binBVHJoint[] Joints;
        

        public byte[] ToBytes()
        {
            byte[] outputbytes;

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter iostream = new BinaryWriter(ms))
            {
                iostream.Write(BinBVHUtil.ES(Utils.UInt16ToBytes(unknown0)));
                iostream.Write(BinBVHUtil.ES(Utils.UInt16ToBytes(unknown1)));
                iostream.Write(BinBVHUtil.ES(Utils.IntToBytes(Priority)));
                iostream.Write(BinBVHUtil.ES(Utils.FloatToBytes(Length)));
                iostream.Write(BinBVHUtil.WriteNullTerminatedString(ExpressionName));
                iostream.Write(BinBVHUtil.ES(Utils.FloatToBytes(InPoint)));
                iostream.Write(BinBVHUtil.ES(Utils.FloatToBytes(OutPoint)));
                iostream.Write(BinBVHUtil.ES(Utils.IntToBytes(Loop ? 1 : 0)));
                iostream.Write(BinBVHUtil.ES(Utils.FloatToBytes(EaseInTime)));
                iostream.Write(BinBVHUtil.ES(Utils.FloatToBytes(EaseOutTime)));
                iostream.Write(BinBVHUtil.ES(Utils.UIntToBytes(HandPose)));
                iostream.Write(BinBVHUtil.ES(Utils.UIntToBytes((uint)(Joints.Length))));

                for (int i = 0; i < Joints.Length; i++)
                {
                    Joints[i].WriteBytesToStream(iostream, InPoint, OutPoint);
                }
                iostream.Write(BinBVHUtil.ES(Utils.IntToBytes(0)));
            
                using (MemoryStream ms2 = (MemoryStream)iostream.BaseStream)
                    outputbytes = ms2.ToArray();
            }

            return outputbytes;
        }
        
        public BinBVHAnimation()
        {
            rotationkeys = 0;
            positionkeys = 0;
            unknown0 = 1;
            unknown1 = 0;
            Priority = 1;
            Length = 0;
            ExpressionName = string.Empty;
            InPoint = 0;
            OutPoint = 0;
            Loop = false;
            EaseInTime = 0;
            EaseOutTime = 0;
            HandPose = 1;
            m_jointCount = 0;
            
            Joints = new binBVHJoint[1];
            Joints[0] = new binBVHJoint();
            Joints[0].Name = "mPelvis";
            Joints[0].Priority = 7;
            Joints[0].positionkeys = new binBVHJointKey[1];
            Joints[0].rotationkeys = new binBVHJointKey[1];
            Random rnd = new Random();

            Joints[0].rotationkeys[0] = new binBVHJointKey();
            Joints[0].rotationkeys[0].time = (0f);
            Joints[0].rotationkeys[0].key_element.X = ((float)rnd.NextDouble() * 2 - 1);
            Joints[0].rotationkeys[0].key_element.Y = ((float)rnd.NextDouble() * 2 - 1);
            Joints[0].rotationkeys[0].key_element.Z = ((float)rnd.NextDouble() * 2 - 1);

            Joints[0].positionkeys[0] = new binBVHJointKey();
            Joints[0].positionkeys[0].time = (0f);
            Joints[0].positionkeys[0].key_element.X = ((float)rnd.NextDouble() * 2 - 1);
            Joints[0].positionkeys[0].key_element.Y = ((float)rnd.NextDouble() * 2 - 1);
            Joints[0].positionkeys[0].key_element.Z = ((float)rnd.NextDouble() * 2 - 1);
            

        }

        public BinBVHAnimation(byte[] animationdata)
        {
            int i = 0;
            if (!BitConverter.IsLittleEndian)
            {
                unknown0 = Utils.BytesToUInt16(BinBVHUtil.EndianSwap(animationdata,i,2)); i += 2; // Always 1
                unknown1 = Utils.BytesToUInt16(BinBVHUtil.EndianSwap(animationdata, i, 2)); i += 2; // Always 0
                Priority = Utils.BytesToInt(BinBVHUtil.EndianSwap(animationdata, i, 4)); i += 4;
                Length = Utils.BytesToFloat(BinBVHUtil.EndianSwap(animationdata, i, 4), 0); i += 4;
            }
            else
            {
                unknown0 = Utils.BytesToUInt16(animationdata, i); i += 2; // Always 1
                unknown1 = Utils.BytesToUInt16(animationdata, i); i += 2; // Always 0
                Priority = Utils.BytesToInt(animationdata, i); i += 4;
                Length = Utils.BytesToFloat(animationdata, i); i += 4;
            }
            ExpressionName = ReadBytesUntilNull(animationdata, ref i);
            if (!BitConverter.IsLittleEndian)
            {
                InPoint = Utils.BytesToFloat(BinBVHUtil.EndianSwap(animationdata, i, 4), 0); i += 4;
                OutPoint = Utils.BytesToFloat(BinBVHUtil.EndianSwap(animationdata, i, 4), 0); i += 4;
                Loop = (Utils.BytesToInt(BinBVHUtil.EndianSwap(animationdata, i, 4)) != 0); i += 4;
                EaseInTime = Utils.BytesToFloat(BinBVHUtil.EndianSwap(animationdata, i, 4), 0); i += 4;
                EaseOutTime = Utils.BytesToFloat(BinBVHUtil.EndianSwap(animationdata, i, 4), 0); i += 4;
                HandPose = Utils.BytesToUInt(BinBVHUtil.EndianSwap(animationdata, i, 4)); i += 4; // Handpose?

                m_jointCount = Utils.BytesToUInt(animationdata, i); i += 4; // Get Joint count
            }
            else
            {
                InPoint = Utils.BytesToFloat(animationdata, i); i += 4;
                OutPoint = Utils.BytesToFloat(animationdata, i); i += 4;
                Loop = (Utils.BytesToInt(animationdata, i) != 0); i += 4;
                EaseInTime = Utils.BytesToFloat(animationdata, i); i += 4;
                EaseOutTime = Utils.BytesToFloat(animationdata, i); i += 4;
                HandPose = Utils.BytesToUInt(animationdata, i); i += 4; // Handpose?

                m_jointCount = Utils.BytesToUInt(animationdata, i); i += 4; // Get Joint count
            }
            Joints = new binBVHJoint[m_jointCount];

            // deserialize the number of joints in the animation.
            // Joints are variable length blocks of binary data consisting of joint data and keyframes
            for (int iter = 0; iter < m_jointCount; iter++)
            {
                binBVHJoint joint = readJoint(animationdata, ref i);
                Joints[iter] = joint;
            }
        }

        
        /// <summary>
        /// Variable length strings seem to be null terminated in the animation asset..    but..
        /// use with caution, home grown.
        /// advances the index.
        /// </summary>
        /// <param name="data">The animation asset byte array</param>
        /// <param name="i">The offset to start reading</param>
        /// <returns>a string</returns>
        private static string ReadBytesUntilNull(byte[] data, ref int i)
        {
            char nterm = '\0'; // Null terminator
            int endpos = i;
            int startpos = i;

            // Find the null character
            for (int j = i; j < data.Length; j++)
            {
                char spot = Convert.ToChar(data[j]);
                if (spot == nterm)
                {
                    endpos = j;
                    break;
                }
            }

            // if we got to the end, then it's a zero length string
            if (i == endpos)
            {
                // advance the 1 null character
                i++;
                return string.Empty;
            }
            else
            {
                // We found the end of the string
                // append the bytes from the beginning of the string to the end of the string
                // advance i
                byte[] interm = new byte[endpos-i];
                for (; i<endpos; i++)
                {
                    interm[i-startpos] = data[i];
                }
                i++;  // advance past the null character

                return Utils.BytesToString(interm);
            }
        }

        /// <summary>
        /// Read in a Joint from an animation asset byte array
        /// Variable length Joint fields, yay!
        /// Advances the index
        /// </summary>
        /// <param name="data">animation asset byte array</param>
        /// <param name="i">Byte Offset of the start of the joint</param>
        /// <returns>The Joint data serialized into the binBVHJoint structure</returns>
        private binBVHJoint readJoint(byte[] data, ref int i)
        {
           
            binBVHJointKey[] positions;
            binBVHJointKey[] rotations;

            binBVHJoint pJoint = new binBVHJoint();

            /*
                109
                84
                111
                114
                114
                111
                0 <--- Null terminator
            */

            pJoint.Name = ReadBytesUntilNull(data, ref i); // Joint name

            /* 
                 2 <- Priority Revisited
                 0
                 0
                 0
            */

            /* 
                5 <-- 5 keyframes
                0
                0
                0
                ... 5 Keyframe data blocks
            */

            /* 
                2 <-- 2 keyframes
                0
                0
                0
                ..  2 Keyframe data blocks
            */
            if (!BitConverter.IsLittleEndian)
            {
                pJoint.Priority = Utils.BytesToInt(BinBVHUtil.EndianSwap(data, i, 4)); i += 4; // Joint Priority override?
                rotationkeys = Utils.BytesToInt(BinBVHUtil.EndianSwap(data, i, 4)); i += 4; // How many rotation keyframes
            }
            else
            {
                pJoint.Priority = Utils.BytesToInt(data, i); i += 4; // Joint Priority override?
                rotationkeys = Utils.BytesToInt(data, i); i += 4; // How many rotation keyframes
            }

            // argh! floats into two bytes!..   bad bad bad bad
            // After fighting with it for a while..  -1, to 1 seems to give the best results
            rotations = readKeys(data, ref i, rotationkeys, -1f, 1f);
            for (int iter = 0; iter < rotations.Length; iter++)
            {
                rotations[iter].W = 1f -
                    (rotations[iter].key_element.X + rotations[iter].key_element.Y +
                     rotations[iter].key_element.Z);
            }


            if (!BitConverter.IsLittleEndian)
            {
                positionkeys = Utils.BytesToInt(BinBVHUtil.EndianSwap(data, i, 4)); i += 4; // How many position keyframes
            }
            else
            {
                positionkeys = Utils.BytesToInt(data, i); i += 4; // How many position keyframes
            }

            // Read in position keyframes
            // argh! more floats into two bytes!..  *head desk*
            // After fighting with it for a while..  -5, to 5 seems to give the best results
            positions = readKeys(data, ref i, positionkeys, -5f, 5f);

            pJoint.rotationkeys = rotations;
            pJoint.positionkeys = positions;

            return pJoint;
        }

        /// <summary>
        /// Read Keyframes of a certain type
        /// advance i
        /// </summary>
        /// <param name="data">Animation Byte array</param>
        /// <param name="i">Offset in the Byte Array.  Will be advanced</param>
        /// <param name="keycount">Number of Keyframes</param>
        /// <param name="min">Scaling Min to pass to the Uint16ToFloat method</param>
        /// <param name="max">Scaling Max to pass to the Uint16ToFloat method</param>
        /// <returns></returns>
        private binBVHJointKey[] readKeys(byte[] data, ref int i, int keycount, float min, float max)
        {
            float x;
            float y;
            float z;

            /*
                0.o, Float values in Two bytes.. this is just wrong >:(
                17          255  <-- Time Code
                17          255  <-- Time Code
                255         255  <-- X
                127         127  <-- X
                255         255  <-- Y
                127         127  <-- Y
                213         213  <-- Z
                142         142  <---Z

            */

            binBVHJointKey[] m_keys = new binBVHJointKey[keycount];
            for (int j = 0; j < keycount; j++)
            {
                binBVHJointKey pJKey = new binBVHJointKey();
                if (!BitConverter.IsLittleEndian)
                {
                    pJKey.time = Utils.UInt16ToFloat(BinBVHUtil.EndianSwap(data, i, 2), 0, InPoint, OutPoint); i += 2;
                    x = Utils.UInt16ToFloat(BinBVHUtil.EndianSwap(data, i, 2), 0, min, max); i += 2;
                    y = Utils.UInt16ToFloat(BinBVHUtil.EndianSwap(data, i, 2), 0, min, max); i += 2;
                    z = Utils.UInt16ToFloat(BinBVHUtil.EndianSwap(data, i, 2), 0, min, max); i += 2;
                }
                else
                {
                    pJKey.time = Utils.UInt16ToFloat(data, i, InPoint, OutPoint); i += 2;
                    x = Utils.UInt16ToFloat(data, i, min, max); i += 2;
                    y = Utils.UInt16ToFloat(data, i, min, max); i += 2;
                    z = Utils.UInt16ToFloat(data, i, min, max); i += 2;
                }
                pJKey.key_element = new Vector3(x, y, z);
                m_keys[j] = pJKey;
            }
            return m_keys;
        }
    
       

    }
    /// <summary>
    /// A Joint and it's associated meta data and keyframes
    /// </summary>
    public struct binBVHJoint
    {
        /// <summary>
        /// Name of the Joint.  Matches the avatar_skeleton.xml in client distros
        /// </summary>
        public string Name;

        /// <summary>
        /// Joint Animation Override?   Was the same as the Priority in testing.. 
        /// </summary>
        public int Priority;

        /// <summary>
        /// Array of Rotation Keyframes in order from earliest to latest
        /// </summary>
        public binBVHJointKey[] rotationkeys;

        /// <summary>
        /// Array of Position Keyframes in order from earliest to latest
        /// This seems to only be for the Pelvis?
        /// </summary>
        public binBVHJointKey[] positionkeys;



        public void WriteBytesToStream(BinaryWriter iostream, float InPoint, float OutPoint)
        {
            iostream.Write(BinBVHUtil.WriteNullTerminatedString(Name));
            iostream.Write(BinBVHUtil.ES(Utils.IntToBytes(Priority)));
            iostream.Write(BinBVHUtil.ES(Utils.IntToBytes(rotationkeys.Length)));
            for (int i=0;i<rotationkeys.Length;i++)
            {
                rotationkeys[i].WriteBytesToStream(iostream, InPoint, OutPoint,  -1f, 1f);
            }
            iostream.Write(BinBVHUtil.ES(Utils.IntToBytes((positionkeys.Length))));
            for (int i = 0; i < positionkeys.Length; i++)
            {
                positionkeys[i].WriteBytesToStream(iostream, InPoint, OutPoint, -256f, 256f);
            }
        }
    }

    /// <summary>
    /// A Joint Keyframe.  This is either a position or a rotation.
    /// </summary>
    public struct binBVHJointKey
    {
        // Time in seconds for this keyframe.
        public float time;

        /// <summary>
        /// Either a Vector3 position or a Vector3 Euler rotation
        /// </summary>
        public Vector3 key_element;

        public float W;

        public void WriteBytesToStream(BinaryWriter iostream, float InPoint, float OutPoint, float min, float max)
        {
            iostream.Write(BinBVHUtil.ES(Utils.UInt16ToBytes(BinBVHUtil.FloatToUInt16(time, InPoint, OutPoint))));
            iostream.Write(BinBVHUtil.ES(Utils.UInt16ToBytes(BinBVHUtil.FloatToUInt16(key_element.X, min, max))));
            iostream.Write(BinBVHUtil.ES(Utils.UInt16ToBytes(BinBVHUtil.FloatToUInt16(key_element.Y, min, max))));
            iostream.Write(BinBVHUtil.ES(Utils.UInt16ToBytes(BinBVHUtil.FloatToUInt16(key_element.Z, min, max))));
        }
    }

    /// <summary>
    /// Poses set in the animation metadata for the hands.
    /// </summary>
    public enum HandPose : uint
    {
        Spread = 0,
        Relaxed = 1,
        Point_Both = 2,
        Fist = 3,
        Relaxed_Left = 4,
        Point_Left = 5,
        Fist_Left = 6,
        Relaxed_Right = 7,
        Point_Right = 8,
        Fist_Right = 9,
        Salute_Right = 10,
        Typing = 11,
        Peace_Right = 12
    }
    public static class BinBVHUtil
    {
        public const float ONE_OVER_U16_MAX = 1.0f / UInt16.MaxValue;
       
        public static UInt16 FloatToUInt16(float val, float lower, float upper)
        {
            UInt16 uival = 0;
            //m_parentGroup.GetTimeDilation() * (float)ushort.MaxValue
            //0-1

//            float difference = upper - lower;
            // we're trying to get a zero lower and modify all values equally so we get a percentage position
            if (lower > 0)
            {
                upper -= lower;
                val = val - lower;
                
                // start with 500 upper and 200 lower..    subtract 200 from the upper and the value
            }
            else //if (lower < 0 && upper > 0)
            {
                // double negative, 0 minus negative 5 is 5.
                upper += 0 - lower;
                lower += 0 - lower;
                val += 0 - lower;
            }

            if (upper == 0)
                val = 0;
            else
            {
                val /= upper;
            }

            uival = (UInt16)(val * UInt16.MaxValue);

            return uival;
        }
        
        
        /// <summary>
        /// Endian Swap
        /// Swaps endianness if necessary
        /// </summary>
        /// <param name="arr">Input array</param>
        /// <returns></returns>
        public static byte[] ES(byte[] arr)
        {
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(arr);
            return arr;
        }
        public static byte[] EndianSwap(byte[] arr, int offset, int len)
        {
            byte[] bendian = new byte[offset + len];
            Buffer.BlockCopy(arr, offset, bendian, 0, len);
            Array.Reverse(bendian);
            return bendian;
        }

        public static byte[] WriteNullTerminatedString(string str)
        {
            byte[] output = new byte[str.Length + 1];
            Char[] chr = str.ToCharArray();
            int i = 0;
            for (i = 0; i < chr.Length; i++)
            {
                output[i] = Convert.ToByte(chr[i]);

            }
            
            output[i] = Convert.ToByte('\0');
            return output;
        }

    }
}
/*
switch (jointname)
                {
                    case "mPelvis":
                    case "mTorso":
                    case "mNeck":
                    case "mHead":
                    case "mChest":
                    case "mHipLeft":
                    case "mHipRight":
                    case "mKneeLeft":
                    case "mKneeRight":
                        // XYZ->ZXY
                        t = x;
                        x = y;
                        y = t;
                        break;
                    case "mCollarLeft":
                    case "mCollarRight":
                    case "mElbowLeft":
                    case "mElbowRight":
                        // YZX ->ZXY
                        t = z;
                        z = x;
                        x = y;
                        y = t;
                        break;
                    case "mWristLeft":
                    case "mWristRight":
                    case "mShoulderLeft":
                    case "mShoulderRight":
                        // ZYX->ZXY
                        t = y;
                        y = z;
                        z = t;

                        break;
                    case "mAnkleLeft":
                    case "mAnkleRight":
                        // XYZ ->ZXY
                        t = x;
                        x = z;
                        z = y;
                        y = t;
                        break;
                }
*/