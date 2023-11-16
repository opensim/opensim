using System;
using System.IO;

namespace OpenSim.Region.Framework.Scenes
{
    public class LinksetDataEntry
    {
         public LinksetDataEntry(string value, string password)
        {
            this.Value = value;
            this.Password = password;
        }

        public string Value { get; private set; }
        public string Password { get; private set; } = string.Empty;

        public bool IsProtected()
        {
            return (string.IsNullOrEmpty(this.Password) == false);
        }
    
        public bool CheckPassword(string pass)
        {
            // A undocumented caveat for LinksetData appears to be that even for unprotected values, if a pass is provided, it is still treated as protected
            if (this.Password == pass)
                return true;
            else
                return false;
        }

        public string CheckPasswordAndGetValue(string pass)
        {
            if (string.IsNullOrEmpty(this.Password) || (this.Password == pass)) 
                return this.Value;
            else 
                return string.Empty;
        }
    }
}