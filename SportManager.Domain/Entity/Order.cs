using SportManager.Domain.Constants;
using System.ComponentModel.DataAnnotations.Schema;

namespace SportManager.Domain.Entity;

[Table(TableNameConstants.Order)]
public class Order : EntityBase<Guid>
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; }
    public string? Notes { get; set; }
    public StateOrder State { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public Payment Payment { get; set; }
    public Guid ShippingAddressId { get; set; }

    public Guid? VoucherId { get; set; }
    [ForeignKey(nameof(VoucherId))]
    public Voucher? Voucher { get; set; }
    public decimal DiscountAmount { get; set; }

    // Tính tổng tiền của đơn hàng trước khi giảm giá
    public decimal SubTotal => OrderItems.Sum(item => item.TotalPrice);

    // Tổng tiền sau khi áp dụng giảm giá
    public decimal Total => SubTotal - DiscountAmount;
}

public enum StateOrder
{
    Pending, // mới đặt có thể hủy đơn hàng được
    Canceled, // đã hủy đơn hàng, không thao tác tiếp được
    Shipped,
    Receivered,
    Sendered,
    Processing
}