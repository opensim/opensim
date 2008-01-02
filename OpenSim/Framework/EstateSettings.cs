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
*     * Neither the name of the OpenSim Project nor the
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
* 
*/
using System;
using System.IO;
using libsecondlife;

namespace OpenSim.Framework
{
    public class EstateSettings
    {
        //Settings to this island
        private float m_billableFactor;

        public float billableFactor
        {
            get { return m_billableFactor; }
            set
            {
                m_billableFactor = value;
                configMember.forceSetConfigurationOption("billable_factor", m_billableFactor.ToString());
            }
        }


        private uint m_estateID;

        public uint estateID
        {
            get { return m_estateID; }
            set
            {
                m_estateID = value;
                configMember.forceSetConfigurationOption("estate_id", m_estateID.ToString());
            }
        }


        private uint m_parentEstateID;

        public uint parentEstateID
        {
            get { return m_parentEstateID; }
            set
            {
                m_parentEstateID = value;
                configMember.forceSetConfigurationOption("parent_estate_id", m_parentEstateID.ToString());
            }
        }

        private byte m_maxAgents;

        public byte maxAgents
        {
            get { return m_maxAgents; }
            set
            {
                m_maxAgents = value;
                configMember.forceSetConfigurationOption("max_agents", m_maxAgents.ToString());
            }
        }

        private float m_objectBonusFactor;

        public float objectBonusFactor
        {
            get { return m_objectBonusFactor; }
            set
            {
                m_objectBonusFactor = value;
                configMember.forceSetConfigurationOption("object_bonus_factor", m_objectBonusFactor.ToString());
            }
        }

        private int m_redirectGridX;

        public int redirectGridX
        {
            get { return m_redirectGridX; }
            set
            {
                m_redirectGridX = value;
                configMember.forceSetConfigurationOption("redirect_grid_x", m_redirectGridX.ToString());
            }
        }

        private int m_redirectGridY;

        public int redirectGridY
        {
            get { return m_redirectGridY; }
            set
            {
                m_redirectGridY = value;
                configMember.forceSetConfigurationOption("redirect_grid_y", m_redirectGridY.ToString());
            }
        }

        private Simulator.RegionFlags m_regionFlags;

        public Simulator.RegionFlags regionFlags
        {
            get { return m_regionFlags; }
            set
            {
                m_regionFlags = value;
                configMember.forceSetConfigurationOption("region_flags", ((uint)m_regionFlags).ToString());
            }
        }


        private Simulator.SimAccess m_simAccess;

        public Simulator.SimAccess simAccess
        {
            get { return m_simAccess; }
            set
            {
                m_simAccess = value;
                configMember.forceSetConfigurationOption("sim_access", ((byte)m_simAccess).ToString());
            }
        }

        private float m_sunHour;

        public float sunHour
        {
            get { return m_sunHour; }
            set
            {
                m_sunHour = value;
                configMember.forceSetConfigurationOption("sun_hour", m_sunHour.ToString());
            }
        }

        private float m_terrainRaiseLimit;

        public float terrainRaiseLimit
        {
            get { return m_terrainRaiseLimit; }
            set
            {
                m_terrainRaiseLimit = value;
                configMember.forceSetConfigurationOption("terrain_raise_limit", m_terrainRaiseLimit.ToString());
            }
        }

        private float m_terrainLowerLimit;

        public float terrainLowerLimit
        {
            get { return m_terrainLowerLimit; }
            set
            {
                m_terrainLowerLimit = value;
                configMember.forceSetConfigurationOption("terrain_lower_limit", m_terrainLowerLimit.ToString());
            }
        }

        private bool m_useFixedSun;

        public bool useFixedSun
        {
            get { return m_useFixedSun; }
            set
            {
                m_useFixedSun = value;
                configMember.forceSetConfigurationOption("use_fixed_sun", m_useFixedSun.ToString());
            }
        }


        private int m_pricePerMeter;

        public int pricePerMeter
        {
            get { return m_pricePerMeter; }
            set
            {
                m_pricePerMeter = value;
                configMember.forceSetConfigurationOption("price_per_meter", m_pricePerMeter.ToString());
            }
        }


        private ushort m_regionWaterHeight;

        public ushort regionWaterHeight
        {
            get { return m_regionWaterHeight; }
            set
            {
                m_regionWaterHeight = value;
                configMember.forceSetConfigurationOption("region_water_height", m_regionWaterHeight.ToString());
            }
        }


        private bool m_regionAllowTerraform;

        public bool regionAllowTerraform
        {
            get { return m_regionAllowTerraform; }
            set
            {
                m_regionAllowTerraform = value;
                configMember.forceSetConfigurationOption("region_allow_terraform", m_regionAllowTerraform.ToString());
            }
        }


        // Region Information
        // Low resolution 'base' textures. No longer used.
        private LLUUID m_terrainBase0;

        public LLUUID terrainBase0
        {
            get { return m_terrainBase0; }
            set
            {
                m_terrainBase0 = value;
                configMember.forceSetConfigurationOption("terrain_base_0", m_terrainBase0.ToString());
            }
        }

        private LLUUID m_terrainBase1;

        public LLUUID terrainBase1
        {
            get { return m_terrainBase1; }
            set
            {
                m_terrainBase1 = value;
                configMember.forceSetConfigurationOption("terrain_base_1", m_terrainBase1.ToString());
            }
        }

        private LLUUID m_terrainBase2;

        public LLUUID terrainBase2
        {
            get { return m_terrainBase2; }
            set
            {
                m_terrainBase2 = value;
                configMember.forceSetConfigurationOption("terrain_base_2", m_terrainBase2.ToString());
            }
        }

        private LLUUID m_terrainBase3;

        public LLUUID terrainBase3
        {
            get { return m_terrainBase3; }
            set
            {
                m_terrainBase3 = value;
                configMember.forceSetConfigurationOption("terrain_base_3", m_terrainBase3.ToString());
            }
        }


        // Higher resolution terrain textures
        private LLUUID m_terrainDetail0;

        public LLUUID terrainDetail0
        {
            get { return m_terrainDetail0; }
            set
            {
                m_terrainDetail0 = value;
                configMember.forceSetConfigurationOption("terrain_detail_0", m_terrainDetail0.ToString());
            }
        }

        private LLUUID m_terrainDetail1;

        public LLUUID terrainDetail1
        {
            get { return m_terrainDetail1; }
            set
            {
                m_terrainDetail1 = value;
                configMember.forceSetConfigurationOption("terrain_detail_1", m_terrainDetail1.ToString());
            }
        }

        private LLUUID m_terrainDetail2;

        public LLUUID terrainDetail2
        {
            get { return m_terrainDetail2; }
            set
            {
                m_terrainDetail2 = value;
                configMember.forceSetConfigurationOption("terrain_detail_2", m_terrainDetail2.ToString());
            }
        }

        private LLUUID m_terrainDetail3;

        public LLUUID terrainDetail3
        {
            get { return m_terrainDetail3; }
            set
            {
                m_terrainDetail3 = value;
                configMember.forceSetConfigurationOption("terrain_detail_3", m_terrainDetail3.ToString());
            }
        }

        // First quad - each point is bilinearly interpolated at each meter of terrain
        private float m_terrainStartHeight0;

        public float terrainStartHeight0
        {
            get { return m_terrainStartHeight0; }
            set
            {
                m_terrainStartHeight0 = value;
                configMember.forceSetConfigurationOption("terrain_start_height_0", m_terrainStartHeight0.ToString());
            }
        }


        private float m_terrainStartHeight1;

        public float terrainStartHeight1
        {
            get { return m_terrainStartHeight1; }
            set
            {
                m_terrainStartHeight1 = value;
                configMember.forceSetConfigurationOption("terrain_start_height_1", m_terrainStartHeight1.ToString());
            }
        }

        private float m_terrainStartHeight2;

        public float terrainStartHeight2
        {
            get { return m_terrainStartHeight2; }
            set
            {
                m_terrainStartHeight2 = value;
                configMember.forceSetConfigurationOption("terrain_start_height_2", m_terrainStartHeight2.ToString());
            }
        }

        private float m_terrainStartHeight3;

        public float terrainStartHeight3
        {
            get { return m_terrainStartHeight3; }
            set
            {
                m_terrainStartHeight3 = value;
                configMember.forceSetConfigurationOption("terrain_start_height_3", m_terrainStartHeight3.ToString());
            }
        }

        // Second quad - also bilinearly interpolated.
        // Terrain texturing is done that:
        // 0..3 (0 = base0, 3 = base3) = (terrain[x,y] - start[x,y]) / range[x,y]
        private float m_terrainHeightRange0;

        public float terrainHeightRange0
        {
            get { return m_terrainHeightRange0; }
            set
            {
                m_terrainHeightRange0 = value;
                configMember.forceSetConfigurationOption("terrain_height_range_0", m_terrainHeightRange0.ToString());
            }
        }

        private float m_terrainHeightRange1;

        public float terrainHeightRange1
        {
            get { return m_terrainHeightRange1; }
            set
            {
                m_terrainHeightRange1 = value;
                configMember.forceSetConfigurationOption("terrain_height_range_1", m_terrainHeightRange1.ToString());
            }
        }

        private float m_terrainHeightRange2;

        public float terrainHeightRange2
        {
            get { return m_terrainHeightRange2; }
            set
            {
                m_terrainHeightRange2 = value;
                configMember.forceSetConfigurationOption("terrain_height_range_2", m_terrainHeightRange2.ToString());
            }
        }

        private float m_terrainHeightRange3;

        public float terrainHeightRange3
        {
            get { return m_terrainHeightRange3; }
            set
            {
                m_terrainHeightRange3 = value;
                configMember.forceSetConfigurationOption("terrain_height_range_3", m_terrainHeightRange3.ToString());
            }
        }

        // Terrain Default (Must be in F32 Format!)
        private string m_terrainFile;

        public string terrainFile
        {
            get { return m_terrainFile; }
            set
            {
                m_terrainFile = value;
                configMember.forceSetConfigurationOption("terrain_file", m_terrainFile.ToString());
            }
        }

        private double m_terrainMultiplier;

        public double terrainMultiplier
        {
            get { return m_terrainMultiplier; }
            set
            {
                m_terrainMultiplier = value;
                configMember.forceSetConfigurationOption("terrain_multiplier", m_terrainMultiplier.ToString());
            }
        }

        private float m_waterHeight;

        public float waterHeight
        {
            get { return m_waterHeight; }
            set
            {
                m_waterHeight = value;
                configMember.forceSetConfigurationOption("water_height", m_waterHeight.ToString());
            }
        }

        private LLUUID m_terrainImageID;

        public LLUUID terrainImageID
        {
            get { return m_terrainImageID; }
            set
            {
                m_terrainImageID = value;
                // I don't think there is a reason that this actually
                // needs to be written back to the estate settings
                // file.

                // configMember.forceSetConfigurationOption("terrain_image_id", m_terrainImageID.ToString());
            }
        }

        private ConfigurationMember configMember;

        public EstateSettings()
        {
            // Temporary hack to prevent multiple loadings.
            if (configMember == null)
            {
                configMember =
                    new ConfigurationMember(Path.Combine(Util.configDir(), "estate_settings.xml"), "ESTATE SETTINGS",
                                            loadConfigurationOptions, handleIncomingConfiguration);
                configMember.performConfigurationRetrieve();
            }
        }

        public void loadConfigurationOptions()
        {
            configMember.addConfigurationOption("billable_factor", ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, "",
                                                "0.0", true);
            configMember.addConfigurationOption("estate_id", ConfigurationOption.ConfigurationTypes.TYPE_UINT32, "", "0",
                                                true);
            configMember.addConfigurationOption("parent_estate_id", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "", "0", true);
            configMember.addConfigurationOption("max_agents", ConfigurationOption.ConfigurationTypes.TYPE_BYTE, "", "40",
                                                true);

            configMember.addConfigurationOption("object_bonus_factor", ConfigurationOption.ConfigurationTypes.TYPE_FLOAT,
                                                "", "1.0", true);
            configMember.addConfigurationOption("redirect_grid_x", ConfigurationOption.ConfigurationTypes.TYPE_INT32, "",
                                                "0", true);
            configMember.addConfigurationOption("redirect_grid_y", ConfigurationOption.ConfigurationTypes.TYPE_INT32, "",
                                                "0", true);
            configMember.addConfigurationOption("region_flags", ConfigurationOption.ConfigurationTypes.TYPE_UINT32, "",
                                                "0", true);
            configMember.addConfigurationOption("sim_access", ConfigurationOption.ConfigurationTypes.TYPE_BYTE, "", "21",
                                                true);
            configMember.addConfigurationOption("sun_hour", ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, "", "0",
                                                true);
            configMember.addConfigurationOption("terrain_raise_limit", ConfigurationOption.ConfigurationTypes.TYPE_FLOAT,
                                                "", "0", true);
            configMember.addConfigurationOption("terrain_lower_limit", ConfigurationOption.ConfigurationTypes.TYPE_FLOAT,
                                                "", "0", true);
            configMember.addConfigurationOption("use_fixed_sun", ConfigurationOption.ConfigurationTypes.TYPE_BOOLEAN, "",
                                                "false", true);
            configMember.addConfigurationOption("price_per_meter", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "", "1", true);
            configMember.addConfigurationOption("region_water_height",
                                                ConfigurationOption.ConfigurationTypes.TYPE_UINT16, "", "20", true);
            configMember.addConfigurationOption("region_allow_terraform",
                                                ConfigurationOption.ConfigurationTypes.TYPE_BOOLEAN, "", "true", true);

            configMember.addConfigurationOption("terrain_base_0", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID, "",
                                                "b8d3965a-ad78-bf43-699b-bff8eca6c975", true);
            configMember.addConfigurationOption("terrain_base_1", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID, "",
                                                "abb783e6-3e93-26c0-248a-247666855da3", true);
            configMember.addConfigurationOption("terrain_base_2", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID, "",
                                                "179cdabd-398a-9b6b-1391-4dc333ba321f", true);
            configMember.addConfigurationOption("terrain_base_3", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID, "",
                                                "beb169c7-11ea-fff2-efe5-0f24dc881df2", true);

            configMember.addConfigurationOption("terrain_detail_0", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                "", "00000000-0000-0000-0000-000000000000", true);
            configMember.addConfigurationOption("terrain_detail_1", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                "", "00000000-0000-0000-0000-000000000000", true);
            configMember.addConfigurationOption("terrain_detail_2", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                "", "00000000-0000-0000-0000-000000000000", true);
            configMember.addConfigurationOption("terrain_detail_3", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                "", "00000000-0000-0000-0000-000000000000", true);

            configMember.addConfigurationOption("terrain_start_height_0",
                                                ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, "", "10.0", true);
            configMember.addConfigurationOption("terrain_start_height_1",
                                                ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, "", "10.0", true);
            configMember.addConfigurationOption("terrain_start_height_2",
                                                ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, "", "10.0", true);
            configMember.addConfigurationOption("terrain_start_height_3",
                                                ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, "", "10.0", true);

            configMember.addConfigurationOption("terrain_height_range_0",
                                                ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, "", "60.0", true);
            configMember.addConfigurationOption("terrain_height_range_1",
                                                ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, "", "60.0", true);
            configMember.addConfigurationOption("terrain_height_range_2",
                                                ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, "", "60.0", true);
            configMember.addConfigurationOption("terrain_height_range_3",
                                                ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, "", "60.0", true);

            configMember.addConfigurationOption("terrain_file",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "",
                                                "default.r32", true);
            configMember.addConfigurationOption("terrain_multiplier", ConfigurationOption.ConfigurationTypes.TYPE_FLOAT,
                                                "", "60.0", true);
            configMember.addConfigurationOption("water_height", ConfigurationOption.ConfigurationTypes.TYPE_DOUBLE, "",
                                                "20.0", true);
            configMember.addConfigurationOption("terrain_image_id", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                "", "00000000-0000-0000-0000-000000000000", true);
        }

        public bool handleIncomingConfiguration(string configuration_key, object configuration_result)
        {
            switch (configuration_key)
            {
                case "billable_factor":
                    m_billableFactor = (float) configuration_result;
                    break;
                case "estate_id":
                    m_estateID = (uint) configuration_result;
                    break;
                case "parent_estate_id":
                    m_parentEstateID = (uint) configuration_result;
                    break;
                case "max_agents":
                    m_maxAgents = (byte) configuration_result;
                    break;

                case "object_bonus_factor":
                    m_objectBonusFactor = (float) configuration_result;
                    break;
                case "redirect_grid_x":
                    m_redirectGridX = (int) configuration_result;
                    break;
                case "redirect_grid_y":
                    m_redirectGridY = (int) configuration_result;
                    break;
                case "region_flags":
                    m_regionFlags = (Simulator.RegionFlags) ((uint) configuration_result);
                    break;
                case "sim_access":
                    m_simAccess = (Simulator.SimAccess) ((byte) configuration_result);
                    break;
                case "sun_hour":
                    m_sunHour = (float) configuration_result;
                    break;
                case "terrain_raise_limit":
                    m_terrainRaiseLimit = (float) configuration_result;
                    break;
                case "terrain_lower_limit":
                    m_terrainLowerLimit = (float) configuration_result;
                    break;
                case "use_fixed_sun":
                    m_useFixedSun = (bool) configuration_result;
                    break;
                case "price_per_meter":
                    m_pricePerMeter = Convert.ToInt32(configuration_result);
                    break;
                case "region_water_height":
                    m_regionWaterHeight = (ushort) configuration_result;
                    break;
                case "region_allow_terraform":
                    m_regionAllowTerraform = (bool) configuration_result;
                    break;

                case "terrain_base_0":
                    m_terrainBase0 = (LLUUID) configuration_result;
                    break;
                case "terrain_base_1":
                    m_terrainBase1 = (LLUUID) configuration_result;
                    break;
                case "terrain_base_2":
                    m_terrainBase2 = (LLUUID) configuration_result;
                    break;
                case "terrain_base_3":
                    m_terrainBase3 = (LLUUID) configuration_result;
                    break;

                case "terrain_detail_0":
                    m_terrainDetail0 = (LLUUID) configuration_result;
                    break;
                case "terrain_detail_1":
                    m_terrainDetail1 = (LLUUID) configuration_result;
                    break;
                case "terrain_detail_2":
                    m_terrainDetail2 = (LLUUID) configuration_result;
                    break;
                case "terrain_detail_3":
                    m_terrainDetail3 = (LLUUID) configuration_result;
                    break;

                case "terrain_start_height_0":
                    m_terrainStartHeight0 = (float) configuration_result;
                    break;
                case "terrain_start_height_1":
                    m_terrainStartHeight1 = (float) configuration_result;
                    break;
                case "terrain_start_height_2":
                    m_terrainStartHeight2 = (float) configuration_result;
                    break;
                case "terrain_start_height_3":
                    m_terrainStartHeight3 = (float) configuration_result;
                    break;

                case "terrain_height_range_0":
                    m_terrainHeightRange0 = (float) configuration_result;
                    break;
                case "terrain_height_range_1":
                    m_terrainHeightRange1 = (float) configuration_result;
                    break;
                case "terrain_height_range_2":
                    m_terrainHeightRange2 = (float) configuration_result;
                    break;
                case "terrain_height_range_3":
                    m_terrainHeightRange3 = (float) configuration_result;
                    break;

                case "terrain_file":
                    m_terrainFile = (string) configuration_result;
                    break;
                case "terrain_multiplier":
                    m_terrainMultiplier = Convert.ToDouble(configuration_result);
                    break;
                case "water_height":
                    double tmpVal = (double) configuration_result;
                    m_waterHeight = (float) tmpVal;
                    break;
                case "terrain_image_id":
                    m_terrainImageID = (LLUUID) configuration_result;
                    break;
            }

            return true;
        }
    }
}