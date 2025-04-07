using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BeautySky.Models;

public partial class Order
{
    public int OrderId { get; set; }

    public DateTime? OrderDate { get; set; }

    public int? UserId { get; set; }

    public decimal? TotalAmount { get; set; }

    public int? PromotionId { get; set; }

    public decimal? DiscountAmount { get; set; }

    public decimal? FinalAmount { get; set; }

    public int? PaymentId { get; set; }
    public DateTime? CancelledDate { get; set; }
    public string? CancelledReason { get; set; }

    public string Status { get; set; } = null!;
    public DateTime? DeliveryDate { get; set; }
    [JsonIgnore]

    public DateTime? ShippingDate { get; set; }
    [JsonIgnore]

    public virtual ICollection<OrderProduct> OrderProducts { get; set; } = new List<OrderProduct>();
    [JsonIgnore]

    public virtual Payment? Payment { get; set; }
    [JsonIgnore]
    public virtual Promotion? Promotion { get; set; }
    [JsonIgnore]
    public virtual User? User { get; set; }
    
}
