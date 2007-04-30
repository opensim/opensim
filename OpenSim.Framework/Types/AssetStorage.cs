using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Framework.Types
{
	public class AssetStorage
	{

		public AssetStorage() {
		}

		public AssetStorage(LLUUID assetUUID) {
			UUID=assetUUID;
		}

		public byte[] Data;
		public sbyte Type;
		public string Name;
		public LLUUID UUID;
	}
}
