using InventoryHold.Contracts.Requests;
using InventoryHold.Contracts.Responses;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace InventoryHold.WebApi.Controllers;

[ApiController]
[Route("api/holds")]
public sealed class HoldsController : ControllerBase
{
    private readonly IHoldService _holdService;

    public HoldsController(IHoldService holdService)
    {
        _holdService = holdService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(HoldResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateHoldRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var hold = await _holdService.CreateAsync(
                request.Items.Select(item =>
                    (item.ProductId.Trim(), item.Quantity)),
                cancellationToken);

            var response = ToResponse(hold);

            return CreatedAtAction(
                nameof(GetById),
                new { id = hold.Id },
                response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Product not found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Unable to create hold",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid hold request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(HoldResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        string id,
        CancellationToken cancellationToken)
    {
        var hold = await _holdService.GetByIdAsync(id, cancellationToken);

        return hold is null
            ? NotFound()
            : Ok(ToResponse(hold));
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Release(
        string id,
        CancellationToken cancellationToken)
    {
        var released = await _holdService.ReleaseAsync(id, cancellationToken);

        return released ? NoContent() : NotFound();
    }

    private static HoldResponse ToResponse(InventoryHoldEntity hold) =>
        new(
            hold.Id,
            hold.Status.ToString(),
            hold.CreatedAtUtc,
            hold.ExpiresAtUtc,
            hold.Items.Select(item => new HoldItemResponse(
                item.ProductId,
                item.ProductName,
                item.Quantity)).ToList());
}
