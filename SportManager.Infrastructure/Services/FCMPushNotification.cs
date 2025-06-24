using FirebaseAdmin.Messaging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SportManager.Application.Abstractions;
using SportManager.Application.Common.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

    /// <summary>
    /// Gửi thông báo đến tất cả các thiết bị đã đăng ký của một người dùng cụ thể.
    /// </summary>
    /// <param name="userId">ID của người dùng.</param>
    /// <param name="title">Tiêu đề thông báo.</param>
    /// <param name="body">Nội dung thông báo.</param>
    /// <param name="data">Dữ liệu tùy chỉnh kèm theo thông báo.</param>
    public async Task SendNotificationToUserAsync(string userId, string title, string body, Dictionary<string, string>? data = null)
    {
        // Bước 1: Lấy thông tin người dùng bao gồm mảng FCM token từ cơ sở dữ liệu
        var user = await _dbContext.Users
                                    .FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));

        if (user == null)
        {
            Console.WriteLine($"Người dùng {userId} không tồn tại.");
            return;
        }

        // Nếu FcmTokens là null, khởi tạo một danh sách rỗng để tránh lỗi NullReference
        var fcmTokens = user.FcmTokens?.ToList() ?? new List<string>();

        if (!fcmTokens.Any()) // Kiểm tra nếu danh sách token rỗng
        {
            Console.WriteLine($"Người dùng {userId} không có FCM token nào được đăng ký.");
            return;
        }

        Console.WriteLine($"Người dùng {userId} có {fcmTokens.Count} FCM token. Đang gửi thông báo...");

        var invalidTokens = new List<string>();

        foreach (var token in fcmTokens)
        {
            if (string.IsNullOrEmpty(token))
            {
                continue; 
            }

            var message = new FirebaseAdmin.Messaging.Message()
            {
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data,
                Token = token,
            };

            try
            {
                string response = await _firebaseMessaging.SendAsync(message);
                Console.WriteLine($"Đã gửi thành công thông báo tới token {token}: {response}");
            }
            catch (FirebaseMessagingException ex)
            {
                if (ex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                    ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
                {
                    Console.WriteLine($"FCM Token {token} không còn hợp lệ hoặc sai định dạng. Cần xóa khỏi DB.");
                    invalidTokens.Add(token); // Đánh dấu để xóa sau
                }
                else
                {
                    Console.WriteLine($"Lỗi Firebase Messaging không xác định khi gửi tới token {token}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi không xác định khi gửi thông báo tới token {token}: {ex.Message}");
            }
        }

        // Bước 2: Xóa các token không hợp lệ khỏi cơ sở dữ liệu và cập nhật mảng FcmTokens của người dùng
        if (invalidTokens.Any())
        {
            Console.WriteLine($"Đang xóa {invalidTokens.Count} token không hợp lệ khỏi DB cho người dùng {userId}.");

            // Lọc ra các token hợp lệ
            var validTokens = fcmTokens.Except(invalidTokens).ToList();

            // Cập nhật lại mảng FcmTokens của người dùng
            user.FcmTokens = validTokens.ToArray(); // Chuyển lại thành mảng string[]

            _dbContext.Users.Update(user); // Đánh dấu User là đã thay đổi
            await _dbContext.SaveChangesAsync(); // Lưu thay đổi vào DB
            Console.WriteLine($"Đã cập nhật FcmTokens của người dùng {userId} sau khi xóa các token không hợp lệ.");
        }
    }

    // Phương thức này vẫn có thể giữ nguyên nếu bạn cần gửi đến một token cụ thể
    // Tuy nhiên, logic gửi tới người dùng sẽ gọi phương thức trên
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
            if (ex.MessagingErrorCode == MessagingErrorCode.Unregistered || ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
            {
                Console.WriteLine($"FCM Token {fcmToken} không còn hợp lệ hoặc sai định dạng. Cần xử lý việc xóa khỏi DB.");
                // TODO: Logic để xóa token này khỏi cơ sở dữ liệu của bạn nếu cần.
                // Lưu ý: Với cách bạn đang lưu token (mảng trong User),
                // việc xóa token nên được thực hiện bởi SendNotificationToUserAsync
                // để đảm bảo tính nhất quán của mảng FcmTokens.
            }
            else
            {
                Console.WriteLine($"Lỗi Firebase Messaging: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi không xác định khi gửi thông báo: {ex.Message}");
        }
    }
}
