using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class OsGroupsInvite
{
    public string InviteId { get; set; }

    public string GroupId { get; set; }

    public string RoleId { get; set; }

    public string PrincipalId { get; set; }

    public DateTime Tmstamp { get; set; }
}
