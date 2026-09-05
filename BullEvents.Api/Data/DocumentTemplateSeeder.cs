using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// The letters a developer actually sends, ready to edit.
///
/// Seeded rather than left blank because an empty template library is a feature
/// nobody switches on: writing a demand letter from nothing, in HTML, is not
/// what a collections manager is going to do on a Tuesday. These are real
/// drafts with the right clauses in them — the penal interest sentence, the
/// TDS line, the "cheques in favour of" instruction — and the point is that
/// somebody edits the wording rather than invents the document.
///
/// Additive only. A template somebody has edited is never overwritten, and one
/// they deleted is not resurrected.
/// </summary>
public static class DocumentTemplateSeeder
{
    public static async Task SeedAsync(AppDbContext db, int companyId, CancellationToken ct = default)
    {
        var existing = await db.DocumentTemplates
            .IgnoreQueryFilters()
            .Where(t => t.CompanyId == companyId)
            .Select(t => t.Kind)
            .ToListAsync(ct);

        var have = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var (kind, name, subject, body) in Library)
        {
            if (have.Contains(kind)) continue;

            db.DocumentTemplates.Add(new DocumentTemplate
            {
                CompanyId = companyId,
                Kind = kind,
                Name = name,
                Subject = subject,
                Body = body.Trim(),
                IsDefault = true,
                IsSystem = true,
                IsActive = true,
            });

            added++;
        }

        if (added > 0) await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The shell every letter sits in — letterhead, body, sign-off.
    ///
    /// Concatenated rather than interpolated. A raw interpolated string reads
    /// <c>{{…}}</c> as a C# hole, which is exactly the delimiter the merge
    /// tokens use — the two languages collide, and the compiler wins.
    /// </summary>
    private static string Wrap(string inner) =>
        Header + inner + Footer;

    private const string Header = """
        <div style="font-family:Segoe UI,Arial,sans-serif;font-size:13.5px;line-height:1.6;color:#1a1a1a;max-width:720px">
          <div style="border-bottom:2px solid #0176d3;padding-bottom:10px;margin-bottom:18px">
            <div style="font-size:17px;font-weight:700">{{company.name}}</div>
            <div style="font-size:12px;color:#5c5c5c">{{company.address}}</div>
          </div>

          <div style="text-align:right;font-size:12px;color:#5c5c5c;margin-bottom:14px">
            Ref: {{document.number}}<br>Date: {{today}}
          </div>

        """;

    private const string Footer = """

          <div style="margin-top:26px">
            For <strong>{{company.name}}</strong><br><br><br>
            Authorised Signatory
          </div>

          <div style="margin-top:22px;padding-top:10px;border-top:1px solid #eee;font-size:11px;color:#8a8a8a">
            This is a computer-generated document issued against booking {{booking.number}}.
          </div>
        </div>
        """;

    private static readonly (string Kind, string Name, string Subject, string Body)[] Library =
    [
        /* ---------------- demand ---------------- */
        (TemplateKinds.DemandLetter,
         "Demand letter",
         "Payment due — {{unit.project}} {{unit.number}} ({{demand.label}})",
         Wrap("""
          <p>To,<br>
          <strong>{{customer.salutation}} {{customer.name}}</strong><br>
          {{customer.address}}</p>

          <p><strong>Sub: Demand for payment — {{unit.project}}, Unit {{unit.number}}</strong></p>

          <p>Dear {{customer.name}},</p>

          <p>With reference to your booking <strong>{{booking.number}}</strong> dated
          {{booking.date}} for Unit <strong>{{unit.number}}</strong> ({{unit.configuration}},
          {{unit.area}}) in {{unit.tower}}, {{unit.project}}, we write to inform you that the
          following instalment has fallen due as per the agreed payment plan.</p>

          <table style="width:100%;border-collapse:collapse;font-size:13px;margin:14px 0">
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa;width:45%">Demand reference</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{demand.number}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Towards</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{demand.label}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Amount payable</td>
                <td style="padding:7px 9px;border:1px solid #ddd"><strong>{{demand.amount}}</strong></td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Payable on or before</td>
                <td style="padding:7px 9px;border:1px solid #ddd"><strong>{{demand.dueDate}}</strong></td></tr>
          </table>

          <p>You are requested to remit the above amount on or before the due date. Payments
          received after the due date will attract interest at <strong>{{demand.interestRate}}</strong>
          calculated on a day-to-day basis on the outstanding amount, in terms of the agreement.</p>

          <p><strong>Tax deducted at source:</strong> where applicable under Section 194-IA, kindly
          deduct TDS at 1% and remit the balance, forwarding the challan so that your account is
          credited with the gross amount.</p>

          <p>Cheques and demand drafts may be drawn in favour of <strong>{{company.name}}</strong>.
          Kindly quote {{booking.number}} on the reverse of the instrument.</p>

          <p style="margin-top:18px"><strong>Your account as it stands today</strong></p>
          {{table.outstanding}}

          <p>If payment has already been made, kindly ignore this notice and share the payment
          details so that we may update our records.</p>
          """)),

        /* ---------------- reminder ---------------- */
        (TemplateKinds.ReminderLetter,
         "Payment reminder",
         "Overdue: {{demand.label}} — {{unit.project}} {{unit.number}}",
         Wrap("""
          <p>To,<br>
          <strong>{{customer.salutation}} {{customer.name}}</strong></p>

          <p><strong>Sub: Overdue payment — {{unit.project}}, Unit {{unit.number}}</strong></p>

          <p>Dear {{customer.name}},</p>

          <p>Our records show that the instalment towards <strong>{{demand.label}}</strong>
          under demand {{demand.number}}, which fell due on <strong>{{demand.dueDate}}</strong>,
          remains unpaid as on date.</p>

          <table style="width:100%;border-collapse:collapse;font-size:13px;margin:14px 0">
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa;width:45%">Principal outstanding</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{demand.outstanding}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Days past due</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{demand.daysLate}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Interest accrued at {{demand.interestRate}}</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{demand.interest}}</td></tr>
          </table>

          <p>Interest continues to accrue day by day until the amount is received. We request you
          to clear the outstanding at the earliest to avoid further charges, and to keep the
          construction and handover schedule of your unit on track.</p>

          <p>Should you be facing a genuine difficulty, please write to us — a revised schedule
          can often be arranged, and doing so is always better than letting interest accumulate.</p>
          """)),

        /* ---------------- allotment ---------------- */
        (TemplateKinds.AllotmentLetter,
         "Allotment letter",
         "Allotment of Unit {{unit.number}} — {{unit.project}}",
         Wrap("""
          <p>To,<br>
          <strong>{{customer.salutation}} {{customer.name}}</strong><br>
          {{customer.address}}</p>

          <p style="text-align:center;font-size:15px;font-weight:700;margin:18px 0">
            LETTER OF ALLOTMENT
          </p>

          <p>Dear {{customer.name}},</p>

          <p>We are pleased to confirm the allotment of the following unit in your favour,
          pursuant to your application and the booking amount received by us.</p>

          <table style="width:100%;border-collapse:collapse;font-size:13px;margin:14px 0">
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa;width:45%">Booking reference</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{booking.number}} dated {{booking.date}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Project</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{unit.project}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Unit</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{unit.number}}, {{unit.tower}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Configuration / area</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{unit.configuration}} · {{unit.area}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Agreement value</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{booking.agreementValue}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Total consideration</td>
                <td style="padding:7px 9px;border:1px solid #ddd"><strong>{{booking.grandTotal}}</strong></td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Co-applicants</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{customer.coApplicants}}</td></tr>
          </table>

          <p><strong>Payment schedule</strong></p>
          {{table.schedule}}

          <p>This allotment is subject to the terms of the Agreement for Sale to be executed
          between us, and to payment of each instalment on its due date. Kindly attend our office
          for execution and registration of the agreement within the period prescribed under the
          applicable law.</p>

          <p>We thank you for choosing {{unit.project}} and look forward to a long association.</p>
          """)),

        /* ---------------- welcome ---------------- */
        (TemplateKinds.WelcomeLetter,
         "Welcome letter",
         "Welcome to {{unit.project}}",
         Wrap("""
          <p>Dear {{customer.salutation}} {{customer.name}},</p>

          <p>Welcome to <strong>{{unit.project}}</strong>. We are delighted that you have chosen
          to make Unit {{unit.number}} your own.</p>

          <p>Your booking reference is <strong>{{booking.number}}</strong>. Please quote it in all
          correspondence and on every payment instrument — it is how we identify your account.</p>

          <p><strong>What happens next</strong></p>
          <ol>
            <li>Our team will collect the KYC papers for every applicant on the booking.</li>
            <li>The Agreement for Sale will be drawn up, franked and registered.</li>
            <li>Instalments fall due as construction reaches each stage. We will write to you
                before each one.</li>
            <li>On completion we will offer possession, walk the unit with you, and hand over.</li>
          </ol>

          <p><strong>Your payment schedule</strong></p>
          {{table.schedule}}

          <p>If anything is unclear, please write to us. It is far easier to answer a question
          now than to correct a misunderstanding later.</p>
          """)),

        /* ---------------- receipt ---------------- */
        (TemplateKinds.PaymentReceipt,
         "Payment receipt",
         "Receipt {{receipt.number}} — {{unit.project}} {{unit.number}}",
         Wrap("""
          <p style="text-align:center;font-size:15px;font-weight:700;margin:6px 0 18px">RECEIPT</p>

          <table style="width:100%;border-collapse:collapse;font-size:13px;margin:14px 0">
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa;width:45%">Receipt number</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{receipt.number}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Received from</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{customer.salutation}} {{customer.name}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Against</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{booking.number}} · Unit {{unit.number}}, {{unit.project}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Mode</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{receipt.mode}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Received on</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{receipt.date}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">TDS withheld by payer</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{receipt.tds}}</td></tr>
            <tr><td style="padding:9px;border:1px solid #ddd;background:#f0f7ff"><strong>Amount credited</strong></td>
                <td style="padding:9px;border:1px solid #ddd;background:#f0f7ff"><strong>{{receipt.amount}}</strong></td></tr>
          </table>

          <p style="font-size:12px;color:#5c5c5c">The amount credited includes any tax deducted at
          source by the payer. Receipts against cheques and demand drafts are subject to
          realisation.</p>

          <p><strong>Your account after this payment</strong></p>
          {{table.outstanding}}
          """)),

        /* ---------------- statement ---------------- */
        (TemplateKinds.StatementOfAccount,
         "Statement of account",
         "Statement of account — {{booking.number}}",
         Wrap("""
          <p><strong>{{customer.salutation}} {{customer.name}}</strong><br>
          {{booking.number}} · Unit {{unit.number}}, {{unit.tower}}, {{unit.project}}</p>

          <p style="text-align:center;font-size:15px;font-weight:700;margin:16px 0">
            STATEMENT OF ACCOUNT
          </p>

          <table style="width:100%;border-collapse:collapse;font-size:13px;margin:12px 0">
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa;width:45%">Total consideration</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{booking.grandTotal}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Received to date</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{booking.received}}</td></tr>
            <tr><td style="padding:9px;border:1px solid #ddd;background:#f0f7ff"><strong>Outstanding</strong></td>
                <td style="padding:9px;border:1px solid #ddd;background:#f0f7ff"><strong>{{booking.outstanding}}</strong></td></tr>
          </table>

          <p><strong>Ledger</strong></p>
          {{table.ledger}}

          <p><strong>Currently outstanding</strong></p>
          {{table.outstanding}}

          <p style="font-size:12px;color:#5c5c5c">Please examine this statement and report any
          discrepancy within fifteen days, failing which it will be taken as correct.</p>
          """)),

        /* ---------------- possession offer ---------------- */
        (TemplateKinds.PossessionOffer,
         "Offer of possession",
         "Offer of possession — Unit {{unit.number}}, {{unit.project}}",
         Wrap("""
          <p>To,<br>
          <strong>{{customer.salutation}} {{customer.name}}</strong></p>

          <p><strong>Sub: Offer of possession — Unit {{unit.number}}, {{unit.tower}},
          {{unit.project}}</strong></p>

          <p>Dear {{customer.name}},</p>

          <p>We are pleased to inform you that construction of your unit is complete and the
          Occupancy Certificate has been obtained. We hereby offer you possession of Unit
          <strong>{{unit.number}}</strong>.</p>

          <p><strong>Before possession can be handed over, the following must be completed:</strong></p>
          <ol>
            <li>Clearance of all outstanding dues, including any interest.</li>
            <li>Execution and registration of the Sale Deed, where not already done.</li>
            <li>Payment of the maintenance advance and corpus fund.</li>
            <li>Completion of the possession formalities and joint inspection of the unit.</li>
          </ol>

          <p><strong>Amounts outstanding as on date</strong></p>
          {{table.outstanding}}

          <p>Kindly contact our office to schedule the joint inspection. Any defects noted during
          the inspection will be recorded on a snag list and attended to before handover.</p>

          <p>Please note that maintenance charges become payable from the date of this offer,
          whether or not physical possession is taken.</p>
          """)),

        /* ---------------- possession letter ---------------- */
        (TemplateKinds.PossessionLetter,
         "Possession letter",
         "Possession of Unit {{unit.number}} — {{unit.project}}",
         Wrap("""
          <p style="text-align:center;font-size:15px;font-weight:700;margin:6px 0 18px">
            LETTER OF POSSESSION
          </p>

          <p>This is to certify that physical possession of Unit <strong>{{unit.number}}</strong>,
          {{unit.tower}}, {{unit.project}}, admeasuring {{unit.area}}, has been handed over to
          <strong>{{customer.salutation}} {{customer.name}}</strong> under booking
          {{booking.number}}, all dues having been cleared.</p>

          <p>The allottee has inspected the unit and taken possession in its present condition,
          subject to the defect liability period provided under the agreement and applicable law.</p>

          <p>Keys, fittings and the documents listed in the handover checklist have been handed
          over on {{today}}.</p>

          <div style="margin-top:34px;display:flex;justify-content:space-between">
            <div>____________________<br><span style="font-size:12px">Allottee</span></div>
          </div>
          """)),

        /* ---------------- NOC ---------------- */
        (TemplateKinds.NocForLoan,
         "No objection certificate — home loan",
         "NOC for mortgage — Unit {{unit.number}}, {{unit.project}}",
         Wrap("""
          <p style="text-align:center;font-size:15px;font-weight:700;margin:6px 0 18px">
            NO OBJECTION CERTIFICATE
          </p>

          <p>To whomsoever it may concern,</p>

          <p>This is to certify that <strong>{{customer.salutation}} {{customer.name}}</strong>
          has booked Unit <strong>{{unit.number}}</strong>, {{unit.tower}}, in our project
          {{unit.project}}, under booking reference {{booking.number}} dated {{booking.date}},
          for a total consideration of {{booking.grandTotal}}.</p>

          <p>We have <strong>no objection</strong> to the allottee availing a housing loan against
          the said unit and to the lending institution creating a charge or mortgage over it, to
          the extent of the amounts financed.</p>

          <p>This certificate is issued on the express condition that all payments under the
          agreement, whether financed or otherwise, continue to be made on their due dates, and
          that no charge is created over the unit until the full consideration is received by us.</p>

          <p>Disbursements may be made in favour of <strong>{{company.name}}</strong> quoting
          booking reference {{booking.number}}.</p>
          """)),

        /* ---------------- cancellation ---------------- */
        (TemplateKinds.CancellationLetter,
         "Cancellation letter",
         "Cancellation of booking {{booking.number}}",
         Wrap("""
          <p>To,<br>
          <strong>{{customer.salutation}} {{customer.name}}</strong></p>

          <p><strong>Sub: Cancellation of booking {{booking.number}} — Unit {{unit.number}},
          {{unit.project}}</strong></p>

          <p>Dear {{customer.name}},</p>

          <p>This is to confirm the cancellation of your booking for Unit
          <strong>{{unit.number}}</strong>, {{unit.tower}}, {{unit.project}}, with effect from
          {{today}}.</p>

          <p>The amounts received against this booking, the deductions applicable under the terms
          of the agreement, and the net amount refundable are set out in the cancellation
          statement accompanying this letter.</p>

          <p>The refund will be processed to the account from which payment was originally
          received, within the period stipulated in the agreement, after the original allotment
          letter and receipts are returned to us.</p>

          <p>The unit stands released and may be offered to other applicants from the date of
          this letter.</p>
          """)),

        /* ---------------- transfer ---------------- */
        (TemplateKinds.TransferLetter,
         "Transfer letter",
         "Transfer of booking {{booking.number}}",
         Wrap("""
          <p><strong>Sub: Transfer of Unit {{unit.number}}, {{unit.project}}</strong></p>

          <p>This is to confirm that the booking bearing reference
          <strong>{{booking.number}}</strong> in respect of Unit {{unit.number}}, {{unit.tower}},
          {{unit.project}}, has been transferred with our consent, with effect from {{today}}.</p>

          <p>All rights and obligations under the agreement, including the outstanding payment
          schedule, stand transferred to the incoming allottee, who has accepted the same. The
          transfer charges payable to us have been received.</p>

          <p><strong>Outstanding position on the date of transfer</strong></p>
          {{table.outstanding}}

          <p>The outgoing allottee is released from further obligations under the agreement from
          the date of this letter.</p>
          """)),

        /* ---------------- brokerage invoice ---------------- */
        (TemplateKinds.BrokerageInvoice,
         "Brokerage advice",
         "Brokerage advice — {{booking.number}}",
         Wrap("""
          <p style="text-align:center;font-size:15px;font-weight:700;margin:6px 0 18px">
            BROKERAGE ADVICE
          </p>

          <p>Brokerage becoming payable in respect of the following booking:</p>

          <table style="width:100%;border-collapse:collapse;font-size:13px;margin:14px 0">
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa;width:45%">Booking</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{booking.number}} dated {{booking.date}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Unit</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{unit.number}}, {{unit.tower}}, {{unit.project}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Allottee</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{customer.name}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Agreement value</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{booking.agreementValue}}</td></tr>
            <tr><td style="padding:7px 9px;border:1px solid #ddd;background:#fafafa">Collected to date</td>
                <td style="padding:7px 9px;border:1px solid #ddd">{{booking.received}}</td></tr>
          </table>

          <p style="font-size:12px;color:#5c5c5c">Brokerage is computed on the agreement value at
          the agreed slab, is subject to deduction of tax at source under Section 194-H, and
          becomes payable only on the trigger stated in the channel partner agreement.</p>
          """)),
    ];
}
