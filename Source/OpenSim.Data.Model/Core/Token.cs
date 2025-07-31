using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class Token
{
    public string Uuid { get; set; }

    public string Token1 { get; set; }

    public DateTime Validity { get; set; }
}
