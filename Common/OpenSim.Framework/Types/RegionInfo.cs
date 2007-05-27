using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Console;
using libsecondlife;

namespace OpenSim.Framework.Types
{
    public class RegionInfo
    {
        public LLUUID SimUUID;
        public string RegionName;
        public uint RegionLocX;
        public uint RegionLocY;
        public ulong RegionHandle;
        public ushort RegionWaterHeight = 20;
        public bool RegionTerraform = true;

        public int IPListenPort;
        public string IPListenAddr;

        private bool isSandbox;
        public string DataStore;

        // Region Information
        // Low resolution 'base' textures. No longer used.
        public LLUUID TerrainBase0 = new LLUUID("b8d3965a-ad78-bf43-699b-bff8eca6c975"); // Default
        public LLUUID TerrainBase1 = new LLUUID("abb783e6-3e93-26c0-248a-247666855da3"); // Default
        public LLUUID TerrainBase2 = new LLUUID("179cdabd-398a-9b6b-1391-4dc333ba321f"); // Default
        public LLUUID TerrainBase3 = new LLUUID("beb169c7-11ea-fff2-efe5-0f24dc881df2"); // Default
        // Higher resolution terrain textures
        public LLUUID TerrainDetail0 = new LLUUID("00000000-0000-0000-0000-000000000000");
        public LLUUID TerrainDetail1 = new LLUUID("00000000-0000-0000-0000-000000000000");
        public LLUUID TerrainDetail2 = new LLUUID("00000000-0000-0000-0000-000000000000");
        public LLUUID TerrainDetail3 = new LLUUID("00000000-0000-0000-0000-000000000000");
        // First quad - each point is bilinearly interpolated at each meter of terrain
        public float TerrainStartHeight00 = 10.0f;       // NW Corner ( I think )
        public float TerrainStartHeight01 = 10.0f;       // NE Corner ( I think )
        public float TerrainStartHeight10 = 10.0f;       // SW Corner ( I think )
        public float TerrainStartHeight11 = 10.0f;       // SE Corner ( I think )
        // Second quad - also bilinearly interpolated.
        // Terrain texturing is done that:
        // 0..3 (0 = base0, 3 = base3) = (terrain[x,y] - start[x,y]) / range[x,y]
        public float TerrainHeightRange00 = 60.0f;
        public float TerrainHeightRange01 = 60.0f;
        public float TerrainHeightRange10 = 60.0f;
        public float TerrainHeightRange11 = 60.0f;

        // Terrain Default (Must be in F32 Format!)
        public string TerrainFile = "default.r32";
        public double TerrainMultiplier = 60.0;

        public RegionInfo()
        {

        }


        public void InitConfig(bool sandboxMode, IGenericConfig configData)
        {
            this.isSandbox = sandboxMode;
            try
            {
                // Sim UUID
                string attri = "";
                attri = configData.GetAttribute("SimUUID");
                if (attri == "")
                {
                    this.SimUUID = LLUUID.Random();
                    configData.SetAttribute("SimUUID", this.SimUUID.ToString());
                }
                else
                {
                    this.SimUUID = new LLUUID(attri);
                }

                // Sim name
                attri = "";
                attri = configData.GetAttribute("SimName");
                if (attri == "")
                {
                    this.RegionName = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Name", "OpenSim test");
                    configData.SetAttribute("SimName", this.RegionName);
                }
                else
                {
                    this.RegionName = attri;
                }
                // Sim/Grid location X
                attri = "";
                attri = configData.GetAttribute("SimLocationX");
                if (attri == "")
                {
                    string location = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Grid Location X", "997");
                    configData.SetAttribute("SimLocationX", location);
                    this.RegionLocX = (uint)Convert.ToUInt32(location);
                }
                else
                {
                    this.RegionLocX = (uint)Convert.ToUInt32(attri);
                }
                // Sim/Grid location Y
                attri = "";
                attri = configData.GetAttribute("SimLocationY");
                if (attri == "")
                {
                    string location = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Grid Location Y", "996");
                    configData.SetAttribute("SimLocationY", location);
                    this.RegionLocY = (uint)Convert.ToUInt32(location);
                }
                else
                {
                    this.RegionLocY = (uint)Convert.ToUInt32(attri);
                }

                // Local storage datastore
                attri = "";
                attri = configData.GetAttribute("Datastore");
                if (attri == "")
                {
                    string datastore = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Filename for local storage", "localworld.yap");
                    configData.SetAttribute("Datastore", datastore);
                    this.DataStore = datastore;
                }
                else
                {
                    this.DataStore = attri;
                }

                //Sim Listen Port
                attri = "";
                attri = configData.GetAttribute("SimListenPort");
                if (attri == "")
                {
                    string port = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("UDP port for client connections", "9000");
                    configData.SetAttribute("SimListenPort", port);
                    this.IPListenPort = Convert.ToInt32(port);
                }
                else
                {
                    this.IPListenPort = Convert.ToInt32(attri);
                }

                //Sim Listen Address
                attri = "";
                attri = configData.GetAttribute("SimListenAddress");
                if (attri == "")
                {
                    this.IPListenAddr = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("IP Address to listen on for client connections", "127.0.0.1");
                    configData.SetAttribute("SimListenAddress", this.IPListenAddr);
                }
                else
                {
                    // Probably belongs elsewhere, but oh well.
                    if (attri.Trim().StartsWith("SYSTEMIP"))
                    {
                        string localhostname = System.Net.Dns.GetHostName();
                        System.Net.IPAddress[] ips = System.Net.Dns.GetHostAddresses(localhostname);
                        try
                        {
                            this.IPListenAddr = ips[0].ToString();
                        }
                        catch (Exception e)
                        {
                            e.ToString();
                            this.IPListenAddr = "127.0.0.1"; // Use the default if we fail
                        }
                    }
                    else
                    {
                        this.IPListenAddr = attri;
                    }
                }

               
                this.RegionHandle = Util.UIntsToLong((RegionLocX * 256), (RegionLocY * 256));
               
                configData.Commit();
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM,"Config.cs:InitConfig() - Exception occured");
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM,e.ToString());
            }

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Sim settings loaded:");
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "UUID: " + this.SimUUID.ToStringHyphenated());
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Name: " + this.RegionName);
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Region Location: [" + this.RegionLocX.ToString() + "," + this.RegionLocY + "]");
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Region Handle: " + this.RegionHandle.ToString());
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Listening on IP: " + this.IPListenAddr + ":" + this.IPListenPort);
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Sandbox Mode? " + isSandbox.ToString());
  
        }
    }
}
