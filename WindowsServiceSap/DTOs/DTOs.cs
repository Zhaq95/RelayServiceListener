using System;

namespace WindowsServiceSap.DTOs
{
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
        // public int SiteId { get; set; }
    }

    public class QueryRequestIS
    {
        public string DatabaseName { get; set; }
        public string TableName { get; set; }
        public string Query { get; set; }
        // public int SiteId { get; set; }
    }

    public class RelayConnectionDetails
    {
        public string Key1 { get; set; }
        public string Key2 { get; set; }
    }
}
