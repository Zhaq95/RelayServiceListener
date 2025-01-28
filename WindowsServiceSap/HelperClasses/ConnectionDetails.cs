using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WindowsServiceSap.DTOs;
using Microsoft.SqlServer.Server;

namespace WindowsServiceSap.HelperClasses
{
    public class ConnectionDetails
    {
        private readonly Logger logger;

        public ConnectionDetails()
        {
            logger = new Logger();
        }

        public async Task SaveOrUpdateConnectionDetailsAsync(ConnectionDetailsRequest connectionDetails)
        {
            try
            {
                string customJsonFilePath = Path.Combine(AppContext.BaseDirectory, "connectionDetails.json");

                // Encrypt sensitive fields
                byte[] encryptedUserName = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(connectionDetails.UserName),
                    null,
                    DataProtectionScope.LocalMachine
                );

                byte[] encryptedPassword = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(connectionDetails.Password),
                    null,
                    DataProtectionScope.LocalMachine
                );

                var connectionData = new Dictionary<string, object>
            {
                { "ServerAddress", connectionDetails.ServerAddress },
                { "PortNumber", connectionDetails.PortNumber },
                { "UserName", Convert.ToBase64String(encryptedUserName) },
                { "Password", Convert.ToBase64String(encryptedPassword) }
            };

                string json = "{}";
                if (File.Exists(customJsonFilePath))
                {
                    json = await Task.Run(() => File.ReadAllText(customJsonFilePath));
                }

                var existingData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                                   ?? new Dictionary<string, object>();

                if (existingData.ContainsKey("ConnectionInfo"))
                {
                    existingData["ConnectionInfo"] = connectionData;
                }
                else
                {
                    existingData.Add("ConnectionInfo", connectionData);
                }

                var updatedJson = System.Text.Json.JsonSerializer.Serialize(existingData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await Task.Run(() => File.WriteAllText(customJsonFilePath, updatedJson));
                await logger.WriteLogAsync("ConnectionDetails.json file is created.");
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while saving or updating connection details in the custom JSON file.", ex);
            }
        }

        public async Task<ConnectionDetailsRequest> LoadConnectionDetailsAsync()
        {
            try
            {
                string customJsonFilePath = Path.Combine(AppContext.BaseDirectory, "connectionDetails.json");

                if (!File.Exists(customJsonFilePath))
                {
                    await logger.WriteLogAsync("ConnectionDetails.json file is not found.");
                    throw new FileNotFoundException("ConnectionDetails.json file not found.");
                }

                string json = await Task.Run(() => File.ReadAllText(customJsonFilePath));
                var existingData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                if (existingData == null || !existingData.ContainsKey("ConnectionInfo"))
                {
                    throw new Exception("ConnectionInfo section not found in the JSON file.");
                }

                var connectionData = (JsonElement)existingData["ConnectionInfo"];
                string serverAddress = connectionData.GetProperty("ServerAddress").GetString();
                string portNumber = connectionData.GetProperty("PortNumber").GetString();

                // Throw custom exception for invalid connection details
                if (string.IsNullOrWhiteSpace(serverAddress) || string.IsNullOrWhiteSpace(portNumber))
                {
                    throw new InvalidOperationException("ConnectionDetails.json File is empty or contains invalid data");
                }
                var connectionDetails = new ConnectionDetailsRequest
                {
                    ServerAddress = serverAddress,
                    PortNumber = portNumber,
                    UserName = DecryptField(connectionData.GetProperty("UserName").GetString()),
                    Password = DecryptField(connectionData.GetProperty("Password").GetString())
                };

                return connectionDetails;
            }
            catch (InvalidOperationException ex) { throw new InvalidOperationException(ex.Message); }
            catch (FileNotFoundException exi) { throw new FileNotFoundException("ConnectionDetails File not Found", exi); }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while reading the connection details.", ex);
            }
        }

        public async Task SaveOrUpdateOdbcConnectionDetailsAsync(OdbcConnectionDetailsRequest connectionDetails)
        {
            try
            {
                string customJsonFilePath = Path.Combine(AppContext.BaseDirectory, "OdbcconnectionDetails.json");

                // Encrypt sensitive fields
                byte[] encryptedUserName = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(connectionDetails.QueryString),
                    null,
                    DataProtectionScope.LocalMachine
                );

                

                var connectionData = new Dictionary<string, object>
            {
                { "ConnectionString", Convert.ToBase64String(encryptedUserName) },

            };

                string json = "{}";
                if (File.Exists(customJsonFilePath))
                {
                    json = await Task.Run(() => File.ReadAllText(customJsonFilePath));
                }

                var existingData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                                   ?? new Dictionary<string, object>();

                if (existingData.ContainsKey("ConnectionInfo"))
                {
                    existingData["ConnectionInfo"] = connectionData;
                }
                else
                {
                    existingData.Add("ConnectionInfo", connectionData);
                }

                var updatedJson = System.Text.Json.JsonSerializer.Serialize(existingData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await Task.Run(() => File.WriteAllText(customJsonFilePath, updatedJson));
                await logger.WriteLogAsync("OdbcConnectionDetails.json file is created.");
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while saving or updating Odbc connection details in the custom JSON file.", ex);
            }
        }

        public async Task<OdbcConnectionDetailsRequest> LoadOdbcConnectionDetailsAsync()
        {
            try
            {
                string customJsonFilePath = Path.Combine(AppContext.BaseDirectory, "OdbcconnectionDetails.json");

                if (!File.Exists(customJsonFilePath))
                {
                    await logger.WriteLogAsync("OdbcConnectionDetails.json file is not found.");
                    throw new FileNotFoundException("OdbcconnectionDetails.json file not found.");
                }

                string json = await Task.Run(() => File.ReadAllText(customJsonFilePath));
                var existingData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                if (existingData == null || !existingData.ContainsKey("ConnectionInfo"))
                {
                    throw new Exception("ConnectionInfo section not found in the JSON file.");
                }

                var connectionData = (JsonElement)existingData["ConnectionInfo"];
                string ConnectionString = connectionData.GetProperty("ConnectionString").GetString();

                // Throw custom exception for invalid connection details
                if (string.IsNullOrWhiteSpace(ConnectionString))
                {
                    throw new InvalidOperationException("OdbcConnectionDetails.json File is empty or contains invalid data");
                }
                var connectionDetails = new OdbcConnectionDetailsRequest
                {
           
                    QueryString = DecryptField(connectionData.GetProperty("ConnectionString").GetString()),
                };

                return connectionDetails;
            }
            catch (InvalidOperationException ex) { throw new InvalidOperationException(ex.Message); }
            catch (FileNotFoundException exi) { throw new FileNotFoundException("ConnectionDetails File not Found", exi); }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while reading the connection details.", ex);
            }
        }

        public async Task SaveRelayConnectionDetailsAsync(RelayConnectionDetails relayDetails)
        {
            try
            {
                string filePath = Path.Combine(AppContext.BaseDirectory, "RelayConnectionDetails.json");

                var json = JsonSerializer.Serialize(relayDetails, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await Task.Run(() => File.WriteAllText(filePath, json));
                Console.WriteLine("RelayConnectionDetails saved successfully.");
                await logger.WriteLogAsync("RelayConnectionDetails saved successfully.");
                FileInfo fileInfo = new FileInfo(filePath);
                FileSecurity fileSecurity = fileInfo.GetAccessControl();

                // Define admin access rules
                fileSecurity.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));

                // Apply the updated permissions to the file
                fileInfo.SetAccessControl(fileSecurity);

                Console.WriteLine("Admin permissions assigned to RelayConnectionDetails.json.");
                await logger.WriteLogAsync("Admin permissions assigned to RelayConnectionDetails.json.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving RelayConnectionDetails: {ex.Message}");
                await logger.WriteLogAsync($"Error saving RelayConnectionDetails: {ex.Message}");
                throw;
            }
        }

        public async Task<RelayConnectionDetails> LoadRelayConnectionDetailsAsync()
        {
            try
            {
                string filePath = Path.Combine(AppContext.BaseDirectory, "RelayConnectionDetails.json");

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("RelayConnectionDetails.json file not found.");
                }

                string json = await Task.Run(() => File.ReadAllText(filePath));
                var relayDetails = JsonSerializer.Deserialize<RelayConnectionDetails>(json);

                if (relayDetails == null)
                {
                    throw new Exception("RelayConnectionDetails.json is empty or contains invalid data.");
                }

                // Validate Key1 and Key2
                if (string.IsNullOrWhiteSpace(relayDetails.Key1) || string.IsNullOrWhiteSpace(relayDetails.Key2))
                {
                    throw new InvalidOperationException("Key1 or Key2 in RelayConnectionDetails.json is missing or empty.");
                }

                return relayDetails;
            }
            catch(InvalidOperationException ex) { throw new InvalidOperationException($"Error reading RelayConnectionDetails: {ex.Message}"); }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading RelayConnectionDetails: {ex.Message}");
                throw;
            }
        }


        private string DecryptField(string encryptedField)
        {
            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedField);
                byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while decrypting the field.", ex);
            }
        }

        public async Task<bool> DeleteConnectionDetailsAsync(DeleteConnectionDetailsDTO connectionDetails)
        {
            try
            {
                string customJsonFilePath = Path.Combine(AppContext.BaseDirectory, "connectionDetails.json");

                if (File.Exists(customJsonFilePath))
                {
                    // Ensure the file has admin permissions before attempting to delete it
                    FileInfo fileInfo = new FileInfo(customJsonFilePath);
                    FileSecurity fileSecurity = fileInfo.GetAccessControl();

                    // Grant full control to the Administrators group
                    fileSecurity.AddAccessRule(new FileSystemAccessRule(
                        new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                        FileSystemRights.FullControl,
                        AccessControlType.Allow));

                    fileInfo.SetAccessControl(fileSecurity);

                    // Delete the file
                    File.Delete(customJsonFilePath);
                    Console.WriteLine("ConnectionDetails.json file deleted successfully.");
                    await logger.WriteLogAsync("ConnectionDetails.json file deleted successfully.");
                    return true;

                }
                else
                {
                    Console.WriteLine("ConnectionDetails.json file does not exist.");
                    await logger.WriteLogAsync("ConnectionDetails.json file does not exist.");
                    return false;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Permission issue while deleting the file: {ex.Message}");
                await logger.WriteLogAsync($"Permission issue while deleting the file: {ex.Message}");
                throw new Exception("An error occurred while deleting connection details", ex);
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while deleting connection details", ex);
            }
        }

        public async Task<bool> DeleteOdbcConnectionDetailsAsync(DeleteConnectionDetailsDTO connectionDetails)
        {
            try
            {
                string customJsonFilePath = Path.Combine(AppContext.BaseDirectory, "OdbcconnectionDetails.json");

                if (File.Exists(customJsonFilePath))
                {
                    // Ensure the file has admin permissions before attempting to delete it
                    FileInfo fileInfo = new FileInfo(customJsonFilePath);
                    FileSecurity fileSecurity = fileInfo.GetAccessControl();

                    // Grant full control to the Administrators group
                    fileSecurity.AddAccessRule(new FileSystemAccessRule(
                        new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                        FileSystemRights.FullControl,
                        AccessControlType.Allow));

                    fileInfo.SetAccessControl(fileSecurity);

                    // Delete the file
                    File.Delete(customJsonFilePath);
                    Console.WriteLine("OdbcconnectionDetails.json file deleted successfully.");
                    await logger.WriteLogAsync("OdbcconnectionDetails.json file deleted successfully.");
                    return true;

                }
                else
                {
                    Console.WriteLine("OdbcconnectionDetails.json file does not exist.");
                    await logger.WriteLogAsync("OdbcconnectionDetails.json file does not exist.");
                    return false;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Permission issue while deleting the file: {ex.Message}");
                await logger.WriteLogAsync($"Permission issue while deleting the file: {ex.Message}");
                throw new Exception("An error occurred while deleting connection details", ex);
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while deleting Odbc connection details", ex);
            }
        }


    }

}
