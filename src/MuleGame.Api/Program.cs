using MuleGame.Api.Services;
using MuleGame.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Register game services
builder.Services.AddSingleton<GameSessionManager>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<IProductionService, ProductionService>();
builder.Services.AddScoped<IAuctionService, AuctionService>();
builder.Services.AddScoped<IRandomEventService, RandomEventService>();
builder.Services.AddScoped<IAIService, AIService>();

// Configure CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>("/gamehub");

// Fallback to index.html for SPA
app.MapFallbackToFile("index.html");

app.Run();
