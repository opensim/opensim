using System;

namespace OpenSim._32BitLaunch
{
    class Program
    {
        static void Main(string[] args)
        {
            System.Console.WriteLine("32-bit OpenSim executor");
            System.Console.WriteLine("-----------------------");
            System.Console.WriteLine("");
            System.Console.WriteLine("This application is compiled for 32-bit CPU and will run under WOW32 or similar.");
            System.Console.WriteLine("All 64-bit incompatibilities should be gone.");
            System.Console.WriteLine("");
            System.Threading.Thread.Sleep(300);
            try
            {
                OpenSim.Application.Main(args);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("OpenSim threw an exception:");
                System.Console.WriteLine(ex.ToString());
                System.Console.WriteLine("");
                System.Console.WriteLine("Application will now terminate!");
                System.Console.WriteLine("");
            }
        }
    }
}
