using Microsoft.EntityFrameworkCore;
using ConcessionTrackerAPI.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<ConcessionInfo> ConcessionInfo { get; set; } = null!;

    public DbSet<AppLoginDetail> AppLoginDetail { get; set; }

    public DbSet<CTUser> Users { get; set; }





    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<ConcessionInfo>(entity =>
        {
            entity.ToTable("ConcessionInfo");



            entity.HasNoKey();

            entity.Property(e => e.coninfo_vch_conname)
                  .HasColumnName("coninfo_vch_conname");

            entity.Property(e => e.coninfo_vch_dbconnectionstring)
                  .HasColumnName("coninfo_vch_dbconnectionstring");
        });


        modelBuilder.Entity<CTUser>(entity =>
        {
            entity.ToTable("Users");


            entity.HasKey(e => e.usr_int_usrid);

            entity.Property(e => e.usr_int_usrid).HasColumnName("usr_int_usrid");
            entity.Property(e => e.usr_vch_name).HasColumnName("usr_vch_name");
            entity.Property(e => e.usr_vch_emailid).HasColumnName("usr_vch_emailid");
            entity.Property(e => e.usr_vch_pswd).HasColumnName("usr_vch_pswd");
        });

        modelBuilder.Entity<AppLoginDetail>(entity =>
        {
            entity.ToTable("AppLoginDetail");

            entity.Property(e => e.apl_vch_emailid)
                  .HasColumnName("apl_vch_emailid");

            entity.Property(e => e.apl_vch_password)
                  .HasColumnName("apl_vch_password");

            entity.Property(e => e.apl_vch_fcmtoken)
                  .HasColumnName("apl_vch_fcmtoken");

            entity.Property(e => e.apl_bit_loginstatus)
                  .HasColumnName("apl_bit_loginstatus");
        });



    }
}
