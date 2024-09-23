using OpenMetaverse;
using OpenSim.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface IDisplayNameModule
    {
        public string GetDisplayName(UUID avatar);
    }
}
