using System;
using System.IO.Ports;
using System.Security.Cryptography;
using System.Text;
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
            TxtCom.Text = Properties.Settings.Default.ComPort;
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
            string comPort = TxtCom.Text.Trim();
            if (string.IsNullOrWhiteSpace(comPort))
            {
                status.AppendLine("COM: Skipped — no port specified.");
            }
            else
            {
                try
                {
                    using (var sp = new SerialPort(comPort, 9600, Parity.None, 8, StopBits.One))
                    {
                        sp.Open();
                        status.AppendLine($"COM: OK — {comPort} opened successfully.");
                        sp.Close();
                    }
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

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Encrypt password
            byte[] clearBytes = Encoding.UTF8.GetBytes(TxtPass.Password);
            byte[] encryptedBytes = ProtectedData.Protect(clearBytes, null, DataProtectionScope.CurrentUser);

            // Save to App Settings
            Properties.Settings.Default.InstanceName = TxtInstance.Text;
            Properties.Settings.Default.ApiUsername = TxtUser.Text;
            Properties.Settings.Default.EncryptedPassword = Convert.ToBase64String(encryptedBytes);
            Properties.Settings.Default.ComPort = TxtCom.Text;
            Properties.Settings.Default.Save();

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
