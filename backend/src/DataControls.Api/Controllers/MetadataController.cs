using DataControls.Core.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace DataControls.Api.Controllers;

/// <summary>Exposes the semantic layer: what can be controlled, in business terms.</summary>
[ApiController]
[Route("api/metadata")]
[Produces("application/json")]
public sealed class MetadataController(MetadataService metadata) : ControllerBase
{
    /// <summary>The business entities controls can be defined on (employees, departments...).</summary>
    [HttpGet("entities")]
    public Task<IReadOnlyList<EntityDto>> GetEntities(CancellationToken ct) => metadata.GetEntitiesAsync(ct);

    /// <summary>Every field that can be used in a control on the entity, including fields of related entities.</summary>
    [HttpGet("entities/{entityKey}/fields")]
    public Task<IReadOnlyList<SelectableFieldDto>> GetFields(string entityKey, CancellationToken ct) =>
        metadata.GetSelectableFieldsAsync(entityKey, ct);

    /// <summary>The comparison operators and the field types each one fits.</summary>
    [HttpGet("operators")]
    public IReadOnlyList<OperatorDto> GetOperators() => MetadataService.GetOperators();

    /// <summary>Reloads metadata from the database (after an admin updated the meta tables).</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Refresh()
    {
        metadata.Refresh();
        return NoContent();
    }
}
