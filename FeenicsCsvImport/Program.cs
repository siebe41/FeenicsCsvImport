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

            string acreInstance = Environment.GetEnvironmentVariable("ACRE_INSTANCE");
            string acreUser = Environment.GetEnvironmentVariable("ACRE_USER");
            string acrePass = Environment.GetEnvironmentVariable("ACRE_PASS");
            string authJson = Environment.GetEnvironmentVariable("GOOGLE_AUTH_JSON");
            string webAppUrl = Environment.GetEnvironmentVariable("WEB_APP_URL");
            string macroSecret = Environment.GetEnvironmentVariable("MACRO_SECRET");
            string spreadsheetId = Environment.GetEnvironmentVariable("SPREADSHEET_ID");
            string sheetTabName = Environment.GetEnvironmentVariable("SHEET_TAB_NAME");

            if (string.IsNullOrEmpty(acreInstance) || string.IsNullOrEmpty(acreUser))
            {
                Console.WriteLine("Failed to start: Missing Acre environment variables.");
                return;
            }

            // Pass the new variables into your orchestrator
            var orchestrator = new SheetsOrchestrator(
                authJson, webAppUrl, macroSecret, spreadsheetId, sheetTabName,
                acreInstance, acreUser, acrePass);

            
        }
    }
}
