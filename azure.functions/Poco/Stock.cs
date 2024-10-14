using Newtonsoft.Json;

namespace azure.functions.Poco
{
    public class Stock
    {
        public string id { get; set; }
        public string symbol { get; set; }
        public decimal price { get; set; }
        public string timestamp { get; set; }
        public string range { get; set; }
    }
}
