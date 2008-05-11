using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using IronPython.Hosting;
using log4net;
using Nini.Config;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Modules.Python
{
    class PythonModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private PythonEngine m_python;

        public void Initialise(Scene scene, IConfigSource source)
        {
            
        }

        public void PostInitialise()
        {
            m_log.Info("[PYTHON] Initialising IronPython engine.");
            m_python = new PythonEngine();
            m_python.AddToPath(System.Environment.CurrentDirectory + System.IO.Path.DirectorySeparatorChar + "Python");
        }

        public void Close()
        {
            
        }

        public string Name
        {
            get { return "PythonModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }
    }
}
