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
            .Produces<LoanApplicationDetail>()
            .ProducesProblem(StatusCodes.Status404NotFound);

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

        return detail is null
            ? Results.Problem($"Application {id} was not found.", statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(detail);
    }
}
