using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class UserAccount
{
    public string PrincipalId { get; set; }

    public string ScopeId { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string Email { get; set; }

    public string ServiceUrls { get; set; }

    public int? Created { get; set; }

    public int UserLevel { get; set; }

    public int UserFlags { get; set; }

    public string UserTitle { get; set; }

    public int Active { get; set; }
}
