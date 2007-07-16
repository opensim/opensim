using System.Collections.Generic;
using System.Net;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Data;
using libsecondlife;
using libsecondlife.Packets;


namespace OpenSim.Framework
{
   public  class NpcClientBase : IClientAPI
   {
#pragma warning disable 67
       public event ImprovedInstantMessage OnInstantMessage;
       public event ChatFromViewer OnChatFromViewer;
       public event RezObject OnRezObject;
       public event ModifyTerrain OnModifyTerrain;
       public event SetAppearance OnSetAppearance;
       public event StartAnim OnStartAnim;
       public event LinkObjects OnLinkObjects;
       public event RequestMapBlocks OnRequestMapBlocks;
       public event TeleportLocationRequest OnTeleportLocationRequest;

       public event GenericCall4 OnDeRezObject;
       public event GenericCall OnRegionHandShakeReply;
       public event GenericCall OnRequestWearables;
       public event GenericCall2 OnCompleteMovementToRegion;
       public event UpdateAgent OnAgentUpdate;
       public event GenericCall OnRequestAvatarsData;
       public event AddNewPrim OnAddPrim;
       public event ObjectDuplicate OnObjectDuplicate;
       public event UpdateVector OnGrapObject;
       public event ObjectSelect OnDeGrapObject;
       public event MoveObject OnGrapUpdate;

       public event UpdateShape OnUpdatePrimShape;
       public event ObjectSelect OnObjectSelect;
       public event GenericCall7 OnObjectDescription;
       public event GenericCall7 OnObjectName;
       public event UpdatePrimFlags OnUpdatePrimFlags;
       public event UpdatePrimTexture OnUpdatePrimTexture;
       public event UpdateVector OnUpdatePrimGroupPosition;
       public event UpdateVector OnUpdatePrimSinglePosition;
       public event UpdatePrimRotation OnUpdatePrimGroupRotation;
       public event UpdatePrimSingleRotation OnUpdatePrimSingleRotation;
       public event UpdatePrimGroupRotation OnUpdatePrimGroupMouseRotation;
       public event UpdateVector OnUpdatePrimScale;
       public event StatusChange OnChildAgentStatus;
       public event GenericCall2 OnStopMovement;
       public event NewAvatar OnNewAvatar;
       public event GenericCall6 OnRemoveAvatar;

       public event UUIDNameRequest OnNameFromUUIDRequest;

       public event ParcelPropertiesRequest OnParcelPropertiesRequest;
       public event ParcelDivideRequest OnParcelDivideRequest;
       public event ParcelJoinRequest OnParcelJoinRequest;
       public event ParcelPropertiesUpdateRequest OnParcelPropertiesUpdateRequest;
       public event ParcelSelectObjects OnParcelSelectObjects;
       public event ParcelObjectOwnerRequest OnParcelObjectOwnerRequest;
       public event ObjectDeselect OnObjectDeselect;


       public event EstateOwnerMessageRequest OnEstateOwnerMessage;
#pragma warning restore 67

       public virtual LLVector3 StartPos
       {
           get { return new LLVector3(); }
           set {  }
       }

       public virtual LLUUID AgentId
       {
           get { return LLUUID.Random(); }
       }

       public virtual string FirstName
       {
           get { return ""; }
       }

       public virtual string LastName
       {
           get { return ""; }
       }

       public NpcClientBase()
       {
       }

       public virtual void OutPacket(Packet newPack){}
       public virtual void SendWearables(AvatarWearable[] wearables){}
       public virtual void SendStartPingCheck(byte seq){}
       public virtual void SendKillObject(ulong regionHandle, uint avatarLocalID){}
       public virtual void SendAnimation(LLUUID animID, int seq, LLUUID sourceAgentId){}
       public virtual void SendRegionHandshake(RegionInfo regionInfo){}
       public virtual void SendChatMessage(string message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID){}
       public virtual void SendChatMessage(byte[] message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID){}
       public virtual void SendInstantMessage(string message, LLUUID target, string fromName){}
       public virtual void SendLayerData(float[] map){}
       public virtual void SendLayerData(int px, int py, float[] map){}
       public virtual void MoveAgentIntoRegion(RegionInfo regInfo, LLVector3 pos, LLVector3 look){}
       public virtual void InformClientOfNeighbour(ulong neighbourHandle, IPEndPoint neighbourExternalEndPoint){}
       public virtual AgentCircuitData RequestClientInfo() { return new AgentCircuitData(); }
       public virtual void CrossRegion(ulong newRegionHandle, LLVector3 pos, LLVector3 lookAt, IPEndPoint newRegionExternalEndPoint){}
       public virtual void SendMapBlock(List<MapBlockData> mapBlocks){}
       public virtual void SendLocalTeleport(LLVector3 position, LLVector3 lookAt, uint flags){}
       public virtual void SendRegionTeleport(ulong regionHandle, byte simAccess, IPEndPoint regionExternalEndPoint, uint locationID, uint flags){}
       public virtual void SendTeleportCancel(){}
       public virtual void SendTeleportLocationStart(){}
       public virtual void SendMoneyBalance(LLUUID transaction, bool success, byte[] description, int balance){}

       public virtual void SendAvatarData(ulong regionHandle, string firstName, string lastName, LLUUID avatarID, uint avatarLocalID, LLVector3 Pos, byte[] textureEntry){}
       public virtual void SendAvatarTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, LLVector3 position, LLVector3 velocity){}

       public virtual void AttachObject(uint localID, LLQuaternion rotation, byte attachPoint){}
       public virtual void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimData primData, LLVector3 pos, LLQuaternion rotation, LLUUID textureID, uint flags){}
       public virtual void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimData primData, LLVector3 pos, LLUUID textureID, uint flags){}
       public virtual void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape, LLVector3 pos, LLQuaternion rotation, uint flags, LLUUID objectID, LLUUID ownerID, string text, uint parentID){}
       public virtual void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape, LLVector3 pos, uint flags, LLUUID objectID, LLUUID ownerID, string text, uint parentID){}
       public virtual void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, LLVector3 position, LLQuaternion rotation){}

       public virtual void SendInventoryFolderDetails(LLUUID ownerID, LLUUID folderID, List<InventoryItemBase> items){}
       public virtual void SendInventoryItemDetails(LLUUID ownerID, LLUUID folderID, InventoryItemBase item){}
       public virtual void SendNameReply(LLUUID profileId, string firstname, string lastname){}
    }
}
