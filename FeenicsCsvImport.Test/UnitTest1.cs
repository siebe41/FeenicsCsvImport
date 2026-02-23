using FeenicsCsvImport.ClassLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace FeenicsCsvImport.Test
{
    #region UserCsvModel Tests

    [TestClass]
    public class UserCsvModelTests
    {
        [TestMethod]
        public void UserCsvModel_SetProperties_ReturnsCorrectValues()
        {
            var user = new UserCsvModel
            {
                Name = "John Doe",
                Address = "123 Main St, City, ST 12345",
                Phone = "555-0123",
                Email = "john@test.com",
                Birthday = new DateTime(1990, 5, 15)
            };

            Assert.AreEqual("John Doe", user.Name);
            Assert.AreEqual("123 Main St, City, ST 12345", user.Address);
            Assert.AreEqual("555-0123", user.Phone);
            Assert.AreEqual("john@test.com", user.Email);
            Assert.AreEqual(new DateTime(1990, 5, 15), user.Birthday);
        }

        [TestMethod]
        public void UserCsvModel_DefaultValues_AreNull()
        {
            var user = new UserCsvModel();

            Assert.IsNull(user.Name);
            Assert.IsNull(user.Address);
            Assert.IsNull(user.Phone);
            Assert.IsNull(user.Email);
            Assert.AreEqual(default(DateTime), user.Birthday);
        }
    }

    #endregion

    #region AccessLevelRule Tests

    [TestClass]
    public class AccessLevelRuleTests
    {
        [TestMethod]
        public void GetActiveOn_ReturnsDobPlusStartAge()
        {
            var rule = new AccessLevelRule { StartAge = 12 };
            var dob = new DateTime(2010, 6, 15);
            Assert.AreEqual(new DateTime(2022, 6, 15), rule.GetActiveOn(dob));
        }

        [TestMethod]
        public void GetExpiresOn_WithEndAge_ReturnsDobPlusEndAge()
        {
            var rule = new AccessLevelRule { StartAge = 12, EndAge = 14 };
            var dob = new DateTime(2010, 6, 15);
            Assert.AreEqual(new DateTime(2024, 6, 15), rule.GetExpiresOn(dob));
        }

        [TestMethod]
        public void GetExpiresOn_WithNullEndAge_ReturnsNull()
        {
            var rule = new AccessLevelRule { StartAge = 18, EndAge = null };
            var dob = new DateTime(2010, 6, 15);
            Assert.IsNull(rule.GetExpiresOn(dob));
        }

        [TestMethod]
        public void GetExpiresOn_WithZeroEndAge_ReturnsNull()
        {
            var rule = new AccessLevelRule { StartAge = 18, EndAge = 0 };
            var dob = new DateTime(2010, 6, 15);
            Assert.IsNull(rule.GetExpiresOn(dob));
        }

        [TestMethod]
        public void AgeRangeDisplay_WithEndAge_ShowsRange()
        {
            var rule = new AccessLevelRule { StartAge = 12, EndAge = 14 };
            Assert.AreEqual("12-14", rule.AgeRangeDisplay);
        }

        [TestMethod]
        public void AgeRangeDisplay_WithNullEndAge_ShowsPlus()
        {
            var rule = new AccessLevelRule { StartAge = 18, EndAge = null };
            Assert.AreEqual("18+", rule.AgeRangeDisplay);
        }

        [TestMethod]
        public void AgeRangeDisplay_WithZeroEndAge_ShowsPlus()
        {
            var rule = new AccessLevelRule { StartAge = 18, EndAge = 0 };
            Assert.AreEqual("18+", rule.AgeRangeDisplay);
        }
    }

    #endregion

    #region DuplicateHandling Tests

    [TestClass]
    public class DuplicateHandlingTests
    {
        [TestMethod]
        public void DuplicateHandling_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)DuplicateHandling.Skip);
            Assert.AreEqual(1, (int)DuplicateHandling.Update);
            Assert.AreEqual(2, (int)DuplicateHandling.CreateNew);
        }
    }

    #endregion

    #region ImportConfiguration Tests

    [TestClass]
    public class ImportConfigurationTests
    {
        [TestMethod]
        public void ImportConfiguration_DefaultApiUrl_IsCorrect()
        {
            var config = new ImportConfiguration();
            Assert.AreEqual("https://api.us.acresecurity.cloud", config.ApiUrl);
        }

        [TestMethod]
        public void ImportConfiguration_DefaultDuplicateHandling_IsSkip()
        {
            var config = new ImportConfiguration();
            Assert.AreEqual(DuplicateHandling.Skip, config.DuplicateHandling);
        }

        [TestMethod]
        public void ImportConfiguration_DefaultRetrySettings_AreCorrect()
        {
            var config = new ImportConfiguration();
            Assert.AreEqual(100, config.ApiCallDelayMs);
            Assert.AreEqual(5, config.MaxRetries);
            Assert.AreEqual(1000, config.InitialRetryDelayMs);
            Assert.AreEqual(30000, config.MaxRetryDelayMs);
        }

        [TestMethod]
        public void ImportConfiguration_DefaultAccessLevelRules_IsEmpty()
        {
            var config = new ImportConfiguration();
            Assert.IsNotNull(config.AccessLevelRules);
            Assert.AreEqual(0, config.AccessLevelRules.Count);
        }

        [TestMethod]
        public void ImportConfiguration_CreateDefault_HasThreeRules()
        {
            var config = ImportConfiguration.CreateDefault();
            Assert.AreEqual(3, config.AccessLevelRules.Count);
            Assert.AreEqual("PoolOnlyAccess-Age12", config.AccessLevelRules[0].Name);
            Assert.AreEqual(12, config.AccessLevelRules[0].StartAge);
            Assert.AreEqual(14, config.AccessLevelRules[0].EndAge);
            Assert.AreEqual("PoolAndGymAccess-Age14", config.AccessLevelRules[1].Name);
            Assert.AreEqual("PoolAndGymAfterHoursAccess-Age18", config.AccessLevelRules[2].Name);
            Assert.AreEqual(18, config.AccessLevelRules[2].StartAge);
            Assert.IsNull(config.AccessLevelRules[2].EndAge);
        }

        [TestMethod]
        public void ImportConfiguration_SetCustomValues_ReturnsCorrectValues()
        {
            var config = new ImportConfiguration
            {
                ApiUrl = "https://custom.api.com",
                Instance = "TestInstance",
                Username = "testuser",
                Password = "testpass",
                DuplicateHandling = DuplicateHandling.Update,
                ApiCallDelayMs = 200,
                MaxRetries = 10
            };

            Assert.AreEqual("https://custom.api.com", config.ApiUrl);
            Assert.AreEqual("TestInstance", config.Instance);
            Assert.AreEqual("testuser", config.Username);
            Assert.AreEqual("testpass", config.Password);
            Assert.AreEqual(DuplicateHandling.Update, config.DuplicateHandling);
            Assert.AreEqual(200, config.ApiCallDelayMs);
            Assert.AreEqual(10, config.MaxRetries);
        }
    }

    #endregion

    #region ImportPreviewModel Tests

    [TestClass]
    public class ImportPreviewModelTests
    {
        private static IList<AccessLevelRule> DefaultRules => new List<AccessLevelRule>
        {
            new AccessLevelRule { Name = "PoolOnlyAccess-Age12", StartAge = 12, EndAge = 14 },
            new AccessLevelRule { Name = "PoolAndGymAccess-Age14", StartAge = 14, EndAge = 18 },
            new AccessLevelRule { Name = "PoolAndGymAfterHoursAccess-Age18", StartAge = 18, EndAge = null }
        };

        [TestMethod]
        public void FromCsvRecord_CopiesBasicProperties()
        {
            var record = new UserCsvModel
            {
                Name = "Jane Doe",
                Email = "jane@test.com",
                Phone = "555-9876",
                Address = "456 Oak Ave",
                Birthday = new DateTime(2000, 1, 1)
            };

            var preview = ImportPreviewModel.FromCsvRecord(record, DefaultRules);

            Assert.AreEqual("Jane Doe", preview.Name);
            Assert.AreEqual("jane@test.com", preview.Email);
            Assert.AreEqual("555-9876", preview.Phone);
            Assert.AreEqual("456 Oak Ave", preview.Address);
            Assert.AreEqual(new DateTime(2000, 1, 1), preview.Birthday);
        }

        [TestMethod]
        public void FromCsvRecord_GeneratesCorrectNumberOfAccessLevels()
        {
            var record = new UserCsvModel { Name = "Test", Birthday = DateTime.UtcNow.AddYears(-10) };
            var preview = ImportPreviewModel.FromCsvRecord(record, DefaultRules);
            Assert.AreEqual(3, preview.AccessLevels.Count);
        }

        [TestMethod]
        public void FromCsvRecord_ChildUnder12_AllScheduled()
        {
            var birthday = DateTime.UtcNow.AddYears(-5);
            var record = new UserCsvModel { Name = "Young Child", Birthday = birthday };

            var preview = ImportPreviewModel.FromCsvRecord(record, DefaultRules);

            Assert.AreEqual("Scheduled", preview.AccessLevels[0].Status);
            Assert.AreEqual("Scheduled", preview.AccessLevels[1].Status);
            Assert.AreEqual("Scheduled", preview.AccessLevels[2].Status);

            Assert.AreEqual(birthday.AddYears(12), preview.AccessLevels[0].Start);
            Assert.AreEqual(birthday.AddYears(14), preview.AccessLevels[0].End);
            Assert.AreEqual(birthday.AddYears(14), preview.AccessLevels[1].Start);
            Assert.AreEqual(birthday.AddYears(18), preview.AccessLevels[1].End);
            Assert.AreEqual(birthday.AddYears(18), preview.AccessLevels[2].Start);
            Assert.IsNull(preview.AccessLevels[2].End);
        }

        [TestMethod]
        public void FromCsvRecord_Teen13_PoolActiveOthersScheduled()
        {
            var birthday = DateTime.UtcNow.AddYears(-13);
            var record = new UserCsvModel { Name = "Teen 13", Birthday = birthday };

            var preview = ImportPreviewModel.FromCsvRecord(record, DefaultRules);

            Assert.AreEqual("Active", preview.AccessLevels[0].Status);
            Assert.AreEqual("Scheduled", preview.AccessLevels[1].Status);
            Assert.AreEqual("Scheduled", preview.AccessLevels[2].Status);
        }

        [TestMethod]
        public void FromCsvRecord_Teen16_PoolExpiredGymActiveAllScheduled()
        {
            var birthday = DateTime.UtcNow.AddYears(-16);
            var record = new UserCsvModel { Name = "Teen 16", Birthday = birthday };

            var preview = ImportPreviewModel.FromCsvRecord(record, DefaultRules);

            Assert.AreEqual("Expired", preview.AccessLevels[0].Status);
            Assert.AreEqual("Active", preview.AccessLevels[1].Status);
            Assert.AreEqual("Scheduled", preview.AccessLevels[2].Status);
        }

        [TestMethod]
        public void FromCsvRecord_Adult25_FirstTwoExpiredAllActive()
        {
            var birthday = DateTime.UtcNow.AddYears(-25);
            var record = new UserCsvModel { Name = "Adult 25", Birthday = birthday };

            var preview = ImportPreviewModel.FromCsvRecord(record, DefaultRules);

            Assert.AreEqual("Expired", preview.AccessLevels[0].Status);
            Assert.AreEqual("Expired", preview.AccessLevels[1].Status);
            Assert.AreEqual("Active", preview.AccessLevels[2].Status);
        }

        [TestMethod]
        public void FromCsvRecord_PermanentRule_EndIsNull()
        {
            var birthday = DateTime.UtcNow.AddYears(-20);
            var record = new UserCsvModel { Name = "Adult", Birthday = birthday };

            var preview = ImportPreviewModel.FromCsvRecord(record, DefaultRules);

            Assert.IsNull(preview.AccessLevels[2].End);
        }

        [TestMethod]
        public void FromCsvRecord_WithCustomRules_UsesProvidedRules()
        {
            var rules = new List<AccessLevelRule>
            {
                new AccessLevelRule { Name = "Custom", StartAge = 5, EndAge = 10 }
            };
            var birthday = DateTime.UtcNow.AddYears(-7);
            var record = new UserCsvModel { Name = "Test", Birthday = birthday };

            var preview = ImportPreviewModel.FromCsvRecord(record, rules);

            Assert.AreEqual(1, preview.AccessLevels.Count);
            Assert.AreEqual("Custom", preview.AccessLevels[0].RuleName);
            Assert.AreEqual("5-10", preview.AccessLevels[0].AgeRange);
            Assert.AreEqual("Active", preview.AccessLevels[0].Status);
        }
    }

    #endregion

    #region ParseSingleStringAddress Tests

    [TestClass]
    public class ParseSingleStringAddressTests
    {
        [TestMethod]
        public void FullUSAddress_ParsesCorrectly()
        {
            var result = ImportService.ParseSingleStringAddress("123 Main Street, Springfield, IL 62701");
            Assert.AreEqual("123 Main Street", result.Street);
            Assert.AreEqual("Springfield", result.City);
            Assert.AreEqual("IL", result.Province);
            Assert.AreEqual("62701", result.PostalCode);
            Assert.AreEqual("Home", result.Type);
            Assert.AreEqual("US", result.Country);
        }

        [TestMethod]
        public void USAddressWithZipPlus4_ParsesCorrectly()
        {
            var result = ImportService.ParseSingleStringAddress("456 Oak Ave, Chicago, IL 60601-1234");
            Assert.AreEqual("456 Oak Ave", result.Street);
            Assert.AreEqual("Chicago", result.City);
            Assert.AreEqual("IL", result.Province);
            Assert.AreEqual("60601-1234", result.PostalCode);
        }

        [TestMethod]
        public void CanadianAddress_ParsesCorrectly()
        {
            var result = ImportService.ParseSingleStringAddress("789 Maple Road, Toronto, ON M5V 2T6");
            Assert.AreEqual("789 Maple Road", result.Street);
            Assert.AreEqual("Toronto", result.City);
            Assert.AreEqual("ON", result.Province);
            Assert.AreEqual("M5V 2T6", result.PostalCode);
        }

        [TestMethod]
        public void NullAddress_ReturnsDefaults()
        {
            var result = ImportService.ParseSingleStringAddress(null);
            Assert.IsNull(result.Street);
            Assert.IsNull(result.City);
            Assert.IsNull(result.Province);
            Assert.IsNull(result.PostalCode);
            Assert.AreEqual("Home", result.Type);
            Assert.AreEqual("US", result.Country);
        }

        [TestMethod]
        public void EmptyAddress_ReturnsDefaults()
        {
            var result = ImportService.ParseSingleStringAddress("");
            Assert.IsNull(result.Street);
            Assert.IsNull(result.City);
        }

        [TestMethod]
        public void WhitespaceAddress_ReturnsDefaults()
        {
            var result = ImportService.ParseSingleStringAddress("   ");
            Assert.IsNull(result.Street);
            Assert.IsNull(result.City);
        }

        [TestMethod]
        public void OnlyStreet_ParsesAsStreet()
        {
            var result = ImportService.ParseSingleStringAddress("123 Main Street");
            Assert.AreEqual("123 Main Street", result.Street);
            Assert.IsNull(result.City);
            Assert.IsNull(result.Province);
            Assert.IsNull(result.PostalCode);
        }

        [TestMethod]
        public void StreetAndCity_ParsesCorrectly()
        {
            var result = ImportService.ParseSingleStringAddress("123 Main Street, Springfield");
            Assert.AreEqual("123 Main Street", result.Street);
            Assert.AreEqual("Springfield", result.City);
        }

        [TestMethod]
        public void MultiPartStreet_ParsesCorrectly()
        {
            var result = ImportService.ParseSingleStringAddress("123 Main Street, Apt 4B, Springfield, IL 62701");
            Assert.AreEqual("123 Main Street, Apt 4B", result.Street);
            Assert.AreEqual("Springfield", result.City);
            Assert.AreEqual("IL", result.Province);
            Assert.AreEqual("62701", result.PostalCode);
        }
    }

    #endregion

    #region Is429Exception Tests

    [TestClass]
    public class Is429ExceptionTests
    {
        [TestMethod]
        public void HttpRequestExceptionWith429_ReturnsTrue()
        {
            var ex = new HttpRequestException("status code: 429");
            Assert.IsTrue(ImportService.Is429Exception(ex));
        }

        [TestMethod]
        public void HttpRequestExceptionWithTooManyRequests_ReturnsTrue()
        {
            var ex = new HttpRequestException("Too Many Requests");
            Assert.IsTrue(ImportService.Is429Exception(ex));
        }

        [TestMethod]
        public void GenericExceptionWith429_ReturnsTrue()
        {
            var ex = new Exception("Error: 429 rate limit exceeded");
            Assert.IsTrue(ImportService.Is429Exception(ex));
        }

        [TestMethod]
        public void GenericExceptionWithTooManyRequests_ReturnsTrue()
        {
            var ex = new Exception("Too Many Requests - slow down");
            Assert.IsTrue(ImportService.Is429Exception(ex));
        }

        [TestMethod]
        public void GenericExceptionWithTooManyRequestsCamelCase_ReturnsTrue()
        {
            var ex = new Exception("TooManyRequests");
            Assert.IsTrue(ImportService.Is429Exception(ex));
        }

        [TestMethod]
        public void NestedExceptionWith429_ReturnsTrue()
        {
            var inner = new HttpRequestException("429 Too Many Requests");
            var ex = new Exception("Outer", inner);
            Assert.IsTrue(ImportService.Is429Exception(ex));
        }

        [TestMethod]
        public void GenericException_ReturnsFalse()
        {
            var ex = new Exception("Something went wrong");
            Assert.IsFalse(ImportService.Is429Exception(ex));
        }

        [TestMethod]
        public void HttpRequestExceptionWith500_ReturnsFalse()
        {
            var ex = new HttpRequestException("status code: 500");
            Assert.IsFalse(ImportService.Is429Exception(ex));
        }
    }

    #endregion

    #region ImportService Constructor Tests

    [TestClass]
    public class ImportServiceConstructorTests
    {
        [TestMethod]
        public void WithValidConfig_CreatesInstance()
        {
            var service = new ImportService(new ImportConfiguration());
            Assert.IsNotNull(service);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void WithNullConfig_Throws()
        {
            new ImportService(null);
        }

        [TestMethod]
        public void WithLogger_CreatesInstance()
        {
            var service = new ImportService(new ImportConfiguration(), msg => { });
            Assert.IsNotNull(service);
        }
    }

    #endregion

    #region ImportProgress Tests

    [TestClass]
    public class ImportProgressTests
    {
        [TestMethod]
        public void SetProperties_ReturnsCorrectValues()
        {
            var progress = new ImportProgress
            {
                CurrentStep = 50,
                TotalSteps = 100,
                Message = "Processing...",
                IsError = false
            };

            Assert.AreEqual(50, progress.CurrentStep);
            Assert.AreEqual(100, progress.TotalSteps);
            Assert.AreEqual("Processing...", progress.Message);
            Assert.IsFalse(progress.IsError);
        }

        [TestMethod]
        public void DefaultValues_AreCorrect()
        {
            var progress = new ImportProgress();
            Assert.AreEqual(0, progress.CurrentStep);
            Assert.AreEqual(0, progress.TotalSteps);
            Assert.IsNull(progress.Message);
            Assert.IsFalse(progress.IsError);
        }
    }

    #endregion

    #region ImportResult Tests

    [TestClass]
    public class ImportResultTests
    {
        [TestMethod]
        public void DefaultValues_AreCorrect()
        {
            var result = new ImportResult();

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, result.PeopleCreated);
            Assert.AreEqual(0, result.PeopleUpdated);
            Assert.AreEqual(0, result.AccessLevelsAssigned);
            Assert.IsNotNull(result.Errors);
            Assert.AreEqual(0, result.Errors.Count);
            Assert.IsNotNull(result.Warnings);
            Assert.AreEqual(0, result.Warnings.Count);
        }

        [TestMethod]
        public void AddErrors_StoresCorrectly()
        {
            var result = new ImportResult();
            result.Errors.Add("Error 1");
            result.Errors.Add("Error 2");

            Assert.AreEqual(2, result.Errors.Count);
            Assert.AreEqual("Error 1", result.Errors[0]);
            Assert.AreEqual("Error 2", result.Errors[1]);
        }

        [TestMethod]
        public void AddWarnings_StoresCorrectly()
        {
            var result = new ImportResult();
            result.Warnings.Add("Warning 1");
            result.Warnings.Add("Warning 2");

            Assert.AreEqual(2, result.Warnings.Count);
            Assert.AreEqual("Warning 1", result.Warnings[0]);
            Assert.AreEqual("Warning 2", result.Warnings[1]);
        }

        [TestMethod]
        public void SetSuccessfulResult_ReturnsCorrectValues()
        {
            var result = new ImportResult
            {
                Success = true,
                PeopleCreated = 10,
                PeopleUpdated = 3,
                AccessLevelsAssigned = 30
            };

            Assert.IsTrue(result.Success);
            Assert.AreEqual(10, result.PeopleCreated);
            Assert.AreEqual(3, result.PeopleUpdated);
            Assert.AreEqual(30, result.AccessLevelsAssigned);
        }
    }

    #endregion

    #region SavedSettings Tests

    [TestClass]
    public class SavedSettingsTests
    {
        [TestMethod]
        public void DefaultValues_AreCorrect()
        {
            var settings = new SavedSettings();

            Assert.AreEqual("https://api.us.acresecurity.cloud", settings.ApiUrl);
            Assert.IsNull(settings.Instance);
            Assert.IsNull(settings.Username);
            Assert.AreEqual(DuplicateHandling.Skip, settings.DuplicateHandling);
            Assert.IsNotNull(settings.AccessLevelRules);
            Assert.AreEqual(0, settings.AccessLevelRules.Count);
        }

        [TestMethod]
        public void CreateDefault_HasThreeRules()
        {
            var settings = SavedSettings.CreateDefault();

            Assert.AreEqual(3, settings.AccessLevelRules.Count);
            Assert.AreEqual("PoolOnlyAccess-Age12", settings.AccessLevelRules[0].Name);
            Assert.AreEqual("PoolAndGymAfterHoursAccess-Age18", settings.AccessLevelRules[2].Name);
        }

        [TestMethod]
        public void SaveAndLoad_RoundTrips()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "feenics_test_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var settings = new SavedSettings
                {
                    ApiUrl = "https://test.api.com",
                    Instance = "TestInst",
                    Username = "user1",
                    DuplicateHandling = DuplicateHandling.Update
                };
                settings.AccessLevelRules.Add(new AccessLevelRule { Name = "TestRule", StartAge = 5, EndAge = 10 });
                settings.Save(tempPath);

                var loaded = SavedSettings.Load(tempPath);

                Assert.AreEqual("https://test.api.com", loaded.ApiUrl);
                Assert.AreEqual("TestInst", loaded.Instance);
                Assert.AreEqual("user1", loaded.Username);
                Assert.AreEqual(DuplicateHandling.Update, loaded.DuplicateHandling);
                Assert.AreEqual(1, loaded.AccessLevelRules.Count);
                Assert.AreEqual("TestRule", loaded.AccessLevelRules[0].Name);
                Assert.AreEqual(5, loaded.AccessLevelRules[0].StartAge);
                Assert.AreEqual(10, loaded.AccessLevelRules[0].EndAge);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [TestMethod]
        public void Load_NonExistentFile_ReturnsDefault()
        {
            var loaded = SavedSettings.Load(Path.Combine(Path.GetTempPath(), "nonexistent_12345.json"));
            Assert.AreEqual(3, loaded.AccessLevelRules.Count);
            Assert.AreEqual("https://api.us.acresecurity.cloud", loaded.ApiUrl);
        }

        [TestMethod]
        public void Load_CorruptFile_ReturnsDefault()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "feenics_corrupt_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(tempPath, "not valid json {{{");
                var loaded = SavedSettings.Load(tempPath);
                Assert.AreEqual(3, loaded.AccessLevelRules.Count);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }

    #endregion
}
