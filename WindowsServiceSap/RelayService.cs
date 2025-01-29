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
using WindowsServiceSap.DTOs;
using WindowsServiceSap.HelperClasses;
using WindowsServiceSap.Services;

namespace WindowsServiceSap
{
    public sealed class RelayService : IDisposable
    {
        private HybridConnectionListener _listener;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private System.Timers.Timer _listenerStatusTimer;
        private Logger logger;
        private SapQueryExecutor _queryExecutor;
        private ConnectionDetails _connectionDetails;
        private ConnectorStatus _connectorStatus;
        public RelayService()
        {
            logger = new Logger();
            _queryExecutor = new SapQueryExecutor();
            _connectionDetails = new ConnectionDetails();
            _connectorStatus = new ConnectorStatus();
        }




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
                    await logger.WriteLogAsync($"RelayConnectionDetails.json not found. Creating default configuration file...");

                    // Initialize RelayConnectionDetails with blank values
                    var newRelayDetails = new RelayConnectionDetails
                    {
                        Key1 = "",  // Blank for the user to fill in
                        Key2 = "" // Blank for the user to fill in
                    };

                    // Save the RelayConnectionDetails with blank values to file
                    await _connectionDetails.SaveRelayConnectionDetailsAsync(newRelayDetails);

                    Console.WriteLine("Default RelayConnectionDetails.json file created. Please update this file with valid values.");
                    await logger.WriteLogAsync($"Default RelayConnectionDetails.json file created. Please update this file with valid values.");

                    return;
                }

                // Step 2: Load RelayConnectionDetails (after creation or already existing)
                var relayDetails = await _connectionDetails.LoadRelayConnectionDetailsAsync();

                // Check if values are missing (optional)
                if (string.IsNullOrEmpty(relayDetails.Key1) || string.IsNullOrEmpty(relayDetails.Key2))
                {
                    Console.WriteLine("Warning: RelayConnectionString or HybridConnectionName is still missing in RelayConnectionDetails.json.");
                    await logger.WriteLogAsync($"Warning: RelayConnectionString or HybridConnectionName is still missing in RelayConnectionDetails.json.");

                    return;
                }

                // Step 3: Decrypt the encrypted keys
                string decryptedConnectionString = EncryptionHelper.Decrypt(relayDetails.Key1);
                string decryptedHybridConnectionName = EncryptionHelper.Decrypt(relayDetails.Key2);

                if (string.IsNullOrEmpty(decryptedConnectionString) || string.IsNullOrEmpty(decryptedHybridConnectionName))
                {
                    Console.WriteLine("Decrypted RelayConnectionString or HybridConnectionName is empty.");
                    await logger.WriteLogAsync($"Decrypted RelayConnectionString or HybridConnectionName is empty..");

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
                            await logger.WriteLogAsync($"Error processing request: {ex.Message}");

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
                Console.WriteLine("Relay Listener Service started successfully.");
                await logger.WriteLogAsync("Relay Listener Service started successfully.");


                // Start the status timer to monitor the listener
                _listenerStatusTimer = new System.Timers.Timer(30000);
                _listenerStatusTimer.Elapsed += async (sender, e) => await CheckListenerStatusAsync();
                _listenerStatusTimer.AutoReset = true;
                _listenerStatusTimer.Enabled = true;

                // Keep the service running until cancellation is requested
                await Task.Delay(Timeout.Infinite, _cts.Token);
            }
            catch (InvalidOperationException ex)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("ConnectRelay operation canceled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Relay Listener Service: {ex.Message}");
                await logger.WriteLogAsync($"Error in Relay Listener Service: {ex.Message}");

                throw;
            }
            finally
            {
                if (_listener != null)
                {
                    Console.WriteLine("Closing HybridConnectionListener...");
                    await logger.WriteLogAsync("Closing HybridConnectionListener...");

                    await _listener.CloseAsync(CancellationToken.None);
                    _listener = null;
                    Console.WriteLine("HybridConnectionListener closed.");
                    await logger.WriteLogAsync("HybridConnectionListener closed.");

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
                    await logger.WriteLogAsync($"Received request body: {requestBody}");

                }

                using (var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8, 1024, leaveOpen: true))
                {
                    if (string.IsNullOrEmpty(requestName))
                    {
                        await SendErrorResponseAsync(context, writer, HttpStatusCode.BadRequest, "RequestName header is missing.");
                        return;
                    }
                    if (requestName == "SendConnectionDetails")
                    {
                        await HandleRequestAsync<ConnectionDetailsRequest>(context, writer, requestBody, async (details) =>
                        {
                            await _connectionDetails.SaveOrUpdateConnectionDetailsAsync(details);
                            return JsonSerializer.Serialize(new { message = "Connection details saved successfully." });
                        });
                        return;
                    }
                    else if (requestName == "CheckRelayConnection")
                    {
                        await HandleRequestAsync<QueryRequest>(context, writer, requestBody, async (req) =>
                            {
                                var result = await _connectionDetails.LoadRelayConnectionDetailsAsync();
                                return JsonSerializer.Serialize(result); // Serialize the list to JSON
                            });
                        return;
                    }
                    else if (requestName == "DeleteConnectionDetails")
                    {
                        await HandleRequestAsync<DeleteConnectionDetailsDTO>(context, writer, requestBody, async (req) =>
                            {
                                var result = await _connectionDetails.DeleteConnectionDetailsAsync(req);
                                return JsonSerializer.Serialize(new
                                {
                                    Success = result,
                                    Message = result ? "File deleted successfully." : "File does not exist."
                                });
                            });
                        return;
                    }
                    else if (requestName == "CheckConnectorStatus")
                    {
                        await HandleRequestAsync<QueryRequest>(context, writer, requestBody, async (req) =>
                        {
                            var result = await _connectorStatus.CheckStatus(req.Query);
                            return JsonSerializer.Serialize(result); // Serialize the list to JSON
                        });
                    }
                    // ✅ Load connection details ONLY if the file exists
                    string jsonFilePath = Path.Combine(AppContext.BaseDirectory, "ConnectionDetails.json");
                    if (!File.Exists(jsonFilePath))
                    {
                        await SendErrorResponseAsync(context, HttpStatusCode.BadRequest, "ConnectionDetails.json file does not exist. Please send connection details first.");
                        return;
                    }
                    var connectionDetails = await _connectionDetails.LoadConnectionDetailsAsync();
                    if (connectionDetails == null || string.IsNullOrEmpty(connectionDetails.ConnectorType))
                    {
                        await SendErrorResponseAsync(context, HttpStatusCode.BadRequest, "ConnectorType is missing or invalid.");
                        return;
                    }


                    // ✅ Route based on ConnectorType
                    switch (connectionDetails.ConnectorType.ToLower().Replace(" ", ""))
                    {
                        case "saphana":
                            if (requestName == "ExecuteQuery")
                            {
                                await HandleRequestAsync<QueryRequest>(context, writer, requestBody, async (req) =>
                                {
                                    var result = await _queryExecutor.ExecuteSapQuery(req.Query);
                                    return JsonSerializer.Serialize(result);
                                });
                            }
                            else if (requestName == "ExecuteOdbcQuery")
                            {
                                await HandleRequestAsync<QueryRequest>(context, writer, requestBody, async (req) =>
                                {
                                    var result = await _queryExecutor.ExecuteSapOdbcQuery(req.Query);
                                    return JsonSerializer.Serialize(result);
                                });
                            }
                            else if (requestName == "ExecuteISQuery")
                            {
                                await HandleRequestAsync<QueryRequestIS>(context, writer, requestBody, async (req) =>
                                {
                                    var result = await _queryExecutor.ExecuteSapISQuery(req);
                                    return JsonSerializer.Serialize(result); // Serialize the list to JSON
                                });
                            }
                            
                            break;

                        case "odbcsql":
                            
                            break;

                        default:
                            await SendErrorResponseAsync(context, writer, HttpStatusCode.BadRequest, "Unsupported ConnectorType.");
                            break;
                    }

                    //switch (requestName)
                    //{
                    //    case "SendConnectionDetails":
                    //        await HandleRequestAsync<ConnectionDetailsRequest>(context, writer, requestBody, async (details) =>
                    //        {
                    //            await _connectionDetails.SaveOrUpdateConnectionDetailsAsync(details);
                    //            return JsonSerializer.Serialize(new { message = "Connection details saved successfully." }); // Serialize to JSON
                    //        });
                    //        break;

                    //    case "SendOdbcConnectionDetails":
                    //        await HandleRequestAsync<OdbcConnectionDetailsRequest>(context, writer, requestBody, async (details) =>
                    //        {
                    //            await _connectionDetails.SaveOrUpdateOdbcConnectionDetailsAsync(details);
                    //            return JsonSerializer.Serialize(new { message = " ODbc Connection details saved successfully." }); // Serialize to JSON
                    //        });
                    //        break;
                    //    case "ExecuteSapQuery":
                    //        await HandleRequestAsync<QueryRequest>(context, writer, requestBody, async (req) =>
                    //        {
                    //            var result = await _queryExecutor.ExecuteSapQuery(req.Query);
                    //            return JsonSerializer.Serialize(result); // Serialize the list to JSON
                    //        });
                    //        break;
                    //    case "ExecuteSapOdbcQuery":
                    //        await HandleRequestAsync<QueryRequest>(context, writer, requestBody, async (req) =>
                    //        {
                    //            var result = await _queryExecutor.ExecuteSapOdbcQuery(req.Query);
                    //            return JsonSerializer.Serialize(result); // Serialize the list to JSON
                    //        });
                    //        break;
                    //    case "ExecuteSapISQuery":
                    //        await HandleRequestAsync<QueryRequestIS>(context, writer, requestBody, async (req) =>
                    //        {
                    //            var result = await _queryExecutor.ExecuteSapISQuery(req);
                    //            return JsonSerializer.Serialize(result); // Serialize the list to JSON
                    //        });
                    //        break;

                    //    case "CheckStatus":
                    //        await HandleRequestAsync<QueryRequest>(context, writer, requestBody, async (req) =>
                    //        {
                    //            var result = await _queryExecutor.CheckStatus(req.Query);
                    //            return JsonSerializer.Serialize(result); // Serialize the list to JSON
                    //        });
                    //        break;
                    //    case "CheckRelayConnection":
                    //        await HandleRequestAsync<QueryRequest>(context, writer, requestBody, async (req) =>
                    //        {
                    //            var result = await _connectionDetails.LoadRelayConnectionDetailsAsync();
                    //            return JsonSerializer.Serialize(result); // Serialize the list to JSON
                    //        });
                    //        break;
                    //    case "DeleteConnectionDetails":
                    //        await HandleRequestAsync<DeleteConnectionDetailsDTO>(context, writer, requestBody, async (req) =>
                    //        {
                    //            var result = await _connectionDetails.DeleteConnectionDetailsAsync(req);
                    //            return JsonSerializer.Serialize(new
                    //            {
                    //                Success = result,
                    //                Message = result ? "File deleted successfully." : "File does not exist."
                    //            });
                    //        });
                    //        break;
                    //    default:
                    //        await SendErrorResponseAsync(context, writer, HttpStatusCode.BadRequest, "Invalid RequestName header.");
                    //        break;
                    //}
                }
            }
            catch (FileNotFoundException ex)
            { await SendErrorResponseAsync(context, HttpStatusCode.NotFound, $"ConnectionDetails.json file not found: {ex.Message}"); }
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



        #region Private Functions
        private async Task CheckListenerStatusAsync()
        {
            if (_listener != null && !_listener.IsOnline)
            {
                Console.WriteLine("HybridConnectionListener is offline. Attempting to reopen...");
                await logger.WriteLogAsync("HybridConnectionListener is offline. Attempting to reopen...");

                try
                {
                    await _listener.OpenAsync(_cts.Token);
                    Console.WriteLine("HybridConnectionListener reopened successfully.");
                    await logger.WriteLogAsync("HybridConnectionListener reopened successfully.");

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reopening listener: {ex.Message}");
                    await logger.WriteLogAsync($"Error reopening listener: {ex.Message}");

                }
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
            catch (FileNotFoundException ex) 
            {
                await SendErrorResponseAsync(context, writer, HttpStatusCode.NotFound, $"Connection Details File not Found: {ex.Message}");

            }
            catch (OdbcException ex)
            {
                await SendErrorResponseAsync(context, writer, HttpStatusCode.BadRequest, $"Something went wrong with ODBC: {ex.Message}");

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

        #endregion

    }
}
