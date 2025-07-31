using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Identity;

public partial class IdentityRole
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string NormalizedName { get; set; }

    public string ConcurrencyStamp { get; set; }

    public virtual ICollection<IdentityRoleClaim> IdentityRoleClaims { get; set; } = new List<IdentityRoleClaim>();

    public virtual ICollection<IdentityUser> Users { get; set; } = new List<IdentityUser>();
}
