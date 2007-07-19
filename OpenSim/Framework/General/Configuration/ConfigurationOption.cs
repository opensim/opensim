using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Configuration
{
    public class ConfigurationOption
    {
        public enum ConfigurationTypes
        {
            TYPE_STRING,
            TYPE_STRING_NOT_EMPTY,
            TYPE_UINT16,
            TYPE_UINT32,
            TYPE_UINT64, 
            TYPE_INT16,
            TYPE_INT32,
            TYPE_INT64,
            TYPE_IP_ADDRESS,
            TYPE_CHARACTER,
            TYPE_BOOLEAN,
            TYPE_BYTE,
            TYPE_LLUUID,
            TYPE_LLVECTOR3,
            TYPE_FLOAT,
            TYPE_DOUBLE
        };

        public string configurationKey = "";
        public string configurationQuestion = "";
        public string configurationDefault = "";

        public ConfigurationTypes configurationType = ConfigurationTypes.TYPE_STRING;
        public bool configurationUseDefaultNoPrompt = false;
    }
}
