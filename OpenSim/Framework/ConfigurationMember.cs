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
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Xml;
using log4net;
using OpenMetaverse;
//using OpenSim.Framework.Console;

namespace OpenSim.Framework
{
    public class ConfigurationMember
    {
        #region Delegates

        public delegate bool ConfigurationOptionResult(string configuration_key, object configuration_result);

        public delegate void ConfigurationOptionsLoad();

        #endregion

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private int cE = 0;

        private string configurationDescription = String.Empty;
        private string configurationFilename = String.Empty;
        private XmlNode configurationFromXMLNode = null;
        private List<ConfigurationOption> configurationOptions = new List<ConfigurationOption>();
        private IGenericConfig configurationPlugin = null;

        /// <summary>
        /// This is the default configuration DLL loaded
        /// </summary>
        private string configurationPluginFilename = "OpenSim.Framework.Configuration.XML.dll";

        private ConfigurationOptionsLoad loadFunction;
        private ConfigurationOptionResult resultFunction;

        private bool useConsoleToPromptOnError = true;

        public ConfigurationMember(string configuration_filename, string configuration_description,
                                   ConfigurationOptionsLoad load_function, ConfigurationOptionResult result_function, bool use_console_to_prompt_on_error)
        {
            configurationFilename = configuration_filename;
            configurationDescription = configuration_description;
            loadFunction = load_function;
            resultFunction = result_function;
            useConsoleToPromptOnError = use_console_to_prompt_on_error;
        }

        public ConfigurationMember(XmlNode configuration_xml, string configuration_description,
                                   ConfigurationOptionsLoad load_function, ConfigurationOptionResult result_function, bool use_console_to_prompt_on_error)
        {
            configurationFilename = String.Empty;
            configurationFromXMLNode = configuration_xml;
            configurationDescription = configuration_description;
            loadFunction = load_function;
            resultFunction = result_function;
            useConsoleToPromptOnError = use_console_to_prompt_on_error;
        }

        public void setConfigurationFilename(string filename)
        {
            configurationFilename = filename;
        }

        public void setConfigurationDescription(string desc)
        {
            configurationDescription = desc;
        }

        public void setConfigurationResultFunction(ConfigurationOptionResult result)
        {
            resultFunction = result;
        }

        public void forceConfigurationPluginLibrary(string dll_filename)
        {
            configurationPluginFilename = dll_filename;
        }

        private void checkAndAddConfigOption(ConfigurationOption option)
        {
            if ((option.configurationKey != String.Empty && option.configurationQuestion != String.Empty) ||
                (option.configurationKey != String.Empty && option.configurationUseDefaultNoPrompt))
            {
                if (!configurationOptions.Contains(option))
                {
                    configurationOptions.Add(option);
                }
            }
            else
            {
                m_log.Info(
                    "Required fields for adding a configuration option is invalid. Will not add this option (" +
                    option.configurationKey + ")");
            }
        }

        public void addConfigurationOption(string configuration_key,
                                           ConfigurationOption.ConfigurationTypes configuration_type,
                                           string configuration_question, string configuration_default,
                                           bool use_default_no_prompt)
        {
            ConfigurationOption configOption = new ConfigurationOption();
            configOption.configurationKey = configuration_key;
            configOption.configurationQuestion = configuration_question;
            configOption.configurationDefault = configuration_default;
            configOption.configurationType = configuration_type;
            configOption.configurationUseDefaultNoPrompt = use_default_no_prompt;
            configOption.shouldIBeAsked = null; //Assumes true, I can ask whenever
            checkAndAddConfigOption(configOption);
        }

        public void addConfigurationOption(string configuration_key,
                                           ConfigurationOption.ConfigurationTypes configuration_type,
                                           string configuration_question, string configuration_default,
                                           bool use_default_no_prompt,
                                           ConfigurationOption.ConfigurationOptionShouldBeAsked shouldIBeAskedDelegate)
        {
            ConfigurationOption configOption = new ConfigurationOption();
            configOption.configurationKey = configuration_key;
            configOption.configurationQuestion = configuration_question;
            configOption.configurationDefault = configuration_default;
            configOption.configurationType = configuration_type;
            configOption.configurationUseDefaultNoPrompt = use_default_no_prompt;
            configOption.shouldIBeAsked = shouldIBeAskedDelegate;
            checkAndAddConfigOption(configOption);
        }

        // TEMP - REMOVE
        public void performConfigurationRetrieve()
        {
            if (cE > 1)
                m_log.Error("READING CONFIGURATION COUT: " + cE.ToString());


            configurationPlugin = LoadConfigDll(configurationPluginFilename);
            configurationOptions.Clear();
            if (loadFunction == null)
            {
                m_log.Error("Load Function for '" + configurationDescription +
                            "' is null. Refusing to run configuration.");
                return;
            }

            if (resultFunction == null)
            {
                m_log.Error("Result Function for '" + configurationDescription +
                            "' is null. Refusing to run configuration.");
                return;
            }

            //m_log.Debug("[CONFIG]: Calling Configuration Load Function...");
            loadFunction();

            if (configurationOptions.Count <= 0)
            {
                m_log.Error("[CONFIG]: No configuration options were specified for '" + configurationOptions +
                            "'. Refusing to continue configuration.");
                return;
            }

            bool useFile = true;
            if (configurationPlugin == null)
            {
                m_log.Error("[CONFIG]: Configuration Plugin NOT LOADED!");
                return;
            }

            if (configurationFilename.Trim() != String.Empty)
            {
                configurationPlugin.SetFileName(configurationFilename);
                try
                {
                    configurationPlugin.LoadData();
                    useFile = true;
                }
                catch (XmlException e)
                {
                    m_log.WarnFormat("[CONFIG] Not using {0}: {1}",
                            configurationFilename,
                            e.Message.ToString());
                    //m_log.Error("Error loading " + configurationFilename + ": " + e.ToString());
                    useFile = false;
                }
            }
            else
            {
                if (configurationFromXMLNode != null)
                {
                    m_log.Info("Loading from XML Node, will not save to the file");
                    configurationPlugin.LoadDataFromString(configurationFromXMLNode.OuterXml);
                }

                m_log.Info("XML Configuration Filename is not valid; will not save to the file.");
                useFile = false;
            }

            foreach (ConfigurationOption configOption in configurationOptions)
            {
                bool convertSuccess = false;
                object return_result = null;
                string errorMessage = String.Empty;
                bool ignoreNextFromConfig = false;
                while (convertSuccess == false)
                {
                    string console_result = String.Empty;
                    string attribute = null;
                    if (useFile || configurationFromXMLNode != null)
                    {
                        if (!ignoreNextFromConfig)
                        {
                            attribute = configurationPlugin.GetAttribute(configOption.configurationKey);
                        }
                        else
                        {
                            ignoreNextFromConfig = false;
                        }
                    }

                    if (attribute == null)
                    {
                        if (configOption.configurationUseDefaultNoPrompt || useConsoleToPromptOnError == false)
                        {
                            console_result = configOption.configurationDefault;
                        }
                        else
                        {
                            if ((configOption.shouldIBeAsked != null &&
                                 configOption.shouldIBeAsked(configOption.configurationKey)) ||
                                configOption.shouldIBeAsked == null)
                            {
                                if (configurationDescription.Trim() != String.Empty)
                                {
                                    console_result =
                                        MainConsole.Instance.CmdPrompt(
                                            configurationDescription + ": " + configOption.configurationQuestion,
                                            configOption.configurationDefault);
                                }
                                else
                                {
                                    console_result =
                                        MainConsole.Instance.CmdPrompt(configOption.configurationQuestion,
                                                                       configOption.configurationDefault);
                                }
                            }
                            else
                            {
                                //Dont Ask! Just use default
                                console_result = configOption.configurationDefault;
                            }
                        }
                    }
                    else
                    {
                        console_result = attribute;
                    }

                    // if the first character is a "$", assume it's the name
                    // of an environment variable and substitute with the value of that variable
                    if (console_result.StartsWith("$"))
                        console_result = Environment.GetEnvironmentVariable(console_result.Substring(1));

                    switch (configOption.configurationType)
                    {
                        case ConfigurationOption.ConfigurationTypes.TYPE_STRING:
                            return_result = console_result;
                            convertSuccess = true;
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY:
                            if (console_result.Length > 0)
                            {
                                return_result = console_result;
                                convertSuccess = true;
                            }
                            errorMessage = "a string that is not empty";
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_BOOLEAN:
                            bool boolResult;
                            if (Boolean.TryParse(console_result, out boolResult))
                            {
                                convertSuccess = true;
                                return_result = boolResult;
                            }
                            errorMessage = "'true' or 'false' (Boolean)";
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_BYTE:
                            byte byteResult;
                            if (Byte.TryParse(console_result, out byteResult))
                            {
                                convertSuccess = true;
                                return_result = byteResult;
                            }
                            errorMessage = "a byte (Byte)";
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_CHARACTER:
                            char charResult;
                            if (Char.TryParse(console_result, out charResult))
                            {
                                convertSuccess = true;
                                return_result = charResult;
                            }
                            errorMessage = "a character (Char)";
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_INT16:
                            short shortResult;
                            if (Int16.TryParse(console_result, out shortResult))
                            {
                                convertSuccess = true;
                                return_result = shortResult;
                            }
                            errorMessage = "a signed 32 bit integer (short)";
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_INT32:
                            int intResult;
                            if (Int32.TryParse(console_result, out intResult))
                            {
                                convertSuccess = true;
                                return_result = intResult;
                            }
                            errorMessage = "a signed 32 bit integer (int)";
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_INT64:
                            long longResult;
                            if (Int64.TryParse(console_result, out longResult))
                            {
                                convertSuccess = true;
                                return_result = longResult;
                            }
                            errorMessage = "a signed 32 bit integer (long)";
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_IP_ADDRESS:
                            IPAddress ipAddressResult;
                            if (IPAddress.TryParse(console_result, out ipAddressResult))
                            {
                                convertSuccess = true;
                                return_result = ipAddressResult;
                            }
                            errorMessage = "an IP Address (IPAddress)";
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_UUID:
                            UUID uuidResult;
                            if (UUID.TryParse(console_result, out uuidResult))
                            {
                                convertSuccess = true;
                                return_result = uuidResult;
                            }
                            errorMessage = "a UUID (UUID)";
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_UUID_NULL_FREE:
                            UUID uuidResult2;
                            if (UUID.TryParse(console_result, out uuidResult2))
                            {
                                convertSuccess = true;

                                if (uuidResult2 == UUID.Zero)
                                    uuidResult2 = UUID.Random();

                                return_result = uuidResult2;
                            }
                            errorMessage = "a non-null UUID (UUID)";
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_Vector3:
                            Vector3 vectorResult;
                            if (Vector3.TryParse(console_result, out vectorResult))
                            {
                                convertSuccess = true;
                                return_result = vectorResult;
                            }
                            errorMessage = "a vector (Vector3)";
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_UINT16:
                            ushort ushortResult;
                            if (UInt16.TryParse(console_result, out ushortResult))
                            {
                                convertSuccess = true;
                                return_result = ushortResult;
                            }
                            errorMessage = "an unsigned 16 bit integer (ushort)";
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_UINT32:
                            uint uintResult;
                            if (UInt32.TryParse(console_result, out uintResult))
                            {
                                convertSuccess = true;
                                return_result = uintResult;
                            }
                            errorMessage = "an unsigned 32 bit integer (uint)";
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_UINT64:
                            ulong ulongResult;
                            if (UInt64.TryParse(console_result, out ulongResult))
                            {
                                convertSuccess = true;
                                return_result = ulongResult;
                            }
                            errorMessage = "an unsigned 64 bit integer (ulong)";
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_FLOAT:
                            float floatResult;
                            if (
                                float.TryParse(console_result, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, Culture.NumberFormatInfo,
                                               out floatResult))
                            {
                                convertSuccess = true;
                                return_result = floatResult;
                            }
                            errorMessage = "a single-precision floating point number (float)";
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_DOUBLE:
                            double doubleResult;
                            if (
                                Double.TryParse(console_result, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, Culture.NumberFormatInfo,
                                                out doubleResult))
                            {
                                convertSuccess = true;
                                return_result = doubleResult;
                            }
                            errorMessage = "an double-precision floating point number (double)";
                            break;
                    }

                    if (convertSuccess)
                    {
                        if (useFile)
                        {
                            configurationPlugin.SetAttribute(configOption.configurationKey, console_result);
                        }

                        if (!resultFunction(configOption.configurationKey, return_result))
                        {
                            m_log.Info(
                                "The handler for the last configuration option denied that input, please try again.");
                            convertSuccess = false;
                            ignoreNextFromConfig = true;
                        }
                    }
                    else
                    {
                        if (configOption.configurationUseDefaultNoPrompt)
                        {
                            m_log.Error(string.Format(
                                            "[CONFIG]: [{3}]:[{1}] is not valid default for parameter [{0}].\nThe configuration result must be parsable to {2}.\n",
                                            configOption.configurationKey, console_result, errorMessage,
                                            configurationFilename));
                            convertSuccess = true;
                        }
                        else
                        {
                            m_log.Warn(string.Format(
                                           "[CONFIG]: [{3}]:[{1}] is not a valid value [{0}].\nThe configuration result must be parsable to {2}.\n",
                                           configOption.configurationKey, console_result, errorMessage,
                                           configurationFilename));
                            ignoreNextFromConfig = true;
                        }
                    }
                }
            }

            if (useFile)
            {
                configurationPlugin.Commit();
                configurationPlugin.Close();
            }
        }

        private static IGenericConfig LoadConfigDll(string dllName)
        {
            Assembly pluginAssembly = Assembly.LoadFrom(dllName);
            IGenericConfig plug = null;

            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (pluginType.IsPublic)
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type typeInterface = pluginType.GetInterface("IGenericConfig", true);

                        if (typeInterface != null)
                        {
                            plug =
                                (IGenericConfig) Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                        }
                    }
                }
            }

            pluginAssembly = null;
            return plug;
        }

        public void forceSetConfigurationOption(string configuration_key, string configuration_value)
        {
            configurationPlugin.LoadData();
            configurationPlugin.SetAttribute(configuration_key, configuration_value);
            configurationPlugin.Commit();
            configurationPlugin.Close();
        }
    }
}
