using BusinessObject.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess
{
    public class ShareItDbContext : DbContext
    {
        public ShareItDbContext(DbContextOptions<ShareItDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Profile> Profiles { get; set; }
        public DbSet<BlacklistedToken> BlacklistedTokens { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<BankAccount> BankAccounts { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Favorite> Favorites { get; set; }
        public DbSet<Category> Categories { get; set; }

        public DbSet<ProviderApplication> ProviderApplications { get; set; }
        public DbSet<DiscountCode> DiscountCodes { get; set; }
        public DbSet<UsedDiscountCode> UsedDiscountCodes { get; set; }

        // Rental Violation tables
        public DbSet<RentalViolation> RentalViolations { get; set; }
        public DbSet<RentalViolationImage> RentalViolationImages { get; set; }

        // Deposit Refund table
        public DbSet<DepositRefund> DepositRefunds { get; set; }

        // Withdrawal Request table
        public DbSet<WithdrawalRequest> WithdrawalRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();

            modelBuilder.Entity<Notification>()
                .Property(n => n.Type)
                .HasConversion<string>();

            modelBuilder.Entity<Report>()
                .Property(r => r.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Report>()
                .Property(r => r.ReportType)
                .HasConversion<string>();

            modelBuilder.Entity<Transaction>()
                .Property(t => t.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Product>()
                .Property(t => t.AvailabilityStatus)
                .HasConversion<string>();

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .Property(t => t.Status)
                .HasConversion<string>();

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.TransactionType)
                .HasConversion<string>();
                
            modelBuilder.Entity<Order>();

            modelBuilder.Entity<CartItem>()
                .Property(ci => ci.TransactionType)
                .HasConversion<string>();

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.MessagesSent)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany(u => u.MessagesReceived)
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany(u => u.OrdersAsCustomer)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Provider)
                .WithMany(u => u.OrdersAsProvider)
                .HasForeignKey(o => o.ProviderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Report>()
                .HasOne(r => r.Reporter)
                .WithMany(u => u.ReportsMade)
                .HasForeignKey(r => r.ReporterId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Report>()
                .HasOne(r => r.Reportee)
                .WithMany(u => u.ReportsReceived)
                .HasForeignKey(r => r.ReporteeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.User1)
                .WithMany()
                .HasForeignKey(c => c.User1Id)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.User2)
                .WithMany()
                .HasForeignKey(c => c.User2Id)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.LastMessage)
                .WithMany()
                .HasForeignKey(c => c.LastMessageId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Feedback>()
                .Property(f => f.TargetType)
                .HasConversion<string>();

            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.Customer)
                .WithMany()
                .HasForeignKey(f => f.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.Product)
                .WithMany(p => p.Feedbacks)
                .HasForeignKey(f => f.ProductId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.Order)
                .WithMany()
                .HasForeignKey(f => f.OrderId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.OrderItem)
                .WithMany()
                .HasForeignKey(f => f.OrderItemId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.ProviderResponder)
                .WithMany()
                .HasForeignKey(f => f.ProviderResponseById)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Profile)
                .WithOne(p => p.User)
                .HasForeignKey<Profile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Provider)
                .WithMany(u => u.Products)
                .HasForeignKey(p => p.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Product>()
                .Property(p => p.AverageRating)
                .HasColumnType("decimal(2,1)");

            modelBuilder.Entity<Product>()
                .Property(p => p.RatingCount);

            // Add indexes for frequently queried columns (OData filtering/sorting)
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.AvailabilityStatus);

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.PricePerDay);

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.AverageRating);

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.RentCount);

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.CreatedAt);

            modelBuilder.Entity<ProductImage>()
                .HasOne(pi => pi.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(pi => pi.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Product)
                .WithMany()
                .HasForeignKey(ci => ci.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany(u => u.OrdersAsCustomer)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Provider)
                .WithMany(u => u.OrdersAsProvider)
                .HasForeignKey(o => o.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Cart)
                .WithMany(c => c.Items)
                .HasForeignKey(ci => ci.CartId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Favorite>()
                .HasKey(f => new { f.UserId, f.ProductId });

            modelBuilder.Entity<Favorite>()
                .HasOne(f => f.User)
                .WithMany(u => u.Favorites)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Favorite>()
                .HasOne(f => f.Product)
                .WithMany(p => p.Favorites)
                .HasForeignKey(f => f.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.MessagesSent)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany(u => u.MessagesReceived)
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.User1)
                .WithMany()
                .HasForeignKey(c => c.User1Id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.User2)
                .WithMany()
                .HasForeignKey(c => c.User2Id)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.LastMessage)
                .WithOne()
                .HasForeignKey<Conversation>(c => c.LastMessageId)
                .OnDelete(DeleteBehavior.ClientSetNull);



            // ProviderApplication configuration
            modelBuilder.Entity<ProviderApplication>()
                .Property(p => p.Status)
                .HasConversion<string>();

            modelBuilder.Entity<ProviderApplication>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProviderApplication>()
                .HasOne(p => p.ReviewedByAdmin)
                .WithMany()
                .HasForeignKey(p => p.ReviewedByAdminId)
                .OnDelete(DeleteBehavior.Restrict);

            // DiscountCode configuration
            modelBuilder.Entity<DiscountCode>()
                .HasIndex(dc => dc.Code)
                .IsUnique();

            modelBuilder.Entity<DiscountCode>()
                .Property(dc => dc.Value)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<DiscountCode>()
                .Property(dc => dc.DiscountType)
                .HasConversion<string>();

            modelBuilder.Entity<DiscountCode>()
                .Property(dc => dc.Status)
                .HasConversion<string>();

            modelBuilder.Entity<DiscountCode>()
                .Property(dc => dc.UsageType)
                .HasConversion<string>();

            // UsedDiscountCode configuration
            modelBuilder.Entity<UsedDiscountCode>()
                .HasOne(udc => udc.User)
                .WithMany()
                .HasForeignKey(udc => udc.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UsedDiscountCode>()
                .HasOne(udc => udc.DiscountCode)
                .WithMany(dc => dc.UsedDiscountCodes)
                .HasForeignKey(udc => udc.DiscountCodeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UsedDiscountCode>()
                .HasOne(udc => udc.Order)
                .WithMany()
                .HasForeignKey(udc => udc.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            // RentalViolation configuration - Convert enums to strings
            modelBuilder.Entity<RentalViolation>()
                .Property(rv => rv.ViolationType)
                .HasConversion<string>();

            modelBuilder.Entity<RentalViolation>()
                .Property(rv => rv.Status)
                .HasConversion<string>();

            modelBuilder.Entity<RentalViolationImage>()
                .Property(rvi => rvi.UploadedBy)
                .HasConversion<string>();

            // RentalViolation relationships
            modelBuilder.Entity<RentalViolation>()
                .HasOne(rv => rv.OrderItem)
                .WithMany()
                .HasForeignKey(rv => rv.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RentalViolationImage>()
                .HasOne(rvi => rvi.Violation)
                .WithMany(rv => rv.Images)
                .HasForeignKey(rvi => rvi.ViolationId)
                .OnDelete(DeleteBehavior.Cascade);

            // DepositRefund configuration - 1-1 relationship with Order
            modelBuilder.Entity<DepositRefund>()
                .Property(dr => dr.Status)
                .HasConversion<string>();

            modelBuilder.Entity<DepositRefund>()
                .HasOne(dr => dr.Order)
                .WithOne(o => o.DepositRefund)
                .HasForeignKey<DepositRefund>(dr => dr.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DepositRefund>()
                .HasOne(dr => dr.Customer)
                .WithMany()
                .HasForeignKey(dr => dr.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DepositRefund>()
                .HasOne(dr => dr.ProcessedByAdmin)
                .WithMany()
                .HasForeignKey(dr => dr.ProcessedByAdminId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DepositRefund>()
                .HasOne(dr => dr.RefundBankAccount)
                .WithMany()
                .HasForeignKey(dr => dr.RefundBankAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // WithdrawalRequest configuration
            modelBuilder.Entity<WithdrawalRequest>()
                .Property(wr => wr.Amount)
                .HasColumnType("decimal(18, 0)");
            
            modelBuilder.Entity<WithdrawalRequest>()
                .Property(wr => wr.Status)
                .HasConversion<string>();

            modelBuilder.Entity<WithdrawalRequest>()
                .HasOne(wr => wr.Provider)
                .WithMany()
                .HasForeignKey(wr => wr.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WithdrawalRequest>()
                .HasOne(wr => wr.ProcessedByAdmin)
                .WithMany()
                .HasForeignKey(wr => wr.ProcessedByAdminId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WithdrawalRequest>()
                .HasOne(wr => wr.BankAccount)
                .WithMany()
                .HasForeignKey(wr => wr.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            base.OnModelCreating(modelBuilder);
        }
    }
}
