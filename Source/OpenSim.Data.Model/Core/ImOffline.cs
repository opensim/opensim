using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class ImOffline
{
    public int Id { get; set; }

    public string PrincipalId { get; set; }

    public string FromId { get; set; }

    public string Message { get; set; }

    public DateTime Tmstamp { get; set; }
}
