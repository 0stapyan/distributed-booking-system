using RabbitMQ.Client;

var host = Environment.GetEnvironmentVariable("RabbitMq__HostName") ?? "localhost";
var port = int.Parse(Environment.GetEnvironmentVariable("RabbitMq__Port") ?? "5672");
var user = Environment.GetEnvironmentVariable("RabbitMq__UserName") ?? "guest";
var password = Environment.GetEnvironmentVariable("RabbitMq__Password") ?? "guest";
var vhost = Environment.GetEnvironmentVariable("RabbitMq__VirtualHost") ?? "/";

Console.WriteLine($"[topology] Connecting to RabbitMQ at {host}:{port}...");

var factory = new ConnectionFactory
{
    HostName = host,
    Port = port,
    UserName = user,
    Password = password,
    VirtualHost = vhost,
};

await using var connection = await factory.CreateConnectionAsync();
await using var channel = await connection.CreateChannelAsync();

Console.WriteLine("[topology] Connected. Declaring topology for team-06...");

const string dlxName = "team-06.notifications.dlx";
const string dlqName = "team-06.notifications.dlq";

await channel.ExchangeDeclareAsync(dlxName, ExchangeType.Fanout, durable: true, autoDelete: false);
Console.WriteLine($"[topology] Exchange '{dlxName}' declared");

await channel.QueueDeclareAsync(dlqName, durable: true, exclusive: false, autoDelete: false, arguments: null);
Console.WriteLine($"[topology] Queue '{dlqName}' declared");

await channel.QueueBindAsync(dlqName, dlxName, routingKey: string.Empty);
Console.WriteLine($"[topology] '{dlqName}' bound to '{dlxName}'");

const string bookingsExchange = "team-06.bookings";
const string bookingsQueue = "team-06.bookings.booking-created";
const string bookingsKey = "booking-created";

await channel.ExchangeDeclareAsync(bookingsExchange, ExchangeType.Direct, durable: true, autoDelete: false);
Console.WriteLine($"[topology] Exchange '{bookingsExchange}' declared");

var bookingQueueArgs = new Dictionary<string, object?>
{
    ["x-dead-letter-exchange"] = dlxName,
};

await channel.QueueDeclareAsync(bookingsQueue, durable: true, exclusive: false, autoDelete: false, arguments: bookingQueueArgs);
Console.WriteLine($"[topology] Queue '{bookingsQueue}' declared (DLX -> '{dlxName}')");

await channel.QueueBindAsync(bookingsQueue, bookingsExchange, routingKey: bookingsKey);
Console.WriteLine($"[topology] '{bookingsQueue}' bound to '{bookingsExchange}' with key '{bookingsKey}'");

const string hotelsExchange = "team-06.hotels";
const string hotelsQueue = "team-06.hotels.hotel-created";
const string hotelsKey = "hotel-created";

await channel.ExchangeDeclareAsync(hotelsExchange, ExchangeType.Direct, durable: true, autoDelete: false);
Console.WriteLine($"[topology] Exchange '{hotelsExchange}' declared");

var hotelQueueArgs = new Dictionary<string, object?>
{
    ["x-dead-letter-exchange"] = dlxName,
};

await channel.QueueDeclareAsync(hotelsQueue, durable: true, exclusive: false, autoDelete: false, arguments: hotelQueueArgs);
Console.WriteLine($"[topology] Queue '{hotelsQueue}' declared (DLX -> '{dlxName}')");

await channel.QueueBindAsync(hotelsQueue, hotelsExchange, routingKey: hotelsKey);
Console.WriteLine($"[topology] '{hotelsQueue}' bound to '{hotelsExchange}' with key '{hotelsKey}'");

const string sagaExchange = "team-06.saga";

await channel.ExchangeDeclareAsync(sagaExchange, ExchangeType.Direct, durable: true, autoDelete: false);
Console.WriteLine($"[topology] Exchange '{sagaExchange}' declared");

var sagaQueueArgs = new Dictionary<string, object?>
{
    ["x-dead-letter-exchange"] = dlxName,
};

string[][] sagaBindings =
[
    // happy path
    ["team-06.saga.reserve-room", "reserve-room"],
    ["team-06.saga.room-reserved", "room-reserved"],
    ["team-06.saga.charge-payment", "charge-payment"],
    ["team-06.saga.payment-charged", "payment-charged"],
    ["team-06.saga.booking-confirmed", "booking-confirmed"],
    
    // failure
    ["team-06.saga.room-reservation-failed", "room-reservation-failed"],
    ["team-06.saga.payment-failed", "payment-failed"],
    
    // compensation
    ["team-06.saga.release-room", "release-room"],
    ["team-06.saga.room-released", "room-released"],
    ["team-06.saga.booking-cancelled", "booking-cancelled"],
];

foreach (var binding in sagaBindings)
{
    await channel.QueueDeclareAsync(binding[0], durable: true, exclusive: false, autoDelete: false, arguments: sagaQueueArgs);
    await channel.QueueBindAsync(binding[0], sagaExchange, routingKey: binding[1]);
    Console.WriteLine($"[topology] Queue '{binding[0]}' bound to '{sagaExchange}' with key '{binding[1]}'");
}

Console.WriteLine("[topology] Done. Topology is up.");