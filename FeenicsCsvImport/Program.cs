using CsvHelper;
using Feenics.Keep.WebApi.Model;
using Feenics.Keep.WebApi.Wrapper;
using FeenicsCsvImport.ClassLibrary;
using MQTTnet.Client;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FeenicsCsvImport
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("=== Starting FeenicsCsvImport ===");

                string acreInstance = Environment.GetEnvironmentVariable("ACRE_INSTANCE");
                string acreUser = Environment.GetEnvironmentVariable("ACRE_USER");
                string acrePass = Environment.GetEnvironmentVariable("ACRE_PASS");
                string authJson = Environment.GetEnvironmentVariable("GOOGLE_AUTH_JSON");
                string webAppUrl = Environment.GetEnvironmentVariable("WEB_APP_URL");
                string macroSecret = Environment.GetEnvironmentVariable("MACRO_SECRET");
                string spreadsheetId = Environment.GetEnvironmentVariable("SPREADSHEET_ID");
                string sheetTabName = Environment.GetEnvironmentVariable("SHEET_TAB_NAME");
                string accessLevelRules = Environment.GetEnvironmentVariable("ACCESS_LEVEL_RULES");

                Console.WriteLine($"ACRE_INSTANCE: {(string.IsNullOrEmpty(acreInstance) ? "MISSING" : "set")}");
                Console.WriteLine($"ACRE_USER: {(string.IsNullOrEmpty(acreUser) ? "MISSING" : "set")}");
                Console.WriteLine($"ACRE_PASS: {(string.IsNullOrEmpty(acrePass) ? "MISSING" : "set")}");
                Console.WriteLine($"GOOGLE_AUTH_JSON: {(string.IsNullOrEmpty(authJson) ? "MISSING" : $"set ({authJson.Length} chars)")}");
                Console.WriteLine($"WEB_APP_URL: {(string.IsNullOrEmpty(webAppUrl) ? "MISSING" : "set")}");
                Console.WriteLine($"MACRO_SECRET: {(string.IsNullOrEmpty(macroSecret) ? "MISSING" : "set")}");
                Console.WriteLine($"SPREADSHEET_ID: {(string.IsNullOrEmpty(spreadsheetId) ? "MISSING" : "set")}");
                Console.WriteLine($"SHEET_TAB_NAME: {(string.IsNullOrEmpty(sheetTabName) ? "MISSING" : sheetTabName)}");
                Console.WriteLine($"ACCESS_LEVEL_RULES: {(string.IsNullOrEmpty(accessLevelRules) ? "MISSING" : $"set ({accessLevelRules.Length} chars)")}");

                if (string.IsNullOrEmpty(acreInstance) || string.IsNullOrEmpty(acreUser))
                {
                    Console.WriteLine("FATAL: Missing Acre environment variables.");
                    Environment.ExitCode = 1;
                    return;
                }

                Console.WriteLine("Creating SheetsOrchestrator...");
                var orchestrator = new SheetsOrchestrator(
                    authJson, webAppUrl, macroSecret, spreadsheetId, sheetTabName,
                    acreInstance, acreUser, acrePass, accessLevelRules);

                Console.WriteLine("Starting automation...");
                await orchestrator.ExecuteAutomationAsync();
                Console.WriteLine("=== FeenicsCsvImport completed successfully ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL UNHANDLED EXCEPTION in Main: {ex.GetType().FullName}");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner Stack Trace: {ex.InnerException.StackTrace}");
                }
                Environment.ExitCode = 1;
            }
        }
    }
}
