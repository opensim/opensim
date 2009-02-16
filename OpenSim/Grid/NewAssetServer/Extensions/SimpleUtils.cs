using System;
using System.IO;
using OpenMetaverse;

namespace AssetServer.Extensions
{
    public static class SimpleUtils
    {
        public static string ParseNameFromFilename(string filename)
        {
            filename = Path.GetFileName(filename);

            int dot = filename.LastIndexOf('.');
            int firstDash = filename.IndexOf('-');

            if (dot - 37 > 0 && firstDash > 0)
                return filename.Substring(0, firstDash);
            else
                return String.Empty;
        }

        public static UUID ParseUUIDFromFilename(string filename)
        {
            int dot = filename.LastIndexOf('.');

            if (dot > 35)
            {
                // Grab the last 36 characters of the filename
                string uuidString = filename.Substring(dot - 36, 36);
                UUID uuid;
                UUID.TryParse(uuidString, out uuid);
                return uuid;
            }
            else
            {
                UUID uuid;
                if (UUID.TryParse(Path.GetFileName(filename), out uuid))
                    return uuid;
                else
                    return UUID.Zero;
            }
        }
    }
}
