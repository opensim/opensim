using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Economy;

/// <summary>
/// Rev.3
/// </summary>
public partial class Userinfo
{
    public string User { get; set; }

    public string Simip { get; set; }

    public string Avatar { get; set; }

    public string Pass { get; set; }

    public sbyte Type { get; set; }

    public sbyte Class { get; set; }

    public string Serverurl { get; set; }
}
