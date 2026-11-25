using AutoJx2.Interfaces;
using AutoJx2.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace AutoJx2
{
    /// <summary>
    /// Window for adding a new account
    /// </summary>
    public partial class AddAccountWindow : Window
    {
        private readonly IConfigurationManager _configurationManager;
        private readonly ILogger<AddAccountWindow>? _logger;
        private Models.GameAccount? _editingAccount;

        public event EventHandler<Models.GameAccount>? AccountSaved;

        public AddAccountWindow(IConfigurationManager configurationManager, ILogger<AddAccountWindow>? logger,
            IEnumerable<string>? availableServers = null)
        {
            InitializeComponent();
            _configurationManager = configurationManager;
            _logger = logger;

            var serverNames = availableServers?.ToList() ?? new List<string>();
            ServerComboBox.ItemsSource = serverNames;
            if (serverNames.Count > 0)
                ServerComboBox.SelectedIndex = 0;

            // Focus the first text box
            UsernameTextBox.Focus();
        }

        /// <summary>
        /// Constructor for editing an account
        /// </summary>
        public AddAccountWindow(IConfigurationManager configurationManager, ILogger<AddAccountWindow>? logger,
            Models.GameAccount account, IEnumerable<string>? availableServers = null)
            : this(configurationManager, logger, availableServers)
        {
            _editingAccount = account;
            Title = "Edit Account";
            LoadAccountData();
        }

        private void LoadAccountData()
        {
            if (_editingAccount == null) return;

            try
            {
                UsernameTextBox.Text = _editingAccount.Username;
                ServerComboBox.Text = _editingAccount.Server;
                NotesTextBox.Text = _editingAccount.Notes ?? "";
                
                // Do not display the existing password for security reasons
                PasswordBox.Password = "";
                
                _logger?.LogInformation("Account data loaded for editing: {Username}", _editingAccount.Username);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading account data");
                ShowValidationMessage("Error loading account data");
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateInput())
                    return;

                var account = new Models.GameAccount
                {
                    Username = UsernameTextBox.Text.Trim(),
                    Password = EncryptPassword(PasswordBox.Password),
                    Server = ServerComboBox.Text.Trim(),
                    Notes = string.IsNullOrWhiteSpace(NotesTextBox.Text) ? null : NotesTextBox.Text.Trim(),
                    LastUpdated = DateTime.Now
                };

                if (_editingAccount != null)
                {
                    // Update the account
                    account.Id = _editingAccount.Id;
                    account.CreatedAt = _editingAccount.CreatedAt;
                    account.IsHidden = _editingAccount.IsHidden;
                    account.ProcessId = _editingAccount.ProcessId;
                    account.IsRunning = _editingAccount.IsRunning;
                    
                    _configurationManager.UpdateAccount(account);
                    _logger?.LogInformation("Account updated: {Username}", account.Username);
                }
                else
                {
                    // Add a new account
                    _configurationManager.AddAccount(account);
                    _logger?.LogInformation("Account added: {Username}", account.Username);
                }

                AccountSaved?.Invoke(this, account);
                DialogResult = true;
                Close();
            }
            catch (InvalidOperationException ex)
            {
                ShowValidationMessage(ex.Message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving account");
                ShowValidationMessage($"Error saving the account: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool ValidateInput()
        {
            ClearValidationMessage();

            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                ShowValidationMessage("Please enter a username");
                UsernameTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                ShowValidationMessage("Please enter a password");
                PasswordBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(ServerComboBox.Text))
            {
                ShowValidationMessage("Please select a server");
                ServerComboBox.Focus();
                return false;
            }

            // Validate field lengths
            if (UsernameTextBox.Text.Length < 3)
            {
                ShowValidationMessage("Username must contain at least 3 characters");
                UsernameTextBox.Focus();
                return false;
            }

            if (PasswordBox.Password.Length < 4)
            {
                ShowValidationMessage("Password must contain at least 4 characters");
                PasswordBox.Focus();
                return false;
            }

            return true;
        }

        private void ShowValidationMessage(string message)
        {
            ValidationMessage.Text = message;
            ValidationMessage.Visibility = Visibility.Visible;
        }

        private void ClearValidationMessage()
        {
            ValidationMessage.Text = "";
            ValidationMessage.Visibility = Visibility.Collapsed;
        }

        private string EncryptPassword(string password)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var encryptedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error encrypting password");
                throw new InvalidOperationException("Error encrypting the password");
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Focus the first text box
            UsernameTextBox.Focus();
        }

        private void ServerComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}
