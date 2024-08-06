using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class GloebitUser
{
    public string AppKey { get; set; }

    public string PrincipalId { get; set; }

    public string GloebitId { get; set; }

    public string GloebitToken { get; set; }

    public string LastSessionId { get; set; }
}
