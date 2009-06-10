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
using System.IO;
using System.Text.RegularExpressions;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class WearableItem
    {
        public string WearableName = "";
        public WearableType WearType = WearableType.Invalid;

        public string ItemInfo = "Created Wearable";

        public SortedList<int, VisualSetting> VisualSettings = new SortedList<int, VisualSetting>();
        // public LLObject.TextureEntry TextureEntry = null;
        //public byte[] TextureEntry = null;

        public List<string> TextureStrings = new List<string>();

        //permissions
        public uint BaseMask = 0;
        public uint OwnerMask = 0;
        public uint GroupMask = 0;
        public uint EveryoneMask = 0;
        public uint NextOwnerMask = 0;

        public UUID CreatorID = UUID.Zero;
        public UUID OwnerID = UUID.Zero;
        public UUID LastOwnerID = UUID.Zero;
        public UUID GroupID = UUID.Zero;

        //sale
        public string SaleType = "not";
        public int SalePrice = 10;

        private string BuildString = "";


        public WearableItem(string wearableName, WearableType type)
        {
            WearableName = wearableName;
            WearType = type;
        }

        public WearableItem(string wearableName)
        {
            WearableName = wearableName;
            WearType = ConvertNameToType(WearableName);
        }

        public WearableItem(WearableType type)
        {
            WearType = type;
            WearableName = Enum.GetName(typeof(WearableType), type).ToLower();
        }

        public WearableItem()
        {
        }

        public void AddVisualSetting(VisualSetting setting)
        {
            if (!VisualSettings.ContainsKey(setting.VisualParam.ParamID))
            {
                VisualSettings.Add(setting.VisualParam.ParamID, setting);
            }
        }

        public bool TryGetSetting(string paramName, out VisualSetting paramSetting)
        {
            foreach (VisualSetting setting in VisualSettings.Values)
            {
                if (setting.VisualParam.Name == paramName)
                {
                    paramSetting = setting;
                    return true;
                }
            }

            paramSetting = null;
            return false;
        }

        public bool SetParamValue(string paramName, float value)
        {
            VisualSetting paramSetting;
            if (TryGetSetting(paramName, out paramSetting))
            {
                if ((value >= paramSetting.VisualParam.MinValue) && (value <= paramSetting.VisualParam.MaxValue))
                {
                    paramSetting.Value = value;
                    return true;
                }
            }
            return false;
        }

        public void RandomiseValues()
        {
            foreach (VisualSetting setting in VisualSettings.Values)
            {
                //int randNum = Util.RandomClass.Next(0, 1000);
                float range = setting.VisualParam.MaxValue - setting.VisualParam.MinValue;
                // float val = ((float) randNum) / ((float)(1000.0f / range));
                float val = (float)Util.RandomClass.NextDouble() * range * 0.2f;
                setting.Value = setting.VisualParam.MinValue + (range / 2) + val;
            }
        }

        public WearableType ConvertNameToType(string name)
        {
            return (WearableType)Enum.Parse(typeof(WearableType), name, true);
        }

        public string ToAssetFormat()
        {
            BuildString = "LLWearable version 22\n";
            BuildString += "New Item \n";
            BuildString += ItemInfo + "\n";


            AddSectionStart("permissions");
            AddTabbedNameValueLine("base_mask", BaseMask.ToString("00000000"));
            AddTabbedNameValueLine("owner_mask", OwnerMask.ToString("00000000"));
            AddTabbedNameValueLine("group_mask", GroupMask.ToString("00000000"));
            AddTabbedNameValueLine("everyone_mask", EveryoneMask.ToString("00000000"));
            AddTabbedNameValueLine("next_owner_mask", NextOwnerMask.ToString("00000000"));
            AddTabbedNameValueLine("creator_id", CreatorID.ToString());
            AddTabbedNameValueLine("owner_id", OwnerID.ToString());
            AddTabbedNameValueLine("last_owner_id", LastOwnerID.ToString());
            AddTabbedNameValueLine("group_id", GroupID.ToString());
            AddSectionEnd();

            AddSectionStart("sale_info");
            AddTabbedNameValueLine("sale_type", SaleType.ToString());
            AddTabbedNameValueLine("sale_price", SalePrice.ToString());
            AddSectionEnd();

            AddNameValueLine("type", ((byte)WearType).ToString());
            AddNameValueLine("parameters", VisualSettings.Count.ToString());

            foreach (KeyValuePair<int, VisualSetting> kp in VisualSettings)
            {
                AddNameValueLine(kp.Key.ToString(), kp.Value.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (TextureStrings.Count == 0)
            {
                AddNameValueLine("textures", "0"); //todo output texture entry
            }
            else
            {
                AddNameValueLine("textures", TextureStrings.Count.ToString());
                for (int i = 0; i < TextureStrings.Count; i++)
                {
                    BuildString += TextureStrings[i] + "\n";
                }
                BuildString += "\n";

            }

            return BuildString;
        }

        public void SaveToFile(string fileName)
        {
            File.WriteAllText(fileName, this.ToAssetFormat());
        }

        public void AddSectionStart(string sectionName)
        {
            BuildString += "\t" + sectionName + " 0\n";
            BuildString += "\t{\n";
        }

        public void AddSectionEnd()
        {
            BuildString += "\t}\n";
        }

        private void AddTabbedNameValueLine(string name, string value)
        {
            BuildString += "\t\t";
            BuildString += name + "\t";
            BuildString += value + "\n";
        }

        private void AddNameValueLine(string name, string value)
        {
            // BuildString += "\t\t";
            BuildString += name + " ";
            BuildString += value + "\n";
        }

        #region Static Methods
        public static List<VisualParam> FindParamsForWearable(string wearableName)
        {
            List<VisualParam> wearableParams = new List<VisualParam>();
            foreach (VisualParam param in VisualParams.Params.Values)
            {
                if (param.Wearable == wearableName)
                {
                    wearableParams.Add(param);
                }
            }

            return wearableParams;
        }

        public static WearableItem Create(string wearableTypeName)
        {
            WearableItem wearableItem = new WearableItem(wearableTypeName);
            List<VisualParam> typeParams = FindParamsForWearable(wearableTypeName);
            foreach (VisualParam param in typeParams)
            {
                wearableItem.AddVisualSetting(new VisualSetting(param));
            }
            return wearableItem;
        }

        public static WearableItem CreateFromAsset(string assetData)
        {
            UUID creatorID = UUID.Zero;
            UUID ownerID = UUID.Zero;
            UUID lastOwnerID = UUID.Zero;
            UUID groupID = UUID.Zero;

            char[] newlineDelimiter = { '\n' };
            string[] lines = assetData.Split(newlineDelimiter);

            WearableItem wearableObject = null;
            Regex r = new Regex("[\t ]+");
            bool reachedParams = false;
            bool reachedTextures = false;
            foreach (string line in lines)
            {
                string trimLine = line.Trim();
                // m_log.Debug("line : " + trimLine);

                string[] splitLine = r.Split(trimLine);
                if (splitLine.Length > 1)
                {
                    switch (splitLine[0])
                    {
                        case "textures":
                            reachedParams = false;
                            reachedTextures = true;
                            break;

                        case "type":
                            string wearableTypeName = Enum.GetName(typeof(WearableType), (WearableType)Convert.ToInt32(splitLine[1]));
                            wearableObject = Create(wearableTypeName.ToLower());
                            break;

                        case "parameters":
                            reachedParams = true;
                            break;

                        case "creator_id":
                            creatorID = new UUID(splitLine[1]);
                            break;

                        case "owner_id":
                            ownerID = new UUID(splitLine[1]);
                            break;

                        case "last_owner_id":
                            lastOwnerID = new UUID(splitLine[1]);
                            break;

                        case "group_id":
                            groupID = new UUID(splitLine[1]);
                            break;

                        default:
                            if ((wearableObject != null) && (reachedParams))
                            {
                                int id = Convert.ToInt32(splitLine[0]);
                                if (wearableObject.VisualSettings.ContainsKey(id))
                                {

                                    wearableObject.VisualSettings[id].Value = Convert.ToSingle(splitLine[1], CultureInfo.InvariantCulture);
                                }
                            }
                            else if ((wearableObject != null) && (reachedTextures))
                            {
                                wearableObject.TextureStrings.Add(line);
                            }
                            break;
                    }
                }
            }

            if (wearableObject != null)
            {
                wearableObject.CreatorID = creatorID;
                wearableObject.OwnerID = ownerID;
                wearableObject.LastOwnerID = lastOwnerID;
                wearableObject.GroupID = groupID;
            }

            return wearableObject;
        }
        #endregion

        #region Nested Class
        public class VisualSetting
        {
            public VisualParam VisualParam;
            public float Value = 0;

            public VisualSetting(VisualParam param, float value)
            {
                VisualParam = param;
                Value = value;
            }

            public VisualSetting(VisualParam param)
            {
                VisualParam = param;
                Value = param.DefaultValue;
            }
        }
        #endregion
    }
}
