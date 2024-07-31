using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class Inventoryitem
{
    public string AssetId { get; set; }

    public int? AssetType { get; set; }

    public string InventoryName { get; set; }

    public string InventoryDescription { get; set; }

    public uint? InventoryNextPermissions { get; set; }

    public uint? InventoryCurrentPermissions { get; set; }

    public int? InvType { get; set; }

    public string CreatorId { get; set; }

    public uint InventoryBasePermissions { get; set; }

    public uint InventoryEveryOnePermissions { get; set; }

    public int SalePrice { get; set; }

    public sbyte SaleType { get; set; }

    public int CreationDate { get; set; }

    public string GroupId { get; set; }

    public sbyte GroupOwned { get; set; }

    public uint Flags { get; set; }

    public string InventoryId { get; set; }

    public string AvatarId { get; set; }

    public string ParentFolderId { get; set; }

    public uint InventoryGroupPermissions { get; set; }
}
