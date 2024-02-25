﻿using XFramework.Core.Attributes;

namespace HealthEssentials.Api.Generators;

[GenerateApiFromNamespace("HealthEssentials.Domain.Generics.Contracts",new[] {
    nameof(Ailment),
    nameof(AilmentType),
    nameof(AilmentTypeGroup),
    nameof(AilmentTag),
    nameof(Availability),
    nameof(Consultation),
    nameof(ConsultationType),
    nameof(ConsultationTypeGroup),
    nameof(ConsultationJobOrder),
    nameof(ConsultationJobOrderLaboratory),
    nameof(ConsultationJobOrderMedicine),
    nameof(ConsultationTag),
    nameof(Doctor),
    nameof(DoctorConsultation),
    nameof(DoctorConsultationJobOrder),
    nameof(DoctorType),
    nameof(DoctorTypeGroup),
    nameof(DoctorTag),
    nameof(Hospital),
    nameof(HospitalConsultation),
    nameof(HospitalType),
    nameof(HospitalTypeGroup),
    nameof(HospitalLaboratory),
    nameof(HospitalLocation),
    nameof(HospitalService),
    nameof(HospitalServiceType),
    nameof(HospitalServiceTypeGroup),
    nameof(HospitalServiceTag),
    nameof(HospitalTag),
    nameof(Laboratory),
    nameof(LaboratoryType),
    nameof(LaboratoryTypeGroup),
    nameof(LaboratoryJobOrder),
    nameof(LaboratoryJobOrderDetail),
    nameof(LaboratoryJobOrderResult),
    nameof(LaboratoryJobOrderResultFile),
    nameof(LaboratoryBranch),
    nameof(LaboratoryLocationTag),
    nameof(LaboratoryMember),
    nameof(LaboratoryService),
    nameof(LaboratoryServiceType),
    nameof(LaboratoryServiceTypeGroup),
    nameof(LaboratoryServiceTag),
    nameof(LaboratoryTag),
    nameof(Logistic),
    nameof(LogisticType),
    nameof(LogisticJobOrder),
    nameof(LogisticJobOrderDetail),
    nameof(LogisticJobOrderLocation),
    nameof(LogisticRider),
    nameof(LogisticRiderHandle),
    nameof(LogisticRiderTag),
    nameof(Medicine),
    nameof(MedicineType),
    nameof(MedicineTypeGroup),
    nameof(MedicineIntake),
    nameof(MedicineIntakeType),
    nameof(MedicineTag),
    nameof(MedicineVendor),
    nameof(MetaData),
    nameof(MetaDataType),
    nameof(MetaDataTypeGroup),
    nameof(Patient),
    nameof(PatientAilment),
    nameof(PatientAilmentDetail),
    nameof(PatientConsultation),
    nameof(PatientType),
    nameof(PatientTypeGroup),
    nameof(PatientLaboratory),
    nameof(PatientReminder),
    nameof(PatientTag),
    nameof(Pharmacy),
    nameof(PharmacyType),
    nameof(PharmacyJobOrder),
    nameof(PharmacyJobOrderConsultationJobOrder),
    nameof(PharmacyJobOrderMedicine),
    nameof(PharmacyLocation),
    nameof(PharmacyMember),
    nameof(PharmacyService),
    nameof(PharmacyServiceType),
    nameof(PharmacyServiceTypeGroup),
    nameof(PharmacyServiceTag),
    nameof(PharmacyStock),
    nameof(PharmacyTag),
    nameof(Schedule),
    nameof(ScheduleType),
    nameof(ScheduleTypeGroup),
    nameof(SchedulePriority),
    nameof(SchedulePriorityType),
    nameof(ScheduleTag),
    nameof(Tag),
    nameof(TagType),
    nameof(TagTypeGroup),
    nameof(Unit),
    nameof(UnitType),
    nameof(UnitTypeGroup),
    nameof(Vendor),
    nameof(VendorType),
    nameof(VendorTypeGroup)
})]
public static partial class HealthEssentialsApiGenerator;