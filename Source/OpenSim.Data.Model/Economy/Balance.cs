using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Economy;

/// <summary>
/// Rev.4
/// </summary>
public partial class Balance
{
    public string User { get; set; }

    public int Balance1 { get; set; }

    public sbyte? Status { get; set; }

    public sbyte Type { get; set; }
}
