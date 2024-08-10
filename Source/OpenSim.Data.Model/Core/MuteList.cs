using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class MuteList
{
    public string AgentId { get; set; }

    public string MuteId { get; set; }

    public string MuteName { get; set; }

    public int MuteType { get; set; }

    public int MuteFlags { get; set; }

    public int Stamp { get; set; }
}
