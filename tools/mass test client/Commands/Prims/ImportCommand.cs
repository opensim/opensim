using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using System.Threading;
using System.IO;
using libsecondlife;

namespace libsecondlife.TestClient
{
    enum ImporterState
    {
        RezzingParent,
        RezzingChildren,
        Linking,
        Idle
    }

    public class Linkset
    {
        public Primitive RootPrim;
        public List<Primitive> Children = new List<Primitive>();

        public Linkset()
        {
            RootPrim = new Primitive();
        }

        public Linkset(Primitive rootPrim)
        {
            RootPrim = rootPrim;
        }
    }

    public class ImportCommand : Command
    {
        Primitive currentPrim;
        LLVector3 currentPosition;
        SecondLife currentClient;
        AutoResetEvent primDone;
        List<Primitive> primsCreated;
        List<uint> linkQueue;
        uint rootLocalID = 0;
        bool registeredCreateEvent = false;

        ImporterState state = ImporterState.Idle;

        public ImportCommand(TestClient testClient)
        {
            Name = "import";
            Description = "Import prims from an exported xml file. Usage: import inputfile.xml";
            primDone = new AutoResetEvent(false);
            registeredCreateEvent = false;
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            if (args.Length != 1)
                return "Usage: import inputfile.xml";

            string filename = args[0];
            Dictionary<uint, Primitive> prims;

            currentClient = Client;

            try
            {
                XmlReader reader = XmlReader.Create(filename);
                List<Primitive> listprims = Helpers.PrimListFromXml(reader);
                reader.Close();

                // Create a dictionary indexed by the old local ID of the prims
                prims = new Dictionary<uint, Primitive>();
                foreach (Primitive prim in listprims)
                {
                    prims.Add(prim.LocalID, prim);
                }
            }
            catch (Exception)
            {
                return "Failed to import the object XML file, maybe it doesn't exist or is in the wrong format?";
            }

            if (!registeredCreateEvent)
            {
                Client.OnPrimCreated += new TestClient.PrimCreatedCallback(TestClient_OnPrimCreated);
                registeredCreateEvent = true;
            }

            // Build an organized structure from the imported prims
            Dictionary<uint, Linkset> linksets = new Dictionary<uint, Linkset>();
            foreach (Primitive prim in prims.Values)
            {
                if (prim.ParentID == 0)
                {
                    if (linksets.ContainsKey(prim.LocalID))
                        linksets[prim.LocalID].RootPrim = prim;
                    else
                        linksets[prim.LocalID] = new Linkset(prim);
                }
                else
                {
                    if (!linksets.ContainsKey(prim.ParentID))
                        linksets[prim.ParentID] = new Linkset();

                    linksets[prim.ParentID].Children.Add(prim);
                }
            }

            primsCreated = new List<Primitive>();
            Console.WriteLine("Importing " + linksets.Count + " structures.");

            foreach (Linkset linkset in linksets.Values)
            {
                if (linkset.RootPrim.LocalID != 0)
                {
                    state = ImporterState.RezzingParent;
                    currentPrim = linkset.RootPrim;
                    // HACK: Offset the root prim position so it's not lying on top of the original
                    // We need a more elaborate solution for importing with relative or absolute offsets
                    linkset.RootPrim.Position = Client.Self.Position;
                    linkset.RootPrim.Position.Z += 3.0f;
                    currentPosition = linkset.RootPrim.Position;
                    // A better solution would move the bot to the desired position.
                    // or to check if we are within a certain distance of the desired position.

                    // Rez the root prim with no rotation
                    LLQuaternion rootRotation = linkset.RootPrim.Rotation;
                    linkset.RootPrim.Rotation = LLQuaternion.Identity;

                    Client.Objects.AddPrim(Client.Network.CurrentSim, linkset.RootPrim.Data, LLUUID.Zero,
                        linkset.RootPrim.Position, linkset.RootPrim.Scale, linkset.RootPrim.Rotation);

                    if (!primDone.WaitOne(10000, false))
                        return "Rez failed, timed out while creating the root prim.";

                    state = ImporterState.RezzingChildren;

                    // Rez the child prims
                    foreach (Primitive prim in linkset.Children)
                    {
                        currentPrim = prim;
                        currentPosition = prim.Position + linkset.RootPrim.Position;

                        Client.Objects.AddPrim(Client.Network.CurrentSim, prim.Data, LLUUID.Zero, currentPosition,
                            prim.Scale, prim.Rotation);

                        if (!primDone.WaitOne(10000, false))
                            return "Rez failed, timed out while creating child prim.";
                    }

                    if (linkset.Children.Count != 0)
                    {
                        // Create a list of the local IDs of the newly created prims
                        List<uint> primIDs = new List<uint>(primsCreated.Count);
                        primIDs.Add(rootLocalID); // Root prim is first in list.
                        foreach (Primitive prim in primsCreated)
                        {
                            if (prim.LocalID != rootLocalID)
                                primIDs.Add(prim.LocalID);
                        }
                        linkQueue = new List<uint>(primIDs.Count);
                        linkQueue.AddRange(primIDs);

                        // Link and set the permissions + rotation
                        state = ImporterState.Linking;
                        Client.Objects.LinkPrims(Client.Network.CurrentSim, linkQueue);
                        if (primDone.WaitOne(100000 * linkset.Children.Count, false))
                        {
                            Client.Objects.SetPermissions(Client.Network.CurrentSim, primIDs,
                                Helpers.PermissionWho.Everyone | Helpers.PermissionWho.Group | Helpers.PermissionWho.NextOwner,
                                Helpers.PermissionType.Copy | Helpers.PermissionType.Modify | Helpers.PermissionType.Move |
                                Helpers.PermissionType.Transfer, true);

                            Client.Objects.SetRotation(Client.Network.CurrentSim, rootLocalID, rootRotation);
                        }
                        else
                        {
                            Console.WriteLine("Warning: Failed to link {0} prims", linkQueue.Count);
                        }
                    }
                    else
                    {
                        Client.Objects.SetRotation(Client.Network.CurrentSim, rootLocalID, rootRotation);
                    }
                    state = ImporterState.Idle;
                }
                else
                {
                    // Skip linksets with a missing root prim
                    Console.WriteLine("WARNING: Skipping a linkset with a missing root prim");
                }

                // Reset everything for the next linkset
                primsCreated.Clear();
            }

            return "Import complete.";
        }

        void TestClient_OnPrimCreated(Simulator simulator, Primitive prim)
        {
            if ((prim.Flags & LLObject.ObjectFlags.CreateSelected) == 0)
                return; // We received an update for an object we didn't create

            switch (state)
            {
                case ImporterState.RezzingParent:
                    rootLocalID = prim.LocalID;
                    goto case ImporterState.RezzingChildren;
                case ImporterState.RezzingChildren:
                    if (!primsCreated.Contains(prim))
                    {
                        Console.WriteLine("Setting properties for " + prim.LocalID);
                        // TODO: Is there a way to set all of this at once, and update more ObjectProperties stuff?
                        currentClient.Objects.SetPosition(simulator, prim.LocalID, currentPosition);
                        currentClient.Objects.SetTextures(simulator, prim.LocalID, currentPrim.Textures);
                        currentClient.Objects.SetLight(simulator, prim.LocalID, currentPrim.Light);
                        currentClient.Objects.SetFlexible(simulator, prim.LocalID, currentPrim.Flexible);

                        if (!String.IsNullOrEmpty(currentPrim.Properties.Name))
                            currentClient.Objects.SetName(simulator, prim.LocalID, currentPrim.Properties.Name);
                        if (!String.IsNullOrEmpty(currentPrim.Properties.Description))
                            currentClient.Objects.SetDescription(simulator, prim.LocalID, 
                                currentPrim.Properties.Description);

                        primsCreated.Add(prim);
                        primDone.Set();
                    }
                    break;
                case ImporterState.Linking:
                    lock (linkQueue)
                    {
                        int index = linkQueue.IndexOf(prim.LocalID);
                        if (index != -1)
                        {
                            linkQueue.RemoveAt(index);
                            if (linkQueue.Count == 0)
                                primDone.Set();
                        }
                    }
                    break;
            }
        }
    }
}
