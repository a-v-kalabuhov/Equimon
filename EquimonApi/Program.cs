using Microsoft.EntityFrameworkCore;
using EquimonApi.Data;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext with PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Minimal API services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS for frontend access
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

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.EnsureCreatedAsync();
}

app.UseCors("AllowAll");

// Enable Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- Department Endpoints (Minimal API) ---

// GET: /departments - Get all departments
app.MapGet("/departments", async (AppDbContext db) => 
    await db.Departments.ToListAsync())
    .WithName("GetAllDepartments")
    .WithOpenApi();

// GET: /departments/tree - Get departments as a hierarchical tree
app.MapGet("/departments/tree", async (AppDbContext db) =>
{
    var all = await db.Departments.ToListAsync();
    var lookup = all.ToDictionary(d => d.Id, d => new DepartmentDto(d));
    
    var roots = new List<DepartmentDto>();
    foreach (var dept in all)
    {
        var dto = lookup[dept.Id];
        if (dept.ParentId == null)
        {
            roots.Add(dto);
        }
        else if (lookup.TryGetValue(dept.ParentId.Value, out var parent))
        {
            parent.Children.Add(dto);
        }
    }
    return Results.Ok(roots);
})
.WithName("GetDepartmentTree")
.WithOpenApi();

// GET: /departments/{id} - Get department by ID
app.MapGet("/departments/{id}", async (int id, AppDbContext db) => 
    await db.Departments.FindAsync(id) is Department dept ? Results.Ok(dept) : Results.NotFound())
    .WithName("GetDepartmentById")
    .WithOpenApi();

// POST: /departments - Create department
app.MapPost("/departments", async (DepartmentCreateDto dto, AppDbContext db) =>
{
    var dept = new Department
    {
        Name = dto.Name,
        ParentId = dto.ParentId
    };
    
    db.Departments.Add(dept);
    await db.SaveChangesAsync();
    
    return Results.Created($"/departments/{dept.Id}", dept);
})
.WithName("CreateDepartment")
.WithOpenApi();

// PUT: /departments/{id} - Update department
app.MapPut("/departments/{id}", async (int id, DepartmentUpdateDto dto, AppDbContext db) =>
{
    var dept = await db.Departments.FindAsync(id);
    if (dept is null) return Results.NotFound();

    dept.Name = dto.Name;
    dept.ParentId = dto.ParentId;
    
    await db.SaveChangesAsync();
    
    return Results.Ok(dept);
})
.WithName("UpdateDepartment")
.WithOpenApi();

// DELETE: /departments/{id} - Delete department
app.MapDelete("/departments/{id}", async (int id, AppDbContext db) =>
{
    var dept = await db.Departments.FindAsync(id);
    if (dept is null) return Results.NotFound();

    db.Departments.Remove(dept);
    await db.SaveChangesAsync();
    
    return Results.NoContent();
})
.WithName("DeleteDepartment")
.WithOpenApi();

// --- Machine Endpoints (Minimal API) ---

// GET: /machines - Get all machines
app.MapGet("/machines", async (AppDbContext db) => 
    await db.Machines.Include(m => m.Department).ToListAsync())
    .WithName("GetAllMachines")
    .WithOpenApi();

// GET: /machines/{id} - Get machine by ID
app.MapGet("/machines/{id}", async (int id, AppDbContext db) => 
    await db.Machines.Include(m => m.Department).FirstOrDefaultAsync(m => m.Id == id) is Machine machine 
        ? Results.Ok(machine) 
        : Results.NotFound())
    .WithName("GetMachineById")
    .WithOpenApi();

// GET: /departments/{id}/machines - Get machines by department
app.MapGet("/departments/{id}/machines", async (int id, AppDbContext db) => 
    await db.Machines.Where(m => m.DepartmentId == id).Include(m => m.Department).ToListAsync())
    .WithName("GetMachinesByDepartment")
    .WithOpenApi();

// POST: /machines - Create machine
app.MapPost("/machines", async (MachineCreateDto dto, AppDbContext db) =>
{
    var machine = new Machine
    {
        SerialNumber = dto.SerialNumber,
        Manufacturer = dto.Manufacturer,
        Brand = dto.Brand,
        Model = dto.Model,
        DepartmentId = dto.DepartmentId
    };
    
    db.Machines.Add(machine);
    await db.SaveChangesAsync();
    
    return Results.Created($"/machines/{machine.Id}", machine);
})
.WithName("CreateMachine")
.WithOpenApi();

// PUT: /machines/{id} - Update machine
app.MapPut("/machines/{id}", async (int id, MachineUpdateDto dto, AppDbContext db) =>
{
    var machine = await db.Machines.FindAsync(id);
    if (machine is null) return Results.NotFound();

    machine.SerialNumber = dto.SerialNumber;
    machine.Manufacturer = dto.Manufacturer;
    machine.Brand = dto.Brand;
    machine.Model = dto.Model;
    machine.DepartmentId = dto.DepartmentId;
    
    await db.SaveChangesAsync();
    
    return Results.Ok(machine);
})
.WithName("UpdateMachine")
.WithOpenApi();

// DELETE: /machines/{id} - Delete machine
app.MapDelete("/machines/{id}", async (int id, AppDbContext db) =>
{
    var machine = await db.Machines.FindAsync(id);
    if (machine is null) return Results.NotFound();

    db.Machines.Remove(machine);
    await db.SaveChangesAsync();
    
    return Results.NoContent();
})
.WithName("DeleteMachine")
.WithOpenApi();

// --- Sensor Endpoints (Minimal API) ---

// GET: /sensors - Get all sensors
app.MapGet("/sensors", async (AppDbContext db) => 
    await db.Sensors.Include(s => s.Machine).ToListAsync())
    .WithName("GetAllSensors")
    .WithOpenApi();

// GET: /sensors/{id} - Get sensor by ID
app.MapGet("/sensors/{id}", async (int id, AppDbContext db) => 
    await db.Sensors.Include(s => s.Machine).FirstOrDefaultAsync(s => s.Id == id) is Sensor sensor 
        ? Results.Ok(sensor) 
        : Results.NotFound())
    .WithName("GetSensorById")
    .WithOpenApi();

// GET: /machines/{id}/sensors - Get sensors by machine
app.MapGet("/machines/{id}/sensors", async (int id, AppDbContext db) => 
    await db.Sensors.Where(s => s.MachineId == id).Include(s => s.Machine).ToListAsync())
    .WithName("GetSensorsByMachine")
    .WithOpenApi();

// POST: /sensors - Create sensor
app.MapPost("/sensors", async (SensorCreateDto dto, AppDbContext db) =>
{
    var sensor = new Sensor
    {
        Name = dto.Name,
        DataType = dto.DataType,
        Threshold = dto.Threshold,
        MachineId = dto.MachineId
    };
    
    db.Sensors.Add(sensor);
    await db.SaveChangesAsync();
    
    return Results.Created($"/sensors/{sensor.Id}", sensor);
})
.WithName("CreateSensor")
.WithOpenApi();

// PUT: /sensors/{id} - Update sensor
app.MapPut("/sensors/{id}", async (int id, SensorUpdateDto dto, AppDbContext db) =>
{
    var sensor = await db.Sensors.FindAsync(id);
    if (sensor is null) return Results.NotFound();

    sensor.Name = dto.Name;
    sensor.DataType = dto.DataType;
    sensor.Threshold = dto.Threshold;
    sensor.MachineId = dto.MachineId;
    
    await db.SaveChangesAsync();
    
    return Results.Ok(sensor);
})
.WithName("UpdateSensor")
.WithOpenApi();

// DELETE: /sensors/{id} - Delete sensor
app.MapDelete("/sensors/{id}", async (int id, AppDbContext db) =>
{
    var sensor = await db.Sensors.FindAsync(id);
    if (sensor is null) return Results.NotFound();

    db.Sensors.Remove(sensor);
    await db.SaveChangesAsync();
    
    return Results.NoContent();
})
.WithName("DeleteSensor")
.WithOpenApi();

app.MapGet("/", () => "Equimon API is running!");

app.Run();

// --- DTOs for Minimal API ---
record DepartmentCreateDto(string Name, int? ParentId);
record DepartmentUpdateDto(string Name, int? ParentId);
record DepartmentDto(int Id, string Name, int? ParentId, DateTime CreatedAt, List<DepartmentDto> Children)
{
    public DepartmentDto(Department d) : this(d.Id, d.Name, d.ParentId, d.CreatedAt, new List<DepartmentDto>()) {}
};

record MachineCreateDto(string SerialNumber, string Manufacturer, string Brand, string Model, int DepartmentId);
record MachineUpdateDto(string SerialNumber, string Manufacturer, string Brand, string Model, int DepartmentId);

record SensorCreateDto(string Name, SensorDataType DataType, double? Threshold, int MachineId);
record SensorUpdateDto(string Name, SensorDataType DataType, double? Threshold, int MachineId);
