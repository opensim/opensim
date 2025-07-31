using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class Fsasset
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public int Type { get; set; }

    public string Hash { get; set; }

    public int CreateTime { get; set; }

    public int AccessTime { get; set; }

    public int AssetFlags { get; set; }
}
