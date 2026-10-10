using InventoryHold.Domain.Repositories;
using InventoryHold.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace InventoryHold.WebApi.Controllers;

[ApiController]
[Route("api/inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryCache _inventoryCache;

    public InventoryController(
        IInventoryRepository inventoryRepository,
        IInventoryCache inventoryCache)
    {
        _inventoryRepository = inventoryRepository;
        _inventoryCache = inventoryCache;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var cachedInventory =
            await _inventoryCache.GetInventoryAsync(cancellationToken);

        if (cachedInventory is not null)
        {
            Response.Headers["X-Cache"] = "HIT";
            return Ok(cachedInventory);
        }

        var inventory =
            await _inventoryRepository.GetAllAsync(cancellationToken);

        await _inventoryCache.SetInventoryAsync(
            inventory, cancellationToken);

        Response.Headers["X-Cache"] = "MISS";

        return Ok(inventory);
    }
}
