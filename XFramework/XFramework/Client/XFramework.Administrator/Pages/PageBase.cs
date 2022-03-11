using System.Net.Http.Json;
using MudBlazor.Examples.Data.Models;

namespace XFramework.Administrator.Pages;

public class PageBase : BlazorStateComponent
{
   [Inject] public IHttpClient HttpClient { get; set; }
   [Inject] public NavigationManager NavigationManager { get; set; }
   [Inject] public ISessionStorageService SessionStorageService { get; set; }
   [Inject] public SweetAlertService SweetAlertService { get; set; }
   [Inject] public IJSRuntime JsRuntime { get; set; }
   [Inject] public EndPointsModel EndPoints { get; set; }
   [Inject] public HttpClient httpClient { get; set; }

   public SessionState SessionState => GetState<SessionState>();

   public async Task NavigateTo(string url)
   {
      NavigationManager.NavigateTo(url);
   }
   public IEnumerable<Element> Elements = new List<Element>();

   public List<Element> Elements { get; set; } = new (){
      new Element
      {
         Group = "Group",
         Position = 1,
         Name = "aaa",
         Number = 1,
         Sign = "asas",
         Molar = 0,
         Electrons = null
      },
      new Element
      {
         Group = "Group",
         Position = 1,
         Name = "aaa",
         Number = 1,
         Sign = "asas",
         Molar = 0,
         Electrons = null
      },
      new Element
      {
         Group = "Group",
         Position = 1,
         Name = "aaa",
         Number = 1,
         Sign = "asas",
         Molar = 0,
         Electrons = null
      },
      new Element
      {
         Group = "Group",
         Position = 1,
         Name = "aaa",
         Number = 1,
         Sign = "asas",
         Molar = 0,
         Electrons = null
      },
      new Element
      {
         Group = "Group",
         Position = 1,
         Name = "aaa",
         Number = 1,
         Sign = "asas",
         Molar = 0,
         Electrons = null
      },
      new Element
      {
         Group = "Group",
         Position = 1,
         Name = "aaa",
         Number = 1,
         Sign = "asas",
         Molar = 0,
         Electrons = null
      },
      
   };



}