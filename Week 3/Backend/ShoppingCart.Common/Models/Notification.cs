using System.ComponentModel.DataAnnotations;

namespace ShoppingCart.Common.Models;

public class Notification : BaseEntity
{
    public int UserId { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string Type { get; set; } = "Info"; // Info, Success, Warning, Error
    
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Unread"; // Unread, Read
    
    [StringLength(100)]
    public string? RelatedEntityType { get; set; } // Order, Payment, etc.
    
    public int? RelatedEntityId { get; set; }
    
    public DateTime? ReadAt { get; set; }
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
} 