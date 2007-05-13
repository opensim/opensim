using System;
using System.Threading;
using System.ServiceProcess;

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
		
		
	}

	public static void Main()
	{
	}
}
