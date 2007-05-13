using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using System.Threading;
using libsecondlife;
using libsecondlife.Packets;
using libsecondlife.AssetSystem;

namespace libsecondlife.TestClient
{
    public class LoginDetails
    {
        public string FirstName;
        public string LastName;
        public string Password;
        public string StartLocation;
        public string MasterName;
        public LLUUID MasterKey;
    }

    public class StartPosition
    {
        public string sim;
        public int x;
        public int y;
        public int z;

        public StartPosition()
        {
            this.sim = null;
            this.x = 0;
            this.y = 0;
            this.z = 0;
        }
    }

    public class ClientManager
    {
        public Dictionary<LLUUID, SecondLife> Clients = new Dictionary<LLUUID, SecondLife>();
        public Dictionary<Simulator, Dictionary<uint, Primitive>> SimPrims = new Dictionary<Simulator, Dictionary<uint, Primitive>>();

        public bool Running = true;

	public static SecondLife MainClient;

        string contactPerson = String.Empty;
        private LLUUID resolvedMasterKey = LLUUID.Zero;
        private ManualResetEvent keyResolution = new ManualResetEvent(false);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="accounts"></param>
        public ClientManager(List<LoginDetails> accounts, string c)
        {
            this.contactPerson = c;
            foreach (LoginDetails account in accounts)
                Login(account);
        }

        public ClientManager(List<LoginDetails> accounts, string c, string s)
        {
            this.contactPerson = c;
            char sep = '/';
            string[] startbits = s.Split(sep);

            foreach (LoginDetails account in accounts)
            {
                account.StartLocation = NetworkManager.StartLocation(startbits[0], Int32.Parse(startbits[1]),
                    Int32.Parse(startbits[2]), Int32.Parse(startbits[3]));
                Login(account);
            }
        }

        public string ExportAvatarRestMethod( string request, string path, string param )
        {
		Console.WriteLine("Got a request to export an avatar!");
		DoCommandAll("Executing exportoutfitcommand " + param + " " + param + ".xml", null, null);
		
		MainClient.Self.InstantMessage(new LLUUID(param), "(automated bot message) Your avatar has been copied OK, if you wish to use it to create your account please type yes, otherwise ignore this message. Note that you are responsible for obtaining all copyright permissions for textures etc on your avatar", new LLUUID(param));
		
		return "OK";
	}


        /// <summary>
        /// 
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>
        public TestClient Login(LoginDetails account)
        {
            // Check if this client is already logged in
            foreach (TestClient c in Clients.Values)
            {
                if (c.Self.FirstName == account.FirstName && c.Self.LastName == account.LastName)
                {
                    Logout(c);
                    break;
                }
            }

            TestClient client = new TestClient(this);

            // Optimize the throttle
            client.Throttle.Wind = 0;
            client.Throttle.Cloud = 0;
            client.Throttle.Land = 1000000;
            client.Throttle.Task = 1000000;

			client.SimPrims = SimPrims;
			client.MasterName = account.MasterName;
            client.MasterKey = account.MasterKey;

			if (!String.IsNullOrEmpty(account.StartLocation))
            {
                if (!client.Network.Login(account.FirstName, account.LastName, account.Password, "TestClient",
                    account.StartLocation, contactPerson))
                {
                    Console.WriteLine("Failed to login " + account.FirstName + " " + account.LastName + ": " +
                        client.Network.LoginMessage);
                }
            }
            else
            {
                if (!client.Network.Login(account.FirstName, account.LastName, account.Password, "TestClient",
                    contactPerson))
                {
                    Console.WriteLine("Failed to login " + account.FirstName + " " + account.LastName + ": " +
                        client.Network.LoginStatusMessage);
                }
            }

            if (client.Network.Connected)
            {
                if (account.MasterKey == LLUUID.Zero && !String.IsNullOrEmpty(account.MasterName))
                {
                    Console.WriteLine("Resolving {0}'s UUID", account.MasterName);
                    // Find master's key from name
                    DirectoryManager.DirPeopleReplyCallback callback = new DirectoryManager.DirPeopleReplyCallback(KeyResolvHandler);
                    client.Directory.OnDirPeopleReply += callback;
                    client.Directory.StartPeopleSearch(DirectoryManager.DirFindFlags.People, account.MasterName);
                    if (keyResolution.WaitOne(TimeSpan.FromMinutes(1), false))
                    {
                        account.MasterKey = resolvedMasterKey;
                        Console.WriteLine("\"{0}\" resolved to {1}", account.MasterName, account.MasterKey);
                    }
                    else
                    {
                        Console.WriteLine("Unable to obtain UUID for \"{0}\". No master will be used. Try specifying a key with --masterkey.", account.MasterName);
                    }
                    client.Directory.OnDirPeopleReply -= callback;
                    keyResolution.Reset();
                }

                client.MasterKey = account.MasterKey;

                Clients[client.Network.AgentID] = client;

        	MainClient = client;
	        Console.WriteLine("Logged in " + client.ToString());
            }

            return client;
        }

        private void KeyResolvHandler(LLUUID queryid, List<DirectoryManager.AgentSearchData> matches)
        {
            LLUUID master = matches[0].AgentID;
            if (matches.Count > 1)
            {
                Console.WriteLine("Possible masters:");
                for (int i = 0; i < matches.Count; ++i)
                {
                    Console.WriteLine("{0}: {1}", i, matches[i].FirstName + " " + matches[i].LastName);
                }
                Console.Write("Ambiguous master, choose one:");
                string read = Console.ReadLine();
                while (read != null)
                {
                    int choice = 0;
                    if (int.TryParse(read, out choice))
                    {
                        master = matches[choice].AgentID;
                        break;
                    }
                    else
                    {
                        Console.WriteLine("Responce misunderstood.");
                        Console.Write("Type the corresponding number:");
                    }
                    read = Console.ReadLine();
                }
            }
            resolvedMasterKey = master;
            keyResolution.Set();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public TestClient Login(string[] args)
        {
            LoginDetails account = new LoginDetails();
            account.FirstName = args[0];
            account.LastName = args[1];
            account.Password = args[2];

            if (args.Length == 4)
            {
                account.StartLocation = NetworkManager.StartLocation(args[3], 128, 128, 40);
            }

            return Login(account);
        }

        /// <summary>
        /// 
        /// </summary>
        public void Run()
        {
            Console.WriteLine("Type quit to exit.  Type help for a command list.");

            while (Running)
            {
                PrintPrompt();
                string input = Console.ReadLine();
                DoCommandAll(input, null, null);
            }

            foreach (SecondLife client in Clients.Values)
            {
                if (client.Network.Connected)
                    client.Network.Logout();
            }
        }

        private void PrintPrompt()
        {
            int online = 0;

            foreach (SecondLife client in Clients.Values)
            {
                if (client.Network.Connected) online++;
            }

            Console.Write(online + " avatars online> ");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="fromAgentID"></param>
        /// <param name="imSessionID"></param>
        public void DoCommandAll(string cmd, LLUUID fromAgentID, LLUUID imSessionID)
        {
            string[] tokens = cmd.Trim().Split(new char[] { ' ', '\t' });
            string firstToken = tokens[0].ToLower();

            if (tokens.Length == 0)
                return;

            if (firstToken == "login")
            {
                // Special login case: Only call it once, and allow it with
                // no logged in avatars
                string[] args = new string[tokens.Length - 1];
                Array.Copy(tokens, 1, args, 0, args.Length);
                Login(args);
            }
            else if (firstToken == "quit")
            {
                Quit();
                Console.WriteLine("All clients logged out and program finished running.");
            }
            else
            {
                // make a copy of the clients list so that it can be iterated without fear of being changed during iteration
                Dictionary<LLUUID, SecondLife> clientsCopy = new Dictionary<LLUUID, SecondLife>(Clients);

                foreach (TestClient client in clientsCopy.Values)
                    client.DoCommand(cmd, fromAgentID, imSessionID);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public void Logout(TestClient client)
        {
            Clients.Remove(client.Network.AgentID);
            client.Network.Logout();
        }

        /// <summary>
        /// 
        /// </summary>
        public void LogoutAll()
        {
            // make a copy of the clients list so that it can be iterated without fear of being changed during iteration
            Dictionary<LLUUID, SecondLife> clientsCopy = new Dictionary<LLUUID, SecondLife>(Clients);

            foreach (TestClient client in clientsCopy.Values)
                Logout(client);
        }

        /// <summary>
        /// 
        /// </summary>
        public void Quit()
        {
            LogoutAll();
            Running = false;
            // TODO: It would be really nice if we could figure out a way to abort the ReadLine here in so that Run() will exit.
        }
    }
}
