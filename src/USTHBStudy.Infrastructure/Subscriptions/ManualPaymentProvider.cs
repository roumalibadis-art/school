namespace USTHBStudy.Infrastructure.Subscriptions;

using Microsoft.Extensions.Options;
using USTHBStudy.Application.Subscriptions;

/// <summary>Bound from <c>Payments:Manual</c>. Holds the bank/CCP details shown to students.</summary>
public sealed class ManualPaymentOptions
{
    public const string SectionName = "Payments:Manual";

    public string AccountName { get; set; } = "USTHB Study";
    public string AccountNumber { get; set; } = "";
    public string Bank { get; set; } = "";
    public string ContactEmail { get; set; } = "support@example.local";
}

/// <summary>
/// The MVP payment provider (PRD §25): records a pending payment and tells the student how to pay
/// (CCP / BaridiMob / bank transfer) quoting the reference. An admin verifies and approves — Premium
/// is never activated automatically.
/// </summary>
public sealed class ManualPaymentProvider : IPaymentProvider
{
    private readonly ManualPaymentOptions _options;

    public ManualPaymentProvider(IOptions<ManualPaymentOptions> options) => _options = options.Value;

    public string Key => "manual";

    public Task<PaymentInitiation> InitiateAsync(PaymentInitiationRequest request, CancellationToken ct = default)
    {
        var lines = new List<string>
        {
            $"Montant : {request.Amount:0.##} {request.Currency} — {request.PlanName}",
            $"Référence à indiquer : {request.Reference}",
        };
        if (!string.IsNullOrWhiteSpace(_options.AccountNumber))
        {
            lines.Add($"Compte : {_options.AccountName} — {_options.AccountNumber}"
                      + (string.IsNullOrWhiteSpace(_options.Bank) ? "" : $" ({_options.Bank})"));
        }

        lines.Add($"Après paiement, votre abonnement est activé par un administrateur. Support : {_options.ContactEmail}");

        return Task.FromResult(new PaymentInitiation("manual", request.Reference, string.Join('\n', lines)));
    }
}
