namespace Fundo.LoanApp.Infrastructure.ExternalService;

public sealed class ExternalServiceOptions
{
    public const string SectionName = "ExternalService";

    public string BaseUrl { get; set; } = string.Empty;
}
