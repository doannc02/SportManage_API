using SportManager.Application.Abstractions;
using SportManager.Application.Common.Exception;
using SportManager.Application.Common.Interfaces;
using SportManager.Application.Customer.Models;
using SportManager.Domain.Entity;

namespace SportManager.Application.Customer.Commands.Update;

public class UpdateCustomerCommand : CustomerDto, IRequest<UpdateCustomerResponse>
{
    public required Guid Id { get; set; }
    public string? ConfirmPassWord { get; set; } = null;
    public void Normalize()
    {
        Address = Address.Trim();
        Phone = Phone.Trim();
    }
}

public record UpdateCustomerResponse
{
    public Guid Id { get; set; }
}

public class UpdateCustomerValidator : CustomerValidatorBase<UpdateCustomerCommand>
{
    public UpdateCustomerValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(Guid.Empty)
            .WithMessage("CUSTOMER_ID_REQUIRED");

        ValidateFormatAndLength();
        ValidateShippingAddresses(true);
    }
}

public class UpdateCustomerCommandHandler(IApplicationDbContext applicationDbContext, IAuthService _passwordHasher)
    : IRequestHandler<UpdateCustomerCommand, UpdateCustomerResponse>
{
    public async Task<UpdateCustomerResponse> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        request.Normalize();

        var customer = await applicationDbContext.Customers
            .Include(c => c.ShippingAddresses)
            .Include(c => c.User)
            .Where(x => x.Id == request.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            throw new ApplicationException(ErrorCode.NOT_FOUND, ErrorCode.NOT_FOUND);
        }

        // Cập nhật thông tin customer
        customer.Address = request.Address;
        customer.Age = request.Age;
        customer.Gender = (Gender)request.Gender;
        //customer.Phone = request.Phone;
        customer.User.Avatar = request?.AvatarUrl;
        customer.User.Email = request.Email;
        customer.User.Username = request.UserName;

        if (request.Password == request.ConfirmPassWord)
        {
            customer.User.PasswordHash = _passwordHasher.HashPassword(request.Password, request.UserName);
        }
        // Xử lý ShippingAddresses
        if (request.ShippingAddresses is not null)
        {
            var inputMap = request.ShippingAddresses
                .Where(x => x.Id != null && x.Id != Guid.Empty)
                .ToDictionary(x => x.Id!.Value);

            var existingMap = customer.ShippingAddresses.ToDictionary(x => x.Id);

            var updatedAddresses = request.ShippingAddresses.Select(input =>
            {
                if (input.Id != null && input.Id != Guid.Empty && existingMap.TryGetValue(input.Id.Value, out var existing))
                {
                    return MapShippingAddress(input, existing);
                }

                return MapShippingAddress(input);
            }).ToList();

            // Tìm và xoá các address bị remove
            var removedIds = existingMap.Keys.Except(inputMap.Keys).ToList();
            if (removedIds.Any())
            {
                var addressesToRemove = customer.ShippingAddresses
                    .Where(x => removedIds.Contains(x.Id))
                    .ToList();

                applicationDbContext.ShippingAddresses.RemoveRange(addressesToRemove);
            }

            customer.ShippingAddresses = updatedAddresses;
        }

        applicationDbContext.Customers.Update(customer);
        await applicationDbContext.SaveChangesAsync(cancellationToken);

        return new UpdateCustomerResponse { Id = customer.Id };
    }

    //private async Task<bool> IsEmailOrUsernameExist(UpdateCustomerCommand request, CancellationToken cancellationToken)
    //{
    //    var normalizedEmail = request.Email.Trim().ToLowerInvariant();
    //    var normalizedUsername = request.UserName.Trim().ToLowerInvariant();

    //    return await applicationDbContext.Customers
    //        .AnyAsync(x => x.Id != request.Id &&
    //                       (x.User.Email.Trim().ToLowerInvariant() == normalizedEmail ||
    //                        x.User.Username.Trim().ToLowerInvariant() == normalizedUsername),
    //            cancellationToken);
    //}

    private static ShippingAddress MapShippingAddress(ShippingAddressView dto, ShippingAddress? existing = null)
    {
        var address = existing ?? new ShippingAddress();

        address.AddressLine = dto.AddressLine;
        address.Ward = dto.Ward;
        address.City = dto.City;
        address.CityId = dto.CityId;
        address.Country = dto.Country;
        address.CountryId = dto.CountryId;
        address.District = dto.District;
        address.Phone = dto.Phone?.Trim();
        address.PostalCode = dto.PostalCode;
        address.RecipientName = dto.RecipientName;
        address.IsDefault = dto.IsDefault;

        return address;
    }
}
