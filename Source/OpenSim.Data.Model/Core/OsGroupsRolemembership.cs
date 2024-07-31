using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class OsGroupsRolemembership
{
    public string GroupId { get; set; }

    public string RoleId { get; set; }

    public string PrincipalId { get; set; }
}
