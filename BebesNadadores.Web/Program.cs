using BebesNadadores.Infrastructure;
using BebesNadadores.Infrastructure.Identity;
using BebesNadadores.Web.Authorization;
using BebesNadadores.Web.Components;
using MudBlazor.Services;

var builder =
    WebApplication.CreateBuilder(args);


builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();


builder.Services.AddRazorPages();

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddMudServices();


builder.Services.AddInfrastructure(
    builder.Configuration);


builder.Services.AddAuthorization(options =>
{
    PoliticasSistema.Configurar(options);
});


var app =
    builder.Build();


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);

    app.UseHsts();
}


app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.UseAntiforgery();


app.MapStaticAssets();

app.MapRazorPages();


app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();


using (var scope = app.Services.CreateScope())
{
    await IdentitySeeder.SeedAsync(
        scope.ServiceProvider,
        app.Configuration);
}


app.Run();