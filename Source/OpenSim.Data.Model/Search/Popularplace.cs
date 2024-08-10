using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Search;

public partial class Popularplace
{
    public string ParcelUuid { get; set; }

    public string Name { get; set; }

    public float Dwell { get; set; }

    public string InfoUuid { get; set; }

    public bool HasPicture { get; set; }

    public string Mature { get; set; }

    public string GatekeeperUrl { get; set; }
}
