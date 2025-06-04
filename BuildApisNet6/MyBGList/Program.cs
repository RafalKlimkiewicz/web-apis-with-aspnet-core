using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

using MyBGList.Models;
using MyBGList.Swagger;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
//secrects for staging env
if (builder.Environment.IsStaging() || builder.Environment.EnvironmentName == "Staging")
{
    builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: true);
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.ParameterFilter<SortColumnFilter>();
    options.ParameterFilter<SortOrderFilter>();
    options.ResolveConflictingActions(apiDesc => apiDesc.First());
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "MyBGList", Version = "v1.0" });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(cfg =>
    {
        cfg.WithOrigins(builder.Configuration["AllowedOrigins"]);
        cfg.AllowAnyHeader();
        cfg.AllowAnyMethod();
    });

    options.AddPolicy(name: "AnyOrigin",
        cfg =>
        {
            cfg.AllowAnyOrigin();
            cfg.AllowAnyHeader();
            cfg.AllowAnyMethod();
        });
});


builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

Console.WriteLine($"ASPNETCORE_ENVIRONMENT = {app.Environment.EnvironmentName}");


app.Logger.LogInformation("Hosting environment: {Env}", app.Environment.EnvironmentName);
Console.WriteLine($"ASPNETCORE_ENVIRONMENT = {app.Environment.EnvironmentName}");

if (app.Configuration.GetValue<bool>("UseSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Configuration.GetValue<bool>("UseDeveloperExceptionPage"))
    app.UseDeveloperExceptionPage();
else
    app.UseExceptionHandler("/error");

app.MapGet("/error", () => Results.Problem()).RequireCors("AnyOrigin"); ;

app.MapGet("/cod/test", [EnableCors("AnyOrigin")][ResponseCache(NoStore = true)] () =>
   Results.Text("<script>" +
   "window.alert('Your client supports JavaScript!" +
   "\\r\\n\\r\\n" +
   $"Server time (UTC): {DateTime.UtcNow.ToString("o")}" +
   "\\r\\n" +
   "Client time (UTC): ' + new Date().toISOString());" +
   "</script>" +
   "<noscript>Your client does not support JavaScript</noscript>",
   "text/html"));

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers().RequireCors("AnyOrigin");

app.Run();
