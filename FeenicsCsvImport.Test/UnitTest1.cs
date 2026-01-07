using FeenicsCsvImport.ClassLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
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
            // Arrange & Act
            var user = new UserCsvModel
            {
                Name = "John Doe",
                Address = "123 Main St, City, ST 12345",
                Phone = "555-123-4567",
                Email = "john@example.com",
                Birthday = new DateTime(1990, 5, 15)
            };

            // Assert
            Assert.AreEqual("John Doe", user.Name);
            Assert.AreEqual("123 Main St, City, ST 12345", user.Address);
            Assert.AreEqual("555-123-4567", user.Phone);
            Assert.AreEqual("john@example.com", user.Email);
            Assert.AreEqual(new DateTime(1990, 5, 15), user.Birthday);
        }

        [TestMethod]
        public void UserCsvModel_DefaultValues_AreNull()
        {
            // Arrange & Act
            var user = new UserCsvModel();

            // Assert
            Assert.IsNull(user.Name);
            Assert.IsNull(user.Address);
            Assert.IsNull(user.Phone);
            Assert.IsNull(user.Email);
            Assert.AreEqual(default(DateTime), user.Birthday);
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
            // Arrange & Act
            var config = new ImportConfiguration();

            // Assert
            Assert.AreEqual("https://api.us.acresecurity.cloud", config.ApiUrl);
        }

        [TestMethod]
        public void ImportConfiguration_DefaultAccessLevelNames_AreCorrect()
        {
            // Arrange & Act
            var config = new ImportConfiguration();

            // Assert
            Assert.AreEqual("PoolOnlyAccess-Age12", config.PoolAccessLevelName);
            Assert.AreEqual("PoolAndGymAccess-Age14", config.PoolGymAccessLevelName);
            Assert.AreEqual("PoolAndGymAfterHoursAccess-Age18", config.AllAccessLevelName);
        }

        [TestMethod]
        public void ImportConfiguration_DefaultRetrySettings_AreCorrect()
        {
            // Arrange & Act
            var config = new ImportConfiguration();

            // Assert
            Assert.AreEqual(100, config.ApiCallDelayMs);
            Assert.AreEqual(5, config.MaxRetries);
            Assert.AreEqual(1000, config.InitialRetryDelayMs);
            Assert.AreEqual(30000, config.MaxRetryDelayMs);
        }

        [TestMethod]
        public void ImportConfiguration_SetCustomValues_ReturnsCorrectValues()
        {
            // Arrange & Act
            var config = new ImportConfiguration
            {
                ApiUrl = "https://custom.api.com",
                Instance = "TestInstance",
                Username = "testuser",
                Password = "testpass",
                PoolAccessLevelName = "CustomPool",
                PoolGymAccessLevelName = "CustomPoolGym",
                AllAccessLevelName = "CustomAll",
                ApiCallDelayMs = 200,
                MaxRetries = 10,
                InitialRetryDelayMs = 2000,
                MaxRetryDelayMs = 60000
            };

            // Assert
            Assert.AreEqual("https://custom.api.com", config.ApiUrl);
            Assert.AreEqual("TestInstance", config.Instance);
            Assert.AreEqual("testuser", config.Username);
            Assert.AreEqual("testpass", config.Password);
            Assert.AreEqual("CustomPool", config.PoolAccessLevelName);
            Assert.AreEqual("CustomPoolGym", config.PoolGymAccessLevelName);
            Assert.AreEqual("CustomAll", config.AllAccessLevelName);
            Assert.AreEqual(200, config.ApiCallDelayMs);
            Assert.AreEqual(10, config.MaxRetries);
            Assert.AreEqual(2000, config.InitialRetryDelayMs);
            Assert.AreEqual(60000, config.MaxRetryDelayMs);
        }
    }

    #endregion

    #region ImportPreviewModel Tests

    [TestClass]
    public class ImportPreviewModelTests
    {
        [TestMethod]
        public void FromCsvRecord_CopiesBasicProperties()
        {
            // Arrange
            var record = new UserCsvModel
            {
                Name = "Jane Doe",
                Email = "jane@example.com",
                Phone = "555-987-6543",
                Address = "456 Oak Ave",
                Birthday = new DateTime(2000, 1, 1)
            };

            // Act
            var preview = ImportPreviewModel.FromCsvRecord(record);

            // Assert
            Assert.AreEqual("Jane Doe", preview.Name);
            Assert.AreEqual("jane@example.com", preview.Email);
            Assert.AreEqual("555-987-6543", preview.Phone);
            Assert.AreEqual("456 Oak Ave", preview.Address);
            Assert.AreEqual(new DateTime(2000, 1, 1), preview.Birthday);
        }

        [TestMethod]
        public void FromCsvRecord_ChildUnder12_AllAccessScheduled()
        {
            // Arrange - Child born 5 years ago (under 12)
            var birthday = DateTime.UtcNow.AddYears(-5);
            var record = new UserCsvModel
            {
                Name = "Young Child",
                Birthday = birthday
            };

            // Act
            var preview = ImportPreviewModel.FromCsvRecord(record);

            // Assert - All access levels should be scheduled
            Assert.AreEqual("Scheduled", preview.PoolAccessStatus);
            Assert.AreEqual("Scheduled", preview.PoolGymAccessStatus);
            Assert.AreEqual("Scheduled", preview.AllAccessStatus);

            // Verify dates
            Assert.AreEqual(birthday.AddYears(12), preview.PoolAccessStart);
            Assert.AreEqual(birthday.AddYears(14), preview.PoolAccessEnd);
            Assert.AreEqual(birthday.AddYears(14), preview.PoolGymAccessStart);
            Assert.AreEqual(birthday.AddYears(18), preview.PoolGymAccessEnd);
            Assert.AreEqual(birthday.AddYears(18), preview.AllAccessStart);
        }

        [TestMethod]
        public void FromCsvRecord_Teen13YearsOld_PoolActiveOthersScheduled()
        {
            // Arrange - Teen born 13 years ago (between 12 and 14)
            var birthday = DateTime.UtcNow.AddYears(-13);
            var record = new UserCsvModel
            {
                Name = "Teen 13",
                Birthday = birthday
            };

            // Act
            var preview = ImportPreviewModel.FromCsvRecord(record);

            // Assert
            Assert.AreEqual("Active", preview.PoolAccessStatus);
            Assert.AreEqual("Scheduled", preview.PoolGymAccessStatus);
            Assert.AreEqual("Scheduled", preview.AllAccessStatus);
        }

        [TestMethod]
        public void FromCsvRecord_Teen16YearsOld_PoolExpiredPoolGymActiveAllScheduled()
        {
            // Arrange - Teen born 16 years ago (between 14 and 18)
            var birthday = DateTime.UtcNow.AddYears(-16);
            var record = new UserCsvModel
            {
                Name = "Teen 16",
                Birthday = birthday
            };

            // Act
            var preview = ImportPreviewModel.FromCsvRecord(record);

            // Assert
            Assert.AreEqual("Expired", preview.PoolAccessStatus);
            Assert.AreEqual("Active", preview.PoolGymAccessStatus);
            Assert.AreEqual("Scheduled", preview.AllAccessStatus);
        }

        [TestMethod]
        public void FromCsvRecord_Adult25YearsOld_PoolAndPoolGymExpiredAllActive()
        {
            // Arrange - Adult born 25 years ago (over 18)
            var birthday = DateTime.UtcNow.AddYears(-25);
            var record = new UserCsvModel
            {
                Name = "Adult 25",
                Birthday = birthday
            };

            // Act
            var preview = ImportPreviewModel.FromCsvRecord(record);

            // Assert
            Assert.AreEqual("Expired", preview.PoolAccessStatus);
            Assert.AreEqual("Expired", preview.PoolGymAccessStatus);
            Assert.AreEqual("Active", preview.AllAccessStatus);
        }

        [TestMethod]
        public void FromCsvRecord_Exactly12YearsOld_PoolActive()
        {
            // Arrange - Person exactly 12 years old
            var birthday = DateTime.UtcNow.AddYears(-12);
            var record = new UserCsvModel
            {
                Name = "Exactly 12",
                Birthday = birthday
            };

            // Act
            var preview = ImportPreviewModel.FromCsvRecord(record);

            // Assert
            Assert.AreEqual("Active", preview.PoolAccessStatus);
            Assert.AreEqual("Scheduled", preview.PoolGymAccessStatus);
            Assert.AreEqual("Scheduled", preview.AllAccessStatus);
        }

        [TestMethod]
        public void FromCsvRecord_Exactly14YearsOld_PoolExpiredPoolGymActive()
        {
            // Arrange - Person exactly 14 years old
            var birthday = DateTime.UtcNow.AddYears(-14);
            var record = new UserCsvModel
            {
                Name = "Exactly 14",
                Birthday = birthday
            };

            // Act
            var preview = ImportPreviewModel.FromCsvRecord(record);

            // Assert
            Assert.AreEqual("Expired", preview.PoolAccessStatus);
            Assert.AreEqual("Active", preview.PoolGymAccessStatus);
            Assert.AreEqual("Scheduled", preview.AllAccessStatus);
        }

        [TestMethod]
        public void FromCsvRecord_Exactly18YearsOld_AllActive()
        {
            // Arrange - Person exactly 18 years old
            var birthday = DateTime.UtcNow.AddYears(-18);
            var record = new UserCsvModel
            {
                Name = "Exactly 18",
                Birthday = birthday
            };

            // Act
            var preview = ImportPreviewModel.FromCsvRecord(record);

            // Assert
            Assert.AreEqual("Expired", preview.PoolAccessStatus);
            Assert.AreEqual("Expired", preview.PoolGymAccessStatus);
            Assert.AreEqual("Active", preview.AllAccessStatus);
        }
    }

    #endregion

    #region ImportService - ParseSingleStringAddress Tests

    [TestClass]
    public class ParseSingleStringAddressTests
    {
        [TestMethod]
        public void ParseSingleStringAddress_FullUSAddress_ParsesCorrectly()
        {
            // Arrange
            string address = "123 Main Street, Springfield, IL 62701";

            // Act
            var result = ImportService.ParseSingleStringAddress(address);

            // Assert
            Assert.AreEqual("123 Main Street", result.Street);
            Assert.AreEqual("Springfield", result.City);
            Assert.AreEqual("IL", result.Province);
            Assert.AreEqual("62701", result.PostalCode);
            Assert.AreEqual("Home", result.Type);
            Assert.AreEqual("US", result.Country);
        }

        [TestMethod]
        public void ParseSingleStringAddress_USAddressWithZipPlus4_ParsesCorrectly()
        {
            // Arrange
            string address = "456 Oak Ave, Chicago, IL 60601-1234";

            // Act
            var result = ImportService.ParseSingleStringAddress(address);

            // Assert
            Assert.AreEqual("456 Oak Ave", result.Street);
            Assert.AreEqual("Chicago", result.City);
            Assert.AreEqual("IL", result.Province);
            Assert.AreEqual("60601-1234", result.PostalCode);
        }

        [TestMethod]
        public void ParseSingleStringAddress_CanadianAddress_ParsesCorrectly()
        {
            // Arrange
            string address = "789 Maple Road, Toronto, ON M5V 2T6";

            // Act
            var result = ImportService.ParseSingleStringAddress(address);

            // Assert
            Assert.AreEqual("789 Maple Road", result.Street);
            Assert.AreEqual("Toronto", result.City);
            Assert.AreEqual("ON", result.Province);
            Assert.AreEqual("M5V 2T6", result.PostalCode);
        }

        [TestMethod]
        public void ParseSingleStringAddress_NullAddress_ReturnsEmptyAddressInfo()
        {
            // Act
            var result = ImportService.ParseSingleStringAddress(null);

            // Assert
            Assert.IsNull(result.Street);
            Assert.IsNull(result.City);
            Assert.IsNull(result.Province);
            Assert.IsNull(result.PostalCode);
            Assert.AreEqual("Home", result.Type);
            Assert.AreEqual("US", result.Country);
        }

        [TestMethod]
        public void ParseSingleStringAddress_EmptyAddress_ReturnsEmptyAddressInfo()
        {
            // Act
            var result = ImportService.ParseSingleStringAddress("");

            // Assert
            Assert.IsNull(result.Street);
            Assert.IsNull(result.City);
            Assert.IsNull(result.Province);
            Assert.IsNull(result.PostalCode);
        }

        [TestMethod]
        public void ParseSingleStringAddress_WhitespaceAddress_ReturnsEmptyAddressInfo()
        {
            // Act
            var result = ImportService.ParseSingleStringAddress("   ");

            // Assert
            Assert.IsNull(result.Street);
            Assert.IsNull(result.City);
        }

        [TestMethod]
        public void ParseSingleStringAddress_OnlyStreet_ParsesAsStreet()
        {
            // Arrange
            string address = "123 Main Street";

            // Act
            var result = ImportService.ParseSingleStringAddress(address);

            // Assert
            Assert.AreEqual("123 Main Street", result.Street);
            Assert.IsNull(result.City);
            Assert.IsNull(result.Province);
            Assert.IsNull(result.PostalCode);
        }

        [TestMethod]
        public void ParseSingleStringAddress_StreetAndCity_ParsesCorrectly()
        {
            // Arrange
            string address = "123 Main Street, Springfield";

            // Act
            var result = ImportService.ParseSingleStringAddress(address);

            // Assert
            Assert.AreEqual("123 Main Street", result.Street);
            Assert.AreEqual("Springfield", result.City);
        }

        [TestMethod]
        public void ParseSingleStringAddress_MultiLineStreet_ParsesCorrectly()
        {
            // Arrange
            string address = "123 Main Street, Apt 4B, Springfield, IL 62701";

            // Act
            var result = ImportService.ParseSingleStringAddress(address);

            // Assert
            Assert.AreEqual("123 Main Street, Apt 4B", result.Street);
            Assert.AreEqual("Springfield", result.City);
            Assert.AreEqual("IL", result.Province);
            Assert.AreEqual("62701", result.PostalCode);
        }
    }

    #endregion

    #region ImportService - Is429Exception Tests

    [TestClass]
    public class Is429ExceptionTests
    {
        [TestMethod]
        public void Is429Exception_HttpRequestExceptionWith429InMessage_ReturnsTrue()
        {
            // Arrange
            var ex = new HttpRequestException("Response status code does not indicate success: 429");

            // Act
            var result = ImportService.Is429Exception(ex);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Is429Exception_HttpRequestExceptionWithTooManyRequests_ReturnsTrue()
        {
            // Arrange
            var ex = new HttpRequestException("Too Many Requests");

            // Act
            var result = ImportService.Is429Exception(ex);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Is429Exception_GenericExceptionWith429_ReturnsTrue()
        {
            // Arrange
            var ex = new Exception("Error: 429 rate limit exceeded");

            // Act
            var result = ImportService.Is429Exception(ex);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Is429Exception_GenericExceptionWithTooManyRequests_ReturnsTrue()
        {
            // Arrange
            var ex = new Exception("Too Many Requests - please slow down");

            // Act
            var result = ImportService.Is429Exception(ex);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Is429Exception_GenericExceptionWithTooManyRequestsCamelCase_ReturnsTrue()
        {
            // Arrange
            var ex = new Exception("TooManyRequests");

            // Act
            var result = ImportService.Is429Exception(ex);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Is429Exception_NestedExceptionWith429_ReturnsTrue()
        {
            // Arrange
            var innerEx = new HttpRequestException("429 Too Many Requests");
            var ex = new Exception("Outer exception", innerEx);

            // Act
            var result = ImportService.Is429Exception(ex);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Is429Exception_GenericException_ReturnsFalse()
        {
            // Arrange
            var ex = new Exception("Something went wrong");

            // Act
            var result = ImportService.Is429Exception(ex);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Is429Exception_HttpRequestExceptionWithOtherStatus_ReturnsFalse()
        {
            // Arrange
            var ex = new HttpRequestException("Response status code does not indicate success: 500");

            // Act
            var result = ImportService.Is429Exception(ex);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Is429Exception_NullInnerException_ReturnsFalse()
        {
            // Arrange
            var ex = new Exception("Generic error");

            // Act
            var result = ImportService.Is429Exception(ex);

            // Assert
            Assert.IsFalse(result);
        }
    }

    #endregion

    #region ImportService - Constructor Tests

    [TestClass]
    public class ImportServiceConstructorTests
    {
        [TestMethod]
        public void ImportService_WithValidConfig_CreatesInstance()
        {
            // Arrange
            var config = new ImportConfiguration();

            // Act
            var service = new ImportService(config);

            // Assert
            Assert.IsNotNull(service);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ImportService_WithNullConfig_ThrowsArgumentNullException()
        {
            // Act
            var service = new ImportService(null);
        }

        [TestMethod]
        public void ImportService_WithLogger_CreatesInstance()
        {
            // Arrange
            var config = new ImportConfiguration();
            string loggedMessage = null;
            Action<string> logger = msg => loggedMessage = msg;

            // Act
            var service = new ImportService(config, logger);

            // Assert
            Assert.IsNotNull(service);
        }
    }

    #endregion

    #region ImportProgress Tests

    [TestClass]
    public class ImportProgressTests
    {
        [TestMethod]
        public void ImportProgress_SetProperties_ReturnsCorrectValues()
        {
            // Arrange & Act
            var progress = new ImportProgress
            {
                CurrentStep = 50,
                TotalSteps = 100,
                Message = "Processing...",
                IsError = false
            };

            // Assert
            Assert.AreEqual(50, progress.CurrentStep);
            Assert.AreEqual(100, progress.TotalSteps);
            Assert.AreEqual("Processing...", progress.Message);
            Assert.IsFalse(progress.IsError);
        }

        [TestMethod]
        public void ImportProgress_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var progress = new ImportProgress();

            // Assert
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
        public void ImportResult_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var result = new ImportResult();

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, result.PeopleCreated);
            Assert.AreEqual(0, result.AccessLevelsAssigned);
            Assert.IsNotNull(result.Errors);
            Assert.AreEqual(0, result.Errors.Count);
            Assert.IsNotNull(result.Warnings);
            Assert.AreEqual(0, result.Warnings.Count);
        }

        [TestMethod]
        public void ImportResult_AddErrors_StoresCorrectly()
        {
            // Arrange
            var result = new ImportResult();

            // Act
            result.Errors.Add("Error 1");
            result.Errors.Add("Error 2");

            // Assert
            Assert.AreEqual(2, result.Errors.Count);
            Assert.AreEqual("Error 1", result.Errors[0]);
            Assert.AreEqual("Error 2", result.Errors[1]);
        }

        [TestMethod]
        public void ImportResult_AddWarnings_StoresCorrectly()
        {
            // Arrange
            var result = new ImportResult();

            // Act
            result.Warnings.Add("Warning 1");
            result.Warnings.Add("Warning 2");

            // Assert
            Assert.AreEqual(2, result.Warnings.Count);
            Assert.AreEqual("Warning 1", result.Warnings[0]);
            Assert.AreEqual("Warning 2", result.Warnings[1]);
        }

        [TestMethod]
        public void ImportResult_SetSuccessfulResult_ReturnsCorrectValues()
        {
            // Arrange & Act
            var result = new ImportResult
            {
                Success = true,
                PeopleCreated = 10,
                AccessLevelsAssigned = 30
            };

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(10, result.PeopleCreated);
            Assert.AreEqual(30, result.AccessLevelsAssigned);
        }
    }

    #endregion
}
