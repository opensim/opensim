using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Search;

public partial class Parcel
{
    public string ParcelUuid { get; set; }

    public string RegionUuid { get; set; }

    public string Parcelname { get; set; }

    public string Landingpoint { get; set; }

    public string Description { get; set; }

    public string Searchcategory { get; set; }

    public string Build { get; set; }

    public string Script { get; set; }

    public string Public { get; set; }

    public float Dwell { get; set; }

    public string Infouuid { get; set; }

    public string Mature { get; set; }

    public string GatekeeperUrl { get; set; }

    public string ImageUuid { get; set; }
}
