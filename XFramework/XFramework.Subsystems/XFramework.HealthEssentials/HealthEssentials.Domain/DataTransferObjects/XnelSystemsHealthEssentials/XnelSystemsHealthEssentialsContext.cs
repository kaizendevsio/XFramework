using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials
{
    public partial class XnelSystemsHealthEssentialsContext : DbContext
    {
        public XnelSystemsHealthEssentialsContext()
        {
        }

        public XnelSystemsHealthEssentialsContext(DbContextOptions<XnelSystemsHealthEssentialsContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Ailment> Ailments { get; set; } = null!;
        public virtual DbSet<AilmentEntity> AilmentEntities { get; set; } = null!;
        public virtual DbSet<AilmentEntityGroup> AilmentEntityGroups { get; set; } = null!;
        public virtual DbSet<AilmentTag> AilmentTags { get; set; } = null!;
        public virtual DbSet<Availability> Availabilities { get; set; } = null!;
        public virtual DbSet<Consultation> Consultations { get; set; } = null!;
        public virtual DbSet<ConsultationEntity> ConsultationEntities { get; set; } = null!;
        public virtual DbSet<ConsultationEntityGroup> ConsultationEntityGroups { get; set; } = null!;
        public virtual DbSet<ConsultationJobOrder> ConsultationJobOrders { get; set; } = null!;
        public virtual DbSet<ConsultationJobOrderLaboratory> ConsultationJobOrderLaboratories { get; set; } = null!;
        public virtual DbSet<ConsultationJobOrderMedicine> ConsultationJobOrderMedicines { get; set; } = null!;
        public virtual DbSet<ConsultationTag> ConsultationTags { get; set; } = null!;
        public virtual DbSet<Doctor> Doctors { get; set; } = null!;
        public virtual DbSet<DoctorConsultation> DoctorConsultations { get; set; } = null!;
        public virtual DbSet<DoctorConsultationJobOrder> DoctorConsultationJobOrders { get; set; } = null!;
        public virtual DbSet<DoctorEntity> DoctorEntities { get; set; } = null!;
        public virtual DbSet<DoctorEntityGroup> DoctorEntityGroups { get; set; } = null!;
        public virtual DbSet<DoctorTag> DoctorTags { get; set; } = null!;
        public virtual DbSet<Hospital> Hospitals { get; set; } = null!;
        public virtual DbSet<HospitalConsultation> HospitalConsultations { get; set; } = null!;
        public virtual DbSet<HospitalEntity> HospitalEntities { get; set; } = null!;
        public virtual DbSet<HospitalEntityGroup> HospitalEntityGroups { get; set; } = null!;
        public virtual DbSet<HospitalLaboratory> HospitalLaboratories { get; set; } = null!;
        public virtual DbSet<HospitalLocation> HospitalLocations { get; set; } = null!;
        public virtual DbSet<HospitalService> HospitalServices { get; set; } = null!;
        public virtual DbSet<HospitalServiceEntity> HospitalServiceEntities { get; set; } = null!;
        public virtual DbSet<HospitalServiceEntityGroup> HospitalServiceEntityGroups { get; set; } = null!;
        public virtual DbSet<HospitalServiceTag> HospitalServiceTags { get; set; } = null!;
        public virtual DbSet<HospitalTag> HospitalTags { get; set; } = null!;
        public virtual DbSet<Laboratory> Laboratories { get; set; } = null!;
        public virtual DbSet<LaboratoryEntity> LaboratoryEntities { get; set; } = null!;
        public virtual DbSet<LaboratoryEntityGroup> LaboratoryEntityGroups { get; set; } = null!;
        public virtual DbSet<LaboratoryJobOrder> LaboratoryJobOrders { get; set; } = null!;
        public virtual DbSet<LaboratoryJobOrderDetail> LaboratoryJobOrderDetails { get; set; } = null!;
        public virtual DbSet<LaboratoryJobOrderResult> LaboratoryJobOrderResults { get; set; } = null!;
        public virtual DbSet<LaboratoryJobOrderResultFile> LaboratoryJobOrderResultFiles { get; set; } = null!;
        public virtual DbSet<LaboratoryLocation> LaboratoryLocations { get; set; } = null!;
        public virtual DbSet<LaboratoryLocationTag> LaboratoryLocationTags { get; set; } = null!;
        public virtual DbSet<LaboratoryMember> LaboratoryMembers { get; set; } = null!;
        public virtual DbSet<LaboratoryService> LaboratoryServices { get; set; } = null!;
        public virtual DbSet<LaboratoryServiceEntity> LaboratoryServiceEntities { get; set; } = null!;
        public virtual DbSet<LaboratoryServiceEntityGroup> LaboratoryServiceEntityGroups { get; set; } = null!;
        public virtual DbSet<LaboratoryServiceTag> LaboratoryServiceTags { get; set; } = null!;
        public virtual DbSet<LaboratoryTag> LaboratoryTags { get; set; } = null!;
        public virtual DbSet<Logistic> Logistics { get; set; } = null!;
        public virtual DbSet<LogisticEntity> LogisticEntities { get; set; } = null!;
        public virtual DbSet<LogisticJobOrder> LogisticJobOrders { get; set; } = null!;
        public virtual DbSet<LogisticJobOrderDetail> LogisticJobOrderDetails { get; set; } = null!;
        public virtual DbSet<LogisticJobOrderLocation> LogisticJobOrderLocations { get; set; } = null!;
        public virtual DbSet<LogisticRider> LogisticRiders { get; set; } = null!;
        public virtual DbSet<LogisticRiderHandle> LogisticRiderHandles { get; set; } = null!;
        public virtual DbSet<LogisticRiderTag> LogisticRiderTags { get; set; } = null!;
        public virtual DbSet<Medicine> Medicines { get; set; } = null!;
        public virtual DbSet<MedicineEntity> MedicineEntities { get; set; } = null!;
        public virtual DbSet<MedicineEntityGroup> MedicineEntityGroups { get; set; } = null!;
        public virtual DbSet<MedicineIntake> MedicineIntakes { get; set; } = null!;
        public virtual DbSet<MedicineIntakeEntity> MedicineIntakeEntities { get; set; } = null!;
        public virtual DbSet<MedicineTag> MedicineTags { get; set; } = null!;
        public virtual DbSet<MedicineVendor> MedicineVendors { get; set; } = null!;
        public virtual DbSet<MetaDataEntity> MetaDataEntities { get; set; } = null!;
        public virtual DbSet<MetaDataEntityGroup> MetaDataEntityGroups { get; set; } = null!;
        public virtual DbSet<MetaDatum> MetaData { get; set; } = null!;
        public virtual DbSet<Patient> Patients { get; set; } = null!;
        public virtual DbSet<PatientAilment> PatientAilments { get; set; } = null!;
        public virtual DbSet<PatientAilmentDetail> PatientAilmentDetails { get; set; } = null!;
        public virtual DbSet<PatientConsultation> PatientConsultations { get; set; } = null!;
        public virtual DbSet<PatientEntity> PatientEntities { get; set; } = null!;
        public virtual DbSet<PatientEntityGroup> PatientEntityGroups { get; set; } = null!;
        public virtual DbSet<PatientLaboratory> PatientLaboratories { get; set; } = null!;
        public virtual DbSet<PatientReminder> PatientReminders { get; set; } = null!;
        public virtual DbSet<PatientTag> PatientTags { get; set; } = null!;
        public virtual DbSet<Pharmacy> Pharmacies { get; set; } = null!;
        public virtual DbSet<PharmacyEntity> PharmacyEntities { get; set; } = null!;
        public virtual DbSet<PharmacyJobOrder> PharmacyJobOrders { get; set; } = null!;
        public virtual DbSet<PharmacyJobOrderConsultationJobOrder> PharmacyJobOrderConsultationJobOrders { get; set; } = null!;
        public virtual DbSet<PharmacyJobOrderMedicine> PharmacyJobOrderMedicines { get; set; } = null!;
        public virtual DbSet<PharmacyLocation> PharmacyLocations { get; set; } = null!;
        public virtual DbSet<PharmacyMember> PharmacyMembers { get; set; } = null!;
        public virtual DbSet<PharmacyService> PharmacyServices { get; set; } = null!;
        public virtual DbSet<PharmacyServiceEntity> PharmacyServiceEntities { get; set; } = null!;
        public virtual DbSet<PharmacyServiceEntityGroup> PharmacyServiceEntityGroups { get; set; } = null!;
        public virtual DbSet<PharmacyServiceTag> PharmacyServiceTags { get; set; } = null!;
        public virtual DbSet<PharmacyStock> PharmacyStocks { get; set; } = null!;
        public virtual DbSet<PharmacyTag> PharmacyTags { get; set; } = null!;
        public virtual DbSet<Schedule> Schedules { get; set; } = null!;
        public virtual DbSet<ScheduleEntity> ScheduleEntities { get; set; } = null!;
        public virtual DbSet<ScheduleEntityGroup> ScheduleEntityGroups { get; set; } = null!;
        public virtual DbSet<SchedulePriority> SchedulePriorities { get; set; } = null!;
        public virtual DbSet<SchedulePriorityEntity> SchedulePriorityEntities { get; set; } = null!;
        public virtual DbSet<ScheduleTag> ScheduleTags { get; set; } = null!;
        public virtual DbSet<Tag> Tags { get; set; } = null!;
        public virtual DbSet<TagEntity> TagEntities { get; set; } = null!;
        public virtual DbSet<TagEntityGroup> TagEntityGroups { get; set; } = null!;
        public virtual DbSet<Unit> Units { get; set; } = null!;
        public virtual DbSet<UnitEntity> UnitEntities { get; set; } = null!;
        public virtual DbSet<UnitEntityGroup> UnitEntityGroups { get; set; } = null!;
        public virtual DbSet<Vendor> Vendors { get; set; } = null!;
        public virtual DbSet<VendorEntity> VendorEntities { get; set; } = null!;
        public virtual DbSet<VendorEntityGroup> VendorEntityGroups { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
                optionsBuilder.UseNpgsql("Host=localhost;Database=XnelSystemsHealthEssentials;Username=dbAdmin;Password=4*5WD-K8%f*NqmPY");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("uuid-ossp");

            modelBuilder.Entity<Ailment>(entity =>
            {
                entity.ToTable("Ailment", "Ailment");

                entity.HasIndex(e => e.Guid, "ailment_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.EntityId).HasColumnName("EntityID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.OtherName).HasColumnType("character varying");

                entity.Property(e => e.ShortName).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.Ailments)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("ailment_ailmententity_id_fk");
            });

            modelBuilder.Entity<AilmentEntity>(entity =>
            {
                entity.ToTable("AilmentEntity", "Ailment");

                entity.HasIndex(e => e.Guid, "ailmententity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.AilmentEntities)
                    .HasForeignKey(d => d.GroupId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("ailmententity_ailmententitygroup_id_fk");
            });

            modelBuilder.Entity<AilmentEntityGroup>(entity =>
            {
                entity.ToTable("AilmentEntityGroup", "Ailment");

                entity.HasIndex(e => e.Guid, "ailmententitygroup_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<AilmentTag>(entity =>
            {
                entity.ToTable("AilmentTag", "Ailment");

                entity.HasIndex(e => e.Guid, "ailmenttag_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.Ailment)
                    .WithMany(p => p.AilmentTags)
                    .HasForeignKey(d => d.AilmentId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("ailmenttag_ailment_id_fk");

                entity.HasOne(d => d.Tag)
                    .WithMany(p => p.AilmentTags)
                    .HasForeignKey(d => d.TagId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("ailmenttag_tag_id_fk");
            });

            modelBuilder.Entity<Availability>(entity =>
            {
                entity.ToTable("Availability", "Schedule");

                entity.HasIndex(e => e.Guid, "availability_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.EntityId).HasColumnName("EntityID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsAvailable)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.Availabilities)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("schedule_scheduleentity_id_fk");
            });

            modelBuilder.Entity<Consultation>(entity =>
            {
                entity.ToTable("Consultation", "Consultation");

                entity.HasIndex(e => e.Guid, "consultation_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.EntityId).HasColumnName("EntityID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.ShortName).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.Consultations)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("consultation_consultationentity_id_fk");
            });

            modelBuilder.Entity<ConsultationEntity>(entity =>
            {
                entity.ToTable("ConsultationEntity", "Consultation");

                entity.HasIndex(e => e.Guid, "consultationentity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.ConsultationEntities)
                    .HasForeignKey(d => d.GroupId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("consultationentity_consultationentitygroup_id_fk");
            });

            modelBuilder.Entity<ConsultationEntityGroup>(entity =>
            {
                entity.ToTable("ConsultationEntityGroup", "Consultation");

                entity.HasIndex(e => e.Guid, "consultationentitygroup_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<ConsultationJobOrder>(entity =>
            {
                entity.ToTable("ConsultationJobOrder", "Consultation");

                entity.HasIndex(e => e.Guid, "consultationjoborder_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Diagnosis).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.MeetingLink).HasColumnType("character varying");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Prescription).HasColumnType("character varying");

                entity.Property(e => e.ReferenceNumber).HasColumnType("character varying");

                entity.Property(e => e.Remarks).HasColumnType("character varying");

                entity.Property(e => e.Symptoms).HasColumnType("character varying");

                entity.Property(e => e.Treatment).HasColumnType("character varying");

                entity.HasOne(d => d.Consultation)
                    .WithMany(p => p.ConsultationJobOrders)
                    .HasForeignKey(d => d.ConsultationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("consultationjoborder_consultation_id_fk");

                entity.HasOne(d => d.Schedule)
                    .WithMany(p => p.ConsultationJobOrders)
                    .HasForeignKey(d => d.ScheduleId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("consultationjoborder_schedule_id_fk");
            });

            modelBuilder.Entity<ConsultationJobOrderLaboratory>(entity =>
            {
                entity.ToTable("ConsultationJobOrderLaboratory", "Consultation");

                entity.HasIndex(e => e.Guid, "consultationjoborderlaboratory_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.PrescriptionNote).HasColumnType("character varying");

                entity.Property(e => e.Quantity).HasColumnType("character varying");

                entity.Property(e => e.Remarks).HasColumnType("character varying");

                entity.HasOne(d => d.ConsultationJobOrder)
                    .WithMany(p => p.ConsultationJobOrderLaboratories)
                    .HasForeignKey(d => d.ConsultationJobOrderId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("consultationjoborderlaboratory_consultationjoborder_id_fk");

                entity.HasOne(d => d.LaboratoryService)
                    .WithMany(p => p.ConsultationJobOrderLaboratories)
                    .HasForeignKey(d => d.LaboratoryServiceId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("consultationjoborderlaboratory_laboratoryserviceentity_id_fk");

                entity.HasOne(d => d.SuggestedLaboratoryLocation)
                    .WithMany(p => p.ConsultationJobOrderLaboratories)
                    .HasForeignKey(d => d.SuggestedLaboratoryLocationId)
                    .HasConstraintName("consultationjoborderlaboratory_laboratorylocation_id_fk");
            });

            modelBuilder.Entity<ConsultationJobOrderMedicine>(entity =>
            {
                entity.ToTable("ConsultationJobOrderMedicine", "Consultation");

                entity.HasIndex(e => e.Guid, "consultationjoborderdetail_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Duration).HasPrecision(2);

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.PrescriptionNote).HasColumnType("character varying");

                entity.Property(e => e.Quantity).HasColumnType("character varying");

                entity.Property(e => e.Remarks).HasColumnType("character varying");

                entity.HasOne(d => d.ConsultationJobOrder)
                    .WithMany(p => p.ConsultationJobOrderMedicines)
                    .HasForeignKey(d => d.ConsultationJobOrderId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("consultationjobordermedicine_consultationjoborder_id_fk");

                entity.HasOne(d => d.DosageUnit)
                    .WithMany(p => p.ConsultationJobOrderMedicineDosageUnits)
                    .HasForeignKey(d => d.DosageUnitId)
                    .HasConstraintName("consultationjobordermedicine_unit_id_fk_3");

                entity.HasOne(d => d.DurationUnit)
                    .WithMany(p => p.ConsultationJobOrderMedicineDurationUnits)
                    .HasForeignKey(d => d.DurationUnitId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("consultationjobordermedicine_unit_id_fk_2");

                entity.HasOne(d => d.IntakeUnit)
                    .WithMany(p => p.ConsultationJobOrderMedicineIntakeUnits)
                    .HasForeignKey(d => d.IntakeUnitId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("consultationjobordermedicine_unit_id_fk");

                entity.HasOne(d => d.Medicine)
                    .WithMany(p => p.ConsultationJobOrderMedicines)
                    .HasForeignKey(d => d.MedicineId)
                    .HasConstraintName("consultationjobordermedicine_medicine_id_fk");

                entity.HasOne(d => d.MedicineIntake)
                    .WithMany(p => p.ConsultationJobOrderMedicines)
                    .HasForeignKey(d => d.MedicineIntakeId)
                    .HasConstraintName("consultationjobordermedicine_medicineintake_id_fk");
            });

            modelBuilder.Entity<ConsultationTag>(entity =>
            {
                entity.ToTable("ConsultationTag", "Consultation");

                entity.HasIndex(e => e.Guid, "consultationtag_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.Consultation)
                    .WithMany(p => p.ConsultationTags)
                    .HasForeignKey(d => d.ConsultationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("consultationtag_pharmacy_id_fk");

                entity.HasOne(d => d.Tag)
                    .WithMany(p => p.ConsultationTags)
                    .HasForeignKey(d => d.TagId)
                    .HasConstraintName("consultationtag_tag_id_fk");
            });

            modelBuilder.Entity<Doctor>(entity =>
            {
                entity.ToTable("Doctor", "Doctor");

                entity.HasIndex(e => e.Guid, "doctor_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.Clinic).HasColumnType("character varying");

                entity.Property(e => e.ClinicAddress).HasColumnType("character varying");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.CredentialId).HasColumnType("character varying");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.EntityId).HasColumnName("EntityID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.PhilHealthNumber).HasColumnType("character varying");

                entity.Property(e => e.PrcNumber).HasColumnType("character varying");

                entity.Property(e => e.PtrNumber).HasColumnType("character varying");

                entity.Property(e => e.Remarks).HasColumnType("character varying");

                entity.Property(e => e.TinNumber).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.Doctors)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("doctor_doctorentity_id_fk");
            });

            modelBuilder.Entity<DoctorConsultation>(entity =>
            {
                entity.ToTable("DoctorConsultation", "Doctor");

                entity.HasIndex(e => e.Guid, "doctorconsultation_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.MaxDiscount).HasDefaultValueSql("0");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Price).HasDefaultValueSql("0");

                entity.Property(e => e.Quantity).HasDefaultValueSql("1");

                entity.HasOne(d => d.Consultation)
                    .WithMany(p => p.DoctorConsultations)
                    .HasForeignKey(d => d.ConsultationId)
                    .HasConstraintName("doctorconsultation_consultation_id_fk");

                entity.HasOne(d => d.Doctor)
                    .WithMany(p => p.DoctorConsultations)
                    .HasForeignKey(d => d.DoctorId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("doctorconsultation_doctor_id_fk");
            });

            modelBuilder.Entity<DoctorConsultationJobOrder>(entity =>
            {
                entity.ToTable("DoctorConsultationJobOrder", "Doctor");

                entity.HasIndex(e => e.Guid, "consultationjoborder_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.ConsultationJobOrder)
                    .WithMany(p => p.DoctorConsultationJobOrders)
                    .HasForeignKey(d => d.ConsultationJobOrderId)
                    .HasConstraintName("doctorconsultation_consultationjoborder_id_fk");

                entity.HasOne(d => d.Doctor)
                    .WithMany(p => p.DoctorConsultationJobOrders)
                    .HasForeignKey(d => d.DoctorId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("doctorconsultation_doctor_id_fk");
            });

            modelBuilder.Entity<DoctorEntity>(entity =>
            {
                entity.ToTable("DoctorEntity", "Doctor");

                entity.HasIndex(e => e.Guid, "doctorentity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.DoctorEntities)
                    .HasForeignKey(d => d.GroupId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("doctorentity_doctorentitygroup_id_fk");
            });

            modelBuilder.Entity<DoctorEntityGroup>(entity =>
            {
                entity.ToTable("DoctorEntityGroup", "Doctor");

                entity.HasIndex(e => e.Guid, "doctorentitygroup_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<DoctorTag>(entity =>
            {
                entity.ToTable("DoctorTag", "Doctor");

                entity.HasIndex(e => e.Guid, "doctortag_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.Doctor)
                    .WithMany(p => p.DoctorTags)
                    .HasForeignKey(d => d.DoctorId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("doctortag_doctor_id_fk");

                entity.HasOne(d => d.Tag)
                    .WithMany(p => p.DoctorTags)
                    .HasForeignKey(d => d.TagId)
                    .HasConstraintName("doctor_tag_id_fk");
            });

            modelBuilder.Entity<Hospital>(entity =>
            {
                entity.ToTable("Hospital", "Hospital");

                entity.HasIndex(e => e.Guid, "hospital_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Email).HasColumnType("character varying");

                entity.Property(e => e.EntityId).HasColumnName("EntityID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.Logo).HasColumnType("character varying");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.Phone).HasColumnType("character varying");

                entity.Property(e => e.Remarks).HasColumnType("character varying");

                entity.Property(e => e.Website).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.Hospitals)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("hospital_hospitalentity_id_fk");
            });

            modelBuilder.Entity<HospitalConsultation>(entity =>
            {
                entity.ToTable("HospitalConsultation", "Hospital");

                entity.HasIndex(e => e.Guid, "hospitalconsultation_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Consultation)
                    .WithMany(p => p.HospitalConsultations)
                    .HasForeignKey(d => d.ConsultationId)
                    .HasConstraintName("hospitalconsultation_consultation_id_fk");

                entity.HasOne(d => d.Hospital)
                    .WithMany(p => p.HospitalConsultations)
                    .HasForeignKey(d => d.HospitalId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("hospitalconsultation_hospital_id_fk");
            });

            modelBuilder.Entity<HospitalEntity>(entity =>
            {
                entity.ToTable("HospitalEntity", "Hospital");

                entity.HasIndex(e => e.Guid, "hospitalentity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.HospitalEntities)
                    .HasForeignKey(d => d.GroupId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("hospitalentity_hospitalentitygroup_id_fk");
            });

            modelBuilder.Entity<HospitalEntityGroup>(entity =>
            {
                entity.ToTable("HospitalEntityGroup", "Hospital");

                entity.HasIndex(e => e.Guid, "hospitalentitygroup_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<HospitalLaboratory>(entity =>
            {
                entity.ToTable("HospitalLaboratory", "Hospital");

                entity.HasIndex(e => e.Guid, "hospitallaboratory_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Hospital)
                    .WithMany(p => p.HospitalLaboratories)
                    .HasForeignKey(d => d.HospitalId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("hospitalconsultation_hospital_id_fk");

                entity.HasOne(d => d.Laboratory)
                    .WithMany(p => p.HospitalLaboratories)
                    .HasForeignKey(d => d.LaboratoryId)
                    .HasConstraintName("hospitallaboratory_laboratory_id_fk");
            });

            modelBuilder.Entity<HospitalLocation>(entity =>
            {
                entity.ToTable("HospitalLocation", "Hospital");

                entity.HasIndex(e => e.Guid, "hospitallocation_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.Building).HasMaxLength(500);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.Street).HasMaxLength(500);

                entity.Property(e => e.Subdivision).HasMaxLength(500);

                entity.Property(e => e.UnitNumber).HasMaxLength(500);

                entity.HasOne(d => d.Hospital)
                    .WithMany(p => p.HospitalLocations)
                    .HasForeignKey(d => d.HospitalId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("hospitallocation_hospital_id_fk");
            });

            modelBuilder.Entity<HospitalService>(entity =>
            {
                entity.ToTable("HospitalService", "Hospital");

                entity.HasIndex(e => e.Guid, "hospitalservice_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.EntityId).HasColumnName("EntityID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.ShortName).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.HospitalServices)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("hospitalservice_hospitalserviceentity_id_fk");

                entity.HasOne(d => d.Hospital)
                    .WithMany(p => p.HospitalServices)
                    .HasForeignKey(d => d.HospitalId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("hospitalservice_hospital_id_fk");

                entity.HasOne(d => d.HospitalLocation)
                    .WithMany(p => p.HospitalServices)
                    .HasForeignKey(d => d.HospitalLocationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("hospitalservice_hospitallocation_id_fk");

                entity.HasOne(d => d.Unit)
                    .WithMany(p => p.HospitalServices)
                    .HasForeignKey(d => d.UnitId)
                    .HasConstraintName("hospitalservice_unit_id_fk");
            });

            modelBuilder.Entity<HospitalServiceEntity>(entity =>
            {
                entity.ToTable("HospitalServiceEntity", "Hospital");

                entity.HasIndex(e => e.Guid, "hospitalserviceentity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.HospitalServiceEntities)
                    .HasForeignKey(d => d.GroupId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("hospitalserviceentity_hospitalserviceentitygroup_id_fk");
            });

            modelBuilder.Entity<HospitalServiceEntityGroup>(entity =>
            {
                entity.ToTable("HospitalServiceEntityGroup", "Hospital");

                entity.HasIndex(e => e.Guid, "hospitalserviceentitygroup_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<HospitalServiceTag>(entity =>
            {
                entity.ToTable("HospitalServiceTag", "Hospital");

                entity.HasIndex(e => e.Guid, "hospitalservicetag_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.HospitalService)
                    .WithMany(p => p.HospitalServiceTags)
                    .HasForeignKey(d => d.HospitalServiceId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("hospitalservicetag_hospital_id_fk");

                entity.HasOne(d => d.Tag)
                    .WithMany(p => p.HospitalServiceTags)
                    .HasForeignKey(d => d.TagId)
                    .HasConstraintName("hospitaltag_tag_id_fk");
            });

            modelBuilder.Entity<HospitalTag>(entity =>
            {
                entity.ToTable("HospitalTag", "Hospital");

                entity.HasIndex(e => e.Guid, "hospitaltag_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.Hospital)
                    .WithMany(p => p.HospitalTags)
                    .HasForeignKey(d => d.HospitalId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("hospitaltag_hospital_id_fk");

                entity.HasOne(d => d.Tag)
                    .WithMany(p => p.HospitalTags)
                    .HasForeignKey(d => d.TagId)
                    .HasConstraintName("hospital_tag_id_fk");
            });

            modelBuilder.Entity<Laboratory>(entity =>
            {
                entity.ToTable("Laboratory", "Laboratory");

                entity.HasIndex(e => e.Guid, "laboratory_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Email).HasColumnType("character varying");

                entity.Property(e => e.EntityId).HasColumnName("EntityID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.Logo).HasColumnType("character varying");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.Phone).HasColumnType("character varying");

                entity.Property(e => e.ShortName).HasColumnType("character varying");

                entity.Property(e => e.Website).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.Laboratories)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratory_laboratoryentity_id_fk");
            });

            modelBuilder.Entity<LaboratoryEntity>(entity =>
            {
                entity.ToTable("LaboratoryEntity", "Laboratory");

                entity.HasIndex(e => e.Guid, "laboratoryentity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.LaboratoryEntities)
                    .HasForeignKey(d => d.GroupId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratoryentity_laboratoryentitygroup_id_fk");
            });

            modelBuilder.Entity<LaboratoryEntityGroup>(entity =>
            {
                entity.ToTable("LaboratoryEntityGroup", "Laboratory");

                entity.HasIndex(e => e.Guid, "laboratoryentitygroup_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<LaboratoryJobOrder>(entity =>
            {
                entity.ToTable("LaboratoryJobOrder", "Laboratory");

                entity.HasIndex(e => e.Guid, "laboratoryjoborder_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.ReferenceNumber).HasColumnType("character varying");

                entity.Property(e => e.Remarks).HasColumnType("character varying");

                entity.HasOne(d => d.ConsultationJobOrder)
                    .WithMany(p => p.LaboratoryJobOrders)
                    .HasForeignKey(d => d.ConsultationJobOrderId)
                    .HasConstraintName("laboratoryjoborder_consultationjoborder_id_fk");

                entity.HasOne(d => d.Laboratory)
                    .WithMany(p => p.LaboratoryJobOrders)
                    .HasForeignKey(d => d.LaboratoryId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratoryjoborder_laboratory_id_fk");

                entity.HasOne(d => d.LaboratoryLocation)
                    .WithMany(p => p.LaboratoryJobOrders)
                    .HasForeignKey(d => d.LaboratoryLocationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratoryjoborder_laboratorylocation_id_fk");

                entity.HasOne(d => d.Patient)
                    .WithMany(p => p.LaboratoryJobOrders)
                    .HasForeignKey(d => d.PatientId)
                    .HasConstraintName("laboratoryjoborder_patient_id_fk");

                entity.HasOne(d => d.Schedule)
                    .WithMany(p => p.LaboratoryJobOrders)
                    .HasForeignKey(d => d.ScheduleId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratoryjoborder_schedule_id_fk");
            });

            modelBuilder.Entity<LaboratoryJobOrderDetail>(entity =>
            {
                entity.ToTable("LaboratoryJobOrderDetail", "Laboratory");

                entity.HasIndex(e => e.Guid, "laboratoryjoborderdetail_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Quantity).HasColumnType("character varying");

                entity.Property(e => e.Remarks).HasColumnType("character varying");

                entity.HasOne(d => d.LaboratoryJobOrder)
                    .WithMany(p => p.LaboratoryJobOrderDetails)
                    .HasForeignKey(d => d.LaboratoryJobOrderId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratoryjoborderdetail_laboratoryjoborder_id_fk");

                entity.HasOne(d => d.LaboratoryService)
                    .WithMany(p => p.LaboratoryJobOrderDetails)
                    .HasForeignKey(d => d.LaboratoryServiceId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratoryjoborder_laboratoryservice_id_fk");
            });

            modelBuilder.Entity<LaboratoryJobOrderResult>(entity =>
            {
                entity.ToTable("LaboratoryJobOrderResult", "Laboratory");

                entity.HasIndex(e => e.Guid, "laboratoryjoborderresult_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.Remarks).HasColumnType("character varying");

                entity.HasOne(d => d.LaboratoryJobOrder)
                    .WithMany(p => p.LaboratoryJobOrderResults)
                    .HasForeignKey(d => d.LaboratoryJobOrderId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratoryjoborderresult_laboratoryjoborder_id_fk");
            });

            modelBuilder.Entity<LaboratoryJobOrderResultFile>(entity =>
            {
                entity.ToTable("LaboratoryJobOrderResultFiles", "Laboratory");

                entity.HasIndex(e => e.Guid, "laboratoryjoborderresultfiles_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.LaboratoryJobOrderResult)
                    .WithMany(p => p.LaboratoryJobOrderResultFiles)
                    .HasForeignKey(d => d.LaboratoryJobOrderResultId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratoryjoborderresultfiles_laboratoryjoborderresult_id_fk");
            });

            modelBuilder.Entity<LaboratoryLocation>(entity =>
            {
                entity.ToTable("LaboratoryLocation", "Laboratory");

                entity.HasIndex(e => e.Guid, "laboratorylocation_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.AlternativePhone).HasColumnType("character varying");

                entity.Property(e => e.Building).HasMaxLength(500);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Email).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.Phone).HasColumnType("character varying");

                entity.Property(e => e.Status).HasDefaultValueSql("0");

                entity.Property(e => e.Street).HasMaxLength(500);

                entity.Property(e => e.Subdivision).HasMaxLength(500);

                entity.Property(e => e.UnitNumber).HasMaxLength(500);

                entity.Property(e => e.Website).HasColumnType("character varying");

                entity.HasOne(d => d.Laboratory)
                    .WithMany(p => p.LaboratoryLocations)
                    .HasForeignKey(d => d.LaboratoryId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratorylocation_laboratory_id_fk");
            });

            modelBuilder.Entity<LaboratoryLocationTag>(entity =>
            {
                entity.ToTable("LaboratoryLocationTag", "Laboratory");

                entity.HasIndex(e => e.Guid, "laboratorylocationtag_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.LaboratoryLocation)
                    .WithMany(p => p.LaboratoryLocationTags)
                    .HasForeignKey(d => d.LaboratoryLocationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratorylocationtag_laboratorylocation_id_fk");

                entity.HasOne(d => d.Tag)
                    .WithMany(p => p.LaboratoryLocationTags)
                    .HasForeignKey(d => d.TagId)
                    .HasConstraintName("laboratorylocationtag_tag_id_fk");
            });

            modelBuilder.Entity<LaboratoryMember>(entity =>
            {
                entity.ToTable("LaboratoryMember", "Laboratory");

                entity.HasIndex(e => e.Guid, "laboratorymember_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.CredentialId).HasColumnType("character varying");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.Laboratory)
                    .WithMany(p => p.LaboratoryMembers)
                    .HasForeignKey(d => d.LaboratoryId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratorymember_laboratory_id_fk");

                entity.HasOne(d => d.LaboratoryLocation)
                    .WithMany(p => p.LaboratoryMembers)
                    .HasForeignKey(d => d.LaboratoryLocationId)
                    .HasConstraintName("laboratorymember_laboratorylocation_id_fk");
            });

            modelBuilder.Entity<LaboratoryService>(entity =>
            {
                entity.ToTable("LaboratoryService", "Laboratory");

                entity.HasIndex(e => e.Guid, "laboratoryservice_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.EntityId).HasColumnName("EntityID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.ShortName).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.LaboratoryServices)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratoryservice_laboratoryserviceentity_id_fk");

                entity.HasOne(d => d.Laboratory)
                    .WithMany(p => p.LaboratoryServices)
                    .HasForeignKey(d => d.LaboratoryId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratoryservice_laboratory_id_fk");

                entity.HasOne(d => d.LaboratoryLocation)
                    .WithMany(p => p.LaboratoryServices)
                    .HasForeignKey(d => d.LaboratoryLocationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratoryservice_laboratorylocation_id_fk");

                entity.HasOne(d => d.Unit)
                    .WithMany(p => p.LaboratoryServices)
                    .HasForeignKey(d => d.UnitId)
                    .HasConstraintName("laboratoryservice_unit_id_fk");
            });

            modelBuilder.Entity<LaboratoryServiceEntity>(entity =>
            {
                entity.ToTable("LaboratoryServiceEntity", "Laboratory");

                entity.HasIndex(e => e.Guid, "laboratoryserviceentity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.LaboratoryServiceEntities)
                    .HasForeignKey(d => d.GroupId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratoryserviceentity_laboratoryserviceentitygroup_id_fk");
            });

            modelBuilder.Entity<LaboratoryServiceEntityGroup>(entity =>
            {
                entity.ToTable("LaboratoryServiceEntityGroup", "Laboratory");

                entity.HasIndex(e => e.Guid, "laboratoryserviceentitygroup_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<LaboratoryServiceTag>(entity =>
            {
                entity.ToTable("LaboratoryServiceTag", "Laboratory");

                entity.HasIndex(e => e.Guid, "laboratoryservicetag_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.LaboratoryService)
                    .WithMany(p => p.LaboratoryServiceTags)
                    .HasForeignKey(d => d.LaboratoryServiceId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratoryservicetag_pharmacy_id_fk");

                entity.HasOne(d => d.Tag)
                    .WithMany(p => p.LaboratoryServiceTags)
                    .HasForeignKey(d => d.TagId)
                    .HasConstraintName("pharmacytag_tag_id_fk");
            });

            modelBuilder.Entity<LaboratoryTag>(entity =>
            {
                entity.ToTable("LaboratoryTag", "Laboratory");

                entity.HasIndex(e => e.Guid, "laboratorytag_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.Laboratory)
                    .WithMany(p => p.LaboratoryTags)
                    .HasForeignKey(d => d.LaboratoryId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("laboratorytag_pharmacy_id_fk");

                entity.HasOne(d => d.Tag)
                    .WithMany(p => p.LaboratoryTags)
                    .HasForeignKey(d => d.TagId)
                    .HasConstraintName("pharmacytag_tag_id_fk");
            });

            modelBuilder.Entity<Logistic>(entity =>
            {
                entity.ToTable("Logistic", "Logistic");

                entity.HasIndex(e => e.Guid, "logistic_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Email).HasColumnType("character varying");

                entity.Property(e => e.EntityId).HasColumnName("EntityID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.Logo).HasColumnType("character varying");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.Phone).HasColumnType("character varying");

                entity.Property(e => e.Remarks).HasColumnType("character varying");

                entity.Property(e => e.Website).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.Logistics)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("logistic_logisticentity_id_fk");
            });

            modelBuilder.Entity<LogisticEntity>(entity =>
            {
                entity.ToTable("LogisticEntity", "Logistic");

                entity.HasIndex(e => e.Guid, "logisticentity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<LogisticJobOrder>(entity =>
            {
                entity.ToTable("LogisticJobOrder", "Logistic");

                entity.HasIndex(e => e.Guid, "logisticjoborder_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Rider)
                    .WithMany(p => p.LogisticJobOrders)
                    .HasForeignKey(d => d.RiderId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("logisticjoborder_logisticrider_id_fk");

                entity.HasOne(d => d.Schedule)
                    .WithMany(p => p.LogisticJobOrders)
                    .HasForeignKey(d => d.ScheduleId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("logisticjoborder_schedule_id_fk");
            });

            modelBuilder.Entity<LogisticJobOrderDetail>(entity =>
            {
                entity.ToTable("LogisticJobOrderDetail", "Logistic");

                entity.HasIndex(e => e.Guid, "logisticjoborderdetail_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.LocationGuid).HasColumnType("character varying");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.LogisticJobOrder)
                    .WithMany(p => p.LogisticJobOrderDetails)
                    .HasForeignKey(d => d.LogisticJobOrderId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("logisticjoborderdetail_logisticjoborder_id_fk");

                entity.HasOne(d => d.Unit)
                    .WithMany(p => p.LogisticJobOrderDetails)
                    .HasForeignKey(d => d.UnitId)
                    .HasConstraintName("logisticjoborderdetail_unit_id_fk");
            });

            modelBuilder.Entity<LogisticJobOrderLocation>(entity =>
            {
                entity.ToTable("LogisticJobOrderLocation", "Logistic");

                entity.HasIndex(e => e.Guid, "logisticjoborderlocation_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.Building).HasMaxLength(500);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.Street).HasMaxLength(500);

                entity.Property(e => e.Subdivision).HasMaxLength(500);

                entity.Property(e => e.UnitNumber).HasMaxLength(500);

                entity.HasOne(d => d.LogisticJobOrder)
                    .WithMany(p => p.LogisticJobOrderLocations)
                    .HasForeignKey(d => d.LogisticJobOrderId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("logisticjoborderlocation_logisticjoborder_id_fk");
            });

            modelBuilder.Entity<LogisticRider>(entity =>
            {
                entity.ToTable("LogisticRider", "Logistic");

                entity.HasIndex(e => e.Guid, "logisticrider_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.Remarks).HasColumnType("character varying");
            });

            modelBuilder.Entity<LogisticRiderHandle>(entity =>
            {
                entity.ToTable("LogisticRiderHandle", "Logistic");

                entity.HasIndex(e => e.Guid, "logisticriderhandle_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.LogisticId).HasColumnName("LogisticID");

                entity.Property(e => e.LogisticRiderId).HasColumnName("LogisticRiderID");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Logistic)
                    .WithMany(p => p.LogisticRiderHandles)
                    .HasForeignKey(d => d.LogisticId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("logisticriderhandle_logistic_id_fk");

                entity.HasOne(d => d.LogisticRider)
                    .WithMany(p => p.LogisticRiderHandles)
                    .HasForeignKey(d => d.LogisticRiderId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("logisticriderhandle_logisticrider_id_fk");
            });

            modelBuilder.Entity<LogisticRiderTag>(entity =>
            {
                entity.ToTable("LogisticRiderTag", "Logistic");

                entity.HasIndex(e => e.Guid, "logisticridertag_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.LogisticRider)
                    .WithMany(p => p.LogisticRiderTags)
                    .HasForeignKey(d => d.LogisticRiderId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("logistictag_logisticrider_id_fk");

                entity.HasOne(d => d.Tag)
                    .WithMany(p => p.LogisticRiderTags)
                    .HasForeignKey(d => d.TagId)
                    .HasConstraintName("logisticrider_tag_id_fk");
            });

            modelBuilder.Entity<Medicine>(entity =>
            {
                entity.ToTable("Medicine", "Medicine");

                entity.HasIndex(e => e.Guid, "medicine_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.ChemicalComponent).HasColumnType("character varying");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.ScientificName).HasColumnType("character varying");

                entity.Property(e => e.ShortName).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.Medicines)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("medicine_medicineentity_id_fk");
            });

            modelBuilder.Entity<MedicineEntity>(entity =>
            {
                entity.ToTable("MedicineEntity", "Medicine");

                entity.HasIndex(e => e.Guid, "medicineentity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.MedicineEntities)
                    .HasForeignKey(d => d.GroupId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("medicineentity_medicineentitygroup_id_fk");
            });

            modelBuilder.Entity<MedicineEntityGroup>(entity =>
            {
                entity.ToTable("MedicineEntityGroup", "Medicine");

                entity.HasIndex(e => e.Guid, "medicineentitygroup_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<MedicineIntake>(entity =>
            {
                entity.ToTable("MedicineIntake", "Medicine");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.ScientificName).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.MedicineIntakes)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("medicineintake_medicineintakeentity_id_fk");

                entity.HasOne(d => d.Unit)
                    .WithMany(p => p.MedicineIntakes)
                    .HasForeignKey(d => d.UnitId)
                    .HasConstraintName("medicineintake_unit_id_fk");
            });

            modelBuilder.Entity<MedicineIntakeEntity>(entity =>
            {
                entity.ToTable("MedicineIntakeEntity", "Medicine");

                entity.HasIndex(e => e.Guid, "medicineintakeentity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<MedicineTag>(entity =>
            {
                entity.ToTable("MedicineTag", "Medicine");

                entity.HasIndex(e => e.Guid, "medicinetag_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.Medicine)
                    .WithMany(p => p.MedicineTags)
                    .HasForeignKey(d => d.MedicineId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("medicinetag_medicine_id_fk");

                entity.HasOne(d => d.Tag)
                    .WithMany(p => p.MedicineTags)
                    .HasForeignKey(d => d.TagId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("medicinetag_tag_id_fk");
            });

            modelBuilder.Entity<MedicineVendor>(entity =>
            {
                entity.ToTable("MedicineVendor", "Medicine");

                entity.HasIndex(e => e.Guid, "medicinevendor_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Medicine)
                    .WithMany(p => p.MedicineVendors)
                    .HasForeignKey(d => d.MedicineId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("medicinetag_medicine_id_fk");

                entity.HasOne(d => d.Vendor)
                    .WithMany(p => p.MedicineVendors)
                    .HasForeignKey(d => d.VendorId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("medicinevendor_vendor_id_fk");
            });

            modelBuilder.Entity<MetaDataEntity>(entity =>
            {
                entity.ToTable("MetaDataEntity", "MetaData");

                entity.HasIndex(e => e.Guid, "metadataentity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.MetaDataEntities)
                    .HasForeignKey(d => d.GroupId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("metadataentity_metadataentitygroup_id_fk");
            });

            modelBuilder.Entity<MetaDataEntityGroup>(entity =>
            {
                entity.ToTable("MetaDataEntityGroup", "MetaData");

                entity.HasIndex(e => e.Guid, "metadataentitygroup_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<MetaDatum>(entity =>
            {
                entity.ToTable("MetaData", "MetaData");

                entity.HasIndex(e => e.Guid, "metadata_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.MetaData)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("metadata_metadataentity_id_fk");
            });

            modelBuilder.Entity<Patient>(entity =>
            {
                entity.ToTable("Patient", "Patient");

                entity.HasIndex(e => e.Guid, "patient_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.CredentialId).HasColumnType("character varying");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.EntityId).HasColumnName("EntityID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Remarks).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.Patients)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("patient_patiententity_id_fk");
            });

            modelBuilder.Entity<PatientAilment>(entity =>
            {
                entity.ToTable("PatientAilment", "Patient");

                entity.HasIndex(e => e.Guid, "patientailment_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Remarks).HasColumnType("character varying");

                entity.HasOne(d => d.Ailment)
                    .WithMany(p => p.PatientAilments)
                    .HasForeignKey(d => d.AilmentId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("patientailment_ailment_id_fk");

                entity.HasOne(d => d.Patient)
                    .WithMany(p => p.PatientAilments)
                    .HasForeignKey(d => d.PatientId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("patientailment_patient_id_fk");
            });

            modelBuilder.Entity<PatientAilmentDetail>(entity =>
            {
                entity.ToTable("PatientAilmentDetail", "Patient");

                entity.HasIndex(e => e.Guid, "patientailmentdetail_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.DoctorName).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Remarks).HasColumnType("character varying");

                entity.HasOne(d => d.ConsultationJobOrder)
                    .WithMany(p => p.PatientAilmentDetails)
                    .HasForeignKey(d => d.ConsultationJobOrderId)
                    .HasConstraintName("patientailment_ailment_id_fk");

                entity.HasOne(d => d.PatientAilment)
                    .WithMany(p => p.PatientAilmentDetails)
                    .HasForeignKey(d => d.PatientAilmentId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("patientailmentdetail_patientailment_id_fk");
            });

            modelBuilder.Entity<PatientConsultation>(entity =>
            {
                entity.ToTable("PatientConsultation", "Patient");

                entity.HasIndex(e => e.Guid, "patientconsultation_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.ConsultationJobOrder)
                    .WithMany(p => p.PatientConsultations)
                    .HasForeignKey(d => d.ConsultationJobOrderId)
                    .HasConstraintName("patientconsultation_ailment_id_fk");

                entity.HasOne(d => d.Patient)
                    .WithMany(p => p.PatientConsultations)
                    .HasForeignKey(d => d.PatientId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("patientconsultation_patient_id_fk");
            });

            modelBuilder.Entity<PatientEntity>(entity =>
            {
                entity.ToTable("PatientEntity", "Patient");

                entity.HasIndex(e => e.Guid, "patiententity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.PatientEntities)
                    .HasForeignKey(d => d.GroupId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("patiententity_patiententitygroup_id_fk");
            });

            modelBuilder.Entity<PatientEntityGroup>(entity =>
            {
                entity.ToTable("PatientEntityGroup", "Patient");

                entity.HasIndex(e => e.Guid, "patiententitygroup_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<PatientLaboratory>(entity =>
            {
                entity.ToTable("PatientLaboratory", "Patient");

                entity.HasIndex(e => e.Guid, "patientlaboratory_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.LaboratoryJobOrder)
                    .WithMany(p => p.PatientLaboratories)
                    .HasForeignKey(d => d.LaboratoryJobOrderId)
                    .HasConstraintName("laboratoryjoborder_ailment_id_fk");

                entity.HasOne(d => d.Patient)
                    .WithMany(p => p.PatientLaboratories)
                    .HasForeignKey(d => d.PatientId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("patientconsultation_patient_id_fk");
            });

            modelBuilder.Entity<PatientReminder>(entity =>
            {
                entity.ToTable("PatientReminder", "Patient");

                entity.HasIndex(e => e.Guid, "patientreminder_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.ConsultationJobOrder)
                    .WithMany(p => p.PatientReminders)
                    .HasForeignKey(d => d.ConsultationJobOrderId)
                    .HasConstraintName("patientreminder_ailment_id_fk");

                entity.HasOne(d => d.Patient)
                    .WithMany(p => p.PatientReminders)
                    .HasForeignKey(d => d.PatientId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("patientreminder_patient_id_fk");
            });

            modelBuilder.Entity<PatientTag>(entity =>
            {
                entity.ToTable("PatientTag", "Patient");

                entity.HasIndex(e => e.Guid, "patienttag_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.Patient)
                    .WithMany(p => p.PatientTags)
                    .HasForeignKey(d => d.PatientId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("patienttag_patient_id_fk");

                entity.HasOne(d => d.Tag)
                    .WithMany(p => p.PatientTags)
                    .HasForeignKey(d => d.TagId)
                    .HasConstraintName("patient_tag_id_fk");
            });

            modelBuilder.Entity<Pharmacy>(entity =>
            {
                entity.ToTable("Pharmacy", "Pharmacy");

                entity.HasIndex(e => e.Guid, "pharmacy_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.ChemicalComponent).HasColumnType("character varying");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Email).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.Logo).HasColumnType("character varying");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.Phone).HasColumnType("character varying");

                entity.Property(e => e.ShortName).HasColumnType("character varying");

                entity.Property(e => e.Slogan).HasColumnType("character varying");

                entity.Property(e => e.Website).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.Pharmacies)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("pharmacy_pharmacyentity_id_fk");
            });

            modelBuilder.Entity<PharmacyEntity>(entity =>
            {
                entity.ToTable("PharmacyEntity", "Pharmacy");

                entity.HasIndex(e => e.Guid, "pharmacyentity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<PharmacyJobOrder>(entity =>
            {
                entity.ToTable("PharmacyJobOrder", "Pharmacy");

                entity.HasIndex(e => e.Guid, "pharmacyjoborder_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.PrescriptionNote).HasColumnType("character varying");

                entity.Property(e => e.ReferenceNumber).HasColumnType("character varying");

                entity.Property(e => e.Remarks).HasColumnType("character varying");

                entity.HasOne(d => d.Patient)
                    .WithMany(p => p.PharmacyJobOrders)
                    .HasForeignKey(d => d.PatientId)
                    .HasConstraintName("pharmacyjoborder_patient_id_fk");

                entity.HasOne(d => d.PharmacyLocation)
                    .WithMany(p => p.PharmacyJobOrders)
                    .HasForeignKey(d => d.PharmacyLocationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("pharmacyjoborder_pharmacylocation_id_fk");

                entity.HasOne(d => d.Schedule)
                    .WithMany(p => p.PharmacyJobOrders)
                    .HasForeignKey(d => d.ScheduleId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("pharmacyjoborder_schedule_id_fk");
            });

            modelBuilder.Entity<PharmacyJobOrderConsultationJobOrder>(entity =>
            {
                entity.ToTable("PharmacyJobOrderConsultationJobOrder", "Pharmacy");

                entity.HasIndex(e => e.Guid, "pharmacyjoborderconsultationjoborder_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Remarks).HasColumnType("character varying");

                entity.HasOne(d => d.ConsultationJobOrder)
                    .WithMany(p => p.PharmacyJobOrderConsultationJobOrders)
                    .HasForeignKey(d => d.ConsultationJobOrderId)
                    .HasConstraintName("pharmacyjoborder_consultationjoborder_id_fk");

                entity.HasOne(d => d.PharmacyJobOrder)
                    .WithMany(p => p.PharmacyJobOrderConsultationJobOrders)
                    .HasForeignKey(d => d.PharmacyJobOrderId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("pharmacyjobordermedicine_pharmacyjoborder_id_fk");
            });

            modelBuilder.Entity<PharmacyJobOrderMedicine>(entity =>
            {
                entity.ToTable("PharmacyJobOrderMedicine", "Pharmacy");

                entity.HasIndex(e => e.Guid, "pharmacyjoborderdetail_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.PrescriptionNote).HasColumnType("character varying");

                entity.Property(e => e.Quantity).HasColumnType("character varying");

                entity.Property(e => e.Remarks).HasColumnType("character varying");

                entity.HasOne(d => d.Medicine)
                    .WithMany(p => p.PharmacyJobOrderMedicines)
                    .HasForeignKey(d => d.MedicineId)
                    .HasConstraintName("pharmacyjobordermedicine_medicine_id_fk");

                entity.HasOne(d => d.MedicineIntake)
                    .WithMany(p => p.PharmacyJobOrderMedicines)
                    .HasForeignKey(d => d.MedicineIntakeId)
                    .HasConstraintName("pharmacyjobordermedicine_medicineintake_id_fk");

                entity.HasOne(d => d.PharmacyJobOrder)
                    .WithMany(p => p.PharmacyJobOrderMedicines)
                    .HasForeignKey(d => d.PharmacyJobOrderId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("pharmacyjobordermedicine_pharmacyjoborder_id_fk");
            });

            modelBuilder.Entity<PharmacyLocation>(entity =>
            {
                entity.ToTable("PharmacyLocation", "Pharmacy");

                entity.HasIndex(e => e.Guid, "pharmacylocation_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.AlternativePhone).HasColumnType("character varying");

                entity.Property(e => e.Building).HasMaxLength(500);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Email).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.Phone).HasColumnType("character varying");

                entity.Property(e => e.Status).HasDefaultValueSql("0");

                entity.Property(e => e.Street).HasMaxLength(500);

                entity.Property(e => e.Subdivision).HasMaxLength(500);

                entity.Property(e => e.UnitNumber).HasMaxLength(500);

                entity.Property(e => e.Website).HasColumnType("character varying");

                entity.HasOne(d => d.Pharmacy)
                    .WithMany(p => p.PharmacyLocations)
                    .HasForeignKey(d => d.PharmacyId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("pharmacylocation_pharmacy_id_fk");
            });

            modelBuilder.Entity<PharmacyMember>(entity =>
            {
                entity.ToTable("PharmacyMember", "Pharmacy");

                entity.HasIndex(e => e.Guid, "pharmacymember_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.CredentialId).HasColumnType("character varying");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.Pharmacy)
                    .WithMany(p => p.PharmacyMembers)
                    .HasForeignKey(d => d.PharmacyId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("pharmacymember_pharmacy_id_fk");

                entity.HasOne(d => d.PharmacyLocation)
                    .WithMany(p => p.PharmacyMembers)
                    .HasForeignKey(d => d.PharmacyLocationId)
                    .HasConstraintName("pharmacymember_pharmacylocation_id_fk");
            });

            modelBuilder.Entity<PharmacyService>(entity =>
            {
                entity.ToTable("PharmacyService", "Pharmacy");

                entity.HasIndex(e => e.Guid, "pharmacyservice_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.EntityId).HasColumnName("EntityID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.ShortName).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.PharmacyServices)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("pharmacyservice_pharmacyserviceentity_id_fk");

                entity.HasOne(d => d.Pharmacy)
                    .WithMany(p => p.PharmacyServices)
                    .HasForeignKey(d => d.PharmacyId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("pharmacyservice_pharmacy_id_fk");

                entity.HasOne(d => d.PharmacyLocation)
                    .WithMany(p => p.PharmacyServices)
                    .HasForeignKey(d => d.PharmacyLocationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("pharmacyservice_pharmacylocation_id_fk");

                entity.HasOne(d => d.Unit)
                    .WithMany(p => p.PharmacyServices)
                    .HasForeignKey(d => d.UnitId)
                    .HasConstraintName("pharmacyservice_unit_id_fk");
            });

            modelBuilder.Entity<PharmacyServiceEntity>(entity =>
            {
                entity.ToTable("PharmacyServiceEntity", "Pharmacy");

                entity.HasIndex(e => e.Guid, "pharmacyserviceentity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.PharmacyServiceEntities)
                    .HasForeignKey(d => d.GroupId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("pharmacyserviceentity_pharmacyserviceentitygroup_id_fk");
            });

            modelBuilder.Entity<PharmacyServiceEntityGroup>(entity =>
            {
                entity.ToTable("PharmacyServiceEntityGroup", "Pharmacy");

                entity.HasIndex(e => e.Guid, "pharmacyserviceentitygroup_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<PharmacyServiceTag>(entity =>
            {
                entity.ToTable("PharmacyServiceTag", "Pharmacy");

                entity.HasIndex(e => e.Guid, "pharmacyservicetag_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.PharmacyService)
                    .WithMany(p => p.PharmacyServiceTags)
                    .HasForeignKey(d => d.PharmacyServiceId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("pharmacyservicetag_pharmacy_id_fk");

                entity.HasOne(d => d.Tag)
                    .WithMany(p => p.PharmacyServiceTags)
                    .HasForeignKey(d => d.TagId)
                    .HasConstraintName("pharmacytag_tag_id_fk");
            });

            modelBuilder.Entity<PharmacyStock>(entity =>
            {
                entity.ToTable("PharmacyStocks", "Pharmacy");

                entity.HasIndex(e => e.Guid, "pharmacystocks_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.LastRestock).HasDefaultValueSql("now()");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Medicine)
                    .WithMany(p => p.PharmacyStocks)
                    .HasForeignKey(d => d.MedicineId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("pharmacystocks_medicine_id_fk");

                entity.HasOne(d => d.Pharmacy)
                    .WithMany(p => p.PharmacyStocks)
                    .HasForeignKey(d => d.PharmacyId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("pharmacystocks_pharmacy_id_fk");

                entity.HasOne(d => d.UnitNavigation)
                    .WithMany(p => p.PharmacyStocks)
                    .HasForeignKey(d => d.Unit)
                    .HasConstraintName("pharmacystocks_unit_id_fk");
            });

            modelBuilder.Entity<PharmacyTag>(entity =>
            {
                entity.ToTable("PharmacyTag", "Pharmacy");

                entity.HasIndex(e => e.Guid, "pharmacytag_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.Pharmacy)
                    .WithMany(p => p.PharmacyTags)
                    .HasForeignKey(d => d.PharmacyId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("pharmacytag_pharmacy_id_fk");

                entity.HasOne(d => d.Tag)
                    .WithMany(p => p.PharmacyTags)
                    .HasForeignKey(d => d.TagId)
                    .HasConstraintName("pharmacytag_tag_id_fk");
            });

            modelBuilder.Entity<Schedule>(entity =>
            {
                entity.ToTable("Schedule", "Schedule");

                entity.HasIndex(e => e.Guid, "schedule_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.EntityId).HasColumnName("EntityID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.PriorityId).HasColumnName("PriorityID");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.Schedules)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("schedule_scheduleentity_id_fk");

                entity.HasOne(d => d.Priority)
                    .WithMany(p => p.Schedules)
                    .HasForeignKey(d => d.PriorityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("schedule_schedulepriority_id_fk");
            });

            modelBuilder.Entity<ScheduleEntity>(entity =>
            {
                entity.ToTable("ScheduleEntity", "Schedule");

                entity.HasIndex(e => e.Guid, "scheduleentity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.ScheduleEntities)
                    .HasForeignKey(d => d.GroupId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("scheduleentity_scheduleentitygroup_id_fk");
            });

            modelBuilder.Entity<ScheduleEntityGroup>(entity =>
            {
                entity.ToTable("ScheduleEntityGroup", "Schedule");

                entity.HasIndex(e => e.Guid, "scheduleentitygroup_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<SchedulePriority>(entity =>
            {
                entity.ToTable("SchedulePriority", "Schedule");

                entity.HasIndex(e => e.Guid, "schedulepriority_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.EntityId).HasColumnName("EntityID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.SchedulePriorities)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("schedulepriority_schedulepriorityentity_id_fk");
            });

            modelBuilder.Entity<SchedulePriorityEntity>(entity =>
            {
                entity.ToTable("SchedulePriorityEntity", "Schedule");

                entity.HasIndex(e => e.Guid, "schedulepriorityentity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<ScheduleTag>(entity =>
            {
                entity.ToTable("ScheduleTag", "Schedule");

                entity.HasIndex(e => e.Guid, "scheduletag_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Value).HasColumnType("character varying");

                entity.HasOne(d => d.Schedule)
                    .WithMany(p => p.ScheduleTags)
                    .HasForeignKey(d => d.ScheduleId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("scheduletag_schedule_id_fk");

                entity.HasOne(d => d.Tag)
                    .WithMany(p => p.ScheduleTags)
                    .HasForeignKey(d => d.TagId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("scheduletag_tag_id_fk");
            });

            modelBuilder.Entity<Tag>(entity =>
            {
                entity.ToTable("Tag", "Tag");

                entity.HasIndex(e => e.Guid, "tag_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.EntityId).HasColumnName("EntityID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.ShortName).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.Tags)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tag_tagentity_id_fk");
            });

            modelBuilder.Entity<TagEntity>(entity =>
            {
                entity.ToTable("TagEntity", "Tag");

                entity.HasIndex(e => e.Guid, "tagentity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.TagEntities)
                    .HasForeignKey(d => d.GroupId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("tagentity_tagentitygroup_id_fk");
            });

            modelBuilder.Entity<TagEntityGroup>(entity =>
            {
                entity.ToTable("TagEntityGroup", "Tag");

                entity.HasIndex(e => e.Guid, "tagentitygroup_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<Unit>(entity =>
            {
                entity.ToTable("Unit", "Unit");

                entity.HasIndex(e => e.Guid, "unit_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.EntityId).HasColumnName("EntityID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.Property(e => e.ShortName).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.Units)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("unit_unitentity_id_fk");
            });

            modelBuilder.Entity<UnitEntity>(entity =>
            {
                entity.ToTable("UnitEntity", "Unit");

                entity.HasIndex(e => e.Guid, "unitentity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.UnitEntities)
                    .HasForeignKey(d => d.GroupId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("unitentity_unitentitygroup_id_fk");
            });

            modelBuilder.Entity<UnitEntityGroup>(entity =>
            {
                entity.ToTable("UnitEntityGroup", "Unit");

                entity.HasIndex(e => e.Guid, "unitentitygroup_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            modelBuilder.Entity<Vendor>(entity =>
            {
                entity.ToTable("Vendor", "Services");

                entity.HasIndex(e => e.Guid, "vendor_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Description).HasColumnType("character varying");

                entity.Property(e => e.EntityId).HasColumnName("EntityID");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Entity)
                    .WithMany(p => p.Vendors)
                    .HasForeignKey(d => d.EntityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("vendor_vendorentity_id_fk");
            });

            modelBuilder.Entity<VendorEntity>(entity =>
            {
                entity.ToTable("VendorEntity", "Services");

                entity.HasIndex(e => e.Guid, "vendorentity_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.VendorEntities)
                    .HasForeignKey(d => d.GroupId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("vendorentity_vendorentitygroup_id_fk");
            });

            modelBuilder.Entity<VendorEntityGroup>(entity =>
            {
                entity.ToTable("VendorEntityGroup", "Services");

                entity.HasIndex(e => e.Guid, "vendorentitygroup_guid_uindex")
                    .IsUnique();

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Guid)
                    .HasColumnType("character varying")
                    .HasDefaultValueSql("(uuid_generate_v4())::text");

                entity.Property(e => e.IsEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("true");

                entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

                entity.Property(e => e.Name).HasColumnType("character varying");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
