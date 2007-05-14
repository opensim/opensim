using System;
using System.Diagnostics;
using System.Threading;
using System.ServiceProcess;
using System.Xml;
using System.IO;

public class OpenGridMasterService : System.ServiceProcess.ServiceBase {

	private Thread ServiceWorkerThread;

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
 		Process p = new Process();

		p.StartInfo.Arguments = "-setuponly"; 
		p.StartInfo.FileName  = "OpenGridServices.GridServer.exe";
 		p.Start();

		return "<gridserver />";	// we let the gridserver handle it's own setup
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
