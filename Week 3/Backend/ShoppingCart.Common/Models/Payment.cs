using System.ComponentModel.DataAnnotations;

namespace ShoppingCart.Common.Models;

public class Payment : BaseEntity
{
    public int OrderId { get; set; }
    
    [Required]
    [StringLength(100)]
    public string PaymentMethod { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Pending";
    
    [Required]
    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }
    
    [StringLength(255)]
    public string? TransactionId { get; set; }
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    public DateTime? ProcessedAt { get; set; }
    
    [StringLength(500)]
    public string? ErrorMessage { get; set; }
    
    // Navigation properties
    public virtual Order Order { get; set; } = null!;
} 