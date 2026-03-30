using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FeenicsCsvImport.ClassLibrary
{
    public class SheetsOrchestrator
    {
        private readonly string _googleAuthJson;
        private readonly string _webAppUrl;
        private readonly string _macroSecret;
        private readonly string _spreadsheetID;
        private readonly string _tabName;
        private readonly string _acreInstance;
		private readonly string _acreUser;
		private readonly string _acrePass;
		private readonly string _accessLevelRulesJson;

		// The constructor demands the secrets be passed in
		public SheetsOrchestrator(
			string googleAuthJson, string webAppUrl, string macroSecret,
			string spreadsheetId, string sheetTabName,
			string acreInstance, string acreUser, string acrePass,
			string accessLevelRulesJson)
		{			
            _googleAuthJson = googleAuthJson ?? throw new ArgumentNullException(nameof(googleAuthJson));
            _webAppUrl = webAppUrl ?? throw new ArgumentNullException(nameof(webAppUrl));
            _macroSecret = macroSecret ?? throw new ArgumentNullException(nameof(macroSecret));
            _spreadsheetID = spreadsheetId ?? throw new ArgumentNullException(nameof(spreadsheetId));
            _tabName = sheetTabName ?? throw new ArgumentNullException(nameof(sheetTabName)); 
			_acreInstance = acreInstance ?? throw new ArgumentNullException(nameof(acreInstance));
			_acreUser = acreUser ?? throw new ArgumentNullException(nameof(acreUser));
			_acrePass = acrePass ?? throw new ArgumentNullException(nameof(acrePass));
			_accessLevelRulesJson = accessLevelRulesJson ?? throw new ArgumentNullException(nameof(accessLevelRulesJson));
		}

		public async Task ExecuteAutomationAsync()
		{
			// Step 1: Trigger macro to refresh sheet data
			//try
			//{
			//	Console.WriteLine("Step 1: Triggering macro to refresh sheet data...");
			//	await TriggerMacroAsync();
			//	Console.WriteLine("Step 1: Macro triggered successfully.");
			//}
			//catch (Exception ex)
			//{
			//	Console.WriteLine($"Step 1 FAILED: Trigger macro: {ex.GetType().FullName}: {ex.Message}");
			//	Console.WriteLine($"Stack Trace: {ex.StackTrace}");
			//	throw;
			//}

			// Step 2: Read data from Google Sheets
			IList<IList<object>> sheetData;
			try
			{
				Console.WriteLine("Step 2: Reading data from Google Sheets...");
				sheetData = await ReadSheetDataAsync();
				Console.WriteLine($"Step 2: Read {sheetData?.Count ?? 0} rows from Sheets.");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Step 2 FAILED: Read sheet data: {ex.GetType().FullName}: {ex.Message}");
				Console.WriteLine($"Stack Trace: {ex.StackTrace}");
				throw;
			}

			if (sheetData == null || sheetData.Count == 0)
			{
				Console.WriteLine("No data found in sheet. Nothing to import.");
				return;
			}

			string tempCsvPath = "temp_users.csv";

			try
			{
				// Step 3: Save data to CSV
				try
				{
					Console.WriteLine("Step 3: Saving sheet data to temporary CSV...");
					SaveDataToCsv(sheetData, tempCsvPath);
					Console.WriteLine($"Step 3: CSV created at '{tempCsvPath}'.");
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Step 3 FAILED: Save CSV: {ex.GetType().FullName}: {ex.Message}");
					Console.WriteLine($"Stack Trace: {ex.StackTrace}");
					throw;
				}

				// Step 4: Configure and run Acre Security Import
				try
				{
					Console.WriteLine("Step 4: Configuring Acre Security import...");

					Console.WriteLine("Step 4: Parsing access level rules from JSON...");
					var accessLevelRules = AccessLevelRule.ParseFromJson(_accessLevelRulesJson);
					Console.WriteLine($"Step 4: Parsed {accessLevelRules.Count} access level rule(s):");
					foreach (var rule in accessLevelRules)
					{
						Console.WriteLine($"  - {rule.Name} (Age {rule.AgeRangeDisplay})");
					}

                    var config = new ImportConfiguration
					{
						ApiUrl = "https://api.us.acresecurity.cloud",
						Instance = _acreInstance,
						Username = _acreUser,
						Password = _acrePass,
						DuplicateHandling = DuplicateHandling.Update,
						ApiCallDelayMs = 100,
						MaxRetries = 5,
						InitialRetryDelayMs = 1000,
						MaxRetryDelayMs = 30000,
						AccessLevelRules = accessLevelRules
					};

					var service = new ImportService(config, Console.WriteLine);
					Console.WriteLine("Step 4: Executing import...");
					var result = await service.ExecuteImportAsync(tempCsvPath);
					Console.WriteLine("Step 4: Import execution finished.");

					// Report final results
					Console.WriteLine();
					Console.WriteLine("=== Import Summary ===");
					Console.WriteLine($"Success: {result.Success}");
					Console.WriteLine($"People Created: {result.PeopleCreated}");
					Console.WriteLine($"People Updated: {result.PeopleUpdated}");
					Console.WriteLine($"Access Levels Assigned: {result.AccessLevelsAssigned}");

					if (result.Errors.Count > 0)
					{
						Console.WriteLine();
						Console.WriteLine("Errors:");
						foreach (var error in result.Errors)
						{
							Console.WriteLine($"  - {error}");
						}
					}

					if (result.Warnings.Count > 0)
					{
						Console.WriteLine();
						Console.WriteLine("Warnings:");
						foreach (var warning in result.Warnings)
						{
							Console.WriteLine($"  - {warning}");
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Step 4 FAILED: Acre import: {ex.GetType().FullName}: {ex.Message}");
					Console.WriteLine($"Stack Trace: {ex.StackTrace}");
					throw;
				}
			}
			finally
			{
				if (File.Exists(tempCsvPath))
				{
					File.Delete(tempCsvPath);
					Console.WriteLine("Cleanup: Temporary CSV file deleted.");
				}
			}

			// Step 5: Trigger post-import macro
			try
			{
				Console.WriteLine("Step 5: Triggering post-import macro...");
				await TriggerMacroAsync();
				Console.WriteLine("Step 5: Post-import macro triggered successfully.");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Step 5 FAILED: Post-import macro: {ex.GetType().FullName}: {ex.Message}");
				Console.WriteLine($"Stack Trace: {ex.StackTrace}");
				throw;
			}
		}

		// --- Helper to convert Google Sheets data to CSV ---
		private void SaveDataToCsv(IList<IList<object>> data, string filePath)
		{
			using (var writer = new StreamWriter(filePath))
			{
				// Google Sheets API omits trailing empty cells, producing jagged rows.
				// Determine the expected column count from the header row and pad
				// shorter rows so CsvHelper can map every column correctly.
				int columnCount = data.Count > 0 ? data[0].Count : 0;

				foreach (var row in data)
				{
					// Track the actual column count across all rows
					if (row.Count > columnCount)
						columnCount = row.Count;
				}

				foreach (var row in data)
				{
					var cells = new List<string>(columnCount);
					for (int c = 0; c < columnCount; c++)
					{
						string value = c < row.Count ? (row[c]?.ToString() ?? "") : "";

						// Basic escaping: If a cell contains a comma or quote, wrap it in quotes
						if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
						{
							value = $"\"{value.Replace("\"", "\"\"")}\"";
						}
						cells.Add(value);
					}

					writer.WriteLine(string.Join(",", cells));
				}
			}
		}

		// Changed return type from Task to Task<IList<IList<object>>>
		public async Task<IList<IList<object>>> ReadSheetDataAsync()
		{
			Console.WriteLine("ReadSheetDataAsync: Creating Google credential...");
			GoogleCredential credential;
			try
			{
				credential = CredentialFactory.FromJson<ServiceAccountCredential>(_googleAuthJson)
					.ToGoogleCredential()
					.CreateScoped(SheetsService.Scope.SpreadsheetsReadonly);
				Console.WriteLine("ReadSheetDataAsync: Credential created successfully.");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"ReadSheetDataAsync FAILED: Create credential: {ex.GetType().FullName}: {ex.Message}");
				throw;
			}

			Console.WriteLine("ReadSheetDataAsync: Creating SheetsService...");
			var service = new SheetsService(new BaseClientService.Initializer()
			{
				HttpClientInitializer = credential,
				ApplicationName = "GitHub Actions Automation",
			});

			Console.WriteLine($"ReadSheetDataAsync: Requesting data from spreadsheet '{_spreadsheetID}', tab '{_tabName}'...");
			try
			{
				var request = service.Spreadsheets.Values.Get(_spreadsheetID, _tabName);
				var response = await request.ExecuteAsync();
				Console.WriteLine($"ReadSheetDataAsync: Got {response.Values?.Count ?? 0} rows.");
				return response.Values;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"ReadSheetDataAsync FAILED: Sheets API call: {ex.GetType().FullName}: {ex.Message}");
				throw;
			}
		}

		private async Task TriggerMacroAsync()
        {
            Console.WriteLine($"TriggerMacroAsync: Posting to web app URL...");
            try
            {
                using (var client = new HttpClient())
                {
                    var payload = new { Secret = _macroSecret };
                    string jsonString = JsonSerializer.Serialize(payload);
                    var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(_webAppUrl, content);
                    Console.WriteLine($"TriggerMacroAsync: Response status: {response.StatusCode}");
                    response.EnsureSuccessStatusCode();
                    Console.WriteLine("TriggerMacroAsync: Success.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TriggerMacroAsync FAILED: {ex.GetType().FullName}: {ex.Message}");
                throw;
            }
        }
    }
}