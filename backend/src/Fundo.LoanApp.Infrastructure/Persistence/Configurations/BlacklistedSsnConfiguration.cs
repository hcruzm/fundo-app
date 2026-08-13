using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fundo.LoanApp.Infrastructure.Persistence.Configurations;

public sealed class BlacklistedSsnConfiguration : IEntityTypeConfiguration<BlacklistedSsn>
{
    public void Configure(EntityTypeBuilder<BlacklistedSsn> builder)
    {
        builder.ToTable("blacklisted_ssns");
        builder.HasKey(b => b.SsnHash);
        builder.Property(b => b.SsnHash).HasColumnName("ssn_hash");
        builder.Property(b => b.Note).HasColumnName("note").HasMaxLength(200).IsRequired();
    }
}
