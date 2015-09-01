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

namespace OpenSim.Framework
{
    public class ConfigurationOption
    {
        #region Delegates

        public delegate bool ConfigurationOptionShouldBeAsked(string configuration_key);

        #endregion

        #region ConfigurationTypes enum

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
            TYPE_UUID,
            TYPE_UUID_NULL_FREE,
            TYPE_Vector3,
            TYPE_FLOAT,
            TYPE_DOUBLE
        } ;

        #endregion

        public string configurationDefault = String.Empty;

        public string configurationKey = String.Empty;
        public string configurationQuestion = String.Empty;

        public ConfigurationTypes configurationType = ConfigurationTypes.TYPE_STRING;
        public bool configurationUseDefaultNoPrompt = false;
        public ConfigurationOptionShouldBeAsked shouldIBeAsked; //Should I be asked now? Based on previous answers
    }
}
