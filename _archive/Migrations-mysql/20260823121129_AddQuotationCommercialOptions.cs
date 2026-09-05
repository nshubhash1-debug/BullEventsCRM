using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BullRealty.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationCommercialOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "DefaultDiscount",
                table: "QuotationTemplates",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ApplyDiscount",
                table: "QuotationTemplates",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeAssuredReturn",
                table: "QuotationTemplates",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeBuyBack",
                table: "QuotationTemplates",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeRentalYield",
                table: "QuotationTemplates",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxPercent",
                table: "Quotations",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "StandardDiscountPercent",
                table: "Quotations",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountPercent",
                table: "Quotations",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "AssuredReturnAmount",
                table: "Quotations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AssuredReturnPercent",
                table: "Quotations",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AssuredReturnYears",
                table: "Quotations",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BuyBackAmount",
                table: "Quotations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BuyBackEligibleAfterYears",
                table: "Quotations",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BuyBackHorizonYears",
                table: "Quotations",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BuyBackPercentPerYear",
                table: "Quotations",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BuyBackValue",
                table: "Quotations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "DiscountApplied",
                table: "Quotations",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountLabel",
                table: "Quotations",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "GrossRentalYield",
                table: "Quotations",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RentPerMonth",
                table: "Quotations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RentPerSqftPerMonth",
                table: "Quotations",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ReturnConditions",
                table: "Quotations",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnHorizonYears",
                table: "Quotations",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnOnInvestment",
                table: "Quotations",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnsTotalEarned",
                table: "Quotations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "ShowAssuredReturn",
                table: "Quotations",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowBuyBack",
                table: "Quotations",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowRentalYield",
                table: "Quotations",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<decimal>(
                name: "Percent",
                table: "QuotationMilestones",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountPercent",
                table: "QuotationLines",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxRate",
                table: "QuotationCharges",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxRate",
                table: "PaymentPlans",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "StandardDiscount",
                table: "PaymentPlans",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountTolerance",
                table: "PaymentPlans",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "BuyBackPercentPerYear",
                table: "PaymentPlans",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "BuyBackEligibleAfterYears",
                table: "PaymentPlans",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "AssuredReturnYears",
                table: "PaymentPlans",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "AssuredReturnPercent",
                table: "PaymentPlans",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Percent",
                table: "PaymentPlanMilestones",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxRate",
                table: "ChargeHeads",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            // Quotations raised before the switch existed were priced with the
            // discount their plan granted. Anything else reprints them as
            // "no discount applied" over a visibly discounted rate.
            migrationBuilder.Sql("UPDATE Quotations SET DiscountApplied = 1;");

            // The returns snapshot starts empty, so an older quotation would
            // print no annexure at all. Seeding it from the plan it was raised
            // against keeps every already-issued investor quotation printing
            // the page it printed yesterday.
            migrationBuilder.Sql("""
                UPDATE Quotations q
                JOIN PaymentPlans p ON p.Id = q.PaymentPlanId
                SET q.ShowAssuredReturn        = p.AssuredReturnPercent > 0,
                    q.AssuredReturnPercent     = p.AssuredReturnPercent,
                    q.AssuredReturnYears       = p.AssuredReturnYears,
                    q.AssuredReturnAmount      = ROUND(q.Subtotal * p.AssuredReturnPercent * p.AssuredReturnYears, 2),
                    q.ShowBuyBack              = p.BuyBackPercentPerYear > 0,
                    q.BuyBackPercentPerYear    = p.BuyBackPercentPerYear,
                    q.BuyBackEligibleAfterYears = p.BuyBackEligibleAfterYears,
                    q.BuyBackHorizonYears      = p.AssuredReturnYears,
                    q.BuyBackAmount            = ROUND(q.Subtotal * p.BuyBackPercentPerYear * p.AssuredReturnYears, 2),
                    q.BuyBackValue             = q.Subtotal + ROUND(q.Subtotal * p.BuyBackPercentPerYear * p.AssuredReturnYears, 2),
                    q.ShowRentalYield          = p.IndicativeRentPerSqftPerMonth > 0,
                    q.RentPerSqftPerMonth      = p.IndicativeRentPerSqftPerMonth,
                    q.RentPerMonth             = ROUND(q.SaleableArea * p.IndicativeRentPerSqftPerMonth, 2),
                    q.ReturnHorizonYears       = p.AssuredReturnYears,
                    q.ReturnConditions         = p.ReturnConditions
                WHERE p.AssuredReturnPercent > 0
                   OR p.BuyBackPercentPerYear > 0
                   OR p.IndicativeRentPerSqftPerMonth > 0;
                """);

            migrationBuilder.Sql("""
                UPDATE Quotations
                SET ReturnsTotalEarned = AssuredReturnAmount + BuyBackAmount,
                    ReturnOnInvestment = CASE
                        WHEN Subtotal > 0
                        THEN ROUND((AssuredReturnAmount + BuyBackAmount) / Subtotal, 6)
                        ELSE 0 END,
                    GrossRentalYield = CASE
                        WHEN Subtotal > 0 THEN ROUND(RentPerMonth * 12 / Subtotal, 6)
                        ELSE 0 END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplyDiscount",
                table: "QuotationTemplates");

            migrationBuilder.DropColumn(
                name: "IncludeAssuredReturn",
                table: "QuotationTemplates");

            migrationBuilder.DropColumn(
                name: "IncludeBuyBack",
                table: "QuotationTemplates");

            migrationBuilder.DropColumn(
                name: "IncludeRentalYield",
                table: "QuotationTemplates");

            migrationBuilder.DropColumn(
                name: "AssuredReturnAmount",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "AssuredReturnPercent",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "AssuredReturnYears",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "BuyBackAmount",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "BuyBackEligibleAfterYears",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "BuyBackHorizonYears",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "BuyBackPercentPerYear",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "BuyBackValue",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "DiscountApplied",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "DiscountLabel",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "GrossRentalYield",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "RentPerMonth",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "RentPerSqftPerMonth",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "ReturnConditions",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "ReturnHorizonYears",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "ReturnOnInvestment",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "ReturnsTotalEarned",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "ShowAssuredReturn",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "ShowBuyBack",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "ShowRentalYield",
                table: "Quotations");

            migrationBuilder.AlterColumn<decimal>(
                name: "DefaultDiscount",
                table: "QuotationTemplates",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxPercent",
                table: "Quotations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "StandardDiscountPercent",
                table: "Quotations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountPercent",
                table: "Quotations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "Percent",
                table: "QuotationMilestones",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountPercent",
                table: "QuotationLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxRate",
                table: "QuotationCharges",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxRate",
                table: "PaymentPlans",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "StandardDiscount",
                table: "PaymentPlans",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountTolerance",
                table: "PaymentPlans",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "BuyBackPercentPerYear",
                table: "PaymentPlans",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "BuyBackEligibleAfterYears",
                table: "PaymentPlans",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "AssuredReturnYears",
                table: "PaymentPlans",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "AssuredReturnPercent",
                table: "PaymentPlans",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "Percent",
                table: "PaymentPlanMilestones",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxRate",
                table: "ChargeHeads",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);
        }
    }
}
