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

        public PermissionManager(Scene scene)
        {
            m_scene = scene;
        }

        public delegate void OnPermissionErrorDelegate(LLUUID user, string reason);
        public event OnPermissionErrorDelegate OnPermissionError;

        protected virtual void SendPermissionError(LLUUID user, string reason)
        {
            if (OnPermissionError != null)
                OnPermissionError(user, reason);
        }

        protected virtual bool IsAdministrator(LLUUID user)
        {
            return m_scene.RegionInfo.MasterAvatarAssignedUUID == user;
        }

        protected virtual bool IsEstateManager(LLUUID user)
        {
            return false;
        }

        public virtual bool CanRezObject(LLUUID user, LLVector3 position)
        {
            return true;
        }


        #region Object Permissions

        protected virtual bool GenericObjectPermission(LLUUID user, LLUUID obj)
        {
            // Default: deny
            bool permission = false;

            // If it's not an object, we cant edit it.
            if (!(m_scene.Entities[obj] is SceneObjectGroup))
                return false;

            SceneObjectGroup task = (SceneObjectGroup)m_scene.Entities[obj];
            LLUUID taskOwner = null;  

            // Object owners should be able to edit their own content
            if (user == taskOwner)
                permission = true;

            // Users should be able to edit what is over their land.
            if (m_scene.LandManager.getLandObject(task.Pos.X, task.Pos.Y).landData.ownerID == user)
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

        public virtual bool CanEditScript(LLUUID user, LLUUID script)
        {
            return false;
        }

        public virtual bool CanRunScript(LLUUID user, LLUUID script)
        {
            return false;
        }

        public virtual bool CanTerraform(LLUUID user, LLUUID position)
        {
            return false;
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
