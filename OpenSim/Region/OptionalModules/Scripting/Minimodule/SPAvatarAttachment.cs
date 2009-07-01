using System;

using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public class SPAvatarAttachment : IAvatarAttachment
	{
        private readonly Scene m_rootScene;
		private readonly IAvatar m_parent;
		private readonly int m_location;
		private readonly UUID m_itemId;
		private readonly UUID m_assetId;
		
		public SPAvatarAttachment(Scene rootScene, IAvatar self, int location, UUID itemId, UUID assetId)
		{
			m_rootScene = rootScene;
			m_parent = self;
			m_location = location;
			m_itemId = itemId;
			m_assetId = assetId;
		}
		
		public int Location { get { return m_location; } }
		
		public IObject Asset
		{
			get
			{
				return new SOPObject(m_rootScene, m_rootScene.GetSceneObjectPart(m_assetId).LocalId);
			}
		}
	}
}
