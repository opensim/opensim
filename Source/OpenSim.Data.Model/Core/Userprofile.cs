using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class Userprofile
{
    public string Useruuid { get; set; }

    public string ProfilePartner { get; set; }

    public byte[] ProfileAllowPublish { get; set; }

    public byte[] ProfileMaturePublish { get; set; }

    public string ProfileUrl { get; set; }

    public int ProfileWantToMask { get; set; }

    public string ProfileWantToText { get; set; }

    public int ProfileSkillsMask { get; set; }

    public string ProfileSkillsText { get; set; }

    public string ProfileLanguages { get; set; }

    public string ProfileImage { get; set; }

    public string ProfileAboutText { get; set; }

    public string ProfileFirstImage { get; set; }

    public string ProfileFirstText { get; set; }
}
