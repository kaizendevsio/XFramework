using HealthEssentials.Domain.Generics.Contracts;
using Microsoft.EntityFrameworkCore;

namespace HealthEssentials.Domain.Contexts;

public partial class HealthEssentialsContext : DbContext
{
    public HealthEssentialsContext()
    {
    }

    public HealthEssentialsContext(DbContextOptions<HealthEssentialsContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Ailment> Ailments { get; set; }

    public virtual DbSet<AilmentType> AilmentTypes { get; set; }

    public virtual DbSet<AilmentTypeGroup> AilmentTypeGroups { get; set; }

    public virtual DbSet<AilmentTag> AilmentTags { get; set; }

    public virtual DbSet<Availability> Availabilities { get; set; }

    public virtual DbSet<Consultation> Consultations { get; set; }

    public virtual DbSet<ConsultationType> ConsultationTypes { get; set; }

    public virtual DbSet<ConsultationTypeGroup> ConsultationTypeGroups { get; set; }

    public virtual DbSet<ConsultationJobOrder> ConsultationJobOrders { get; set; }

    public virtual DbSet<ConsultationJobOrderLaboratory> ConsultationJobOrderLaboratories { get; set; }

    public virtual DbSet<ConsultationJobOrderMedicine> ConsultationJobOrderMedicines { get; set; }

    public virtual DbSet<ConsultationTag> ConsultationTags { get; set; }

    public virtual DbSet<Doctor> Doctors { get; set; }

    public virtual DbSet<DoctorConsultation> DoctorConsultations { get; set; }

    public virtual DbSet<DoctorConsultationJobOrder> DoctorConsultationJobOrders { get; set; }

    public virtual DbSet<DoctorType> DoctorTypes { get; set; }

    public virtual DbSet<DoctorTypeGroup> DoctorTypeGroups { get; set; }

    public virtual DbSet<DoctorTag> DoctorTags { get; set; }

    public virtual DbSet<Hospital> Hospitals { get; set; }

    public virtual DbSet<HospitalConsultation> HospitalConsultations { get; set; }

    public virtual DbSet<HospitalType> HospitalTypes { get; set; }

    public virtual DbSet<HospitalTypeGroup> HospitalTypeGroups { get; set; }

    public virtual DbSet<HospitalLaboratory> HospitalLaboratories { get; set; }

    public virtual DbSet<HospitalLocation> HospitalLocations { get; set; }

    public virtual DbSet<HospitalService> HospitalServices { get; set; }

    public virtual DbSet<HospitalServiceType> HospitalServiceTypes { get; set; }

    public virtual DbSet<HospitalServiceTypeGroup> HospitalServiceTypeGroups { get; set; }

    public virtual DbSet<HospitalServiceTag> HospitalServiceTags { get; set; }

    public virtual DbSet<HospitalTag> HospitalTags { get; set; }

    public virtual DbSet<Laboratory> Laboratories { get; set; }

    public virtual DbSet<LaboratoryType> LaboratoryTypes { get; set; }

    public virtual DbSet<LaboratoryTypeGroup> LaboratoryTypeGroups { get; set; }

    public virtual DbSet<LaboratoryJobOrder> LaboratoryJobOrders { get; set; }

    public virtual DbSet<LaboratoryJobOrderDetail> LaboratoryJobOrderDetails { get; set; }

    public virtual DbSet<LaboratoryJobOrderResult> LaboratoryJobOrderResults { get; set; }

    public virtual DbSet<LaboratoryJobOrderResultFile> LaboratoryJobOrderResultFiles { get; set; }

    public virtual DbSet<LaboratoryBranch> LaboratoryBranches { get; set; }

    public virtual DbSet<LaboratoryBranchTag> LaboratoryBranchTags { get; set; }

    public virtual DbSet<LaboratoryMember> LaboratoryMembers { get; set; }

    public virtual DbSet<LaboratoryService> LaboratoryServices { get; set; }

    public virtual DbSet<LaboratoryServiceType> LaboratoryServiceTypes { get; set; }

    public virtual DbSet<LaboratoryServiceTypeGroup> LaboratoryServiceTypeGroups { get; set; }

    public virtual DbSet<LaboratoryServiceTag> LaboratoryServiceTags { get; set; }

    public virtual DbSet<LaboratoryTag> LaboratoryTags { get; set; }

    public virtual DbSet<Logistic> Logistics { get; set; }

    public virtual DbSet<LogisticType> LogisticTypes { get; set; }

    public virtual DbSet<LogisticJobOrder> LogisticJobOrders { get; set; }

    public virtual DbSet<LogisticJobOrderDetail> LogisticJobOrderDetails { get; set; }

    public virtual DbSet<LogisticJobOrderLocation> LogisticJobOrderLocations { get; set; }

    public virtual DbSet<LogisticRider> LogisticRiders { get; set; }

    public virtual DbSet<LogisticRiderHandle> LogisticRiderHandles { get; set; }

    public virtual DbSet<LogisticRiderTag> LogisticRiderTags { get; set; }

    public virtual DbSet<Medicine> Medicines { get; set; }

    public virtual DbSet<MedicineType> MedicineTypes { get; set; }

    public virtual DbSet<MedicineTypeGroup> MedicineTypeGroups { get; set; }

    public virtual DbSet<MedicineIntake> MedicineIntakes { get; set; }

    public virtual DbSet<MedicineIntakeType> MedicineIntakeTypes { get; set; }

    public virtual DbSet<MedicineTag> MedicineTags { get; set; }

    public virtual DbSet<MedicineVendor> MedicineVendors { get; set; }

    public virtual DbSet<MetaData> MetaData { get; set; }

    public virtual DbSet<MetaDataType> MetaDataTypes { get; set; }

    public virtual DbSet<MetaDataTypeGroup> MetaDataTypeGroups { get; set; }

    public virtual DbSet<Patient> Patients { get; set; }

    public virtual DbSet<PatientAilment> PatientAilments { get; set; }

    public virtual DbSet<PatientAilmentDetail> PatientAilmentDetails { get; set; }

    public virtual DbSet<PatientConsultation> PatientConsultations { get; set; }

    public virtual DbSet<PatientType> PatientTypes { get; set; }

    public virtual DbSet<PatientTypeGroup> PatientTypeGroups { get; set; }

    public virtual DbSet<PatientLaboratory> PatientLaboratories { get; set; }

    public virtual DbSet<PatientReminder> PatientReminders { get; set; }

    public virtual DbSet<PatientTag> PatientTags { get; set; }

    public virtual DbSet<Pharmacy> Pharmacies { get; set; }

    public virtual DbSet<PharmacyType> PharmacyTypes { get; set; }

    public virtual DbSet<PharmacyJobOrder> PharmacyJobOrders { get; set; }

    public virtual DbSet<PharmacyJobOrderConsultationJobOrder> PharmacyJobOrderConsultationJobOrders { get; set; }

    public virtual DbSet<PharmacyJobOrderMedicine> PharmacyJobOrderMedicines { get; set; }

    public virtual DbSet<PharmacyBranch> PharmacyBranches { get; set; }

    public virtual DbSet<PharmacyMember> PharmacyMembers { get; set; }

    public virtual DbSet<PharmacyService> PharmacyServices { get; set; }

    public virtual DbSet<PharmacyServiceType> PharmacyServiceTypes { get; set; }

    public virtual DbSet<PharmacyServiceTypeGroup> PharmacyServiceTypeGroups { get; set; }

    public virtual DbSet<PharmacyServiceTag> PharmacyServiceTags { get; set; }

    public virtual DbSet<PharmacyStock> PharmacyStocks { get; set; }

    public virtual DbSet<PharmacyTag> PharmacyTags { get; set; }

    public virtual DbSet<Schedule> Schedules { get; set; }

    public virtual DbSet<ScheduleType> ScheduleTypes { get; set; }

    public virtual DbSet<ScheduleTypeGroup> ScheduleTypeGroups { get; set; }

    public virtual DbSet<SchedulePriority> SchedulePriorities { get; set; }

    public virtual DbSet<SchedulePriorityType> SchedulePriorityTypes { get; set; }

    public virtual DbSet<ScheduleTag> ScheduleTags { get; set; }

    public virtual DbSet<Tag> Tags { get; set; }

    public virtual DbSet<TagType> TagTypes { get; set; }

    public virtual DbSet<TagTypeGroup> TagTypeGroups { get; set; }

    public virtual DbSet<Unit> Units { get; set; }

    public virtual DbSet<UnitType> UnitTypes { get; set; }

    public virtual DbSet<UnitTypeGroup> UnitTypeGroups { get; set; }

    public virtual DbSet<Vendor> Vendors { get; set; }

    public virtual DbSet<VendorType> VendorTypes { get; set; }

    public virtual DbSet<VendorTypeGroup> VendorTypeGroups { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        
        modelBuilder.HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<Ailment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ailment_pk");

            entity.ToTable("Ailment", "Ailment");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())"); // Generate new UUID on insert

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.TypeId).HasColumnName("TypeID");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.OtherName).HasColumnType("character varying");
            entity.Property(e => e.ShortName).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.Ailments)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ailment_ailmententity_id_fk");
        });


        modelBuilder.Entity<AilmentType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ailmententity_pk");

            entity.ToTable("AilmentType", "Ailment");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Group).WithMany(p => p.AilmentTypes)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ailmententity_ailmententitygroup_id_fk");
        });

        modelBuilder.Entity<AilmentTypeGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ailmententitygroup_pk");

            entity.ToTable("AilmentTypeGroup", "Ailment");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<AilmentTag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("metadata_pk");

            entity.ToTable("AilmentTag", "Ailment");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Ailment).WithMany(p => p.AilmentTags)
                .HasForeignKey(d => d.AilmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ailmenttag_ailment_id_fk");

            entity.HasOne(d => d.Tag).WithMany(p => p.AilmentTags)
                .HasForeignKey(d => d.TagId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ailmenttag_tag_id_fk");
        });

        modelBuilder.Entity<Availability>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("availability_pk");

            entity.ToTable("Availability", "Schedule");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.TypeId).HasColumnName("TypeID");

            entity.Property(e => e.IsAvailable)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.Availabilities)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("schedule_scheduleentity_id_fk");
        });

        modelBuilder.Entity<Consultation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("consultation_pk");

            entity.ToTable("Consultation", "Consultation");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.TypeId).HasColumnName("TypeID");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.ShortName).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.Consultations)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("consultation_consultationentity_id_fk");
        });

        modelBuilder.Entity<ConsultationType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("consultationentity_pk");

            entity.ToTable("ConsultationType", "Consultation");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Group).WithMany(p => p.ConsultationTypes)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("consultationentity_consultationentitygroup_id_fk");
        });

        modelBuilder.Entity<ConsultationTypeGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("consultationentitygroup_pk");

            entity.ToTable("ConsultationTypeGroup", "Consultation");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<ConsultationJobOrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("consultationjoborder_pk");

            entity.ToTable("ConsultationJobOrder", "Consultation");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Diagnosis).HasColumnType("character varying");

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

            entity.HasOne(d => d.Consultation).WithMany(p => p.ConsultationJobOrders)
                .HasForeignKey(d => d.ConsultationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("consultationjoborder_consultation_id_fk");

            entity.HasOne(d => d.Schedule).WithMany(p => p.ConsultationJobOrders)
                .HasForeignKey(d => d.ScheduleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("consultationjoborder_schedule_id_fk");
        });

        modelBuilder.Entity<ConsultationJobOrderLaboratory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("consultationjoborderlaboratory_pk");

            entity.ToTable("ConsultationJobOrderLaboratory", "Consultation");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.PrescriptionNote).HasColumnType("character varying");
            entity.Property(e => e.Quantity).HasColumnType("character varying");
            entity.Property(e => e.Remarks).HasColumnType("character varying");

            entity.HasOne(d => d.ConsultationJobOrder).WithMany(p => p.ConsultationJobOrderLaboratories)
                .HasForeignKey(d => d.ConsultationJobOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("consultationjoborderlaboratory_consultationjoborder_id_fk");

            entity.HasOne(d => d.LaboratoryService).WithMany(p => p.ConsultationJobOrderLaboratories)
                .HasForeignKey(d => d.LaboratoryServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("consultationjoborderlaboratory_laboratoryserviceentity_id_fk");

            entity.HasOne(d => d.SuggestedLaboratoryBranch).WithMany(p => p.ConsultationJobOrderLaboratories)
                .HasForeignKey(d => d.SuggestedLaboratoryBranchId)
                .HasConstraintName("consultationjoborderlaboratory_laboratoryBranch_id_fk");
        });

        modelBuilder.Entity<ConsultationJobOrderMedicine>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("consultationjobordermedicine_pk");

            entity.ToTable("ConsultationJobOrderMedicine", "Consultation");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Duration).HasPrecision(2);

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.PrescriptionNote).HasColumnType("character varying");
            entity.Property(e => e.Remarks).HasColumnType("character varying");

            entity.HasOne(d => d.ConsultationJobOrder).WithMany(p => p.ConsultationJobOrderMedicines)
                .HasForeignKey(d => d.ConsultationJobOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("consultationjobordermedicine_consultationjoborder_id_fk");

            entity.HasOne(d => d.DosageUnit).WithMany(p => p.ConsultationJobOrderMedicineDosageUnits)
                .HasForeignKey(d => d.DosageUnitId)
                .HasConstraintName("consultationjobordermedicine_unit_id_fk_3");

            entity.HasOne(d => d.DurationUnit).WithMany(p => p.ConsultationJobOrderMedicineDurationUnits)
                .HasForeignKey(d => d.DurationUnitId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("consultationjobordermedicine_unit_id_fk_2");

            entity.HasOne(d => d.IntakeUnit).WithMany(p => p.ConsultationJobOrderMedicineIntakeUnits)
                .HasForeignKey(d => d.IntakeUnitId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("consultationjobordermedicine_unit_id_fk");

            entity.HasOne(d => d.Medicine).WithMany(p => p.ConsultationJobOrderMedicines)
                .HasForeignKey(d => d.MedicineId)
                .HasConstraintName("consultationjobordermedicine_medicine_id_fk");
            
        });

        modelBuilder.Entity<ConsultationTag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("consultationtag_pk");

            entity.ToTable("ConsultationTag", "Consultation");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Consultation).WithMany(p => p.ConsultationTags)
                .HasForeignKey(d => d.ConsultationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("consultationtag_pharmacy_id_fk");

            entity.HasOne(d => d.Tag).WithMany(p => p.ConsultationTags)
                .HasForeignKey(d => d.TagId)
                .HasConstraintName("consultationtag_tag_id_fk");
        });

        modelBuilder.Entity<Doctor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("doctor_pk");

            entity.ToTable("Doctor", "Doctor");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.Clinic).HasColumnType("character varying");
            entity.Property(e => e.ClinicAddress).HasColumnType("character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CredentialId).HasColumnType("character varying");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.TypeId).HasColumnName("TypeID");

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

            entity.HasOne(d => d.Type).WithMany(p => p.Doctors)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("doctor_doctorentity_id_fk");
        });

        modelBuilder.Entity<DoctorConsultation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("doctorconsultation_pk");

            entity.ToTable("DoctorConsultation", "Doctor");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.MaxDiscount).HasDefaultValueSql("0");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Price).HasDefaultValueSql("0");
            entity.Property(e => e.Quantity).HasDefaultValueSql("1");

            entity.HasOne(d => d.Consultation).WithMany(p => p.DoctorConsultations)
                .HasForeignKey(d => d.ConsultationId)
                .HasConstraintName("doctorconsultation_consultation_id_fk");

            entity.HasOne(d => d.Doctor).WithMany(p => p.DoctorConsultations)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("doctorconsultation_doctor_id_fk");
        });

        modelBuilder.Entity<DoctorConsultationJobOrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("doctorconsultationjoborder_pk");

            entity.ToTable("DoctorConsultationJobOrder", "Doctor");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.ConsultationJobOrder).WithMany(p => p.DoctorConsultationJobOrders)
                .HasForeignKey(d => d.ConsultationJobOrderId)
                .HasConstraintName("doctorconsultation_consultationjoborder_id_fk");

            entity.HasOne(d => d.Doctor).WithMany(p => p.DoctorConsultationJobOrders)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("doctorconsultation_doctor_id_fk");
        });

        modelBuilder.Entity<DoctorType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("doctorentity_pk");

            entity.ToTable("DoctorType", "Doctor");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Group).WithMany(p => p.DoctorTypes)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("doctorentity_doctorentitygroup_id_fk");
        });

        modelBuilder.Entity<DoctorTypeGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("doctorentitygroup_pk");

            entity.ToTable("DoctorTypeGroup", "Doctor");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<DoctorTag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("doctortag_pk");

            entity.ToTable("DoctorTag", "Doctor");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Doctor).WithMany(p => p.DoctorTags)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("doctortag_doctor_id_fk");

            entity.HasOne(d => d.Tag).WithMany(p => p.DoctorTags)
                .HasForeignKey(d => d.TagId)
                .HasConstraintName("doctor_tag_id_fk");
        });

        modelBuilder.Entity<Hospital>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("hospital_pk");

            entity.ToTable("Hospital", "Hospital");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.Email).HasColumnType("character varying");
            entity.Property(e => e.TypeId).HasColumnName("TypeID");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.Logo).HasColumnType("character varying");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.Phone).HasColumnType("character varying");
            entity.Property(e => e.Remarks).HasColumnType("character varying");
            entity.Property(e => e.Website).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.Hospitals)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("hospital_hospitalentity_id_fk");
        });

        modelBuilder.Entity<HospitalConsultation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("hospitalconsultation_pk");

            entity.ToTable("HospitalConsultation", "Hospital");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Consultation).WithMany(p => p.HospitalConsultations)
                .HasForeignKey(d => d.ConsultationId)
                .HasConstraintName("hospitalconsultation_consultation_id_fk");

            entity.HasOne(d => d.Hospital).WithMany(p => p.HospitalConsultations)
                .HasForeignKey(d => d.HospitalId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("hospitalconsultation_hospital_id_fk");
        });

        modelBuilder.Entity<HospitalType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("hospitalentity_pk");

            entity.ToTable("HospitalType", "Hospital");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Group).WithMany(p => p.HospitalTypes)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("hospitalentity_hospitalentitygroup_id_fk");
        });

        modelBuilder.Entity<HospitalTypeGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("hospitalentitygroup_pk");

            entity.ToTable("HospitalTypeGroup", "Hospital");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<HospitalLaboratory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("hospitallaboratory_pk");

            entity.ToTable("HospitalLaboratory", "Hospital");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Hospital).WithMany(p => p.HospitalLaboratories)
                .HasForeignKey(d => d.HospitalId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("hospitalconsultation_hospital_id_fk");

            entity.HasOne(d => d.Laboratory).WithMany(p => p.HospitalLaboratories)
                .HasForeignKey(d => d.LaboratoryId)
                .HasConstraintName("hospitallaboratory_laboratory_id_fk");
        });

        modelBuilder.Entity<HospitalLocation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("hospitallocation_pk");

            entity.ToTable("HospitalLocation", "Hospital");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.BarangayId).HasColumnType("uuid");
            entity.Property(e => e.Building).HasMaxLength(500);
            entity.Property(e => e.CityId).HasColumnType("uuid");
            entity.Property(e => e.CountryId).HasColumnType("uuid");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.ProvinceId).HasColumnType("uuid");
            entity.Property(e => e.RegionId).HasColumnType("uuid");
            entity.Property(e => e.Street).HasMaxLength(500);
            entity.Property(e => e.Subdivision).HasMaxLength(500);
            entity.Property(e => e.UnitNumber).HasMaxLength(500);

            entity.HasOne(d => d.Hospital).WithMany(p => p.HospitalLocations)
                .HasForeignKey(d => d.HospitalId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("hospitallocation_hospital_id_fk");
        });

        modelBuilder.Entity<HospitalService>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("hospitalservice_pk");

            entity.ToTable("HospitalService", "Hospital");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.TypeId).HasColumnName("TypeID");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.ShortName).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.HospitalServices)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("hospitalservice_hospitalserviceentity_id_fk");

            entity.HasOne(d => d.Hospital).WithMany(p => p.HospitalServices)
                .HasForeignKey(d => d.HospitalId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("hospitalservice_hospital_id_fk");

            entity.HasOne(d => d.HospitalLocation).WithMany(p => p.HospitalServices)
                .HasForeignKey(d => d.HospitalLocationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("hospitalservice_hospitallocation_id_fk");

            entity.HasOne(d => d.Unit).WithMany(p => p.HospitalServices)
                .HasForeignKey(d => d.UnitId)
                .HasConstraintName("hospitalservice_unit_id_fk");
        });

        modelBuilder.Entity<HospitalServiceType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("hospitalserviceentity_pk");

            entity.ToTable("HospitalServiceType", "Hospital");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Group).WithMany(p => p.HospitalServiceTypes)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("hospitalserviceentity_hospitalserviceentitygroup_id_fk");
        });

        modelBuilder.Entity<HospitalServiceTypeGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("hospitalserviceentitygroup_pk");

            entity.ToTable("HospitalServiceTypeGroup", "Hospital");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<HospitalServiceTag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("hospitalservicetag_pk");

            entity.ToTable("HospitalServiceTag", "Hospital");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.HospitalService).WithMany(p => p.HospitalServiceTags)
                .HasForeignKey(d => d.HospitalServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("hospitalservicetag_hospital_id_fk");

            entity.HasOne(d => d.Tag).WithMany(p => p.HospitalServiceTags)
                .HasForeignKey(d => d.TagId)
                .HasConstraintName("hospitaltag_tag_id_fk");
        });

        modelBuilder.Entity<HospitalTag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("hospitaltag_pk");

            entity.ToTable("HospitalTag", "Hospital");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Hospital).WithMany(p => p.HospitalTags)
                .HasForeignKey(d => d.HospitalId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("hospitaltag_hospital_id_fk");

            entity.HasOne(d => d.Tag).WithMany(p => p.HospitalTags)
                .HasForeignKey(d => d.TagId)
                .HasConstraintName("hospital_tag_id_fk");
        });

        modelBuilder.Entity<Laboratory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("laboratory_pk");

            entity.ToTable("Laboratory", "Laboratory");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.Email).HasColumnType("character varying");
            entity.Property(e => e.TypeId).HasColumnName("TypeID");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.Logo).HasColumnType("character varying");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.Phone).HasColumnType("character varying");
            entity.Property(e => e.ShortName).HasColumnType("character varying");
            entity.Property(e => e.Website).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.Laboratories)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratory_laboratoryentity_id_fk");
        });

        modelBuilder.Entity<LaboratoryType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("laboratoryentity_pk");

            entity.ToTable("LaboratoryType", "Laboratory");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Group).WithMany(p => p.LaboratoryTypes)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratoryentity_laboratoryentitygroup_id_fk");
        });

        modelBuilder.Entity<LaboratoryTypeGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("laboratoryentitygroup_pk");

            entity.ToTable("LaboratoryTypeGroup", "Laboratory");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<LaboratoryJobOrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("laboratoryjoborder_pk");

            entity.ToTable("LaboratoryJobOrder", "Laboratory");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.ReferenceNumber).HasColumnType("character varying");
            entity.Property(e => e.Remarks).HasColumnType("character varying");

            entity.HasOne(d => d.ConsultationJobOrder).WithMany(p => p.LaboratoryJobOrders)
                .HasForeignKey(d => d.ConsultationJobOrderId)
                .HasConstraintName("laboratoryjoborder_consultationjoborder_id_fk");

            entity.HasOne(d => d.Laboratory).WithMany(p => p.LaboratoryJobOrders)
                .HasForeignKey(d => d.LaboratoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratoryjoborder_laboratory_id_fk");

            entity.HasOne(d => d.LaboratoryBranch).WithMany(p => p.LaboratoryJobOrders)
                .HasForeignKey(d => d.LaboratoryBranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratoryjoborder_laboratoryBranch_id_fk");

            entity.HasOne(d => d.Patient).WithMany(p => p.LaboratoryJobOrders)
                .HasForeignKey(d => d.PatientId)
                .HasConstraintName("laboratoryjoborder_patient_id_fk");

            entity.HasOne(d => d.Schedule).WithMany(p => p.LaboratoryJobOrders)
                .HasForeignKey(d => d.ScheduleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratoryjoborder_schedule_id_fk");
        });

        modelBuilder.Entity<LaboratoryJobOrderDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("laboratoryjoborderdetail_pk");

            entity.ToTable("LaboratoryJobOrderDetail", "Laboratory");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Quantity).HasColumnType("character varying");
            entity.Property(e => e.Remarks).HasColumnType("character varying");

            entity.HasOne(d => d.LaboratoryJobOrder).WithMany(p => p.LaboratoryJobOrderDetails)
                .HasForeignKey(d => d.LaboratoryJobOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratoryjoborderdetail_laboratoryjoborder_id_fk");

            entity.HasOne(d => d.LaboratoryService).WithMany(p => p.LaboratoryJobOrderDetails)
                .HasForeignKey(d => d.LaboratoryServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratoryjoborder_laboratoryservice_id_fk");
        });

        modelBuilder.Entity<LaboratoryJobOrderResult>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("laboratoryjoborderresult_pk");

            entity.ToTable("LaboratoryJobOrderResult", "Laboratory");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.Remarks).HasColumnType("character varying");

            entity.HasOne(d => d.LaboratoryJobOrder).WithMany(p => p.LaboratoryJobOrderResults)
                .HasForeignKey(d => d.LaboratoryJobOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratoryjoborderresult_laboratoryjoborder_id_fk");
        });

        modelBuilder.Entity<LaboratoryJobOrderResultFile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("laboratoryjoborderresultfiles_pk");

            entity.ToTable("LaboratoryJobOrderResultFiles", "Laboratory");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.LaboratoryJobOrderResult).WithMany(p => p.LaboratoryJobOrderResultFiles)
                .HasForeignKey(d => d.LaboratoryJobOrderResultId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratoryjoborderresultfiles_laboratoryjoborderresult_id_fk");
        });

        modelBuilder.Entity<LaboratoryBranch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("laboratoryBranch_pk");

            entity.ToTable("LaboratoryBranch", "Laboratory");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.AlternativePhone).HasColumnType("character varying");
            entity.Property(e => e.BarangayId).HasColumnType("uuid");
            entity.Property(e => e.Building).HasMaxLength(500);
            entity.Property(e => e.CityId).HasColumnType("uuid");
            entity.Property(e => e.CountryId).HasColumnType("uuid");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.Email).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.Phone).HasColumnType("character varying");
            entity.Property(e => e.ProvinceId).HasColumnType("uuid");
            entity.Property(e => e.RegionId).HasColumnType("uuid");
            entity.Property(e => e.Status).HasDefaultValueSql("0");
            entity.Property(e => e.Street).HasMaxLength(500);
            entity.Property(e => e.Subdivision).HasMaxLength(500);
            entity.Property(e => e.UnitNumber).HasMaxLength(500);
            entity.Property(e => e.Website).HasColumnType("character varying");

            entity.HasOne(d => d.Laboratory).WithMany(p => p.LaboratoryBranches)
                .HasForeignKey(d => d.LaboratoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratoryBranch_laboratory_id_fk");
        });

        modelBuilder.Entity<LaboratoryBranchTag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("laboratoryBranchtag_pk");

            entity.ToTable("LaboratoryBranchTag", "Laboratory");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.LaboratoryBranch).WithMany(p => p.LaboratoryBranchTags)
                .HasForeignKey(d => d.LaboratoryBranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratoryBranchtag_laboratoryBranch_id_fk");

            entity.HasOne(d => d.Tag).WithMany(p => p.LaboratoryBranchTags)
                .HasForeignKey(d => d.TagId)
                .HasConstraintName("laboratorylocationtag_tag_id_fk");
        });

        modelBuilder.Entity<LaboratoryMember>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("laboratorymember_pk");

            entity.ToTable("LaboratoryMember", "Laboratory");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CredentialId).HasColumnType("character varying");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Laboratory).WithMany(p => p.LaboratoryMembers)
                .HasForeignKey(d => d.LaboratoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratorymember_laboratory_id_fk");

            entity.HasOne(d => d.LaboratoryBranch).WithMany(p => p.LaboratoryMembers)
                .HasForeignKey(d => d.LaboratoryBranchId)
                .HasConstraintName("laboratorymember_laboratorylocation_id_fk");
        });

        modelBuilder.Entity<LaboratoryService>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("laboratoryservice_pk");

            entity.ToTable("LaboratoryService", "Laboratory");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.TypeId).HasColumnName("TypeID");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.ShortName).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.LaboratoryServices)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratoryservice_laboratoryserviceentity_id_fk");

            entity.HasOne(d => d.Laboratory).WithMany(p => p.LaboratoryServices)
                .HasForeignKey(d => d.LaboratoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratoryservice_laboratory_id_fk");

            entity.HasOne(d => d.LaboratoryBranch).WithMany(p => p.LaboratoryServices)
                .HasForeignKey(d => d.LaboratoryBranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratoryservice_laboratorylocation_id_fk");

            entity.HasOne(d => d.Unit).WithMany(p => p.LaboratoryServices)
                .HasForeignKey(d => d.UnitId)
                .HasConstraintName("laboratoryservice_unit_id_fk");
        });

        modelBuilder.Entity<LaboratoryServiceType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("laboratoryserviceentity_pk");

            entity.ToTable("LaboratoryServiceType", "Laboratory");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Group).WithMany(p => p.LaboratoryServiceTypes)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratoryserviceentity_laboratoryserviceentitygroup_id_fk");
        });

        modelBuilder.Entity<LaboratoryServiceTypeGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("laboratoryserviceentitygroup_pk");

            entity.ToTable("LaboratoryServiceTypeGroup", "Laboratory");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<LaboratoryServiceTag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("laboratoryservicetag_pk");

            entity.ToTable("LaboratoryServiceTag", "Laboratory");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.LaboratoryService).WithMany(p => p.LaboratoryServiceTags)
                .HasForeignKey(d => d.LaboratoryServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratoryservicetag_pharmacy_id_fk");

            entity.HasOne(d => d.Tag).WithMany(p => p.LaboratoryServiceTags)
                .HasForeignKey(d => d.TagId)
                .HasConstraintName("pharmacytag_tag_id_fk");
        });

        modelBuilder.Entity<LaboratoryTag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pharmacytag_pk");

            entity.ToTable("LaboratoryTag", "Laboratory");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Laboratory).WithMany(p => p.LaboratoryTags)
                .HasForeignKey(d => d.LaboratoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("laboratorytag_pharmacy_id_fk");

            entity.HasOne(d => d.Tag).WithMany(p => p.LaboratoryTags)
                .HasForeignKey(d => d.TagId)
                .HasConstraintName("pharmacytag_tag_id_fk");
        });

        modelBuilder.Entity<Logistic>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("logistic_pk");

            entity.ToTable("Logistic", "Logistic");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.Email).HasColumnType("character varying");
            entity.Property(e => e.TypeId).HasColumnName("TypeID");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.Logo).HasColumnType("character varying");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.Phone).HasColumnType("character varying");
            entity.Property(e => e.Remarks).HasColumnType("character varying");
            entity.Property(e => e.Website).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.Logistics)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("logistic_logisticentity_id_fk");
        });

        modelBuilder.Entity<LogisticType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("logisticentity_pk");

            entity.ToTable("LogisticType", "Logistic");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<LogisticJobOrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("logisticjoborder_pk");

            entity.ToTable("LogisticJobOrder", "Logistic");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Rider).WithMany(p => p.LogisticJobOrders)
                .HasForeignKey(d => d.RiderId)
                .HasConstraintName("logisticjoborder_logisticrider_id_fk");

            entity.HasOne(d => d.Schedule).WithMany(p => p.LogisticJobOrders)
                .HasForeignKey(d => d.ScheduleId)
                .HasConstraintName("logisticjoborder_schedule_id_fk");
        });

        modelBuilder.Entity<LogisticJobOrderDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("logisticjoborderdetail_pk");

            entity.ToTable("LogisticJobOrderDetail", "Logistic");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.LocationGuid).HasColumnType("character varying");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.LogisticJobOrder).WithMany(p => p.LogisticJobOrderDetails)
                .HasForeignKey(d => d.LogisticJobOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("logisticjoborderdetail_logisticjoborder_id_fk");

            entity.HasOne(d => d.Unit).WithMany(p => p.LogisticJobOrderDetails)
                .HasForeignKey(d => d.UnitId)
                .HasConstraintName("logisticjoborderdetail_unit_id_fk");
        });

        modelBuilder.Entity<LogisticJobOrderLocation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("logisticjoborderlocation_pk");

            entity.ToTable("LogisticJobOrderLocation", "Logistic");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.BarangayId).HasColumnType("uuid");
            entity.Property(e => e.Building).HasMaxLength(500);
            entity.Property(e => e.CityId).HasColumnType("uuid");
            entity.Property(e => e.ClientGuid).HasColumnType("character varying");
            entity.Property(e => e.ClientName).HasColumnType("character varying");
            entity.Property(e => e.CountryId).HasColumnType("uuid");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.ProvinceId).HasColumnType("uuid");
            entity.Property(e => e.RegionId).HasColumnType("uuid");
            entity.Property(e => e.Street).HasMaxLength(500);
            entity.Property(e => e.Subdivision).HasMaxLength(500);
            entity.Property(e => e.UnitNumber).HasMaxLength(500);

            entity.HasOne(d => d.LogisticJobOrder).WithMany(p => p.LogisticJobOrderLocations)
                .HasForeignKey(d => d.LogisticJobOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("logisticjoborderlocation_logisticjoborder_id_fk");
        });

        modelBuilder.Entity<LogisticRider>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("logisticrider_pk");

            entity.ToTable("LogisticRider", "Logistic");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CredentialId).HasColumnType("character varying");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.LicenseNumber).HasColumnType("character varying");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.PlateNumber).HasColumnType("character varying");
            entity.Property(e => e.Remarks).HasColumnType("character varying");
            entity.Property(e => e.VehicleType).HasColumnType("character varying");
        });

        modelBuilder.Entity<LogisticRiderHandle>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("logisticriderhandle_pk");

            entity.ToTable("LogisticRiderHandle", "Logistic");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.LogisticId).HasColumnName("LogisticID");
            entity.Property(e => e.LogisticRiderId).HasColumnName("LogisticRiderID");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Logistic).WithMany(p => p.LogisticRiderHandles)
                .HasForeignKey(d => d.LogisticId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("logisticriderhandle_logistic_id_fk");

            entity.HasOne(d => d.LogisticRider).WithMany(p => p.LogisticRiderHandles)
                .HasForeignKey(d => d.LogisticRiderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("logisticriderhandle_logisticrider_id_fk");
        });

        modelBuilder.Entity<LogisticRiderTag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("logisticridertag_pk");

            entity.ToTable("LogisticRiderTag", "Logistic");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.LogisticRider).WithMany(p => p.LogisticRiderTags)
                .HasForeignKey(d => d.LogisticRiderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("logistictag_logisticrider_id_fk");

            entity.HasOne(d => d.Tag).WithMany(p => p.LogisticRiderTags)
                .HasForeignKey(d => d.TagId)
                .HasConstraintName("logisticrider_tag_id_fk");
        });

        modelBuilder.Entity<Medicine>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("medicine_pk");

            entity.ToTable("Medicine", "Medicine");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.ChemicalComponent).HasColumnType("character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.ScientificName).HasColumnType("character varying");
            entity.Property(e => e.ShortName).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.Medicines)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("medicine_medicineentity_id_fk");
        });

        modelBuilder.Entity<MedicineType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("metadataentity_pk");

            entity.ToTable("MedicineType", "Medicine");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Group).WithMany(p => p.MedicineTypes)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("medicineentity_medicineentitygroup_id_fk");
        });

        modelBuilder.Entity<MedicineTypeGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("medicineentitygroup_pk");

            entity.ToTable("MedicineTypeGroup", "Medicine");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<MedicineIntake>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("medicineintake_pk");

            entity.ToTable("MedicineIntake", "Medicine");

            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.ScientificName).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.MedicineIntakes)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("medicineintake_medicineintakeentity_id_fk");

            entity.HasOne(d => d.Unit).WithMany(p => p.MedicineIntakes)
                .HasForeignKey(d => d.UnitId)
                .HasConstraintName("medicineintake_unit_id_fk");
        });

        modelBuilder.Entity<MedicineIntakeType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("medicineintakeentity_pk");

            entity.ToTable("MedicineIntakeType", "Medicine");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<MedicineTag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("metadata_pk");

            entity.ToTable("MedicineTag", "Medicine");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Medicine).WithMany(p => p.MedicineTags)
                .HasForeignKey(d => d.MedicineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("medicinetag_medicine_id_fk");

            entity.HasOne(d => d.Tag).WithMany(p => p.MedicineTags)
                .HasForeignKey(d => d.TagId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("medicinetag_tag_id_fk");
        });

        modelBuilder.Entity<MedicineVendor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("medicinevendor_pk");

            entity.ToTable("MedicineVendor", "Medicine");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Medicine).WithMany(p => p.MedicineVendors)
                .HasForeignKey(d => d.MedicineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("medicinetag_medicine_id_fk");

            entity.HasOne(d => d.Vendor).WithMany(p => p.MedicineVendors)
                .HasForeignKey(d => d.VendorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("medicinevendor_vendor_id_fk");
        });

        modelBuilder.Entity<MetaData>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("metadata_pk");

            entity.ToTable("MetaData", "MetaData");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.MetaData)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("metadata_metadataentity_id_fk");
        });

        modelBuilder.Entity<MetaDataType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("metadataentity_pk");

            entity.ToTable("MetaDataType", "MetaData");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Group).WithMany(p => p.MetaDataTypes)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("metadataentity_metadataentitygroup_id_fk");
        });

        modelBuilder.Entity<MetaDataTypeGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("metadataentitygroup_pk");

            entity.ToTable("MetaDataTypeGroup", "MetaData");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("patient_pk");

            entity.ToTable("Patient", "Patient");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CredentialId).HasColumnType("character varying");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.TypeId).HasColumnName("TypeID");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Remarks).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.Patients)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("patient_patiententity_id_fk");
        });

        modelBuilder.Entity<PatientAilment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("patientailment_pk");

            entity.ToTable("PatientAilment", "Patient");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Remarks).HasColumnType("character varying");

            entity.HasOne(d => d.Ailment).WithMany(p => p.PatientAilments)
                .HasForeignKey(d => d.AilmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("patientailment_ailment_id_fk");

            entity.HasOne(d => d.Patient).WithMany(p => p.PatientAilments)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("patientailment_patient_id_fk");
        });

        modelBuilder.Entity<PatientAilmentDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("patientailmentdetail_pk");

            entity.ToTable("PatientAilmentDetail", "Patient");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DoctorName).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Remarks).HasColumnType("character varying");

            entity.HasOne(d => d.ConsultationJobOrder).WithMany(p => p.PatientAilmentDetails)
                .HasForeignKey(d => d.ConsultationJobOrderId)
                .HasConstraintName("patientailment_ailment_id_fk");

            entity.HasOne(d => d.PatientAilment).WithMany(p => p.PatientAilmentDetails)
                .HasForeignKey(d => d.PatientAilmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("patientailmentdetail_patientailment_id_fk");
        });

        modelBuilder.Entity<PatientConsultation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("patientconsultation_pk");

            entity.ToTable("PatientConsultation", "Patient");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.ConsultationJobOrder).WithMany(p => p.PatientConsultations)
                .HasForeignKey(d => d.ConsultationJobOrderId)
                .HasConstraintName("patientconsultation_ailment_id_fk");

            entity.HasOne(d => d.Patient).WithMany(p => p.PatientConsultations)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("patientconsultation_patient_id_fk");
        });

        modelBuilder.Entity<PatientType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("patiententity_pk");

            entity.ToTable("PatientType", "Patient");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Group).WithMany(p => p.PatientTypes)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("patiententity_patiententitygroup_id_fk");
        });

        modelBuilder.Entity<PatientTypeGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("patiententitygroup_pk");

            entity.ToTable("PatientTypeGroup", "Patient");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<PatientLaboratory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("patientlaboratory_pk");

            entity.ToTable("PatientLaboratory", "Patient");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.LaboratoryJobOrder).WithMany(p => p.PatientLaboratories)
                .HasForeignKey(d => d.LaboratoryJobOrderId)
                .HasConstraintName("laboratoryjoborder_ailment_id_fk");

            entity.HasOne(d => d.Patient).WithMany(p => p.PatientLaboratories)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("patientconsultation_patient_id_fk");
        });

        modelBuilder.Entity<PatientReminder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("patientreminder_pk");

            entity.ToTable("PatientReminder", "Patient");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.ConsultationJobOrder).WithMany(p => p.PatientReminders)
                .HasForeignKey(d => d.ConsultationJobOrderId)
                .HasConstraintName("patientreminder_ailment_id_fk");

            entity.HasOne(d => d.Patient).WithMany(p => p.PatientReminders)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("patientreminder_patient_id_fk");
        });

        modelBuilder.Entity<PatientTag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pharmacytag_pk");

            entity.ToTable("PatientTag", "Patient");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Patient).WithMany(p => p.PatientTags)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("patienttag_patient_id_fk");

            entity.HasOne(d => d.Tag).WithMany(p => p.PatientTags)
                .HasForeignKey(d => d.TagId)
                .HasConstraintName("patient_tag_id_fk");
        });

        modelBuilder.Entity<Pharmacy>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pharmacy_pk");

            entity.ToTable("Pharmacy", "Pharmacy");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.ChemicalComponent).HasColumnType("character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.Email).HasColumnType("character varying");

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

            entity.HasOne(d => d.Type).WithMany(p => p.Pharmacies)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pharmacy_pharmacyentity_id_fk");
        });

        modelBuilder.Entity<PharmacyType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pharmacyentity_pk");

            entity.ToTable("PharmacyType", "Pharmacy");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<PharmacyJobOrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pharmacyjoborder_pk");

            entity.ToTable("PharmacyJobOrder", "Pharmacy");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.PrescriptionNote).HasColumnType("character varying");
            entity.Property(e => e.ReferenceNumber).HasColumnType("character varying");
            entity.Property(e => e.Remarks).HasColumnType("character varying");

            entity.HasOne(d => d.Patient).WithMany(p => p.PharmacyJobOrders)
                .HasForeignKey(d => d.PatientId)
                .HasConstraintName("pharmacyjoborder_patient_id_fk");

            entity.HasOne(d => d.PharmacyBranch).WithMany(p => p.PharmacyJobOrders)
                .HasForeignKey(d => d.PharmacyBranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pharmacyjoborder_pharmacylocation_id_fk");

            entity.HasOne(d => d.Schedule).WithMany(p => p.PharmacyJobOrders)
                .HasForeignKey(d => d.ScheduleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pharmacyjoborder_schedule_id_fk");
        });

        modelBuilder.Entity<PharmacyJobOrderConsultationJobOrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pharmacyjoborderconsultationjoborder_pk");

            entity.ToTable("PharmacyJobOrderConsultationJobOrder", "Pharmacy");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Remarks).HasColumnType("character varying");

            entity.HasOne(d => d.ConsultationJobOrder).WithMany(p => p.PharmacyJobOrderConsultationJobOrders)
                .HasForeignKey(d => d.ConsultationJobOrderId)
                .HasConstraintName("pharmacyjoborder_consultationjoborder_id_fk");

            entity.HasOne(d => d.PharmacyJobOrder).WithMany(p => p.PharmacyJobOrderConsultationJobOrders)
                .HasForeignKey(d => d.PharmacyJobOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pharmacyjobordermedicine_pharmacyjoborder_id_fk");
        });

        modelBuilder.Entity<PharmacyJobOrderMedicine>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pharmacyjobordermedicine_pk");

            entity.ToTable("PharmacyJobOrderMedicine", "Pharmacy");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Duration)
                .HasPrecision(2)
                .HasDefaultValueSql("1");
            entity.Property(e => e.DurationUnitId);

            entity.Property(e => e.IntakeUnitId);
            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.PrescriptionNote).HasColumnType("character varying");
            entity.Property(e => e.Remarks).HasColumnType("character varying");

            entity.HasOne(d => d.DosageUnit).WithMany(p => p.PharmacyJobOrderMedicineDosageUnits)
                .HasForeignKey(d => d.DosageUnitId)
                .HasConstraintName("pharmacyjobordermedicine_unit_id_fk_3");

            entity.HasOne(d => d.DurationUnit).WithMany(p => p.PharmacyJobOrderMedicineDurationUnits)
                .HasForeignKey(d => d.DurationUnitId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pharmacyjobordermedicine_unit_id_fk_2");

            entity.HasOne(d => d.IntakeUnit).WithMany(p => p.PharmacyJobOrderMedicineIntakeUnits)
                .HasForeignKey(d => d.IntakeUnitId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pharmacyjobordermedicine_unit_id_fk");

            entity.HasOne(d => d.Medicine).WithMany(p => p.PharmacyJobOrderMedicines)
                .HasForeignKey(d => d.MedicineId)
                .HasConstraintName("pharmacyjobordermedicine_medicine_id_fk");

            entity.HasOne(d => d.PharmacyJobOrder).WithMany(p => p.PharmacyJobOrderMedicines)
                .HasForeignKey(d => d.PharmacyJobOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pharmacyjobordermedicine_pharmacyjoborder_id_fk");
        });

        modelBuilder.Entity<PharmacyBranch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pharmacylocation_pk");

            entity.ToTable("PharmacyBranch", "Pharmacy");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.AlternativePhone).HasColumnType("character varying");
            entity.Property(e => e.BarangayId).HasColumnType("uuid");
            entity.Property(e => e.Building).HasMaxLength(500);
            entity.Property(e => e.CityId).HasColumnType("uuid");
            entity.Property(e => e.CountryId).HasColumnType("uuid");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.Email).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.Phone).HasColumnType("character varying");
            entity.Property(e => e.ProvinceId).HasColumnType("uuid");
            entity.Property(e => e.RegionId).HasColumnType("uuid");
            entity.Property(e => e.Status).HasDefaultValueSql("0");
            entity.Property(e => e.Street).HasMaxLength(500);
            entity.Property(e => e.Subdivision).HasMaxLength(500);
            entity.Property(e => e.UnitNumber).HasMaxLength(500);
            entity.Property(e => e.Website).HasColumnType("character varying");

            entity.HasOne(d => d.Pharmacy).WithMany(p => p.PharmacyBranches)
                .HasForeignKey(d => d.PharmacyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pharmacylocation_pharmacy_id_fk");
        });

        modelBuilder.Entity<PharmacyMember>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pharmacymember_pk");

            entity.ToTable("PharmacyMember", "Pharmacy");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CredentialId).HasColumnType("character varying");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Pharmacy).WithMany(p => p.PharmacyMembers)
                .HasForeignKey(d => d.PharmacyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pharmacymember_pharmacy_id_fk");

            entity.HasOne(d => d.PharmacyBranch).WithMany(p => p.PharmacyMembers)
                .HasForeignKey(d => d.PharmacyBranchId)
                .HasConstraintName("pharmacymember_pharmacylocation_id_fk");
        });

        modelBuilder.Entity<PharmacyService>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pharmacyservice_pk");

            entity.ToTable("PharmacyService", "Pharmacy");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.TypeId).HasColumnName("TypeID");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.ShortName).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.PharmacyServices)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pharmacyservice_pharmacyserviceentity_id_fk");

            entity.HasOne(d => d.Pharmacy).WithMany(p => p.PharmacyServices)
                .HasForeignKey(d => d.PharmacyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pharmacyservice_pharmacy_id_fk");

            entity.HasOne(d => d.PharmacyBranch).WithMany(p => p.PharmacyServices)
                .HasForeignKey(d => d.PharmacyBranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pharmacyservice_pharmacylocation_id_fk");

            entity.HasOne(d => d.Unit).WithMany(p => p.PharmacyServices)
                .HasForeignKey(d => d.UnitId)
                .HasConstraintName("pharmacyservice_unit_id_fk");
        });

        modelBuilder.Entity<PharmacyServiceType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pharmacyserviceentity_pk");

            entity.ToTable("PharmacyServiceType", "Pharmacy");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Group).WithMany(p => p.PharmacyServiceTypes)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pharmacyserviceentity_pharmacyserviceentitygroup_id_fk");
        });

        modelBuilder.Entity<PharmacyServiceTypeGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pharmacyserviceentitygroup_pk");

            entity.ToTable("PharmacyServiceTypeGroup", "Pharmacy");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<PharmacyServiceTag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pharmacyservicetag_pk");

            entity.ToTable("PharmacyServiceTag", "Pharmacy");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.PharmacyService).WithMany(p => p.PharmacyServiceTags)
                .HasForeignKey(d => d.PharmacyServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pharmacyservicetag_pharmacy_id_fk");

            entity.HasOne(d => d.Tag).WithMany(p => p.PharmacyServiceTags)
                .HasForeignKey(d => d.TagId)
                .HasConstraintName("pharmacytag_tag_id_fk");
        });

        modelBuilder.Entity<PharmacyStock>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pharmacystocks_pk");

            entity.ToTable("PharmacyStocks", "Pharmacy");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.LastRestock).HasDefaultValueSql("now()");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Medicine).WithMany(p => p.PharmacyStocks)
                .HasForeignKey(d => d.MedicineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pharmacystocks_medicine_id_fk");

            entity.HasOne(d => d.Pharmacy).WithMany(p => p.PharmacyStocks)
                .HasForeignKey(d => d.PharmacyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pharmacystocks_pharmacy_id_fk");

            entity.HasOne(d => d.UnitNavigation).WithMany(p => p.PharmacyStocks)
                .HasForeignKey(d => d.Unit)
                .HasConstraintName("pharmacystocks_unit_id_fk");
        });

        modelBuilder.Entity<PharmacyTag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pharmacytag_pk");

            entity.ToTable("PharmacyTag", "Pharmacy");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Pharmacy).WithMany(p => p.PharmacyTags)
                .HasForeignKey(d => d.PharmacyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("pharmacytag_pharmacy_id_fk");

            entity.HasOne(d => d.Tag).WithMany(p => p.PharmacyTags)
                .HasForeignKey(d => d.TagId)
                .HasConstraintName("pharmacytag_tag_id_fk");
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("schedule_pk");

            entity.ToTable("Schedule", "Schedule");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.TypeId).HasColumnName("TypeID");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.PriorityId).HasColumnName("PriorityID");

            entity.HasOne(d => d.Type).WithMany(p => p.Schedules)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("schedule_scheduleentity_id_fk");

            entity.HasOne(d => d.Priority).WithMany(p => p.Schedules)
                .HasForeignKey(d => d.PriorityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("schedule_schedulepriority_id_fk");
        });

        modelBuilder.Entity<ScheduleType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("scheduleentity_pk");

            entity.ToTable("ScheduleType", "Schedule");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Group).WithMany(p => p.ScheduleTypes)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("scheduleentity_scheduleentitygroup_id_fk");
        });

        modelBuilder.Entity<ScheduleTypeGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("scheduleentitygroup_pk");

            entity.ToTable("ScheduleTypeGroup", "Schedule");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<SchedulePriority>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("schedulepriority_pk");

            entity.ToTable("SchedulePriority", "Schedule");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.TypeId).HasColumnName("TypeID");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.SchedulePriorities)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("schedulepriority_schedulepriorityentity_id_fk");
        });

        modelBuilder.Entity<SchedulePriorityType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("schedulepriorityentity_pk");

            entity.ToTable("SchedulePriorityType", "Schedule");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<ScheduleTag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("metadata_pk");

            entity.ToTable("ScheduleTag", "Schedule");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Value).HasColumnType("character varying");

            entity.HasOne(d => d.Schedule).WithMany(p => p.ScheduleTags)
                .HasForeignKey(d => d.ScheduleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("scheduletag_schedule_id_fk");

            entity.HasOne(d => d.Tag).WithMany(p => p.ScheduleTags)
                .HasForeignKey(d => d.TagId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("scheduletag_tag_id_fk");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tag_pk");

            entity.ToTable("Tag", "Tag");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.TypeId).HasColumnName("TypeID");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.ShortName).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.Tags)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tag_tagentity_id_fk");
        });

        modelBuilder.Entity<TagType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tagentity_pk");

            entity.ToTable("TagType", "Tag");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Group).WithMany(p => p.TagTypes)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tagentity_tagentitygroup_id_fk");
        });

        modelBuilder.Entity<TagTypeGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tagentitygroup_pk");

            entity.ToTable("TagTypeGroup", "Tag");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<Unit>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("unit_pk");

            entity.ToTable("Unit", "Unit");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.TypeId).HasColumnName("TypeID");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
            entity.Property(e => e.ShortName).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.Units)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("unit_unitentity_id_fk");
        });

        modelBuilder.Entity<UnitType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("unitentity_pk");

            entity.ToTable("UnitType", "Unit");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Group).WithMany(p => p.UnitTypes)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("unitentity_unitentitygroup_id_fk");
        });

        modelBuilder.Entity<UnitTypeGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("unitentitygroup_pk");

            entity.ToTable("UnitTypeGroup", "Unit");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        modelBuilder.Entity<Vendor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("metadata_pk");

            entity.ToTable("Vendor", "Services");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Description).HasColumnType("character varying");
            entity.Property(e => e.TypeId).HasColumnName("TypeID");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Type).WithMany(p => p.Vendors)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("vendor_vendorentity_id_fk");
        });

        modelBuilder.Entity<VendorType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vendorentity_pk");

            entity.ToTable("VendorType", "Services");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");

            entity.HasOne(d => d.Group).WithMany(p => p.VendorTypes)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("vendorentity_vendorentitygroup_id_fk");
        });

        modelBuilder.Entity<VendorTypeGroup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vendorentitygroup_pk");

            entity.ToTable("VendorTypeGroup", "Services");

            
            entity.Property(e => e.Id)
                .HasColumnName("ID")
                .HasDefaultValueSql("(uuid_generate_v4())");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.Property(e => e.IsEnabled)
                .IsRequired()
                .HasDefaultValueSql("true");
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Name).HasColumnType("character varying");
        });

        OnModelCreatingPartial(modelBuilder);
        SeedDatabase(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

    protected void SeedDatabase(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConsultationTypeGroup>().HasData(
            new ConsultationTypeGroup
            {
                Id = new Guid("3246bc14-c348-4e4e-9b0d-5d1d51760d16"),
                Name = "Unnamed",
            }
        );

        modelBuilder.Entity<ConsultationType>().HasData(
            new ConsultationType
            {
                Id = new Guid("5037e7ae-864c-4336-b0bd-32350cf334aa"),
                Name = "Emergency Consultation",
                Description = "Urgent consultation for acute illnesses or injuries",
                GroupId = new Guid("3246bc14-c348-4e4e-9b0d-5d1d51760d16")
            },
            new ConsultationType
            {
                Id = new Guid("f9c35417-bc70-4035-b70a-ac2c71b8a051"),
                Name = "Follow-up Check-up",
                Description = "Subsequent check-up following an initial consultation or treatment",
                GroupId = new Guid("3246bc14-c348-4e4e-9b0d-5d1d51760d16")
            },
            new ConsultationType
            {
                Id = new Guid("cda4df14-aa1c-4fc9-86a4-cb855b84b555"),
                Name = "General Check-up",
                Description = "Routine health check-up with primary care physician",
                GroupId = new Guid("3246bc14-c348-4e4e-9b0d-5d1d51760d16")
            },
            new ConsultationType
            {
                Id = new Guid("73e5ed91-581e-47bf-8d7e-9c273847c483"),
                Name = "Mental Health Consultation",
                Description = "Consultation for mental health issues (e.g., with a psychiatrist or psychologist)",
                GroupId = new Guid("3246bc14-c348-4e4e-9b0d-5d1d51760d16")
            },
            new ConsultationType
            {
                Id = new Guid("3259dc78-49d0-4359-8c8b-b3cfe7ccc421"),
                Name = "Post-surgical Follow-up",
                Description = "Follow-up visit after a surgical procedure",
                GroupId = new Guid("3246bc14-c348-4e4e-9b0d-5d1d51760d16")
            },
            new ConsultationType
            {
                Id = new Guid("c3221472-9a87-4be9-982a-d442296b487a"),
                Name = "Pre-surgical Consultation",
                Description = "Consultation before undergoing surgery",
                GroupId = new Guid("3246bc14-c348-4e4e-9b0d-5d1d51760d16")
            },
            new ConsultationType
            {
                Id = new Guid("ab3dc83a-0841-45ea-822d-19580b32cd91"),
                Name = "Specialist Consultation",
                Description = "Consultation with a medical specialist (e.g., cardiologist, neurologist)",
                GroupId = new Guid("3246bc14-c348-4e4e-9b0d-5d1d51760d16")
            }
        );

        modelBuilder.Entity<Consultation>().HasData(
            new Consultation
            {
                Id = new Guid("ac671073-c789-41c4-8c63-eb9c9084fde9"),
                TypeId = new Guid("ab3dc83a-0841-45ea-822d-19580b32cd91"),
                Name = "Cardiologist",
                Description = "Heart and cardiovascular system related consultations"
            },
            new Consultation
            {
                Id = new Guid("c051027c-dd33-4b2b-ac59-0024e650b231"),
                TypeId = new Guid("ab3dc83a-0841-45ea-822d-19580b32cd91"),
                Name = "Dermatologist",
                Description = "Skin related consultations, including skin diseases, allergies, and cosmetic concerns"
            },
            new Consultation
            {
                Id = new Guid("8c0d8be1-5241-40e9-bf2c-5a458ba58bac"),
                TypeId = new Guid("ab3dc83a-0841-45ea-822d-19580b32cd91"),
                Name = "ENT (Ear, Nose, Throat)",
                Description = "Consultation for conditions affecting the ear, nose, and throat"
            },
            new Consultation
            {
                Id = new Guid("f354d822-c472-4677-8086-7d6ec89047bf"),
                TypeId = new Guid("ab3dc83a-0841-45ea-822d-19580b32cd91"),
                Name = "Gastroenterologist",
                Description = "Consultation for digestive system and gastrointestinal tract issues"
            },
            new Consultation
            {
                Id = new Guid("439c4908-63e2-4648-92b6-8ed10772e7ef"),
                TypeId = new Guid("ab3dc83a-0841-45ea-822d-19580b32cd91"),
                Name = "General Check-up",
                Description = "Routine health check-up with a primary care physician"
            },
            new Consultation
            {
                Id = new Guid("0297352d-4788-4ee4-9241-e2cea66e41cd"),
                TypeId = new Guid("ab3dc83a-0841-45ea-822d-19580b32cd91"),
                Name = "Neurologist",
                Description = "Consultation for disorders of the nervous system, brain, and spinal cord"
            },
            new Consultation
            {
                Id = new Guid("46e7bbe6-7ffe-47f7-858b-0f521273eb0f"),
                TypeId = new Guid("ab3dc83a-0841-45ea-822d-19580b32cd91"),
                Name = "Optometrist/Ophthalmologist",
                Description = "Eye examinations, vision care, and treatment of eye-related conditions"
            },
            new Consultation
            {
                Id = new Guid("aa4e36c7-cde2-481b-9c6a-e7276ab8f55a"),
                TypeId = new Guid("ab3dc83a-0841-45ea-822d-19580b32cd91"),
                Name = "Orthopedist",
                Description = "Consultation for issues related to bones, joints, muscles, and ligaments"
            },
            new Consultation
            {
                Id = new Guid("901e6429-b7a1-421e-a72c-f25f4963e81b"),
                TypeId = new Guid("ab3dc83a-0841-45ea-822d-19580b32cd91"),
                Name = "Pediatrician",
                Description = "Healthcare for infants, children, and adolescents"
            },
            new Consultation
            {
                Id = new Guid("17d8f674-e960-4b4b-ac54-4d1f9a42a8d7"),
                TypeId = new Guid("ab3dc83a-0841-45ea-822d-19580b32cd91"),
                Name = "Psychiatrist/Psychologist",
                Description = "Mental health consultations, including therapy and medication management"
            },
            new Consultation
            {
                Id = new Guid("66e64415-73ea-4797-bc0a-d13a2c095501"),
                TypeId = new Guid("cda4df14-aa1c-4fc9-86a4-cb855b84b555"),
                Name = "General Check-up",
                Description = "Routine health check-up with primary care physician"
            }
        );
        
        // Seeding data for PatientTypeGroup
        modelBuilder.Entity<PatientTypeGroup>().HasData(
            new PatientTypeGroup
            {
                Id = new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"),
                Name = "General",
                IsEnabled = true,
                IsDeleted = false,
                ConcurrencyStamp = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                // TenantId is not set, defaulting to null
            },
            new PatientTypeGroup
            {
                Id = new Guid("7a284dea-f6c1-4025-9149-842ccae76236"),
                Name = "Specialized",
                IsEnabled = true,
                IsDeleted = false,
                ConcurrencyStamp = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                // TenantId is not set, defaulting to null
            }
        );

        // Seeding data for PatientType
        modelBuilder.Entity<PatientType>().HasData(
            new PatientType
            {
                Id = new Guid("34ee8325-8060-43c7-b4a8-b8e861db6c47"),
                Name = "Outpatient",
                Description = "Outpatient services",
                GroupId = new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"), // General
                SortOrder = 1,
                IsEnabled = true,
                IsDeleted = false,
                ConcurrencyStamp = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            },
            new PatientType
            {
                Id = new Guid("da22eb54-e064-43d1-89ed-51591f21f903"),
                Name = "Inpatient",
                Description = "Inpatient services",
                GroupId = new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"), // General
                SortOrder = 2,
                IsEnabled = true,
                IsDeleted = false,
                ConcurrencyStamp = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            },
            new PatientType
            {
                Id = new Guid("486917df-2b21-41fe-aa03-c564014f8cad"),
                Name = "Pediatric",
                Description = "Specialized pediatric services",
                GroupId = new Guid("7a284dea-f6c1-4025-9149-842ccae76236"), // Specialized
                SortOrder = 1,
                IsEnabled = true,
                IsDeleted = false,
                ConcurrencyStamp = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            },
            // Adding new patient types
            new PatientType
            {
                Id = new Guid("1001c82f-987a-4ed2-893f-b0237aec69c4"),
                Name = "Emergency",
                Description = "Immediate medical attention for life-threatening conditions",
                GroupId = new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"), // General
                SortOrder = 3,
                IsEnabled = true,
                IsDeleted = false,
                ConcurrencyStamp = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            },
            new PatientType
            {
                Id = new Guid("e18ac75a-2d74-4670-8da2-201a476306ac"),
                Name = "Surgical",
                Description = "Patients admitted for surgical procedures",
                GroupId = new Guid("7a284dea-f6c1-4025-9149-842ccae76236"), // Specialized
                SortOrder = 2,
                IsEnabled = true,
                IsDeleted = false,
                ConcurrencyStamp = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            },
            new PatientType
            {
                Id = new Guid("e36b4212-3add-452f-8448-825242821176"),
                Name = "Chronic Care",
                Description = "Long-term care for ongoing conditions like diabetes, heart disease",
                GroupId = new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"), // General
                SortOrder = 4,
                IsEnabled = true,
                IsDeleted = false,
                ConcurrencyStamp = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            },
            new PatientType
            {
                Id = new Guid("d8149d7e-fc1b-4b3d-8c37-7befea74bbce"),
                Name = "Rehabilitation",
                Description = "Recovery and rehabilitation services for post-surgery or injury",
                GroupId = new Guid("7a284dea-f6c1-4025-9149-842ccae76236"), // Specialized
                SortOrder = 3,
                IsEnabled = true,
                IsDeleted = false,
                ConcurrencyStamp = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            },
            new PatientType
            {
                Id = new Guid("b9099ba0-e446-4b71-8ec0-be1ced260a42"),
                Name = "Maternity",
                Description = "Care for childbirth and postnatal services",
                GroupId = new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"), // General
                SortOrder = 5,
                IsEnabled = true,
                IsDeleted = false,
                ConcurrencyStamp = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            },
            new PatientType
            {
                Id = new Guid("ff0fdb4a-64b8-4b27-8c05-f55819a4511e"),
                Name = "Geriatric",
                Description = "Specialized care for elderly patients",
                GroupId = new Guid("7a284dea-f6c1-4025-9149-842ccae76236"), // Specialized
                SortOrder = 4,
                IsEnabled = true,
                IsDeleted = false,
                ConcurrencyStamp = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            },
            new PatientType
            {
                Id = new Guid("ad6aa943-8f49-47f6-980c-7f45d5e4db58"),
                Name = "Psychiatric",
                Description = "Treatment for mental health conditions",
                GroupId = new Guid("7a284dea-f6c1-4025-9149-842ccae76236"), // Specialized
                SortOrder = 5,
                IsEnabled = true,
                IsDeleted = false,
                ConcurrencyStamp = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            },
            new PatientType
            {
                Id = new Guid("0a98a21f-0e34-418c-a2f9-67e42b0898fe"),
                Name = "Palliative Care",
                Description = "Relief from the symptoms and stress of a serious illness",
                GroupId = new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"), // General
                SortOrder = 6,
                IsEnabled = true,
                IsDeleted = false,
                ConcurrencyStamp = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            },
            new PatientType
            {
                Id = new Guid("26b63a34-5aee-4474-9b9c-74a867e947cc"),
                Name = "Ambulatory",
                Description = "Patients visiting for outpatient services without overnight stay",
                GroupId = new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"), // General
                SortOrder = 7,
                IsEnabled = true,
                IsDeleted = false,
                ConcurrencyStamp = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            },
            new PatientType
            {
                Id = new Guid("c611fced-0cf2-4fc5-80ae-de0154bba11e"),
                Name = "Home Care",
                Description = "Medical care or treatment provided at the patient's home",
                GroupId = new Guid("1fdf84c3-a53f-42bc-9cf9-22ca728ddef3"), // General
                SortOrder = 8,
                IsEnabled = true,
                IsDeleted = false,
                ConcurrencyStamp = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            }
        );

    }
}