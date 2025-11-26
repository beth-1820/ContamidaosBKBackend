using ContaminaDOS.Data;
using ContaminaDOS.Business;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MongoDB configuration
builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var client = new MongoClient(builder.Configuration["MongoDb:ConnectionString"]);
    return client.GetDatabase(builder.Configuration["MongoDb:DatabaseName"]);
});

builder.Services.AddScoped<GameData>();
builder.Services.AddScoped<GameBusiness>();

// CORS
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

// Enable Swagger ALWAYS (Development + Production)
app.UseSwagger();
app.UseSwaggerUI();

// app.UseHttpsRedirection();

app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();

// Root endpoint to avoid 404 on /
app.MapGet("/", () => "API ContaminaDOS funcionando correctamente ??");

app.Run();
