using Sap.Data.Hana;
using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WindowsServiceSap.DTOs;

namespace WindowsServiceSap.HelperClasses
{
    public class ConnectorStatus
    {
        private readonly ConnectionDetails _service;
        public ConnectorStatus()
        {
                _service = new ConnectionDetails();
        }
        public async Task<string> CheckStatus(string query, int connectorId)
        {
            try
            {
                var connectionDetails = await _service.LoadConnectionDetails(connectorId);

                if (connectionDetails == null || string.IsNullOrEmpty(connectionDetails.ConnectorType))
                {
                    throw new Exception("ConnectorType is missing or invalid.");
                }

                string normalizedConnectorType = connectionDetails.ConnectorType.ToLower().Replace(" ", "");
                switch (normalizedConnectorType)
                {
                    case "saphana":
                        return await CheckSapHanaStatus(connectionDetails);
                    case "odbcmssql":
                        return await CheckOdbcSqlStatus(connectionDetails);
                    default:
                        throw new Exception($"Unsupported ConnectorType: {connectionDetails.ConnectorType}");
                }

                
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
           
        }
        private async Task<string> CheckSapHanaStatus(ConnectionDetailsRequest connectionDetails)
        {
            try
            {
                var sapConnectionString = $"Server={connectionDetails.ServerAddress}:{connectionDetails.PortNumber};UserID={connectionDetails.UserName};Password={connectionDetails.Password};";

                using (var connection = new HanaConnection(sapConnectionString))
                {
                    await connection.OpenAsync();
                    connection.Close();
                }

                return "True";
            }
            catch (InvalidOperationException ex) { throw new InvalidOperationException(ex.Message); }
            catch (Sap.Data.Hana.HanaException ex) when (ex.Message.IndexOf("authentication failed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ex.Message; // Authentication failed
            }
            catch (Sap.Data.Hana.HanaException ex) when (ex.Message.Contains("System call 'connect' failed"))
            {
                throw new Exception("Error: Unable to connect to the SAP HANA database. Please check credentials and network connectivity.", ex);
            }
            catch (FileNotFoundException ex)
            {

                throw new FileNotFoundException("Error: Failed to load connection details", ex.Message);
            }
            catch (Exception ex)
            {
                return ex.Message; // Any other error
            }
        }

        private async Task<string> CheckOdbcSqlStatus(ConnectionDetailsRequest connectionDetails)
        {
            try
            {
                var odbcConnectionString = $"Driver={{ODBC Driver 17 for SQL Server}};Server={connectionDetails.ServerAddress},{connectionDetails.PortNumber};UID={connectionDetails.UserName};PWD={connectionDetails.Password};";

                using (var connection = new OdbcConnection(odbcConnectionString))
                {
                    await connection.OpenAsync();
                    connection.Close();
                }

                return "True";
            }
            catch (InvalidOperationException ex) { throw new InvalidOperationException(ex.Message); }

            catch (OdbcException ex) when (ex.Message.IndexOf("authentication failed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ex.Message; // Authentication failed
            }
            catch (OdbcException ex)
            {
                throw new Exception("Error: Unable to connect to the ODBC SQL database. Please check credentials and network connectivity.", ex);
            }
            catch (FileNotFoundException ex)
            {

                throw new FileNotFoundException("Error: Failed to load connection details", ex.Message);
            }
            catch (Exception ex)
            {
                return ex.Message; // Any other error
            }
        }


    }
}
