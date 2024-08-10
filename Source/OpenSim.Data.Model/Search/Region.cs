using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Search;

public partial class Region
{
    public string Regionname { get; set; }

    public string RegionUuid { get; set; }

    public string Regionhandle { get; set; }

    public string Url { get; set; }

    public string Owner { get; set; }

    public string Owneruuid { get; set; }

    public string GatekeeperUrl { get; set; }
}
