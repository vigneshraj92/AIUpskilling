namespace ShoppingCart.Common.Patterns.Strategy;

public class CryptoPaymentStrategy : IPaymentStrategy
{
    public string PaymentMethodName => "Crypto";

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request)
    {
        if (!ValidatePaymentRequest(request))
        {
            return new PaymentResult
            {
                IsSuccess = false,
                Message = "Invalid payment request for cryptocurrency"
            };
        }

        // Simulate cryptocurrency payment processing
        await Task.Delay(2000); // Simulate blockchain transaction

        var cryptoType = request.PaymentData.GetValueOrDefault("CryptoType")?.ToString();
        var walletAddress = request.PaymentData.GetValueOrDefault("WalletAddress")?.ToString();

        if (string.IsNullOrEmpty(cryptoType) || string.IsNullOrEmpty(walletAddress))
        {
            return new PaymentResult
            {
                IsSuccess = false,
                Message = "Invalid cryptocurrency payment details"
            };
        }

        // Validate wallet address (basic validation)
        if (walletAddress.Length < 26)
        {
            return new PaymentResult
            {
                IsSuccess = false,
                Message = "Invalid wallet address"
            };
        }

        // Simulate successful payment
        var transactionId = $"CR_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
        
        return new PaymentResult
        {
            IsSuccess = true,
            TransactionId = transactionId,
            Message = "Cryptocurrency payment processed successfully",
            AdditionalData = new Dictionary<string, object>
            {
                ["CryptoType"] = cryptoType,
                ["WalletAddress"] = walletAddress,
                ["PaymentMethod"] = "Crypto"
            }
        };
    }

    public bool ValidatePaymentRequest(PaymentRequest request)
    {
        if (request.Amount <= 0)
            return false;

        if (string.IsNullOrEmpty(request.OrderId))
            return false;

        if (!request.PaymentData.ContainsKey("CryptoType") || 
            !request.PaymentData.ContainsKey("WalletAddress"))
            return false;

        return true;
    }
} 