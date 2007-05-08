using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public abstract class Command
    {
		public string Name;
		public string Description;
		public TestClient Client;

		public abstract string Execute(string[] args, LLUUID fromAgentID);

		/// <summary>
		/// When set to true, think will be called.
		/// </summary>
		public bool Active;

		/// <summary>
		/// Called twice per second, when Command.Active is set to true.
		/// </summary>
		public virtual void Think()
		{
		}
    }
}
