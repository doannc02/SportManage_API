using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SportManager.API.Controllers;
using SportManager.Application.Abstractions;
using SportManager.Application.ChatHubs.Models;
using SportManager.Application.Common.Interfaces;
using SportManager.Domain.Entity;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ApiControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _userService;
    private readonly IHubContext<ChatHub> _hubContext;
    public UsersController(IApplicationDbContext dbContext, ICurrentUserService userService, IHubContext<ChatHub> hubContext)
    {
        _dbContext = dbContext;
        _userService = userService;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _dbContext.Users
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Email
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentUser()
    {
        if (!Guid.TryParse(_userService.UserId, out var userId))
            return BadRequest("Invalid user ID");

        var user = await _dbContext.Users
            .Where(x => x.Id.Equals(userId))
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Email,
                Roles = u.UserRoles.Select(k => new 
                {
                    RoleName = k.Role.Name  
                }).ToList()  
            })
            .FirstOrDefaultAsync();  

        return Ok(user);
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        if (!Guid.TryParse(_userService.UserId, out var userId))
            return BadRequest("Invalid user ID");

        var conversations = await _dbContext.Conversations
            .Where(c => c.Participants.Any(p => p.UserId == userId))
            .Select(c => new
            {
                c.Id,
                c.Title,
                Participants = c.Participants.Select(p => new
                {
                    p.UserId,
                    p.User.Username
                }),
                LastMessage = c.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => new
                    {
                        m.Content,
                        m.MessageType,
                        m.CreatedAt
                    })
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(conversations);
    }

    [HttpGet("conversations/{conversationId}/messages")] // Fixed route
    public async Task<IActionResult> GetConversationMessages(Guid conversationId)
    {
        var result = await _dbContext.Messages
            .Where(m => m.ConversationId == conversationId)
            .Select(a => new
            {
                a.Id,
                a.SenderId,
                a.Content,
                a.MessageType,
            }).ToListAsync();
        return Ok(result);
    }

    [HttpPost("conversations")]
    public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request)
    {
        if (!Guid.TryParse(_userService.UserId, out var currentUserId))
            return BadRequest("Invalid current user ID");

        if (request.Participants == null || request.Participants.Count < 1)
            return BadRequest("A conversation must have at least one other participant.");

        // Tìm admin đầu tiên trong hệ thống
        var idAdmin = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Where(u => u.UserRoles.Any(ur => ur.Role.Name.ToLower() == "admin"))
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        if (idAdmin == Guid.Empty)
            return BadRequest("No admin user found.");

        // Thêm idAdmin nếu chưa có trong danh sách participants
        var allParticipants = request.Participants.ToList();
        if (!allParticipants.Contains(idAdmin))
            allParticipants.Add(idAdmin);

        // Kiểm tra xem đã tồn tại cuộc trò chuyện giữa các user đó chưa
        var existing = await _dbContext.Conversations
            .Include(c => c.Participants)
            .ToListAsync();

        var matchedConversation = existing.FirstOrDefault(c =>
            c.Participants.Select(p => p.UserId).OrderBy(id => id).SequenceEqual(allParticipants.OrderBy(id => id)));

        if (matchedConversation != null)
        {
            return Ok(new
            {
                Message = "Conversation already exists",
                Conversation = new
                {
                    matchedConversation.Id,
                    matchedConversation.Title
                }
            });
        }

        // Tạo mới conversation
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = request.Title ?? "New Conversation",
            CreatedAt = DateTime.UtcNow,
            Participants = allParticipants.Select(id => new ConversationParticipant
            {
                UserId = id
            }).ToList()
        };

        _dbContext.Conversations.Add(conversation);
        await _dbContext.SaveChangesAsync();
        // Gửi SignalR event cho các participant
        await _hubContext.Clients
            .Groups(allParticipants.Select(id => id.ToString()))
            .SendAsync("ConversationCreated", new
            {
                conversation.Id,
                conversation.Title,
                Participants = conversation.Participants.Select(p => new { p.UserId })
            });

        return Ok(new
        {
            Message = "Conversation created",
            Conversation = new
            {
                conversation.Id,
                conversation.Title,
                Participants = conversation.Participants.Select(p => new
                {
                    p.UserId
                })
            }
        });
    }

    public class SaveFcmTokenRequest
    {
        public Guid UserId { get; set; }
        public string FcmToken { get; set; } = null!;
    }

    [HttpPost("save-fcm-token")]
    public async Task<IActionResult> SaveFcmToken([FromBody] SaveFcmTokenRequest request)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);

        if (user == null)
        {
            return NotFound("Người dùng không tồn tại.");
        }

        user.FcmToken = request.FcmToken;
        await _dbContext.SaveChangesAsync();

        return Ok("FCM Token đã được lưu thành công.");
    }

}

