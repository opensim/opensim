using System;
using System.Collections.Generic;

using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

using OpenMetaverse;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface IInventoryAccessModule
    {
        UUID CapsUpdateInventoryItemAsset(IClientAPI remoteClient, UUID itemID, byte[] data);
        UUID DeleteToInventory(DeRezAction action, UUID folderID, SceneObjectGroup objectGroup, IClientAPI remoteClient);
        SceneObjectGroup RezObject(IClientAPI remoteClient, UUID itemID, Vector3 RayEnd, Vector3 RayStart,
                                    UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
                                    bool RezSelected, bool RemoveItem, UUID fromTaskID, bool attachment);
        void TransferInventoryAssets(InventoryItemBase item, UUID sender, UUID receiver);
    }
}
