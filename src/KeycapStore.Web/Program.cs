using KeycapStore.Infrastructure;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.Web.Localization;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog(logger =>
{
    logger.ReadFrom.Configuration(builder.Configuration).Enrich.FromLogContext();

    if (builder.Environment.IsDevelopment())
    {
        logger.WriteTo.Console();
    }
    else
    {
        logger.WriteTo.Console(new RenderedCompactJsonFormatter());
    }
});

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddViewLocalization();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

var app = builder.Build();

if (args.Contains("migrate"))
{
    await app.Services.MigrateDatabaseAsync();
    return;
}


if (args.Contains("create-admin"))
{
    var emailIndex = Array.IndexOf(args, "--email");
    if (emailIndex < 0 || emailIndex + 1 >= args.Length)
    {
        Console.Error.WriteLine("Uso: create-admin --email <e-mail>");
        Environment.ExitCode = 1;
        return;
    }

    var result = await app.Services.CreateAdminAsync(args[emailIndex + 1]);
    if (!result.Succeeded)
    {
        Console.Error.WriteLine("Não foi possível criar o admin: " + string.Join(" ", result.Errors));
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine(result.GeneratedPassword is null
        ? "Conta existente promovida a Admin. A senha não mudou."
        : $"Admin criado. Senha (anote agora; ela não será mostrada de novo): {result.GeneratedPassword}");
    return;
}

if (args.Contains("reset-admin-password"))
{
    var emailIndex = Array.IndexOf(args, "--email");
    if (emailIndex < 0 || emailIndex + 1 >= args.Length)
    {
        Console.Error.WriteLine("Uso: reset-admin-password --email <e-mail>");
        Environment.ExitCode = 1;
        return;
    }

    var result = await app.Services.ResetAdminPasswordAsync(args[emailIndex + 1]);
    if (!result.Succeeded)
    {
        Console.Error.WriteLine("Não foi possível trocar a senha: " + string.Join(" ", result.Errors));
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine($"Senha nova do admin (anote agora; ela não será mostrada de novo): {result.NewPassword}");
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRequestLocalization(options =>
{
    options.SetDefaultCulture(SiteCultures.Portuguese)
        .AddSupportedCultures(SiteCultures.All)
        .AddSupportedUICultures(SiteCultures.All);

    options.RequestCultureProviders = options.RequestCultureProviders
        .Where(provider => provider is not AcceptLanguageHeaderRequestCultureProvider)
        .ToList();
});

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapStaticAssets();

app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
