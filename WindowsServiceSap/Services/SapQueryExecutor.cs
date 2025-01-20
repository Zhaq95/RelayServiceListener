using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
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
        public async Task<List<Dictionary<string, object>>> ExecuteQuery(string query)
        {

                var results = new List<Dictionary<string, object>>();

                // Fetch connection details from JSON based on ConnectionName
                var connectionDetails = await _service.LoadConnectionDetailsAsync();
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



        public async Task<List<Dictionary<string, object>>> ExecuteOdbcQuery(string query)
        {
            var driver = "HDBODBC32";
            var connectionDetails = await _service.LoadConnectionDetailsAsync();
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


        public async Task<List<Dictionary<string, object>>> ExecuteISQuery(QueryRequestIS request)
        {
            var connectionDetails = await _service.LoadConnectionDetailsAsync();
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

        public async Task<string> CheckStatus(string query)
        {
            try
            {
                var connectionDetails = await _service.LoadConnectionDetailsAsync();
                var sapConnectionString = $"Server={connectionDetails.ServerAddress}:{connectionDetails.PortNumber};UserID={connectionDetails.UserName};Password={connectionDetails.Password};";

                var results = new List<Dictionary<string, object>>();

                using (var connection = new HanaConnection(sapConnectionString))
                {
                    await connection.OpenAsync();

                    connection.Close();
                }

                return "True";
            }
            catch (Sap.Data.Hana.HanaException ex) when (ex.Message.IndexOf("authentication failed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Log the authentication failure and return "False"
                return "False";
            }
            catch (Exception ex)
            {
                // Log other exceptions and return "False"
                return "False";
            }
        }
    }
}
