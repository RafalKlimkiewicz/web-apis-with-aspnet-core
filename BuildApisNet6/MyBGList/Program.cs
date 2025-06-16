using System.Drawing;
using System.Runtime.CompilerServices;

using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

using MyBGList.Constants;
using MyBGList.Models;
using MyBGList.Swagger;

using Serilog;
using Serilog.Sinks.MSSqlServer;

using ILogger = Microsoft.Extensions.Logging.ILogger;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders()
    .AddJsonConsole(
        options =>
        {
            options.TimestampFormat = "HH:mm";
            options.UseUtcTimestamp = true;
        }
    )
    .AddSimpleConsole(
// moved to appsettings
//options =>
//{
//    options.SingleLine = true;
//    options.TimestampFormat = "HH:mm:ss ";
//    options.UseUtcTimestamp = true;
//}
).AddDebug();

builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration);

    //or appsettings
    //lc.MinimumLevel.Is(Serilog.Events.LogEventLevel.Warning);
    //lc.MinimumLevel.Override("MyBGList", Serilog.Events.LogEventLevel.Information);

    lc.Enrich.WithMachineName();
    lc.Enrich.WithThreadId();
    lc.Enrich.WithThreadName(); // Excercise 7.5.4

    lc.WriteTo.File("Logs/log.txt",
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] [{MachineName} #{ThreadId} {ThreadName}] {Message:lj}{NewLine}{Exception}",
        rollingInterval: RollingInterval.Day);

    lc.WriteTo.File("Logs/errors.txt",
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] [{MachineName} #{ThreadId} {ThreadName}] {Message:lj}{NewLine}{Exception}",
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error,
        rollingInterval: RollingInterval.Day);


    lc.WriteTo.MSSqlServer(
        //restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
        connectionString: ctx.Configuration.GetConnectionString("DefaultConnection"),
        sinkOptions: new MSSqlServerSinkOptions
        {
            TableName = "LogEvents",
            AutoCreateSqlTable = true
        },
        columnOptions: new ColumnOptions()
        {
            AdditionalColumns = new SqlColumn[]
            {
                new() {
                    ColumnName = "SourceContext",
                    PropertyName = "SourceContext",
                    DataType = System.Data.SqlDbType.NVarChar,
                },
                new() {
                    ColumnName = "EventId",
                    PropertyName = "EventId",
                    DataType = System.Data.SqlDbType.NVarChar,
                }
            }
        });
}, writeToProviders: true);

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

    options.CacheProfiles.Add("NoCache", new CacheProfile() { NoStore = true });
    options.CacheProfiles.Add("Any-60", new CacheProfile()
    {
        Location = ResponseCacheLocation.Any,
        Duration = 60
    });
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

builder.Services.AddResponseCaching(options =>
{
    options.MaximumBodySize = 32 * 1024 * 1024; //Sets max response body size to 32 MB //
    options.SizeLimit = 50 * 1024 * 1024; //Sets max middleware size to 50 MB
});

//dotnet sql-cache create "{connectionString}" dbo AppCache
builder.Services.AddDistributedSqlServerCache(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.SchemaName = "dbo";
    options.TableName = "AppCache";
});

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"];
});


builder.Services.AddMemoryCache();

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
    //instead of "/error"
    app.UseExceptionHandler(action =>
    {
        action.Run(async context =>
        {
            var exceptionHandler = context.Features.Get<IExceptionHandlerPathFeature>();

            var details = new ProblemDetails();

            details.Detail = exceptionHandler?.Error.Message;

            details.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;
            details.Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1";
            details.Status = StatusCodes.Status500InternalServerError;

            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(details));
        });
    });

app.MapGet("/error",
    [EnableCors("AnyOrigin")]
[ResponseCache(NoStore = true)] (HttpContext context, [FromServices] ILogger<Program> logger) =>
    {
        var exceptionHangler = context.Features.Get<IExceptionHandlerPathFeature>();

        var details = new ProblemDetails
        {
            Title = "Error",
            Detail = exceptionHangler?.Error.Message,
            Status = exceptionHangler?.Error switch
            {
                NotImplementedException _ => StatusCodes.Status501NotImplemented,
                TimeoutException _ => StatusCodes.Status504GatewayTimeout,
                _ => StatusCodes.Status500InternalServerError
            },
            Type = exceptionHangler?.Error switch
            {
                NotImplementedException _ => "https://tools.ietf.org/html/rfc7231#section-6.6.2",
                TimeoutException _ => "https://tools.ietf.org/html/rfc7231#section-6.6.5",
                _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            },
        };

        details.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;

        logger.LogError(CustomLogEvents.Error_Get, exceptionHangler?.Error, "An unhandled exception occurred.");
        app.Logger.LogError(CustomLogEvents.Error_Get, exceptionHangler?.Error, "An unhandled exception occurred. {errorMessage}",
            exceptionHangler?.Error.Message);

        return Results.Problem(details);
    }); //.RequireCors("AnyOrigin");

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

app.MapGet("/cache/test/1", [EnableCors("AnyOrigin")]
(HttpContext context) =>
{
    context.Response.Headers["cache-control"] = "no-cache, no-store";
    return Results.Ok();
});

app.MapGet("/cache/test/2", [EnableCors("AnyOrigin")]
(HttpContext context) =>
{
    return Results.Ok();
});


app.UseHttpsRedirection();

app.UseCors();

app.UseResponseCaching();

app.UseAuthorization();

//fallback for no cache if missed
app.Use((context, next) =>
{
    //context.Response.Headers["cache-control"] = "no-cache, no-store"; //or strong typed
    context.Response.GetTypedHeaders().CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
    {
        NoCache = true,
        NoStore = true
    };
    return next.Invoke();
});

app.MapControllers().RequireCors("AnyOrigin");

app.Run();
