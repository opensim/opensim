
using System;
using System.Text;

using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
//using OpenSim.Services.AssetService;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
	
	
	public class InventoryItem : IInventoryItem
	{
		TaskInventoryItem m_privateItem;
		Scene m_rootSceene;
		
		public InventoryItem(Scene rootScene, TaskInventoryItem internalItem)
		{
			m_rootSceene = rootScene;
			m_privateItem = internalItem;
		}

		// Marked internal, to prevent scripts from accessing the internal type
		internal TaskInventoryItem ToTaskInventoryItem()
   		{
			return m_privateItem;
		}

		/// <summary>
		/// This will attempt to convert from an IInventoryItem to an InventoryItem object
		/// </summary>
		/// <description>
		/// In order for this to work the object which implements IInventoryItem must inherit from InventoryItem, otherwise
		/// an exception is thrown.
		/// </description>
		/// <param name="i">
		/// The interface to upcast <see cref="IInventoryItem"/>
		/// </param>
		/// <returns>
		/// The object backing the interface implementation <see cref="InventoryItem"/>
		/// </returns>
		internal static InventoryItem FromInterface(IInventoryItem i)
   		{
			if(typeof(InventoryItem).IsAssignableFrom(i.GetType()))
			{
				return (InventoryItem)i;
			}
			else
			{
				throw new ApplicationException("[MRM] There is no legal conversion from IInventoryItem to InventoryItem");
			}
		}
			
		public int Type { get { return m_privateItem.Type; } }
		public UUID AssetID { get { return m_privateItem.AssetID; } }
		
		public T RetreiveAsset<T>() where T : OpenMetaverse.Asset, new()
		{
			AssetBase a = m_rootSceene.AssetService.Get(AssetID.ToString());
			T result = new T();

			if((sbyte)result.AssetType != a.Type)
				throw new ApplicationException("[MRM] The supplied asset class does not match the found asset");
			
			result.AssetData = a.Data;
			result.Decode();
			return result;
		}
	}
}
