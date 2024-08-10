using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Identity;

public partial class IdentityUserToken
{
    public string UserId { get; set; }

    public string LoginProvider { get; set; }

    public string Name { get; set; }

    public string Value { get; set; }

    public virtual IdentityUser User { get; set; }
}
