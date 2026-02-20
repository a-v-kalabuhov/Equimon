using MQTTnet;
using MQTTnet.Client;
using Npgsql;
using System.Text.Json;

var factory = new MqttFactory();
var mqttClient = factory.CreateMqttClient();

var mqttOptions = new MqttClientOptionsBuilder()
    .WithTcpServer("emqx", 1883)
    .WithClientId("pg-subscriber")
    .WithCredentials("admin", "public")
    .Build();

await mqttClient.ConnectAsync(mqttOptions);

mqttClient.ApplicationMessageReceivedAsync += async args =>
{
    var topic = args.ApplicationMessage.Topic;
    var payload = System.Text.Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment.Array);
    
    using var doc = JsonDocument.Parse(payload);
    var root = doc.RootElement;
    var timestamp = root.TryGetProperty("timestamp", out var ts) ? DateTime.Parse(ts.GetString()!) : DateTime.UtcNow;
    var value = root.TryGetProperty("value", out var val) ? val.GetDouble() : 0;
    var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;

    // Parse machine/sensor из topic: tpa/1/temp_sensor_001/value
    var parts = topic.Split('/');
    var machineId = parts[1];
    var sensorId = parts[2];
    var metric = parts[3];

    var connString = "Host=postgres;Database=tpa_db;Username=postgres;Password=public";
    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    await using var cmd = new NpgsqlCommand(@"
        INSERT INTO tpa_data (machine_id, sensor_id, metric, value, timestamp, status)
        VALUES (@machine, @sensor, @metric, @value, @ts, @status)", conn);
    cmd.Parameters.AddWithValue("machine", machineId);
    cmd.Parameters.AddWithValue("sensor", sensorId);
    cmd.Parameters.AddWithValue("metric", metric);
    cmd.Parameters.AddWithValue("value", value);
    cmd.Parameters.AddWithValue("ts", timestamp);
    cmd.Parameters.AddWithValue("status", (object)status ?? DBNull.Value);

    await cmd.ExecuteNonQueryAsync();
    Console.WriteLine($"Сохранено: {topic} -> {value}");
};

await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("tpa/#").Build());
Console.WriteLine("Подписчик запущен. Ctrl+C для остановки.");
await Task.Delay(-1);  // Бесконечный loop
