using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using StudentCourseManagement.Infrastructure.AuthEntities;

namespace StudentCourseManagement.Infrastructure.AuthData;

public partial class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<UsersDatum> UsersData { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UsersDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UsersDat__3214EC077F56367A");

            entity.HasIndex(e => e.Username, "UQ__UsersDat__536C85E4AD66E523").IsUnique();

            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Role).HasMaxLength(50);
            entity.Property(e => e.Username).HasMaxLength(100);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
