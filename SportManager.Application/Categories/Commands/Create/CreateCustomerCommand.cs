using SportManager.Application.Abstractions;
using SportManager.Application.Categroies.Models;
using SportManager.Application.Common.Exception;
using SportManager.Application.Common.Interfaces;
using SportManager.Domain.Entity;

namespace SportManager.Application.Categories.Commands.Create;

public class CreateCategoryCommand : CategoryDto, IRequest<CreateCategoryResponse>
{
}

public class CreateCategoryResponse
{
    public Guid Id { get; set; }
}
public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CreateCategoryResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuthService _passwordHasher;

    public CreateCategoryCommandHandler(IApplicationDbContext dbContext, IAuthService passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<CreateCategoryResponse> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existingUser = await _dbContext.Categories
            .Where(u => u.Name.ToLower() == request.Name.ToLower())
            .Select(u => new { u.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (existingUser != null)
        {
            if (existingUser.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase))
                throw new ApplicationException("DUPLICATE_NAME");

        }

  
        var user = new Domain.Entity.Category
        {
           Name = request.Name,
           Description = request.Description,
           Logo = request.Logo,
        };

        _dbContext.Categories.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new CreateCategoryResponse { Id = user.Id };

    }
}


