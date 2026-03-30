using System;
using System.IO.Ports;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Feenics.Keep.WebApi.Wrapper;
using FeenicsCsvImport.ClassLibrary;

namespace FeenicsCardSwipeMonitor
{
    public partial class SettingsWindow : Window
    {
        private string _updateUrl;

        public SettingsWindow()
        {
            InitializeComponent();

            // Apply the same Shield icon used by the system tray
            var icon = System.Drawing.SystemIcons.Shield;
            Icon = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            // Load existing non-sensitive settings
            TxtInstance.Text = Properties.Settings.Default.InstanceName;
            TxtUser.Text = Properties.Settings.Default.ApiUsername;
            ChkLogToFeenics.IsChecked = Properties.Settings.Default.LogToFeenics;

            // Load COM Ports and Serial Settings
            LoadComPorts();
            LoadSerialSettings();
        }

        private void LoadComPorts()
        {
            string savedPort = Properties.Settings.Default.ComPort;
            CmbCom.Items.Clear();

            // Grab all active COM ports from Windows
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                CmbCom.Items.Add(port);
            }

            // Select the saved port if it exists. If it's a new port not in the list, add it anyway.
            if (!string.IsNullOrEmpty(savedPort))
            {
                if (!CmbCom.Items.Contains(savedPort))
                {
                    CmbCom.Items.Add(savedPort);
                }
                CmbCom.SelectedItem = savedPort;
            }
            else if (CmbCom.Items.Count > 0)
            {
                CmbCom.SelectedIndex = 0; // Default to the first found port
            }
        }

        private void LoadSerialSettings()
        {
            // Populate dropdown options
            CmbBaud.ItemsSource = new int[] { 4800, 9600, 19200, 38400, 57600, 115200 };
            CmbDataBits.ItemsSource = new int[] { 7, 8 };
            CmbParity.ItemsSource = Enum.GetNames(typeof(Parity));
            CmbStopBits.ItemsSource = Enum.GetNames(typeof(StopBits));

            // Select saved or default values
            CmbBaud.SelectedItem = Properties.Settings.Default.BaudRate != 0 ? Properties.Settings.Default.BaudRate : 9600;
            CmbDataBits.SelectedItem = Properties.Settings.Default.DataBits != 0 ? Properties.Settings.Default.DataBits : 8;
            CmbParity.SelectedItem = !string.IsNullOrEmpty(Properties.Settings.Default.Parity) ? Properties.Settings.Default.Parity : "None";
            CmbStopBits.SelectedItem = !string.IsNullOrEmpty(Properties.Settings.Default.StopBits) ? Properties.Settings.Default.StopBits : "One";
        }

        private void BtnRefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            LoadComPorts();
            TxtStatus.Text = "Port list refreshed.";
        }

        private async void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            BtnTest.IsEnabled = false;
            TxtStatus.Text = "";

            var status = new StringBuilder();

            // --- Test Feenics API Connection ---
            string instance = TxtInstance.Text.Trim();
            string username = TxtUser.Text.Trim();
            string password = TxtPass.Password;

            // If no password was typed, try the saved encrypted password
            if (string.IsNullOrEmpty(password))
            {
                password = Decrypt(Properties.Settings.Default.EncryptedPassword);
            }

            if (string.IsNullOrWhiteSpace(instance) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                status.AppendLine("API: Skipped — instance, username, or password is empty.");
            }
            else
            {
                status.AppendLine("API: Connecting...");
                TxtStatus.Text = status.ToString();

                try
                {
                    var client = new Client("https://api.us.acresecurity.cloud");
                    var (success, error, msg) = await client.LoginAsync(instance, username, password);

                    if (success)
                    {
                        var inst = await client.GetCurrentInstanceAsync();
                        status.Clear();
                        status.AppendLine($"API: OK — connected to \"{inst.CommonName}\".");
                    }
                    else
                    {
                        status.Clear();
                        status.AppendLine($"API: FAILED — {msg}");
                    }
                }
                catch (Exception ex)
                {
                    status.Clear();
                    status.AppendLine($"API: ERROR — {ex.Message}");
                }
            }

            // --- Test COM Port ---
            string comPort = CmbCom.Text.Trim(); // Read from ComboBox instead of TextBox
            if (string.IsNullOrWhiteSpace(comPort))
            {
                status.AppendLine("COM: Skipped — no port specified.");
            }
            else
            {
                try
                {
                    // Pull parameters from dropdowns
                    int baud = CmbBaud.SelectedItem != null ? (int)CmbBaud.SelectedItem : 9600;
                    int dataBits = CmbDataBits.SelectedItem != null ? (int)CmbDataBits.SelectedItem : 8;
                    Parity parity = CmbParity.SelectedItem != null ? (Parity)Enum.Parse(typeof(Parity), CmbParity.SelectedItem.ToString()) : Parity.None;
                    StopBits stopBits = CmbStopBits.SelectedItem != null ? (StopBits)Enum.Parse(typeof(StopBits), CmbStopBits.SelectedItem.ToString()) : StopBits.One;

                    using (var sp = new SerialPort(comPort, baud, parity, dataBits, stopBits))
                    {
                        sp.Open();
                        status.AppendLine($"COM: OK — {comPort} opened successfully at {baud} baud.");
                        sp.Close();
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    status.AppendLine($"COM: FAILED — {comPort} is in use by another program (or the configuration utility).");
                }
                catch (Exception ex)
                {
                    status.AppendLine($"COM: FAILED — {ex.Message}");
                }
            }

            TxtStatus.Text = status.ToString().TrimEnd();
            BtnTest.IsEnabled = true;
        }

        private async void BtnSimulate_Click(object sender, RoutedEventArgs e)
        {
            string badge = TxtSimBadge.Text.Trim();
            if (string.IsNullOrWhiteSpace(badge))
            {
                TxtStatus.Text = "Simulate: Enter a badge number first.";
                return;
            }

            // Use current form values, falling back to saved settings
            string instance = TxtInstance.Text.Trim();
            string username = TxtUser.Text.Trim();
            string password = TxtPass.Password;

            if (string.IsNullOrEmpty(instance))
                instance = Properties.Settings.Default.InstanceName;
            if (string.IsNullOrEmpty(username))
                username = Properties.Settings.Default.ApiUsername;
            if (string.IsNullOrEmpty(password))
                password = Decrypt(Properties.Settings.Default.EncryptedPassword);

            if (string.IsNullOrWhiteSpace(instance) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                TxtStatus.Text = "Simulate: Fill in the API credentials (or Save them first).";
                return;
            }

            BtnSimulate.IsEnabled = false;
            TxtStatus.Text = $"Simulate: Processing badge \"{badge}\"...";

            try
            {
                Clipboard.SetText(badge);

                var config = new ImportConfiguration
                {
                    ApiUrl = "https://api.us.acresecurity.cloud",
                    Instance = instance,
                    Username = username,
                    Password = password,
                    DuplicateHandling = DuplicateHandling.Update,
                    ApiCallDelayMs = 100,
                    MaxRetries = 5,
                    InitialRetryDelayMs = 1000,
                    MaxRetryDelayMs = 30000
                };

                var service = new ImportService(config, msg => Dispatcher.Invoke(() =>
                {
                    TxtStatus.AppendText(Environment.NewLine + msg);
                    TxtStatus.ScrollToEnd();
                }));

                await service.PostDeskLoginByCardAsync(badge);

                TxtStatus.AppendText(Environment.NewLine + "Simulate: Done.");
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Simulate: ERROR — {ex.Message}";
            }

            BtnSimulate.IsEnabled = true;
        }

        private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            BtnCheckUpdate.IsEnabled = false;
            BtnDownloadUpdate.Visibility = Visibility.Collapsed;
            TxtStatus.Text = "Checking GitHub for updates...";

            try
            {
                Version currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

                // Wrap in a using statement since we only need it briefly for this click
                using (var updateService = new UpdateService("siebe41", "FeenicsCsvImport", currentVersion))
                {
                    var result = await updateService.CheckForUpdatesAsync();

                    if (result.IsUpdateAvailable)
                    {
                        TxtStatus.Text = $"Update Available! You are on {currentVersion.ToString(3)}, but {result.LatestVersion} is available.";
                        _updateUrl = result.ReleaseUrl;
                        BtnDownloadUpdate.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        TxtStatus.Text = $"You are up to date! (v{currentVersion.ToString(3)})";
                    }
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Error checking for updates: {ex.Message}";
            }

            BtnCheckUpdate.IsEnabled = true;
        }

        private void BtnDownloadUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_updateUrl))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_updateUrl) { UseShellExecute = true });
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Encrypt password only if a new one was typed
            if (!string.IsNullOrEmpty(TxtPass.Password))
            {
                byte[] clearBytes = Encoding.UTF8.GetBytes(TxtPass.Password);
                byte[] encryptedBytes = ProtectedData.Protect(clearBytes, null, DataProtectionScope.CurrentUser);
                Properties.Settings.Default.EncryptedPassword = Convert.ToBase64String(encryptedBytes);
            }

            bool isLoggingEnabled = ChkLogToFeenics.IsChecked ?? false;

            // Save to App Settings
            Properties.Settings.Default.InstanceName = TxtInstance.Text;
            Properties.Settings.Default.ApiUsername = TxtUser.Text;
            Properties.Settings.Default.ComPort = CmbCom.Text;
            Properties.Settings.Default.LogToFeenics = isLoggingEnabled;

            // Save serial settings
            if (CmbBaud.SelectedItem != null) Properties.Settings.Default.BaudRate = (int)CmbBaud.SelectedItem;
            if (CmbDataBits.SelectedItem != null) Properties.Settings.Default.DataBits = (int)CmbDataBits.SelectedItem;
            if (CmbParity.SelectedItem != null) Properties.Settings.Default.Parity = CmbParity.SelectedItem.ToString();
            if (CmbStopBits.SelectedItem != null) Properties.Settings.Default.StopBits = CmbStopBits.SelectedItem.ToString();

            Properties.Settings.Default.Save();

            // Apply settings immediately so no restart is needed
            if (Application.Current is App app)
            {
                app.ApplySettings();
            }

            this.Close();
        }

        private string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return "";
            try
            {
                byte[] data = Convert.FromBase64String(cipherText);
                byte[] decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch { return ""; }
        }
    }
}