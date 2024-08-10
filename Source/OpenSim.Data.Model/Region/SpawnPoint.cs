using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Region;

public partial class SpawnPoint
{
    public string RegionId { get; set; }

    public float Yaw { get; set; }

    public float Pitch { get; set; }

    public float Distance { get; set; }
}
