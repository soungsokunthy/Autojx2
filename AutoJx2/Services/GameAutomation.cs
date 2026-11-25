using AutoJx2.Interfaces;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AutoJx2.Services
{
    /// <summary>
    /// Service tự động hóa game sử dụng FlaUI
    /// </summary>
    public class GameAutomation : IGameAutomation, IDisposable
    {
        private readonly ILogger<GameAutomation> _logger;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly object _lockObject = new object();
        
        private Process? _attachedProcess;
        private Application? _application;
        private UIA3Automation? _automation;
        private Window? _gameWindow;
        private bool _disposed = false;
        private bool _isRunning = false;
        
        // Automation settings
        private bool _autoFarming = false;
        private bool _autoQuest = false;
        private bool _autoCombat = false;
        private bool _autoHeal = false;
        private int _delay = 1000;
        private int _maxHpThreshold = 80;
        private int _maxMpThreshold = 70;

        public event EventHandler<string>? StatusChanged;
        public event EventHandler<string>? ErrorOccurred;

        public bool IsRunning => _isRunning;

        public GameAutomation(ILogger<GameAutomation> logger)
        {
            _logger = logger;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void AttachToProcess(Process process)
        {
            try
            {
                if (process == null)
                {
                    throw new ArgumentNullException(nameof(process));
                }

                // Keep only the PID from the UI's Process object. Reacquire a fresh
                // Process handle because UI lists can retain stale Process instances.
                int processId = process.Id;

                lock (_lockObject)
                {
                    if (_isRunning)
                    {
                        StopAutomation();
                    }

                    // Release any previous attachment before connecting again.
                    _gameWindow = null;
                    _application?.Dispose();
                    _application = null;
                    _automation?.Dispose();
                    _automation = null;
                    _attachedProcess?.Dispose();
                    _attachedProcess = null;

                    _attachedProcess = Process.GetProcessById(processId);
                    if (_attachedProcess.HasExited)
                    {
                        throw new InvalidOperationException($"Game process {processId} has already exited. Refresh the process list and try again.");
                    }

                    // Attach by PID rather than passing a possibly stale Process object.
                    _application = Application.Attach(processId);
                    _automation = new UIA3Automation();
                    _gameWindow = _application.GetMainWindow(_automation);

                    if (_gameWindow == null)
                    {
                        throw new InvalidOperationException(
                            $"FlaUI attached to PID {processId}, but could not find its main window. " +
                            "Make sure you selected the actual JX2 game client, not its launcher.");
                    }

                    string processName = _attachedProcess.ProcessName;
                    OnStatusChanged($"Attached to process: {processName} (PID: {processId})");
                    _logger.LogInformation("Successfully attached to process: {ProcessName} (PID: {ProcessId})", processName, processId);
                }
            }
            catch (Exception ex)
            {
                lock (_lockObject)
                {
                    _gameWindow = null;
                    _application?.Dispose();
                    _application = null;
                    _automation?.Dispose();
                    _automation = null;
                    _attachedProcess?.Dispose();
                    _attachedProcess = null;
                }

                _logger.LogError(ex, "Error attaching to process");
                OnErrorOccurred($"Failed to attach to process: {ex.Message}");
                throw;
            }
        }

        public void StartAutomation()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_attachedProcess == null || _gameWindow == null)
                    {
                        throw new InvalidOperationException("No process attached. Please attach to a game process first.");
                    }

                    if (_isRunning)
                    {
                        _logger.LogWarning("Automation is already running");
                        return;
                    }

                    _isRunning = true;
                    _cancellationTokenSource.Cancel(); // Cancel previous task if any
                    _cancellationTokenSource.Dispose();
                    
                    var newCts = new CancellationTokenSource();
                    _cancellationTokenSource = newCts;

                    // Bắt đầu automation trong background task
                    Task.Run(() => RunAutomationLoop(newCts.Token), newCts.Token);
                    
                    OnStatusChanged("Automation started");
                    _logger.LogInformation("Automation started successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting automation");
                OnErrorOccurred($"Failed to start automation: {ex.Message}");
                throw;
            }
        }

        public void StopAutomation()
        {
            try
            {
                lock (_lockObject)
                {
                    if (!_isRunning)
                    {
                        return;
                    }

                    _isRunning = false;
                    _cancellationTokenSource.Cancel();
                    
                    OnStatusChanged("Automation stopped");
                    _logger.LogInformation("Automation stopped successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping automation");
                OnErrorOccurred($"Failed to stop automation: {ex.Message}");
            }
        }

        public void SetAutoFarming(bool enabled)
        {
            _autoFarming = enabled;
            OnStatusChanged($"Auto farming: {(enabled ? "enabled" : "disabled")}");
        }

        public void SetAutoQuest(bool enabled)
        {
            _autoQuest = enabled;
            OnStatusChanged($"Auto quest: {(enabled ? "enabled" : "disabled")}");
        }

        public void SetAutoCombat(bool enabled)
        {
            _autoCombat = enabled;
            OnStatusChanged($"Auto combat: {(enabled ? "enabled" : "disabled")}");
        }

        public void SetAutoHeal(bool enabled)
        {
            _autoHeal = enabled;
            OnStatusChanged($"Auto heal: {(enabled ? "enabled" : "disabled")}");
        }

        public void SetDelay(int milliseconds)
        {
            _delay = Math.Max(100, milliseconds); // Minimum 100ms delay
            OnStatusChanged($"Delay set to {_delay}ms");
        }

        public void SetMaxHpThreshold(int percentage)
        {
            _maxHpThreshold = Math.Clamp(percentage, 1, 100);
            OnStatusChanged($"Max HP threshold set to {_maxHpThreshold}%");
        }

        public void SetMaxMpThreshold(int percentage)
        {
            _maxMpThreshold = Math.Clamp(percentage, 1, 100);
            OnStatusChanged($"Max MP threshold set to {_maxMpThreshold}%");
        }

        private async Task RunAutomationLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    if (_attachedProcess?.HasExited == true)
                    {
                        OnErrorOccurred("Game process has exited");
                        break;
                    }

                    // Thực hiện các automation tasks
                    await PerformAutomationTasks(cancellationToken);
                    
                    // Delay giữa các lần thực hiện
                    await Task.Delay(_delay, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
                _logger.LogInformation("Automation loop cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in automation loop");
                OnErrorOccurred($"Automation error: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        private async Task PerformAutomationTasks(CancellationToken cancellationToken)
        {
            try
            {
                if (_autoHeal)
                {
                    await PerformAutoHeal(cancellationToken);
                }

                if (_autoCombat)
                {
                    await PerformAutoCombat(cancellationToken);
                }

                if (_autoFarming)
                {
                    await PerformAutoFarming(cancellationToken);
                }

                if (_autoQuest)
                {
                    await PerformAutoQuest(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing automation tasks");
            }
        }

        private async Task PerformAutoHeal(CancellationToken cancellationToken)
        {
            try
            {
                // TODO: Implement HP/MP detection logic
                // This would involve finding UI elements that show HP/MP values
                // and then using appropriate healing items or skills
                
                OnStatusChanged("Auto heal: Checking HP/MP status...");
                await Task.Delay(500, cancellationToken);
                
                // Placeholder logic - replace with actual game-specific implementation
                var currentHp = GetCurrentHp(); // This would be implemented based on game UI
                var currentMp = GetCurrentMp(); // This would be implemented based on game UI
                
                if (currentHp < _maxHpThreshold)
                {
                    OnStatusChanged($"Auto heal: HP low ({currentHp}%), using healing item...");
                    UseHealingItem(); // This would be implemented based on game UI
                }
                
                if (currentMp < _maxMpThreshold)
                {
                    OnStatusChanged($"Auto heal: MP low ({currentMp}%), using mana potion...");
                    UseManaPotion(); // This would be implemented based on game UI
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in auto heal");
            }
        }

        private async Task PerformAutoCombat(CancellationToken cancellationToken)
        {
            try
            {
                OnStatusChanged("Auto combat: Checking for enemies...");
                await Task.Delay(500, cancellationToken);
                
                // TODO: Implement enemy detection and combat logic
                // This would involve finding enemies on screen and using appropriate combat skills
                
                var hasEnemy = CheckForEnemies(); // This would be implemented based on game UI
                if (hasEnemy)
                {
                    OnStatusChanged("Auto combat: Enemy detected, attacking...");
                    PerformAttack(); // This would be implemented based on game UI
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in auto combat");
            }
        }

        private async Task PerformAutoFarming(CancellationToken cancellationToken)
        {
            try
            {
                OnStatusChanged("Auto farming: Checking farming status...");
                await Task.Delay(500, cancellationToken);
                
                // TODO: Implement farming logic
                // This would involve checking farming progress and performing farming actions
                
                var farmingProgress = GetFarmingProgress(); // This would be implemented based on game UI
                if (farmingProgress < 100)
                {
                    OnStatusChanged($"Auto farming: Progress {farmingProgress}%, continuing...");
                    ContinueFarming(); // This would be implemented based on game UI
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in auto farming");
            }
        }

        private async Task PerformAutoQuest(CancellationToken cancellationToken)
        {
            try
            {
                OnStatusChanged("Auto quest: Checking quest status...");
                await Task.Delay(500, cancellationToken);
                
                // TODO: Implement quest logic
                // This would involve checking available quests and completing them
                
                var hasQuest = CheckForQuests(); // This would be implemented based on game UI
                if (hasQuest)
                {
                    OnStatusChanged("Auto quest: Quest available, accepting...");
                    AcceptQuest(); // This would be implemented based on game UI
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in auto quest");
            }
        }

        // Placeholder methods - these would be implemented based on actual game UI structure
        private int GetCurrentHp() => 100; // Replace with actual implementation
        private int GetCurrentMp() => 100; // Replace with actual implementation
        private void UseHealingItem() { } // Replace with actual implementation
        private void UseManaPotion() { } // Replace with actual implementation
        private bool CheckForEnemies() => false; // Replace with actual implementation
        private void PerformAttack() { } // Replace with actual implementation
        private int GetFarmingProgress() => 0; // Replace with actual implementation
        private void ContinueFarming() { } // Replace with actual implementation
        private bool CheckForQuests() => false; // Replace with actual implementation
        private void AcceptQuest() { } // Replace with actual implementation

        private void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, status);
        }

        private void OnErrorOccurred(string error)
        {
            ErrorOccurred?.Invoke(this, error);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                StopAutomation();
                _cancellationTokenSource?.Dispose();
                _gameWindow = null;
                _application?.Dispose();
                _application = null;
                _automation?.Dispose();
                _automation = null;
                _attachedProcess?.Dispose();
                _attachedProcess = null;
                _disposed = true;
            }
        }
    }
}
