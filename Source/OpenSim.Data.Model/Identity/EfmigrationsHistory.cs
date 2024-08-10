using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Identity;

public partial class EfmigrationsHistory
{
    public string MigrationId { get; set; }

    public string ProductVersion { get; set; }
}
