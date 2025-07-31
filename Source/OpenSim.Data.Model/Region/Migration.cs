using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Region;

public partial class Migration
{
    public string Name { get; set; }

    public int? Version { get; set; }
}
