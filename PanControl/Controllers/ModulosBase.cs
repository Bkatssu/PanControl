using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PanControl.Data;
using PanControl.Models;

namespace PanControl.Controllers
{
    // ========================================================================
    // MODULO 1: USUARIOS
    // Rutas: /api/usuarios
    // ========================================================================
    [ApiController]
    [Route("api/[controller]")]
    public class UsuariosController : ControllerBase
    {
        private readonly PanControlContext _contexto;

        public UsuariosController(PanControlContext contexto)
        {
            _contexto = contexto;
        }

        // GET /api/usuarios  -> lista todos los usuarios con su rol
        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var usuarios = await _contexto.Usuarios
                .Include(u => u.Rol)
                .Select(u => new
                {
                    u.IdUsuario,
                    u.NombreCompleto,
                    u.NombreUsuario,
                    u.Activo,
                    Rol = u.Rol!.Nombre
                })
                .ToListAsync();

            return Ok(usuarios);
        }

        // GET /api/usuarios/3
        [HttpGet("{id}")]
        public async Task<IActionResult> Obtener(int id)
        {
            var usuario = await _contexto.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.IdUsuario == id);

            if (usuario == null)
                return NotFound(new { error = $"No existe el usuario con id {id}." });

            return Ok(usuario);
        }

        // POST /api/usuarios  -> crea un usuario
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] Usuario usuario)
        {
            if (string.IsNullOrWhiteSpace(usuario.NombreUsuario))
                return BadRequest(new { error = "El nombre de usuario es obligatorio." });

            bool existe = await _contexto.Usuarios
                .AnyAsync(u => u.NombreUsuario == usuario.NombreUsuario);

            if (existe)
                return BadRequest(new { error = "Ya existe un usuario con ese nombre." });

            usuario.FechaRegistro = DateTime.Now;
            _contexto.Usuarios.Add(usuario);
            await _contexto.SaveChangesAsync();

            return CreatedAtAction(nameof(Obtener),
                new { id = usuario.IdUsuario }, usuario);
        }

        // PUT /api/usuarios/3  -> actualiza los datos
        [HttpPut("{id}")]
        public async Task<IActionResult> Actualizar(int id, [FromBody] Usuario datos)
        {
            var usuario = await _contexto.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound(new { error = $"No existe el usuario con id {id}." });

            usuario.NombreCompleto = datos.NombreCompleto;
            usuario.IdRol = datos.IdRol;
            usuario.Activo = datos.Activo;

            if (!string.IsNullOrWhiteSpace(datos.Contrasena))
                usuario.Contrasena = datos.Contrasena;

            await _contexto.SaveChangesAsync();
            return Ok(usuario);
        }

        // DELETE /api/usuarios/3 -> se desactiva, no se borra (baja logica)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Desactivar(int id)
        {
            var usuario = await _contexto.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound(new { error = $"No existe el usuario con id {id}." });

            usuario.Activo = false;
            await _contexto.SaveChangesAsync();

            return Ok(new { mensaje = $"Usuario '{usuario.NombreUsuario}' desactivado." });
        }

        // POST /api/usuarios/login  -> valida el acceso al sistema
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest datos)
        {
            var usuario = await _contexto.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u =>
                    u.NombreUsuario == datos.NombreUsuario &&
                    u.Contrasena == datos.Contrasena &&
                    u.Activo);

            if (usuario == null)
                return Unauthorized(new { error = "Usuario o contrasena incorrectos." });

            return Ok(new
            {
                usuario.IdUsuario,
                usuario.NombreCompleto,
                Rol = usuario.Rol!.Nombre
            });
        }
    }

    // Clase auxiliar para recibir los datos del login
    public class LoginRequest
    {
        public string NombreUsuario { get; set; } = string.Empty;
        public string Contrasena { get; set; } = string.Empty;
    }

    // ========================================================================
    // MODULO 2: MATERIAS PRIMAS
    // Rutas: /api/materiasprimas
    // ========================================================================
    [ApiController]
    [Route("api/[controller]")]
    public class MateriasPrimasController : ControllerBase
    {
        private readonly PanControlContext _contexto;

        public MateriasPrimasController(PanControlContext contexto)
        {
            _contexto = contexto;
        }

        // GET /api/materiasprimas
        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var materias = await _contexto.MateriasPrimas
                .Where(m => m.Activo)
                .OrderBy(m => m.Nombre)
                .ToListAsync();

            return Ok(materias);
        }

        // GET /api/materiasprimas/bajo-inventario -> alerta de reabastecimiento
        [HttpGet("bajo-inventario")]
        public async Task<IActionResult> BajoInventario()
        {
            var materias = await _contexto.MateriasPrimas
                .Where(m => m.Activo && m.Existencia <= m.StockMinimo)
                .OrderBy(m => m.Existencia)
                .ToListAsync();

            return Ok(materias);
        }

        // POST /api/materiasprimas
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] MateriaPrima materia)
        {
            if (string.IsNullOrWhiteSpace(materia.Nombre))
                return BadRequest(new { error = "El nombre es obligatorio." });

            if (materia.Existencia < 0 || materia.StockMinimo < 0)
                return BadRequest(new { error = "Las cantidades no pueden ser negativas." });

            _contexto.MateriasPrimas.Add(materia);
            await _contexto.SaveChangesAsync();

            return Ok(materia);
        }

        // PUT /api/materiasprimas/1
        [HttpPut("{id}")]
        public async Task<IActionResult> Actualizar(int id, [FromBody] MateriaPrima datos)
        {
            var materia = await _contexto.MateriasPrimas.FindAsync(id);
            if (materia == null)
                return NotFound(new { error = $"No existe la materia prima con id {id}." });

            materia.Nombre = datos.Nombre;
            materia.UnidadMedida = datos.UnidadMedida;
            materia.StockMinimo = datos.StockMinimo;
            materia.CostoUnitario = datos.CostoUnitario;

            await _contexto.SaveChangesAsync();
            return Ok(materia);
        }

        // POST /api/materiasprimas/1/entrada?cantidad=50&idUsuario=1
        // Registra una compra: suma existencia y deja el movimiento en el historial
        [HttpPost("{id}/entrada")]
        public async Task<IActionResult> RegistrarEntrada(
            int id, [FromQuery] decimal cantidad, [FromQuery] int idUsuario)
        {
            if (cantidad <= 0)
                return BadRequest(new { error = "La cantidad debe ser mayor a cero." });

            var materia = await _contexto.MateriasPrimas.FindAsync(id);
            if (materia == null)
                return NotFound(new { error = $"No existe la materia prima con id {id}." });

            materia.Existencia += cantidad;

            _contexto.Movimientos.Add(new MovimientoInventario
            {
                Tipo = "ENTRADA",
                IdMateriaPrima = id,
                Cantidad = cantidad,
                Motivo = "Compra de materia prima",
                IdUsuario = idUsuario,
                Fecha = DateTime.Now
            });

            await _contexto.SaveChangesAsync();

            return Ok(new
            {
                mensaje = $"Entrada registrada. {materia.Nombre} tiene ahora {materia.Existencia} {materia.UnidadMedida}.",
                materia
            });
        }
    }

    // ========================================================================
    // MODULO 3: PRODUCTOS Y RECETAS
    // Rutas: /api/productos
    // ========================================================================
    [ApiController]
    [Route("api/[controller]")]
    public class ProductosController : ControllerBase
    {
        private readonly PanControlContext _contexto;

        public ProductosController(PanControlContext contexto)
        {
            _contexto = contexto;
        }

        // GET /api/productos  -> catalogo para la pantalla de ventas
        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var productos = await _contexto.Productos
                .Where(p => p.Activo)
                .OrderBy(p => p.Nombre)
                .Select(p => new
                {
                    p.IdProducto,
                    p.Nombre,
                    p.Descripcion,
                    p.PrecioVenta,
                    p.Existencia,
                    p.StockMinimo,
                    BajoInventario = p.Existencia <= p.StockMinimo
                })
                .ToListAsync();

            return Ok(productos);
        }

        // GET /api/productos/2
        [HttpGet("{id}")]
        public async Task<IActionResult> Obtener(int id)
        {
            var producto = await _contexto.Productos.FindAsync(id);
            if (producto == null)
                return NotFound(new { error = $"No existe el producto con id {id}." });

            return Ok(producto);
        }

        // GET /api/productos/2/receta -> ingredientes del producto
        [HttpGet("{id}/receta")]
        public async Task<IActionResult> ObtenerReceta(int id)
        {
            var producto = await _contexto.Productos.FindAsync(id);
            if (producto == null)
                return NotFound(new { error = $"No existe el producto con id {id}." });

            var receta = await _contexto.Recetas
                .Include(r => r.MateriaPrima)
                .Where(r => r.IdProducto == id)
                .Select(r => new
                {
                    r.IdReceta,
                    r.IdMateriaPrima,
                    MateriaPrima = r.MateriaPrima!.Nombre,
                    r.Cantidad,
                    Unidad = r.MateriaPrima!.UnidadMedida
                })
                .ToListAsync();

            return Ok(new { producto = producto.Nombre, ingredientes = receta });
        }

        // POST /api/productos
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] Producto producto)
        {
            if (string.IsNullOrWhiteSpace(producto.Nombre))
                return BadRequest(new { error = "El nombre del producto es obligatorio." });

            if (producto.PrecioVenta <= 0)
                return BadRequest(new { error = "El precio de venta debe ser mayor a cero." });

            _contexto.Productos.Add(producto);
            await _contexto.SaveChangesAsync();

            return Ok(producto);
        }

        // POST /api/productos/2/receta -> agrega un ingrediente a la receta
        [HttpPost("{id}/receta")]
        public async Task<IActionResult> AgregarIngrediente(int id, [FromBody] Receta receta)
        {
            var producto = await _contexto.Productos.FindAsync(id);
            if (producto == null)
                return NotFound(new { error = $"No existe el producto con id {id}." });

            var materia = await _contexto.MateriasPrimas.FindAsync(receta.IdMateriaPrima);
            if (materia == null)
                return BadRequest(new { error = "La materia prima indicada no existe." });

            if (receta.Cantidad <= 0)
                return BadRequest(new { error = "La cantidad debe ser mayor a cero." });

            receta.IdProducto = id;
            _contexto.Recetas.Add(receta);
            await _contexto.SaveChangesAsync();

            return Ok(new
            {
                mensaje = $"Se agrego {materia.Nombre} a la receta de {producto.Nombre}."
            });
        }

        // PUT /api/productos/2
        [HttpPut("{id}")]
        public async Task<IActionResult> Actualizar(int id, [FromBody] Producto datos)
        {
            var producto = await _contexto.Productos.FindAsync(id);
            if (producto == null)
                return NotFound(new { error = $"No existe el producto con id {id}." });

            producto.Nombre = datos.Nombre;
            producto.Descripcion = datos.Descripcion;
            producto.PrecioVenta = datos.PrecioVenta;
            producto.StockMinimo = datos.StockMinimo;

            await _contexto.SaveChangesAsync();
            return Ok(producto);
        }

        // DELETE /api/productos/2 -> baja logica
        [HttpDelete("{id}")]
        public async Task<IActionResult> Desactivar(int id)
        {
            var producto = await _contexto.Productos.FindAsync(id);
            if (producto == null)
                return NotFound(new { error = $"No existe el producto con id {id}." });

            producto.Activo = false;
            await _contexto.SaveChangesAsync();

            return Ok(new { mensaje = $"Producto '{producto.Nombre}' desactivado." });
        }
    }
}
