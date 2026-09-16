using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AptosMedicos_Usuarios_MedicoUsuarioId",
                table: "AptosMedicos");

            migrationBuilder.DropForeignKey(
                name: "FK_Cajas_Usuarios_UsuarioAperturaId",
                table: "Cajas");

            migrationBuilder.DropForeignKey(
                name: "FK_Cajas_Usuarios_UsuarioCierreId",
                table: "Cajas");

            migrationBuilder.DropForeignKey(
                name: "FK_Profesores_Usuarios_UsuarioId",
                table: "Profesores");

            migrationBuilder.DropForeignKey(
                name: "FK_RolesClaims_Roles_RoleId",
                table: "RolesClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_UsuariosClaims_Usuarios_UserId",
                table: "UsuariosClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_UsuariosLogins_Usuarios_UserId",
                table: "UsuariosLogins");

            migrationBuilder.DropForeignKey(
                name: "FK_UsuariosRoles_Roles_RoleId",
                table: "UsuariosRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UsuariosRoles_Usuarios_UserId",
                table: "UsuariosRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UsuariosTokens_Usuarios_UserId",
                table: "UsuariosTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UsuariosTokens",
                table: "UsuariosTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UsuariosRoles",
                table: "UsuariosRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UsuariosLogins",
                table: "UsuariosLogins");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UsuariosClaims",
                table: "UsuariosClaims");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Usuarios",
                table: "Usuarios");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RolesClaims",
                table: "RolesClaims");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Roles",
                table: "Roles");

            migrationBuilder.RenameTable(
                name: "UsuariosTokens",
                newName: "AspNetUserTokens");

            migrationBuilder.RenameTable(
                name: "UsuariosRoles",
                newName: "AspNetUserRoles");

            migrationBuilder.RenameTable(
                name: "UsuariosLogins",
                newName: "AspNetUserLogins");

            migrationBuilder.RenameTable(
                name: "UsuariosClaims",
                newName: "AspNetUserClaims");

            migrationBuilder.RenameTable(
                name: "Usuarios",
                newName: "AspNetUsers");

            migrationBuilder.RenameTable(
                name: "RolesClaims",
                newName: "AspNetRoleClaims");

            migrationBuilder.RenameTable(
                name: "Roles",
                newName: "AspNetRoles");

            migrationBuilder.RenameIndex(
                name: "IX_UsuariosRoles_RoleId",
                table: "AspNetUserRoles",
                newName: "IX_AspNetUserRoles_RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_UsuariosLogins_UserId",
                table: "AspNetUserLogins",
                newName: "IX_AspNetUserLogins_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_UsuariosClaims_UserId",
                table: "AspNetUserClaims",
                newName: "IX_AspNetUserClaims_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Usuarios_Email",
                table: "AspNetUsers",
                newName: "IX_AspNetUsers_Email");

            migrationBuilder.RenameIndex(
                name: "IX_RolesClaims_RoleId",
                table: "AspNetRoleClaims",
                newName: "IX_AspNetRoleClaims_RoleId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserTokens",
                table: "AspNetUserTokens",
                columns: new[] { "UserId", "LoginProvider", "Name" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserRoles",
                table: "AspNetUserRoles",
                columns: new[] { "UserId", "RoleId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserLogins",
                table: "AspNetUserLogins",
                columns: new[] { "LoginProvider", "ProviderKey" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserClaims",
                table: "AspNetUserClaims",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUsers",
                table: "AspNetUsers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetRoleClaims",
                table: "AspNetRoleClaims",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetRoles",
                table: "AspNetRoles",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AptosMedicos_AspNetUsers_MedicoUsuarioId",
                table: "AptosMedicos",
                column: "MedicoUsuarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Cajas_AspNetUsers_UsuarioAperturaId",
                table: "Cajas",
                column: "UsuarioAperturaId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cajas_AspNetUsers_UsuarioCierreId",
                table: "Cajas",
                column: "UsuarioCierreId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Profesores_AspNetUsers_UsuarioId",
                table: "Profesores",
                column: "UsuarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AptosMedicos_AspNetUsers_MedicoUsuarioId",
                table: "AptosMedicos");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Cajas_AspNetUsers_UsuarioAperturaId",
                table: "Cajas");

            migrationBuilder.DropForeignKey(
                name: "FK_Cajas_AspNetUsers_UsuarioCierreId",
                table: "Cajas");

            migrationBuilder.DropForeignKey(
                name: "FK_Profesores_AspNetUsers_UsuarioId",
                table: "Profesores");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserTokens",
                table: "AspNetUserTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUsers",
                table: "AspNetUsers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserRoles",
                table: "AspNetUserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserLogins",
                table: "AspNetUserLogins");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserClaims",
                table: "AspNetUserClaims");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetRoles",
                table: "AspNetRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetRoleClaims",
                table: "AspNetRoleClaims");

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                newName: "UsuariosTokens");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                newName: "Usuarios");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                newName: "UsuariosRoles");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                newName: "UsuariosLogins");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                newName: "UsuariosClaims");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                newName: "Roles");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                newName: "RolesClaims");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUsers_Email",
                table: "Usuarios",
                newName: "IX_Usuarios_Email");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "UsuariosRoles",
                newName: "IX_UsuariosRoles_RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "UsuariosLogins",
                newName: "IX_UsuariosLogins_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "UsuariosClaims",
                newName: "IX_UsuariosClaims_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "RolesClaims",
                newName: "IX_RolesClaims_RoleId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UsuariosTokens",
                table: "UsuariosTokens",
                columns: new[] { "UserId", "LoginProvider", "Name" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Usuarios",
                table: "Usuarios",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UsuariosRoles",
                table: "UsuariosRoles",
                columns: new[] { "UserId", "RoleId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_UsuariosLogins",
                table: "UsuariosLogins",
                columns: new[] { "LoginProvider", "ProviderKey" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_UsuariosClaims",
                table: "UsuariosClaims",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Roles",
                table: "Roles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RolesClaims",
                table: "RolesClaims",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AptosMedicos_Usuarios_MedicoUsuarioId",
                table: "AptosMedicos",
                column: "MedicoUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cajas_Usuarios_UsuarioAperturaId",
                table: "Cajas",
                column: "UsuarioAperturaId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cajas_Usuarios_UsuarioCierreId",
                table: "Cajas",
                column: "UsuarioCierreId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Profesores_Usuarios_UsuarioId",
                table: "Profesores",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RolesClaims_Roles_RoleId",
                table: "RolesClaims",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UsuariosClaims_Usuarios_UserId",
                table: "UsuariosClaims",
                column: "UserId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UsuariosLogins_Usuarios_UserId",
                table: "UsuariosLogins",
                column: "UserId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UsuariosRoles_Roles_RoleId",
                table: "UsuariosRoles",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UsuariosRoles_Usuarios_UserId",
                table: "UsuariosRoles",
                column: "UserId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UsuariosTokens_Usuarios_UserId",
                table: "UsuariosTokens",
                column: "UserId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
