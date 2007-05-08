using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text;
using System.Threading;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class ExportCommand : Command
    {
        AutoResetEvent GotPermissionsEvent = new AutoResetEvent(false);
        LLObject.ObjectPropertiesFamily Properties;
        bool GotPermissions = false;
        LLUUID SelectedObject = LLUUID.Zero;

        Dictionary<LLUUID, Primitive> PrimsWaiting = new Dictionary<LLUUID, Primitive>();
        AutoResetEvent AllPropertiesReceived = new AutoResetEvent(false);

        public ExportCommand(TestClient testClient)
        {
            testClient.Objects.OnObjectPropertiesFamily += new ObjectManager.ObjectPropertiesFamilyCallback(Objects_OnObjectPropertiesFamily);
            testClient.Objects.OnObjectProperties += new ObjectManager.ObjectPropertiesCallback(Objects_OnObjectProperties);
            testClient.Avatars.OnPointAt += new AvatarManager.PointAtCallback(Avatars_OnPointAt);

            Name = "export";
            Description = "Exports an object to an xml file. Usage: export uuid outputfile.xml";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            if (args.Length != 2 && !(args.Length == 1 && SelectedObject != LLUUID.Zero))
                return "Usage: export uuid outputfile.xml";

            LLUUID id;
            uint localid = 0;
            int count = 0;
            string file;

            if (args.Length == 2)
            {
                file = args[1];
                if (!LLUUID.TryParse(args[0], out id))
                    return "Usage: export uuid outputfile.xml";
            }
            else
            {
                file = args[0];
                id = SelectedObject;
            }
            
            lock (Client.SimPrims)
            {
                if (Client.SimPrims.ContainsKey(Client.Network.CurrentSim))
                {
                    foreach (Primitive prim in Client.SimPrims[Client.Network.CurrentSim].Values)
                    {
                        if (prim.ID == id)
                        {
                            if (prim.ParentID != 0)
                                localid = prim.ParentID;
                            else
                                localid = prim.LocalID;

                            break;
                        }
                    }
                }
            }
            
            if (localid != 0)
            {
                // Check for export permission first
                Client.Objects.RequestObjectPropertiesFamily(Client.Network.CurrentSim, id);
                GotPermissionsEvent.WaitOne(8000, false);

                if (!GotPermissions)
                {
                    return "Couldn't fetch permissions for the requested object, try again";
                }
                else
                {
                    GotPermissions = false;
                    if (Properties.OwnerID != Client.Network.AgentID && 
                        Properties.OwnerID != Client.MasterKey && 
                        Client.Network.AgentID != Client.Self.ID)
                    {
                        return "That object is owned by " + Properties.OwnerID + ", we don't have permission " +
                            "to export it";
                    }
                }

                try
                {
					XmlWriterSettings settings = new XmlWriterSettings();
					settings.Indent = true;
                    XmlWriter writer = XmlWriter.Create(file, settings);

					try
					{
                        List<Primitive> prims = new List<Primitive>();

						lock (Client.SimPrims)
						{
							if (Client.SimPrims.ContainsKey(Client.Network.CurrentSim))
							{
                                foreach (Primitive prim in Client.SimPrims[Client.Network.CurrentSim].Values)
								{
									if (prim.LocalID == localid || prim.ParentID == localid)
									{
										prims.Add(prim);
										count++;
									}
								}
							}
						}

                        bool complete = RequestObjectProperties(prims, 250);
						
                        //Serialize it!
						Helpers.PrimListToXml(prims, writer);

                        if (!complete) {
                            Console.WriteLine("Warning: Unable to retrieve full properties for:");
                            foreach (LLUUID uuid in PrimsWaiting.Keys)
                                Console.WriteLine(uuid);
                        }
					}
					finally
					{
						writer.Close();
					}
                }
                catch (Exception e)
                {
                    string ret = "Failed to write to " + file + ":" + e.ToString();
                    if (ret.Length > 1000)
                    {
                        ret = ret.Remove(1000);
                    }
                    return ret;
                }
                return "Exported " + count + " prims to " + file;
            }
            else
            {
                return "Couldn't find UUID " + id.ToString() + " in the " + 
                    Client.SimPrims[Client.Network.CurrentSim].Count + 
                    "objects currently indexed in the current simulator";
            }
        }

        private bool RequestObjectProperties(List<Primitive> objects, int msPerRequest)
        {
            // Create an array of the local IDs of all the prims we are requesting properties for
            uint[] localids = new uint[objects.Count];
            
            lock (PrimsWaiting)
            {
                PrimsWaiting.Clear();

                for (int i = 0; i < objects.Count; ++i)
                {
                    localids[i] = objects[i].LocalID;
                    PrimsWaiting.Add(objects[i].ID, objects[i]);
                }
            }

            Client.Objects.SelectObjects(Client.Network.CurrentSim, localids);

            return AllPropertiesReceived.WaitOne(2000 + msPerRequest * objects.Count, false);
        }

        void Avatars_OnPointAt(LLUUID sourceID, LLUUID targetID, LLVector3d targetPos, 
            MainAvatar.PointAtType pointType, float duration, LLUUID id)
        {
            if (sourceID == Client.MasterKey)
            {
                //Client.DebugLog("Master is now selecting " + targetID.ToStringHyphenated());
                SelectedObject = targetID;
            }
        }

        void Objects_OnObjectPropertiesFamily(Simulator simulator, LLObject.ObjectPropertiesFamily properties)
        {
            Properties = properties;
            GotPermissions = true;
            GotPermissionsEvent.Set();
        }

        void Objects_OnObjectProperties(Simulator simulator, LLObject.ObjectProperties properties)
        {
            lock (PrimsWaiting)
            {
                Primitive prim;
                if (PrimsWaiting.TryGetValue(properties.ObjectID, out prim))
                {
                    prim.Properties = properties;
                }
                PrimsWaiting.Remove(properties.ObjectID);

                if (PrimsWaiting.Count == 0)
                    AllPropertiesReceived.Set();
            }
        }
    }
}
