using System.ComponentModel.DataAnnotations;

namespace ShoppingCart.Common.Models;

public class User : BaseEntity
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [StringLength(255)]
    public string PasswordHash { get; set; } = string.Empty;
    
    [StringLength(20)]
    public string? PhoneNumber { get; set; }
    
    [StringLength(255)]
    public string? Address { get; set; }
    
    public string Role { get; set; } = "Customer";
    
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
} 