using Microsoft.EntityFrameworkCore;
using PanControl.Models;

namespace PanControl.Data
{
    // ========================================================================
    // El DbContext es el puente entre las clases de C# y la base de datos.
    // Cada DbSet<T> representa una tabla sobre la que se pueden hacer
    // consultas con LINQ (Where, OrderBy, Include, etc.).
    // ========================================================================
    public class PanControlContext : DbContext
    {
        public PanControlContext(DbContextOptions<PanControlContext> options)
            : base(options) { }

        public DbSet<Rol> Roles => Set<Rol>();
        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<MateriaPrima> MateriasPrimas => Set<MateriaPrima>();
        public DbSet<Producto> Productos => Set<Producto>();
        public DbSet<Receta> Recetas => Set<Receta>();
        public DbSet<MovimientoInventario> Movimientos => Set<MovimientoInventario>();
        public DbSet<Venta> Ventas => Set<Venta>();
        public DbSet<DetalleVenta> DetalleVentas => Set<DetalleVenta>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Precision de los decimales para que coincida con MySQL
            modelBuilder.Entity<MateriaPrima>().Property(m => m.Existencia).HasPrecision(10, 2);
            modelBuilder.Entity<MateriaPrima>().Property(m => m.StockMinimo).HasPrecision(10, 2);
            modelBuilder.Entity<MateriaPrima>().Property(m => m.CostoUnitario).HasPrecision(10, 2);
            modelBuilder.Entity<Producto>().Property(p => p.PrecioVenta).HasPrecision(10, 2);
            modelBuilder.Entity<Receta>().Property(r => r.Cantidad).HasPrecision(10, 3);
            modelBuilder.Entity<MovimientoInventario>().Property(m => m.Cantidad).HasPrecision(10, 2);
            modelBuilder.Entity<Venta>().Property(v => v.Total).HasPrecision(10, 2);
            modelBuilder.Entity<DetalleVenta>().Property(d => d.PrecioUnitario).HasPrecision(10, 2);
            modelBuilder.Entity<DetalleVenta>().Property(d => d.Subtotal).HasPrecision(10, 2);

            // Una venta tiene muchos detalles; si se borra la venta se borran
            modelBuilder.Entity<Venta>()
                .HasMany(v => v.Detalles)
                .WithOne(d => d.Venta!)
                .HasForeignKey(d => d.IdVenta)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
