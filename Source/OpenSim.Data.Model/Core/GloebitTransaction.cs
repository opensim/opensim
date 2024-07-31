using System;
using System.Collections.Generic;

namespace OpenSim.Data.Model.Core;

public partial class GloebitTransaction
{
    public string TransactionId { get; set; }

    public string PayerId { get; set; }

    public string PayerName { get; set; }

    public string PayeeId { get; set; }

    public string PayeeName { get; set; }

    public int Amount { get; set; }

    public int TransactionType { get; set; }

    public string TransactionTypeString { get; set; }

    public bool IsSubscriptionDebit { get; set; }

    public string SubscriptionId { get; set; }

    public string PartId { get; set; }

    public string PartName { get; set; }

    public string PartDescription { get; set; }

    public string CategoryId { get; set; }

    public int? SaleType { get; set; }

    public bool Submitted { get; set; }

    public bool ResponseReceived { get; set; }

    public bool ResponseSuccess { get; set; }

    public string ResponseStatus { get; set; }

    public string ResponseReason { get; set; }

    public int PayerEndingBalance { get; set; }

    public bool Enacted { get; set; }

    public bool Consumed { get; set; }

    public bool Canceled { get; set; }

    public DateTime CTime { get; set; }

    public DateTime? EnactedTime { get; set; }

    public DateTime? FinishedTime { get; set; }
}
