﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportManager.Application.Common.Models;
using SportManager.Application.Orders.Models;
using SportManager.Application.Orders.Queries;
using SportManager.Domain.Entity;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;

namespace SportManager.API.Controllers.v1;

[Route("api/orders")]
[ApiController]
public class OrdersController : ApiControllerBase
{
    //[Authorize(Policy = "ADMIN")]
    [HttpPost]
    public async Task<Guid> Create(PlaceOrderCommand input, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(input, cancellationToken);
        //  await DbContextTransaction.CommitAsync(cancellationToken);
        return result;
    }

    //[Authorize(Policy = "Admin")]
    [AllowAnonymous]
    [HttpGet("admin-paging")]
    public async Task<PageResult<OrderDto>> GetOrders(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyWord = null,
        [FromQuery] StateOrder? state = null,
        [FromQuery] string? customerId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var query = new GetOrdersWithPaginationQuery
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            SearchTerm = keyWord,
            State = state,
            FromDate = fromDate,
            ToDate = toDate,
            CustomerId = customerId,
        };

        var result = await Mediator.Send(query);
        return result;
    }

    [HttpGet("user-paging")]
    public async Task<PageResult<OrderDto>> GetCustomerOrders(
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 10,
    [FromQuery] string? keyWord = null,
    [FromQuery] StateOrder? state = null,
    [FromQuery] DateTime? fromDate = null,
    [FromQuery] DateTime? toDate = null)
    {
        var query = new GetCustomerOrdersQuery
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            State = state,
            SearchTerm = keyWord,
            FromDate = fromDate,
            ToDate = toDate,
        };

        var result = await Mediator.Send(query);
        return result;
    }

    [HttpGet("{id:guid}")]
    public async Task<OrderDto> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
        => await Mediator.Send(new GetOrderByIdQuery(id), cancellationToken);

    ////[Authorize(Policy = "ADMIN")]
    //[HttpPut]
    //public async Task<UpdateCommandResponse> Update(UpdateOrderCommand input, CancellationToken cancellationToken)
    //{
    //    var result = await Mediator.Send(input, cancellationToken);
    //    //  await DbContextTransaction.CommitAsync(cancellationToken);
    //    return result;
    //}
    ////[Authorize(Policy = "ADMIN")]
    //[HttpDelete("{id:guid}")]
    //public async Task<int> Delete([FromRoute] Guid id, CancellationToken cancellationToken)
    //{
    //    var result = await Mediator.Send(new DeleteOrderCommand(id), cancellationToken);
    //    //  await DbContextTransaction.CommitAsync(cancellationToken);
    //    return result;
    //}
    ////[Authorize(Policy = "ADMIN")]


    //[HttpGet("paging")]
    //public async Task<PageResult<OrderPageResponse>> Get(
    //    [FromQuery] GetsPagingQuery request,
    //    CancellationToken cancellationToken)
    //    => await Mediator.Send(request, cancellationToken);
}
