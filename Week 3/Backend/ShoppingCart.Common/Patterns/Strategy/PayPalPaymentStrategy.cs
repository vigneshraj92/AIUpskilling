namespace ShoppingCart.Common.Patterns.Strategy;

public class PayPalPaymentStrategy : IPaymentStrategy
{
    public string PaymentMethodName => "PayPal";

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request)
    {
        if (!ValidatePaymentRequest(request))
        {
            return new PaymentResult
            {
                IsSuccess = false,
                Message = "Invalid payment request for PayPal"
            };
        }

        // Simulate PayPal payment processing
        await Task.Delay(1500); // Simulate API call

        var paypalEmail = request.PaymentData.GetValueOrDefault("PayPalEmail")?.ToString();

        if (string.IsNullOrEmpty(paypalEmail) || !IsValidEmail(paypalEmail))
        {
            return new PaymentResult
            {
                IsSuccess = false,
                Message = "Invalid PayPal email address"
            };
        }

        // Simulate successful payment
        var transactionId = $"PP_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
        
        return new PaymentResult
        {
            IsSuccess = true,
            TransactionId = transactionId,
            Message = "PayPal payment processed successfully",
            AdditionalData = new Dictionary<string, object>
            {
                ["PayPalEmail"] = paypalEmail,
                ["PaymentMethod"] = "PayPal"
            }
        };
    }

    public bool ValidatePaymentRequest(PaymentRequest request)
    {
        if (request.Amount <= 0)
            return false;

        if (string.IsNullOrEmpty(request.OrderId))
            return false;

        if (!request.PaymentData.ContainsKey("PayPalEmail"))
            return false;

        return true;
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
} 