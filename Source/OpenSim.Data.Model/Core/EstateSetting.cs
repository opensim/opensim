using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class EstateSetting
{
    public uint EstateId { get; set; }

    public string EstateName { get; set; }

    public sbyte AbuseEmailToEstateOwner { get; set; }

    public sbyte DenyAnonymous { get; set; }

    public sbyte ResetHomeOnTeleport { get; set; }

    public sbyte FixedSun { get; set; }

    public sbyte DenyTransacted { get; set; }

    public sbyte BlockDwell { get; set; }

    public sbyte DenyIdentified { get; set; }

    public sbyte AllowVoice { get; set; }

    public sbyte UseGlobalTime { get; set; }

    public int PricePerMeter { get; set; }

    public sbyte TaxFree { get; set; }

    public sbyte AllowDirectTeleport { get; set; }

    public int RedirectGridX { get; set; }

    public int RedirectGridY { get; set; }

    public uint ParentEstateId { get; set; }

    public double SunPosition { get; set; }

    public sbyte EstateSkipScripts { get; set; }

    public float BillableFactor { get; set; }

    public sbyte PublicAccess { get; set; }

    public string AbuseEmail { get; set; }

    public string EstateOwner { get; set; }

    public sbyte DenyMinors { get; set; }

    public sbyte AllowLandmark { get; set; }

    public sbyte AllowParcelChanges { get; set; }

    public sbyte AllowSetHome { get; set; }

    public sbyte AllowEnviromentOverride { get; set; }
}
