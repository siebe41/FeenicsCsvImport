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
        private readonly string _tabName; private readonly string _acreInstance;
		private readonly string _acreUser;
		private readonly string _acrePass;

		// The constructor demands the secrets be passed in
		public SheetsOrchestrator(
			string googleAuthJson, string webAppUrl, string macroSecret,
			string spreadsheetId, string sheetTabName,
			string acreInstance, string acreUser, string acrePass)
		{			
            _googleAuthJson = googleAuthJson ?? throw new ArgumentNullException(nameof(googleAuthJson));
            _webAppUrl = webAppUrl ?? throw new ArgumentNullException(nameof(webAppUrl));
            _macroSecret = macroSecret ?? throw new ArgumentNullException(nameof(macroSecret));
            _spreadsheetID = spreadsheetId ?? throw new ArgumentNullException(nameof(spreadsheetId));
            _tabName = sheetTabName ?? throw new ArgumentNullException(nameof(sheetTabName)); 
			_acreInstance = acreInstance ?? throw new ArgumentNullException(nameof(acreInstance));
			_acreUser = acreUser ?? throw new ArgumentNullException(nameof(acreUser));
			_acrePass = acrePass ?? throw new ArgumentNullException(nameof(acrePass));
		}

		public async Task ExecuteAutomationAsync()
		{
			// 1. Read data from Google Sheets
			IList<IList<object>> sheetData = await ReadSheetDataAsync();

			if (sheetData != null && sheetData.Count > 0)
			{
				Console.WriteLine($"Core: Successfully retrieved {sheetData.Count} rows from Sheets.");

				// Define a temporary file name
				string tempCsvPath = "temp_users.csv";

				try
				{
					// 2. Save data to a temporary CSV file
					SaveDataToCsv(sheetData, tempCsvPath);
					Console.WriteLine("Core: Temporary CSV file created.");

					// 3. Configure Acre Security Import
					var config = new ImportConfiguration
					{
						ApiUrl = "https://api.us.acresecurity.cloud",
						Instance = _acreInstance,
						Username = _acreUser,
						Password = _acrePass,
						DuplicateHandling = DuplicateHandling.Skip,
						ApiCallDelayMs = 100,
						MaxRetries = 5,
						InitialRetryDelayMs = 1000,
						MaxRetryDelayMs = 30000
					};

					// 4. Execute the import
					var service = new ImportService(config, Console.WriteLine);
					Console.WriteLine("Core: Executing Acre Security Import...");
					var result = await service.ExecuteImportAsync(tempCsvPath);
					Console.WriteLine("Core: Import execution finished.");

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
				finally
				{
					// 5. Cleanup: Always delete the file, even if the import fails
					if (File.Exists(tempCsvPath))
					{
						File.Delete(tempCsvPath);
						Console.WriteLine("Core: Temporary CSV file deleted.");
					}
				}
			}

			// 6. Trigger the macro (if that's still part of your workflow)
			await TriggerMacroAsync();
		}

		// --- Helper to convert Google Sheets data to CSV ---
		private void SaveDataToCsv(IList<IList<object>> data, string filePath)
		{
			using (var writer = new StreamWriter(filePath))
			{

				foreach (var row in data)
				{
					var formattedRow = row.Select(cell =>
					{
						string value = cell?.ToString() ?? "";

						// Basic escaping: If a cell contains a comma or quote, wrap it in quotes
						if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
						{
							return $"\"{value.Replace("\"", "\"\"")}\"";
						}
						return value;
					});

					writer.WriteLine(string.Join(",", formattedRow));
				}
			}
		}

		// Changed return type from Task to Task<IList<IList<object>>>
		public async Task<IList<IList<object>>> ReadSheetDataAsync()
		{
			GoogleCredential credential = CredentialFactory.FromJson<ServiceAccountCredential>(_googleAuthJson)
	        .ToGoogleCredential()
	        .CreateScoped(SheetsService.Scope.SpreadsheetsReadonly);

			var service = new SheetsService(new BaseClientService.Initializer()
			{
				HttpClientInitializer = credential,
				ApplicationName = "GitHub Actions Automation",
			});

			var request = service.Spreadsheets.Values.Get(_spreadsheetID,_tabName);
			var response = await request.ExecuteAsync();

			// Return the raw list of rows back to the caller
			return response.Values;
		}

		private async Task TriggerMacroAsync()
        {
            Console.WriteLine("Core: Triggering Web App...");
            using (var client = new HttpClient()){

                var payload = new { Secret = _macroSecret };
                string jsonString = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(_webAppUrl, content);
                response.EnsureSuccessStatusCode(); // Throws an exception if the request fails
            }
        }
    }
}