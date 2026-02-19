using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services.Interfaces;

namespace Golem_Mining_Suite.Services
{
    public class WalletService : ObservableObject, IWalletService
    {
        private const string WalletFileName = "wallet_data.json";
        private readonly string _walletPath;

        private double _currentBalance;
        public double CurrentBalance
        {
            get => _currentBalance;
            private set => SetProperty(ref _currentBalance, value);
        }

        public ObservableCollection<WalletTransaction> Transactions { get; private set; } = new();

        public WalletService()
        {
            _walletPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, WalletFileName);
            Load();
        }

        public void AddTransaction(double amount, TransactionType type, string category, string description)
        {
            // Calculate new balance
            double balanceChange = (type == TransactionType.Income || type == TransactionType.Deposit) ? amount : -amount;
            double newBalance = CurrentBalance + balanceChange;

            var transaction = new WalletTransaction
            {
                Type = type,
                Category = category,
                Amount = amount,
                Description = description,
                BalanceAfter = newBalance
            };

            // Calculate balance if it was somehow desynced or empty? No, trust the running total.
            
            Transactions.Insert(0, transaction); // Add to top
            CurrentBalance = newBalance;
            Save();
        }

        public double GetBalance() => CurrentBalance;

        private void Load()
        {
            try
            {
                if (File.Exists(_walletPath))
                {
                    var json = File.ReadAllText(_walletPath);
                    var data = JsonSerializer.Deserialize<WalletData>(json);
                    
                    if (data != null)
                    {
                        CurrentBalance = data.CurrentBalance;
                        Transactions = new ObservableCollection<WalletTransaction>(data.Transactions.OrderByDescending(t => t.Date));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load wallet: {ex.Message}");
            }
        }

        private void Save()
        {
            try
            {
                var data = new WalletData
                {
                    CurrentBalance = CurrentBalance,
                    Transactions = Transactions.ToList()
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(_walletPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save wallet: {ex.Message}");
            }
        }

        private class WalletData
        {
            public double CurrentBalance { get; set; }
            public System.Collections.Generic.List<WalletTransaction> Transactions { get; set; } = new();
        }
    }
}
