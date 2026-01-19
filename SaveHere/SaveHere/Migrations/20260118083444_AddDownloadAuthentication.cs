using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaveHere.Migrations
{
    /// <inheritdoc />
    public partial class AddDownloadAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CustomFilename",
                table: "YoutubeDownloadQueueItems",
                newName: "CustomFileName");

            migrationBuilder.AddColumn<string>(
                name: "SubtitleLanguage",
                table: "YoutubeDownloadQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthBearerToken",
                table: "FileDownloadQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthCookies",
                table: "FileDownloadQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthCustomHeaders",
                table: "FileDownloadQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthPassword",
                table: "FileDownloadQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AuthType",
                table: "FileDownloadQueueItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AuthUsername",
                table: "FileDownloadQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BufferSizeKB",
                table: "FileDownloadQueueItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CustomFileName",
                table: "FileDownloadQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableCompression",
                table: "FileDownloadQueueItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ParallelConnections",
                table: "FileDownloadQueueItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsRangeRequests",
                table: "FileDownloadQueueItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UseHttp2",
                table: "FileDownloadQueueItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubtitleLanguage",
                table: "YoutubeDownloadQueueItems");

            migrationBuilder.DropColumn(
                name: "AuthBearerToken",
                table: "FileDownloadQueueItems");

            migrationBuilder.DropColumn(
                name: "AuthCookies",
                table: "FileDownloadQueueItems");

            migrationBuilder.DropColumn(
                name: "AuthCustomHeaders",
                table: "FileDownloadQueueItems");

            migrationBuilder.DropColumn(
                name: "AuthPassword",
                table: "FileDownloadQueueItems");

            migrationBuilder.DropColumn(
                name: "AuthType",
                table: "FileDownloadQueueItems");

            migrationBuilder.DropColumn(
                name: "AuthUsername",
                table: "FileDownloadQueueItems");

            migrationBuilder.DropColumn(
                name: "BufferSizeKB",
                table: "FileDownloadQueueItems");

            migrationBuilder.DropColumn(
                name: "CustomFileName",
                table: "FileDownloadQueueItems");

            migrationBuilder.DropColumn(
                name: "EnableCompression",
                table: "FileDownloadQueueItems");

            migrationBuilder.DropColumn(
                name: "ParallelConnections",
                table: "FileDownloadQueueItems");

            migrationBuilder.DropColumn(
                name: "SupportsRangeRequests",
                table: "FileDownloadQueueItems");

            migrationBuilder.DropColumn(
                name: "UseHttp2",
                table: "FileDownloadQueueItems");

            migrationBuilder.RenameColumn(
                name: "CustomFileName",
                table: "YoutubeDownloadQueueItems",
                newName: "CustomFilename");
        }
    }
}
