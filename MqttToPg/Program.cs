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
    
    // New format: array of sensor readings
    List<(string sensorId, DateTime valueDt, double? value, string? status)> readings;
    try
    {
        readings = new List<(string, DateTime, double?, string?)>();
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        
        if (root.ValueKind != JsonValueKind.Array)
        {
            Console.WriteLine("Error: Payload is not an array");
            return;
        }
        
        foreach (var item in root.EnumerateArray())
        {
            var sensorId = item.TryGetProperty("sensor_id", out var sid) ? sid.GetString()! : "unknown";
            var timestamp = item.TryGetProperty("timestamp", out var ts) ? DateTime.Parse(ts.GetString()!) : DateTime.UtcNow;
            var value = item.TryGetProperty("value", out var v) ? v.GetDouble() : (double?)null;
            var status = item.TryGetProperty("status", out var st) ? st.GetString() : null;
            
            readings.Add((sensorId, timestamp, value, status));
        }
        
        Console.WriteLine($"Parsed: {readings.Count} readings from {topic}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error parsing payload: {ex.Message}");
        Console.WriteLine($"Stack: {ex.StackTrace}");
        return;
    }

    // Parse machine from topic: imm/1/readings
    var parts = topic.Split('/');
    var machineId = parts[1];

    var connString = "Host=postgres;Database=imm_db;Username=postgres;Password=public;Trust Server Certificate=true;Ssl Mode=Disable;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=10;";
    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    Console.WriteLine($"Connected to db");

    await using var cmd = new NpgsqlCommand(@"
        INSERT INTO imm_data (machine_id, sensor_id, metric, sensor_value, sensor_val_f, value_dt, sensor_status)
        VALUES (@machine_id, @sensor_id, @metric, @sensor_value, @sensor_val_f, @value_dt, @sensor_status)", conn);

    // Use batch insert for multiple readings
    var machineIdParam = cmd.Parameters.AddWithValue("machine_id", NpgsqlDbType.Text, machineId);
    
    // We'll execute one insert per reading for simplicity (can be optimized with batch later)
    foreach (var reading in readings)
    {
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("machine_id", NpgsqlDbType.Text, machineId);
        cmd.Parameters.AddWithValue("sensor_id", NpgsqlDbType.Text, reading.sensorId);
        cmd.Parameters.AddWithValue("metric", NpgsqlDbType.Text, "value");
        cmd.Parameters.AddWithValue("sensor_value", NpgsqlDbType.Text, reading.value?.ToString() ?? "");
        cmd.Parameters.AddWithValue("sensor_val_f", NpgsqlDbType.Double, reading.value ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("value_dt", reading.valueDt);
        cmd.Parameters.AddWithValue("sensor_status", NpgsqlDbType.Text, reading.status ?? (object)DBNull.Value);
        
        try
        {
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine($"Stored: machine={machineId}, sensor={reading.sensorId}, value={reading.value}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error storing reading: {ex.Message}");
        }
    }
};

await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("imm/#").Build());
Console.WriteLine("Subscriber started. Ctrl+C to stop.");
await Task.Delay(-1);  // infinite loop
