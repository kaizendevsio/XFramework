﻿// <auto-generated />
using System;
using Fluid;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Fluid.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20241016152530_InitialCreate")]
    partial class InitialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.0");

            modelBuilder.Entity("Fluid.Core.Models.Entity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<Guid>("ConcurrencyStamp")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("ModifiedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<Guid>("TenantId")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Entities");
                });

            modelBuilder.Entity("Fluid.Core.Models.EntityColumn", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<Guid>("ConcurrencyStamp")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("DefaultValue")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Description")
                        .HasColumnType("TEXT");

                    b.Property<Guid?>("EntityId")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("INTEGER");

                    b.Property<string>("MaxLength")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("ModifiedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<Guid>("TenantId")
                        .HasColumnType("TEXT");

                    b.Property<string>("Type")
                        .HasColumnType("TEXT");

                    b.Property<bool?>("Unique")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("EntityId");

                    b.ToTable("EntityColumns");
                });

            modelBuilder.Entity("Fluid.Core.Models.EntityColumn", b =>
                {
                    b.HasOne("Fluid.Core.Models.Entity", null)
                        .WithMany("Columns")
                        .HasForeignKey("EntityId");
                });

            modelBuilder.Entity("Fluid.Core.Models.Entity", b =>
                {
                    b.Navigation("Columns");
                });
#pragma warning restore 612, 618
        }
    }
}
