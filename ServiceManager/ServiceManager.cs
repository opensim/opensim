using System;
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

	public static void Main()
	{
		Console.WriteLine("Starting up OGS master service");
		try {
			ServiceBase.Run(new OpenGridMasterService());
		} catch(Exception e) {
			Console.WriteLine("THIS SHOULD NEVER HAPPEN!!!!!!!!!!!!!!!!!!!!!");
			Console.WriteLine(e.ToString());
		}
	}
}
