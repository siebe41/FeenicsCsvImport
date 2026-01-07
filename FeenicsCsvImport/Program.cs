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
            // Configure the import service
            var config = new ImportConfiguration
            {
                ApiUrl = "https://api.us.acresecurity.cloud",
                Instance = "YOUR_INSTANCE",
                Username = "YOUR_USER",
                Password = "YOUR_PASS",
                PoolAccessLevelName = "PoolOnlyAccess-Age12",
                PoolGymAccessLevelName = "PoolAndGymAccess-Age14",
                AllAccessLevelName = "PoolAndGymAfterHoursAccess-Age18",
                ApiCallDelayMs = 100,
                MaxRetries = 5,
                InitialRetryDelayMs = 1000,
                MaxRetryDelayMs = 30000
            };

            // Create service with console logging
            var service = new ImportService(config, Console.WriteLine);

            // Execute import
            var result = await service.ExecuteImportAsync("users.csv");

            // Report final results
            Console.WriteLine();
            Console.WriteLine("=== Import Summary ===");
            Console.WriteLine($"Success: {result.Success}");
            Console.WriteLine($"People Created: {result.PeopleCreated}");
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
    }
}
