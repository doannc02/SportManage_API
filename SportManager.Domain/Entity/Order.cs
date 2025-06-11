using SportManager.Domain.Constants;
using System.ComponentModel.DataAnnotations.Schema;

namespace SportManager.Domain.Entity;

[Table(TableNameConstants.Order)]
public class Order : EntityBase<Guid>
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; }
    public string? Notes { get; set; }
    // Lý do hủy đơn hàng, nếu có
    public string? ReasonCancel { get; set; }
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

    // Các thuộc tính mới để theo dõi thời gian của từng trạng thái
    public DateTime? ConfirmedDate { get; set; } // Ngày xác nhận đơn hàng
    public DateTime? PreparingDate { get; set; } // Ngày bắt đầu chuẩn bị hàng
    public DateTime? ShippedDate { get; set; } // Ngày shipper giao hàng
    public DateTime? DeliveredDate { get; set; } // Ngày khách hàng nhận được hàng (giao thành công)
    public DateTime? CanceledDate { get; set; } // Ngày hủy đơn hàng
    public Guid? ShipperId { get; set; } // ID của shipper nếu có
    public string? ImageConfirmed { get; set; } // Hình ảnh xác nhận đơn hàng, nếu có

    public DateTime? ExpectedDeliveryDate { get; set; }
}

public enum StateOrder
{
    Canceled, // đã hủy đơn hàng, không thao tác tiếp được
    Receivered,
    Sendered,
    Pending,        // Mới đặt, chờ xác nhận
    Confirmed,      // Đã xác nhận (tương ứng với "Đã xác nhận" trên UI)
    Processing,     // Đang chuẩn bị hàng (tương ứng với "Đang chuẩn bị hàng" trên UI)
    Shipped,        // Đang giao hàng (tương ứng với "Đang giao hàng" trên UI)
    Delivered,      // Đã giao hàng (tương ứng với "Đã giao hàng" trên UI)
    Returned,   // Đã hoàn hàng (hủy sau khi giao)
    Refunded,   // Đã hoàn tiền
}