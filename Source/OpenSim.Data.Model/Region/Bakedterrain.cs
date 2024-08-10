using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Region;

public partial class Bakedterrain
{
    public string RegionUuid { get; set; }

    public int? Revision { get; set; }

    public byte[] Heightfield { get; set; }
}
