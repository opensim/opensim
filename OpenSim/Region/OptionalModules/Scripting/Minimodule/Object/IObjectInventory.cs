
using System;
using System.Collections.Generic;

using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule.Object
{
	
	/// <summary>
	/// This implements the methods neccesary to operate on the inventory of an object
	/// </summary>
	public interface IObjectInventory : IDictionary<UUID, IInventoryItem>
	{
		IInventoryItem this[string name] { get; }
	}
}
