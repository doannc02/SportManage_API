using FirebaseAdmin.Messaging;
using MediatR;
using Microsoft.EntityFrameworkCore; 
using SportManager.Application.Abstractions;
using SportManager.Application.Common.Interfaces;

namespace SportManager.Infrastructure.Services;

public class FirebasePushNotificationService : IPushNotificationService
{
    private readonly FirebaseMessaging _firebaseMessaging;
    private readonly IApplicationDbContext _dbContext; 

    public FirebasePushNotificationService(FirebaseMessaging firebaseMessaging, IApplicationDbContext dbContext)
    {
        _firebaseMessaging = firebaseMessaging;
        _dbContext = dbContext;
    }

    public async Task SendNotificationToUserAsync(string userId, string title, string body, Dictionary<string, string>? data = null)
    {
        // Bước 1: Lấy FCM token của người dùng từ cơ sở dữ liệu  
        var user = await _dbContext.Users
                       .FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));

        if (user == null || string.IsNullOrEmpty(user.FcmToken)) 
        {
            Console.WriteLine($"Người dùng {userId} không có FCM token hoặc không tồn tại.");
            return;
        }

        await SendNotificationToDeviceAsync(user.FcmToken, title, body, data);
    }

    public async Task SendNotificationToDeviceAsync(string fcmToken, string title, string body, Dictionary<string, string>? data = null)
    {
        var message = new FirebaseAdmin.Messaging.Message()
        {
            Notification = new Notification
            {
                Title = title,
                Body = body
            },
            Data = data, 
            Token = fcmToken,
        };

        try
        {
            string response = await _firebaseMessaging.SendAsync(message);
            Console.WriteLine($"Đã gửi thành công thông báo: {response}");
        }
        catch (FirebaseMessagingException ex)
        {
            if (ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
            {
                Console.WriteLine($"FCM Token {fcmToken} không còn hợp lệ. Cần xóa khỏi DB.");
                // TODO: Xóa token này khỏi cơ sở dữ liệu của bạn  
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi không xác định khi gửi thông báo: {ex.Message}");
        }
    }
}
