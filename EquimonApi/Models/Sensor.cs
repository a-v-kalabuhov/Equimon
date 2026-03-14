namespace EquimonApi.Models;

public enum SensorDataType
{
    Integer,      // Целое число
    Float,        // Вещественное число
    Boolean,      // Булево значение (0/1)
    String        // Строка
}

public class Sensor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SensorDataType DataType { get; set; }
    public double? Threshold { get; set; }  // Порог изменения (только для Integer и Float)
    public int MachineId { get; set; }
    public Machine? Machine { get; set; }
}
