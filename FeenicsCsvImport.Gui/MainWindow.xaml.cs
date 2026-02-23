using FeenicsCsvImport.ClassLibrary;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
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
        private readonly ObservableCollection<AccessLevelRule> _rules;

        public MainWindow()
        {
            InitializeComponent();

            // Load saved settings
            var settings = SavedSettings.Load();
            txtApiUrl.Text = settings.ApiUrl ?? "https://api.us.acresecurity.cloud";
            txtInstance.Text = settings.Instance ?? "";
            txtUsername.Text = settings.Username ?? "";

            switch (settings.DuplicateHandling)
            {
                case DuplicateHandling.Update:
                    rbUpdate.IsChecked = true;
                    break;
                case DuplicateHandling.CreateNew:
                    rbCreateNew.IsChecked = true;
                    break;
                case DuplicateHandling.Skip:
                default:
                    rbSkip.IsChecked = true;
                    break;
            }

            _rules = new ObservableCollection<AccessLevelRule>();
            if (settings.AccessLevelRules != null && settings.AccessLevelRules.Count > 0)
            {
                foreach (var rule in settings.AccessLevelRules)
                    _rules.Add(rule);
            }
            else
            {
                _rules.Add(new AccessLevelRule { Name = "PoolOnlyAccess-Age12", StartAge = 12, EndAge = 14, CreateIfMissing = false });
                _rules.Add(new AccessLevelRule { Name = "PoolAndGymAccess-Age14", StartAge = 14, EndAge = 18, CreateIfMissing = false });
                _rules.Add(new AccessLevelRule { Name = "PoolAndGymAfterHoursAccess-Age18", StartAge = 18, EndAge = null, CreateIfMissing = false });
            }
            dgRules.ItemsSource = _rules;

            LogMessage("Settings loaded.");
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = BuildSavedSettings();
                settings.Save();
                LogMessage("Settings saved.");
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to save settings: {ex.Message}");
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnLoadAccessLevels_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtApiUrl.Text) ||
                string.IsNullOrWhiteSpace(txtInstance.Text) ||
                string.IsNullOrWhiteSpace(txtUsername.Text) ||
                string.IsNullOrWhiteSpace(txtPassword.Password))
            {
                MessageBox.Show("Please enter API URL, Instance, Username, and Password before loading access levels.",
                    "Missing Credentials", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnLoadAccessLevels.IsEnabled = false;
            btnLoadAccessLevels.Content = "Loading...";
            LogMessage("Fetching access levels from instance...");

            try
            {
                var names = await ImportService.FetchAccessLevelNamesAsync(
                    txtApiUrl.Text, txtInstance.Text, txtUsername.Text, txtPassword.Password);

                LogMessage($"Found {names.Count} access levels in instance.");

                if (names.Count == 0)
                {
                    MessageBox.Show("No access levels found in the instance.", "No Access Levels", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var existingNames = new System.Collections.Generic.List<string>();
                foreach (var rule in _rules)
                {
                    if (!string.IsNullOrWhiteSpace(rule.Name))
                        existingNames.Add(rule.Name);
                }

                var picker = new AccessLevelPickerWindow(names, existingNames);
                picker.Owner = this;

                if (picker.ShowDialog() == true && picker.SelectedNames.Count > 0)
                {
                    int added = 0;
                    foreach (var name in picker.SelectedNames)
                    {
                        bool alreadyExists = false;
                        foreach (var rule in _rules)
                        {
                            if (rule.Name == name)
                            {
                                alreadyExists = true;
                                break;
                            }
                        }

                        if (!alreadyExists)
                        {
                            _rules.Add(new AccessLevelRule { Name = name, StartAge = 0, EndAge = null, CreateIfMissing = false });
                            added++;
                        }
                    }
                    LogMessage($"Added {added} access level(s) to rules. Set the Start Age and End Age for each.");
                }
            }
            catch (Exception ex)
            {
                var details = ImportService.FormatExceptionDetails(ex);
                LogMessage($"Failed to load access levels: {ex.Message}");
                LogMessage($"  {details}");
                MessageBox.Show($"Failed to load access levels:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnLoadAccessLevels.IsEnabled = true;
                btnLoadAccessLevels.Content = "Load from Instance";
            }
        }

        private SavedSettings BuildSavedSettings()
        {
            var settings = new SavedSettings
            {
                ApiUrl = txtApiUrl.Text,
                Instance = txtInstance.Text,
                Username = txtUsername.Text,
                DuplicateHandling = GetSelectedDuplicateHandling()
            };

            foreach (var rule in _rules)
            {
                if (!string.IsNullOrWhiteSpace(rule.Name))
                    settings.AccessLevelRules.Add(rule);
            }

            return settings;
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

        private void BtnExportTemplate_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                Title = "Save CSV Template",
                FileName = "import_template.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    ImportService.WriteTemplateCsv(saveFileDialog.FileName);
                    LogMessage($"Template exported to: {saveFileDialog.FileName}");
                    MessageBox.Show(
                        "Template CSV exported successfully.\n\nThe file contains sample rows — replace them with your data and keep the header row.",
                        "Template Exported", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    LogMessage($"Failed to export template: {ex.Message}");
                    MessageBox.Show($"Failed to export template: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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
                if (config.AccessLevelRules.Count == 0)
                {
                    MessageBox.Show("Please define at least one access level rule.", "No Rules", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var service = new ImportService(config);
                var previewData = service.LoadCsvForPreview(_selectedFilePath);

                var previewWindow = new PreviewWindow(previewData, config.AccessLevelRules);
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

            if (string.IsNullOrWhiteSpace(txtApiUrl.Text) ||
                string.IsNullOrWhiteSpace(txtInstance.Text) || 
                string.IsNullOrWhiteSpace(txtUsername.Text) || 
                string.IsNullOrWhiteSpace(txtPassword.Password))
            {
                MessageBox.Show("Please enter API URL, Instance, Username, and Password.", "Missing Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var config = CreateConfiguration();
            if (config.AccessLevelRules.Count == 0)
            {
                MessageBox.Show("Please define at least one access level rule.", "No Rules", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (config.DuplicateHandling == DuplicateHandling.CreateNew)
            {
                var confirm = MessageBox.Show(
                    "\"Create new\" mode is selected. If a person with the same name already exists, a duplicate entry will be created.\n\nAre you sure you want to continue?",
                    "Duplicate Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                    return;
            }

            SetUIEnabled(false);
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                // Auto-save settings before import (without password)
                try { BuildSavedSettings().Save(); } catch { }

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
                LogMessage($"People Updated: {result.PeopleUpdated}");
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
                    MessageBox.Show(
                        $"Import completed successfully!\n\nPeople Created: {result.PeopleCreated}\nPeople Updated: {result.PeopleUpdated}\nAccess Levels Assigned: {result.AccessLevelsAssigned}",
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
                var details = ImportService.FormatExceptionDetails(ex);
                LogMessage($"Import failed: {ex.Message}");
                LogMessage($"  {details}");
                MessageBox.Show($"Import failed: {ex.Message}\n\nCheck the log for full details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var config = new ImportConfiguration
            {
                ApiUrl = txtApiUrl.Text,
                Instance = txtInstance.Text,
                Username = txtUsername.Text,
                Password = txtPassword.Password,
                DuplicateHandling = GetSelectedDuplicateHandling(),
                ApiCallDelayMs = 100,
                MaxRetries = 5,
                InitialRetryDelayMs = 1000,
                MaxRetryDelayMs = 30000
            };

            foreach (var rule in _rules)
            {
                if (!string.IsNullOrWhiteSpace(rule.Name))
                {
                    config.AccessLevelRules.Add(rule);
                }
            }

            return config;
        }

        private DuplicateHandling GetSelectedDuplicateHandling()
        {
            if (rbUpdate.IsChecked == true) return DuplicateHandling.Update;
            if (rbCreateNew.IsChecked == true) return DuplicateHandling.CreateNew;
            return DuplicateHandling.Skip;
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
            dgRules.IsEnabled = enabled;
            rbSkip.IsEnabled = enabled;
            rbUpdate.IsEnabled = enabled;
            rbCreateNew.IsEnabled = enabled;
            btnSaveSettings.IsEnabled = enabled;
            btnLoadAccessLevels.IsEnabled = enabled;
            btnExportTemplate.IsEnabled = enabled;
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
