using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Search;

public partial class Hostsregister
{
    public string Host { get; set; }

    public int Port { get; set; }

    public int Register { get; set; }

    public int Nextcheck { get; set; }

    public bool Checked { get; set; }

    public int Failcounter { get; set; }

    public string GatekeeperUrl { get; set; }
}
