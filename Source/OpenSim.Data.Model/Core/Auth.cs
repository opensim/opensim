using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class Auth
{
    public string Uuid { get; set; }

    public string PasswordHash { get; set; }

    public string PasswordSalt { get; set; }

    public string WebLoginKey { get; set; }

    public string AccountType { get; set; }
}
