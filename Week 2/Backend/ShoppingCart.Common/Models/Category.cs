using System.ComponentModel.DataAnnotations;

namespace ShoppingCart.Common.Models;

public class Category : BaseEntity
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    [StringLength(500)]
    public string? ImageUrl { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
} 