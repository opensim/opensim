using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Search;

public partial class Allparcel
{
    public string RegionUuid { get; set; }

    public string Parcelname { get; set; }

    public string OwnerUuid { get; set; }

    public string GroupUuid { get; set; }

    public string Landingpoint { get; set; }

    public string ParcelUuid { get; set; }

    public string InfoUuid { get; set; }

    public int Parcelarea { get; set; }

    public string GatekeeperUrl { get; set; }
}
