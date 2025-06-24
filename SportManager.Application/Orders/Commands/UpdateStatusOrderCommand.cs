using SportManager.Application.Abstractions;
using SportManager.Application.Common.Interfaces;
using SportManager.Domain.Entity;

namespace SportManager.Application.Orders.Commands;

public class UpdateOrderStatusCommand : IRequest<bool>
{
    public Guid OrderId { get; set; }
    public StateOrder NewStatus { get; set; }
    public string? Reason { get; set; } // Lý do hủy đơn hoặc từ chối
    public Guid? ShipperId { get; set; } 
    public string? ImageConfirmed { get; set; } // Hình ảnh xác nhận giao hàng
}

public class UpdateOrderStatusCommandHandler(
    IApplicationDbContext _dbContext,
    ICurrentUserService _currentUser,
    IPushNotificationService _pushNotificationService) 
    : IRequestHandler<UpdateOrderStatusCommand, bool>
{
    public async Task<bool> Handle(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
       
        var order = await _dbContext.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.Payment)
            .Include(o => o.Customer) 
                .ThenInclude(c => c.User) 
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null)
        {
            throw new ApplicationException("Đơn hàng không tồn tại.");
        }

        var currentUserId = Guid.Parse(_currentUser.UserId);
        var isAdmin = _currentUser.Roles.Contains("Admin");
        var isShipper = _currentUser.Roles.Contains("Shipper");

        ValidateStatusTransition(order.State, request.NewStatus, isAdmin, isShipper);

        StateOrder oldStatus = order.State; 

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

        foreach (var item in order.OrderItems)
        {
            item.State = order.State;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Gửi thông báo nếu trạng thái chuyển sang Canceled
        if (oldStatus != StateOrder.Canceled && request.NewStatus == StateOrder.Canceled)
        {
            if (order.CustomerId != Guid.Empty) 
            {
                var title = "Đơn hàng của bạn đã bị hủy!";
                var body = $"Mã đơn hàng: #{order.Id}. Lý do: {order.ReasonCancel ?? "Không có lý do cụ thể."}";
                var data = new Dictionary<string, string>
                {
                    { "orderId", order.Id.ToString() },
                    { "status", "canceled" }
                };
                await _pushNotificationService.SendNotificationToUserAsync(order.Customer.User.Id.ToString(), title, body, data);
            }
        }

        if(oldStatus == StateOrder.Canceled && request.NewStatus == StateOrder.RejectCancel)
        {
            if (order.CustomerId != Guid.Empty)
            {
                order.State = StateOrder.Shipped;
                var title = "Yêu cầu huỷ đơn hàng của bạn bị từ chối!";
                var body = $"Mã đơn hàng: #{order.Id}. Lý do yêu cầu: {order.ReasonCancel ?? "Không có lý do cụ thể."}";
                var data = new Dictionary<string, string>
                {
                    { "orderId", order.Id.ToString() },
                    { "status", "canceled" }
                };
                await _pushNotificationService.SendNotificationToUserAsync(order.Customer.User.Id.ToString(), title, body, data);
            }
        }

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

        var orderItems = await _dbContext.OrderItems
            .Include(oi => oi.ProductVariant)
            .Where(oi => oi.OrderId == order.Id)
            .ToListAsync(cancellationToken);

        foreach (var item in orderItems)
        {
            item.ProductVariant.StockQuantity += item.Quantity;
        }

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
            [StateOrder.Shipped] = new() { StateOrder.Delivered, StateOrder.Canceled, StateOrder.Returned },
            [StateOrder.Delivered] = new() { StateOrder.Returned },
            [StateOrder.Canceled] = new() { },
            [StateOrder.Returned] = new() { StateOrder.Refunded },
            [StateOrder.Refunded] = new() { },
            [StateOrder.Receivered] = new() { StateOrder.Pending },
            [StateOrder.Sendered] = new() { StateOrder.Pending }
        };

        if (isShipper && !(currentStatus == StateOrder.Shipped && newStatus == StateOrder.Delivered))
        {
            throw new ApplicationException("Shipper chỉ có thể cập nhật trạng thái thành Đã giao hàng.");
        }

        if (isAdmin && newStatus == StateOrder.Canceled)
        {
            return;
        }

        if (!validTransitions[currentStatus].Contains(newStatus))
        {
            throw new ApplicationException($"Không thể chuyển từ trạng thái {currentStatus} sang {newStatus}.");
        }
    }
}