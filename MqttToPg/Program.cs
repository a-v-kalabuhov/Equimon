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
    
    string sensor_value = string.Empty;
    string? sensor_status = string.Empty;
    DateTime value_dt = DateTime.Now;
    double? sensor_val_f = null;
    try
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        value_dt = root.TryGetProperty("timestamp", out var ts) ? DateTime.Parse(ts.GetString()!) : DateTime.UtcNow;
        sensor_val_f = root.TryGetProperty("value", out var f) ? f.GetDouble() : null;
        sensor_value = root.TryGetProperty("value", out var val) ? val.ToString() : string.Empty;
        sensor_status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
        Console.WriteLine($"Parsed: {topic} -> value_dt={value_dt}; sensor_value={sensor_value}; sensor_val_f={sensor_val_f}; status={sensor_status}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine($"Stack: {ex.StackTrace}");
        return;
    }

    // Parse machine/sensor from topic: imm/1/temp_sensor_001/value
    var parts = topic.Split('/');
    var machineId = parts[1];
    var sensorId = parts[2];
    var metric = parts[3];

    Console.WriteLine($"Parsed: {topic} -> machineId={machineId}; sensorId={sensorId}; metric={metric}");

    var connString = "Host=postgres;Database=imm_db;Username=postgres;Password=public;Trust Server Certificate=true;Ssl Mode=Disable;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=10;";
    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    Console.WriteLine($"Connected to db");

    await using var cmd = new NpgsqlCommand(@"
        INSERT INTO imm_data (machine_id, sensor_id, metric, sensor_value, sensor_val_f, value_dt, sensor_status)
        VALUES (@machine_id, @sensor_id, @metric, @sensor_value, @sensor_val_f, @value_dt, @sensor_status)", conn);
    Console.WriteLine($"Command created");

    var p1 = cmd.Parameters.AddWithValue("machine_id", NpgsqlDbType.Text, machineId);
    Console.WriteLine($"Params prepared: machine_id={p1.Value}");
    var p2 = cmd.Parameters.AddWithValue("sensor_id", NpgsqlDbType.Text, sensorId);
    Console.WriteLine($"Params prepared: sensor_id={p2.Value}");
    var p3 = cmd.Parameters.AddWithValue("metric", NpgsqlDbType.Text, metric);
    Console.WriteLine($"Params prepared: metric={p3.Value}");
    var p4 = cmd.Parameters.AddWithValue("sensor_value", NpgsqlDbType.Text, sensor_value);
    Console.WriteLine($"Params prepared: sensor_value={p4.Value}");
    var p5 = cmd.Parameters.AddWithValue("sensor_val_f", NpgsqlDbType.Double, sensor_val_f ?? (object)DBNull.Value);
    Console.WriteLine($"Params prepared: sensor_val_f={p5.Value}");
    var p6 = cmd.Parameters.AddWithValue("value_dt", value_dt);
    Console.WriteLine($"Params prepared: value_dt={p6.Value}");
    var p7 = cmd.Parameters.AddWithValue("sensor_status", NpgsqlDbType.Text, sensor_status ?? (object)DBNull.Value);
    Console.WriteLine($"Params prepared: sensor_status={p7.Value}");
    //
    try
    {
        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine($"Stored: {topic} -> {sensor_value}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine($"Stack: {ex.StackTrace}");
    }
};

await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("imm/#").Build());
Console.WriteLine("Subscrber started. Ctrl+C to stop.");
await Task.Delay(-1);  // infinite loop
