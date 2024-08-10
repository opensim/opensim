using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class HgTravelingDatum
{
    public string SessionId { get; set; }

    public string UserId { get; set; }

    public string GridExternalName { get; set; }

    public string ServiceToken { get; set; }

    public string ClientIpaddress { get; set; }

    public string MyIpaddress { get; set; }

    public DateTime Tmstamp { get; set; }
}
