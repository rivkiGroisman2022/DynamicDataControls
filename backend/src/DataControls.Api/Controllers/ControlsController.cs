using DataControls.Core.Controls;
using DataControls.Core.Definitions;
using Microsoft.AspNetCore.Mvc;

namespace DataControls.Api.Controllers;

/// <summary>
/// Managing and running controls. A "thin" controller: it only maps HTTP to services.
/// No business logic and no try/catch (errors are handled in GlobalExceptionHandler).
/// </summary>
[ApiController]
[Route("api/controls")]
[Produces("application/json")]
public sealed class ControlsController(ControlService controls, ControlRunService runner, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ControlListItemDto>> GetAll(CancellationToken ct) => controls.GetAllAsync(ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ControlDetailsDto> Get(int id, CancellationToken ct) => controls.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ControlDetailsDto>> Create(ControlUpsertRequest request, CancellationToken ct)
    {
        var created = await controls.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public Task<ControlDetailsDto> Update(int id, ControlUpsertRequest request, CancellationToken ct) =>
        controls.UpdateAsync(id, request, ct);

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await controls.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Checks a definition without saving: validity, description in words, and (in development) the SQL that would be produced.</summary>
    [HttpPost("preview")]
    public Task<DefinitionPreviewDto> Preview(ControlDefinition definition, CancellationToken ct) =>
        controls.PreviewAsync(definition, includeSql: env.IsDevelopment(), ct);

    /// <summary>Runs a saved control and returns the violating records with the reason for each.</summary>
    [HttpPost("{id:int}/run")]
    public Task<ControlRunResultDto> Run(int id, [FromQuery] int maxRows = 1000, CancellationToken ct = default) =>
        runner.RunAsync(id, maxRows, ct);

    /// <summary>Test run of a definition that has not been saved yet (from the editor / an AI suggestion).</summary>
    [HttpPost("test-run")]
    public Task<ControlRunResultDto> TestRun(ControlDefinition definition, CancellationToken ct) =>
        runner.TestRunAsync(definition, ct: ct);

    [HttpGet("{id:int}/runs")]
    public Task<IReadOnlyList<ControlRunHistoryItem>> History(int id, [FromQuery] int take = 20, CancellationToken ct = default) =>
        runner.GetHistoryAsync(id, take, ct);
}
