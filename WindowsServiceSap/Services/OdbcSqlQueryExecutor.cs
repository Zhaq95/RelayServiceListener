using Sap.Data.Hana;
using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WindowsServiceSap.DTOs;
using WindowsServiceSap.HelperClasses;

namespace WindowsServiceSap.Services
{
    public class OdbcSqlQueryExecutor
    {
        private readonly ConnectionDetails _service;
        public OdbcSqlQueryExecutor()
        {
            _service = new ConnectionDetails();
        }


        public async Task<List<Dictionary<string, object>>> ExecuteSqlQuery(string query)
        {
            var results = new List<Dictionary<string, object>>();

            try
            {
                var connectionDetails = await _service.LoadConnectionDetailsAsync();
                var odbcConnectionString = $"Driver={{ODBC Driver 17 for SQL Server}};Server={connectionDetails.ServerAddress},{connectionDetails.PortNumber};UID={connectionDetails.UserName};PWD={connectionDetails.Password};";

                using (var connection = new OdbcConnection(odbcConnectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new OdbcCommand(query, connection))
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
                throw new FileNotFoundException("Error:Failed to load connection details", ex);
            }
            catch (OdbcException ex) when (ex.Message.IndexOf("authentication failed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Handle ODBC-specific exception
                throw new Exception("Error: Authentication failed. Please check the credentials.", ex);
            }
            catch (OdbcException ex)
            {
                throw new Exception($"Error: An ODBC-related error occurred: {ex.Message}", ex);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(ex.Message);
            }
            catch (Exception ex)
            {
                // Catch other potential exceptions and log them accordingly.
                throw new Exception("Error: An error occurred while executing the query.", ex);
            }

            return results;
        }

       
    }
}
