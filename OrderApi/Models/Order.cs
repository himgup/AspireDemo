namespace OrderApi.Models;

public class Order
{
    public int Id { get; set; }
    public string Item { get; set; } = "";
    public int Quantity { get; set; }
    public string Status { get; set; } = "Pending";
}

public record CreateOrderRequest(string Item, int Quantity);
public record UpdateStatusRequest(string Status);
