using System.Diagnostics;
using Fundo.LoanApp.Api.Contracts;
using Fundo.LoanApp.Api.Validation;
using Fundo.LoanApp.Application.LoanApplications;

namespace Fundo.LoanApp.Api.Endpoints;

public static class LoanApplicationEndpoints
{
    public static IEndpointRouteBuilder MapLoanApplicationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/applications").WithTags("Applications");

        group.MapPost("/", SubmitAsync)
            .AddEndpointFilter<ValidationFilter<SubmitApplicationRequest>>()
            .WithSummary("Submits a loan application and returns the decision.")
            .Produces<SubmitApplicationResponse>(StatusCodes.Status201Created)
            .Produces<SubmitApplicationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetAsync)
            .WithSummary("Returns a stored application with its customer.")
            .Produces<ApplicationDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/", ListAsync)
            .WithSummary("Returns every stored application, most recently updated first.")
            .Produces<IReadOnlyList<ApplicationSummaryResponse>>();

        return routes;
    }

    // The endpoint maps a result to a status code and nothing else. Every decision
    // about what that result should be was made in the handler and the rule engine.
    private static async Task<IResult> SubmitAsync(
        SubmitApplicationRequest request,
        SubmitLoanApplicationHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(request.ToCommand(), ct);

        return result switch
        {
            SubmitLoanApplicationResult.Approved approved => Results.Created(
                $"/api/applications/{approved.ApplicationId}",
                new SubmitApplicationResponse(
                    "Approved",
                    approved.ApplicationId,
                    approved.CustomerId,
                    approved.IsReturningCustomer,
                    Reason: null)),

            // A denial answers a well-formed request, so it is a 200, not a 4xx.
            SubmitLoanApplicationResult.Denied denied => Results.Ok(
                new SubmitApplicationResponse(
                    "Denied",
                    ApplicationId: null,
                    CustomerId: null,
                    IsReturningCustomer: false,
                    denied.Reason)),

            _ => throw new UnreachableException()
        };
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        GetLoanApplicationHandler handler,
        CancellationToken ct)
    {
        var detail = await handler.HandleAsync(id, ct);

        if (detail is null)
        {
            return Results.Problem($"Application {id} was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        var response = new ApplicationDetailResponse(
            detail.ApplicationId,
            detail.CustomerId,
            detail.RequestedAmount,
            detail.Status,
            detail.FirstName,
            detail.LastName,
            detail.CompanyName,
            detail.MaskedSsn,
            new AddressResponse(
                detail.Address.Street,
                detail.Address.City,
                detail.Address.State,
                detail.Address.PostalCode),
            detail.CreatedAt,
            detail.UpdatedAt);

        return Results.Ok(response);
    }

    private static async Task<IResult> ListAsync(
        ListLoanApplicationsHandler handler,
        CancellationToken ct)
    {
        var summaries = await handler.HandleAsync(ct);

        var response = summaries
            .Select(summary => new ApplicationSummaryResponse(
                summary.ApplicationId,
                summary.CustomerId,
                summary.FirstName,
                summary.LastName,
                summary.CompanyName,
                summary.RequestedAmount,
                summary.MaskedSsn,
                summary.City,
                summary.State,
                summary.IsReturningCustomer,
                summary.CreatedAt,
                summary.UpdatedAt))
            .ToList();

        return Results.Ok(response);
    }
}
