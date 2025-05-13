var builder = WebApplication.CreateBuilder(args);
// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts => opts.ResolveConflictingActions(apiDesc => apiDesc.First()));

var app = builder.Build();

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

app.MapGet("/error", () => Results.Problem());

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
