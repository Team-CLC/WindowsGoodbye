﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;
using WindowsGoodbye;

namespace WindowsGoodbye.Migrations
{
    [DbContext(typeof(DatabaseContext))]
    partial class DatabaseContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.0.2-rtm-10011");

            modelBuilder.Entity("WindowsGoodbye.DeviceAuthRecord", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<Guid?>("DeviceId");

                    b.Property<DateTime>("Time");

                    b.HasKey("Id");

                    b.HasIndex("DeviceId");

                    b.ToTable("AuthRecords");
                });

            modelBuilder.Entity("WindowsGoodbye.DeviceInfo", b =>
                {
                    b.Property<Guid>("DeviceId")
                        .ValueGeneratedOnAdd();

                    b.Property<byte[]>("AuthKey");

                    b.Property<string>("DeviceFriendlyName");

                    b.Property<byte[]>("DeviceKey");

                    b.Property<string>("DeviceMacAddress");

                    b.Property<string>("DeviceModelName");

                    b.Property<string>("LastConnectedHost");

                    b.HasKey("DeviceId");

                    b.ToTable("Devices");
                });

            modelBuilder.Entity("WindowsGoodbye.DeviceAuthRecord", b =>
                {
                    b.HasOne("WindowsGoodbye.DeviceInfo", "Device")
                        .WithMany()
                        .HasForeignKey("DeviceId");
                });
#pragma warning restore 612, 618
        }
    }
}
