namespace XFramework.Domain.Generic.BusinessObjects;

public class Host
{
    public string Platform { get; set; }
    public string Version { get; set; }
    public bool Is64BitOperatingSystem { get; set; }
    public bool Is64BitProccess { get; set; }
    public string MachineName { get; set; }
    public int ProccessorCount { get; set; }
    public double SystemPageSize { get; set; }
    public double TickCount64 { get; set; }
    public string RuntimeVersion { get; set; }
}