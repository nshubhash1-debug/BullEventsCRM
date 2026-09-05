using BullEvents.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BullEvents.Api.Services;

/// <summary>
/// Everything the printed quotation says, gathered before rendering.
///
/// The document takes a snapshot rather than an EF entity on purpose: rendering
/// must not be able to trigger a lazy load halfway down page two, and the same
/// shape can be filled from a preview for a draft that has not been saved.
/// </summary>
public record QuotationDocumentModel
{
    /// <summary>
    /// The seller, as the masthead prints it. Empty rather than a hard-coded
    /// name: a default naming some other developer would put that company on a
    /// priced offer, which is worse than a blank line.
    /// </summary>
    public string DeveloperName { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string ProjectAddress { get; init; } = string.Empty;
    public string? ReraNumber { get; init; }
    public string? DeveloperContact { get; init; }

    public string QuoteNumber { get; init; } = string.Empty;
    public DateTime IssueDate { get; init; }
    public DateTime ValidUntil { get; init; }
    public int Version { get; init; } = 1;
    public string Status { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;
    public string? CustomerPhone { get; init; }
    public string? CustomerEmail { get; init; }
    public string? BillingAddress { get; init; }

    public string UnitNumber { get; init; } = string.Empty;
    public string? TowerName { get; init; }
    public string UnitType { get; init; } = string.Empty;
    public int Floor { get; init; }

    /// <summary>The three areas the cost sheet states — priced on the saleable one.</summary>
    public decimal SaleableArea { get; init; }
    public decimal BuiltUpArea { get; init; }
    public decimal CarpetArea { get; init; }

    public string? Facing { get; init; }
    public string? ViewType { get; init; }

    public string PaymentPlanName { get; init; } = string.Empty;
    public string? RateCardLabel { get; init; }
    public decimal RatePerSqft { get; init; }
    public decimal PlcPerSqft { get; init; }
    public decimal Discount { get; init; }

    /// <summary>
    /// False when the offer holds the list price. The derivation then prints no
    /// discount line at all rather than a line reading "less 0%", which reads to
    /// a buyer as though something was withheld.
    /// </summary>
    public bool DiscountApplied { get; init; } = true;

    /// <summary>What the concession is called — "Launch offer", "Festive".</summary>
    public string? DiscountLabel { get; init; }

    /// <summary>The money the discount is worth across the whole unit.</summary>
    public decimal DiscountAmount { get; init; }

    public decimal EffectiveRatePerSqft { get; init; }

    /* ---------------- unit cost ---------------- */

    public decimal BasicAmount { get; init; }
    public decimal TaxRate { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }

    /* ---------------- other heads ---------------- */

    public IReadOnlyList<ChargeResult> Charges { get; init; } = [];
    public decimal ChargesBasic { get; init; }
    public decimal ChargesTax { get; init; }
    public decimal ChargesTotal { get; init; }
    public decimal RefundableTotal { get; init; }

    public decimal GrandTotal { get; init; }
    public decimal ScheduledTotal { get; init; }
    public string AmountInWords { get; init; } = string.Empty;

    public IReadOnlyList<MilestoneResult> Milestones { get; init; } = [];

    /* ---------------- investor annexure ---------------- */

    public InvestorAnnexure? Annexure { get; init; }

    public string? SalesPersonName { get; init; }
    public string? SalesPersonId { get; init; }
    public string? Notes { get; init; }
    public string? TermsAndConditions { get; init; }

    /// <summary>Stamped across the page for anything not yet issued.</summary>
    public string? Watermark { get; init; }
}

/// <summary>
/// The printed quotation, set as an accounting document.
///
/// Modelled on the ruled voucher every Indian buyer, broker and banker already
/// knows how to read — a boxed masthead, a party block against a reference
/// block, a particulars grid that foots to a total, the amount in words, and a
/// declaration over a signature panel. Three decisions follow from that.
///
/// There is no colour. Not as a style preference: this document is printed on
/// office lasers, faxed, scanned into loan files and photocopied at registrar
/// counters, and every one of those steps drops colour to grey. A layout that
/// relies on a tinted band to separate a total from a row stops working the
/// first time somebody copies it.
///
/// Emphasis is carried by rules and weight instead. A hairline separates rows
/// inside a block, a full rule closes a block, and a double rule closes the
/// consideration — the grammar a ledger already uses.
///
/// Every block is boxed and every column is ruled, because the reader of a cost
/// sheet checks figures against their column heading, and an unruled grid makes
/// them count across with a finger.
/// </summary>
public class QuotationDocument(QuotationDocumentModel model) : IDocument
{
    /* ------------------------------------------------------------------ *
     * Ink — black on white, and three greys
     * ------------------------------------------------------------------ */

    /// <summary>Figures, headings, and the rules that carry the structure.</summary>
    private static readonly Color Ink = Color.FromHex("#000000");

    private static readonly Color Body = Color.FromHex("#1A1A1A");
    private static readonly Color Muted = Color.FromHex("#555555");
    private static readonly Color Faint = Color.FromHex("#767676");

    /// <summary>Block borders and column rules.</summary>
    private static readonly Color Line = Color.FromHex("#000000");

    /// <summary>Row separators inside a block — lighter, so the box still reads.</summary>
    private static readonly Color Hair = Color.FromHex("#B8B8B8");

    private static readonly Color Paper = Color.FromHex("#FFFFFF");

    /// <summary>Body and figures. Segoe UI carries the rupee glyph; Calibri does not.</summary>
    private const string Sans = "Segoe UI";

    private const float Frame = 0.9f;
    private const float Rule = 0.6f;
    private const float Thread = 0.4f;

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Quotation {model.QuoteNumber} — {model.UnitNumber}",
        Author = model.DeveloperName,
        Subject = $"{model.ProjectName} · {model.UnitNumber}",
    };

    /* ------------------------------------------------------------------ *
     * Formatting
     * ------------------------------------------------------------------ */

    /// <summary>Indian digit grouping — 1,58,12,160 rather than 15,812,160.</summary>
    private static readonly System.Globalization.NumberFormatInfo IndianDigits = new()
    {
        NumberGroupSizes = [3, 2],
        NumberGroupSeparator = ",",
        NumberDecimalSeparator = ".",
        NumberDecimalDigits = 2,
    };

    private static string Money(decimal value) => value.ToString("N", IndianDigits);

    private static string Rupees(decimal value) => "₹ " + Money(value);

    private static string Percent(decimal fraction) =>
        (fraction * 100m).ToString("0.##") + "%";

    private static string Area(decimal value) => $"{value:0.##}";

    private static string Day(DateTime value) => value.ToString("dd-MMM-yyyy");

    /// <summary>
    /// "Tower B" rather than "Tower Tower B". Whether the stored name carries
    /// the word is a per-project habit, so the label asks rather than assumes.
    /// </summary>
    private string? TowerLabel()
    {
        if (string.IsNullOrWhiteSpace(model.TowerName)) return null;

        var name = model.TowerName!.Trim();

        return name.StartsWith("Tower", StringComparison.OrdinalIgnoreCase)
            ? name
            : $"Tower {name}";
    }

    /* ------------------------------------------------------------------ *
     * Page
     * ------------------------------------------------------------------ */

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(26);
            page.PageColor(Paper);

            page.DefaultTextStyle(t => t
                .FontSize(8).FontColor(Body).LineHeight(1.3f)
                .FontFamily(Sans, "Calibri", Fonts.Arial));

            page.Content().Element(Content);
            page.Footer().Element(PageFooter);
        });
    }

    /* ------------------------------------------------------------------ *
     * Body
     * ------------------------------------------------------------------ */

    private void Content(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(0);

            column.Item().Element(Masthead);
            column.Item().Element(ReferenceBlock);
            column.Item().Element(PartyBlock);

            if (!string.IsNullOrWhiteSpace(model.Watermark))
            {
                column.Item().Element(DraftNotice);
            }

            column.Item().Element(RateDerivation);
            column.Item().Element(ChargeTable);
            column.Item().Element(AmountInWords);

            column.Item().PaddingTop(11).Element(ScheduleTable);

            if (model.Charges.Any(x => !x.IncludeInSchedule))
            {
                column.Item().PaddingTop(11).Element(OutsideSchedule);
            }

            if (model.Annexure is { HasAnything: true })
            {
                column.Item().PaddingTop(11).EnsureSpace(320).Element(AnnexureBlock);
            }

            if (!string.IsNullOrWhiteSpace(model.Notes))
            {
                column.Item().PaddingTop(11).Element(RemarksBlock);
            }

            // Kept together. The terms box and the signature panel share a
            // border and read as one closing panel, so a break between them
            // leaves a box open at the foot of one page and a lone signature
            // block at the head of the next.
            column.Item().PaddingTop(11).PreventPageBreak().Column(closing =>
            {
                closing.Item().Element(TermsBlock);
                closing.Item().Element(Signatures);
            });
        });
    }

    /* ------------------------------------------------------------------ *
     * Masthead
     * ------------------------------------------------------------------ */

    /// <summary>
    /// The seller, centred, over a ruled title bar. Printed once — a voucher
    /// states who issued it at the top of the first page and identifies itself
    /// in the footer thereafter.
    /// </summary>
    private void Masthead(IContainer container)
    {
        container.Border(Frame).BorderColor(Line).Column(column =>
        {
            column.Item().PaddingTop(11).PaddingBottom(9).PaddingHorizontal(14)
                .Column(inner =>
            {
                inner.Item().AlignCenter().Text(model.DeveloperName.ToUpperInvariant())
                    .FontSize(14).Bold().FontColor(Ink).LetterSpacing(0.06f);

                if (!string.IsNullOrWhiteSpace(model.ProjectAddress))
                {
                    inner.Item().PaddingTop(3).AlignCenter().Text(model.ProjectAddress)
                        .FontSize(8).FontColor(Muted);
                }

                var credentials = string.Join("   |   ", new[]
                {
                    string.IsNullOrWhiteSpace(model.ReraNumber) ? null : $"RERA {model.ReraNumber}",
                    model.DeveloperContact,
                }.Where(v => !string.IsNullOrWhiteSpace(v)));

                if (credentials.Length > 0)
                {
                    inner.Item().PaddingTop(2).AlignCenter().Text(credentials)
                        .FontSize(7.5f).FontColor(Muted);
                }
            });

            column.Item().BorderTop(Rule).BorderColor(Line)
                .PaddingVertical(5).AlignCenter()
                .Text("QUOTATION   /   COST SHEET")
                .FontSize(10).Bold().FontColor(Ink).LetterSpacing(0.24f);
        });
    }

    /* ------------------------------------------------------------------ *
     * Reference block
     * ------------------------------------------------------------------ */

    /// <summary>
    /// The six facts a document is filed against, in a ruled grid. Set as a
    /// grid rather than a paragraph because this is the block a clerk copies
    /// into a register, one field at a time.
    /// </summary>
    private void ReferenceBlock(IContainer container)
    {
        container.BorderLeft(Frame).BorderRight(Frame).BorderBottom(Frame).BorderColor(Line)
            .Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            void Cell(string label, string value, bool lastInRow = false, bool lastRow = false)
            {
                table.Cell()
                    .BorderRight(lastInRow ? 0 : Thread)
                    .BorderBottom(lastRow ? 0 : Thread)
                    .BorderColor(Hair)
                    .PaddingHorizontal(11).PaddingVertical(6)
                    .Column(c =>
                {
                    c.Item().Text(label.ToUpperInvariant())
                        .FontSize(6.5f).FontColor(Faint).LetterSpacing(0.1f);

                    c.Item().PaddingTop(2).Text(value)
                        .FontSize(8.5f).SemiBold().FontColor(Ink).LineHeight(1.25f);
                });
            }

            Cell("Quotation No.", model.QuoteNumber);
            Cell("Dated", Day(model.IssueDate));
            Cell("Valid until", Day(model.ValidUntil), lastInRow: true);

            Cell("Payment plan", model.PaymentPlanName, lastRow: true);
            Cell("Revision", model.Version > 1 ? $"Rev. {model.Version}" : "Original", lastRow: true);
            Cell("Status", string.IsNullOrWhiteSpace(model.Status) ? "Draft" : model.Status,
                lastInRow: true, lastRow: true);
        });
    }

    /* ------------------------------------------------------------------ *
     * Parties
     * ------------------------------------------------------------------ */

    /// <summary>Buyer on the left, property on the right, as a voucher sets them.</summary>
    private void PartyBlock(IContainer container)
    {
        container.BorderLeft(Frame).BorderRight(Frame).BorderBottom(Frame).BorderColor(Line)
            .Row(row =>
        {
            row.RelativeItem().BorderRight(Thread).BorderColor(Hair)
                .PaddingHorizontal(11).PaddingVertical(8).Column(left =>
            {
                left.Item().Text("QUOTATION TO")
                    .FontSize(6.5f).FontColor(Faint).LetterSpacing(0.1f);

                left.Item().PaddingTop(3).Text(model.CustomerName)
                    .FontSize(11).Bold().FontColor(Ink).LineHeight(1.2f);

                var contact = new[] { model.CustomerPhone, model.CustomerEmail }
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToList();

                if (contact.Count > 0)
                {
                    left.Item().PaddingTop(2).Text(string.Join("   |   ", contact))
                        .FontSize(8).FontColor(Muted);
                }

                if (!string.IsNullOrWhiteSpace(model.BillingAddress))
                {
                    left.Item().PaddingTop(4).Text(model.BillingAddress!)
                        .FontSize(8).FontColor(Body).LineHeight(1.4f);
                }
            });

            row.RelativeItem().PaddingHorizontal(11).PaddingVertical(8).Column(right =>
            {
                right.Item().Text("PROPERTY")
                    .FontSize(6.5f).FontColor(Faint).LetterSpacing(0.1f);

                right.Item().PaddingTop(3).Text($"{model.ProjectName} — {model.UnitNumber}")
                    .FontSize(11).Bold().FontColor(Ink).LineHeight(1.2f);

                right.Item().PaddingTop(2).Text(string.Join("   |   ", new[]
                {
                    TowerLabel(),
                    $"Floor {model.Floor}",
                    model.UnitType,
                    model.Facing is null ? null : $"{model.Facing} facing",
                    model.ViewType,
                }.Where(v => !string.IsNullOrWhiteSpace(v)))).FontSize(8).FontColor(Muted);

                // All three areas, because the buyer is charged on one, lives in
                // another, and RERA requires the third to be stated. Printing
                // only the priced number is what makes a cost sheet look evasive.
                right.Item().PaddingTop(5).Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Body));

                    t.Span("Saleable ");
                    t.Span($"{Area(model.SaleableArea)} sq ft").SemiBold().FontColor(Ink);

                    if (model.BuiltUpArea > 0)
                    {
                        t.Span("   |   Built-up ");
                        t.Span($"{Area(model.BuiltUpArea)} sq ft").FontColor(Ink);
                    }

                    if (model.CarpetArea > 0)
                    {
                        t.Span("   |   Carpet (RERA) ");
                        t.Span($"{Area(model.CarpetArea)} sq ft").FontColor(Ink);
                    }
                });
            });
        });
    }

    /// <summary>
    /// A draft that reaches a customer by accident has to be unmistakable. It
    /// says so in words rather than as a tinted stamp, which is the first thing
    /// a photocopier drops.
    /// </summary>
    private void DraftNotice(IContainer container)
    {
        container.BorderLeft(Frame).BorderRight(Frame).BorderBottom(Frame).BorderColor(Line)
            .PaddingHorizontal(11).PaddingVertical(6)
            .Text($"** {model.Watermark!.ToUpperInvariant()} **")
            .FontSize(8).Bold().FontColor(Ink).LetterSpacing(0.04f);
    }

    /* ------------------------------------------------------------------ *
     * Rate derivation
     * ------------------------------------------------------------------ */

    /// <summary>
    /// How the rate was arrived at, line by line.
    ///
    /// Shown as a derivation rather than a single number because the discount
    /// and the location charge interact in a way customers reliably query — the
    /// discount comes off the basic rate, the location charge goes on after it.
    /// </summary>
    private void RateDerivation(IContainer container)
    {
        container.BorderLeft(Frame).BorderRight(Frame).BorderBottom(Frame).BorderColor(Line)
            .Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(9);
                columns.RelativeColumn(4);
            });

            void Entry(string label, string value)
            {
                table.Cell()
                    .BorderRight(Thread).BorderBottom(Thread).BorderColor(Hair)
                    .PaddingHorizontal(11).PaddingVertical(5)
                    .Text(label).FontSize(8).FontColor(Body);

                table.Cell()
                    .BorderBottom(Thread).BorderColor(Hair)
                    .PaddingHorizontal(11).PaddingVertical(5).AlignRight()
                    .Text(value).FontSize(8).SemiBold().FontColor(Ink);
            }

            var rateLabel = model.RateCardLabel is null
                ? "Basic sale price, per sq ft"
                : $"Basic sale price, per sq ft   ({model.RateCardLabel})";

            Entry(rateLabel, Money(model.RatePerSqft));

            if (model.DiscountApplied && model.Discount > 0)
            {
                var label = string.IsNullOrWhiteSpace(model.DiscountLabel)
                    ? $"{model.PaymentPlanName} discount"
                    : model.DiscountLabel!.Trim();

                Entry($"Less : {label} @ {Percent(model.Discount)}",
                    $"(-) {Money(model.RatePerSqft * model.Discount)}");
            }
            else
            {
                // Said rather than left blank. A buyer comparing two quotations
                // needs to know the second one is at list price on purpose.
                Entry("Less : Discount — none applied, quoted at list rate", "—");
            }

            if (model.PlcPerSqft > 0)
            {
                Entry("Add : Preferential location charge (not discounted)",
                    $"(+) {Money(model.PlcPerSqft)}");
            }

            table.Cell().BorderTop(Rule).BorderRight(Thread).BorderColor(Line)
                .PaddingHorizontal(11).PaddingVertical(6)
                .Text("Applicable rate, per sq ft")
                .FontSize(8.5f).Bold().FontColor(Ink);

            table.Cell().BorderTop(Rule).BorderColor(Line)
                .PaddingHorizontal(11).PaddingVertical(6).AlignRight()
                .Text(Money(model.EffectiveRatePerSqft))
                .FontSize(9.5f).Bold().FontColor(Ink);
        });
    }

    /* ------------------------------------------------------------------ *
     * Particulars
     * ------------------------------------------------------------------ */

    private void ChargeTable(IContainer container)
    {
        var rows = new List<ChargeResult>
        {
            // The unit cost is the first revenue head, not a special case above
            // the table — that is how a cost sheet reads and how the totals have
            // to foot.
            new(null, 0, ChargeGroups.UnitCharge, "Unit Cost", ChargeBases.PerSqft,
                model.SaleableArea, "sq ft", model.EffectiveRatePerSqft,
                model.BasicAmount, model.TaxRate, model.TaxAmount, model.TotalAmount,
                false, true, null),
        };

        rows.AddRange(model.Charges);

        container.BorderLeft(Frame).BorderRight(Frame).BorderBottom(Frame).BorderColor(Line)
            .Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(26);     // sl
                columns.RelativeColumn(7.6f);   // particulars
                columns.RelativeColumn(3.0f);   // taxable value
                columns.RelativeColumn(2.4f);   // gst
                columns.RelativeColumn(3.4f);   // amount
            });

            table.Header(header =>
            {
                Head(header.Cell(), "Sl");
                Head(header.Cell(), "Particulars");
                Head(header.Cell(), "Taxable value", right: true);
                Head(header.Cell(), "GST", right: true);
                Head(header.Cell(), "Amount", right: true, last: true);
            });

            var index = 1;
            string? previousGroup = null;

            foreach (var charge in rows)
            {
                var newGroup = charge.Group != previousGroup;
                previousGroup = charge.Group;

                IContainer Cell(bool right = false, bool last = false)
                {
                    var cell = table.Cell()
                        .BorderRight(last ? 0 : Thread).BorderBottom(Thread).BorderColor(Hair)
                        .PaddingVertical(6)
                        .PaddingLeft(right ? 4 : 9).PaddingRight(right ? 9 : 4);

                    return right ? cell.AlignRight() : cell;
                }

                Cell().Text(index.ToString()).FontSize(7.5f).FontColor(Faint);

                Cell().Column(c =>
                {
                    c.Item().Text(t =>
                    {
                        t.Span(charge.Name).FontSize(8.5f).SemiBold().FontColor(Ink);

                        if (charge.IsRefundable)
                        {
                            t.Span("   (refundable)").FontSize(7).FontColor(Muted);
                        }
                    });

                    var detail = new[] { newGroup ? charge.Group : null, Derivation(charge) }
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .ToList();

                    if (detail.Count > 0)
                    {
                        c.Item().Text(string.Join("   ·   ", detail))
                            .FontSize(7).FontColor(Muted);
                    }
                });

                Cell(right: true).Text(Money(charge.BasicAmount)).FontSize(8.5f).FontColor(Body);

                Cell(right: true).Column(c =>
                {
                    c.Item().AlignRight().Text(Money(charge.TaxAmount))
                        .FontSize(8.5f).FontColor(Body);
                    c.Item().AlignRight().Text($"@ {Percent(charge.TaxRate)}")
                        .FontSize(6.5f).FontColor(Faint);
                });

                Cell(right: true, last: true).Text(Money(charge.TotalAmount))
                    .FontSize(8.5f).SemiBold().FontColor(Ink);

                index++;
            }

            // The total is a row of this table rather than a bar under it, so it
            // sits in the same ruled columns as the figures it sums. A separate
            // element with matching widths drifts against the table's own cell
            // padding, and a total that does not sit under its column is the one
            // defect a reader spots before they read anything.
            Foot(table.Cell(), "");
            Foot(table.Cell(), "Total");
            Foot(table.Cell(), Money(model.BasicAmount + model.ChargesBasic), right: true);
            Foot(table.Cell(), Money(model.TaxAmount + model.ChargesTax), right: true);
            Foot(table.Cell(), Money(model.GrandTotal), right: true, last: true, strong: true);
        });
    }

    private static string? Derivation(ChargeResult charge) => charge.Basis switch
    {
        ChargeBases.PerSqft => $"{charge.Quantity:0.##} sq ft × {Money(charge.Rate)}",
        ChargeBases.PerQuantity => $"{charge.Quantity:0.##} × {Money(charge.Rate)}",
        ChargeBases.PercentOfUnitCost => $"{Percent(charge.Rate)} of unit cost",
        _ => null,
    };

    /* ------------------------------------------------------------------ *
     * Amount in words
     * ------------------------------------------------------------------ */

    /// <summary>
    /// The figure written out, which is the line a document is signed against.
    /// Closed with a double rule — the ledger's way of saying an account is shut.
    /// </summary>
    private void AmountInWords(IContainer container)
    {
        container.BorderLeft(Frame).BorderRight(Frame).BorderBottom(Frame).BorderColor(Line)
            .Column(column =>
        {
            column.Item().PaddingHorizontal(11).PaddingVertical(7).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("AMOUNT CHARGEABLE (IN WORDS)")
                        .FontSize(6.5f).FontColor(Faint).LetterSpacing(0.1f);

                    c.Item().PaddingTop(3).Text(IndianNumberWords.ToRupees(model.GrandTotal))
                        .FontSize(9).Bold().FontColor(Ink).LineHeight(1.35f);
                });

                row.ConstantItem(14);

                row.AutoItem().AlignBottom().AlignRight().Text(Rupees(model.GrandTotal))
                    .FontSize(13).Bold().FontColor(Ink);
            });

            var notes = new List<string>();

            if (model.DiscountApplied && model.DiscountAmount > 0)
            {
                notes.Add($"Concession allowed {Rupees(model.DiscountAmount)} "
                    + $"@ {Percent(model.Discount)} on the basic rate.");
            }

            if (model.RefundableTotal > 0)
            {
                notes.Add($"Includes refundable deposits {Rupees(model.RefundableTotal)}.");
            }

            if (notes.Count > 0)
            {
                column.Item().BorderTop(Thread).BorderColor(Hair)
                    .PaddingHorizontal(11).PaddingVertical(5)
                    .Text(string.Join("   ", notes))
                    .FontSize(7.5f).FontColor(Muted);
            }
        });
    }

    /* ------------------------------------------------------------------ *
     * Payment schedule
     * ------------------------------------------------------------------ */

    private void ScheduleTable(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Element(c => BlockTitle(c, "Payment schedule",
                model.ScheduledTotal < model.GrandTotal
                    ? $"{model.PaymentPlanName} — spreads {Rupees(model.ScheduledTotal)}; "
                        + "the heads listed after this fall due on their own terms"
                    : model.PaymentPlanName));

            column.Item().Border(Frame).BorderColor(Line).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    // Wide enough for two digits: a construction-linked plan runs
                    // to nineteen rows, and a column sized for one wraps "10".
                    columns.ConstantColumn(26);
                    columns.RelativeColumn(6.0f);
                    columns.RelativeColumn(1.8f);
                    columns.RelativeColumn(3.0f);
                    columns.RelativeColumn(2.6f);
                    columns.RelativeColumn(3.2f);
                    columns.RelativeColumn(3.2f);
                });

                table.Header(header =>
                {
                    Head(header.Cell(), "Sl");
                    Head(header.Cell(), "Milestone");
                    Head(header.Cell(), "%", right: true);
                    Head(header.Cell(), "Taxable value", right: true);
                    Head(header.Cell(), "GST", right: true);
                    Head(header.Cell(), "Instalment", right: true);
                    Head(header.Cell(), "Cumulative", right: true, last: true);
                });

                var index = 1;
                var cumulative = 0m;

                foreach (var milestone in model.Milestones)
                {
                    cumulative += milestone.TotalAmount;

                    IContainer Cell(bool right = false, bool last = false)
                    {
                        var cell = table.Cell()
                            .BorderRight(last ? 0 : Thread).BorderBottom(Thread).BorderColor(Hair)
                            .PaddingVertical(5.5f)
                            .PaddingLeft(right ? 4 : 9).PaddingRight(right ? 9 : 4);

                        return right ? cell.AlignRight() : cell;
                    }

                    Cell().Text(index.ToString()).FontSize(7.5f).FontColor(Faint);

                    Cell().Column(c =>
                    {
                        c.Item().Text(milestone.Label).FontSize(8.5f).FontColor(Ink);

                        if (milestone.DueDate is DateTime due)
                        {
                            c.Item().Text($"Indicative due {Day(due)}")
                                .FontSize(6.5f).FontColor(Faint);
                        }
                    });

                    Cell(right: true).Text(Percent(milestone.Percent))
                        .FontSize(7.75f).FontColor(Muted);
                    Cell(right: true).Text(Money(milestone.BasicAmount))
                        .FontSize(8.5f).FontColor(Body);
                    Cell(right: true).Text(Money(milestone.TaxAmount))
                        .FontSize(8.5f).FontColor(Body);
                    Cell(right: true).Text(Money(milestone.TotalAmount))
                        .FontSize(8.5f).SemiBold().FontColor(Ink);
                    Cell(right: true, last: true).Text(Money(cumulative))
                        .FontSize(7.75f).FontColor(Muted);

                    index++;
                }

                Foot(table.Cell(), "");
                Foot(table.Cell(), "Total");
                Foot(table.Cell(), "", right: true);
                Foot(table.Cell(), Money(model.Milestones.Sum(m => m.BasicAmount)), right: true);
                Foot(table.Cell(), Money(model.Milestones.Sum(m => m.TaxAmount)), right: true);
                Foot(table.Cell(), Money(model.Milestones.Sum(m => m.TotalAmount)),
                    right: true, strong: true);
                Foot(table.Cell(), "", right: true, last: true);
            });
        });
    }

    /// <summary>
    /// Heads the plan does not spread, with when each falls due — so the buyer
    /// is not surprised at possession by a number that was on page one and never
    /// appeared in the schedule they were reading from.
    /// </summary>
    private void OutsideSchedule(IContainer container)
    {
        var unscheduled = model.Charges.Where(x => !x.IncludeInSchedule).ToList();

        container.Column(column =>
        {
            column.Item().Element(c => BlockTitle(
                c, "Payable outside the schedule", "Due on their own terms"));

            column.Item().Border(Frame).BorderColor(Line).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(26);
                    columns.RelativeColumn(9);
                    columns.RelativeColumn(4);
                });

                table.Header(header =>
                {
                    Head(header.Cell(), "Sl");
                    Head(header.Cell(), "Particulars");
                    Head(header.Cell(), "Amount", right: true, last: true);
                });

                for (var i = 0; i < unscheduled.Count; i++)
                {
                    var charge = unscheduled[i];
                    var last = i == unscheduled.Count - 1;

                    table.Cell()
                        .BorderRight(Thread).BorderBottom(last ? 0 : Thread).BorderColor(Hair)
                        .PaddingVertical(6).PaddingLeft(9)
                        .Text((i + 1).ToString()).FontSize(7.5f).FontColor(Faint);

                    table.Cell()
                        .BorderRight(Thread).BorderBottom(last ? 0 : Thread).BorderColor(Hair)
                        .PaddingVertical(6).PaddingHorizontal(9).Column(c =>
                    {
                        c.Item().Text(t =>
                        {
                            t.Span(charge.Name).FontSize(8.5f).SemiBold().FontColor(Ink);

                            if (charge.IsRefundable)
                            {
                                t.Span("   (refundable)").FontSize(7).FontColor(Muted);
                            }
                        });

                        c.Item().Text(charge.DueLabel ?? "Due as advised")
                            .FontSize(7).FontColor(Muted);
                    });

                    table.Cell()
                        .BorderBottom(last ? 0 : Thread).BorderColor(Hair)
                        .PaddingVertical(6).PaddingRight(9).AlignRight().AlignMiddle()
                        .Text(Money(charge.TotalAmount))
                        .FontSize(8.5f).SemiBold().FontColor(Ink);
                }
            });
        });
    }

    /* ------------------------------------------------------------------ *
     * Investment annexure
     * ------------------------------------------------------------------ */

    /// <summary>
    /// The investment case, printed only for the offers that carry one.
    ///
    /// Three ruled blocks rather than a page of cards: the terms of the offer,
    /// the year-by-year projection, and the ledger that foots to what is earned.
    /// Each is conditional on its own switch, so a quotation offering a buy-back
    /// and no assured return prints a buy-back annexure rather than an
    /// assured-return one with zeros down it.
    /// </summary>
    private void AnnexureBlock(IContainer container)
    {
        var a = model.Annexure!;

        container.Column(column =>
        {
            column.Item().Element(c => BlockTitle(
                c, "Annexure — indicative return",
                $"Computed on the basic sale price under the {model.PaymentPlanName}. "
                    + "Indicative only, and subject to the conditions stated below."));

            /* ---------------- terms of the offer ---------------- */

            var terms = new List<(string Label, string Value)>();

            if (a.HasAssuredReturn)
            {
                terms.Add(("Assured return",
                    $"{Percent(a.AssuredReturnPercent)} of BSP per year "
                        + $"× {a.AssuredReturnYears:0.##} yrs"));
            }

            if (a.HasBuyBack)
            {
                terms.Add(("Buy-back",
                    $"{Percent(a.BuyBackPercentPerYear)} per year, exercisable after "
                        + $"{a.BuyBackEligibleAfterYears:0.##} yrs"));
            }

            if (a.HasRentalYield)
            {
                terms.Add(("Indicative rent",
                    $"{Money(a.IndicativeRentPerSqftPerMonth)} / sq ft / month"));
            }

            terms.Add(("Total return", Rupees(a.TotalEarned)));

            if (a.ReturnOnInvestment > 0)
            {
                terms.Add(("Return on investment",
                    $"{Percent(a.ReturnOnInvestment)} over {a.HorizonYears:0.##} yrs"));
            }

            if (a.AnnualisedReturn > 0)
            {
                terms.Add(("Annualised", $"{Percent(a.AnnualisedReturn)} per year, simple"));
            }

            if (a.HasRentalYield && a.GrossRentalYield > 0)
            {
                terms.Add(("Gross rental yield", Percent(a.GrossRentalYield)));
            }

            column.Item().Border(Frame).BorderColor(Line).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                // The last row is whatever the count leaves over, so its cells
                // take no bottom rule and the box closes on its own border.
                var lastRowStart = terms.Count - (terms.Count % 3 == 0 ? 3 : terms.Count % 3);

                for (var i = 0; i < terms.Count; i++)
                {
                    table.Cell()
                        .BorderRight(i % 3 == 2 ? 0 : Thread)
                        .BorderBottom(i >= lastRowStart ? 0 : Thread)
                        .BorderColor(Hair)
                        .PaddingHorizontal(11).PaddingVertical(6).Column(c =>
                    {
                        c.Item().Text(terms[i].Label.ToUpperInvariant())
                            .FontSize(6.5f).FontColor(Faint).LetterSpacing(0.1f);

                        c.Item().PaddingTop(2).Text(terms[i].Value)
                            .FontSize(8.5f).SemiBold().FontColor(Ink).LineHeight(1.25f);
                    });
                }
            });

            /* ---------------- year by year ---------------- */

            if (a.Schedule.Count > 0)
            {
                column.Item().PaddingTop(11).Element(c => BlockTitle(
                    c, "Year by year",
                    a.HasAssuredReturn ? null : "Indicative rental income"));

                column.Item().Border(Frame).BorderColor(Line).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(30);
                        columns.RelativeColumn(3.2f);
                        if (a.HasAssuredReturn) columns.RelativeColumn(3.2f);
                        if (a.HasRentalYield) columns.RelativeColumn(3.2f);
                        columns.RelativeColumn(3.4f);
                    });

                    table.Header(header =>
                    {
                        Head(header.Cell(), "Yr");
                        Head(header.Cell(), "Period ending");

                        if (a.HasAssuredReturn)
                        {
                            Head(header.Cell(), "Assured return", right: true);
                        }

                        if (a.HasRentalYield)
                        {
                            Head(header.Cell(), "Rental income", right: true);
                        }

                        Head(header.Cell(), "Cumulative", right: true, last: true);
                    });

                    foreach (var year in a.Schedule)
                    {
                        IContainer Cell(bool right = false, bool last = false)
                        {
                            var cell = table.Cell()
                                .BorderRight(last ? 0 : Thread).BorderBottom(Thread).BorderColor(Hair)
                                .PaddingVertical(5.5f)
                                .PaddingLeft(right ? 4 : 9).PaddingRight(right ? 9 : 4);

                            return right ? cell.AlignRight() : cell;
                        }

                        Cell().Text(year.Year.ToString()).FontSize(7.5f).FontColor(Faint);

                        Cell().Text(year.PeriodEnd.ToString("MMM yyyy")
                                + (year.YearFraction < 1 ? "   (part year)" : ""))
                            .FontSize(8.5f).FontColor(Body);

                        if (a.HasAssuredReturn)
                        {
                            Cell(right: true).Text(Money(year.AssuredReturn))
                                .FontSize(8.5f).FontColor(Ink);
                        }

                        if (a.HasRentalYield)
                        {
                            Cell(right: true).Text(Money(year.RentalIncome))
                                .FontSize(8.5f).FontColor(Body);
                        }

                        Cell(right: true, last: true).Text(Money(year.CumulativeReturn))
                            .FontSize(8.5f).SemiBold().FontColor(Ink);
                    }
                });

                if (a.HasAssuredReturn && a.HasRentalYield)
                {
                    column.Item().PaddingTop(4).Text(
                            "The assured return and the rental income are alternatives, not a sum — "
                            + "the rent column states what the unit would earn on the open market "
                            + "once the assured term ends.")
                        .FontSize(7).FontColor(Muted).LineHeight(1.4f);
                }
            }

            /* ---------------- the ledger ---------------- */

            column.Item().PaddingTop(11).Border(Frame).BorderColor(Line).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(9);
                    columns.RelativeColumn(4);
                });

                void Entry(string label, string value)
                {
                    table.Cell().BorderRight(Thread).BorderBottom(Thread).BorderColor(Hair)
                        .PaddingHorizontal(11).PaddingVertical(5.5f)
                        .Text(label).FontSize(8).FontColor(Body);

                    table.Cell().BorderBottom(Thread).BorderColor(Hair)
                        .PaddingHorizontal(11).PaddingVertical(5.5f).AlignRight()
                        .Text(value).FontSize(8.5f).FontColor(Ink);
                }

                Entry("Basic sale price — the capital this return is computed on",
                    Money(a.BasicSalePrice));

                if (a.HasAssuredReturn)
                {
                    Entry($"Assured return over {a.AssuredReturnYears:0.##} years",
                        Money(a.AssuredReturnAmount));
                }

                if (a.HasBuyBack)
                {
                    Entry("Buy-back appreciation", Money(a.BuyBackAmount));
                    Entry("Buy-back value — basic price plus appreciation", Money(a.BuyBackValue));
                }

                if (a.HasRentalYield)
                {
                    Entry("Indicative rent, per month", Money(a.IndicativeRentPerMonth));
                    Entry("Indicative rent, per year", Money(a.IndicativeRentPerYear));
                }

                table.Cell().BorderTop(Rule).BorderRight(Thread).BorderColor(Line)
                    .PaddingHorizontal(11).PaddingVertical(6)
                    .Text(a.HasAssuredReturn && a.HasBuyBack
                        ? "Total earned — assured return and buy-back"
                        : a.HasBuyBack
                            ? "Total earned — buy-back appreciation"
                            : "Total earned — assured return")
                    .FontSize(8.5f).Bold().FontColor(Ink);

                table.Cell().BorderTop(Rule).BorderColor(Line)
                    .PaddingHorizontal(11).PaddingVertical(6).AlignRight()
                    .Text(Money(a.TotalEarned))
                    .FontSize(9.5f).Bold().FontColor(Ink);
            });

            if (!string.IsNullOrWhiteSpace(a.Conditions))
            {
                column.Item().PaddingTop(11).Element(c => Conditions(c, a.Conditions!));
            }
        });
    }

    private static void Conditions(IContainer container, string text)
    {
        container.Border(Frame).BorderColor(Line)
            .PaddingHorizontal(11).PaddingVertical(8).Column(column =>
        {
            column.Item().Text("CONDITIONS")
                .FontSize(6.5f).FontColor(Faint).LetterSpacing(0.1f);

            var index = 1;

            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                column.Item().PaddingTop(3).Row(row =>
                {
                    row.ConstantItem(16).Text($"{index}.").FontSize(7.5f).FontColor(Muted);
                    row.RelativeItem().Text(line.Trim())
                        .FontSize(7.5f).FontColor(Body).LineHeight(1.45f);
                });

                index++;
            }
        });
    }

    /* ------------------------------------------------------------------ *
     * Remarks, terms, signatures
     * ------------------------------------------------------------------ */

    private void RemarksBlock(IContainer container)
    {
        container.Border(Frame).BorderColor(Line)
            .PaddingHorizontal(11).PaddingVertical(8).Column(column =>
        {
            column.Item().Text("REMARKS")
                .FontSize(6.5f).FontColor(Faint).LetterSpacing(0.1f);

            column.Item().PaddingTop(3).Text(model.Notes!)
                .FontSize(8).FontColor(Body).LineHeight(1.5f);
        });
    }

    private void TermsBlock(IContainer container)
    {
        var terms = (string.IsNullOrWhiteSpace(model.TermsAndConditions)
                ? DefaultTerms
                : model.TermsAndConditions!)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToList();

        container.BorderLeft(Frame).BorderRight(Frame).BorderTop(Frame).BorderColor(Line)
            .PaddingHorizontal(11).PaddingVertical(8).Column(column =>
        {
            column.Item().Text("TERMS & CONDITIONS")
                .FontSize(6.5f).FontColor(Faint).LetterSpacing(0.1f);

            // Two columns, because eight clauses set to the full measure eat a
            // third of a page to say what fits comfortably in a sixth.
            var half = (terms.Count + 1) / 2;

            column.Item().PaddingTop(4).Row(row =>
            {
                void Half(int from, int count, bool pad)
                {
                    row.RelativeItem().PaddingRight(pad ? 14 : 0).Column(c =>
                    {
                        for (var i = from; i < from + count && i < terms.Count; i++)
                        {
                            c.Item().PaddingBottom(4).Row(line =>
                            {
                                line.ConstantItem(16).Text($"{i + 1}.")
                                    .FontSize(7.5f).FontColor(Muted);

                                line.RelativeItem().Text(terms[i])
                                    .FontSize(7.5f).FontColor(Body).LineHeight(1.45f);
                            });
                        }
                    });
                }

                Half(0, half, pad: true);
                Half(half, terms.Count - half, pad: false);
            });
        });
    }

    private const string DefaultTerms =
        """
        This quotation is an offer to sell and does not constitute an allotment. Allotment is subject to the execution of the agreement to sell.
        Prices are valid until the date shown above and are subject to revision without notice thereafter.
        GST is charged at the rate prevailing on the date of each instalment and any statutory revision will apply.
        Stamp duty, registration charges and any statutory levies not itemised above are payable separately at actuals.
        Preferential location charges, where applicable, are included in the applicable rate and are not discounted.
        Refundable deposits carry no interest and are returned on the terms set out in the agreement to sell.
        Cheques and transfers are to be drawn in favour of the developer named on this document. No cash is accepted.
        Delay in payment beyond the due date attracts interest as set out in the agreement to sell.
        """;

    /// <summary>Closes the terms box, so the two read as one panel.</summary>
    private void Signatures(IContainer container)
    {
        container.ShowEntire()
            .BorderLeft(Frame).BorderRight(Frame).BorderBottom(Frame).BorderTop(Thread)
            .BorderColor(Line)
            .Row(row =>
        {
            row.RelativeItem().BorderRight(Thread).BorderColor(Hair)
                .PaddingHorizontal(11).PaddingVertical(8).Column(left =>
            {
                left.Item().Text("ACCEPTED BY")
                    .FontSize(6.5f).FontColor(Faint).LetterSpacing(0.1f);

                left.Item().PaddingTop(3).Text(model.CustomerName)
                    .FontSize(9).SemiBold().FontColor(Ink);

                left.Item().Height(30);
                left.Item().Text("Signature & date").FontSize(7.5f).FontColor(Muted);
            });

            row.RelativeItem().PaddingHorizontal(11).PaddingVertical(8).Column(right =>
            {
                right.Item().AlignRight().Text($"for {model.DeveloperName}")
                    .FontSize(9).SemiBold().FontColor(Ink);

                if (!string.IsNullOrWhiteSpace(model.SalesPersonName))
                {
                    right.Item().PaddingTop(2).AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Muted));
                        t.Span($"Raised by {model.SalesPersonName}");

                        if (!string.IsNullOrWhiteSpace(model.SalesPersonId))
                        {
                            t.Span($"   |   User {model.SalesPersonId}");
                        }
                    });
                }

                right.Item().Height(model.SalesPersonName is null ? 30 : 19);
                right.Item().AlignRight().Text("Authorised Signatory")
                    .FontSize(7.5f).SemiBold().FontColor(Ink);
            });
        });
    }

    /* ------------------------------------------------------------------ *
     * Footer
     * ------------------------------------------------------------------ */

    private void PageFooter(IContainer container)
    {
        container.PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(
                    $"{model.QuoteNumber}   |   {model.ProjectName} · {model.UnitNumber}"
                    + "   |   This is a computer-generated document.")
                .FontSize(6.5f).FontColor(Faint);

            row.AutoItem().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(6.5f).FontColor(Faint));
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
            });
        });
    }

    /* ------------------------------------------------------------------ *
     * Shared bits
     * ------------------------------------------------------------------ */

    /// <summary>A block's caption, set above its box rather than inside it.</summary>
    private static void BlockTitle(IContainer container, string title, string? note)
    {
        container.PaddingBottom(4).Column(column =>
        {
            column.Item().Text(title.ToUpperInvariant())
                .FontSize(8).Bold().FontColor(Ink).LetterSpacing(0.1f);

            if (!string.IsNullOrWhiteSpace(note))
            {
                column.Item().PaddingTop(1).Text(note!)
                    .FontSize(7).FontColor(Muted).LineHeight(1.35f);
            }
        });
    }

    /// <summary>A column heading: small caps over a rule, no fill.</summary>
    private static void Head(IContainer cell, string text, bool right = false, bool last = false)
    {
        var container = cell
            .BorderRight(last ? 0 : Thread).BorderBottom(Rule).BorderColor(Line)
            .PaddingVertical(5)
            .PaddingLeft(right ? 4 : 9).PaddingRight(right ? 9 : 4);

        (right ? container.AlignRight() : container)
            .Text(text.ToUpperInvariant())
            .FontSize(6.75f).SemiBold().FontColor(Ink).LetterSpacing(0.1f);
    }

    /// <summary>
    /// A totals cell — a row of the same table, closed with a rule above it so
    /// it reads as the foot of the column rather than as another entry in it.
    /// </summary>
    private static void Foot(
        IContainer cell, string text, bool right = false, bool last = false, bool strong = false)
    {
        var container = cell
            .BorderTop(Rule).BorderRight(last ? 0 : Thread).BorderColor(Line)
            .PaddingVertical(6)
            .PaddingLeft(right ? 4 : 9).PaddingRight(right ? 9 : 4);

        if (text.Length == 0)
        {
            container.Text("");
            return;
        }

        (right ? container.AlignRight() : container).Text(text)
            .FontSize(strong ? 9.5f : 8.5f).Bold().FontColor(Ink);
    }
}
