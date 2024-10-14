namespace azure.functions.Poco
{
    public class StockRules
    {
        public string symbol { get; set; }
        public decimal maxPrice { get; set; }
        public decimal minPrice { get; set; }
    }
}
