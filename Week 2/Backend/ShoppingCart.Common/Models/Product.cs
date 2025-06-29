using System.ComponentModel.DataAnnotations;

namespace ShoppingCart.Common.Models;

public class Product : BaseEntity
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    [Required]
    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }
    
    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }
    
    [StringLength(100)]
    public string? Brand { get; set; }
    
    [StringLength(50)]
    public string? SKU { get; set; }
    
    [StringLength(500)]
    public string? ImageUrl { get; set; }
    
    public int CategoryId { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual Category Category { get; set; } = null!;
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
} 