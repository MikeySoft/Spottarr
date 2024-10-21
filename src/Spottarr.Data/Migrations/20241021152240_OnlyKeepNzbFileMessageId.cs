﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Spottarr.Data.Migrations
{
    /// <inheritdoc />
    public partial class OnlyKeepNzbFileMessageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NzbFiles");

            migrationBuilder.AddColumn<string>(
                name: "ImageMessageId",
                table: "Spots",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NzbMessageId",
                table: "Spots",
                type: "TEXT",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageMessageId",
                table: "Spots");

            migrationBuilder.DropColumn(
                name: "NzbMessageId",
                table: "Spots");

            migrationBuilder.CreateTable(
                name: "NzbFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SpotId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    MessageId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NzbFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NzbFiles_Spots_SpotId",
                        column: x => x.SpotId,
                        principalTable: "Spots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NzbFiles_MessageId",
                table: "NzbFiles",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NzbFiles_SpotId",
                table: "NzbFiles",
                column: "SpotId",
                unique: true);
        }
    }
}
