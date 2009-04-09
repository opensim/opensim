using System.Drawing;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public interface IGraphics
    {
        UUID SaveBitmap(Bitmap data);
        UUID SaveBitmap(Bitmap data, bool lossless, bool temporary);
        Bitmap LoadBitmap(UUID assetID);
    }
}
