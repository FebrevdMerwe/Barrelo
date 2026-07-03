using Darts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Darts.Infrastructure.Persistence.Configurations;

public sealed class ThrowRecordConfiguration : IEntityTypeConfiguration<ThrowRecord>
{
    public void Configure(EntityTypeBuilder<ThrowRecord> builder)
    {
        builder.ToTable("ThrowRecords");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Ring).HasConversion<string>().IsRequired();
        builder.Property(t => t.Source).HasConversion<string>().IsRequired();
        builder.Property(t => t.RawNotation).IsRequired().HasMaxLength(20);
        builder.Property(t => t.PositionX).IsRequired();
        builder.Property(t => t.PositionY).IsRequired();
        builder.HasIndex(t => new { t.MatchId, t.Sequence });
    }
}
