using MQTTnet;
using MQTTnet.Client;
using Npgsql;
using NpgsqlTypes;
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
    var bytes = args.ApplicationMessage.PayloadSegment.Array ?? Array.Empty<byte>();
    var payload = System.Text.Encoding.UTF8.GetString(bytes);

    Console.WriteLine($"Received: {topic} -> {payload}");
    
    using var doc = JsonDocument.Parse(payload);
    var root = doc.RootElement;
    var timestamp = root.TryGetProperty("timestamp", out var ts) ? DateTime.Parse(ts.GetString()!) : DateTime.UtcNow;
    var value = root.TryGetProperty("value", out var val) ? val.GetDouble() : 0;
    var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;

    // Parse machine/sensor from topic: imm/1/temp_sensor_001/value
    var parts = topic.Split('/');
    var machineId = parts[1];
    var sensorId = parts[2];
    var metric = parts[3];

    var connString = "Host=postgres;Database=imm_db;Username=postgres;Password=public";
    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    await using var cmd = new NpgsqlCommand(@"
        INSERT INTO imm_data (machine_id, sensor_id, metric, value, timestamp, status)
        VALUES (@machine, @sensor, @metric, @value, @ts, @status)", conn);
    cmd.Parameters.AddWithValue("machine", machineId);
    cmd.Parameters.AddWithValue("sensor", sensorId);
    cmd.Parameters.AddWithValue("metric", metric);
    cmd.Parameters.AddWithValue("value", value);
    cmd.Parameters.AddWithValue("ts", timestamp);
    var statusParam = new NpgsqlParameter("status", NpgsqlDbType.Varchar | NpgsqlDbType.Unknown)
    {
        Value = status ?? (object)DBNull.Value
    };
    cmd.Parameters.Add(statusParam);
    
    await cmd.ExecuteNonQueryAsync();
    Console.WriteLine($"Stored: {topic} -> {value}");
};

await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("imm/#").Build());
Console.WriteLine("Subscrber started. Ctrl+C to stop.");
await Task.Delay(-1);  // infinite loop
