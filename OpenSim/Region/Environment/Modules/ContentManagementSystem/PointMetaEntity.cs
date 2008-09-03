#region Header

// PointMetaEntity.cs created with MonoDevelop
// User: bongiojp at 3:03 PM 8/6/2008
//
// To change standard headers go to Edit->Preferences->Coding->Standard Headers
//

#endregion Header

using System;
using System.Collections.Generic;
using System.Drawing;

using libsecondlife;

using Nini.Config;

using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Physics.Manager;

using log4net;

using Axiom.Math;

namespace OpenSim.Region.Environment.Modules.ContentManagement
{
    public class PointMetaEntity : MetaEntity
    {
        #region Constructors

        public PointMetaEntity(Scene scene, uint LocalId, LLVector3 groupPos, float transparency)
            : base()
        {
            CreatePointEntity(scene, LLUUID.Random(), LocalId, groupPos);
            SetPartTransparency(m_Entity.RootPart, transparency);
        }

        public PointMetaEntity(Scene scene, LLUUID uuid, uint LocalId, LLVector3 groupPos, float transparency)
            : base()
        {
            CreatePointEntity(scene, uuid, LocalId, groupPos);
            SetPartTransparency(m_Entity.RootPart, transparency);
        }

        #endregion Constructors

        #region Private Methods

        private void CreatePointEntity(Scene scene, LLUUID uuid, uint LocalId, LLVector3 groupPos)
        {
            SceneObjectGroup x = new SceneObjectGroup();
            SceneObjectPart y = new SceneObjectPart();

            //Initialize part
            y.Name = "Very Small Point";
            y.RegionHandle = scene.RegionInfo.RegionHandle;
            y.CreationDate = (Int32) (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            y.OwnerID = LLUUID.Zero;
            y.CreatorID = LLUUID.Zero;
            y.LastOwnerID = LLUUID.Zero;
            y.UUID = uuid;

            y.LocalId = LocalId;
            	
            y.Shape = PrimitiveBaseShape.CreateBox();
            y.Scale = new LLVector3(0.01f,0.01f,0.01f);
            y.LastOwnerID = LLUUID.Zero;
            y.GroupPosition = groupPos;
            y.OffsetPosition = new LLVector3(0, 0, 0);
            y.RotationOffset = new LLQuaternion(0,0,0,0);
            y.Velocity = new LLVector3(0, 0, 0);
            y.RotationalVelocity = new LLVector3(0, 0, 0);
            y.AngularVelocity = new LLVector3(0, 0, 0);
            y.Acceleration = new LLVector3(0, 0, 0);

            y.Flags = 0;
            y.TrimPermissions();

            //Initialize group and add part as root part
            x.SetScene(scene);
            y.SetParent(x);
            y.ParentID = 0;
            y.LinkNum = 0;
            x.Children.Add(y.UUID, y);
            x.RootPart = y;
            x.RegionHandle = scene.RegionInfo.RegionHandle;
            x.SetScene(scene);

            m_Entity = x;
        }

        #endregion Private Methods
    }
}