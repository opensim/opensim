using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Net;

using libsecondlife;

using OpenSim.Framework.Console;
using OpenSim.Framework.Configuration.Interfaces;

namespace OpenSim.Framework.Configuration
{
    public class ConfigurationMember
    {
        public delegate bool ConfigurationOptionResult(string configuration_key, object configuration_result);
        public delegate void ConfigurationOptionsLoad();

        private List<ConfigurationOption> configurationOptions = new List<ConfigurationOption>();
        private string configurationFilename = "";
        private string configurationDescription = "";

        private ConfigurationOptionsLoad loadFunction;
        private ConfigurationOptionResult resultFunction;

        private IGenericConfig configurationPlugin = null;
        public ConfigurationMember(string configuration_filename, string configuration_description, ConfigurationOptionsLoad load_function, ConfigurationOptionResult result_function)
        {
            this.configurationFilename = configuration_filename;
            this.configurationDescription = configuration_description;
            this.loadFunction = load_function;
            this.resultFunction = result_function;
            this.configurationPlugin = this.LoadConfigDll("OpenSim.Framework.Configuration.XML.dll");
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
        
        public void addConfigurationOption(string configuration_key, ConfigurationOption.ConfigurationTypes configuration_type, string configuration_question, string configuration_default, bool use_default_no_prompt)
        {
            ConfigurationOption configOption = new ConfigurationOption();
            configOption.configurationKey = configuration_key;
            configOption.configurationQuestion = configuration_question;
            configOption.configurationDefault = configuration_default;
            configOption.configurationType = configuration_type;
            configOption.configurationUseDefaultNoPrompt = use_default_no_prompt;

            if (configuration_key != "" && configuration_question != "")
            {
                if (!configurationOptions.Contains(configOption))
                {
                    configurationOptions.Add(configOption);
                }
            }
            else
            {
                MainLog.Instance.Notice("Required fields for adding a configuration option is invalid. Will not add this option (" + configuration_key + ")");
            }
        }

        public void performConfigurationRetrieve()
        {
            configurationOptions.Clear();
            if(loadFunction == null)
            {
                MainLog.Instance.Error("Load Function for '" + this.configurationDescription + "' is null. Refusing to run configuration.");
                return;
            }

            if(resultFunction == null)
            {
                MainLog.Instance.Error("Result Function for '" + this.configurationDescription + "' is null. Refusing to run configuration.");
                return;
            }

            MainLog.Instance.Verbose("Calling Configuration Load Function...");
            this.loadFunction();

            if(configurationOptions.Count <= 0)
            {
                MainLog.Instance.Error("No configuration options were specified for '" + this.configurationOptions + "'. Refusing to continue configuration.");
                return;
            }

            bool useFile = true;
            if (configurationPlugin == null)
            {
                MainLog.Instance.Error("Configuration Plugin NOT LOADED!");
                return;
            }

            if (configurationFilename.Trim() != "")
            {
                configurationPlugin.SetFileName(configurationFilename);
                configurationPlugin.LoadData();
                useFile = true;
            }

            else
            {
                MainLog.Instance.Notice("XML Configuration Filename is not valid; will not save to the file.");
                useFile = false;
            }

            foreach (ConfigurationOption configOption in configurationOptions)
            {
                bool convertSuccess = false;
                object return_result = null;
                string errorMessage = "";
                bool ignoreNextFromConfig = false;
                while (convertSuccess == false)
                {
                    
                    string console_result = "";
                    string attribute = null;
                    if (useFile)
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
                        if (configOption.configurationUseDefaultNoPrompt)
                        {
                            console_result = configOption.configurationDefault;
                        }
                        else
                        {
                        
                            if (configurationDescription.Trim() != "")
                            {
                                console_result = MainLog.Instance.CmdPrompt(configurationDescription + ": " + configOption.configurationQuestion, configOption.configurationDefault);
                            }
                            else
                            {
                                console_result = MainLog.Instance.CmdPrompt(configOption.configurationQuestion, configOption.configurationDefault);
                            }
                        }                        
                    }
                    else
                    {
                        console_result = attribute;
                    }

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
                        case ConfigurationOption.ConfigurationTypes.TYPE_LLUUID:
                            LLUUID uuidResult;
                            if (LLUUID.TryParse(console_result, out uuidResult))
                            {
                                convertSuccess = true;
                                return_result = uuidResult;
                            }
                            errorMessage = "a UUID (LLUUID)";
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_LLVECTOR3:
                            LLVector3 vectorResult;
                            if (LLVector3.TryParse(console_result, out vectorResult))
                            {
                                convertSuccess = true;
                                return_result = vectorResult;
                            }
                            errorMessage = "a vector (LLVector3)";
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
                            if (float.TryParse(console_result, out floatResult))
                            {
                                convertSuccess = true;
                                return_result = floatResult;
                            }
                            errorMessage = "a single-precision floating point number (float)";
                            break;
                        case ConfigurationOption.ConfigurationTypes.TYPE_DOUBLE:
                            double doubleResult;
                            if (Double.TryParse(console_result, out doubleResult))
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


                        if (!this.resultFunction(configOption.configurationKey, return_result))
                        {
                            Console.MainLog.Instance.Notice("The handler for the last configuration option denied that input, please try again.");
                            convertSuccess = false;
                            ignoreNextFromConfig = true;
                        }
                    }
                    else
                    {
                        if (configOption.configurationUseDefaultNoPrompt)
                        {
                            MainLog.Instance.Error("Default given for '" + configOption.configurationKey + "' is not valid; the configuration result must be " + errorMessage + ". Will skip this option...");
                            convertSuccess = true;
                        }
                        else
                        {
                            MainLog.Instance.Warn("Incorrect result given, the configuration option must be " + errorMessage + ". Prompting for same option...");
                            ignoreNextFromConfig = true;
                        }
                    }
                }
            }

            if(useFile)
            {
                configurationPlugin.Commit();
                configurationPlugin.Close();
            }
        }

        private IGenericConfig LoadConfigDll(string dllName)
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
                            plug = (IGenericConfig)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                        }
                    }
                }
            }

            pluginAssembly = null;
            return plug;
        }
    }
}
