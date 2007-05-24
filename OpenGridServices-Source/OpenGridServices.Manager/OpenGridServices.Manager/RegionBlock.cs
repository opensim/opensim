using System;
using System.Xml;
using libsecondlife;
using OpenSim.Framework.Utilities;

namespace OpenGridServices.Manager
{
	
	
	public class RegionBlock
	{
		public uint regloc_x;
		public uint regloc_y;
		
		public string httpd_url;
		
		public string region_name;
		
		public ulong regionhandle {
			get {  return Util.UIntsToLong(regloc_x*256,regloc_y*256); }
		}

		public Gdk.Pixbuf MiniMap;
						
		public RegionBlock()
		{
		}

		public void LoadFromXmlNode(XmlNode sourcenode)
		{
			this.regloc_x=Convert.ToUInt32(sourcenode.Attributes.GetNamedItem("loc_x").Value);
			this.regloc_y=Convert.ToUInt32(sourcenode.Attributes.GetNamedItem("loc_y").Value);
			this.region_name=sourcenode.Attributes.GetNamedItem("region_name").Value;
			this.httpd_url=sourcenode.Attributes.GetNamedItem("httpd_url").Value;
		}
	}
}
