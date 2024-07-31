using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Search;

public partial class Parcelsale
{
    public string RegionUuid { get; set; }

    public string Parcelname { get; set; }

    public string ParcelUuid { get; set; }

    public int Area { get; set; }

    public int Saleprice { get; set; }

    public string Landingpoint { get; set; }

    public string InfoUuid { get; set; }

    public int Dwell { get; set; }

    public int Parentestate { get; set; }

    public string Mature { get; set; }

    public string GatekeeperUrl { get; set; }
}
