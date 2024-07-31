using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Region;

public partial class Primitem
{
    public int? InvType { get; set; }

    public int? AssetType { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public long? CreationDate { get; set; }

    public int? NextPermissions { get; set; }

    public int? CurrentPermissions { get; set; }

    public int? BasePermissions { get; set; }

    public int? EveryonePermissions { get; set; }

    public int? GroupPermissions { get; set; }

    public int Flags { get; set; }

    public string ItemId { get; set; }

    public string PrimId { get; set; }

    public string AssetId { get; set; }

    public string ParentFolderId { get; set; }

    public string CreatorId { get; set; }

    public string OwnerId { get; set; }

    public string GroupId { get; set; }

    public string LastOwnerId { get; set; }
}
