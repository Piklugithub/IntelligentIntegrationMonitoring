
using IntelligentMonitoring.Application.Abstractions.Logging;
using IntelligentMonitoring.Application.Services.Logging;
using IntelligentMonitoring.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Register API services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Application services
builder.Services.AddScoped<ILogSearchService, LogSearchService>();

// Register Infrastructure services
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
