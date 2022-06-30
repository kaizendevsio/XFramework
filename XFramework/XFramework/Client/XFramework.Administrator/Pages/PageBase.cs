using MudBlazor;
using XFramework.Client.Shared.Components;
using XFramework.Client.Shared.Entity.Models;

namespace XFramework.Administrator.Pages;

public class PageBase : XPageBase
{
   public List<SampleModel> Model { get; set; } = new();
}