using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class SetMasterCommand: Command
    {
		public DateTime Created = DateTime.Now;
        private LLUUID resolvedMasterKey = LLUUID.Zero;
        private ManualResetEvent keyResolution = new ManualResetEvent(false);
        private LLUUID query = LLUUID.Zero;

        public SetMasterCommand(TestClient testClient)
		{
			Name = "setMaster";
			Description = "Sets the user name of the master user.  The master user can IM to run commands.";
            
		}

        public override string Execute(string[] args, LLUUID fromAgentID)
		{
			string masterName = String.Empty;
			for (int ct = 0; ct < args.Length;ct++)
				masterName = masterName + args[ct] + " ";
            masterName = masterName.TrimEnd();

            if (masterName.Length == 0)
                return "Usage setMaster name";

            DirectoryManager.DirPeopleReplyCallback callback = new DirectoryManager.DirPeopleReplyCallback(KeyResolvHandler);
            Client.Directory.OnDirPeopleReply += callback;
            query = Client.Directory.StartPeopleSearch(DirectoryManager.DirFindFlags.People, masterName, 0);
            if (keyResolution.WaitOne(TimeSpan.FromMinutes(1), false))
            {
                Client.MasterKey = resolvedMasterKey;
                keyResolution.Reset();
                Client.Directory.OnDirPeopleReply -= callback;
            }
            else
            {
                keyResolution.Reset();
                Client.Directory.OnDirPeopleReply -= callback;
                return "Unable to obtain UUID for \"" + masterName + "\". Master unchanged.";
            }
            

			foreach (Avatar av in Client.AvatarList.Values)
			{
			    if (av.ID == Client.MasterKey)
			    {
			        Client.Self.InstantMessage(av.ID, "You are now my master.  IM me with \"help\" for a command list.");
			        break;
			    }
			}

            return "Master set to " + masterName + " (" + Client.MasterKey.ToStringHyphenated() + ")";
		}

        private void KeyResolvHandler(LLUUID queryid, List<DirectoryManager.AgentSearchData> matches)
        {
            if (query != queryid)
                return;
            // We can't handle ambiguities here as nicely as we can in ClientManager.
            resolvedMasterKey = matches[0].AgentID;
            keyResolution.Set();
            query = LLUUID.Zero;
        }
    }
}
