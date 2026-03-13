using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using ATS_WPF.Models;
using ATS.CAN.Engine.Models;
using ATS_WPF.Services;
using ATS_WPF.Services.Interfaces;
using ATS.CAN.Engine.Services.Interfaces;
using ATS_WPF.Core;
using ATS.CAN.Engine.Core;
using ATS.CAN.Engine.Services;
using ATS_WPF.ViewModels;

namespace ATS_WPF.Views
{
    public partial class MonitorWindow : Window
    {
        private readonly ObservableCollection<CANMessageEntry> _messages;
        private readonly ConcurrentQueue<CANMessage> _messageQueue = new ConcurrentQueue<CANMessage>();
        private DispatcherTimer? _updateTimer;
        private bool _isMonitoring = false;
        private ICANService? _canService;

        // Decoded IDs for descriptions
        private readonly HashSet<uint> _rxIds = new HashSet<uint> { 0x200, 0x201, 0x300, 0x303 };
        private readonly HashSet<uint> _txIds = new HashSet<uint> { 0x040, 0x048, 0x050, 0x032, 0x033 };

        public MonitorWindow(ICANService? canService = null)
        {
            InitializeComponent();

            _messages = new ObservableCollection<CANMessageEntry>();
            MessageListBox.ItemsSource = _messages;
            _canService = canService;

            InitializeUI();
        }

        private void InitializeUI()
        {
            UpdateMessageCountLabel();
        }

        private void StartMonitorBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isMonitoring = true;
                StartMonitorBtn.IsEnabled = false;
                StopMonitorBtn.IsEnabled = true;
                MonitorStatusTxt.Text = "Monitoring...";
                MonitorStatusTxt.Foreground = System.Windows.Media.Brushes.Green;

                // Subscribe to CANService if available
                if (_canService != null)
                {
                    _canService.MessageReceived += OnCANMessageReceived;
                    MonitorStatusTxt.Text = "Monitoring (Connected)...";
                }
                else
                {
                    MonitorStatusTxt.Text = "Monitoring (No CAN Service)...";
                    MonitorStatusTxt.Foreground = System.Windows.Media.Brushes.Orange;
                }

                // Start update timer (Batched UI updates every 100ms)
                _updateTimer = new DispatcherTimer(DispatcherPriority.Background) 
                { 
                    Interval = TimeSpan.FromMilliseconds(100) 
                };
                _updateTimer.Tick += UpdateTimer_Tick;
                _updateTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Start monitor error: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopMonitorBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isMonitoring = false;
                StartMonitorBtn.IsEnabled = true;
                StopMonitorBtn.IsEnabled = false;
                MonitorStatusTxt.Text = "Stopped";
                MonitorStatusTxt.Foreground = System.Windows.Media.Brushes.Red;

                // Unsubscribe from CANService
                if (_canService != null)
                {
                    _canService.MessageReceived -= OnCANMessageReceived;
                }

                // Stop update timer
                _updateTimer?.Stop();
                _updateTimer = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Stop monitor error: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearMonitorBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _messages.Clear();
                // Clear the backlog too
                while (_messageQueue.TryDequeue(out _)) { }
                UpdateMessageCountLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Clear monitor error: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isMonitoring) return;

            bool addedAny = false;
            int count = 0;
            
            // Drain the queue in the UI thread periodically
            // This batched approach is MUCH more efficient than individual Dispatcher.Invoke calls
            while (_messageQueue.TryDequeue(out var message) && count < 500) // Caps at 500 per 100ms to stay responsive
            {
                string direction = message.Direction;
                string canId = $"0x{message.ID:X3}";
                string data = message.GetDataHexString();
                
                // Decode using ViewModel helper
                var vm = new CANMessageViewModel(message, _rxIds, _txIds);
                string description = vm.Decoded;

                var entry = new CANMessageEntry
                {
                    Timestamp = message.Timestamp.ToString("HH:mm:ss.fff"),
                    Direction = direction,
                    CanId = canId,
                    Data = data,
                    Description = description
                };

                _messages.Add(entry);
                addedAny = true;
                count++;
            }

            if (addedAny)
            {
                // Keep only last 1000 messages
                while (_messages.Count > 1000)
                {
                    _messages.RemoveAt(0);
                }

                // Batch auto-scroll (only once per 100ms)
                if (MessageListBox.Items.Count > 0)
                {
                    MessageListBox.ScrollIntoView(MessageListBox.Items[MessageListBox.Items.Count - 1]);
                }

                UpdateMessageCountLabel();
            }
        }

        private void OnCANMessageReceived(CANMessage message)
        {
            if (!_isMonitoring || message == null) return;
            
            // Extremely lightweight: just drop it in the thread-safe queue
            // No Dispatcher effort here, so the CAN reader thread is NEVER blocked
            _messageQueue.Enqueue(message);
        }

        private void UpdateMessageCountLabel()
        {
            MessageCountTxt.Text = $"{_messages.Count} messages";
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_messages.Count == 0)
                {
                    MessageBox.Show("No messages to export.", "Export",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Create save file dialog
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    FilterIndex = 1,
                    FileName = $"can_monitor_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = "csv",
                    Title = "Export CAN Monitor Messages"
                };

                // Set default directory to application data directory
                try
                {
                    string defaultDir = SettingsManager.Instance.Settings.SaveDirectory;
                    if (Directory.Exists(defaultDir))
                    {
                        saveDialog.InitialDirectory = defaultDir;
                    }
                }
                catch
                {
                    // Use default if settings directory doesn't exist
                }

                if (saveDialog.ShowDialog() == true)
                {
                    ExportMessagesToFile(saveDialog.FileName);
                    MessageBox.Show($"Successfully exported {_messages.Count} messages to:\n{saveDialog.FileName}",
                                  "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Export IO error: {ex.Message}", "Export Failed",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Access denied: {ex.Message}", "Export Failed",
                             MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export error: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportMessagesToFile(string filePath)
        {
            try
            {
                bool isCsv = filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

                using (var writer = new StreamWriter(filePath))
                {
                    if (isCsv)
                    {
                        // Write CSV header
                        writer.WriteLine("Timestamp,Direction,CAN_ID,Data,Description");

                        // Write CSV data
                        foreach (var message in _messages)
                        {
                            // Escape commas and quotes in CSV
                            string escapedDescription = message.Description.Replace("\"", "\"\"");
                            writer.WriteLine($"{message.Timestamp},{message.Direction},{message.CanId},\"{message.Data}\",\"{escapedDescription}\"");
                        }
                    }
                    else
                    {
                        // Write text format with headers
                        writer.WriteLine("CAN Bus Monitor - Message Export");
                        writer.WriteLine($"Export Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        writer.WriteLine($"Total Messages: {_messages.Count}");
                        writer.WriteLine(new string('-', 100));
                        writer.WriteLine();

                        // Write column headers
                        writer.WriteLine(string.Format("{0,-12} {1,-10} {2,-10} {3,-20} {4}",
                            "Timestamp", "Direction", "CAN ID", "Data", "Description"));
                        writer.WriteLine(new string('-', 100));

                        // Write message data
                        foreach (var message in _messages)
                        {
                            writer.WriteLine(string.Format("{0,-12} {1,-10} {2,-10} {3,-20} {4}",
                                message.Timestamp, message.Direction, message.CanId, message.Data, message.Description));
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                throw new IOException($"Failed to write file: {ex.Message}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException($"Access denied: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to write file: {ex.Message}", ex);
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isMonitoring = false;
                _updateTimer?.Stop();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Close error: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _isMonitoring = false;
                _updateTimer?.Stop();

                // Unsubscribe from CANService
                if (_canService != null)
                {
                    _canService.MessageReceived -= OnCANMessageReceived;
                }

                base.OnClosed(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Window close error: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
