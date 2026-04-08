using Microsoft.AspNetCore.Mvc;

namespace SV22T1020591.Shop.Models
{
    public class CartItem
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal SalePrice { get; set; }
        public int Quantity { get; set; }
    }
}
