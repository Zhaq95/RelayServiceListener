using Sap.Data.Hana;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Azure.Relay;
using System.Data.Odbc;
using static WindowsServiceSap.RelayService;
using System.Data;
using Microsoft.Identity.Client;
using System.Security.Cryptography;

namespace WindowsServiceSap
{
    public sealed class RelayService : IDisposable
    {
        private HybridConnectionListener _listener;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private System.Timers.Timer _listenerStatusTimer;



        //public async Task ConnectRelay(CancellationToken stoppingToken)
        //{
        //    try
        //    {

        //        // Ensure TLS security protocol is set globally
        //        System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

        //        // Step 1: Ensure RelayConnectionDetails.json file is created at service start if not present
        //        var relayDetailsFilePath = Path.Combine(AppContext.BaseDirectory, "RelayConnectionDetails.json");

        //        if (!File.Exists(relayDetailsFilePath))
        //        {
        //            Console.WriteLine("RelayConnectionDetails.json not found. Creating default configuration file...");

        //            // Initialize RelayConnectionDetails with blank values
        //            var newRelayDetails = new RelayConnectionDetails
        //            {
        //                Key1 = "",  // Blank for the user to fill in
        //                Key2 = "" // Blank for the user to fill in
        //            };

        //            // Save the RelayConnectionDetails with blank values to file
        //            await SaveRelayConnectionDetailsAsync(newRelayDetails);

        //            Console.WriteLine("Default RelayConnectionDetails.json file created. Please update this file with valid values.");
        //        }
        //        else
        //        {
        //            Console.WriteLine("RelayConnectionDetails.json file found.");
        //        }

        //        // Step 2: Load RelayConnectionDetails (after creation or already existing)
        //        var relayDetails = await LoadRelayConnectionDetailsAsync();

        //        // Check if values are missing (optional)
        //        if (string.IsNullOrEmpty(relayDetails.Key1) || string.IsNullOrEmpty(relayDetails.Key2))
        //        {
        //            Console.WriteLine("Warning: RelayConnectionString or HybridConnectionName is still missing in RelayConnectionDetails.json.");
        //        }

        //        // Step 3: Proceed with the rest of the connection logic
        //        var connectionString = relayDetails.Key1;
        //        var hybridConnectionName = relayDetails.Key2;

        //        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(hybridConnectionName))
        //        {
        //            Console.WriteLine("RelayConnectionString or HybridConnectionName is missing in configuration.");
        //            return;
        //        }

        //        var relayConnectionStringBuilder = new RelayConnectionStringBuilder(connectionString)
        //        {
        //            EntityPath = hybridConnectionName
        //        };

        //        _listener = new HybridConnectionListener(relayConnectionStringBuilder.ToString())
        //        {
        //            RequestHandler = async context =>
        //            {
        //                try
        //                {
        //                    await ProcessRequestAsync(context);
        //                }
        //                catch (Exception ex)
        //                {
        //                    Console.WriteLine($"Error processing request: {ex.Message}");
        //                    using (var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8, 1024, leaveOpen: true))
        //                    {
        //                        await writer.WriteLineAsync($"Error: {ex.Message}");
        //                    }
        //                }
        //                finally
        //                {
        //                    await context.Response.CloseAsync(); // Important: Close the response
        //                }
        //            }
        //        };

        //        // Open the listener and start the service
        //        await _listener.OpenAsync(stoppingToken);
        //        Console.WriteLine("SQL Relay Listener Service started successfully.");

        //        // Start the status timer to monitor the listener
        //        _listenerStatusTimer = new System.Timers.Timer(30000);
        //        _listenerStatusTimer.Elapsed += async (sender, e) => await CheckListenerStatusAsync();
        //        _listenerStatusTimer.AutoReset = true;
        //        _listenerStatusTimer.Enabled = true;

        //        // Keep the service running until cancellation is requested
        //        await Task.Delay(Timeout.Infinite, _cts.Token);
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        Console.WriteLine("ConnectRelay operation canceled.");
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error in SQL Relay Listener Service: {ex.Message}");
        //        throw;
        //    }
        //    finally
        //    {
        //        if (_listener != null)
        //        {
        //            Console.WriteLine("Closing HybridConnectionListener...");
        //            await _listener.CloseAsync(CancellationToken.None);
        //            _listener = null;
        //            Console.WriteLine("HybridConnectionListener closed.");
        //        }
        //    }
        //}

        public async Task ConnectRelay(CancellationToken stoppingToken)
        {
            try
            {
                // Ensure TLS security protocol is set globally
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

                // Step 1: Ensure RelayConnectionDetails.json file is created at service start if not present
                var relayDetailsFilePath = Path.Combine(AppContext.BaseDirectory, "RelayConnectionDetails.json");

                if (!File.Exists(relayDetailsFilePath))
                {
                    Console.WriteLine("RelayConnectionDetails.json not found. Creating default configuration file...");

                    // Initialize RelayConnectionDetails with blank values
                    var newRelayDetails = new RelayConnectionDetails
                    {
                        Key1 = "",  // Blank for the user to fill in
                        Key2 = "" // Blank for the user to fill in
                    };

                    // Save the RelayConnectionDetails with blank values to file
                    await SaveRelayConnectionDetailsAsync(newRelayDetails);

                    Console.WriteLine("Default RelayConnectionDetails.json file created. Please update this file with valid values.");
                    return;
                }

                // Step 2: Load RelayConnectionDetails (after creation or already existing)
                var relayDetails = await LoadRelayConnectionDetailsAsync();

                // Check if values are missing (optional)
                if (string.IsNullOrEmpty(relayDetails.Key1) || string.IsNullOrEmpty(relayDetails.Key2))
                {
                    Console.WriteLine("Warning: RelayConnectionString or HybridConnectionName is still missing in RelayConnectionDetails.json.");
                    return;
                }

                // Step 3: Decrypt the encrypted keys
                string decryptedConnectionString = EncryptionHelper.Decrypt(relayDetails.Key1);
                string decryptedHybridConnectionName = EncryptionHelper.Decrypt(relayDetails.Key2);

                if (string.IsNullOrEmpty(decryptedConnectionString) || string.IsNullOrEmpty(decryptedHybridConnectionName))
                {
                    Console.WriteLine("Decrypted RelayConnectionString or HybridConnectionName is empty.");
                    return;
                }

                var relayConnectionStringBuilder = new RelayConnectionStringBuilder(decryptedConnectionString)
                {
                    EntityPath = decryptedHybridConnectionName
                };

                _listener = new HybridConnectionListener(relayConnectionStringBuilder.ToString())
                {
                    RequestHandler = async context =>
                    {
                        try
                        {
                            await ProcessRequestAsync(context);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing request: {ex.Message}");
                            using (var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8, 1024, leaveOpen: true))
                            {
                                await writer.WriteLineAsync($"Error: {ex.Message}");
                            }
                        }
                        finally
                        {
                            await context.Response.CloseAsync(); // Important: Close the response
                        }
                    }
                };

                // Open the listener and start the service
                await _listener.OpenAsync(stoppingToken);
                Console.WriteLine("SQL Relay Listener Service started successfully.");

                // Start the status timer to monitor the listener
                _listenerStatusTimer = new System.Timers.Timer(30000);
                _listenerStatusTimer.Elapsed += async (sender, e) => await CheckListenerStatusAsync();
                _listenerStatusTimer.AutoReset = true;
                _listenerStatusTimer.Enabled = true;

                // Keep the service running until cancellation is requested
                await Task.Delay(Timeout.Infinite, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("ConnectRelay operation canceled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SQL Relay Listener Service: {ex.Message}");
                throw;
            }
            finally
            {
                if (_listener != null)
                {
                    Console.WriteLine("Closing HybridConnectionListener...");
                    await _listener.CloseAsync(CancellationToken.None);
                    _listener = null;
                    Console.WriteLine("HybridConnectionListener closed.");
                }
            }
        }



        private async Task CheckListenerStatusAsync()
        {
            if (_listener != null && !_listener.IsOnline)
            {
                Console.WriteLine("HybridConnectionListener is offline. Attempting to reopen...");
                try
                {
                    await _listener.OpenAsync(_cts.Token);
                    Console.WriteLine("HybridConnectionListener reopened successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reopening listener: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            // Dispose of resources
            if (_listener != null)
            {
                _listener.CloseAsync().Wait(); // Close the listener gracefully
                _listener = null;
            }
  
        }

        public async Task ProcessRequestAsync(RelayedHttpListenerContext context)
        {
            Console.WriteLine("Incoming HTTP request...");

            try
            {
                string requestName = context.Request.Headers["RequestName"]; // Direct access, can be null

                string requestBody;
                if (context.Request.InputStream == null)
                {
                    Console.WriteLine("Request InputStream is null.");
                    await SendErrorResponseAsync(context, HttpStatusCode.BadRequest, "Request body is empty.");
                    return;
                }

                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                {
                    requestBody = await reader.ReadToEndAsync();
                    Console.WriteLine($"Received request body: {requestBody}");
                }

                using (var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8, 1024, leaveOpen: true))
                {
                    if (string.IsNullOrEmpty(requestName))
                    {
                        await SendErrorResponseAsync(context, writer, HttpStatusCode.BadRequest, "RequestName header is missing.");
                        return;
                    }

                    switch (requestName)
                    {
                        case "SendConnectionDetails":
                            await HandleRequestAsync<ConnectionDetailsRequest>(context, writer, requestBody, async (details) =>
                            {
                                await SaveOrUpdateConnectionDetailsAsync(details);
                                return JsonSerializer.Serialize(new { message = "Connection details saved successfully." }); // Serialize to JSON
                            });
                            break;
                        case "ExecuteQuery":
                            await HandleRequestAsync<QueryRequest>(context, writer, requestBody, async (req) =>
                            {
                                var result = await ExecuteQuery(req.Query);
                                return JsonSerializer.Serialize(result); // Serialize the list to JSON
                            });
                            break;
                        case "ExecuteOdbcQuery":
                            await HandleRequestAsync<QueryRequest>(context, writer, requestBody, async (req) =>
                            {
                                var result = await ExecuteOdbcQuery(req.Query);
                                return JsonSerializer.Serialize(result); // Serialize the list to JSON
                            });
                            break;
                        case "ExecuteISQuery":
                            await HandleRequestAsync<QueryRequestIS>(context, writer, requestBody, async (req) =>
                            {
                                var result = await ExecuteISQuery(req);
                                return JsonSerializer.Serialize(result); // Serialize the list to JSON
                            });
                            break;
                        default:
                            await SendErrorResponseAsync(context, writer, HttpStatusCode.BadRequest, "Invalid RequestName header.");
                            break;
                    }
                }
            }
            catch (Exception ex) // Catch broader exception to handle null reference from missing header
            {
                Console.WriteLine($"Error processing request: {ex}");

                HttpStatusCode statusCode = ex is JsonException ? HttpStatusCode.BadRequest : HttpStatusCode.InternalServerError;
                await SendErrorResponseAsync(context, statusCode, $"An error occurred: {ex.Message}");
            }
            finally
            {
                context.Response.Close();
            }
        }
        private async Task HandleRequestAsync<TRequest>(RelayedHttpListenerContext context, StreamWriter writer, string requestBody, Func<TRequest, Task<string>> handler)
        {
            try
            {
                var request = JsonSerializer.Deserialize<TRequest>(requestBody);
                if (request == null)
                {
                    await SendErrorResponseAsync(context, writer, HttpStatusCode.BadRequest, "Failed to deserialize request body."); // Use HttpStatusCode
                    return;
                }

                var result = await handler(request);
                await SendResponseAsync(context, writer, HttpStatusCode.OK, result); // Add message argument
            }
            catch (JsonException ex)
            {
                await SendErrorResponseAsync(context, writer, HttpStatusCode.BadRequest, $"Invalid JSON in request body: {ex.Message}");
            }
            catch (Exception ex)
            {
                await SendErrorResponseAsync(context, writer, HttpStatusCode.InternalServerError, $"Error processing request: {ex.Message}");
            }
        }
        private async Task SendResponseAsync(RelayedHttpListenerContext context, StreamWriter writer, HttpStatusCode statusCode, string message)
        {
            context.Response.StatusCode = statusCode;
            await writer.WriteLineAsync(message);
        }

        private async Task SendErrorResponseAsync(RelayedHttpListenerContext context, HttpStatusCode statusCode, string errorMessage)
        {
            context.Response.StatusCode = statusCode;
            using (var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8, 1024, leaveOpen: true))
            {
                await writer.WriteLineAsync($"Error: {errorMessage}");
            }
        }

        private async Task SendErrorResponseAsync(RelayedHttpListenerContext context, StreamWriter writer, HttpStatusCode statusCode, string errorMessage)
        {
            context.Response.StatusCode = statusCode;
            await writer.WriteLineAsync($"Error: {errorMessage}");
        }







        private async Task SaveOrUpdateConnectionDetailsAsync(ConnectionDetailsRequest connectionDetails)
        {
            try
            {
                // Define the path to the new JSON file (e.g., connectionDetails.json)
                string customJsonFilePath = Path.Combine(
                    AppContext.BaseDirectory, "connectionDetails.json" // This will create the file in the app's directory
                );

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

                // Convert encrypted fields to Base64 for storage
                var connectionData = new Dictionary<string, object>
        {
            { "ServerAddress", connectionDetails.ServerAddress },
            { "PortNumber", connectionDetails.PortNumber },
            { "UserName", Convert.ToBase64String(encryptedUserName) },
            { "Password", Convert.ToBase64String(encryptedPassword) }
        };

                // If the file exists, read its contents; otherwise, create an empty object
                string json = "{}";
                if (File.Exists(customJsonFilePath))
                {
                    json = await Task.Run(() => File.ReadAllText(customJsonFilePath));
                }

                // Deserialize the existing JSON into a dictionary
                var existingData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                                   ?? new Dictionary<string, object>();

                // Update or add the connection details
                if (existingData.ContainsKey("ConnectionInfo"))
                {
                    existingData["ConnectionInfo"] = connectionData;
                }
                else
                {
                    existingData.Add("ConnectionInfo", connectionData);
                }

                // Serialize the updated data back to JSON
                var updatedJson = System.Text.Json.JsonSerializer.Serialize(existingData, new JsonSerializerOptions
                {
                    WriteIndented = true // Makes the JSON human-readable
                });

                // Write the updated JSON to the custom file
                await Task.Run(() => File.WriteAllText(customJsonFilePath, updatedJson));
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while saving or updating connection details in the custom JSON file.", ex);
            }
        }

        private async Task<ConnectionDetailsRequest> LoadConnectionDetailsAsync()
        {
            try
            {
                // Define the path to the JSON file
                string customJsonFilePath = Path.Combine(
                    AppContext.BaseDirectory, "connectionDetails.json"
                );

                if (!File.Exists(customJsonFilePath))
                {
                    throw new FileNotFoundException("ConnectionDetails.json file not found.");
                }

                // Read the file content
                string json = await Task.Run(() => File.ReadAllText(customJsonFilePath));
                var existingData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                if (existingData == null || !existingData.ContainsKey("ConnectionInfo"))
                {
                    throw new Exception("ConnectionInfo section not found in the JSON file.");
                }

                // Retrieve connection details
                var connectionData = (JsonElement)existingData["ConnectionInfo"];
                var connectionDetails = new ConnectionDetailsRequest
                {
                    ServerAddress = connectionData.GetProperty("ServerAddress").GetString(),
                    PortNumber = connectionData.GetProperty("PortNumber").GetString(),
                    UserName = DecryptField(connectionData.GetProperty("UserName").GetString()),
                    Password = DecryptField(connectionData.GetProperty("Password").GetString())
                };

                return connectionDetails;
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while reading or decrypting the connection details.", ex);
            }
        }

        private async Task SaveRelayConnectionDetailsAsync(RelayConnectionDetails relayDetails)
        {
            try
            {
                string filePath = Path.Combine(AppContext.BaseDirectory, "RelayConnectionDetails.json");

                // Serialize to JSON with indentation for readability
                var json = JsonSerializer.Serialize(relayDetails, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await Task.Run(() => File.WriteAllText(filePath, json));
                Console.WriteLine("RelayConnectionDetails saved successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving RelayConnectionDetails: {ex.Message}");
                throw;
            }
        }


        private async Task<RelayConnectionDetails> LoadRelayConnectionDetailsAsync()
        {
            try
            {
                string filePath = Path.Combine(AppContext.BaseDirectory, "RelayConnectionDetails.json");

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("RelayConnectionDetails.json file not found.");
                }

                string json = await Task.Run(() => File.ReadAllText(filePath));
                return JsonSerializer.Deserialize<RelayConnectionDetails>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading RelayConnectionDetails: {ex.Message}");
                throw;
            }
        }

        private string DecryptField(string encryptedBase64)
        {
            byte[] encryptedData = Convert.FromBase64String(encryptedBase64);

            byte[] decryptedData = ProtectedData.Unprotect(
                encryptedData,
                null,
                DataProtectionScope.LocalMachine
            );

            return Encoding.UTF8.GetString(decryptedData);
        }




        private async Task<List<Dictionary<string, object>>> ExecuteQuery(string query)
        {
            var results = new List<Dictionary<string, object>>();

            // Fetch connection details from JSON based on ConnectionName
            var connectionDetails = await LoadConnectionDetailsAsync();
            var sapConnectionString = $"Server={connectionDetails.ServerAddress}:{connectionDetails.PortNumber};UserID={connectionDetails.UserName};Password={connectionDetails.Password};";

            using (var connection = new HanaConnection(sapConnectionString))
            {
                await connection.OpenAsync();

                using (var command = new HanaCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    command.CommandTimeout = 120;
                    while (await reader.ReadAsync())
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var columnName = reader.GetName(i);
                            var value = reader.GetValue(i);

                            // Convert specific data types to handle compatibility
                            if (value is decimal || value is double || value is float)
                            {
                                row[columnName] = Convert.ToDouble(value);
                            }
                            else if (value is DBNull)
                            {
                                row[columnName] = null;
                            }
                            else
                            {
                                row[columnName] = value?.ToString();
                            }
                        }
                        results.Add(row);
                    }
                }
                connection.Close();
            }

            return results;
        }



        private async Task<List<Dictionary<string, object>>> ExecuteOdbcQuery(string query)
        {
            var driver = "HDBODBC32";
            var connectionDetails = await LoadConnectionDetailsAsync();
            var odbcConnectionString = $"driver={driver};serverNode={connectionDetails.ServerAddress}:{connectionDetails.PortNumber};UID={connectionDetails.UserName};PWD={connectionDetails.Password}"; ;

            var results = new List<Dictionary<string, object>>();

            using (var connection = new OdbcConnection(odbcConnectionString))
            {
                await connection.OpenAsync();

                using (OdbcCommand command = new OdbcCommand(query, connection))
                {
                    command.CommandTimeout = 120;
                    using (OdbcDataReader reader = command.ExecuteReader())
                    {
                        // Read the first row to get column metadata
                        await reader.ReadAsync();

                        // Iterate over columns in the reader to gather metadata
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string columnName = reader.GetName(i);
                            string dataType = reader.GetDataTypeName(i) ?? "BINTEXT";

                            // Modify dataType if it contains '.'
                            if (dataType.Contains('.'))
                            {
                                dataType = dataType.Split('.').Last();
                            }

                            // Add column metadata to results
                            results.Add(new Dictionary<string, object>
                    {
                        { "ColumnName", columnName },
                        { "SourceType", dataType }
                    });
                        }
                    }
                }
                connection.Close();
            }

            return results;
        }


        private async Task<List<Dictionary<string, object>>> ExecuteISQuery(QueryRequestIS request)
        {
            var connectionDetails = await LoadConnectionDetailsAsync();
            var sapConnectionString = $"Server={connectionDetails.ServerAddress}:{connectionDetails.PortNumber};UserID={connectionDetails.UserName};Password={connectionDetails.Password};";

            var results = new List<Dictionary<string, object>>();

            using (var connection = new HanaConnection(sapConnectionString))
            {
                await connection.OpenAsync();
                string interimQuery = "";
                if (request.Query != null) { interimQuery = request.Query; }
                else
                {

                    interimQuery = $"SELECT TOP 1 * FROM {request.DatabaseName}.{request.TableName}";
                }
                using (var command = new HanaCommand(interimQuery, connection))

                using (var reader = await command.ExecuteReaderAsync())
                {
                    command.CommandTimeout = 120;
                    DataTable schemaTable = reader.GetSchemaTable();

                    foreach (DataRow row in schemaTable.Rows)
                    {
                        string columnName = row["ColumnName"].ToString();
                        string dataType = row["DataType"].ToString();

                        if (dataType.Contains('.'))
                        {
                            dataType = dataType.Split('.').Last();
                        }

                        results.Add(new Dictionary<string, object>
                        {
                            { "InterimField", columnName },
                            { "InterimType", dataType }
                        });
                    }
                }
                connection.Close();
            }

            return results;
        }



       






        #region DTO
        public class ConnectionDetailsRequest
        {
            public string ServerAddress { get; set; }
            public string PortNumber { get; set; }
            public string UserName { get; set; }
            public string Password { get; set; }
            
        }
        public class QueryRequest
        {
            public string Query { get; set; }
            //public int SiteId { get; set; }
        }
        public class QueryRequestIS
        {

            public string DatabaseName { get; set; }
            public string TableName { get; set; }
            public string Query { get; set; }
            //public int SiteId { get; set; }
        }
        public class RelayConnectionDetails
        {
            public string Key1 { get; set; }
            public string Key2 { get; set; }
        }
        #endregion

    }
}
