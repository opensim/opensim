using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class Migration
{
    public string Name { get; set; }

    public int? Version { get; set; }
}
