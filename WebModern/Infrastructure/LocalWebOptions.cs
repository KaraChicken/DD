namespace DDTank.Web.Infrastructure;

public sealed class LocalWebOptions
{
    public const string SectionName = "LocalWeb";
    public string BaseUrl { get; init; } = "http://127.0.0.1:5080";
}
