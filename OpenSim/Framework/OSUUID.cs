// OSUUID.cs created with MonoDevelop
// User: sdague at 10:17 AM 4/9/2008
//
// To change standard headers go to Edit->Preferences->Coding->Standard Headers
//

using System;
using libsecondlife;

namespace OpenSim.Framework
{
    [Serializable]
    public class OSUUID: IComparable
    {
        public Guid UUID;
	
        public OSUUID() {}

        /* Constructors */
        public OSUUID(string s)
        {
            if (s == null)
                UUID = new Guid();
            else
                UUID = new Guid(s);
        }

        public OSUUID(Guid g)
        {
            UUID = g;
        }

        public OSUUID(LLUUID l)
        {
            UUID = l.UUID;
        }

        public OSUUID(ulong u)
        {
            UUID = new Guid(0, 0, 0, BitConverter.GetBytes(u));
        }

        // out conversion
        public override string ToString()
        {
            return UUID.ToString();
        }

        public LLUUID ToLLUUID()
        {
            return new LLUUID(UUID);
        }

        // for comparison bits
        public override int GetHashCode()
        {
            return UUID.GetHashCode();
        }

        public override bool Equals(object o)
        {
            if (!(o is LLUUID)) return false;
            
            OSUUID uuid = (OSUUID)o;
            return UUID == uuid.UUID;
        }

        public int CompareTo(object obj)
        {
            if (obj is OSUUID)
            {
                OSUUID ID = (OSUUID)obj;
                return this.UUID.CompareTo(ID.UUID);
            }

            throw new ArgumentException("object is not a OSUUID");
        }

        // Static methods
        public static OSUUID Random()
        {
            return new OSUUID(Guid.NewGuid());
        }

        public static readonly OSUUID Zero = new OSUUID();
    }
}
