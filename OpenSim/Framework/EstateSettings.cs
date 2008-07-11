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
 */

using System;
using System.IO;
using System.Reflection;
using libsecondlife;
using log4net;

namespace OpenSim.Framework
{
    public class EstateSettings
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private ConfigurationMember configMember;

        //Settings to this island
        private float m_billableFactor;

        private uint m_estateID;
        private LLUUID m_estateManager0;
        private LLUUID m_estateManager1;
        private LLUUID m_estateManager2;
        private LLUUID m_estateManager3;
        private LLUUID m_estateManager4;
        private LLUUID m_estateManager5;
        private LLUUID m_estateManager6;
        private LLUUID m_estateManager7;
        private LLUUID m_estateManager8;
        private LLUUID m_estateManager9;
        private string m_estateName;
        private byte m_maxAgents;
        private float m_objectBonusFactor;

        private uint m_parentEstateID;
        private int m_pricePerMeter;
        private int m_redirectGridX;
        private int m_redirectGridY;
        private bool m_regionAllowTerraform;
        private Simulator.RegionFlags m_regionFlags;
        private float m_regionWaterHeight;
        private Simulator.SimAccess m_simAccess;
        private float m_sunHour;
        private LLVector3 m_sunPosition;
        private LLUUID m_terrainBase0;
        private LLUUID m_terrainBase1;
        private LLUUID m_terrainBase2;
        private LLUUID m_terrainBase3;
        private LLUUID m_terrainDetail0;
        private LLUUID m_terrainDetail1;
        private LLUUID m_terrainDetail2;
        private LLUUID m_terrainDetail3;
        private string m_terrainFile;
        private float m_terrainHeightRange0;
        private float m_terrainHeightRange1;
        private float m_terrainHeightRange2;
        private float m_terrainHeightRange3;
        private LLUUID m_terrainImageID;
        private float m_terrainLowerLimit;
        private double m_terrainMultiplier;
        private float m_terrainRaiseLimit;
        private float m_terrainStartHeight0;
        private float m_terrainStartHeight1;
        private float m_terrainStartHeight2;
        private float m_terrainStartHeight3;
        private bool m_useFixedSun;
        private float m_waterHeight;

        public EstateSettings()
        {
            // Temporary hack to prevent multiple loadings.
            if (configMember == null)
            {
                configMember =
                    new ConfigurationMember(Path.Combine(Util.configDir(), "estate_settings.xml"), "ESTATE SETTINGS",
                                            loadConfigurationOptions, handleIncomingConfiguration, true);
                configMember.performConfigurationRetrieve();
            }
        }

        public float billableFactor
        {
            get { return m_billableFactor; }
            set
            {
                m_billableFactor = value;
                configMember.forceSetConfigurationOption("billable_factor", m_billableFactor.ToString());
            }
        }

        public uint estateID
        {
            get { return m_estateID; }
            set
            {
                m_estateID = value;
                configMember.forceSetConfigurationOption("estate_id", m_estateID.ToString());
            }
        }

        public uint parentEstateID
        {
            get { return m_parentEstateID; }
            set
            {
                m_parentEstateID = value;
                configMember.forceSetConfigurationOption("parent_estate_id", m_parentEstateID.ToString());
            }
        }

        public byte maxAgents
        {
            get { return m_maxAgents; }
            set
            {
                m_maxAgents = value;
                configMember.forceSetConfigurationOption("max_agents", m_maxAgents.ToString());
            }
        }

        public float objectBonusFactor
        {
            get { return m_objectBonusFactor; }
            set
            {
                m_objectBonusFactor = value;
                configMember.forceSetConfigurationOption("object_bonus_factor", m_objectBonusFactor.ToString());
            }
        }

        public int redirectGridX
        {
            get { return m_redirectGridX; }
            set
            {
                m_redirectGridX = value;
                configMember.forceSetConfigurationOption("redirect_grid_x", m_redirectGridX.ToString());
            }
        }

        public int redirectGridY
        {
            get { return m_redirectGridY; }
            set
            {
                m_redirectGridY = value;
                configMember.forceSetConfigurationOption("redirect_grid_y", m_redirectGridY.ToString());
            }
        }

        public Simulator.RegionFlags regionFlags
        {
            get { return m_regionFlags; }
            set
            {
                //m_regionFlags = (Simulator.RegionFlags)0x400000;
                m_regionFlags = value;
                configMember.forceSetConfigurationOption("region_flags", ((uint) m_regionFlags).ToString());
            }
        }

        public Simulator.SimAccess simAccess
        {
            get { return m_simAccess; }
            set
            {
                m_simAccess = value;
                configMember.forceSetConfigurationOption("sim_access", ((byte) m_simAccess).ToString());
            }
        }

        public float sunHour
        {
            get { return m_sunHour; }
            set
            {
                m_sunHour = value;

                if (useFixedSun)
                    configMember.forceSetConfigurationOption("sun_hour", m_sunHour.ToString());
            }
        }

        public LLVector3 sunPosition
        {
            get { return m_sunPosition; }
            set
            {
                //Just set - does not need to be written to settings file
                m_sunPosition = value;
            }
        }

        public float terrainRaiseLimit
        {
            get { return m_terrainRaiseLimit; }
            set
            {
                m_terrainRaiseLimit = value;
                configMember.forceSetConfigurationOption("terrain_raise_limit", m_terrainRaiseLimit.ToString());
            }
        }

        public float terrainLowerLimit
        {
            get { return m_terrainLowerLimit; }
            set
            {
                m_terrainLowerLimit = value;
                configMember.forceSetConfigurationOption("terrain_lower_limit", m_terrainLowerLimit.ToString());
            }
        }

        public bool useFixedSun
        {
            get { return m_useFixedSun; }
            set
            {
                m_useFixedSun = value;
                configMember.forceSetConfigurationOption("use_fixed_sun", m_useFixedSun.ToString());
            }
        }

        public int pricePerMeter
        {
            get { return m_pricePerMeter; }
            set
            {
                m_pricePerMeter = value;
                configMember.forceSetConfigurationOption("price_per_meter", m_pricePerMeter.ToString());
            }
        }


        public float regionWaterHeight
        {
            get { return m_regionWaterHeight; }
            set
            {
                m_regionWaterHeight = value;
                configMember.forceSetConfigurationOption("region_water_height", m_regionWaterHeight.ToString());
            }
        }


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

        public LLUUID terrainBase0
        {
            get { return m_terrainBase0; }
            set
            {
                m_terrainBase0 = value;
                configMember.forceSetConfigurationOption("terrain_base_0", m_terrainBase0.ToString());
            }
        }

        public LLUUID terrainBase1
        {
            get { return m_terrainBase1; }
            set
            {
                m_terrainBase1 = value;
                configMember.forceSetConfigurationOption("terrain_base_1", m_terrainBase1.ToString());
            }
        }

        public LLUUID terrainBase2
        {
            get { return m_terrainBase2; }
            set
            {
                m_terrainBase2 = value;
                configMember.forceSetConfigurationOption("terrain_base_2", m_terrainBase2.ToString());
            }
        }

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

        public LLUUID terrainDetail0
        {
            get { return m_terrainDetail0; }
            set
            {
                m_terrainDetail0 = value;
                configMember.forceSetConfigurationOption("terrain_detail_0", m_terrainDetail0.ToString());
            }
        }

        public LLUUID terrainDetail1
        {
            get { return m_terrainDetail1; }
            set
            {
                m_terrainDetail1 = value;
                configMember.forceSetConfigurationOption("terrain_detail_1", m_terrainDetail1.ToString());
            }
        }

        public LLUUID terrainDetail2
        {
            get { return m_terrainDetail2; }
            set
            {
                m_terrainDetail2 = value;
                configMember.forceSetConfigurationOption("terrain_detail_2", m_terrainDetail2.ToString());
            }
        }

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

        public float terrainStartHeight0
        {
            get { return m_terrainStartHeight0; }
            set
            {
                m_terrainStartHeight0 = value;
                configMember.forceSetConfigurationOption("terrain_start_height_0", m_terrainStartHeight0.ToString());
            }
        }


        public float terrainStartHeight1
        {
            get { return m_terrainStartHeight1; }
            set
            {
                m_terrainStartHeight1 = value;
                configMember.forceSetConfigurationOption("terrain_start_height_1", m_terrainStartHeight1.ToString());
            }
        }

        public float terrainStartHeight2
        {
            get { return m_terrainStartHeight2; }
            set
            {
                m_terrainStartHeight2 = value;
                configMember.forceSetConfigurationOption("terrain_start_height_2", m_terrainStartHeight2.ToString());
            }
        }

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

        public float terrainHeightRange0
        {
            get { return m_terrainHeightRange0; }
            set
            {
                m_terrainHeightRange0 = value;
                configMember.forceSetConfigurationOption("terrain_height_range_0", m_terrainHeightRange0.ToString());
            }
        }

        public float terrainHeightRange1
        {
            get { return m_terrainHeightRange1; }
            set
            {
                m_terrainHeightRange1 = value;
                configMember.forceSetConfigurationOption("terrain_height_range_1", m_terrainHeightRange1.ToString());
            }
        }

        public float terrainHeightRange2
        {
            get { return m_terrainHeightRange2; }
            set
            {
                m_terrainHeightRange2 = value;
                configMember.forceSetConfigurationOption("terrain_height_range_2", m_terrainHeightRange2.ToString());
            }
        }

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

        public string terrainFile
        {
            get { return m_terrainFile; }
            set
            {
                m_terrainFile = value;
                configMember.forceSetConfigurationOption("terrain_file", m_terrainFile.ToString());
            }
        }

        public double terrainMultiplier
        {
            get { return m_terrainMultiplier; }
            set
            {
                m_terrainMultiplier = value;
                configMember.forceSetConfigurationOption("terrain_multiplier", m_terrainMultiplier.ToString());
            }
        }

        public float waterHeight
        {
            get { return m_waterHeight; }
            set
            {
                m_waterHeight = value;
                configMember.forceSetConfigurationOption("water_height", m_waterHeight.ToString());
            }
        }

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

        // Estate name

        public string estateName
        {
            get { return m_estateName; }
            set
            {
                m_estateName = value;
                configMember.forceSetConfigurationOption("estate_name", m_estateName.ToString());
            }
        }

        public LLUUID[] estateManagers
        {
            get
            {
                // returns a condensed array of LLUUIDs
                return GetEstateManagers();
            }
            set
            {
                // Sets a Condensed array of LLUUIDS
                int i = 0;
                for (i = 0; i < value.Length; i++)
                {
                    switch (i)
                    {
                        case 0:
                            m_estateManager0 = value[i];
                            break;
                        case 1:
                            m_estateManager1 = value[i];
                            break;
                        case 2:
                            m_estateManager2 = value[i];
                            break;
                        case 3:
                            m_estateManager3 = value[i];
                            break;
                        case 4:
                            m_estateManager4 = value[i];
                            break;
                        case 5:
                            m_estateManager5 = value[i];
                            break;
                        case 6:
                            m_estateManager6 = value[i];
                            break;
                        case 7:
                            m_estateManager7 = value[i];
                            break;
                        case 8:
                            m_estateManager8 = value[i];
                            break;
                        case 9:
                            m_estateManager9 = value[i];
                            break;
                    }
                }

                // Clear the rest of them..   as they're no longer valid
                for (int j = i; j < 10; j++)
                {
                    switch (j)
                    {
                        case 0:
                            m_estateManager0 = LLUUID.Zero;
                            break;
                        case 1:
                            m_estateManager1 = LLUUID.Zero;
                            break;
                        case 2:
                            m_estateManager2 = LLUUID.Zero;
                            break;
                        case 3:
                            m_estateManager3 = LLUUID.Zero;
                            break;
                        case 4:
                            m_estateManager4 = LLUUID.Zero;
                            break;
                        case 5:
                            m_estateManager5 = LLUUID.Zero;
                            break;
                        case 6:
                            m_estateManager6 = LLUUID.Zero;
                            break;
                        case 7:
                            m_estateManager7 = LLUUID.Zero;
                            break;
                        case 8:
                            m_estateManager8 = LLUUID.Zero;
                            break;
                        case 9:
                            m_estateManager9 = LLUUID.Zero;
                            break;
                    }
                }

                for (i = 0; i < 10; i++)
                {
                    // Writes out the Estate managers to the XML file.
                    configMember.forceSetConfigurationOption("estate_manager_" + i, (GetEstateManagerAtPos(i)).ToString());
                }
            }
        }

        #region EstateManager Get Methods to sort out skipped spots in the XML (suser error)

        private LLUUID GetEstateManagerAtPos(int pos)
        {
            // This is a helper for writing them out to the xml file
            switch (pos)
            {
                case 0:
                    return m_estateManager0;

                case 1:
                    return m_estateManager1;

                case 2:
                    return m_estateManager2;

                case 3:
                    return m_estateManager3;

                case 4:
                    return m_estateManager4;

                case 5:
                    return m_estateManager5;

                case 6:
                    return m_estateManager6;

                case 7:
                    return m_estateManager7;

                case 8:
                    return m_estateManager8;

                case 9:
                    return m_estateManager9;

                default:
                    return LLUUID.Zero;
            }
        }

        private LLUUID[] GetEstateManagers()
        {
            int numEstateManagers = GetNumberOfEstateManagers();
            LLUUID[] rEstateManagers = new LLUUID[numEstateManagers];

            int pos = 0;

            for (int i = 0; i < numEstateManagers; i++)
            {
                pos = GetNextEstateManager(pos);

                rEstateManagers[i] = GetEstateManagerAtPos(pos);
                pos++;
            }
            return rEstateManagers;
        }

        private int GetNextEstateManager(int startpos)
        {
            // This is a utility function that skips over estate managers set to LLUUID.Zero
            int i = startpos;
            for (i = startpos; i < 10; i++)
            {
                if (GetEstateManagerAtPos(i) != LLUUID.Zero) return i;
            }
            return i;
        }

        private int GetNumberOfEstateManagers()
        {
            // This function returns the number of estate managers set
            // Regardless of whether there is a skipped spot
            int numEstateManagers = 0;
            if (m_estateManager0 != LLUUID.Zero) numEstateManagers++;
            if (m_estateManager1 != LLUUID.Zero) numEstateManagers++;
            if (m_estateManager2 != LLUUID.Zero) numEstateManagers++;
            if (m_estateManager3 != LLUUID.Zero) numEstateManagers++;
            if (m_estateManager4 != LLUUID.Zero) numEstateManagers++;
            if (m_estateManager5 != LLUUID.Zero) numEstateManagers++;
            if (m_estateManager6 != LLUUID.Zero) numEstateManagers++;
            if (m_estateManager7 != LLUUID.Zero) numEstateManagers++;
            if (m_estateManager8 != LLUUID.Zero) numEstateManagers++;
            if (m_estateManager9 != LLUUID.Zero) numEstateManagers++;

            return numEstateManagers;
        }

        public void AddEstateManager(LLUUID avatarID)
        {
            LLUUID[] testateManagers = GetEstateManagers();
            LLUUID[] nestateManagers = new LLUUID[testateManagers.Length + 1];

            int i = 0;
            for (i = 0; i < testateManagers.Length; i++)
            {
                nestateManagers[i] = testateManagers[i];
            }

            nestateManagers[i] = avatarID;

            //Saves it to the estate settings file
            estateManagers = nestateManagers;
        }

        public void RemoveEstateManager(LLUUID avatarID)
        {
            int notfoundparam = 11; // starting high so the condense routine (max ten) doesn't run if we don't find it.
            LLUUID[] testateManagers = GetEstateManagers(); // temporary estate managers list


            int i = 0;
            int foundpos = notfoundparam;

            // search for estate manager.
            for (i = 0; i < testateManagers.Length; i++)
            {
                if (testateManagers[i] == avatarID)
                {
                    foundpos = i;
                    break;
                }
            }
            if (foundpos < notfoundparam)
            {
                LLUUID[] restateManagers = new LLUUID[testateManagers.Length - 1];

                // fill new estate managers array up to the found spot
                for (int j = 0; j < foundpos; j++)
                    restateManagers[j] = testateManagers[j];

                // skip over the estate manager we're removing and compress
                for (int j = foundpos + 1; j < testateManagers.Length; j++)
                    restateManagers[j - 1] = testateManagers[j];

                estateManagers = restateManagers;
            }
            else
            {
                m_log.Error("[ESTATESETTINGS]: Unable to locate estate manager : " + avatarID.ToString() + " for removal");
            }
        }

        #endregion

        public void loadConfigurationOptions()
        {
            configMember.addConfigurationOption("billable_factor", ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, String.Empty,
                                                "0.0", true);
            configMember.addConfigurationOption("estate_id", ConfigurationOption.ConfigurationTypes.TYPE_UINT32, String.Empty, "100",
                                                true);
            configMember.addConfigurationOption("parent_estate_id", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                String.Empty, "1", true);
            configMember.addConfigurationOption("max_agents", ConfigurationOption.ConfigurationTypes.TYPE_BYTE, String.Empty, "40",
                                                true);

            configMember.addConfigurationOption("object_bonus_factor", ConfigurationOption.ConfigurationTypes.TYPE_FLOAT,
                                                String.Empty, "1.0", true);
            configMember.addConfigurationOption("redirect_grid_x", ConfigurationOption.ConfigurationTypes.TYPE_INT32, String.Empty,
                                                "0", true);
            configMember.addConfigurationOption("redirect_grid_y", ConfigurationOption.ConfigurationTypes.TYPE_INT32, String.Empty,
                                                "0", true);
            configMember.addConfigurationOption("region_flags", ConfigurationOption.ConfigurationTypes.TYPE_UINT32, String.Empty,
                                                "336723974", true); //Taken from a Linden sim for the moment.
            configMember.addConfigurationOption("sim_access", ConfigurationOption.ConfigurationTypes.TYPE_BYTE, String.Empty, "21",
                                                true);
            configMember.addConfigurationOption("sun_hour", ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, String.Empty, "0",
                                                true);
            configMember.addConfigurationOption("terrain_raise_limit", ConfigurationOption.ConfigurationTypes.TYPE_FLOAT,
                                                String.Empty, "4.0", true); //4 is the LL default
            configMember.addConfigurationOption("terrain_lower_limit", ConfigurationOption.ConfigurationTypes.TYPE_FLOAT,
                                                String.Empty, "-4.0", true); //-4.0 is the LL default
            configMember.addConfigurationOption("use_fixed_sun", ConfigurationOption.ConfigurationTypes.TYPE_BOOLEAN, String.Empty,
                                                "false", true);
            configMember.addConfigurationOption("price_per_meter", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                String.Empty, "1", true);
            configMember.addConfigurationOption("region_water_height",
                                                ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, String.Empty, "20", true);
            configMember.addConfigurationOption("region_allow_terraform",
                                                ConfigurationOption.ConfigurationTypes.TYPE_BOOLEAN, String.Empty, "true", true);

            configMember.addConfigurationOption("terrain_base_0", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID, String.Empty,
                                                "b8d3965a-ad78-bf43-699b-bff8eca6c975", true);
            configMember.addConfigurationOption("terrain_base_1", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID, String.Empty,
                                                "abb783e6-3e93-26c0-248a-247666855da3", true);
            configMember.addConfigurationOption("terrain_base_2", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID, String.Empty,
                                                "179cdabd-398a-9b6b-1391-4dc333ba321f", true);
            configMember.addConfigurationOption("terrain_base_3", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID, String.Empty,
                                                "beb169c7-11ea-fff2-efe5-0f24dc881df2", true);

            configMember.addConfigurationOption("terrain_detail_0", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                String.Empty, "00000000-0000-0000-0000-000000000000", true);
            configMember.addConfigurationOption("terrain_detail_1", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                String.Empty, "00000000-0000-0000-0000-000000000000", true);
            configMember.addConfigurationOption("terrain_detail_2", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                String.Empty, "00000000-0000-0000-0000-000000000000", true);
            configMember.addConfigurationOption("terrain_detail_3", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                String.Empty, "00000000-0000-0000-0000-000000000000", true);

            configMember.addConfigurationOption("terrain_start_height_0",
                                                ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, String.Empty, "10.0", true);
            configMember.addConfigurationOption("terrain_start_height_1",
                                                ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, String.Empty, "10.0", true);
            configMember.addConfigurationOption("terrain_start_height_2",
                                                ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, String.Empty, "10.0", true);
            configMember.addConfigurationOption("terrain_start_height_3",
                                                ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, String.Empty, "10.0", true);

            configMember.addConfigurationOption("terrain_height_range_0",
                                                ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, String.Empty, "60.0", true);
            configMember.addConfigurationOption("terrain_height_range_1",
                                                ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, String.Empty, "60.0", true);
            configMember.addConfigurationOption("terrain_height_range_2",
                                                ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, String.Empty, "60.0", true);
            configMember.addConfigurationOption("terrain_height_range_3",
                                                ConfigurationOption.ConfigurationTypes.TYPE_FLOAT, String.Empty, "60.0", true);

            configMember.addConfigurationOption("terrain_file",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, String.Empty,
                                                "default.r32", true);
            configMember.addConfigurationOption("terrain_multiplier", ConfigurationOption.ConfigurationTypes.TYPE_FLOAT,
                                                String.Empty, "60.0", true);
            configMember.addConfigurationOption("water_height", ConfigurationOption.ConfigurationTypes.TYPE_DOUBLE, String.Empty,
                                                "20.0", true);
            configMember.addConfigurationOption("terrain_image_id", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                String.Empty, "00000000-0000-0000-0000-000000000000", true);

            configMember.addConfigurationOption("estate_name", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                String.Empty, "TestEstate", true);
            configMember.addConfigurationOption("estate_manager_0", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                String.Empty, "00000000-0000-0000-0000-000000000000", true);
            configMember.addConfigurationOption("estate_manager_1", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                String.Empty, "00000000-0000-0000-0000-000000000000", true);
            configMember.addConfigurationOption("estate_manager_2", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                String.Empty, "00000000-0000-0000-0000-000000000000", true);
            configMember.addConfigurationOption("estate_manager_3", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                String.Empty, "00000000-0000-0000-0000-000000000000", true);
            configMember.addConfigurationOption("estate_manager_4", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                String.Empty, "00000000-0000-0000-0000-000000000000", true);
            configMember.addConfigurationOption("estate_manager_5", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                String.Empty, "00000000-0000-0000-0000-000000000000", true);
            configMember.addConfigurationOption("estate_manager_6", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                String.Empty, "00000000-0000-0000-0000-000000000000", true);
            configMember.addConfigurationOption("estate_manager_7", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                String.Empty, "00000000-0000-0000-0000-000000000000", true);
            configMember.addConfigurationOption("estate_manager_8", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                String.Empty, "00000000-0000-0000-0000-000000000000", true);
            configMember.addConfigurationOption("estate_manager_9", ConfigurationOption.ConfigurationTypes.TYPE_LLUUID,
                                                String.Empty, "00000000-0000-0000-0000-000000000000", true);
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
                    m_regionWaterHeight = (float) configuration_result;
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
                case "estate_name":
                    m_estateName = (string) configuration_result;
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

                case "estate_manager_0":
                    m_estateManager0 = (LLUUID) configuration_result;
                    break;
                case "estate_manager_1":
                    m_estateManager1 = (LLUUID) configuration_result;
                    break;
                case "estate_manager_2":
                    m_estateManager2 = (LLUUID) configuration_result;
                    break;
                case "estate_manager_3":
                    m_estateManager3 = (LLUUID) configuration_result;
                    break;
                case "estate_manager_4":
                    m_estateManager4 = (LLUUID) configuration_result;
                    break;
                case "estate_manager_5":
                    m_estateManager5 = (LLUUID) configuration_result;
                    break;
                case "estate_manager_6":
                    m_estateManager6 = (LLUUID) configuration_result;
                    break;
                case "estate_manager_7":
                    m_estateManager7 = (LLUUID) configuration_result;
                    break;
                case "estate_manager_8":
                    m_estateManager8 = (LLUUID) configuration_result;
                    break;
                case "estate_manager_9":
                    m_estateManager9 = (LLUUID) configuration_result;
                    break;
            }

            return true;
        }
    }
}
