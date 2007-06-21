using System;
using System.Collections.Generic;
using System.IO;
using CommandLine.Utility;

namespace libsecondlife.TestClient
{
    public class CommandLineArgumentsException : Exception
    {
    }
    
    public class Program
    {

        private static void Usage()
        {
            Console.WriteLine("Usage: " + Environment.NewLine +
                    "MassTestClient.exe --first \"firstname\" --last \"lastname\" --pass \"password\" --contact \"youremail\" [--startpos \"sim/x/y/z\"] [--master \"master name\"] [--masterkey \"master uuid\"] [--loginuri \"loginuri\"] [--masscommandfile \"filename\"]" +
                    Environment.NewLine + Environment.NewLine + "MassTestClient.exe --loginfile \"filename\" --contact \"youremail\" [--master \"master name\"] [--masterkey \"master uuid\"] [--loginuri \"loginuri\"] [--masscommandfile \"filename\"]");
            Console.ReadLine();
        }

        private static List<string> getMassTestCommands()
        {
            List<string> givenCommands = new List<string>();
            Console.WriteLine("Please enter mass test commands to run in an infinite loop. Press enter to end the current command. Entering a blank command represents that you are done.");
            Console.WriteLine("");

            int curCommand = 0;
            string lastCommand = "NULL";
            while (lastCommand.Length > 0)
            {
                Console.Write("Command #" + curCommand + ">");
                lastCommand = Console.ReadLine().Trim();
                if (lastCommand.Length > 0)
                {
                    givenCommands.Add(lastCommand);
                    curCommand++;
                }
            }
            
            return givenCommands;
        }

        static void Main(string[] args)
        {
            Arguments arguments = new Arguments(args);

            ClientManager manager;
            List<LoginDetails> accounts = new List<LoginDetails>();
            LoginDetails account;
            string masterName = String.Empty;
            LLUUID masterKey = LLUUID.Zero;
            string file = String.Empty;
			string contact = String.Empty;
            string loginURI = "https://login.agni.lindenlab.com/cgi-bin/login.cgi";
            try
            {
                if (arguments["masterkey"] != null)
                {
                    masterKey = LLUUID.Parse(arguments["masterkey"]);
                }

                if (arguments["master"] != null)
                {
                    masterName = arguments["master"];
                }

                if (arguments["contact"] == null)
                    throw new CommandLineArgumentsException();

                contact = arguments["contact"];

                if (arguments["file"] != null)
                {
                    file = arguments["file"];

                    // Loading names from a file
                    try
                    {
                        using (StreamReader reader = new StreamReader(file))
                        {
                            string line;
                            int lineNumber = 0;

                            while ((line = reader.ReadLine()) != null)
                            {
                                lineNumber++;
                                string[] tokens = line.Trim().Split(new char[] { ' ', ',' });

                                if (tokens.Length >= 3)
                                {
                                    account = new LoginDetails();
                                    account.FirstName = tokens[0];
                                    account.LastName = tokens[1];
                                    account.Password = tokens[2];

                                    accounts.Add(account);

                                    // Leaving this out until we have per-account masters (if that
                                    // is desirable). For now the command-line option can 
                                    // specify the single master that TestClient supports

                                    //if (tokens.Length == 5)
                                    //{
                                    //    master = tokens[3] + " " + tokens[4];
                                    //}
                                }
                                else
                                {
                                    Console.WriteLine("Invalid data on line " + lineNumber +
                                        ", must be in the format of: FirstName LastName Password");
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error reading from " + args[1]);
                        Console.WriteLine(e.ToString());
                        Console.ReadLine();
                        return;
                    }
                }
                else if (arguments["first"] != null && arguments["last"] != null && arguments["pass"] != null)
                {
                    // Taking a single login off the command-line
                    account = new LoginDetails();
                    account.FirstName = arguments["first"];
                    account.LastName = arguments["last"];
                    account.Password = arguments["pass"];

                    accounts.Add(account);
                }
                else
                {
                    throw new CommandLineArgumentsException();
                }
            }

            catch (CommandLineArgumentsException)
            {
                Usage();
                return;
            }

            if(arguments["loginuri"] != null)
            {
                loginURI = arguments["loginuri"];
            }

            List<string> massTestCommands = new List<string>();
            if(arguments["masscommandfile"] != null)
            {
                string massCommandFile = arguments["masscommandfile"];
                try
                {
                    using (StreamReader reader = new StreamReader(massCommandFile))
                    {
                        string line;

                        while ((line = reader.ReadLine()) != null)
                        {
                              
                            line = line.Trim();
                            if(line.Length > 0)
                            {
                                massTestCommands.Add(line);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error reading from " + args[1]);
                    Console.WriteLine(e.ToString());
                    Console.ReadLine();
                    return;
                }
            }
            else
            {
                Console.Clear();
                massTestCommands = getMassTestCommands();
            }
            
            Console.Clear();
            if (massTestCommands.Count == 0)
            {
                Console.WriteLine("No mass commands entered; Normal 'TestClient' operation will be used");
            }
            else
            {
                Console.WriteLine("Detected " + massTestCommands.Count + " mass commands; MassTestClient operation will be used");
            }

            foreach (LoginDetails a in accounts)
            {
                a.MasterName = masterName;
                a.MasterKey = masterKey;
                a.LoginURI = loginURI;
            }

            // Login the accounts and run the input loop
            if (arguments["startpos"] != null)
            {
                manager = new ClientManager(accounts, contact, arguments["startpos"]);
            }
            else
            {
                manager = new ClientManager(accounts, contact);
            }


            manager.Run(massTestCommands);
        }
    }
}
