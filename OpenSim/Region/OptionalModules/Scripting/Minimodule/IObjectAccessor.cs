using System.Collections.Generic;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public interface IObjectAccessor : ICollection<IObject>
    {
        IObject this[int index] { get; }
        IObject this[uint index] { get; }
        IObject this[UUID index] { get; }
    }
}