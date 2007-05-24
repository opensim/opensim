using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Console
{    
    public interface conscmd_callback
    {
        void RunCmd(string cmd, string[] cmdparams);
        void Show(string ShowWhat);
    }
}
