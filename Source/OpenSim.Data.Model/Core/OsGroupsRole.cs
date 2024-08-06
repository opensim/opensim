using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class OsGroupsRole
{
    public string GroupId { get; set; }

    public string RoleId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string Title { get; set; }

    public ulong Powers { get; set; }
}
