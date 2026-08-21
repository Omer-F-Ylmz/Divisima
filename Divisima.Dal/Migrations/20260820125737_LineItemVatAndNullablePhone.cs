using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Divisima.Dal.Migrations
{
    /// <inheritdoc />
    public partial class LineItemVatAndNullablePhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "vat_rate",
                table: "products",
                type: "decimal(5,4)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "phone",
                table: "customers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_rate",
                table: "categories",
                type: "decimal(5,4)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "phone",
                table: "addresses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateTable(
                name: "invoice_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    invoice_id = table.Column<int>(type: "int", nullable: false),
                    product_id = table.Column<int>(type: "int", nullable: false),
                    product_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    line_subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    vat_rate = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    vat_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    line_total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_invoice_id",
                table: "invoice_items",
                column: "invoice_id");

            // ── KDV ORANI SEED'I ─────────────────────────────────────────────────────────
            // Bu migration tarihinde katalog TAMAMEN GIYIM: seed'deki uc kategori
            // (Kadin Giyim, Elbise, Dis Giyim) ve turetilenleri giyim urunudur -> %10.
            //
            // Neden ACIKCA yaziliyor: oran NULL kalirsa efektif oran EInvoice:KdvRate'e
            // (%20, yuksek-guvenli taraf) duser. Yani tek bir kategori unutulsa giyim urunu
            // %20 ile faturalanir - musteriye fazla KDV yansir. "Fallback'e dusen kategori
            // kalmasin" kurali bu yuzden.
            //
            // DIKKAT (ileride aksesuar eklenince): yeni aksesuar kategorileri 0.20 ile ACIKCA
            // olusturulmalidir. Bu UPDATE yalnizca vat_rate'i NULL olan MEVCUT satirlara
            // dokunur; sonradan eklenen kategorileri etkilemez.
            migrationBuilder.Sql(
                "UPDATE categories SET vat_rate = 0.1000 WHERE vat_rate IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoice_items");

            migrationBuilder.DropColumn(
                name: "vat_rate",
                table: "products");

            migrationBuilder.DropColumn(
                name: "vat_rate",
                table: "categories");

            migrationBuilder.AlterColumn<string>(
                name: "phone",
                table: "customers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "phone",
                table: "addresses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}
