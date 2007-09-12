using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace OpenSim.GUI
{
    public class ProcessManager : Process
    {
        private string m_FileName;
        private string m_Arguments;
        public ProcessManager(string FileName,string Arguments)
        {
            m_FileName = FileName;
            m_Arguments = Arguments;

//                            MyProc = new Process();
                StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                Console.WriteLine("WorkingDirectory: " + StartInfo.WorkingDirectory);
                StartInfo.FileName = m_FileName;

                //p.StartInfo.Arguments = "";
                StartInfo.UseShellExecute = false;
                StartInfo.RedirectStandardError = true;
                StartInfo.RedirectStandardInput = true;
                StartInfo.RedirectStandardOutput = true;
                StartInfo.CreateNoWindow = true;



        }

        public void StartProcess()
        {
            try
            {
                Start();
                BeginOutputReadLine();
                BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception Occurred :{0},{1}",
                          ex.Message, ex.StackTrace.ToString());
            }
        }
        public void StopProcess()
        {
            try
            {
                CancelErrorRead();
                CancelErrorRead();
                if (!HasExited)
                {
                    StandardInput.WriteLine("quit");
                    StandardInput.WriteLine("shutdown");
                    System.Threading.Thread.Sleep(500);
                    if (!HasExited)
                    {
                        Kill();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception Occurred :{0},{1}",
                          ex.Message, ex.StackTrace.ToString());
            }
        }
    }
}
