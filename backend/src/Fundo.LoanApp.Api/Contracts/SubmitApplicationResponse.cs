namespace Fundo.LoanApp.Api.Contracts;

public sealed record SubmitApplicationResponse(
    string Decision,
    Guid? ApplicationId,
    Guid? CustomerId,
    bool IsReturningCustomer,
    string? Reason);
