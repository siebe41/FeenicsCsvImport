using FeenicsCsvImport.ClassLibrary;
using Microsoft.Win32;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FeenicsCsvImport.Gui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _cancellationTokenSource;
        private string _selectedFilePath;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                Title = "Select CSV File to Import"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _selectedFilePath = openFileDialog.FileName;
                txtCsvFile.Text = _selectedFilePath;
                btnPreview.IsEnabled = true;
                btnImport.IsEnabled = true;
                LogMessage($"Selected file: {_selectedFilePath}");
            }
        }

        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath))
            {
                MessageBox.Show("Please select a CSV file first.", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var config = CreateConfiguration();
                var service = new ImportService(config);
                var previewData = service.LoadCsvForPreview(_selectedFilePath);

                var previewWindow = new PreviewWindow(previewData);
                previewWindow.Owner = this;
                previewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading preview: {ex.Message}", "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LogMessage($"Preview error: {ex.Message}");
            }
        }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath))
            {
                MessageBox.Show("Please select a CSV file first.", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtInstance.Text) || 
                string.IsNullOrWhiteSpace(txtUsername.Text) || 
                string.IsNullOrWhiteSpace(txtPassword.Password))
            {
                MessageBox.Show("Please enter all connection settings.", "Missing Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetUIEnabled(false);
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var config = CreateConfiguration();
                var service = new ImportService(config, LogMessage);

                var progress = new Progress<ImportProgress>(p =>
                {
                    progressBar.Value = p.CurrentStep;
                    txtProgress.Text = p.Message;
                });

                LogMessage("Starting import...");
                var result = await service.ExecuteImportAsync(_selectedFilePath, progress, _cancellationTokenSource.Token);

                // Show results
                LogMessage("");
                LogMessage("=== Import Summary ===");
                LogMessage($"Success: {result.Success}");
                LogMessage($"People Created: {result.PeopleCreated}");
                LogMessage($"Access Levels Assigned: {result.AccessLevelsAssigned}");

                if (result.Errors.Count > 0)
                {
                    LogMessage("");
                    LogMessage("Errors:");
                    foreach (var error in result.Errors)
                    {
                        LogMessage($"  - {error}");
                    }
                }

                if (result.Warnings.Count > 0)
                {
                    LogMessage("");
                    LogMessage("Warnings:");
                    foreach (var warning in result.Warnings)
                    {
                        LogMessage($"  - {warning}");
                    }
                }

                if (result.Success)
                {
                    MessageBox.Show($"Import completed successfully!\n\nPeople Created: {result.PeopleCreated}\nAccess Levels Assigned: {result.AccessLevelsAssigned}",
                        "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Import completed with errors. Check the log for details.",
                        "Import Complete", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (OperationCanceledException)
            {
                LogMessage("Import was cancelled by user.");
                MessageBox.Show("Import was cancelled.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogMessage($"Import failed: {ex.Message}");
                MessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetUIEnabled(true);
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            LogMessage("Cancellation requested...");
        }

        private ImportConfiguration CreateConfiguration()
        {
            return new ImportConfiguration
            {
                ApiUrl = txtApiUrl.Text,
                Instance = txtInstance.Text,
                Username = txtUsername.Text,
                Password = txtPassword.Password,
                PoolAccessLevelName = "PoolOnlyAccess-Age12",
                PoolGymAccessLevelName = "PoolAndGymAccess-Age14",
                AllAccessLevelName = "PoolAndGymAfterHoursAccess-Age18",
                ApiCallDelayMs = 100,
                MaxRetries = 5,
                InitialRetryDelayMs = 1000,
                MaxRetryDelayMs = 30000
            };
        }

        private void SetUIEnabled(bool enabled)
        {
            btnBrowse.IsEnabled = enabled;
            btnPreview.IsEnabled = enabled && !string.IsNullOrEmpty(_selectedFilePath);
            btnImport.IsEnabled = enabled && !string.IsNullOrEmpty(_selectedFilePath);
            btnCancel.IsEnabled = !enabled;
            txtApiUrl.IsEnabled = enabled;
            txtInstance.IsEnabled = enabled;
            txtUsername.IsEnabled = enabled;
            txtPassword.IsEnabled = enabled;
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                txtLog.ScrollToEnd();
            });
        }
    }
}
