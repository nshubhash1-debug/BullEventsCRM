using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <summary>
    /// Moves the custom-field bags from text to jsonb.
    ///
    /// Hand-written rather than scaffolded: Postgres has no implicit cast from
    /// text to jsonb, so the generated <c>ALTER COLUMN … TYPE jsonb</c> fails
    /// with "cannot be cast automatically". The <c>USING</c> clause below is the
    /// whole difference, and it is why this file is not regenerated.
    ///
    /// Worth the type change twice over: Postgres can look inside a jsonb column
    /// (the key-existence probe the customisation screen uses is an indexable
    /// <c>?</c> operator), and jsonb is skipped by the case-folding collation
    /// pass — a non-deterministic collation makes a column reject LIKE outright,
    /// which is how the text version failed in the first place.
    /// </summary>
    public partial class CustomFieldsAsJsonb : Migration
    {
        private static readonly string[] Tables = ["Leads", "Contacts", "Opportunities"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                // The collation has to go first: it belongs to text, and the
                // type change rejects the column while it is still applied.
                migrationBuilder.Sql($"""
                    ALTER TABLE "{table}"
                        ALTER COLUMN "CustomFields" TYPE jsonb
                        USING NULLIF("CustomFields", '')::jsonb;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE "{table}"
                        ALTER COLUMN "CustomFields" TYPE text
                        USING "CustomFields"::text;

                    ALTER TABLE "{table}"
                        ALTER COLUMN "CustomFields" SET DATA TYPE text COLLATE "crm_ci";
                    """);
            }
        }
    }
}
