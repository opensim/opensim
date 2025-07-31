using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Identity;

public partial class IdentityUser
{
    public string Id { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public byte[] ProfilePicture { get; set; }

    public string UserName { get; set; }

    public string NormalizedUserName { get; set; }

    public string Email { get; set; }

    public string NormalizedEmail { get; set; }

    public bool EmailConfirmed { get; set; }

    public string PasswordHash { get; set; }

    public string SecurityStamp { get; set; }

    public string ConcurrencyStamp { get; set; }

    public string PhoneNumber { get; set; }

    public bool PhoneNumberConfirmed { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public DateTime? LockoutEnd { get; set; }

    public bool LockoutEnabled { get; set; }

    public int AccessFailedCount { get; set; }

    public virtual ICollection<IdentityUserClaim> IdentityUserClaims { get; set; } = new List<IdentityUserClaim>();

    public virtual ICollection<IdentityUserLogin> IdentityUserLogins { get; set; } = new List<IdentityUserLogin>();

    public virtual ICollection<IdentityUserToken> IdentityUserTokens { get; set; } = new List<IdentityUserToken>();

    public virtual ICollection<IdentityRole> Roles { get; set; } = new List<IdentityRole>();
}
