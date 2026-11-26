using Mediator;
using Microsoft.Extensions.Logging;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Customers;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Items;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Settings;

namespace Migrator;

/// <summary>
/// The demo data of the InvoiceDocuments module, made through the module's own commands, the way a user makes it:
/// a made-up Slovenian issuer (the Slovenian rule pack is registered), two customers, three items, and one document
/// of each kind and state: a pro forma, a paid invoice, an open invoice, a credit note to the paid one, and a draft.
/// Every name, number and account is invented; the tax number only passes the checksum. It runs once: an issuer
/// profile means the data is there. Delete this file with the module (init-product does).
/// </summary>
internal static class InvoiceDocumentsDemoData
{
    public static async Task SeedAsync(IMediator mediator, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        if (await mediator.Send(new GetIssuerProfileQuery(), cancellationToken) is not null)
        {
            return;
        }

        await mediator.Send(new UpdateIssuerProfileCommand(
            Name: "Demo Studio d.o.o.",
            AddressLine: "Demo street 1",
            PostalCode: "1000",
            City: "Ljubljana",
            CountryCode: "SI",
            TaxNumber: "SI12345679",
            Iban: "SI56 0000 0000 0000 000",
            Email: "billing@example.com",
            DefaultCurrency: "EUR",
            DefaultPaymentTermDays: 15), cancellationToken);

        var company = await mediator.Send(new CreateCustomerCommand(
            "Example Company d.o.o.", "accounts@example.com", "Sample road 10", "2000", "Maribor", "SI", null, null), cancellationToken);
        var household = await mediator.Send(new CreateCustomerCommand(
            "Example Household", "household@example.com", "Test lane 5", "6000", "Koper", "SI", null, null), cancellationToken);

        var consulting = await mediator.Send(new CreateItemCommand("Consulting", "One hour of consulting", "h", 60m, "S"), cancellationToken);
        var hosting = await mediator.Send(new CreateItemCommand("Website hosting", "Hosting for one month", "month", 25m, "S"), cancellationToken);
        var guide = await mediator.Send(new CreateItemCommand("Printed guide", "A printed user guide", "pcs", 15m, "R1"), cancellationToken);

        var proForma = await DraftAsync(mediator, DocumentType.ProForma, company, null, cancellationToken, (consulting, 10m));
        await mediator.Send(new IssueDocumentCommand(proForma), cancellationToken);

        var paid = await DraftAsync(mediator, DocumentType.Invoice, company, null, cancellationToken, (consulting, 4m), (hosting, 1m));
        await mediator.Send(new IssueDocumentCommand(paid), cancellationToken);
        await mediator.Send(new MarkDocumentPaidCommand(paid), cancellationToken);

        var open = await DraftAsync(mediator, DocumentType.Invoice, household, null, cancellationToken, (guide, 2m), (hosting, 1m));
        await mediator.Send(new IssueDocumentCommand(open), cancellationToken);

        var creditNote = await DraftAsync(mediator, DocumentType.CreditNote, company, paid, cancellationToken, (consulting, 1m));
        await mediator.Send(new IssueDocumentCommand(creditNote), cancellationToken);

        await DraftAsync(mediator, DocumentType.Invoice, household, null, cancellationToken, (consulting, 2m));

        logger.LogInformation("Created the demo invoice documents");
    }

    private static ValueTask<Guid> DraftAsync(
        IMediator mediator,
        DocumentType type,
        Guid customerId,
        Guid? referenceDocumentId,
        CancellationToken cancellationToken,
        params (Guid ItemId, decimal Quantity)[] lines) =>
        mediator.Send(
            new CreateDocumentDraftCommand(
                type,
                customerId,
                Currency: null,
                DueDate: null,
                Notes: null,
                referenceDocumentId,
                lines.Select(l => new DocumentLineInput(l.ItemId, null, l.Quantity, null, null, null)).ToList()),
            cancellationToken);
}
