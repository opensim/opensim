/*
* Copyright (c) Contributors, http://opensimulator.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using libsecondlife;
using OpenSim.Region.Environment.LandManagement;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment
{
    public class PermissionManager
    {
        protected Scene m_scene;

        // These are here for testing.  They will be taken out
        private uint PERM_ALL = (uint)2147483647;
        private uint PERM_COPY = (uint)32768;
        private uint PERM_MODIFY = (uint)16384;
        private uint PERM_MOVE = (uint)524288;
        private uint PERM_TRANS = (uint)8192;
        private uint PERM_LOCKED = (uint)540672;
        // Bypasses the permissions engine (always returns OK)
        // disable in any production environment
        // TODO: Change this to false when permissions are a desired default
        // TODO: Move to configuration option.
        private bool m_bypassPermissions = true;

        public bool BypassPermissions
        {
            get { return m_bypassPermissions; }
            set { m_bypassPermissions = value; }
        }

        public PermissionManager()
        {
        }

        public PermissionManager(Scene scene)
        {
            m_scene = scene;
        }

        public void Initialise(Scene scene)
        {
            m_scene = scene;
        }

        protected virtual void SendPermissionError(LLUUID user, string reason)
        {
            m_scene.EventManager.TriggerPermissionError(user, reason);
        }

        public virtual bool IsAdministrator(LLUUID user)
        {
            if (m_bypassPermissions)
            {
                return true;
            }

            // If there is no master avatar, return false
            if (m_scene.RegionInfo.MasterAvatarAssignedUUID != null)
            {
                return m_scene.RegionInfo.MasterAvatarAssignedUUID == user;
            }

            return false;
        }

        public virtual bool IsEstateManager(LLUUID user)
        {
            if (m_bypassPermissions)
            {
                return true;
            }
            if (user != null)
            {
                LLUUID[] estatemanagers = m_scene.RegionInfo.EstateSettings.estateManagers;
                for (int i = 0; i < estatemanagers.Length; i++)
                {
                    if (estatemanagers[i] == user)
                        return true;
                }
            }
            // The below is commented out because logically it happens anyway.   It's left in for readability
            //else
            //{
            //return false;
            //}

            return false;
        }

        protected virtual bool IsGridUser(LLUUID user)
        {
            return true;
        }

        protected virtual bool IsGuest(LLUUID user)
        {
            return false;
        }

        public virtual bool CanRezObject(LLUUID user, LLVector3 position)
        {
            bool permission = false;

            string reason = "Insufficient permission";

            Land land = m_scene.LandManager.getLandObject(position.X, position.Y);
            if (land == null) return false;

            if ((land.landData.landFlags & ((int)Parcel.ParcelFlags.CreateObjects)) ==
                (int)Parcel.ParcelFlags.CreateObjects)
                permission = true;

            //TODO: check for group rights

            if (IsAdministrator(user))
            {
                permission = true;
            }
            else
            {
                reason = "Not an administrator";
            }

            if (GenericParcelPermission(user, position))
            {
                permission = true;
            }
            else
            {
                reason = "Not the parcel owner";
            }

            if (!permission)
                SendPermissionError(user, reason);

            return permission;
        }

        #region Object Permissions

        public virtual uint GenerateClientFlags(LLUUID user, LLUUID objID)
        {

            // Here's the way this works, 
            // ObjectFlags and Permission flags are two different enumerations
            // ObjectFlags, however, tells the client to change what it will allow the user to do.
            // So, that means that all of the permissions type ObjectFlags are /temporary/ and only 
            // supposed to be set when customizing the objectflags for the client.  

            // These temporary objectflags get computed and added in this function based on the 
            // Permission mask that's appropriate!
            // Outside of this method, they should never be added to objectflags!
            // -teravus

            if (!m_scene.Entities.ContainsKey(objID))
            {
                return 0;
            }

            // If it's not an object, we cant edit it.
            if (!(m_scene.Entities[objID] is SceneObjectGroup))
            {
                return 0;
            }

            SceneObjectGroup task = (SceneObjectGroup)m_scene.Entities[objID];
            LLUUID objectOwner = task.OwnerID;

            uint objflags = task.RootPart.ObjectFlags;


            // Remove any of the objectFlags that are temporary.  These will get added back if appropriate 
            // in the next bit of code

            objflags &= (uint)
                ~(LLObject.ObjectFlags.ObjectCopy | // Tells client you can copy the object
                LLObject.ObjectFlags.ObjectModify | // tells client you can modify the object
                LLObject.ObjectFlags.ObjectMove |   // tells client that you can move the object (only, no mod)
                LLObject.ObjectFlags.ObjectTransfer | // tells the client that you can /take/ the object if you don't own it
                LLObject.ObjectFlags.ObjectYouOwner | // Tells client that you're the owner of the object
                LLObject.ObjectFlags.ObjectYouOfficer // Tells client that you've got group object editing permission. Used when ObjectGroupOwned is set
                );

            // Creating the three ObjectFlags options for this method to choose from.
            // Customize the OwnerMask
            uint objectOwnerMask = ApplyObjectModifyMasks(task.RootPart.OwnerMask, objflags);
            objectOwnerMask |= (uint)LLObject.ObjectFlags.ObjectYouOwner;

            // Customize the GroupMask
            uint objectGroupMask = ApplyObjectModifyMasks(task.RootPart.GroupMask, objflags);

            // Customize the EveryoneMask
            uint objectEveryoneMask = ApplyObjectModifyMasks(task.RootPart.EveryoneMask, objflags);


            // Hack to allow collaboration until Groups and Group Permissions are implemented
            if ((objectEveryoneMask & (uint)LLObject.ObjectFlags.ObjectMove) != 0)
                objectEveryoneMask |= (uint)LLObject.ObjectFlags.ObjectModify;

            if (m_bypassPermissions)
                return objectOwnerMask;

            // Object owners should be able to edit their own content
            if (user == objectOwner)
            {
                return objectOwnerMask;
            }

            // Users should be able to edit what is over their land.
            Land parcel = m_scene.LandManager.getLandObject(task.AbsolutePosition.X, task.AbsolutePosition.Y);
            if (parcel != null && parcel.landData.ownerID == user)
                return objectOwnerMask;

            // Admin objects should not be editable by the above
            if (IsAdministrator(objectOwner))
                return objectEveryoneMask;

            // Estate users should be able to edit anything in the sim
            if (IsEstateManager(user))
                return objectOwnerMask;



            // Admin should be able to edit anything in the sim (including admin objects)
            if (IsAdministrator(user))
                return objectOwnerMask;


            return objectEveryoneMask;
        }



        private uint ApplyObjectModifyMasks(uint setPermissionMask, uint objectFlagsMask)
        {
            // We are adding the temporary objectflags to the object's objectflags based on the 
            // permission flag given.  These change the F flags on the client.

            if ((setPermissionMask & (uint)PermissionMask.Copy) != 0)
            {
                objectFlagsMask |= (uint)LLObject.ObjectFlags.ObjectCopy;
            }

            if ((setPermissionMask & (uint)PermissionMask.Move) != 0)
            {
                objectFlagsMask |= (uint)LLObject.ObjectFlags.ObjectMove;
            }

            if ((setPermissionMask & (uint)PermissionMask.Modify) != 0)
            {
                objectFlagsMask |= (uint)LLObject.ObjectFlags.ObjectModify;
            }

            if ((setPermissionMask & (uint)PermissionMask.Transfer) != 0)
            {
                objectFlagsMask |= (uint)LLObject.ObjectFlags.ObjectTransfer;
            }

            return objectFlagsMask;
        }

        protected virtual bool GenericObjectPermission(LLUUID currentUser, LLUUID objId)
        {
            // Default: deny
            bool permission = false;
            bool locked = false;

            if (!m_scene.Entities.ContainsKey(objId))
            {
                return false;
            }

            // If it's not an object, we cant edit it.
            if ((!(m_scene.Entities[objId] is SceneObjectGroup)))
            {
                return false;
            }


            SceneObjectGroup group = (SceneObjectGroup)m_scene.Entities[objId];

            LLUUID objectOwner = group.OwnerID;
            locked = ((group.RootPart.OwnerMask & PERM_LOCKED) == 0);

            // People shouldn't be able to do anything with locked objects, except the Administrator
            // The 'set permissions' runs through a different permission check, so when an object owner 
            // sets an object locked, the only thing that they can do is unlock it.
            //
            // Nobody but the object owner can set permissions on an object
            //

            if (locked && (!IsAdministrator(currentUser)))
            {
                return false;
            }

            // Object owners should be able to edit their own content
            if (currentUser == objectOwner)
            {
                permission = true;
            }

            // Users should be able to edit what is over their land.
            Land parcel = m_scene.LandManager.getLandObject(group.AbsolutePosition.X, group.AbsolutePosition.Y);
            if ((parcel != null) && (parcel.landData.ownerID == currentUser))
            {
                permission = true;
            }

            // Estate users should be able to edit anything in the sim
            if (IsEstateManager(currentUser))
            {
                permission = true;
            }

            // Admin objects should not be editable by the above
            if (IsAdministrator(objectOwner))
            {
                permission = false;
            }

            // Admin should be able to edit anything in the sim (including admin objects)
            if (IsAdministrator(currentUser))
            {
                permission = true;
            }

            return permission;
        }

        /// <summary>
        /// Permissions check - can user delete an object?
        /// </summary>
        /// <param name="user">User attempting the delete</param>
        /// <param name="obj">Target object</param>
        /// <returns>Has permission?</returns>
        public virtual bool CanDeRezObject(LLUUID user, LLUUID obj)
        {
            return GenericObjectPermission(user, obj);
        }

        public virtual bool CanEditObject(LLUUID user, LLUUID obj)
        {
            return GenericObjectPermission(user, obj);
        }

        public virtual bool CanEditObjectPosition(LLUUID user, LLUUID obj)
        {
            bool permission = GenericObjectPermission(user, obj);
            if (!permission)
            {
                if (!m_scene.Entities.ContainsKey(obj))
                {
                    return false;
                }

                // The client 
                // may request to edit linked parts, and therefore, it needs 
                // to also check for SceneObjectPart

                // If it's not an object, we cant edit it.
                if ((!(m_scene.Entities[obj] is SceneObjectGroup)))
                {
                    return false;
                }


                SceneObjectGroup task = (SceneObjectGroup)m_scene.Entities[obj];


                LLUUID taskOwner = null;
                // Added this because at this point in time it wouldn't be wise for 
                // the administrator object permissions to take effect.
                LLUUID objectOwner = task.OwnerID;

                // Anyone can move
                if ((task.RootPart.EveryoneMask & PERM_MOVE) != 0)
                    permission = true;

                // Locked
                if ((task.RootPart.OwnerMask & PERM_LOCKED) != 0)
                    permission = false;

            }
            return permission;
        }

        public virtual bool CanCopyObject(LLUUID user, LLUUID obj)
        {
            bool permission = GenericObjectPermission(user, obj);
            if (!permission)
            {
                if (!m_scene.Entities.ContainsKey(obj))
                {
                    return false;
                }

                // If it's not an object, we cant edit it.
                if (!(m_scene.Entities[obj] is SceneObjectGroup))
                {
                    return false;
                }

                SceneObjectGroup task = (SceneObjectGroup)m_scene.Entities[obj];
                LLUUID taskOwner = null;
                // Added this because at this point in time it wouldn't be wise for 
                // the administrator object permissions to take effect.
                LLUUID objectOwner = task.OwnerID;
                if ((task.RootPart.EveryoneMask & PERM_COPY) != 0)
                    permission = true;
            }
            return permission;
        }

        public virtual bool CanReturnObject(LLUUID user, LLUUID obj)
        {
            return GenericObjectPermission(user, obj);
        }

        #endregion

        #region Communication Permissions

        public virtual bool GenericCommunicationPermission(LLUUID user, LLUUID target)
        {
            bool permission = false;
            string reason = "Only registered users may communicate with another account.";

            if (IsGridUser(user))
                permission = true;

            if (!IsGridUser(user))
            {
                permission = false;
                reason = "The person that you are messaging is not a registered user.";
            }
            if (IsAdministrator(user))
                permission = true;

            if (IsEstateManager(user))
                permission = true;

            if (!permission)
                SendPermissionError(user, reason);

            return permission;
        }

        public virtual bool CanInstantMessage(LLUUID user, LLUUID target)
        {
            return GenericCommunicationPermission(user, target);
        }

        public virtual bool CanInventoryTransfer(LLUUID user, LLUUID target)
        {
            return GenericCommunicationPermission(user, target);
        }

        #endregion

        public virtual bool CanEditScript(LLUUID user, LLUUID script)
        {
            return IsAdministrator(user);
        }

        public virtual bool CanRunScript(LLUUID user, LLUUID script)
        {
            return IsAdministrator(user);
        }

        public virtual bool CanTerraform(LLUUID user, LLVector3 position)
        {
            bool permission = false;

            // Estate override
            if (GenericEstatePermission(user))
                permission = true;

            float X = position.X;
            float Y = position.Y;

            if (X > 255)
                X = 255;
            if (Y > 255)
                Y = 255;
            if (X < 0)
                X = 0;
            if (Y < 0)
                Y = 0;

            // Land owner can terraform too
            Land parcel = m_scene.LandManager.getLandObject(X, Y);
            if (parcel != null && GenericParcelPermission(user, parcel))
                permission = true;

            if (!permission)
                SendPermissionError(user, "Not authorized to terraform at this location.");

            return permission;
        }

        #region Estate Permissions

        public virtual bool GenericEstatePermission(LLUUID user)
        {
            // Default: deny
            bool permission = false;

            // Estate admins should be able to use estate tools
            if (IsEstateManager(user))
                permission = true;

            // Administrators always have permission
            if (IsAdministrator(user))
                permission = true;

            return permission;
        }

        public virtual bool CanEditEstateTerrain(LLUUID user)
        {
            return GenericEstatePermission(user);
        }

        public virtual bool CanRestartSim(LLUUID user)
        {
            // Since this is potentially going on a grid...    

            return GenericEstatePermission(user);
            //return m_scene.RegionInfo.MasterAvatarAssignedUUID == user;
        }

        #endregion

        #region Parcel Permissions

        protected virtual bool GenericParcelPermission(LLUUID user, Land parcel)
        {
            bool permission = false;

            if (parcel.landData.ownerID == user)
            {
                permission = true;
            }

            if (parcel.landData.isGroupOwned)
            {
                // TODO: Need to do some extra checks here. Requires group code.
            }

            if (IsEstateManager(user))
            {
                permission = true;
            }

            if (IsAdministrator(user))
            {
                permission = true;
            }

            return permission;
        }

        protected virtual bool GenericParcelPermission(LLUUID user, LLVector3 pos)
        {
            Land parcel = m_scene.LandManager.getLandObject(pos.X, pos.Y);
            if (parcel == null) return false;
            return GenericParcelPermission(user, parcel);
        }

        public virtual bool CanEditParcel(LLUUID user, Land parcel)
        {
            return GenericParcelPermission(user, parcel);
        }

        public virtual bool CanSellParcel(LLUUID user, Land parcel)
        {
            return GenericParcelPermission(user, parcel);
        }

        public virtual bool CanAbandonParcel(LLUUID user, Land parcel)
        {
            return GenericParcelPermission(user, parcel);
        }

        #endregion
    }
}
