using SportManager.Application.Common.Exception;
using SportManager.Application.Common.Interfaces;
using SportManager.Application.Orders.Models;

namespace SportManager.Application.Orders.Queries;

public record GetOrderByIdQuery(Guid Id) : IRequest<OrderDto>
{
}

public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    private readonly IApplicationDbContext _dbContext;

    public GetOrderByIdQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OrderDto> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        // Truy xuất đơn hàng trước
        var order = await _dbContext.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.Voucher)
            .Include(o => o.Customer)
                .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);

        if (order == null)
            return null;

        // Truy xuất địa chỉ giao hàng dựa trên ShippingAddressId từ đơn hàng
        var shippingAddress = await _dbContext.ShippingAddresses
            .FirstOrDefaultAsync(a => a.Id == order.ShippingAddressId, cancellationToken);

        // Tạo đối tượng ShippingAddressViewOrder nếu địa chỉ giao hàng tồn tại
        ShippingAddressViewOrder shippingAddressDto = null;
        if (shippingAddress != null)
        {
            shippingAddressDto = new ShippingAddressViewOrder
            {
                AddressDetail = $"{shippingAddress.Ward}, {shippingAddress.District}, {shippingAddress.City}, {shippingAddress.Country}",
                Phone = shippingAddress.Phone,
                ReceiveName = shippingAddress.RecipientName
            };
        }

        // Trả về OrderDto với thông tin địa chỉ giao hàng
        return new OrderDto
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            CustomerName = order.Customer.User.Username,
            OrderDate = order.OrderDate,
            State = order.State.ToString(),
            Notes = order.Notes,
            SubTotal = order.CalculateSubTotal(),
            DiscountAmount = order.DiscountAmount,
            Total = order.CalculateSubTotal() - order.DiscountAmount,
            VoucherCode = order.Voucher != null ? order.Voucher?.Code : null,
            ShippingAddress = shippingAddress != null
            ? new ShippingAddressViewOrder
            {
                AddressDetail = $"{shippingAddress.Ward}, {shippingAddress.District}, " +
                              $"{shippingAddress.City}, {shippingAddress.Country}",
                Phone = shippingAddress.Phone,
                ReceiveName = shippingAddress.RecipientName
            }
            : null,
            OrderItems = order.OrderItems.Select(item => new OrderItemDto
            {
                Id = item.Id,
                ProductId = item.ProductVariantId,
                ProductName = item.ProductVariant.Name,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice
            }).ToList()
        };
    }

}
