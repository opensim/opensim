using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class Estateban
{
    public uint EstateId { get; set; }

    public string BannedUuid { get; set; }

    public string BannedIp { get; set; }

    public string BannedIpHostMask { get; set; }

    public string BannedNameMask { get; set; }

    public string BanningUuid { get; set; }

    public int BanTime { get; set; }
}
