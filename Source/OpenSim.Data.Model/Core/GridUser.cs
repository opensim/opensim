using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class GridUser
{
    public string UserId { get; set; }

    public string HomeRegionId { get; set; }

    public string HomePosition { get; set; }

    public string HomeLookAt { get; set; }

    public string LastRegionId { get; set; }

    public string LastPosition { get; set; }

    public string LastLookAt { get; set; }

    public string Online { get; set; }

    public string Login { get; set; }

    public string Logout { get; set; }
}
