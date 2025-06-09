using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Diagnostics;
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

builder.Services.AddControllers(options =>
{
    options.ModelBindingMessageProvider.SetValueIsInvalidAccessor((x) => $"The value '{x}' is invalid.");
    options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor((x) => $"The field {x} must be a number.");
    options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((x, y) => $"The value '{x}' is not valid for {y}.");
    options.ModelBindingMessageProvider.SetMissingKeyOrValueAccessor(() => $"A value is required.");
});

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

//Code replaced by the [ManualValidationFilter] attribute
//builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true); //remove automatic [ApiController] ModelState validation for API request - custom validation ModelState

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

app.MapGet("/error",
    [EnableCors("AnyOrigin")]
    [ResponseCache(NoStore = true)]
    (HttpContext context) =>
         {
             var exceptionHandler = context.Features.Get<IExceptionHandlerPathFeature>();

             // TODO: logging, sending notifications, and more 
             var details = new ProblemDetails();

             details.Detail = exceptionHandler?.Error.Message;
             details.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;
             details.Type ="https://tools.ietf.org/html/rfc7231#section-6.6.1";
             details.Status = StatusCodes.Status500InternalServerError;

             return Results.Problem(details);
         }).RequireCors("AnyOrigin");

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
