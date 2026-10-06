using KeycapStore.Infrastructure;
using KeycapStore.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
