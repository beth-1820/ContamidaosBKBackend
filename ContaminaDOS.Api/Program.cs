using ContaminaDOS.Data;
using ContaminaDOS.Business;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var client = new MongoClient(builder.Configuration["MongoDb:ConnectionString"]);
    return client.GetDatabase(builder.Configuration["MongoDb:DatabaseName"]);
});

builder.Services.AddScoped<GameData>();
builder.Services.AddScoped<GameBusiness>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
