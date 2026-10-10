using InventoryHold.Domain.Repositories;
using InventoryHold.Domain.Services;
using InventoryHold.Infrastructure.Messaging;
using InventoryHold.Infrastructure.MongoDB;
using InventoryHold.Infrastructure.Redis;
using MongoDB.Driver;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var mongoConnection = builder.Configuration["MongoDb:ConnectionString"]
    ?? "mongodb://localhost:27017/?replicaSet=rs0&directConnection=true";

var mongoDatabase = builder.Configuration["MongoDb:DatabaseName"]
    ?? "InventoryHoldDb";

var redisConnection = builder.Configuration["Redis:ConnectionString"]
    ?? "localhost:6379";

var rabbitConnection = builder.Configuration["RabbitMq:ConnectionString"]
    ?? "amqp://inventoryapp:localdevpassword@localhost:5672";

var holdDurationMinutes =
    builder.Configuration.GetValue<int?>("Hold:DurationMinutes") ?? 5;

builder.Services.AddSingleton<IMongoClient>(
    _ => new MongoClient(mongoConnection));

builder.Services.AddSingleton(sp =>
    new MongoDbContext(
        sp.GetRequiredService<IMongoClient>(),
        mongoDatabase));

builder.Services.AddScoped<MongoSessionContext>();

builder.Services.AddScoped<IInventoryRepository, MongoInventoryRepository>();
builder.Services.AddScoped<IHoldRepository, MongoHoldRepository>();
builder.Services.AddScoped<IInventoryUnitOfWork, MongoInventoryUnitOfWork>();

builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect(redisConnection));

builder.Services.AddSingleton<IInventoryCache, RedisInventoryCache>();

builder.Services.AddSingleton<IHoldEventPublisher>(
    _ => new RabbitMqHoldEventPublisher(rabbitConnection));

builder.Services.AddScoped<IHoldService>(sp =>
    new HoldService(
        sp.GetRequiredService<IInventoryRepository>(),
        sp.GetRequiredService<IHoldRepository>(),
        sp.GetRequiredService<IInventoryUnitOfWork>(),
        sp.GetRequiredService<IInventoryCache>(),
        sp.GetRequiredService<IHoldEventPublisher>(),
        TimeSpan.FromMinutes(holdDurationMinutes)));

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseCors("ReactFrontend");

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
