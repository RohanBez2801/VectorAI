using Vector.Server.Hubs;
using Vector.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddSingleton<BrainHost>();

var app = builder.Build();

// Configure request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection(); // Disabled for local dev (HTTP)

// Map Hubs
app.MapHub<VectorHub>("/hub/vector");

// Init Brain (in background or blocking? Blocking ensures it's ready)
var brainHost = app.Services.GetRequiredService<BrainHost>();
// We prefer not to block startup indefinitely, but InitAsync is fast enough (just DI setup mostly)
// However, it loads Ollama models maybe? No, checking them is lazy or in init.
try 
{
    // Fire and forget init to not block startup if it hangs on Ollama
    _ = brainHost.InitializeAsync(); 
}
catch (Exception ex)
{
    Console.WriteLine($"Brain Init Error: {ex}");
}

// Simple API for health
app.MapGet("/health", () => "Vector Server Online");

app.Run();

