using System;
using System.Diagnostics;
using System.Threading;
using System.ServiceProcess;
using System.Xml;
using System.IO;
using libsecondlife;
using OpenSim.GenericConfig;

public class OpenGridMasterService : System.ServiceProcess.ServiceBase {

	private Thread ServiceWorkerThread;
	private static string GridURL;		// URL of grid server
	private static string GridSimKey;	// key sent from Grid>Sim
	private static string SimGridKey;	// key sent Sim>Grid
	private static string AssetURL;		// URL of asset server
	private static string UserSendKey;	// key sent from user>sim
	private static string UserRecvKey;	// key sent from sim>user

	public OpenGridMasterService()
	{
		CanPauseAndContinue = false;
		ServiceName = "OpenGridServices-master";
	}

	private void InitializeComponent()
	{
		this.CanPauseAndContinue = false;
		this.CanShutdown = true;
		this.ServiceName = "OpenGridServices-master";
	}

	protected override void OnStart(string[] args)
	{
		ServiceWorkerThread = new Thread(new ThreadStart(MainServiceThread));		
		ServiceWorkerThread.Start();
	}

	protected override void OnStop()
	{
		ServiceWorkerThread.Abort();
	}

	private void MainServiceThread()
	{
	 try {	
	    StreamReader reader=new  StreamReader("opengrid-master-cfg.xml");
	    
	    string configxml = reader.ReadToEnd();
	    XmlDocument doc = new XmlDocument();
            doc.LoadXml(configxml);
            XmlNode rootnode = doc.FirstChild;
            if (rootnode.Name != "regions")
            {
                EventLog.WriteEntry("ERROR! bad XML in opengrid-master-cfg.xml - expected regions tag");
       		Console.WriteLine("Sorry, could not startup the service - please check your opengrid-master-cfg.xml file: missing regions tag");
		(new ServiceController("OpenGridServices-master")).Stop();
	    }

	    for(int i=0; i<=rootnode.ChildNodes.Count; i++)
	    {
	   	if(rootnode.ChildNodes.Item(i).Name != "region") {
			EventLog.WriteEntry("nonfatal error - unexpected tag inside regions block of opengrid-master-cfg.xml");
			(new ServiceController("OpenGridServices-master")).Stop();
		}
	    }
	 } catch(Exception e) {
	    Console.WriteLine(e.ToString());
	    (new ServiceController("OpenGridServices-master")).Stop();
	 }
	
	}

	private static string SetupGrid()
	{
		Console.WriteLine("Running external program (OpenGridServices.GridServer.exe) to configure the grid server");
 		try {
			Process p = new Process();

			p.StartInfo.Arguments = "-setuponly"; 
			p.StartInfo.FileName  = "OpenGridServices.GridServer.exe";
	 		p.Start();

			p.StartInfo.Arguments = "-dumpxmlconf";
			p.Start();

			XmlConfig GridConf = new XmlConfig("opengrid-cfg.xml");
			GridConf.LoadData();
			GridURL="http://" + GridConf.GetAttribute("ListenAddr") + ":" + GridConf.GetAttribute("ListenPort") + "/";

                        StreamReader reader=new  StreamReader("opengrid-cfg.xml");
	                string configxml = reader.ReadToEnd();
	
			return configxml;
		} catch(Exception e) {
			Console.WriteLine("An error occurred while running the grid server, please rectify it and try again");
			Console.WriteLine(e.ToString());
			Environment.Exit(1);
		}
		return "";
	}

	private static string SetupUser()
	{
		return "<user></user>";
	}

	private static string SetupAsset()
	{
		return "<asset></asset>";
	}

	private static string SetupRegion()
	{
		string regionname;
		ulong regionlocx;
		ulong regionlocy;
		string default_terrain;
		uint terrain_multiplier;
		uint baseport;

		string listenaddr;
		string simconfigxml;
		LLUUID SimUUID;

		Console.WriteLine("Setting up region servers");
		Console.Write("Please specify a path to store your region data (e.g /etc/opensim/regions: ");
		string regionpath=Console.ReadLine();
		
		Console.Write("How many regions would you like to configure now? ");
		int numofregions=Convert.ToInt16(Console.ReadLine());	

		Console.Write("What port should the region servers start listening at (first region is normally 9000, then 9001 the second etc, both TCP+UDP): ");
		baseport=Convert.ToUInt16(Console.ReadLine());	

		
		listenaddr=Console.ReadLine();
		
		Console.WriteLine("Now ready to configure regions, please answer the questions about each region in turn");
		for(int i=0; i<=numofregions; i++) {
			Console.WriteLine("Configuring region number " + i.ToString());
			
			Console.Write("Region name: ");
			regionname=Console.ReadLine();
			
			Console.Write("Region location X: ");
			regionlocx=(ulong)Convert.ToUInt32(Console.ReadLine());
	
			Console.Write("Region location Y: ");
			regionlocy=(ulong)Convert.ToUInt32(Console.ReadLine());

			Console.Write("Default terrain file: ");
			default_terrain=Console.ReadLine();
			terrain_multiplier=Convert.ToUInt16(Console.ReadLine());

			SimUUID=LLUUID.Random();
	
			simconfigxml="<Root><Config SimUUID=\"" + SimUUID.ToString() + "\" SimName=\"" + regionname + "\" SimLocationX=\"" + regionlocx.ToString() + "\" SimLocationY=\"" + regionlocy.ToString() + "\" Datastore=\"" + Path.Combine(regionpath,(SimUUID.ToString()+"localworld.yap")) + "\" SimListenPort=\"" + (baseport+i).ToString() + "\" SimListenAddress=\"" + listenaddr + "\" TerrainFile=\"" + default_terrain + "\" TerrainMultiplier=\"" + terrain_multiplier.ToString() + "\" GridServerURL=\"\" GridSendKey=\"\" GridRecvKey=\"\" AssetServerURL=\"\" /></Root>";
	
		}

		return "<regions></regions>";
	}

	public static void InitSetup()
	{
		string choice="";
		
		string GridInfo;
		string UserInfo;
		string AssetInfo;
		string RegionInfo;

		bool grid=false;
		bool user=false;
		bool asset=false;
		bool region=false;
		while(choice!="OK")
		{
	                Console.Clear();
        	        Console.WriteLine("Please select the components you would like to run on this server:\n");

	                Console.WriteLine("1 - [" + (grid ? "X" : " ") + "] Grid server   - this service handles co-ordinates of regions/sims on the grid");
        	        Console.WriteLine("2 - [" + (user ? "X" : " ") + "] User server   - this service handles user login, profiles, inventory and IM");
                	Console.WriteLine("3 - [" + (asset ? "X" : " ") + "] Asset server  - this service handles storage of assets such as textures, objects, sounds, scripts");
                	Console.WriteLine("4 - [" + (region ? "X" : " ") + "] Region server - this is the main opensim server and can run without the above services, it handles physics simulation, terrain, building and other such features");
		

			Console.Write("Type a number to toggle a choice or type OK to accept your current choices: ");
			choice = Console.ReadLine();
			switch(choice)
			{
				case "1":
					grid = (!grid);
				break;

				case "2":
					user = (!user);
				break;

				case "3":
					asset = (!asset);
				break;

				case "4":
					region = (!region);
				break;
			}
		}

		if(grid) GridInfo     = SetupGrid();
		if(user) UserInfo     = SetupUser();
		if(asset) AssetInfo   = SetupAsset();
		if(region) RegionInfo = SetupRegion();
	}	

	public static void Main()
	{
	        if(!File.Exists("opengrid-master-cfg.xml")) 
		{
			Console.WriteLine("Could not find a config file, running initial setup");
			InitSetup();
		}
		Console.WriteLine("Starting up OGS master service");
		try {
			ServiceBase.Run(new OpenGridMasterService());
		} catch(Exception e) {
			Console.WriteLine("THIS SHOULD NEVER HAPPEN!!!!!!!!!!!!!!!!!!!!!");
			Console.WriteLine(e.ToString());
		}
	}
}
