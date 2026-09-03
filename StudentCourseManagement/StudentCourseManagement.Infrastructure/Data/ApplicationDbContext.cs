using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using StudentCourseManagement.Infrastructure.Entities;
using StudentCourseManagement.Infrastructure.AuthEntities;
namespace StudentCourseManagement.Infrastructure.Data;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Course> Courses { get; set; }

    public virtual DbSet<Student> Students { get; set; }

    public virtual DbSet<UsersDatum> UsersData { get; set; }

    public virtual DbSet<StudentCourse> StudentCourses { get; set; }
    public virtual DbSet<EnrollmentRequest> EnrollmentRequests { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StudentCourse>(entity =>
        {
            base.OnModelCreating(modelBuilder);
            entity.HasKey(sc => new { sc.StudentId, sc.CourseId });

            entity.HasOne(sc => sc.Student)
                .WithMany(s => s.StudentCourses)
                .HasForeignKey(sc => sc.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(sc => sc.Course)
                .WithMany(c => c.StudentCourses)
                .HasForeignKey(sc => sc.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<UsersDatum>(entity =>
        {
            entity.ToTable("UsersData");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .HasDefaultValue("");

            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasDefaultValue("");

            entity.Property(e => e.Username)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Role)
                .HasMaxLength(50)
                .IsRequired();
        });

        modelBuilder.Entity<EnrollmentRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasOne(d => d.Student).WithMany()
                .HasForeignKey(d => d.StudentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Course).WithMany()
                .HasForeignKey(d => d.CourseId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}