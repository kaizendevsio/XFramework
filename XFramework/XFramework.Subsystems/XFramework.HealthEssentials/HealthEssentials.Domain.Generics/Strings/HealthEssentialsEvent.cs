namespace HealthEssentials.Domain.Generics.Strings;

public static class HealthEssentialsEvent
{
    public static string CommenceConsultation => "Event:HealthEssentials:CommenceConsultation";
    public static string ConcludeConsultation => "Event:HealthEssentials:ConcludeConsultation";
    public static string PrescriptionReminder => "Event:HealthEssentials:PrescriptionReminder";

    public static string NewJobOrder => "Event:HealthEssentials:NewJobOrder";
    public static string UpdatedJobOrder => "Event:HealthEssentials:UpdatedJobOrder";
    public static string MedicineCartQueryFinished => "Event:HealthEssentials:MedicineCartQueryFinished";
    public static string LaboratoryServiceQueryFinished => "Event:HealthEssentials:LaboratoryServiceQueryFinished";
}