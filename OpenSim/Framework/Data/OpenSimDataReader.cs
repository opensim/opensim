using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using libsecondlife;
using TribalMedia.Framework.Data;

namespace OpenSim.Framework.Data
{
    public class OpenSimDataReader : DataReader
    {
        public OpenSimDataReader(IDataReader source) : base(source)
        {
        }

        public LLVector3 GetVector(string s)
        {
            float x = GetFloat(s + "X");
            float y = GetFloat(s + "Y");
            float z = GetFloat(s + "Z");

            LLVector3 vector = new LLVector3(x, y, z);

            return vector;
        }

        public LLQuaternion GetQuaternion(string s)
        {
            float x = GetFloat(s + "X");
            float y = GetFloat(s + "Y");
            float z = GetFloat(s + "Z");
            float w = GetFloat(s + "W");

            LLQuaternion quaternion = new LLQuaternion(x, y, z, w);

            return quaternion;
        }
    }
}
