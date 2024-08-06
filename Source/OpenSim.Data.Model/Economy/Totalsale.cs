using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Economy;

/// <summary>
/// Rev.3
/// </summary>
public partial class Totalsale
{
    public string Uuid { get; set; }

    public string User { get; set; }

    public string ObjectUuid { get; set; }

    public int Type { get; set; }

    public int TotalCount { get; set; }

    public int TotalAmount { get; set; }

    public int Time { get; set; }
}
