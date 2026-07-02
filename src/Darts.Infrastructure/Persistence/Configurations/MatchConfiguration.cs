using Darts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Darts.Infrastructure.Persistence.Configurations;

public sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("Matches");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.GameId).IsRequired().HasMaxLength(100);
        builder.Property(m => m.GameConfigJson).IsRequired();
        builder.Property(m => m.Status).HasConversion<string>().IsRequired();
        builder.Property(m => m.InputSource).HasConversion<string>().IsRequired();
        builder.Property(m => m.CreatedAtUtc).IsRequired();

        // Participants is get-only (backed by the private _participants field), so EF must read/write
        // through the field directly rather than the property.
        builder.OwnsMany(m => m.Participants, pb =>
        {
            pb.ToTable("MatchParticipants");
            pb.WithOwner().HasForeignKey("MatchId");
            pb.HasKey("MatchId", "Order");
            pb.Property(p => p.PlayerId).IsRequired();
            pb.Property(p => p.Order).ValueGeneratedNever().IsRequired();
            pb.Property(p => p.FinalPosition);
        });
        builder.Navigation(m => m.Participants).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
