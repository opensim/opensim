using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class OsGroupsGroup
{
    public string GroupId { get; set; }

    public string Location { get; set; }

    public string Name { get; set; }

    public string Charter { get; set; }

    public string InsigniaId { get; set; }

    public string FounderId { get; set; }

    public int MembershipFee { get; set; }

    public string OpenEnrollment { get; set; }

    public int ShowInList { get; set; }

    public int AllowPublish { get; set; }

    public int MaturePublish { get; set; }

    public string OwnerRoleId { get; set; }
}
