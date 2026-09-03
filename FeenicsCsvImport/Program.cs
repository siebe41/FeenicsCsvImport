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
            if (args.Length > 0 && string.Equals(args[0], "door-usage-report", StringComparison.OrdinalIgnoreCase))
            {
                await RunDoorUsageReportAsync(args);
                return;
            }

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

        /// <summary>
        /// Usage: FeenicsCsvImport.exe door-usage-report [--door "Side Entrance"] [--months 9]
        ///          [--out report.csv] [--include-denied] [--dump-events N]
        /// Reads ACRE_INSTANCE / ACRE_USER / ACRE_PASS (and optional ACRE_API_URL) the same way
        /// the CSV sync job does, so it can reuse the existing GitHub Actions secrets.
        /// </summary>
        static async Task RunDoorUsageReportAsync(string[] args)
        {
            Console.WriteLine("=== Door Usage Report ===");

            string acreInstance = Environment.GetEnvironmentVariable("ACRE_INSTANCE");
            string acreUser = Environment.GetEnvironmentVariable("ACRE_USER");
            string acrePass = Environment.GetEnvironmentVariable("ACRE_PASS");
            string apiUrl = Environment.GetEnvironmentVariable("ACRE_API_URL");
            if (string.IsNullOrWhiteSpace(apiUrl))
                apiUrl = "https://api.us.acresecurity.cloud";

            if (string.IsNullOrEmpty(acreInstance) || string.IsNullOrEmpty(acreUser) || string.IsNullOrEmpty(acrePass))
            {
                Console.WriteLine("FATAL: ACRE_INSTANCE, ACRE_USER, and ACRE_PASS environment variables are required.");
                Environment.ExitCode = 1;
                return;
            }

            string door = "Side Entrance";
            int months = 9;
            string outPath = null;
            bool includeDenied = false;
            int dumpCount = 0;
            int dumpDays = 30;
            int queryTimeoutMs = 120000;

            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--door":
                        door = args[++i];
                        break;
                    case "--months":
                        months = int.Parse(args[++i]);
                        break;
                    case "--out":
                        outPath = args[++i];
                        break;
                    case "--include-denied":
                        includeDenied = true;
                        break;
                    case "--dump-events":
                        dumpCount = int.Parse(args[++i]);
                        break;
                    case "--dump-events-days":
                        dumpDays = int.Parse(args[++i]);
                        break;
                    case "--query-timeout":
                        queryTimeoutMs = int.Parse(args[++i]);
                        break;
                    default:
                        Console.WriteLine($"Unknown argument: {args[i]}");
                        break;
                }
            }

            var service = new DoorUsageReportService(apiUrl, acreInstance, acreUser, acrePass, Console.WriteLine);

            try
            {
                if (dumpCount > 0)
                {
                    Console.WriteLine($"Dumping the {dumpCount} most recent raw event(s) to help verify field names...");
                    await service.DumpRecentEventsAsync(dumpCount, dumpDays, queryTimeoutMs);
                    return;
                }

                var result = await service.RunAsync(door, months, includeDenied, queryTimeoutMs);

                if (!result.Success)
                {
                    foreach (var err in result.Errors)
                        Console.WriteLine($"ERROR: {err}");
                    Environment.ExitCode = 1;
                    return;
                }

                foreach (var warn in result.Warnings)
                    Console.WriteLine($"WARNING: {warn}");

                Console.WriteLine();
                Console.WriteLine($"{"Name",-30} {"Email",-35} {"Last Used (UTC)",-17} Events");
                foreach (var p in result.People)
                {
                    Console.WriteLine($"{p.Name,-30} {p.Email ?? "(no email on file)",-35} {p.LastUsedUtc:yyyy-MM-dd HH:mm} {p.EventCount,6}");
                }
                Console.WriteLine();
                Console.WriteLine($"Total: {result.People.Count} distinct people, {result.EventsScanned} matching event(s) scanned.");

                if (!string.IsNullOrWhiteSpace(outPath))
                {
                    using (var writer = new StreamWriter(outPath))
                    {
                        writer.WriteLine("Name,Email,LastUsedUtc,EventCount");
                        foreach (var p in result.People)
                        {
                            writer.WriteLine($"\"{p.Name?.Replace("\"", "\"\"")}\",\"{p.Email}\",{p.LastUsedUtc:O},{p.EventCount}");
                        }
                    }
                    Console.WriteLine($"CSV written to {outPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }
    }
}
