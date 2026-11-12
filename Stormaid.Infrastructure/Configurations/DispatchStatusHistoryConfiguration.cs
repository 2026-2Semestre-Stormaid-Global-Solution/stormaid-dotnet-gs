
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Stormaid.Domain.Entities;

namespace Stormaid.Infrastructure.Configurations;

public class DispatchStatusHistoryConfiguration : IEntityTypeConfiguration<DispatchStatusHistory>
{
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public void Configure(EntityTypeBuilder<DispatchStatusHistory> builder)
    {
        builder.ToTable("TB_STORMAID_DISPATCH_HISTORY");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("HISTORY_ID")
            .HasColumnType("NUMBER(19,0)")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.IncidentDispatchId)
            .HasColumnName("DISPATCH_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.PreviousStatus)
            .HasColumnName("PREVIOUS_STATUS")
            .HasColumnType("VARCHAR2(25 CHAR)")
            .HasConversion<string>()
            .HasMaxLength(25)
            .IsRequired(false);

        builder.Property(x => x.NewStatus)
            .HasColumnName("NEW_STATUS")
            .HasColumnType("VARCHAR2(25 CHAR)")
            .HasConversion<string>()
            .HasMaxLength(25)
            .IsRequired();

        builder.Property(x => x.ChangedByUserId)
            .HasColumnName("CHANGED_BY_USER_ID")
            .HasColumnType("NUMBER(19,0)")
            .IsRequired();

        builder.Property(x => x.ChangedByRole)
            .HasColumnName("CHANGED_BY_ROLE")
            .HasColumnType("VARCHAR2(20 CHAR)")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Note)
            .HasColumnName("NOTE")
            .HasColumnType("VARCHAR2(500 CHAR)")
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(x => x.ChangedAt)
            .HasColumnName("CHANGED_AT")
            .HasColumnType("TIMESTAMP(6)")
            .HasConversion(UtcConverter)
            .IsRequired();

        builder.HasOne<IncidentDispatch>()
            .WithMany()
            .HasForeignKey(x => x.IncidentDispatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.IncidentDispatchId);
    }
}
