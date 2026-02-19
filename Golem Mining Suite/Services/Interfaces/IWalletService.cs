using System.Collections.ObjectModel;
using Golem_Mining_Suite.Models;

namespace Golem_Mining_Suite.Services.Interfaces
{
    public interface IWalletService
    {
        double CurrentBalance { get; }
        ObservableCollection<WalletTransaction> Transactions { get; }

        void AddTransaction(double amount, TransactionType type, string category, string description);
        double GetBalance();
    }
}
