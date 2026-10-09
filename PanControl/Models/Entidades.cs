using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PanControl.Models
{
    // ========================================================================
    // Cada clase representa una tabla de la base de datos.
    // Los atributos [Table] y [Column] le dicen a Entity Framework como se
    // llaman realmente las tablas y columnas en MySQL.
    // ========================================================================

    [Table("roles")]
    public class Rol
    {
        [Key]
        [Column("id_rol")]
        public int IdRol { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("descripcion")]
        public string? Descripcion { get; set; }

        // Un rol tiene muchos usuarios
        [JsonIgnore]
        public List<Usuario> Usuarios { get; set; } = new();
    }

    [Table("usuarios")]
    public class Usuario
    {
        [Key]
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Column("nombre_completo")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Column("nombre_usuario")]
        public string NombreUsuario { get; set; } = string.Empty;

        [Column("contrasena")]
        public string Contrasena { get; set; } = string.Empty;

        [Column("id_rol")]
        public int IdRol { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("fecha_registro")]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        [ForeignKey("IdRol")]
        public Rol? Rol { get; set; }
    }

    [Table("materias_primas")]
    public class MateriaPrima
    {
        [Key]
        [Column("id_materia_prima")]
        public int IdMateriaPrima { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("unidad_medida")]
        public string UnidadMedida { get; set; } = string.Empty;

        [Column("existencia")]
        public decimal Existencia { get; set; }

        [Column("stock_minimo")]
        public decimal StockMinimo { get; set; }

        [Column("costo_unitario")]
        public decimal CostoUnitario { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;

        // Propiedad calculada: no existe en la base, se calcula al vuelo.
        [NotMapped]
        public bool BajoInventario => Existencia <= StockMinimo;
    }

    [Table("productos")]
    public class Producto
    {
        [Key]
        [Column("id_producto")]
        public int IdProducto { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("descripcion")]
        public string? Descripcion { get; set; }

        [Column("precio_venta")]
        public decimal PrecioVenta { get; set; }

        [Column("existencia")]
        public int Existencia { get; set; }

        [Column("stock_minimo")]
        public int StockMinimo { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [NotMapped]
        public bool BajoInventario => Existencia <= StockMinimo;

        [JsonIgnore]
        public List<Receta> Recetas { get; set; } = new();
    }

    [Table("recetas")]
    public class Receta
    {
        [Key]
        [Column("id_receta")]
        public int IdReceta { get; set; }

        [Column("id_producto")]
        public int IdProducto { get; set; }

        [Column("id_materia_prima")]
        public int IdMateriaPrima { get; set; }

        // Cantidad de materia prima que lleva UNA unidad del producto
        [Column("cantidad")]
        public decimal Cantidad { get; set; }

        [ForeignKey("IdProducto")]
        [JsonIgnore]
        public Producto? Producto { get; set; }

        [ForeignKey("IdMateriaPrima")]
        public MateriaPrima? MateriaPrima { get; set; }
    }

    [Table("movimientos_inventario")]
    public class MovimientoInventario
    {
        [Key]
        [Column("id_movimiento")]
        public int IdMovimiento { get; set; }

        [Column("tipo")]
        public string Tipo { get; set; } = "ENTRADA";   // ENTRADA o SALIDA

        [Column("id_producto")]
        public int? IdProducto { get; set; }

        [Column("id_materia_prima")]
        public int? IdMateriaPrima { get; set; }

        [Column("cantidad")]
        public decimal Cantidad { get; set; }

        [Column("motivo")]
        public string Motivo { get; set; } = string.Empty;

        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Column("fecha")]
        public DateTime Fecha { get; set; } = DateTime.Now;

        [ForeignKey("IdProducto")]
        public Producto? Producto { get; set; }

        [ForeignKey("IdMateriaPrima")]
        public MateriaPrima? MateriaPrima { get; set; }
    }

    [Table("ventas")]
    public class Venta
    {
        [Key]
        [Column("id_venta")]
        public int IdVenta { get; set; }

        [Column("fecha")]
        public DateTime Fecha { get; set; } = DateTime.Now;

        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Column("total")]
        public decimal Total { get; set; }

        [Column("metodo_pago")]
        public string MetodoPago { get; set; } = "EFECTIVO";

        [Column("anulada")]
        public bool Anulada { get; set; }

        [ForeignKey("IdUsuario")]
        public Usuario? Usuario { get; set; }

        public List<DetalleVenta> Detalles { get; set; } = new();
    }

    [Table("detalle_ventas")]
    public class DetalleVenta
    {
        [Key]
        [Column("id_detalle")]
        public int IdDetalle { get; set; }

        [Column("id_venta")]
        public int IdVenta { get; set; }

        [Column("id_producto")]
        public int IdProducto { get; set; }

        [Column("cantidad")]
        public int Cantidad { get; set; }

        [Column("precio_unitario")]
        public decimal PrecioUnitario { get; set; }

        [Column("subtotal")]
        public decimal Subtotal { get; set; }

        [ForeignKey("IdVenta")]
        [JsonIgnore]
        public Venta? Venta { get; set; }

        [ForeignKey("IdProducto")]
        public Producto? Producto { get; set; }
    }
}
