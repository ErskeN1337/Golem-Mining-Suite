using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services.Interfaces;
using System;
using System.Windows;
using System.ComponentModel;

namespace Golem_Mining_Suite.ViewModels
{
    public partial class WalletViewModel : ObservableObject
    {
        private readonly IWalletService _walletService;

        public ObservableCollection<WalletTransaction> Transactions => _walletService.Transactions;

        [ObservableProperty]
        private double _currentBalance;

        [ObservableProperty]
        private double _transactionAmount;

        [ObservableProperty]
        private string _transactionDescription = string.Empty;

        // For simple UI state management of the "Add Transaction" popup
        [ObservableProperty]
        private bool _isTransactionPopupVisible;

        [ObservableProperty]
        private TransactionType _currentTransactionType;

        [ObservableProperty]
        private string _popupTitle = "Add Transaction";

        public WalletViewModel(IWalletService walletService)
        {
            _walletService = walletService;
            CurrentBalance = _walletService.CurrentBalance;
            
            if (_walletService is INotifyPropertyChanged notifyService)
            {
                notifyService.PropertyChanged += OnServicePropertyChanged;
            }
        }

        private void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IWalletService.CurrentBalance))
            {
                CurrentBalance = _walletService.CurrentBalance;
            }
        }

        [RelayCommand]
        private void OpenDeposit()
        {
            CurrentTransactionType = TransactionType.Deposit;
            PopupTitle = "Manual Deposit";
            TransactionAmount = 0;
            TransactionDescription = "";
            IsTransactionPopupVisible = true;
        }

        [RelayCommand]
        private void OpenWithdraw()
        {
            CurrentTransactionType = TransactionType.Withdraw;
            PopupTitle = "Manual Withdraw";
            TransactionAmount = 0;
            TransactionDescription = "";
            IsTransactionPopupVisible = true;
        }

        [RelayCommand]
        private void ConfirmTransaction()
        {
            if (TransactionAmount <= 0)
            {
                MessageBox.Show("Please enter a valid amount.", "Invalid Amount", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _walletService.AddTransaction(TransactionAmount, CurrentTransactionType, "Manual", TransactionDescription);
            IsTransactionPopupVisible = false;
        }

        [RelayCommand]
        private void CancelTransaction()
        {
            IsTransactionPopupVisible = false;
        }
    }
}
