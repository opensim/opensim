using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Framework.Types;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;
using OpenSim.Region.Capabilities;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.LandManagement;

using libsecondlife;

namespace OpenSim.Region.Environment
{
    public class PermissionManager
    {
        protected Scene m_scene;

        // Bypasses the permissions engine (always returns OK)
        // disable in any production environment
        // TODO: Change this to false when permissions are a desired default
        // TODO: Move to configuration option.
        private bool bypassPermissions = true;

        public PermissionManager(Scene scene)
        {
            m_scene = scene;
        }

        public void DisablePermissions()
        {
            bypassPermissions = true;
        }

        public void EnablePermissions()
        {
            bypassPermissions = false;
        }

        protected virtual void SendPermissionError(LLUUID user, string reason)
        {
            m_scene.EventManager.TriggerPermissionError(user, reason);
        }

        protected virtual bool IsAdministrator(LLUUID user)
        {
            if (bypassPermissions)
                return bypassPermissions;

            return m_scene.RegionInfo.MasterAvatarAssignedUUID == user;
        }

        protected virtual bool IsEstateManager(LLUUID user)
        {
            if (bypassPermissions)
                return bypassPermissions;

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

            if (IsAdministrator(user))
                permission = true;
            else
                reason = "Not an administrator";

            if (GenericParcelPermission(user, position))
                permission = true;
            else
                reason = "Not the parcel owner";

            if (!permission)
                SendPermissionError(user, reason);

            return true;
        }

        #region Object Permissions

        protected virtual bool GenericObjectPermission(LLUUID user, LLUUID objId)
        {
            // Default: deny
            bool permission = false;

            if( !m_scene.Entities.ContainsKey( objId ))
            {
                return false;
            }
            
            // If it's not an object, we cant edit it.
            if (!(m_scene.Entities[objId] is SceneObjectGroup))
            {
                return false;
            }
            
            SceneObjectGroup task = (SceneObjectGroup)m_scene.Entities[objId];
            LLUUID taskOwner = null;  

            // Object owners should be able to edit their own content
            if (user == taskOwner)
                permission = true;

            // Users should be able to edit what is over their land.
            if (m_scene.LandManager.getLandObject(task.AbsolutePosition.X, task.AbsolutePosition.Y).landData.ownerID == user)
                permission = true;

            // Estate users should be able to edit anything in the sim
            if (IsEstateManager(user))
                permission = true;

            // Admin objects should not be editable by the above
            if (IsAdministrator(taskOwner))
                permission = false;

            // Admin should be able to edit anything in the sim (including admin objects)
            if (IsAdministrator(user))
                permission = true;

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

            // Land owner can terraform too
            if (GenericParcelPermission(user, m_scene.LandManager.getLandObject(position.X, position.Y)))
                permission = true;

            if (!permission)
                SendPermissionError(user, "Not authorized to terraform at this location.");

            return permission;
        }

        #region Estate Permissions

        protected virtual bool GenericEstatePermission(LLUUID user)
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

        #endregion

        #region Parcel Permissions

        protected virtual bool GenericParcelPermission(LLUUID user, Land parcel)
        {
            bool permission = false;

            if (parcel.landData.ownerID == user)
                permission = true;

            if (parcel.landData.isGroupOwned)
            {
                // TODO: Need to do some extra checks here. Requires group code.
            }

            if(IsEstateManager(user))
                permission = true;

            if (IsAdministrator(user))
                permission = true;

            return permission;
        }

        protected virtual bool GenericParcelPermission(LLUUID user, LLVector3 pos)
        {
            return GenericParcelPermission(user, m_scene.LandManager.getLandObject(pos.X, pos.Y));
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
