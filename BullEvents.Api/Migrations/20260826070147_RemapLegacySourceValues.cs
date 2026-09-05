using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <summary>
    /// Rewrites the two lead-source values the events vocabulary retired.
    ///
    /// <c>LeadSources.All</c> is what every write validates against, so a row
    /// still carrying <c>PropertyPortal</c> would be readable but not editable —
    /// saving any change to it would fail on a field the user never touched.
    /// Data-only; no schema moves.
    ///
    /// Contacts and Opportunities carry a source from the same list and are
    /// remapped alongside for the same reason.
    /// </summary>
    public partial class RemapLegacySourceValues : Migration
    {
        /// <summary>Old value to new, applied to every table holding a lead source.</summary>
        private static readonly (string From, string To)[] SourceMap =
        [
            ("PropertyPortal", "EventPortal"),
            ("ChannelPartner", "VendorPartner"),
        ];

        private static readonly string[] Tables = ["Leads", "Contacts", "Opportunities"];

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                foreach (var (from, to) in SourceMap)
                {
                    migrationBuilder.Sql(
                        $"""UPDATE "{table}" SET "Source" = '{to}' WHERE "Source" = '{from}';""");
                }
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                foreach (var (from, to) in SourceMap)
                {
                    migrationBuilder.Sql(
                        $"""UPDATE "{table}" SET "Source" = '{from}' WHERE "Source" = '{to}';""");
                }
            }
        }
    }
}
