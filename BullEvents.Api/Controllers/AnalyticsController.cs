using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BullEvents.Api.Controllers;

/// <summary>
/// The dashboard's query surface.
///
/// One generic endpoint rather than a hand-written aggregate per chart: a widget
/// describes what it wants — object, group-by, measure, aggregate, filter,
/// period — and gets back rows already pivoted for a chart. That is what lets
/// the builder offer combinations nobody wrote an endpoint for.
///
/// Tenant isolation is not re-implemented here. Every source query goes through
/// the same global query filters the list views use, so a dashboard cannot
/// group across a company boundary.
/// </summary>
[ApiController]
[Route("api/analytics")]
[Authorize]
[SecuredBy(SecuredObjects.Report)]
public class AnalyticsController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// The catalogue the widget editor is generated from: which objects can be
    /// charted, and for each one the dimensions, measures and date fields it
    /// offers. The client builds its menus from this rather than a hardcoded
    /// list, so the two cannot drift.
    /// </summary>
    [HttpGet("datasets")]
    public ActionResult<IReadOnlyList<DatasetDto>> Datasets() =>
        Ok(AnalyticsCatalog.Datasets.Select(dataset => dataset.Describe()).ToList());

    /// <summary>Runs one widget's query.</summary>
    [PermissionAction(ObjectAction.View)]
    [HttpPost("aggregate")]
    public async Task<ActionResult<AggregateResponse>> Aggregate(
        [FromBody] AggregateRequest request,
        CancellationToken cancellationToken)
    {
        var dataset = AnalyticsCatalog.Require(request.Dataset);
        return Ok(await dataset.RunAsync(db, request, cancellationToken));
    }

    /// <summary>
    /// Runs several widgets in one round trip. A dashboard opens with a dozen
    /// widgets; batching keeps that to one request instead of a dozen racing
    /// each other for the connection pool.
    /// </summary>
    [PermissionAction(ObjectAction.View)]
    [HttpPost("aggregate/batch")]
    public async Task<ActionResult<IReadOnlyList<AggregateResponse>>> AggregateBatch(
        [FromBody] List<AggregateRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count > 40)
        {
            throw ApiException.BadRequest("A batch carries at most 40 widget queries.");
        }

        var results = new List<AggregateResponse>(requests.Count);

        // Sequential on purpose: one DbContext is not thread-safe, and these are
        // small grouped reads where the round trip dominates anyway.
        foreach (var request in requests)
        {
            var dataset = AnalyticsCatalog.Require(request.Dataset);
            results.Add(await dataset.RunAsync(db, request, cancellationToken));
        }

        return Ok(results);
    }

    /// <summary>
    /// Distinct values for one field, most common first — fills the value
    /// dropdown in the widget's filter builder with what is actually in the
    /// data rather than a static option list that can go stale.
    /// </summary>
    [HttpGet("datasets/{datasetId}/values")]
    public async Task<ActionResult<IReadOnlyList<FacetBucket>>> Values(
        string datasetId,
        [FromQuery] string field,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw ApiException.BadRequest("Name the field to list values for.");
        }

        var dataset = AnalyticsCatalog.Require(datasetId);
        return Ok(await dataset.ValuesAsync(db, field, cancellationToken));
    }
}
