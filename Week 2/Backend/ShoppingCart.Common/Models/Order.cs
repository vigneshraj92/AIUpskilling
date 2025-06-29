using System.ComponentModel.DataAnnotations;

namespace ShoppingCart.Common.Models;

public class Order : BaseEntity
{
    [Required]
    [StringLength(50)]
    public string OrderNumber { get; set; } = string.Empty;
    
    public int UserId { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Pending";
    
    [Required]
    [Range(0, double.MaxValue)]
    public decimal TotalAmount { get; set; }
    
    [StringLength(255)]
    public string? ShippingAddress { get; set; }
    
    [StringLength(255)]
    public string? BillingAddress { get; set; }
    
    [StringLength(100)]
    public string? PaymentMethod { get; set; }
    
    [StringLength(50)]
    public string? PaymentStatus { get; set; } = "Pending";
    
    public DateTime? OrderDate { get; set; }
    
    public DateTime? ShippedDate { get; set; }
    
    public DateTime? DeliveredDate { get; set; }
    
    [StringLength(500)]
    public string? Notes { get; set; }
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public virtual Payment? Payment { get; set; }
} 