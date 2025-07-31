using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class Userdatum
{
    public string UserId { get; set; }

    public string TagId { get; set; }

    public string DataKey { get; set; }

    public string DataVal { get; set; }
}
