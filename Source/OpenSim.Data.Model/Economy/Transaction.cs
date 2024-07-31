using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Economy;

/// <summary>
/// Rev.12
/// </summary>
public partial class Transaction
{
    public string Uuid { get; set; }

    public string Sender { get; set; }

    public string Receiver { get; set; }

    public int Amount { get; set; }

    public int SenderBalance { get; set; }

    public int ReceiverBalance { get; set; }

    public string ObjectUuid { get; set; }

    public string ObjectName { get; set; }

    public string RegionHandle { get; set; }

    public string RegionUuid { get; set; }

    public int Type { get; set; }

    public int Time { get; set; }

    public string Secure { get; set; }

    public bool Status { get; set; }

    public string CommonName { get; set; }

    public string Description { get; set; }
}
