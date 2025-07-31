using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Search;

public partial class Event
{
    public string Owneruuid { get; set; }

    public string Name { get; set; }

    public uint Eventid { get; set; }

    public string Creatoruuid { get; set; }

    public int Category { get; set; }

    public string Description { get; set; }

    public int DateUtc { get; set; }

    public int Duration { get; set; }

    public bool Covercharge { get; set; }

    public int Coveramount { get; set; }

    public string Simname { get; set; }

    public string ParcelUuid { get; set; }

    public string GlobalPos { get; set; }

    public int Eventflags { get; set; }

    public string GatekeeperUrl { get; set; }
}
