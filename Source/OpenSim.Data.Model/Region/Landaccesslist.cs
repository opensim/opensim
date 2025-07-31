using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Region;

public partial class Landaccesslist
{
    public string LandUuid { get; set; }

    public string AccessUuid { get; set; }

    public int? Flags { get; set; }

    public int Expires { get; set; }
}
