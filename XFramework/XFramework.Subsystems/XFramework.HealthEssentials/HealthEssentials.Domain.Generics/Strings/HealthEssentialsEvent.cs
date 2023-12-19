namespace HealthEssentials.Domain.Generics.Strings;

public static class HealthEssentialsEvent
{
    public static readonly string CommenceConsultation = "Event:HealthEssentials:CommenceConsultation";
    public static readonly string ConcludeConsultation = "Event:HealthEssentials:ConcludeConsultation";
    public static readonly string PrescriptionReminder = "Event:HealthEssentials:PrescriptionReminder";
    public static readonly string NewJobOrder = "Event:HealthEssentials:NewJobOrder";
    public static readonly string UpdatedJobOrder = "Event:HealthEssentials:UpdatedJobOrder";
    public static readonly string MedicineCartQueryFinished = "Event:HealthEssentials:MedicineCartQueryFinished";
    public static readonly string LaboratoryServiceQueryFinished = "Event:HealthEssentials:LaboratoryServiceQueryFinished";
}

public static class HealthEssentialsTopic
{
    public static readonly string General = "General";
    public static readonly string AllRiders = "Topic:HealthEssentials:AllRiders";

}