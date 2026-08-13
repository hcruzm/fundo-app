namespace Fundo.LoanApp.Application.LoanApplications;

public abstract record SubmitLoanApplicationResult
{
    private SubmitLoanApplicationResult()
    {
    }

    public sealed record Approved(Guid ApplicationId, Guid CustomerId, bool IsReturningCustomer)
        : SubmitLoanApplicationResult;

    public sealed record Denied(string Reason) : SubmitLoanApplicationResult;
}
