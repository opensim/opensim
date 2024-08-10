using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class Friend
{
    public string PrincipalId { get; set; }

    public string Friend1 { get; set; }

    public string Flags { get; set; }

    public string Offered { get; set; }
}
