namespace ShoppingCart.Common.Patterns.Strategy;

public class CreditCardPaymentStrategy : IPaymentStrategy
{
    public string PaymentMethodName => "CreditCard";

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request)
    {
        if (!ValidatePaymentRequest(request))
        {
            return new PaymentResult
            {
                IsSuccess = false,
                Message = "Invalid payment request for credit card"
            };
        }

        // Simulate credit card payment processing
        await Task.Delay(1000); // Simulate API call

        var cardNumber = request.PaymentData.GetValueOrDefault("CardNumber")?.ToString();
        var expiryDate = request.PaymentData.GetValueOrDefault("ExpiryDate")?.ToString();

        // Validate card number (basic validation)
        if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 13)
        {
            return new PaymentResult
            {
                IsSuccess = false,
                Message = "Invalid card number"
            };
        }

        // Simulate successful payment
        var transactionId = $"CC_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
        
        return new PaymentResult
        {
            IsSuccess = true,
            TransactionId = transactionId,
            Message = "Credit card payment processed successfully",
            AdditionalData = new Dictionary<string, object>
            {
                ["LastFourDigits"] = cardNumber[^4..],
                ["PaymentMethod"] = "CreditCard"
            }
        };
    }

    public bool ValidatePaymentRequest(PaymentRequest request)
    {
        if (request.Amount <= 0)
            return false;

        if (string.IsNullOrEmpty(request.OrderId))
            return false;

        if (!request.PaymentData.ContainsKey("CardNumber") || 
            !request.PaymentData.ContainsKey("ExpiryDate") ||
            !request.PaymentData.ContainsKey("CVV"))
            return false;

        return true;
    }
} 