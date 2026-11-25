using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AutoJx2.Interfaces;
using AutoJx2.Services;
using AutoJx2.Models;
using Microsoft.Win32;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace AutoJx2
{
    public partial class MainWindow : System.Windows.Window
    {
        private ILogger<MainWindow>? _logger;
        private ICredentialManager? _credentialManager;
        private IGameAutomation? _gameAutomation;
        private IConfigurationManager? _configurationManager;
        private IGameProcessManager? _gameProcessManager;
        private readonly DispatcherTimer _timer;
        private Process? _attachedProcess;
        private bool _isAutomationRunning = false;
        private DateTime _startTime;
        private List<Models.GameAccount> _accounts = new List<Models.GameAccount>();
        private const string Jx2UserDataIniPath = @"H:\Software\JX2RoyalLauncher\JX2 Royal\UserData\uiCommon.ini";

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize _startTime
            _startTime = DateTime.Now;
            
            // Initialize the timer used to update elapsed time
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
            
            // Lấy services từ DI container
            var app = Application.Current as App;
            if (app != null)
            {
                _logger = app.ServiceProvider.GetRequiredService<ILogger<MainWindow>>();
                _credentialManager = app.ServiceProvider.GetRequiredService<ICredentialManager>();
                _gameAutomation = app.ServiceProvider.GetRequiredService<IGameAutomation>();
                _configurationManager = app.ServiceProvider.GetRequiredService<IConfigurationManager>();
                _gameProcessManager = app.ServiceProvider.GetRequiredService<IGameProcessManager>();
            }
            
            // Initialize the user interface
            InitializeUI();
        }

        public MainWindow(ILogger<MainWindow> logger, ICredentialManager credentialManager, IGameAutomation gameAutomation, IConfigurationManager configurationManager, IGameProcessManager gameProcessManager)
        {
            InitializeComponent();
            
            _logger = logger;
            _credentialManager = credentialManager;
            _gameAutomation = gameAutomation;
            _configurationManager = configurationManager;
            _gameProcessManager = gameProcessManager;
            
            // Initialize _startTime
            _startTime = DateTime.Now;
            
            // Initialize the timer used to update elapsed time
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
            
            // Initialize the user interface
            InitializeUI();
            
            _logger?.LogInformation("MainWindow initialized successfully");
        }

        private void InitializeUI()
        {
            try
            {
                // Load server names from the JX2 launcher configuration before loading saved credentials.
                LoadJx2ServerNames();

                // Load credentials
                LoadCredentials();
                
                // Load settings
                LoadSettings();
                
                // Load game path and accounts
                LoadGamePath();
                LoadAccounts();
                
                // Refresh process list
                RefreshProcesses();
                
                // Set initial button states
                if (StartAutomationButton != null && StopAutomationButton != null)
                {
                    StartAutomationButton.IsEnabled = true;
                    StopAutomationButton.IsEnabled = false;
                }
                
                // Cập nhật status
                UpdateStatus("Ready");
                
                _logger?.LogInformation("UI initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing UI");
                MessageBox.Show($"Error initializing UI: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (_isAutomationRunning && FooterTimeTextBlock != null)
                {
                    var elapsed = DateTime.Now - _startTime;
                    FooterTimeTextBlock.Text = elapsed.ToString(@"hh\:mm\:ss");
                }
                else if (FooterTimeTextBlock != null)
                {
                    FooterTimeTextBlock.Text = "00:00:00";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in timer tick");
            }
        }

        private void RefreshProcessesButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshProcesses();
        }

        private void RefreshProcesses()
        {
            var gameWindows = new List<GameWindowProcess>();

            try
            {
                // Only list JX2Royal processes that currently expose a visible main window.
                foreach (var process in Process.GetProcessesByName("JX2Royal"))
                {
                    try
                    {
                        process.Refresh();
                        string title = process.MainWindowTitle;

                        if (process.MainWindowHandle != IntPtr.Zero &&
                            !string.IsNullOrWhiteSpace(title))
                        {
                            gameWindows.Add(new GameWindowProcess
                            {
                                ProcessId = process.Id,
                                ProcessName = process.ProcessName,
                                MainWindowTitle = title
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "Could not inspect JX2 process {ProcessId}", SafeGetProcessId(process));
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                if (GameProcessComboBox != null)
                {
                    GameProcessComboBox.ItemsSource = gameWindows
                        .OrderBy(p => p.MainWindowTitle)
                        .ToList();

                    if (GameProcessComboBox.Items.Count > 0)
                        GameProcessComboBox.SelectedIndex = 0;

                    _logger?.LogInformation(
                        "Refreshed JX2 game windows. Found {Count} visible hero windows",
                        gameWindows.Count);
                }

                if (gameWindows.Count == 0)
                {
                    UpdateStatus("No visible JX2Royal game windows found");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error refreshing JX2 game windows");
                MessageBox.Show($"Error refreshing game windows: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static int SafeGetProcessId(Process process)
        {
            try { return process.Id; }
            catch { return -1; }
        }

        private sealed class GameWindowProcess
        {
            public int ProcessId { get; set; }
            public string ProcessName { get; set; } = string.Empty;
            public string MainWindowTitle { get; set; } = string.Empty;
        }

        private sealed class LauncherWindowProcess
        {
            public int ProcessId { get; set; }
            public string MainWindowTitle { get; set; } = string.Empty;
            public string DisplayText => $"{MainWindowTitle} (PID: {ProcessId})";
        }

        private void RefreshLauncherButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var launchers = new List<LauncherWindowProcess>();

                foreach (var process in Process.GetProcessesByName("JX2RoyalUpdater"))
                {
                    try
                    {
                        process.Refresh();
                        if (process.MainWindowHandle != IntPtr.Zero &&
                            !string.IsNullOrWhiteSpace(process.MainWindowTitle))
                        {
                            launchers.Add(new LauncherWindowProcess
                            {
                                ProcessId = process.Id,
                                MainWindowTitle = process.MainWindowTitle
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "Could not inspect launcher process {ProcessId}", SafeGetProcessId(process));
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                if (LauncherProcessComboBox != null)
                {
                    LauncherProcessComboBox.ItemsSource = launchers
                        .OrderBy(p => p.MainWindowTitle)
                        .ToList();

                    if (LauncherProcessComboBox.Items.Count > 0)
                        LauncherProcessComboBox.SelectedIndex = 0;
                }

                UpdateAutoLoginStatus(launchers.Count == 0
                    ? "No visible JX2 Royal Launcher found"
                    : $"Found {launchers.Count} visible launcher window(s)");

                _logger?.LogInformation("Found {Count} visible JX2 launcher window(s)", launchers.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error refreshing launcher windows");
                MessageBox.Show($"Error refreshing launcher windows: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InspectLauncherButton_Click(object sender, RoutedEventArgs e)
        {
            if (LauncherProcessComboBox?.SelectedItem is not LauncherWindowProcess selected)
            {
                MessageBox.Show("Click Refresh Launcher and select the JX2 Royal Launcher first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var launcherProcess = Process.GetProcessById(selected.ProcessId);
                launcherProcess.Refresh();
                if (launcherProcess.HasExited || launcherProcess.MainWindowHandle == IntPtr.Zero)
                    throw new InvalidOperationException("The selected launcher window is no longer available. Refresh the launcher list.");

                using var application = Application.Attach(selected.ProcessId);
                using var automation = new UIA3Automation();
                var window = application.GetMainWindow(automation);

                if (window == null)
                    throw new InvalidOperationException("FlaUI attached, but could not read the launcher main window.");

                var report = new StringBuilder();
                report.AppendLine($"Launcher UI inspection: {selected.MainWindowTitle}");
                report.AppendLine($"Process ID: {selected.ProcessId}");
                report.AppendLine($"Window: {window.Name}");
                report.AppendLine("Controls (maximum 250):");

                var controls = window.FindAllDescendants();
                int count = Math.Min(controls.Length, 250);
                for (int i = 0; i < count; i++)
                {
                    var control = controls[i];
                    report.AppendLine(
                        $"[{i + 1}] Type={control.ControlType}; Name={control.Name}; " +
                        $"AutomationId={control.AutomationId}; ClassName={control.ClassName}");
                }

                if (controls.Length > count)
                    report.AppendLine($"... {controls.Length - count} additional controls omitted.");

                string output = report.ToString();
                if (LogsTextBox != null)
                {
                    LogsTextBox.Text = output;
                    LogsTextBox.ScrollToHome();
                }

                UpdateAutoLoginStatus($"Launcher UI inspected: {count} control(s). See Logs tab.");
                _logger?.LogInformation("Inspected launcher UI for PID {ProcessId}; found {ControlCount} controls", selected.ProcessId, controls.Length);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error inspecting launcher UI for PID {ProcessId}", selected.ProcessId);
                MessageBox.Show($"Could not inspect the launcher UI: {ex.Message}", "Launcher Inspection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AttachProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (GameProcessComboBox?.SelectedItem is not GameWindowProcess selected || _gameAutomation == null)
            {
                MessageBox.Show("Please select a JX2 hero window first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Process? process = null;
            try
            {
                // Reopen the process by PID because the process used during refresh was disposed.
                process = Process.GetProcessById(selected.ProcessId);
                process.Refresh();

                if (process.HasExited || process.MainWindowHandle == IntPtr.Zero)
                    throw new InvalidOperationException("The selected game window is no longer available. Click Refresh and select it again.");

                string currentTitle = process.MainWindowTitle;
                if (string.IsNullOrWhiteSpace(currentTitle))
                    throw new InvalidOperationException("The selected process no longer has a visible game window. Click Refresh and try again.");

                _gameAutomation.AttachToProcess(process);

                _attachedProcess?.Dispose();
                _attachedProcess = process;
                process = null; // MainWindow now owns this Process reference.

                UpdateStatus($"Attached to {currentTitle} (PID: {selected.ProcessId})");

                if (StartAutomationButton != null)
                    StartAutomationButton.IsEnabled = true;

                _logger?.LogInformation(
                    "Attached to hero window {WindowTitle} (Process: {ProcessName}, PID: {ProcessId})",
                    currentTitle, selected.ProcessName, selected.ProcessId);
            }
            catch (Exception ex)
            {
                process?.Dispose();
                _logger?.LogError(ex, "Error attaching to selected hero window {WindowTitle}", selected.MainWindowTitle);
                MessageBox.Show($"Error attaching to '{selected.MainWindowTitle}': {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetHotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement hotkey setting
            MessageBox.Show("Hotkey setting feature will be implemented soon.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void StartAutomationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_attachedProcess == null || _gameAutomation == null)
                {
                    MessageBox.Show("Please attach to a game process first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _gameAutomation.StartAutomation();
                _isAutomationRunning = true;
                _startTime = DateTime.Now;
                UpdateStatus("Automation started");
                
                if (StartAutomationButton != null && StopAutomationButton != null)
                {
                    StartAutomationButton.IsEnabled = false;
                    StopAutomationButton.IsEnabled = true;
                }
                
                _logger?.LogInformation("Automation started");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error starting automation");
                MessageBox.Show($"Error starting automation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopAutomationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_gameAutomation != null)
                {
                    _gameAutomation.StopAutomation();
                }
                _isAutomationRunning = false;
                UpdateStatus("Automation stopped");
                
                if (StartAutomationButton != null && StopAutomationButton != null)
                {
                    StartAutomationButton.IsEnabled = true;
                    StopAutomationButton.IsEnabled = false;
                }
                
                _logger?.LogInformation("Automation stopped");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error stopping automation");
                MessageBox.Show($"Error stopping automation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveCredentialsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_credentialManager == null)
                {
                    MessageBox.Show("Credential manager not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var username = UsernameTextBox?.Text ?? "";
                var password = PasswordBox?.Password ?? "";
                var server = ServerComboBox?.Text ?? "";

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Username and password are required.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _credentialManager.SaveCredentials(username, password, server);
                UpdateStatus("Credentials saved successfully");
                _logger?.LogInformation("Credentials saved for user: {Username}", username);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving credentials");
                MessageBox.Show($"Error saving credentials: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (Directory.Exists(logPath))
                {
                    Process.Start("explorer.exe", logPath);
                }
                else
                {
                    MessageBox.Show("Logs directory not found.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error opening logs directory");
                MessageBox.Show($"Error opening logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = new Dictionary<string, object>
                {
                    ["Delay"] = DelayTextBox?.Text ?? "1000",
                    ["MaxHp"] = MaxHpTextBox?.Text ?? "80",
                    ["MaxMp"] = MaxMpTextBox?.Text ?? "70",
                    ["LogLevel"] = LogLevelComboBox?.Text ?? "Information"
                };

                var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                var fileName = $"AutoJx2_Settings_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = fileName,
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveFileDialog.FileName, json);
                    UpdateStatus("Settings exported successfully");
                    _logger?.LogInformation("Settings exported to: {FilePath}", saveFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error exporting settings");
                MessageBox.Show($"Error exporting settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var json = File.ReadAllText(openFileDialog.FileName);
                    var settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    
                    if (settings != null)
                    {
                        if (settings.TryGetValue("Delay", out var delay) && DelayTextBox != null) 
                            DelayTextBox.Text = delay.ToString();
                        if (settings.TryGetValue("MaxHp", out var maxHp) && MaxHpTextBox != null) 
                            MaxHpTextBox.Text = maxHp.ToString();
                        if (settings.TryGetValue("MaxMp", out var maxMp) && MaxMpTextBox != null) 
                            MaxMpTextBox.Text = maxMp.ToString();
                        
                        UpdateStatus("Settings imported successfully");
                        _logger?.LogInformation("Settings imported from: {FilePath}", openFileDialog.FileName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error importing settings");
                MessageBox.Show($"Error importing settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DelayTextBox != null) DelayTextBox.Text = "1000";
                if (MaxHpTextBox != null) MaxHpTextBox.Text = "80";
                if (MaxMpTextBox != null) MaxMpTextBox.Text = "70";
                if (LogLevelComboBox != null) LogLevelComboBox.SelectedIndex = 0;
                
                UpdateStatus("Settings reset to default");
                _logger?.LogInformation("Settings reset to default values");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error resetting settings");
                MessageBox.Show($"Error resetting settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            if (LogsTextBox != null)
            {
                LogsTextBox.Clear();
                UpdateStatus("Logs cleared");
            }
        }

        private void RefreshLogsButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshLogs();
        }

        private void SaveLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (LogsTextBox == null) return;

                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"AutoJx2_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveFileDialog.FileName, LogsTextBox.Text);
                    UpdateStatus("Logs saved successfully");
                    _logger?.LogInformation("Logs saved to: {FilePath}", saveFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving logs");
                MessageBox.Show($"Error saving logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private List<string> ReadJx2ServerNames()
        {
            var serverNames = new List<string>();

            try
            {
                if (!File.Exists(Jx2UserDataIniPath))
                {
                    _logger?.LogWarning("JX2 launcher configuration file was not found: {Path}", Jx2UserDataIniPath);
                    return serverNames;
                }

                var inLoginSection = false;
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var rawLine in File.ReadAllLines(Jx2UserDataIniPath, System.Text.Encoding.UTF8))
                {
                    var line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#"))
                        continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        inLoginSection = string.Equals(line, "[Login]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    if (!inLoginSection)
                        continue;

                    var separator = line.IndexOf('=');
                    if (separator <= 0)
                        continue;

                    var key = line.Substring(0, separator).Trim();
                    var value = line.Substring(separator + 1).Trim();
                    values[key] = value;
                }

                void AddServerName(string? value)
                {
                    if (!string.IsNullOrWhiteSpace(value) &&
                        !serverNames.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
                    {
                        serverNames.Add(value.Trim());
                    }
                }

                if (values.TryGetValue("LastGameServer", out var lastGameServer))
                    AddServerName(lastGameServer);

                for (var i = 0; i <= 6; i++)
                {
                    if (values.TryGetValue($"LastSelName{i}", out var rememberedName))
                        AddServerName(rememberedName);
                }

                _logger?.LogInformation("Loaded {Count} JX2 server name(s) from launcher configuration", serverNames.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error reading JX2 launcher server configuration");
            }

            return serverNames;
        }

        private void LoadJx2ServerNames()
        {
            var serverNames = ReadJx2ServerNames();
            if (ServerComboBox == null)
                return;

            var savedServer = ServerComboBox.Text;
            ServerComboBox.ItemsSource = serverNames;

            if (serverNames.Count > 0)
            {
                ServerComboBox.SelectedItem = serverNames.FirstOrDefault(
                    name => string.Equals(name, savedServer, StringComparison.OrdinalIgnoreCase))
                    ?? serverNames[0];
            }
            else
            {
                ServerComboBox.Text = savedServer;
                UpdateStatus("JX2 server list not found. Check the launcher configuration path.");
            }
        }

        private void LoadCredentials()
        {
            try
            {
                if (_credentialManager == null) return;

                var credentials = _credentialManager.LoadCredentials();
                if (credentials != null)
                {
                    if (UsernameTextBox != null) UsernameTextBox.Text = credentials.Username;
                    if (PasswordBox != null) PasswordBox.Password = credentials.Password;
                    if (ServerComboBox != null) ServerComboBox.Text = credentials.Server;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading credentials");
            }
        }

        private void LoadSettings()
        {
            try
            {
                // Load settings from configuration or use defaults
                if (DelayTextBox != null) DelayTextBox.Text = "1000";
                if (MaxHpTextBox != null) MaxHpTextBox.Text = "80";
                if (MaxMpTextBox != null) MaxMpTextBox.Text = "70";
                if (LogLevelComboBox != null) LogLevelComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading settings");
            }
        }

        private void RefreshLogs()
        {
            try
            {
                if (LogsTextBox == null) return;

                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (Directory.Exists(logPath))
                {
                    var logFiles = Directory.GetFiles(logPath, "*.log")
                        .OrderByDescending(f => File.GetLastWriteTime(f))
                        .FirstOrDefault();

                    if (logFiles != null)
                    {
                        var logContent = File.ReadAllText(logFiles);
                        LogsTextBox.Text = logContent;
                        LogsTextBox.ScrollToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error refreshing logs");
            }
        }

        private void UpdateStatus(string status)
        {
            if (StatusTextBlock != null)
                StatusTextBlock.Text = $"Status: {status}";
            if (FooterStatusTextBlock != null)
                FooterStatusTextBlock.Text = status;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Register global hotkey (F12)
            RegisterHotkey();
        }

        private void RegisterHotkey()
        {
            // TODO: Implement global hotkey registration
            _logger?.LogInformation("Hotkey registration will be implemented");
        }

        #region Auto Login Methods

        private void LoadGamePath()
        {
            try
            {
                if (_configurationManager != null && GamePathTextBox != null)
                {
                    var gamePath = _configurationManager.GetGamePath();
                    GamePathTextBox.Text = string.IsNullOrEmpty(gamePath) ? "No game path selected" : gamePath;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading game path");
            }
        }

        private void LoadAccounts()
        {
            try
            {
                if (_configurationManager != null && AccountsListView != null)
                {
                    _accounts = _configurationManager.GetAccounts();
                    AccountsListView.ItemsSource = _accounts;
                    UpdateAutoLoginStatus($"Loaded {_accounts.Count} account(s)");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading accounts");
                UpdateAutoLoginStatus("Failed to load the account list");
            }
        }

        private void BrowseGamePathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Select so2game.exe",
                    Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                    FileName = "so2game.exe"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    if (GamePathTextBox != null)
                    {
                        GamePathTextBox.Text = openFileDialog.FileName;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error browsing game path");
                MessageBox.Show($"Error selecting the game file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveGamePathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_configurationManager == null || GamePathTextBox == null)
                    return;

                var gamePath = GamePathTextBox.Text.Trim();
                if (string.IsNullOrEmpty(gamePath) || gamePath == "No game path selected")
                {
                    MessageBox.Show("Please select a game path before saving.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!File.Exists(gamePath))
                {
                    MessageBox.Show("The game file does not exist. Please check the path.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _configurationManager.SaveGamePath(gamePath);
                UpdateAutoLoginStatus("Game path saved");
                MessageBox.Show("Game path saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving game path");
                MessageBox.Show($"Error saving the game path: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddAccountButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_configurationManager == null || _logger == null)
                    return;

                var loggerFactory = (Application.Current as App)?.ServiceProvider?.GetRequiredService<ILoggerFactory>();
                var addAccountLogger = loggerFactory?.CreateLogger<AddAccountWindow>();
                var addAccountWindow = new AddAccountWindow(_configurationManager, addAccountLogger, ReadJx2ServerNames());
                addAccountWindow.Owner = this;
                addAccountWindow.AccountSaved += (s, account) =>
                {
                    LoadAccounts();
                    UpdateAutoLoginStatus($"Account '{account.Username}' has been added");
                };

                addAccountWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error opening add account window");
                MessageBox.Show($"Error opening the Add Account window: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshAccountsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadAccounts();
        }

        private void RemoveSelectedAccountsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_configurationManager == null || AccountsListView == null)
                    return;

                var selectedItems = new List<Models.GameAccount>();
                foreach (var item in AccountsListView.SelectedItems)
                {
                    if (item is Models.GameAccount account)
                    {
                        selectedItems.Add(account);
                    }
                }
                
                if (selectedItems.Count == 0)
                {
                    MessageBox.Show("Please select an account to remove.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Are you sure you want to remove the {selectedItems.Count} selected account(s)?", 
                    "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    foreach (var account in selectedItems)
                    {
                        _configurationManager.RemoveAccount(account.Id);
                    }
                    LoadAccounts();
                    UpdateAutoLoginStatus($"Removed {selectedItems.Count} account(s)");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error removing selected accounts");
                MessageBox.Show($"Error removing account: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AccountsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (AccountsListView.SelectedItem is Models.GameAccount account)
                {
                    StartGameForAccount(account);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error starting game on double click");
            }
        }

        private void StartGameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AccountsListView.SelectedItem is Models.GameAccount account)
                {
                    StartGameForAccount(account);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error starting game from context menu");
            }
        }

        private void StopGameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AccountsListView.SelectedItem is GameAccount account && account.ProcessId.HasValue)
                {
                    _gameProcessManager?.StopGame(account.ProcessId.Value);
                    UpdateAutoLoginStatus($"Stopped the game for account '{account.Username}'");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error stopping game from context menu");
            }
        }

        private void EditAccountMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AccountsListView.SelectedItem is Models.GameAccount account && _configurationManager != null && _logger != null)
                {
                    var loggerFactory = (Application.Current as App)?.ServiceProvider?.GetRequiredService<ILoggerFactory>();
                    var editAccountLogger = loggerFactory?.CreateLogger<AddAccountWindow>();
                    var editAccountWindow = new AddAccountWindow(_configurationManager, editAccountLogger, account, ReadJx2ServerNames());
                    editAccountWindow.Owner = this;
                    editAccountWindow.AccountSaved += (s, updatedAccount) =>
                    {
                        LoadAccounts();
                        UpdateAutoLoginStatus($"Account '{updatedAccount.Username}' has been updated");
                    };

                    editAccountWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error opening edit account window");
                MessageBox.Show($"Error opening the Edit Account window: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleVisibilityMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AccountsListView.SelectedItem is Models.GameAccount account && _configurationManager != null)
                {
                    account.IsHidden = !account.IsHidden;
                    _configurationManager.UpdateAccount(account);
                    LoadAccounts();
                    UpdateAutoLoginStatus($"Account '{account.Username}' is now {(account.IsHidden ? "hidden" : "visible")}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error toggling account visibility");
            }
        }

        private void RemoveAccountMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AccountsListView.SelectedItem is GameAccount account && _configurationManager != null)
                {
                    var result = MessageBox.Show($"Are you sure you want to remove account '{account.Username}'?", 
                        "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        _configurationManager.RemoveAccount(account.Id);
                        LoadAccounts();
                        UpdateAutoLoginStatus($"Account '{account.Username}' has been removed");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error removing account from context menu");
                MessageBox.Show($"Error removing account: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartGameForAccount(Models.GameAccount account)
        {
            try
            {
                if (_configurationManager == null || _gameProcessManager == null)
                    return;

                var gamePath = _configurationManager.GetGamePath();
                if (string.IsNullOrEmpty(gamePath))
                {
                    MessageBox.Show("Please select a game path before starting the game.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!File.Exists(gamePath))
                {
                    MessageBox.Show("The game file does not exist. Please check the game path.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var process = _gameProcessManager.StartGame(account, gamePath);
                if (process != null)
                {
                    UpdateAutoLoginStatus($"Started the game for account '{account.Username}'");
                }
                else
                {
                    UpdateAutoLoginStatus($"Unable to start the game for account '{account.Username}'");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error starting game for account: {Username}", account.Username);
                MessageBox.Show($"Error starting the game: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateAutoLoginStatus(string message)
        {
            try
            {
                if (AutoLoginStatusTextBlock != null)
                {
                    AutoLoginStatusTextBlock.Text = message;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating auto login status");
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            _gameAutomation?.Dispose();
            _gameProcessManager?.Dispose();
            base.OnClosed(e);
        }
    }
}
