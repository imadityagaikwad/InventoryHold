using System.ComponentModel.DataAnnotations;

namespace InventoryHold.Contracts.Requests;

public sealed class CreateHoldRequest
{
    [Required]
    [MinLength(1)]
    public List<CreateHoldItemRequest> Items { get; set; } = [];
}

public sealed class CreateHoldItemRequest
{
    [Required]
    [StringLength(100)]
    public string ProductId { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}
