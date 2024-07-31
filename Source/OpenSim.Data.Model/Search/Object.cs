using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Search;

public partial class Object
{
    public string Objectuuid { get; set; }

    public string Parceluuid { get; set; }

    public string Location { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string Regionuuid { get; set; }

    public string GatekeeperUrl { get; set; }
}
