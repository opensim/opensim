using System;
using libsecondlife;
using OpenSim.Framework.Utilities;

namespace OpenGridServices.Manager
{
	
	
	public class RegionBlock
	{
	
		private uint regloc_x;
		private uint regloc_y;
		
		
		
		public ulong regionhandle {
			get {  return Util.UIntsToLong(regloc_x*256,regloc_y*256); }
		}

		public Gdk.Pixbuf MiniMap;
						
		public RegionBlock()
		{
		}
	}
}
