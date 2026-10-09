using Microsoft.EntityFrameworkCore;
using PanControl.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Conexion a MySQL con Entity Framework Core (proveedor Pomelo)
// ---------------------------------------------------------------------------
var cadenaConexion = builder.Configuration.GetConnectionString("PanControlDB");

builder.Services.AddDbContext<PanControlContext>(opciones =>
    opciones.UseMySql(cadenaConexion, ServerVersion.AutoDetect(cadenaConexion)));

// ---------------------------------------------------------------------------
// 2. Controladores (carpeta Controllers) y documentacion Swagger
// ---------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Evita que EF entre en bucle al convertir objetos relacionados a JSON
        o.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---------------------------------------------------------------------------
// 3. CORS: permite que las pantallas HTML/JS consuman la API
// ---------------------------------------------------------------------------
builder.Services.AddCors(opciones =>
{
    opciones.AddPolicy("PermitirTodo", politica =>
        politica.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// 4. Pipeline
// ---------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();   // documentacion interactiva en /swagger
}

app.UseCors("PermitirTodo");

// Permite servir las pantallas HTML desde la carpeta wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();