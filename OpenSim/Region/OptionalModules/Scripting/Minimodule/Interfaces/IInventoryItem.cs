using System;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
	
	/// <summary>
	/// This implements the methods needed to operate on individual inventory items.
	/// </summary>
	public interface IInventoryItem
	{
		int Type { get; }
		UUID AssetID { get; }
		T RetreiveAsset<T>() where T : OpenMetaverse.Asset, new();
	}
}
