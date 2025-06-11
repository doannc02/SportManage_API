using SportManager.Application.Abstractions;
using SportManager.Application.Common.Interfaces;
using SportManager.Domain.Entity;

namespace SportManager.Application.Orders.Commands;

public class UpdateOrderStatusCommand : IRequest<bool>
{
    public Guid OrderId { get; set; }
    public StateOrder NewStatus { get; set; }
    public string? Reason { get; set; } // Lý do hủy đơn hoặc từ chối
    public Guid? ShipperId { get; set; } // ID shipper nếu có
    public string? ImageConfirmed { get; set; } // Hình ảnh xác nhận giao hàng
}

public class UpdateOrderStatusCommandHandler(
    IApplicationDbContext _dbContext,
    ICurrentUserService _currentUser)
    : IRequestHandler<UpdateOrderStatusCommand, bool>
{
    public async Task<bool> Handle(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null)
        {
            throw new ApplicationException("Đơn hàng không tồn tại.");
        }

        // Kiểm tra quyền thay đổi trạng thái
        var currentUserId = Guid.Parse(_currentUser.UserId);
        var isAdmin = _currentUser.Roles.Contains("Admin");
        var isShipper = _currentUser.Roles.Contains("Shipper");

        // Validate quyền và luồng trạng thái
        ValidateStatusTransition(order.State, request.NewStatus, isAdmin, isShipper);

        // Cập nhật trạng thái và các thông tin liên quan
        switch (request.NewStatus)
        {
            case StateOrder.Canceled:
                await HandleCancelOrder(order, request.Reason, isAdmin, cancellationToken);
                break;

            case StateOrder.Confirmed:
                order.State = StateOrder.Confirmed;
                order.ConfirmedDate = DateTime.UtcNow;
                break;

            case StateOrder.Processing:
                order.State = StateOrder.Processing;
                order.PreparingDate = DateTime.UtcNow;
                break;

            case StateOrder.Shipped:
                if (request.ShipperId == null && isShipper)
                {
                    request.ShipperId = currentUserId;
                }

                order.State = StateOrder.Shipped;
                order.ShippedDate = DateTime.UtcNow;
                order.ShipperId = request.ShipperId;
                order.ImageConfirmed = request.ImageConfirmed;
                break;

            case StateOrder.Delivered:
                order.State = StateOrder.Delivered;
                order.DeliveredDate = DateTime.UtcNow;
                order.Payment.Status = PaymentStatus.Completed;
                break;

            case StateOrder.Receivered:
                order.State = StateOrder.Delivered;
                break;

            case StateOrder.Sendered:
                order.State = StateOrder.Sendered;
                break;
        }

        // Cập nhật trạng thái cho từng order item
        foreach (var item in order.OrderItems)
        {
            item.State = order.State;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task HandleCancelOrder(Order order, string? reason, bool isAdmin, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(reason) && !isAdmin)
        {
            throw new ApplicationException("Vui lòng cung cấp lý do hủy đơn hàng.");
        }

        order.State = StateOrder.Canceled;
        order.ReasonCancel = reason;
        order.CanceledDate = DateTime.UtcNow;

        // Hoàn trả số lượng tồn kho nếu đơn hàng đã được xác nhận
        if (order.ConfirmedDate.HasValue)
        {
            var orderItems = await _dbContext.OrderItems
                .Include(oi => oi.ProductVariant)
                .Where(oi => oi.OrderId == order.Id)
                .ToListAsync(cancellationToken);

            foreach (var item in orderItems)
            {
                item.ProductVariant.StockQuantity += item.Quantity;
            }
        }

        // Cập nhật trạng thái thanh toán nếu có
        if (order.Payment != null)
        {
            order.Payment.Status = PaymentStatus.Refunded;
        }
    }

    private void ValidateStatusTransition(StateOrder currentStatus, StateOrder newStatus, bool isAdmin, bool isShipper)
    {
        var validTransitions = new Dictionary<StateOrder, List<StateOrder>>()
        {
            [StateOrder.Pending] = new() { StateOrder.Confirmed, StateOrder.Canceled, StateOrder.Processing },
            [StateOrder.Confirmed] = new() { StateOrder.Processing, StateOrder.Canceled },
            [StateOrder.Processing] = new() { StateOrder.Confirmed, StateOrder.Shipped },
            [StateOrder.Shipped] = new() { StateOrder.Delivered, StateOrder.Canceled, StateOrder.Returned }, // Có thể hủy hoặc hoàn hàng
            [StateOrder.Delivered] = new() { StateOrder.Returned }, // Khách trả hàng
            [StateOrder.Canceled] = new() { }, // End state
            [StateOrder.Returned] = new() { StateOrder.Refunded }, // Sau khi hoàn hàng → hoàn tiền
            [StateOrder.Refunded] = new() { }, // End state
            [StateOrder.Receivered] = new() { StateOrder.Pending },
            [StateOrder.Sendered] = new() { StateOrder.Pending }
        };

        // Shipper chỉ có thể cập nhật từ Shipped -> Delivered
        if (isShipper && !(currentStatus == StateOrder.Shipped && newStatus == StateOrder.Delivered))
        {
            throw new ApplicationException("Shipper chỉ có thể cập nhật trạng thái thành Đã giao hàng.");
        }

        // Admin có thể force một số trạng thái
        if (isAdmin && newStatus == StateOrder.Canceled)
        {
            return; // Admin có thể hủy từ bất kỳ trạng thái nào
        }

        if (!validTransitions[currentStatus].Contains(newStatus))
        {
            throw new ApplicationException($"Không thể chuyển từ trạng thái {currentStatus} sang {newStatus}.");
        }
    }
}