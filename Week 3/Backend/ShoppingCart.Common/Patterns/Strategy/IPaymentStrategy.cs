namespace ShoppingCart.Common.Patterns.Strategy;

public interface IPaymentStrategy
{
    string PaymentMethodName { get; }
    Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request);
    bool ValidatePaymentRequest(PaymentRequest request);
}

public class PaymentRequest
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string OrderId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public Dictionary<string, object> PaymentData { get; set; } = new();
}

public class PaymentResult
{
    public bool IsSuccess { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> AdditionalData { get; set; } = new();
} 