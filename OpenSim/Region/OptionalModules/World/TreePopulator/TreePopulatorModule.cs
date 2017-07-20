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
using System.IO;
using System.Reflection;
using System.Timers;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

using OpenMetaverse;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Framework.InterfaceCommander;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using Timer= System.Timers.Timer;

namespace OpenSim.Region.OptionalModules.World.TreePopulator
{
    /// <summary>
    /// Version 2.02 - Still hacky
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "TreePopulatorModule")]
    public class TreePopulatorModule : INonSharedRegionModule, ICommandableModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Commander m_commander = new Commander("tree");
        private Scene m_scene;

        [XmlRootAttribute(ElementName = "Copse", IsNullable = false)]
        public class Copse
        {
            public string m_name;
            public Boolean m_frozen;
            public Tree m_tree_type;
            public int m_tree_quantity;
            public float m_treeline_low;
            public float m_treeline_high;
            public Vector3 m_seed_point;
            public double m_range;
            public Vector3 m_initial_scale;
            public Vector3 m_maximum_scale;
            public Vector3 m_rate;

            [XmlIgnore]
            public Boolean m_planted;
            [XmlIgnore]
            public List<UUID> m_trees;

            public Copse()
            {
            }

            public Copse(string fileName, Boolean planted)
            {
                Copse cp = (Copse)DeserializeObject(fileName);

                m_name = cp.m_name;
                m_frozen = cp.m_frozen;
                m_tree_quantity = cp.m_tree_quantity;
                m_treeline_high = cp.m_treeline_high;
                m_treeline_low = cp.m_treeline_low;
                m_range = cp.m_range;
                m_tree_type = cp.m_tree_type;
                m_seed_point = cp.m_seed_point;
                m_initial_scale = cp.m_initial_scale;
                m_maximum_scale = cp.m_maximum_scale;
                m_initial_scale = cp.m_initial_scale;
                m_rate = cp.m_rate;
                m_planted = planted;
                m_trees = new List<UUID>();
            }

            public Copse(string copsedef)
            {
                char[] delimiterChars = {':', ';'};
                string[] field = copsedef.Split(delimiterChars);

                m_name = field[1].Trim();
                m_frozen = (copsedef[0] == 'F');
                m_tree_quantity = int.Parse(field[2]);
                m_treeline_high = float.Parse(field[3], Culture.NumberFormatInfo);
                m_treeline_low = float.Parse(field[4], Culture.NumberFormatInfo);
                m_range = double.Parse(field[5], Culture.NumberFormatInfo);
                m_tree_type = (Tree) Enum.Parse(typeof(Tree),field[6]);
                m_seed_point = Vector3.Parse(field[7]);
                m_initial_scale = Vector3.Parse(field[8]);
                m_maximum_scale = Vector3.Parse(field[9]);
                m_rate = Vector3.Parse(field[10]);
                m_planted = true;
                m_trees = new List<UUID>();
            }

            public Copse(string name, int quantity, float high, float low, double range, Vector3 point, Tree type, Vector3 scale, Vector3 max_scale, Vector3 rate, List<UUID> trees)
            {
                m_name = name;
                m_frozen = false;
                m_tree_quantity = quantity;
                m_treeline_high = high;
                m_treeline_low = low;
                m_range = range;
                m_tree_type = type;
                m_seed_point = point;
                m_initial_scale = scale;
                m_maximum_scale = max_scale;
                m_rate = rate;
                m_planted = false;
                m_trees = trees;
            }

            public override string ToString()
            {
                string frozen = (m_frozen ? "F" : "A");

                return string.Format("{0}TPM: {1}; {2}; {3:0.0}; {4:0.0}; {5:0.0}; {6}; {7:0.0}; {8:0.0}; {9:0.0}; {10:0.00};",
                    frozen,
                    m_name,
                    m_tree_quantity,
                    m_treeline_high,
                    m_treeline_low,
                    m_range,
                    m_tree_type,
                    m_seed_point.ToString(),
                    m_initial_scale.ToString(),
                    m_maximum_scale.ToString(),
                    m_rate.ToString());
            }
        }

        private List<Copse> m_copses = new List<Copse>();
        private object mylock;
        private double m_update_ms = 1000.0; // msec between updates
        private bool m_active_trees = false;
        private bool m_enabled = true; // original default
        private bool m_allowGrow = true; // original default

        Timer CalculateTrees;

        #region ICommandableModule Members

        public ICommander CommandInterface
        {
            get { return m_commander; }
        }

        #endregion

        #region Region Module interface

        public void Initialise(IConfigSource config)
        {
            IConfig moduleConfig = config.Configs["Trees"];
            if (moduleConfig != null)
            {
                m_enabled = moduleConfig.GetBoolean("enabled", m_enabled);
                m_active_trees = moduleConfig.GetBoolean("active_trees", m_active_trees);
                m_allowGrow = moduleConfig.GetBoolean("allowGrow", m_allowGrow);
                m_update_ms = moduleConfig.GetDouble("update_rate", m_update_ms);
            }

            if(!m_enabled)
                return;

            m_copses =  new List<Copse>();
            mylock = new object();

            InstallCommands();

            m_log.Debug("[TREES]: Initialised tree populator module");
        }

        public void AddRegion(Scene scene)
        {
            if(!m_enabled)
                return;
            m_scene = scene;
            m_scene.RegisterModuleCommander(m_commander);
            m_scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
            m_scene.EventManager.OnPrimsLoaded += EventManager_OnPrimsLoaded;
        }

        public void RemoveRegion(Scene scene)
        {
            if(!m_enabled)
                return;
            if(m_active_trees && CalculateTrees != null)
            {
                CalculateTrees.Dispose();
                CalculateTrees = null;
            }
            m_scene.EventManager.OnPluginConsole -= EventManager_OnPluginConsole;
            m_scene.EventManager.OnPrimsLoaded -= EventManager_OnPrimsLoaded;
        }   

        public void RegionLoaded(Scene scene)
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "TreePopulatorModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }


        #endregion

        //--------------------------------------------------------------

        private void EventManager_OnPrimsLoaded(Scene s)
        {
            ReloadCopse();
            if (m_copses.Count > 0)
                m_log.Info("[TREES]: Copses loaded" );

            if (m_active_trees)
                activeizeTreeze(true);
        }

        #region ICommandableModule Members

        private void HandleTreeActive(Object[] args)
        {
            if ((Boolean)args[0] && !m_active_trees)
            {
                m_log.InfoFormat("[TREES]: Activating Trees");
                m_active_trees = true;
                activeizeTreeze(m_active_trees);
            }
            else if (!(Boolean)args[0] && m_active_trees)
            {
                m_log.InfoFormat("[TREES]: Trees module is no longer active");
                m_active_trees = false;
                activeizeTreeze(m_active_trees);
            }
            else
            {
                m_log.InfoFormat("[TREES]: Trees module is already in the required state");
            }
        }

        private void HandleTreeFreeze(Object[] args)
        {
            string copsename = ((string)args[0]).Trim();
            Boolean freezeState = (Boolean) args[1];

            lock(mylock)
            {
                foreach (Copse cp in m_copses)
                {
                    if (cp.m_name != copsename)
                        continue;

                    if(!cp.m_frozen && freezeState || cp.m_frozen && !freezeState)
                    {
                        cp.m_frozen = freezeState;
                        List<UUID> losttrees = new List<UUID>();
                        foreach (UUID tree in cp.m_trees)
                        {
                            SceneObjectGroup sog = m_scene.GetSceneObjectGroup(tree);
                            if(sog != null && !sog.IsDeleted)
                            {
                                SceneObjectPart sop = sog.RootPart;
                                string name = sop.Name;
                                if(freezeState)
                                {
                                    if(name.StartsWith("FTPM"))
                                        continue;
                                    if(!name.StartsWith("ATPM"))
                                        continue;
                                    sop.Name = sop.Name.Replace("ATPM", "FTPM");
                                }
                                else
                                {
                                    if(name.StartsWith("ATPM"))
                                        continue;
                                    if(!name.StartsWith("FTPM"))
                                        continue;
                                    sop.Name = sop.Name.Replace("FTPM", "ATPM");
                                }
                                sop.ParentGroup.HasGroupChanged = true;
                                sog.ScheduleGroupForFullUpdate();
                            }
                            else
                               losttrees.Add(tree);
                        }
                        foreach (UUID tree in losttrees)
                            cp.m_trees.Remove(tree);

                        m_log.InfoFormat("[TREES]: Activity for copse {0} is frozen {1}", copsename, freezeState);
                        return;
                    }
                    else
                    {
                        m_log.InfoFormat("[TREES]: Copse {0} is already in the requested freeze state", copsename);
                        return;
                    }
                }
            }
            m_log.InfoFormat("[TREES]: Copse {0} was not found - command failed", copsename);
        }

        private void HandleTreeLoad(Object[] args)
        {
            Copse copse;

            m_log.InfoFormat("[TREES]: Loading copse definition....");

            lock(mylock)
            {
                copse = new Copse(((string)args[0]), false);
                {
                    foreach (Copse cp in m_copses)
                    {
                        if (cp.m_name == copse.m_name)
                        {
                            m_log.InfoFormat("[TREES]: Copse: {0} is already defined - command failed", copse.m_name);
                            return;
                        }
                    }
                }
                m_copses.Add(copse);
            }
            m_log.InfoFormat("[TREES]: Loaded copse: {0}", copse.ToString());
        }

        private void HandleTreePlant(Object[] args)
        {
            string copsename = ((string)args[0]).Trim();

            m_log.InfoFormat("[TREES]: New tree planting for copse {0}", copsename);
            UUID uuid = m_scene.RegionInfo.EstateSettings.EstateOwner;

            lock(mylock)
            {
                foreach (Copse copse in m_copses)
                {
                    if (copse.m_name == copsename)
                    {
                        if (!copse.m_planted)
                        {
                            // The first tree for a copse is created here
                            CreateTree(uuid, copse, copse.m_seed_point, true);
                            copse.m_planted = true;
                            return;
                        }
                        else
                        {
                            m_log.InfoFormat("[TREES]: Copse {0} has already been planted", copsename);
                            return;
                        }
                    }
                }
            }
            m_log.InfoFormat("[TREES]: Copse {0} not found for planting", copsename);
        }

        private void HandleTreeRate(Object[] args)
        {
            m_update_ms = (double)args[0];
            if (m_update_ms >= 1000.0)
            {
                if (m_active_trees)
                {
                    activeizeTreeze(false);
                    activeizeTreeze(true);
                }
                m_log.InfoFormat("[TREES]: Update rate set to {0} mSec", m_update_ms);
            }
            else
            {
                m_log.InfoFormat("[TREES]: minimum rate is 1000.0 mSec - command failed");
            }
        }

        private void HandleTreeReload(Object[] args)
        {
            if (m_active_trees)
            {
                CalculateTrees.Stop();
            }

            ReloadCopse();

            if (m_active_trees)
            {
                CalculateTrees.Start();
            }
        }

        private void HandleTreeRemove(Object[] args)
        {
            string copsename = ((string)args[0]).Trim();
            Copse copseIdentity = null;

            lock(mylock)
            {
                foreach (Copse cp in m_copses)
                {
                    if (cp.m_name == copsename)
                    {
                        copseIdentity = cp;
                    }
                }

                if (copseIdentity != null)
                {
                    foreach (UUID tree in copseIdentity.m_trees)
                    {
                        if (m_scene.Entities.ContainsKey(tree))
                        {
                            SceneObjectPart selectedTree = ((SceneObjectGroup)m_scene.Entities[tree]).RootPart;
                            // Delete tree and alert clients (not silent)
                            m_scene.DeleteSceneObject(selectedTree.ParentGroup, false);
                        }
                        else
                        {
                            m_log.DebugFormat("[TREES]: Tree not in scene {0}", tree);
                        }
                    }
                    copseIdentity.m_trees = null;
                    m_copses.Remove(copseIdentity);
                    m_log.InfoFormat("[TREES]: Copse {0} has been removed", copsename);
                }
                else
                {
                    m_log.InfoFormat("[TREES]: Copse {0} was not found - command failed", copsename);
                }
            }
        }

        private void HandleTreeStatistics(Object[] args)
        {
            m_log.InfoFormat("[TREES]: region {0}:", m_scene.Name);
            m_log.InfoFormat("[TREES]:    Activity State: {0};  Update Rate: {1}", m_active_trees, m_update_ms);
            foreach (Copse cp in m_copses)
            {
                m_log.InfoFormat("[TREES]:    Copse {0}; {1} trees; frozen {2}", cp.m_name, cp.m_trees.Count, cp.m_frozen);
            }
        }

        private void InstallCommands()
        {
            Command treeActiveCommand =
                new Command("active", CommandIntentions.COMMAND_HAZARDOUS, HandleTreeActive, "Change activity state for the trees module");
            treeActiveCommand.AddArgument("activeTF", "The required activity state", "Boolean");

            Command treeFreezeCommand =
                new Command("freeze", CommandIntentions.COMMAND_HAZARDOUS, HandleTreeFreeze, "Freeze/Unfreeze activity for a defined copse");
            treeFreezeCommand.AddArgument("copse", "The required copse", "String");
            treeFreezeCommand.AddArgument("freezeTF", "The required freeze state", "Boolean");

            Command treeLoadCommand =
                new Command("load", CommandIntentions.COMMAND_HAZARDOUS, HandleTreeLoad, "Load a copse definition from an xml file");
            treeLoadCommand.AddArgument("filename", "The (xml) file you wish to load", "String");

            Command treePlantCommand =
                new Command("plant", CommandIntentions.COMMAND_HAZARDOUS, HandleTreePlant, "Start the planting on a copse");
            treePlantCommand.AddArgument("copse", "The required copse", "String");

            Command treeRateCommand =
                new Command("rate", CommandIntentions.COMMAND_HAZARDOUS, HandleTreeRate, "Reset the tree update rate (mSec)");
            treeRateCommand.AddArgument("updateRate", "The required update rate (minimum 1000.0)", "Double");

            Command treeReloadCommand =
                new Command("reload", CommandIntentions.COMMAND_HAZARDOUS, HandleTreeReload, "Reload copses from the in-scene trees");

            Command treeRemoveCommand =
                new Command("remove", CommandIntentions.COMMAND_HAZARDOUS, HandleTreeRemove, "Remove a copse definition and all its in-scene trees");
            treeRemoveCommand.AddArgument("copse", "The required copse", "String");

            Command treeStatisticsCommand =
                new Command("statistics", CommandIntentions.COMMAND_STATISTICAL, HandleTreeStatistics, "Log statistics about the trees");

            m_commander.RegisterCommand("active", treeActiveCommand);
            m_commander.RegisterCommand("freeze", treeFreezeCommand);
            m_commander.RegisterCommand("load", treeLoadCommand);
            m_commander.RegisterCommand("plant", treePlantCommand);
            m_commander.RegisterCommand("rate", treeRateCommand);
            m_commander.RegisterCommand("reload", treeReloadCommand);
            m_commander.RegisterCommand("remove", treeRemoveCommand);
            m_commander.RegisterCommand("statistics", treeStatisticsCommand);
        }

        /// <summary>
        /// Processes commandline input. Do not call directly.
        /// </summary>
        /// <param name="args">Commandline arguments</param>
        private void EventManager_OnPluginConsole(string[] args)
        {
            if (args[0] == "tree")
            {
                if (args.Length == 1)
                {
                    m_commander.ProcessConsoleCommand("help", new string[0]);
                    return;
                }

                string[] tmpArgs = new string[args.Length - 2];
                int i;
                for (i = 2; i < args.Length; i++)
                {
                    tmpArgs[i - 2] = args[i];
                }

                m_commander.ProcessConsoleCommand(args[1], tmpArgs);
            }
        }
        #endregion

        #region IVegetationModule Members

        public SceneObjectGroup AddTree(
            UUID uuid, UUID groupID, Vector3 scale, Quaternion rotation, Vector3 position, Tree treeType, bool newTree)
        {
            PrimitiveBaseShape treeShape = new PrimitiveBaseShape();
            treeShape.PathCurve = 16;
            treeShape.PathEnd = 49900;
            treeShape.PCode = newTree ? (byte)PCode.NewTree : (byte)PCode.Tree;
            treeShape.Scale = scale;
            treeShape.State = (byte)treeType;

            SceneObjectGroup sog = new SceneObjectGroup(uuid, position, rotation, treeShape);
            SceneObjectPart rootPart = sog.RootPart;

            rootPart.AddFlag(PrimFlags.Phantom);

            sog.SetGroup(groupID, null);
            m_scene.AddNewSceneObject(sog, true, false);
            sog.IsSelected = false;
            rootPart.IsSelected = false;
            sog.InvalidateEffectivePerms();
            return sog;
        }

        #endregion

        //--------------------------------------------------------------

        #region Tree Utilities
        static public void SerializeObject(string fileName, Object obj)
        {
            try
            {
                XmlSerializer xs = new XmlSerializer(typeof(Copse));

                using (XmlTextWriter writer = new XmlTextWriter(fileName, Util.UTF8))
                {
                    writer.Formatting = Formatting.Indented;
                    xs.Serialize(writer, obj);
                }
            }
            catch (SystemException ex)
            {
                throw new ApplicationException("Unexpected failure in Tree serialization", ex);
            }
        }

        static public object DeserializeObject(string fileName)
        {
            try
            {
                XmlSerializer xs = new XmlSerializer(typeof(Copse));

                using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                    return xs.Deserialize(fs);
            }
            catch (SystemException ex)
            {
                throw new ApplicationException("Unexpected failure in Tree de-serialization", ex);
            }
        }

        private void ReloadCopse()
        {
            m_copses = new List<Copse>();

            List<SceneObjectGroup> grps = m_scene.GetSceneObjectGroups();
            foreach (SceneObjectGroup grp in grps)
            {
                if(grp.RootPart.Shape.PCode != (byte)PCode.NewTree && grp.RootPart.Shape.PCode != (byte)PCode.Tree)
                    continue;

                if (grp.Name.Length > 5 && (grp.Name.Substring(0, 5) == "ATPM:" || grp.Name.Substring(0, 5) == "FTPM:"))
                {
                    // Create a new copse definition or add uuid to an existing definition
                    try
                    {
                        Boolean copsefound = false;
                        Copse grpcopse = new Copse(grp.Name);

                        lock(mylock)
                        {
                            foreach (Copse cp in m_copses)
                            {
                                if (cp.m_name == grpcopse.m_name)
                                {
                                    copsefound = true;
                                    cp.m_trees.Add(grp.UUID);
                                    //m_log.DebugFormat("[TREES]: Found tree {0}", grp.UUID);
                                }
                            }

                            if (!copsefound)
                            {
                                m_log.InfoFormat("[TREES]: adding copse {0}", grpcopse.m_name);
                                grpcopse.m_trees.Add(grp.UUID);
                                m_copses.Add(grpcopse);
                            }
                        }
                    }
                    catch
                    {
                        m_log.InfoFormat("[TREES]: Ill formed copse definition {0} - ignoring", grp.Name);
                    }
                }
            }
        }
        #endregion

        private void activeizeTreeze(bool activeYN)
        {
            if (activeYN)
            {
                if(CalculateTrees == null)
                    CalculateTrees = new Timer(m_update_ms);
                CalculateTrees.Elapsed += CalculateTrees_Elapsed;
                CalculateTrees.AutoReset = false;
                CalculateTrees.Start();
            }
            else
            {
                 CalculateTrees.Stop();
            }
        }

        private void growTrees()
        {
            if(!m_allowGrow)
                return;

            foreach (Copse copse in m_copses)
            {
                if (copse.m_frozen)
                    continue;

                if(copse.m_trees.Count == 0)
                    continue;

                float maxscale = copse.m_maximum_scale.Z;
                float ratescale = 1.0f;
                List<UUID> losttrees = new List<UUID>();
                foreach (UUID tree in copse.m_trees)
                {
                    SceneObjectGroup sog = m_scene.GetSceneObjectGroup(tree);

                    if (sog != null && !sog.IsDeleted)
                    {
                        SceneObjectPart s_tree = sog.RootPart;
                        if (s_tree.Scale.Z < maxscale)
                        {
                            ratescale = (float)Util.RandomClass.NextDouble();
                            if(ratescale < 0.2f)
                                ratescale = 0.2f;
                            s_tree.Scale += copse.m_rate * ratescale;
                            sog.HasGroupChanged = true;
                            s_tree.ScheduleFullUpdate();
                        }
                    }
                    else
                        losttrees.Add(tree);
                }

                foreach (UUID tree in losttrees)
                    copse.m_trees.Remove(tree);
            }
        }

        private void seedTrees()
        {
            foreach (Copse copse in m_copses)
            {
                if (copse.m_frozen)
                    continue;

                if(copse.m_trees.Count == 0)
                    return;

                bool low = copse.m_trees.Count < (int)(copse.m_tree_quantity * 0.8f);

                if (!low && Util.RandomClass.NextDouble() < 0.75)
                    return;

                int maxbirths =  (int)(copse.m_tree_quantity) - copse.m_trees.Count;
                if(maxbirths <= 1)
                    return;

                if(maxbirths > 20)
                    maxbirths = 20;

                float minscale = 0;
                if(!low && m_allowGrow)
                    minscale = copse.m_maximum_scale.Z * 0.75f;;

                int i = 0;
                UUID[] current = copse.m_trees.ToArray();
                while(--maxbirths > 0)
                {
                    if(current.Length > 1)
                        i = Util.RandomClass.Next(current.Length -1);

                    UUID tree = current[i];
                    SceneObjectGroup sog = m_scene.GetSceneObjectGroup(tree);

                    if (sog != null && !sog.IsDeleted)
                    {
                        SceneObjectPart s_tree = sog.RootPart;

                        // Tree has grown enough to seed if it has grown by at least 25% of seeded to full grown height
                        if (s_tree.Scale.Z > minscale)
                                SpawnChild(copse, s_tree, true);
                    }
                    else if(copse.m_trees.Contains(tree))
                        copse.m_trees.Remove(tree);
                }                   
            }
        }

        private void killTrees()
        {
            foreach (Copse copse in m_copses)
            {
                if (copse.m_frozen)
                    continue;

                if (Util.RandomClass.NextDouble() < 0.25)
                    return;

                int maxbdeaths = copse.m_trees.Count - (int)(copse.m_tree_quantity * .98f) ;
                if(maxbdeaths < 1)
                    return;

                float odds;
                float scale = 1.0f / copse.m_maximum_scale.Z;

                int ntries = maxbdeaths * 4;
                while(ntries-- > 0 )
                {
                    int next = 0;
                    if (copse.m_trees.Count > 1)
                        next = Util.RandomClass.Next(copse.m_trees.Count - 1);
                    UUID tree = copse.m_trees[next];
                    SceneObjectGroup sog = m_scene.GetSceneObjectGroup(tree);
                    if (sog != null && !sog.IsDeleted)
                    {
                        if(m_allowGrow)
                        {
                            odds = sog.RootPart.Scale.Z * scale;
                            odds = odds * odds * odds;
                            odds *= (float)Util.RandomClass.NextDouble();
                        }
                        else
                        {
                            odds = (float)Util.RandomClass.NextDouble();
                            odds = odds * odds * odds;
                        }

                        if(odds > 0.9f)
                        {
                            m_scene.DeleteSceneObject(sog, false);
                            if(maxbdeaths <= 0)
                                break;
                        }
                    }            
                    else
                    {
                        copse.m_trees.Remove(tree);
                        if(copse.m_trees.Count - (int)(copse.m_tree_quantity * .98f) <= 0 )
                            break;
                    }
                }
            }
        }

        private void SpawnChild(Copse copse, SceneObjectPart s_tree, bool low)
        {
            Vector3 position = new Vector3();
           
            float randX = copse.m_maximum_scale.X * 1.25f;
            float randY = copse.m_maximum_scale.Y * 1.25f;
            
            float r = (float)Util.RandomClass.NextDouble();
            randX *=  2.0f * r - 1.0f;
            position.X = s_tree.AbsolutePosition.X + (float)randX;
            
            r = (float)Util.RandomClass.NextDouble();
            randY *=  2.0f * r - 1.0f;
            position.Y = s_tree.AbsolutePosition.Y + (float)randY;

            if (position.X > (m_scene.RegionInfo.RegionSizeX - 1) || position.X <= 0 ||
                position.Y > (m_scene.RegionInfo.RegionSizeY - 1) || position.Y <= 0)
                return;

            randX = position.X - copse.m_seed_point.X;
            randX *= randX;
            randY = position.Y - copse.m_seed_point.Y;
            randY *= randY;
            randX += randY;

            if(randX > copse.m_range * copse.m_range)
                return;

            UUID uuid = m_scene.RegionInfo.EstateSettings.EstateOwner;
            CreateTree(uuid, copse, position, low);
        }

        private void CreateTree(UUID uuid, Copse copse, Vector3 position, bool randomScale)
        {
            position.Z = (float)m_scene.Heightmap[(int)position.X, (int)position.Y];
            if (position.Z < copse.m_treeline_low || position.Z > copse.m_treeline_high)
                return;

            Vector3 scale = copse.m_initial_scale;
            if(randomScale)
            {
                try
                {
                    float t;
                    float r = (float)Util.RandomClass.NextDouble();
                    r *= (float)Util.RandomClass.NextDouble();
                    r *= (float)Util.RandomClass.NextDouble();

                    t = copse.m_maximum_scale.X / copse.m_initial_scale.X;
                    if(t < 1.0)
                        t = 1 / t;
                    t = t * r + 1.0f;
                    scale.X *= t;

                    t = copse.m_maximum_scale.Y / copse.m_initial_scale.Y;
                    if(t < 1.0)
                        t = 1 / t;
                    t = t * r + 1.0f;
                    scale.Y *= t;

                    t = copse.m_maximum_scale.Z / copse.m_initial_scale.Z;
                    if(t < 1.0)
                        t = 1 / t;
                    t = t * r + 1.0f;
                    scale.Z *= t;
                }
                catch
                {
                    scale = copse.m_initial_scale;
                }
            }

            SceneObjectGroup tree = AddTree(uuid, UUID.Zero, scale, Quaternion.Identity, position, copse.m_tree_type, false);
            tree.Name = copse.ToString();
            copse.m_trees.Add(tree.UUID);
            tree.RootPart.ScheduleFullUpdate();
        }

        private void CalculateTrees_Elapsed(object sender, ElapsedEventArgs e)
        {
            if(!m_scene.IsRunning)
                return;
            
            if(Monitor.TryEnter(mylock))
            {
                try
                {
                    if(m_scene.LoginsEnabled )
                    {
                        growTrees();
                        seedTrees();
                        killTrees();
                    }
                }
                catch { }
                if(CalculateTrees != null)
                    CalculateTrees.Start();
                Monitor.Exit(mylock);
            }
        }
    }
}

