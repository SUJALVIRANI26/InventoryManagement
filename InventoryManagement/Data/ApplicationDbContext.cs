using InventoryManagement.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
    {
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
        public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
        public DbSet<SaleOrder> SaleOrders => Set<SaleOrder>();
        public DbSet<SaleOrderItem> SaleOrderItems => Set<SaleOrderItem>();
        public DbSet<BacklogItem> BacklogItems => Set<BacklogItem>();
        public DbSet<BacklogFulfillmentLog> BacklogFulfillmentLogs => Set<BacklogFulfillmentLog>();



        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Product>()
                .HasIndex(p => p.Sku)
                .IsUnique();

            builder.Entity<PurchaseOrder>()
                .HasOne(p => p.CreatedBy)
                .WithMany()
                .HasForeignKey(p => p.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SaleOrder>()
                .HasOne(s => s.SalesUser)
                .WithMany()
                .HasForeignKey(s => s.SalesUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BacklogItem>()
                .HasOne(b => b.SaleOrder)
                .WithMany(s => s.BacklogItems)
                .HasForeignKey(b => b.SaleOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<BacklogItem>()
                .HasOne(b => b.SaleOrderItem)
                .WithMany()
                .HasForeignKey(b => b.SaleOrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
