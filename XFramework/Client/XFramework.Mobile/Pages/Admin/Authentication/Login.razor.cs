using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Ark.Entity.BO;


namespace Ark.Mobile.Pages.Admin.Authentication
{
    public class LoginBase : ComponentBase
    {
        [Inject]
        IJSRuntime JSRuntime { get; set; }
        [Inject]
        HttpClient Http { get; set; }
        [Inject]
        NavigationManager NavManager { get; set; }

        public UserBO userBO { get; set; } = new UserBO() { };

        private async Task PrintWebApiResponse()
        {
            //StateHasChanged();

            var response = await Http.GetStringAsync("startup");
            Console.WriteLine(response);
        }

        public async Task SendFormData()
        {
            ApiResponseBO httpResponse = await Http.PostJsonAsync<ApiResponseBO>("/api/user/authenticate", userBO);
            Console.WriteLine(httpResponse.Message);


            switch (httpResponse.HttpStatusCode)
            {
                case ApiStatus.Success:
                    NavManager.NavigateTo("/Admin/Users");
                    break;
                case ApiStatus.Error:
                    JSRuntime.InvokeAsync<bool>("alert", httpResponse.Message);
                    break;
                default:
                    JSRuntime.InvokeAsync<bool>("alert", httpResponse.Message);
                    break;
            }
        }
    }
}
