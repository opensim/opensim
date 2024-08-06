using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class UserAlias
{
    public int Id { get; set; }

    public string AliasId { get; set; }

    public string UserId { get; set; }

    public string Description { get; set; }
}
