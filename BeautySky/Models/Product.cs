using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BeautySky.Models;

public partial class Product
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = null!;
    [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative")]

    public decimal Price { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative")]

    public int Quantity { get; set; }

    public string? Description { get; set; }

    public string? Ingredient { get; set; }

    public int? CategoryId { get; set; }

    public int? SkinTypeId { get; set; }

    public bool IsActive { get; set; }
    public DateTime? Expire { get; set; }
    [JsonIgnore]
    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();
    [JsonIgnore]
    public virtual Category? Category { get; set; }
    [JsonIgnore]
    public virtual ICollection<OrderProduct> OrderProducts { get; set; } = new List<OrderProduct>();
    [JsonIgnore]
    public virtual ICollection<ProductsImage> ProductsImages { get; set; } = new List<ProductsImage>();
    [JsonIgnore]
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    [JsonIgnore]
    public virtual SkinType? SkinType { get; set; }
}
