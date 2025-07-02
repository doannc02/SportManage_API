using SportManager.Application.Abstractions;
using SportManager.Application.Common.Exception;
using SportManager.Application.Common.Interfaces;
using SportManager.Application.Customer.Models; // Assuming CustomerDto and ShippingAddressView are here
using SportManager.Domain.Entity;
using Microsoft.EntityFrameworkCore; // For FirstOrDefaultAsync in handler
using FluentValidation; // For validator

namespace SportManager.Application.Customer.Commands.Update;

// Assuming CustomerDto looks something like this (for context of validation below):
// public class CustomerDto
// {
//     public string? UserName { get; set; }
//     public string? Email { get; set; }
//     public string? Password { get; set; }
//     public string? Address { get; set; }
//     public string? Phone { get; set; }
//     public int? Age { get; set; }
//     public int? Gender { get; set; } // Assuming int for enum, validate in handler or validator
//     public string? AvatarUrl { get; set; }
//     public List<ShippingAddressView>? ShippingAddresses { get; set; }
// }

// Assuming ShippingAddressView looks something like this:
// public class ShippingAddressView
// {
//     public Guid? Id { get; set; } // Null for new addresses
//     public string? RecipientName { get; set; }
//     public string? Phone { get; set; }
//     public string? AddressLine { get; set; }
//     public string? Ward { get; set; }
//     public string? District { get; set; }
//     public string? City { get; set; }
//     public int? CityId { get; set; }
//     public string? Country { get; set; }
//     public int? CountryId { get; set; }
//     public string? PostalCode { get; set; }
//     public bool IsDefault { get; set; }
// }


public class UpdateCustomerCommand : CustomerDto, IRequest<UpdateCustomerResponse>
{
    public required Guid Id { get; set; } // This is distinct from any ID in CustomerDto
    public string? ConfirmPassWord { get; set; } = null;

    // Normalize input strings to handle nulls gracefully
    public void Normalize()
    {
        UserName = UserName?.Trim();
        Email = Email?.Trim();
        Address = Address?.Trim();
        Phone = Phone?.Trim();

        // Normalize shipping addresses as well
        if (ShippingAddresses != null)
        {
            foreach (var sa in ShippingAddresses)
            {
                sa.RecipientName = sa.RecipientName?.Trim();
                sa.Phone = sa.Phone?.Trim();
                sa.AddressLine = sa.AddressLine?.Trim();
                sa.Ward = sa.Ward?.Trim();
                sa.District = sa.District?.Trim();
                sa.City = sa.City?.Trim();
                sa.Country = sa.Country?.Trim();
                sa.PostalCode = sa.PostalCode?.Trim();
            }
        }
    }
}

public record UpdateCustomerResponse
{
    public Guid Id { get; set; }
}

// 2. UpdateCustomerValidator (Updated)
public class UpdateCustomerValidator : CustomerValidatorBase<UpdateCustomerCommand>
{
    public UpdateCustomerValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(Guid.Empty)
            .WithMessage("CUSTOMER_ID_REQUIRED");

        // Validate basic format and length (from base class)
        // Ensure CustomerDto properties are validated, e.g.:
        // RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("INVALID_EMAIL");
        // RuleFor(x => x.UserName).NotEmpty().MinimumLength(3).WithMessage("USERNAME_TOO_SHORT");
        // RuleFor(x => x.Phone).NotEmpty().Matches(@"^0\d{9,10}$").WithMessage("INVALID_PHONE_FORMAT");
        // RuleFor(x => x.Address).NotEmpty().WithMessage("ADDRESS_REQUIRED");
        // RuleFor(x => x.Age).InclusiveBetween(1, 120).WithMessage("INVALID_AGE_RANGE");
        // RuleFor(x => x.Gender).IsInEnum().WithMessage("INVALID_GENDER");

        ValidateFormatAndLength(); // Assuming this method covers Email, UserName, Phone, Address, Age, Gender format and length.
                                   // If it doesn't, add explicit rules here for any missing fields.

        // Conditional password validation: only if either password field is provided
        When(x => !string.IsNullOrEmpty(x.Password) || !string.IsNullOrEmpty(x.ConfirmPassWord), () =>
        {
            RuleFor(x => x.Password)
                .NotEmpty()
                .WithMessage("PASSWORD_REQUIRED_WHEN_UPDATING")
                .MinimumLength(6) // Example: minimum 6 characters
                .WithMessage("PASSWORD_MIN_LENGTH_6")
                .MaximumLength(50) // Example: maximum 50 characters
                .WithMessage("PASSWORD_MAX_LENGTH_50");

            RuleFor(x => x.ConfirmPassWord)
                .NotEmpty()
                .WithMessage("CONFIRM_PASSWORD_REQUIRED")
                .Equal(x => x.Password)
                .WithMessage("PASSWORD_AND_CONFIRM_PASSWORD_MISMATCH");
        });

        // Nested validation for ShippingAddresses
        // This ensures that if ShippingAddresses list is provided, its items are valid.
        RuleFor(x => x.ShippingAddresses)
            .NotNull() // If ShippingAddresses field is included in the request, it should not be null itself (the list)
            .When(x => x.ShippingAddresses != null) // Only apply rules if the list is present
            .ForEach(address => // Apply rules to each item in the list
            {
                address.ChildRules(sa => // sa is a ShippingAddressView
                {
                    sa.RuleFor(s => s.RecipientName)
                        .NotEmpty()
                        .WithMessage("SHIPPING_ADDRESS_RECIPIENT_NAME_REQUIRED")
                        .MaximumLength(100)
                        .WithMessage("SHIPPING_ADDRESS_RECIPIENT_NAME_TOO_LONG");

                    sa.RuleFor(s => s.Phone)
                        .NotEmpty()
                        .WithMessage("SHIPPING_ADDRESS_PHONE_REQUIRED")
                        .Matches(@"^0\d{9,10}$") // Example: Vietnamese phone number format
                        .WithMessage("SHIPPING_ADDRESS_INVALID_PHONE_FORMAT");

                    sa.RuleFor(s => s.AddressLine)
                        .NotEmpty()
                        .WithMessage("SHIPPING_ADDRESS_LINE_REQUIRED")
                        .MaximumLength(200)
                        .WithMessage("SHIPPING_ADDRESS_LINE_TOO_LONG");

                    sa.RuleFor(s => s.Ward)
                        .NotEmpty()
                        .WithMessage("SHIPPING_ADDRESS_WARD_REQUIRED")
                        .MaximumLength(100)
                        .WithMessage("SHIPPING_ADDRESS_WARD_TOO_LONG");

                    sa.RuleFor(s => s.District)
                        .NotEmpty()
                        .WithMessage("SHIPPING_ADDRESS_DISTRICT_REQUIRED")
                        .MaximumLength(100)
                        .WithMessage("SHIPPING_ADDRESS_DISTRICT_TOO_LONG");

                    sa.RuleFor(s => s.City)
                        .NotEmpty()
                        .WithMessage("SHIPPING_ADDRESS_CITY_REQUIRED")
                        .MaximumLength(100)
                        .WithMessage("SHIPPING_ADDRESS_CITY_TOO_LONG");

                    sa.RuleFor(s => s.Country)
                        .NotEmpty()
                        .WithMessage("SHIPPING_ADDRESS_COUNTRY_REQUIRED")
                        .MaximumLength(100)
                        .WithMessage("SHIPPING_ADDRESS_COUNTRY_TOO_LONG");

                });
            });

        ValidateShippingAddresses(true); // Assuming this is for overall shipping addresses logic (e.g., max count, one default)
    }
}

// 3. UpdateCustomerCommandHandler (Updated)
public class UpdateCustomerCommandHandler(IApplicationDbContext applicationDbContext, IAuthService _passwordHasher)
    : IRequestHandler<UpdateCustomerCommand, UpdateCustomerResponse>
{
    public async Task<UpdateCustomerResponse> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        request.Normalize(); // Ensure all string inputs are trimmed and nulls handled

        var customer = await applicationDbContext.Customers
            .Include(c => c.ShippingAddresses)
            .Include(c => c.User)
            .Where(x => x.Id == request.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            throw new ApplicationException(ErrorCode.NOT_FOUND, "Customer not found.");
        }

        // Validate customer.User exists, as properties like Email, Username, Avatar are updated on it.
        if (customer.User is null)
        {
            // This scenario implies a data integrity issue where a customer exists without a linked user.
            // You might decide to create a new User here if this is a valid flow,
            // but generally, a customer should always have a user.
            throw new ApplicationException(ErrorCode.INVALID_DATA, "Customer's associated user object is missing. Data integrity error.");
        }

        // Cập nhật thông tin customer
        customer.Address = request.Address;
        customer.Age = request.Age;

        // Safely cast Gender (assuming request.Gender is int? and Domain.Entity.Gender is an enum)
        if (request.Gender.HasValue)
        {
            if (Enum.IsDefined(typeof(Gender), request.Gender.Value))
            {
                customer.Gender = (Gender)request.Gender.Value;
            }
            else
            {
                throw new ApplicationException(ErrorCode.INVALID_DATA, $"Invalid Gender value: {request.Gender.Value}.");
            }
        }
        // else if request.Gender is null, the existing Gender value on 'customer' remains unchanged.

        customer.Phone = request.Phone; // Uncommented this line if you intend to update phone

        // User profile updates
        customer.User.Avatar = request.AvatarUrl; // Can be null
        customer.User.Email = request.Email;     // Normalized (trimmed or null)
        customer.User.Username = request.UserName; // Normalized (trimmed or null)

        // --- Password Update Logic ---
        // This block executes if password fields are provided and valid per validator.
        if (!string.IsNullOrEmpty(request.Password) && request.Password == request.ConfirmPassWord)
        {
            // Ensure username is not null/empty for hashing
            if (string.IsNullOrEmpty(request.UserName))
            {
                throw new ApplicationException(ErrorCode.INVALID_DATA, "Username is required to hash the password.");
            }

            // Ensure _passwordHasher dependency is resolved (should be by DI, but defensive check)
            if (_passwordHasher is null)
            {
                throw new ApplicationException(ErrorCode.INVALID_DATA, "Password hasher service is not initialized.");
            }

            // This is the call that previously caused NRE at AuthService.cs:line 72
            // Now, request.Password and request.UserName are guaranteed to be non-null/non-empty strings here.
            customer.User.PasswordHash = _passwordHasher.HashPassword(request.Password, request.UserName);
        }
        // No 'else if' for mismatch needed here, as the validator should catch it beforehand.
        // If the validator is skipped (e.g., direct call without validation),
        // and a mismatch occurs, the 'if' condition `request.Password == request.ConfirmPassWord` would simply be false,
        // and the password update would be skipped. It's generally better to let the validator handle this as an error.


        // --- ShippingAddresses Processing ---
        // Handle only if ShippingAddresses list is provided in the request
        if (request.ShippingAddresses is not null)
        {
            var inputMap = request.ShippingAddresses
                .Where(x => x.Id.HasValue && x.Id != Guid.Empty) // Filter for items with valid IDs for updating
                .ToDictionary(x => x.Id!.Value);

            var existingMap = customer.ShippingAddresses.ToDictionary(x => x.Id);

            var updatedAddresses = new List<ShippingAddress>();

            foreach (var inputDto in request.ShippingAddresses)
            {
                if (inputDto == null)
                {
                    // Log or throw if a null item is found in the list, though validation should prevent this.
                    continue; // Skip null DTOs
                }

                ShippingAddress addressToUpdate;
                if (inputDto.Id.HasValue && inputDto.Id != Guid.Empty && existingMap.TryGetValue(inputDto.Id.Value, out var existingEntity))
                {
                    // Found existing address, update it
                    addressToUpdate = MapShippingAddress(inputDto, existingEntity);
                }
                else
                {
                    // New address, create a new entity
                    addressToUpdate = MapShippingAddress(inputDto);
                }
                updatedAddresses.Add(addressToUpdate);
            }

            // Identify and remove addresses that are no longer in the input
            var removedIds = existingMap.Keys.Except(inputMap.Keys).ToList();
            if (removedIds.Any())
            {
                var addressesToRemove = customer.ShippingAddresses
                    .Where(x => removedIds.Contains(x.Id))
                    .ToList();
                applicationDbContext.ShippingAddresses.RemoveRange(addressesToRemove);
            }

            // Update customer's ShippingAddresses collection
            // This handles adding new ones, updating existing ones, and maintaining the relationship
            customer.ShippingAddresses.Clear();
            foreach (var address in updatedAddresses)
            {
                customer.ShippingAddresses.Add(address);
            }
        }
        // else if request.ShippingAddresses is null, the existing collection on 'customer' remains unchanged.
        // This is the desired behavior for optional address updates.

        // Mark the customer entity as modified. EF Core will detect changes on included entities automatically.
        applicationDbContext.Customers.Update(customer);
        await applicationDbContext.SaveChangesAsync(cancellationToken);

        return new UpdateCustomerResponse { Id = customer.Id };
    }

    // private async Task<bool> IsEmailOrUsernameExist(UpdateCustomerCommand request, CancellationToken cancellationToken)
    // {
    // This helper method for checking existence should ideally be moved to a separate service
    // if it's used elsewhere, or kept here if it's only relevant to this command.
    // Ensure the validation for unique email/username is handled either here or in the validator.
    // If you uncomment this, make sure to integrate it before the save operation.
    // }

    private static ShippingAddress MapShippingAddress(ShippingAddressView dto, ShippingAddress? existing = null)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto), "Shipping address DTO cannot be null.");
        }

        var address = existing ?? new ShippingAddress();

        address.AddressLine = dto.AddressLine;
        address.Ward = dto.Ward;
        address.City = dto.City;
        address.CityId = dto.CityId;
        address.Country = dto.Country;
        address.CountryId = dto.CountryId;
        address.District = dto.District;
        address.Phone = dto.Phone?.Trim(); // Null-conditional operator for trimming
        address.PostalCode = dto.PostalCode?.Trim(); // Null-conditional operator for trimming
        address.RecipientName = dto.RecipientName;
        address.IsDefault = dto.IsDefault;

        return address;
    }
}