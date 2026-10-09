using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PanControl.Data;
using PanControl.Models;

namespace PanControl.Controllers
{
    // ========================================================================
    // Clases para recibir la venta desde la pantalla del cajero.
    // No son tablas: solo sirven para transportar datos (DTO).
    // ========================================================================
    public class VentaRequest
    {
        public int IdUsuario { get; set; }
        public string MetodoPago { get; set; } = "EFECTIVO";
        public List<LineaVenta> Productos { get; set; } = new();
    }

    public class LineaVenta
    {
        public int IdProducto { get; set; }
        public int Cantidad { get; set; }
    }

    // ========================================================================
    // MODULO 4: VENTAS
    // Rutas: /api/ventas
    // ========================================================================
    [ApiController]
    [Route("api/[controller]")]
    public class VentasController : ControllerBase
    {
        private readonly PanControlContext _contexto;

        public VentasController(PanControlContext contexto)
        {
            _contexto = contexto;
        }

        // --------------------------------------------------------------------
        // POST /api/ventas  -> registra una venta completa
        //
        // Pasos:
        //   1. Validar que vengan productos y que el usuario exista.
        //   2. Verificar que haya existencia suficiente de cada producto.
        //   3. Calcular el subtotal de cada linea y el total de la venta.
        //   4. Descontar el inventario.
        //   5. Guardar todo dentro de una TRANSACCION: si algo falla, no se
        //      guarda nada y el inventario no queda descuadrado.
        // --------------------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> RegistrarVenta([FromBody] VentaRequest peticion)
        {
            // --- 1. Validaciones de entrada -------------------------------
            if (peticion.Productos == null || peticion.Productos.Count == 0)
                return BadRequest(new { error = "La venta debe tener al menos un producto." });

            var usuario = await _contexto.Usuarios.FindAsync(peticion.IdUsuario);
            if (usuario == null)
                return BadRequest(new { error = "El usuario que registra la venta no existe." });

            // Se usa una transaccion para que todo se guarde o nada se guarde
            using var transaccion = await _contexto.Database.BeginTransactionAsync();

            try
            {
                var venta = new Venta
                {
                    Fecha = DateTime.Now,
                    IdUsuario = peticion.IdUsuario,
                    MetodoPago = peticion.MetodoPago,
                    Total = 0
                };

                decimal total = 0;
                var detalles = new List<DetalleVenta>();

                // --- 2 y 3. Validar existencias y calcular importes --------
                foreach (var linea in peticion.Productos)
                {
                    if (linea.Cantidad <= 0)
                        return BadRequest(new { error = "Las cantidades deben ser mayores a cero." });

                    var producto = await _contexto.Productos.FindAsync(linea.IdProducto);

                    if (producto == null)
                        return BadRequest(new { error = $"El producto con id {linea.IdProducto} no existe." });

                    if (!producto.Activo)
                        return BadRequest(new { error = $"El producto '{producto.Nombre}' no esta disponible." });

                    if (producto.Existencia < linea.Cantidad)
                        return BadRequest(new
                        {
                            error = $"No hay suficiente '{producto.Nombre}'. " +
                                    $"Disponible: {producto.Existencia}, solicitado: {linea.Cantidad}."
                        });

                    decimal subtotal = producto.PrecioVenta * linea.Cantidad;
                    total += subtotal;

                    detalles.Add(new DetalleVenta
                    {
                        IdProducto = producto.IdProducto,
                        Cantidad = linea.Cantidad,
                        PrecioUnitario = producto.PrecioVenta,
                        Subtotal = subtotal
                    });

                    // --- 4. Descontar del inventario -----------------------
                    producto.Existencia -= linea.Cantidad;

                    // Queda registrado en el historial de movimientos
                    _contexto.Movimientos.Add(new MovimientoInventario
                    {
                        Tipo = "SALIDA",
                        IdProducto = producto.IdProducto,
                        Cantidad = linea.Cantidad,
                        Motivo = "Venta",
                        IdUsuario = peticion.IdUsuario,
                        Fecha = DateTime.Now
                    });
                }

                venta.Total = total;
                venta.Detalles = detalles;

                // --- 5. Guardar ------------------------------------------
                _contexto.Ventas.Add(venta);
                await _contexto.SaveChangesAsync();
                await transaccion.CommitAsync();

                return Ok(new
                {
                    mensaje = "Venta registrada correctamente.",
                    venta.IdVenta,
                    venta.Fecha,
                    Cajero = usuario.NombreCompleto,
                    venta.MetodoPago,
                    Total = venta.Total,
                    Detalle = detalles.Select(d => new
                    {
                        d.IdProducto,
                        d.Cantidad,
                        d.PrecioUnitario,
                        d.Subtotal
                    })
                });
            }
            catch (Exception ex)
            {
                await transaccion.RollbackAsync();
                return StatusCode(500, new
                {
                    error = "Ocurrio un error al registrar la venta.",
                    detalle = ex.Message
                });
            }
        }

        // GET /api/ventas  -> listado general (ultimas 100)
        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var ventas = await _contexto.Ventas
                .Include(v => v.Usuario)
                .Where(v => !v.Anulada)
                .OrderByDescending(v => v.Fecha)
                .Take(100)
                .Select(v => new
                {
                    v.IdVenta,
                    v.Fecha,
                    Cajero = v.Usuario!.NombreCompleto,
                    v.MetodoPago,
                    v.Total
                })
                .ToListAsync();

            return Ok(ventas);
        }

        // GET /api/ventas/5  -> una venta con su detalle completo
        [HttpGet("{id}")]
        public async Task<IActionResult> Obtener(int id)
        {
            var venta = await _contexto.Ventas
                .Include(v => v.Usuario)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.IdVenta == id);

            if (venta == null)
                return NotFound(new { error = $"No existe la venta con id {id}." });

            return Ok(new
            {
                venta.IdVenta,
                venta.Fecha,
                Cajero = venta.Usuario!.NombreCompleto,
                venta.MetodoPago,
                venta.Total,
                venta.Anulada,
                Detalle = venta.Detalles.Select(d => new
                {
                    Producto = d.Producto!.Nombre,
                    d.Cantidad,
                    d.PrecioUnitario,
                    d.Subtotal
                })
            });
        }

        // POST /api/ventas/5/anular -> devuelve los productos al inventario
        [HttpPost("{id}/anular")]
        public async Task<IActionResult> Anular(int id, [FromQuery] int idUsuario)
        {
            var venta = await _contexto.Ventas
                .Include(v => v.Detalles)
                .FirstOrDefaultAsync(v => v.IdVenta == id);

            if (venta == null)
                return NotFound(new { error = $"No existe la venta con id {id}." });

            if (venta.Anulada)
                return BadRequest(new { error = "Esta venta ya fue anulada." });

            using var transaccion = await _contexto.Database.BeginTransactionAsync();
            try
            {
                foreach (var detalle in venta.Detalles)
                {
                    var producto = await _contexto.Productos.FindAsync(detalle.IdProducto);
                    if (producto != null)
                    {
                        producto.Existencia += detalle.Cantidad;   // regresa al stock

                        _contexto.Movimientos.Add(new MovimientoInventario
                        {
                            Tipo = "ENTRADA",
                            IdProducto = producto.IdProducto,
                            Cantidad = detalle.Cantidad,
                            Motivo = $"Anulacion de la venta #{venta.IdVenta}",
                            IdUsuario = idUsuario,
                            Fecha = DateTime.Now
                        });
                    }
                }

                venta.Anulada = true;
                await _contexto.SaveChangesAsync();
                await transaccion.CommitAsync();

                return Ok(new { mensaje = $"Venta #{venta.IdVenta} anulada y productos devueltos al inventario." });
            }
            catch (Exception ex)
            {
                await transaccion.RollbackAsync();
                return StatusCode(500, new { error = "No se pudo anular la venta.", detalle = ex.Message });
            }
        }
    }

    // ========================================================================
    // MODULO 5: PRODUCCION
    // El encargado registra cuanto produjo: suma productos y descuenta las
    // materias primas segun la receta.
    // Rutas: /api/produccion
    // ========================================================================
    public class ProduccionRequest
    {
        public int IdProducto { get; set; }
        public int Cantidad { get; set; }
        public int IdUsuario { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class ProduccionController : ControllerBase
    {
        private readonly PanControlContext _contexto;

        public ProduccionController(PanControlContext contexto)
        {
            _contexto = contexto;
        }

        // POST /api/produccion
        // Ejemplo: se produjeron 100 unidades de pan frances
        [HttpPost]
        public async Task<IActionResult> RegistrarProduccion([FromBody] ProduccionRequest peticion)
        {
            if (peticion.Cantidad <= 0)
                return BadRequest(new { error = "La cantidad producida debe ser mayor a cero." });

            var producto = await _contexto.Productos.FindAsync(peticion.IdProducto);
            if (producto == null)
                return BadRequest(new { error = "El producto indicado no existe." });

            // Ingredientes necesarios segun la receta
            var receta = await _contexto.Recetas
                .Include(r => r.MateriaPrima)
                .Where(r => r.IdProducto == peticion.IdProducto)
                .ToListAsync();

            using var transaccion = await _contexto.Database.BeginTransactionAsync();
            try
            {
                var consumos = new List<object>();

                // Verificar que alcancen las materias primas
                foreach (var ingrediente in receta)
                {
                    decimal necesario = ingrediente.Cantidad * peticion.Cantidad;
                    var materia = ingrediente.MateriaPrima!;

                    if (materia.Existencia < necesario)
                        return BadRequest(new
                        {
                            error = $"No hay suficiente {materia.Nombre}. " +
                                    $"Se necesitan {necesario} {materia.UnidadMedida} y hay {materia.Existencia}."
                        });
                }

                // Descontar materias primas
                foreach (var ingrediente in receta)
                {
                    decimal necesario = ingrediente.Cantidad * peticion.Cantidad;
                    var materia = await _contexto.MateriasPrimas.FindAsync(ingrediente.IdMateriaPrima);

                    materia!.Existencia -= necesario;

                    _contexto.Movimientos.Add(new MovimientoInventario
                    {
                        Tipo = "SALIDA",
                        IdMateriaPrima = materia.IdMateriaPrima,
                        Cantidad = necesario,
                        Motivo = $"Produccion de {peticion.Cantidad} {producto.Nombre}",
                        IdUsuario = peticion.IdUsuario,
                        Fecha = DateTime.Now
                    });

                    consumos.Add(new
                    {
                        materia.Nombre,
                        Consumido = necesario,
                        materia.UnidadMedida,
                        Restante = materia.Existencia
                    });
                }

                // Sumar el producto terminado
                producto.Existencia += peticion.Cantidad;

                _contexto.Movimientos.Add(new MovimientoInventario
                {
                    Tipo = "ENTRADA",
                    IdProducto = producto.IdProducto,
                    Cantidad = peticion.Cantidad,
                    Motivo = "Produccion del dia",
                    IdUsuario = peticion.IdUsuario,
                    Fecha = DateTime.Now
                });

                await _contexto.SaveChangesAsync();
                await transaccion.CommitAsync();

                return Ok(new
                {
                    mensaje = $"Se registraron {peticion.Cantidad} unidades de {producto.Nombre}.",
                    ExistenciaActual = producto.Existencia,
                    MateriasPrimasConsumidas = consumos
                });
            }
            catch (Exception ex)
            {
                await transaccion.RollbackAsync();
                return StatusCode(500, new { error = "No se pudo registrar la produccion.", detalle = ex.Message });
            }
        }
    }

    // ========================================================================
    // MODULO 6: REPORTES
    // Rutas: /api/reportes
    // ========================================================================
    [ApiController]
    [Route("api/[controller]")]
    public class ReportesController : ControllerBase
    {
        private readonly PanControlContext _contexto;

        public ReportesController(PanControlContext contexto)
        {
            _contexto = contexto;
        }

        // GET /api/reportes/ventas-dia
        [HttpGet("ventas-dia")]
        public async Task<IActionResult> VentasDelDia()
        {
            var hoy = DateTime.Today;
            var manana = hoy.AddDays(1);

            var ventas = await _contexto.Ventas
                .Where(v => !v.Anulada && v.Fecha >= hoy && v.Fecha < manana)
                .ToListAsync();

            return Ok(new
            {
                Fecha = hoy.ToString("dd/MM/yyyy"),
                CantidadVentas = ventas.Count,
                TotalVendido = ventas.Sum(v => v.Total)
            });
        }

        // GET /api/reportes/ventas-periodo?desde=2026-09-01&hasta=2026-09-30
        [HttpGet("ventas-periodo")]
        public async Task<IActionResult> VentasPorPeriodo(
            [FromQuery] DateTime desde, [FromQuery] DateTime hasta)
        {
            if (desde > hasta)
                return BadRequest(new { error = "La fecha inicial no puede ser mayor que la final." });

            var hastaFin = hasta.Date.AddDays(1);

            var ventas = await _contexto.Ventas
                .Where(v => !v.Anulada && v.Fecha >= desde.Date && v.Fecha < hastaFin)
                .ToListAsync();

            var porDia = ventas
                .GroupBy(v => v.Fecha.Date)
                .Select(g => new
                {
                    Fecha = g.Key.ToString("dd/MM/yyyy"),
                    Ventas = g.Count(),
                    Total = g.Sum(v => v.Total)
                })
                .OrderBy(x => x.Fecha);

            return Ok(new
            {
                Desde = desde.ToString("dd/MM/yyyy"),
                Hasta = hasta.ToString("dd/MM/yyyy"),
                TotalGeneral = ventas.Sum(v => v.Total),
                CantidadVentas = ventas.Count,
                Detalle = porDia
            });
        }

        // GET /api/reportes/productos-mas-vendidos?top=5
        [HttpGet("productos-mas-vendidos")]
        public async Task<IActionResult> ProductosMasVendidos([FromQuery] int top = 5)
        {
            var resultado = await _contexto.DetalleVentas
                .Include(d => d.Producto)
                .Include(d => d.Venta)
                .Where(d => !d.Venta!.Anulada)
                .GroupBy(d => d.Producto!.Nombre)
                .Select(g => new
                {
                    Producto = g.Key,
                    UnidadesVendidas = g.Sum(d => d.Cantidad),
                    TotalGenerado = g.Sum(d => d.Subtotal)
                })
                .OrderByDescending(x => x.UnidadesVendidas)
                .Take(top)
                .ToListAsync();

            return Ok(resultado);
        }

        // GET /api/reportes/existencias -> productos y materias primas
        [HttpGet("existencias")]
        public async Task<IActionResult> Existencias()
        {
            var productos = await _contexto.Productos
                .Where(p => p.Activo)
                .Select(p => new { p.Nombre, p.Existencia, p.StockMinimo })
                .ToListAsync();

            var materias = await _contexto.MateriasPrimas
                .Where(m => m.Activo)
                .Select(m => new { m.Nombre, m.Existencia, m.StockMinimo, m.UnidadMedida })
                .ToListAsync();

            return Ok(new { Productos = productos, MateriasPrimas = materias });
        }

        // GET /api/reportes/bajo-inventario -> alertas para el administrador
        [HttpGet("bajo-inventario")]
        public async Task<IActionResult> BajoInventario()
        {
            var productos = await _contexto.Productos
                .Where(p => p.Activo && p.Existencia <= p.StockMinimo)
                .Select(p => new { p.Nombre, p.Existencia, p.StockMinimo })
                .ToListAsync();

            var materias = await _contexto.MateriasPrimas
                .Where(m => m.Activo && m.Existencia <= m.StockMinimo)
                .Select(m => new { m.Nombre, m.Existencia, m.StockMinimo, m.UnidadMedida })
                .ToListAsync();

            return Ok(new
            {
                TotalAlertas = productos.Count + materias.Count,
                Productos = productos,
                MateriasPrimas = materias
            });
        }
    }
}
