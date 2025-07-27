using System.Reflection;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using MyBGList.Constants;
using MyBGList.GraphQL;
using MyBGList.gRPC;
using MyBGList.Models;
using MyBGList.Swagger;

using Serilog;
using Serilog.Sinks.MSSqlServer;

using Swashbuckle.AspNetCore.Annotations;

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
    options.EnableAnnotations();

    options.ParameterFilter<SortColumnFilter>();
    options.ParameterFilter<SortOrderFilter>();

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });


    //options.AddSecurityRequirement(new OpenApiSecurityRequirement
    //{
    //    {
    //        new OpenApiSecurityScheme
    //        {
    //            Reference = new OpenApiReference
    //            {
    //                Type = ReferenceType.SecurityScheme,
    //                Id = "Bearer"
    //            }
    //        },
    //        Array.Empty<string>()
    //    }
    //});

    //replacing above - padlock in swaggerUI disaper for public operations
    options.OperationFilter<AuthRequirementFilter>();
    options.DocumentFilter<CustomDocumentFilter>();
    options.RequestBodyFilter<PasswordRequestFilter>();
    options.SchemaFilter<CustomKeyValueFilter>();

    options.ResolveConflictingActions(apiDesc => apiDesc.First());
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "MyBGList", Version = "v1.0" });

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(System.IO.Path.Combine(
AppContext.BaseDirectory, xmlFilename));
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


//builder.WebHost.ConfigureKestrel((context, options) =>
//{
//    options.ListenLocalhost(40443, listenOptions =>
//    {
//        listenOptions.UseHttps();
//        listenOptions.Protocols = HttpProtocols.Http2;
//    });
//});

builder.Services.AddGraphQLServer()
    .AddAuthorization()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddProjections()
    .AddFiltering()
    .AddSorting();

builder.Services.AddGrpc();

//Code replaced by the [ManualValidationFilter] attribute
//builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true); //remove automatic [ApiController] ModelState validation for API request - custom validation ModelState
Console.WriteLine($"PreferHostingUrls: {builder.Configuration["PreferHostingUrls"]}");
builder.Services.AddIdentity<ApiUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;
}).AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
    options.DefaultChallengeScheme =
    options.DefaultForbidScheme =
    options.DefaultScheme =
    options.DefaultSignInScheme =
    options.DefaultSignOutScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["JWT:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["JWT:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(builder.Configuration["JWT:SigningKey"]))
    };
});

builder.Services.AddAuthorization(options =>
{
    //[Authorize(Policy = "ModeratorWithMobilePhone")]
    options.AddPolicy("ModeratorWithMobilePhone", policy => policy
        .RequireClaim(ClaimTypes.Role, RoleNames.Moderator)
        .RequireClaim(ClaimTypes.MobilePhone));

    //[Authorize(Policy = "MinAge18")]
    options.AddPolicy("MinAge18", policy => policy
        .RequireAssertion(ctx =>
            ctx.User.HasClaim(c => c.Type == ClaimTypes.DateOfBirth) &&
                DateTime.ParseExact("yyyyMMdd", ctx.User.Claims
                .First(c => c.Type == ClaimTypes.DateOfBirth).Value, System.Globalization.CultureInfo.InvariantCulture) >= DateTime.Now.AddYears(-18)));
});

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
    app.UseSwaggerUI(options =>
    {
        options.ConfigObject.AdditionalItems["showCommonExtensions"] = true;
        options.ConfigObject.AdditionalItems["showExtensions"] = true;
    });
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

app.MapGet("/cod/test",
[EnableCors("AnyOrigin")]
[ResponseCache(NoStore = true)] () =>
    Results.Text("<script>" +
        "window.alert('Your client supports JavaScript!" +
        "\\r\\n\\r\\n" +
        $"Server time (UTC): {DateTime.UtcNow.ToString("o")}" +
        "\\r\\n" +
        "Client time (UTC): ' + new Date().toISOString());" +
        "</script>" +
        "<noscript>Your client does not support JavaScript</noscript>",
        "text/html"));

app.MapGet("/cache/test/1",
[EnableCors("AnyOrigin")]
(HttpContext context) =>
    {
        context.Response.Headers["cache-control"] = "no-cache, no-store";
        return Results.Ok();
    });

app.MapGet("/cache/test/2",
[Authorize]
[EnableCors("AnyOrigin")]
[SwaggerOperation(
    Tags = new[] { "Auth" },
    Summary = "Auth test #2 (authenticated users).",
    Description = "Returns 200 - OK if called by an authenticated user regardless of its role(s).")]
    (HttpContext context) =>
    {
        return Results.Ok();
    });


app.MapGet("/auth/test/1",
[Authorize]
[EnableCors("AnyOrigin")]
[SwaggerOperation(
    Tags = new[] { "Auth" },
    Summary = "Auth test #1 (authenticated users).",
    Description = "Returns 200 - OK if called by an authenticated user regardless of its role(s).")]
[ResponseCache(NoStore = true)] () =>
   {
       return Results.Ok("You are authorized!");
   });

app.MapGet("/auth/test/2",
[Authorize(Roles = RoleNames.Moderator)]
[EnableCors("AnyOrigin")]
[SwaggerOperation(
    Tags = new[] { "Auth" },
    Summary = "Auth test #2 (authenticated users).",
    Description = "Returns 200 - OK if called by an authenticated user regardless of its role(s).")]
[ResponseCache(NoStore = true)] () =>
    {
        return Results.Ok("You are authorized!");
    });

app.MapGet("/auth/test/3",
    [SwaggerOperation(
    Tags = new[] { "Auth" },
    Summary = "Auth test #3 (authenticated users).",
    Description = "Returns 200 - OK if called by an authenticated user regardless of its role(s).")]
[Authorize(Roles = RoleNames.Administrator)]
[EnableCors("AnyOrigin")]
[ResponseCache(NoStore = true)] () =>
   {
       return Results.Ok("You are authorized!");
   });

app.MapGet("/auth/test/4",
[Authorize(Roles = RoleNames.SuperAdmin)]
[EnableCors("AnyOrigin")]
[SwaggerOperation(
    Tags = new[] { "Auth" },
    Summary = "Auth test #4 (authenticated users).",
    Description = "Returns 200 - OK if called by an authenticated user regardless of its role(s).")]
[ResponseCache(NoStore = true)] () =>
{
    return Results.Ok("You are authorized!");
});


app.UseHttpsRedirection();

app.UseCors();

app.UseResponseCaching();

app.UseAuthentication();
app.UseAuthorization();

app.MapGraphQL();

app.MapGrpcService<GrpcService>();

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
