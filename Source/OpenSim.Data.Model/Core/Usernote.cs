using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class Usernote
{
    public string Useruuid { get; set; }

    public string Targetuuid { get; set; }

    public string Notes { get; set; }
}
