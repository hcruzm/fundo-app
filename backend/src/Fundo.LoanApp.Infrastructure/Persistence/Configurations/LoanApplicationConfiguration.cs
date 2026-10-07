using Fundo.LoanApp.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fundo.LoanApp.Infrastructure.Persistence.Configurations;

public sealed class LoanApplicationConfiguration : IEntityTypeConfiguration<LoanApplication>
{
    public void Configure(EntityTypeBuilder<LoanApplication> builder)
    {
        builder.ToTable("applications");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(a => a.RequestedAmount).HasColumnName("requested_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Unique, not just indexed: the database enforces the same one-customer-to-one-
        // application rule as the aggregate. The relationship is configured on Customer.
        builder.HasIndex(a => a.CustomerId)
            .IsUnique()
            .HasDatabaseName("ix_applications_customer_id");
    }
}
