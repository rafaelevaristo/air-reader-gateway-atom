using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AtomGateway.Api.Hubs;
using AtomGateway.Api.Services;
using AtomGateway.Api.HostedServices;
using AtomGateway.Core.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SignalR
builder.Services.AddSignalR();

// CORS - Permitir projeto Web na porta 3000
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWeb", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "https://localhost:3001",
                "http://127.0.0.1:3000"
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();  // Importante para SignalR
    });
});

// Custom Services
builder.Services.AddSingleton<ISerialService, SerialPortService>();
builder.Services.AddSingleton<DtcProcessingService>();
builder.Services.AddHostedService<SerialListenerService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CORS deve vir primeiro!
app.UseCors("AllowWeb");

app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AtomHub>("/atomhub");

app.Run();