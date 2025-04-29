using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Sap.Data.Hana;
using WindowsServiceSap.DTOs;
using WindowsServiceSap.HelperClasses;

namespace WindowsServiceSap.Services
{
    public class SapQueryExecutor
    {
        private readonly  ConnectionDetails _service;
        public SapQueryExecutor()
        {
                _service = new ConnectionDetails();

        }
        public async Task<List<Dictionary<string, object>>> ExecuteSapQuery(string query,int connectorId)
        {

                var results = new List<Dictionary<string, object>>();

            try
            {
                var connectionDetails = await _service.LoadConnectionDetails(connectorId);
                var sapConnectionString = $"Server={connectionDetails.ServerAddress}:{connectionDetails.PortNumber};UserID={connectionDetails.UserName};Password={connectionDetails.Password};";


                //Fetch connection details from JSON based on ConnectionName
                //var connectionDetails = await _service.LoadOdbcConnectionDetailsAsync();
                //var connectionString = connectionDetails.QueryString;
                //var parameters = ParseConnectionString(connectionString);
                //Build SAP connection string from extracted parameters
                //var sapConnectionString = $"driver={parameters["driver"]};serverNode={parameters["serverNode"]};UID={parameters["UID"]};PWD={parameters["PWD"]}";

                // Open the SAP HANA connection

                //var sapConnectionString = $"Server={connectionDetails.ServerAddress}:{connectionDetails.PortNumber};UserID={connectionDetails.UserName};Password={connectionDetails.Password};";

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
            }
            catch (FileNotFoundException ex)
            {

                throw new FileNotFoundException("Failed to load connection details", ex);
            }
            catch (Sap.Data.Hana.HanaException ex) when (ex.Message.Contains("System call 'connect' failed"))
            {
                // Handle connection-specific HanaException
                throw new Exception("Unable to connect to the SAP HANA database. Please check credentials and network connectivity.", ex);
            }
            catch (Sap.Data.Hana.HanaException ex)
            {
                // Handle other HanaException cases
                throw new Exception($"An SAP HANA-related error occurred: {ex.Message}", ex);
            }
            catch (InvalidOperationException ex) { throw new InvalidOperationException(ex.Message); }
            catch (Exception ex)
            {
                // Catch other potential exceptions and log them accordingly.
                throw new Exception(ex.Message);
            }
            return results;
         }



        public async Task<List<Dictionary<string, object>>> ExecuteSapOdbcQuery(string query, int connectorId)
        {
            var results = new List<Dictionary<string, object>>();

            try
            {
                //var driver = "HDBODBC32";
                string driver = DetectSapOdbcDriver();
                if (string.IsNullOrEmpty(driver))
                    throw new Exception("No SAP HANA ODBC driver (HDBODBC or HDBODBC32) found on the system.");
                Console.WriteLine(driver);

                var connectionDetails = await _service.LoadConnectionDetails(connectorId);
                var odbcConnectionString = $"driver={driver};serverNode={connectionDetails.ServerAddress}:{connectionDetails.PortNumber};UID={connectionDetails.UserName};PWD={connectionDetails.Password}"; ;


                //// Fetch connection details from JSON based on ConnectionName
                //var connectionDetails = await _service.LoadOdbcConnectionDetailsAsync();
                //var connectionString = connectionDetails.QueryString; 
                //var parameters = ParseConnectionString(connectionString);
                //// Build SAP connection string from extracted parameters
                //var odbcSapConnectionString = $"driver={parameters["driver"]};serverNode={parameters["serverNode"]};UID={parameters["UID"]};PWD={parameters["PWD"]}";

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

            }
            catch (InvalidOperationException ex) { throw new InvalidOperationException(ex.Message); }

            catch (OdbcException odbcEx)
            {
         
                throw new Exception(odbcEx.Message);
            }
            catch (FileNotFoundException fileNotFoundEx)
            {
                // Handle specific case of missing file (e.g., connection details file)
                Console.WriteLine($"File Not Found: {fileNotFoundEx.Message}");
                throw new FileNotFoundException("Connection details file not found.", fileNotFoundEx);
            }
            catch (Sap.Data.Hana.HanaException ex) when (ex.Message.Contains("System call 'connect' failed"))
            {
                // Handle connection-specific HanaException
                throw new Exception("Unable to connect to the SAP HANA database. Please check credentials and network connectivity.", ex);

            }
            catch (Sap.Data.Hana.HanaException ex)
            {
                // Handle other HanaException cases
                throw new Exception($"An SAP HANA-related error occurred: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                // Catch any other exceptions
                Console.WriteLine($"An error occurred: {ex.Message}");
                throw new Exception("An unexpected error occurred while executing the query.", ex);
            }
            return results;

        }


        public async Task<List<Dictionary<string, object>>> ExecuteSapISQuery(QueryRequestIS request)
        {
            var connectionDetails = await _service.LoadConnectionDetails(request.ConnectorId);
            var sapConnectionString = $"Server={connectionDetails.ServerAddress}:{connectionDetails.PortNumber};UserID={connectionDetails.UserName};Password={connectionDetails.Password};";

            //// Fetch connection details from JSON based on ConnectionName
            //var connectionDetails = await _service.LoadOdbcConnectionDetailsAsync();
            //var connectionString = connectionDetails.QueryString; 
            //var parameters = ParseConnectionString(connectionString);
            //// Build SAP connection string from extracted parameters
            //var sapConnectionString = $"driver={parameters["driver"]};serverNode={parameters["serverNode"]};UID={parameters["UID"]};PWD={parameters["PWD"]}";





            var results = new List<Dictionary<string, object>>();
            try
            {

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
            }
            catch (InvalidOperationException ex) { throw new InvalidOperationException(ex.Message); }

            catch (FileNotFoundException ex)
            {

                throw new FileNotFoundException("Failed to load connection details", ex);
            }
            catch (Sap.Data.Hana.HanaException ex) when (ex.Message.Contains("System call 'connect' failed"))
            {
                // Handle connection-specific HanaException
                throw new Exception("Unable to connect to the SAP HANA database. Please check credentials and network connectivity.", ex);

            }
            catch (Sap.Data.Hana.HanaException ex)
            {
                // Handle other HanaException cases
                throw new Exception($"An SAP HANA-related error occurred: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                // Catch other potential exceptions and log them accordingly.
                throw new Exception("An error occurred while executing the query.", ex);
            }
            return results;
        }

        private string DetectSapOdbcDriver()
        {
            var drivers = new HashSet<string>();

            // Check for 64-bit ODBC drivers
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\ODBC\ODBCINST.INI\ODBC Drivers"))
            {
                if (key != null)
                {
                    drivers.UnionWith(key.GetValueNames());
                }
            }

            // Check for 32-bit ODBC drivers
            using (var key32 = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\ODBC\ODBCINST.INI\ODBC Drivers"))
            {
                if (key32 != null)
                {
                    drivers.UnionWith(key32.GetValueNames());
                }
            }

            bool is64BitProcess = Environment.Is64BitProcess;

            if (drivers.Contains("HDBODBC") && is64BitProcess)
                return "HDBODBC";  // Use 64-bit driver if process is 64-bit

            if (drivers.Contains("HDBODBC32") && !is64BitProcess)
                return "HDBODBC32"; // Use 32-bit driver if process is 32-bit

            if (drivers.Contains("HDBODBC"))
                return "HDBODBC"; // Fallback to 64-bit if available

            if (drivers.Contains("HDBODBC32"))
                return "HDBODBC32"; // Fallback to 32-bit if only that is available

            return null; // No SAP HANA ODBC driver found
        }




    }
}
