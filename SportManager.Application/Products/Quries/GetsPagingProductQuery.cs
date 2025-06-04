using SportManager.Application.Common.Interfaces;
using SportManager.Application.Products.Models;
using SportManager.Application.Utilities;

namespace SportManager.Application.Products.Quries;
public class ProductPageResponse : ProductDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<ProductReviewView>? Reviews { get; set; } = [];
    public SupplierView Supplier { get; set; }
    public BrandView? Brand { get; set;  }
}

public record SupplierView
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Phone { get; set; }
    public string Email { get; set; }
    public string Address { get; set; }
}
public record  BrandView
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Logo { get; set; }
}
public class GetsPagingQuery : IRequest<PageResult<ProductPageResponse>>
{
    public string? Keyword { get; set; }
    public int PageNumber { get; set; } = 0;
    public int PageSize { get; set; } = 20;
}
public class GetsPagingQueryHandler(IReadOnlyApplicationDbContext dbContext)
    : IRequestHandler<GetsPagingQuery, PageResult<ProductPageResponse>>
{
    public async Task<PageResult<ProductPageResponse>> Handle(GetsPagingQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keyword = request.Keyword?.Trim();
        // Using AsNoTracking for better performance in read-only queries
        var query = dbContext.Products
            .WhereIf(!string.IsNullOrEmpty(keyword), product =>
                EF.Functions.ILike(product.Name, $"%{keyword!}%"))
            .Include(x => x.ProductCategories)
                .ThenInclude(x => x.Category)
            .Include(x => x.Variants)
            .Include(x => x.Reviews)
            .Include(x => x.Supplier)
            .Include(x => x.Brand)
            .Select(product => new ProductPageResponse
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                BrandId = product.BrandId,
                SupplierId = product.SupplierId,
                CreatedAt = product.CreatedAt,
                // Handle Images conversion properly
                Images = product.Images ?? Array.Empty<string>(),
                // Map product categories
                ProductCategories = product.ProductCategories.Select(pc => new ProductCategoryReqDto
                {
                    Id = pc.CategoryId,
                    Category = new CategoryReqdDto
                    {
                        Id = pc.Category.Id,
                        Name = pc.Category.Name,
                        Description = pc.Category.Description,
                        Logo = pc.Category.Logo
                    }
                }).ToList(),
                Brand = product.Brand != null ?
                new BrandView { 
                    Id = product.Brand.Id,
                    Logo = product.Brand.LogoUrl,
                    Name = product.Brand.Name
                } : null,
                Supplier =  new SupplierView { 
                    Id = product.Supplier.Id,
                    Name = product.Supplier.Name,
                    Address = product.Supplier.Address,
                    Email = product.Supplier.ContactEmail,
                    Phone = product.Supplier.Phone
                },
                // map review
                Reviews = product.Reviews.Select(rv => new ProductReviewView
                {
                    ProductId = rv.ProductId,
                    CustomerId = rv.CustomerId,
                    Comment = rv.Comment,
                    UserName = rv.Customer.User.Username,
                    Rating = rv.Rating
                }).ToList(),
                // Map variants
                Variants = product.Variants.Select(v => new ProdVariantReq
                {
                    Id = v.Id,
                    Name = v.Name,
                    Color = v.Color,
                    SKU = v.SKU,
                    Unit = v.Unit,
                    Description = v.Description,
                    Size = v.Size,
                    Price = v.Price,
                    StockQuantity = v.StockQuantity,
                    // Fix variant Images conversion
                    Images = v.Images ?? Array.Empty<string>(),
                    // Fix Attribute conversion
                    Attribute = v.Attribute ?? Array.Empty<string>()
                }).ToList()
            })
            .OrderByDescending(product => product.CreatedAt);
        return await PageResult<ProductPageResponse>.CreateAsync(query, request.PageNumber, request.PageSize, cancellationToken);
    }
}