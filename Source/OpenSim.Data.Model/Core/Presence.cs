using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class Presence
{
    public string UserId { get; set; }

    public string RegionId { get; set; }

    public string SessionId { get; set; }

    public string SecureSessionId { get; set; }

    public DateTime LastSeen { get; set; }
}
