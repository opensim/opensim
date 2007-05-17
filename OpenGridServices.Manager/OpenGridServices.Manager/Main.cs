// project created on 5/14/2007 at 2:04 PM
using System;
using System.Threading;
using Gtk;

namespace OpenGridServices.Manager
{
	class MainClass
	{

		public static bool QuitReq=false;
		public static BlockingQueue<string> PendingOperations = new BlockingQueue<string>();

		private static Thread OperationsRunner;
		
		private static GridServerConnectionManager gridserverConn;
		
		private static MainWindow win;

		public static void DoMainLoop()
		{
			while(!QuitReq)
			{
				Application.RunIteration();
			}
		}

		public static void RunOperations()
		{
			string operation;
			string cmd;
			char[] sep = new char[1];
			sep[0]=' ';
			while(!QuitReq)
			{
				operation=PendingOperations.Dequeue();
				Console.WriteLine(operation);
				cmd = operation.Split(sep)[0];
				switch(cmd) {
					case "connect_to_gridserver":
						win.SetStatus("Connecting to grid server...");						
						gridserverConn.Connect(operation.Split(sep)[1],operation.Split(sep)[2],operation.Split(sep)[3]);
					break;
				}
			}
		}

		public static void Main (string[] args)
		{
			gridserverConn = new GridServerConnectionManager();
			Application.Init ();
			win = new MainWindow ();
			win.Show ();
			OperationsRunner = new Thread(new ThreadStart(RunOperations));
			OperationsRunner.IsBackground=true;
			OperationsRunner.Start();
			DoMainLoop();
		}
	}
}