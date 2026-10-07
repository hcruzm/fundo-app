using Fundo.LoanApp.Domain.Applications;
using Fundo.LoanApp.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fundo.LoanApp.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.SsnHash)
            .HasConversion(hash => hash.Value, value => new SsnHash(value))
            .HasColumnName("ssn_hash")
            .IsRequired();

        builder.HasIndex(c => c.SsnHash)
            .IsUnique()
            .HasDatabaseName("ix_customers_ssn_hash");

        builder.Property(c => c.SsnLast4).HasColumnName("ssn_last4").HasMaxLength(4).IsFixedLength().IsRequired();
        builder.Property(c => c.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        builder.Property(c => c.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
        builder.Property(c => c.CompanyName).HasColumnName("company_name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.OwnsOne(c => c.Address, address =>
        {
            address.Property(a => a.Street).HasColumnName("street").HasMaxLength(200).IsRequired();
            address.Property(a => a.City).HasColumnName("city").HasMaxLength(100).IsRequired();
            address.Property(a => a.State).HasColumnName("state").HasMaxLength(2).IsFixedLength().IsRequired();
            address.Property(a => a.PostalCode).HasColumnName("postal_code").HasMaxLength(10).IsRequired();
        });

        builder.Navigation(c => c.Address).IsRequired();

        // One customer, one application: the application is part of the aggregate and is
        // always loaded with it.
        builder.HasOne(c => c.Application)
            .WithOne()
            .HasForeignKey<LoanApplication>(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(c => c.Application).IsRequired().AutoInclude();
    }
}
