using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class OsGroupsNotice
{
    public string GroupId { get; set; }

    public string NoticeId { get; set; }

    public uint Tmstamp { get; set; }

    public string FromName { get; set; }

    public string Subject { get; set; }

    public string Message { get; set; }

    public int HasAttachment { get; set; }

    public int AttachmentType { get; set; }

    public string AttachmentName { get; set; }

    public string AttachmentItemId { get; set; }

    public string AttachmentOwnerId { get; set; }
}
