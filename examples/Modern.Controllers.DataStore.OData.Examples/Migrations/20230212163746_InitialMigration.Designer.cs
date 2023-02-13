﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Modern.Controllers.DataStore.OData.Examples.DbContexts;

#nullable disable

namespace Modern.Services.DataStore.Examples.Migrations
{
    [DbContext(typeof(CityDbContext))]
    [Migration("20230212163746_InitialMigration")]
    partial class InitialMigration
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.2");

            modelBuilder.Entity("Modern.Services.DataStore.Cached.Examples.Entities.CityDbo", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasColumnName("id");

                    b.Property<double>("Area")
                        .HasColumnType("REAL")
                        .HasColumnName("area");

                    b.Property<string>("Country")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT")
                        .HasColumnName("country");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT")
                        .HasColumnName("name");

                    b.Property<int>("Population")
                        .HasColumnType("INTEGER")
                        .HasColumnName("population");

                    b.HasKey("Id");

                    b.HasIndex("Name");

                    b.ToTable("cities", (string)null);
                });
#pragma warning restore 612, 618
        }
    }
}
