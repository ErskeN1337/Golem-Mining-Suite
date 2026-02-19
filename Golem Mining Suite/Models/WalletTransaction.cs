using System;

namespace Golem_Mining_Suite.Models
{
    public enum TransactionType
    {
        Deposit,
        Withdraw,
        Income,
        Expense
    }

    public class WalletTransaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Date { get; set; } = DateTime.Now;
        public TransactionType Type { get; set; }
        public string Category { get; set; } = "Manual"; // "Mining", "Trading", "Manual", "Refining"
        public double Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public double BalanceAfter { get; set; }
    }
}
