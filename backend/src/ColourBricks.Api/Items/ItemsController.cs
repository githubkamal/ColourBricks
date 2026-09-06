using System.Diagnostics;
using ColourBricks.Api.Authorization;
using ColourBricks.Application.Common.Pagination;
using ColourBricks.Application.Items;
using Microsoft.AspNetCore.Mvc;

namespace ColourBricks.Api.Items;

[ApiController]
[Route("api/v1/items")]
public sealed class ItemsController(IItemService items) : ControllerBase
{
    [HttpGet]
    [HasPermission("materials.view")]
    public Task<PagedResult<ItemSearchItem>> List(
        [FromQuery] long? categoryId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        items.ListAsync(categoryId, search, page, pageSize, cancellationToken);

    [HttpGet("search")]
    [HasPermission("materials.view")]
    public Task<IReadOnlyList<ItemSearchItem>> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default) =>
        items.SearchAsync(q, limit, cancellationToken);

    [HttpGet("{id:long}")]
    [HasPermission("materials.view")]
    public async Task<ActionResult<ItemDto>> Get(long id, CancellationToken cancellationToken)
    {
        ItemDto? item = await items.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [HasPermission("materials.add")]
    public async Task<IActionResult> Create(
        [FromBody] CreateItemRequest request,
        [FromQuery] bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            CreateItemResult result = await items.CreateAsync(request, confirm, cancellationToken);

            if (result.RequiresConfirmation)
            {
                return Ok(new { requiresConfirmation = true, nearDuplicates = result.NearDuplicates });
            }

            return CreatedAtAction(nameof(Get), new { id = result.Item!.Id }, result.Item);
        }
        catch (ItemExactDuplicateException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
    }

    [HttpPut("{id:long}")]
    [HasPermission("materials.edit")]
    public async Task<ActionResult<ItemDto>> Update(
        long id, [FromBody] UpdateItemRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ItemDto? updated = await items.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ItemExactDuplicateException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
    }

    [HttpGet("categories")]
    [HasPermission("materials.view")]
    public Task<IReadOnlyList<ItemCategoryDto>> Categories(CancellationToken cancellationToken) =>
        items.ListCategoriesAsync(cancellationToken);

    [HttpPost("categories")]
    [HasPermission("materials.add")]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateItemCategoryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ItemCategoryDto category = await items.CreateCategoryAsync(request, cancellationToken);
            return Created($"/api/v1/items/categories/{category.Id}", category);
        }
        catch (ItemCategoryExactDuplicateException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
    }

    [HttpPut("categories/{id:long}")]
    [HasPermission("materials.edit")]
    public async Task<ActionResult<ItemCategoryDto>> UpdateCategory(
        long id, [FromBody] UpdateItemCategoryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ItemCategoryDto? updated = await items.UpdateCategoryAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ItemCategoryExactDuplicateException ex)
        {
            return Conflict409(ex.Message, ex.ExistingId);
        }
    }

    [HttpGet("units")]
    [HasPermission("materials.view")]
    public Task<IReadOnlyList<UnitDto>> Units(CancellationToken cancellationToken) =>
        items.ListUnitsAsync(cancellationToken);

    [HttpPost("units")]
    [HasPermission("materials.add")]
    public async Task<ActionResult<UnitDto>> CreateUnit(
        [FromBody] CreateUnitRequest request, CancellationToken cancellationToken)
    {
        UnitDto unit = await items.CreateUnitAsync(request, cancellationToken);
        return Created($"/api/v1/items/units/{unit.Id}", unit);
    }

    private ObjectResult Conflict409(string detail, long existingId)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "A record with this name already exists.",
            Detail = detail,
            Type = "https://datatracker.ietf.org/doc/html/rfc9457",
            Extensions =
            {
                ["existingId"] = existingId,
                ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            },
        };
        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status409Conflict,
            ContentTypes = { "application/problem+json" },
        };
    }
}
