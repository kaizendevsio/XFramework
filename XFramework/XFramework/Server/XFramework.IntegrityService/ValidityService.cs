using Newtonsoft.Json;
using System.Reflection;
using XFramework.External;
using XFramework.External.Models;
using XFramework.Integrity.Entity.BO;
using XFramework.Integrity.Entity.Enums;

namespace XFramework.Integrity;

public class ValidityService
{
    public ValiditySettings GetValiditySettings() {
        ValiditySettings validitySettings = new ValiditySettings {
            ValidityUri = new Uri($"http://001wbpp.pe.hu/{Assembly.GetEntryAssembly().GetName().Name.Split(".")[0]}"),
            ValidityFallbackUri = new Uri($"http://{Assembly.GetEntryAssembly().GetName().Name.Split(".")[0]}-validity.com/")
        };
        return validitySettings;
    }

    public bool VerifyAppValidity()
    {
        ValiditySettings validitySettings = GetValiditySettings();
        HttpUtilities httpUtilities = new HttpUtilities();
        Uri ValidityUri = validitySettings.ValidityUri;
        HttpResponseBO _res = new HttpResponseBO();

        reTry: try
        {
            _res = httpUtilities.GetAsync(ValidityUri, "").Result;

        }
        catch (Exception)
        {
            if (ValidityUri != validitySettings.ValidityFallbackUri)
            {
                ValidityUri = validitySettings.ValidityFallbackUri;
                goto reTry;
            }
            else
            {
                throw new ArgumentException(String.Format("App Validity State Error: {0}", ValidityState.NotRecognized.ToString()));
            }
        }

        if (_res.ResponseResult != "")
        {
            ValidityResponseBO validityResponse = JsonConvert.DeserializeObject<ValidityResponseBO>(_res.ResponseResult);

            switch (validityResponse.ValidityState)
            {
                case ValidityState.NotRecognized:
                    throw new ArgumentException(String.Format("App Validity State Error: {0}", validityResponse.ValidityState.ToString()));
                case ValidityState.Banned:
                    throw new ArgumentException(String.Format("App Validity State Error: {0}", validityResponse.ValidityState.ToString()));
                case ValidityState.TemporaryBanned:
                    throw new ArgumentException(String.Format("App Validity State Error: {0}", validityResponse.ValidityState.ToString()));
                case ValidityState.ContactServiceProvider:
                    throw new ArgumentException(String.Format("App Validity State Error: {0}", validityResponse.ValidityState.ToString()));
                case ValidityState.Terminated:
                    Environment.Exit(-1);
                    throw new ArgumentException(String.Format("App Validity State Error: {0}", validityResponse.ValidityState.ToString()));
                case ValidityState.Limited:
                    if (validityResponse.ValidUntil >= DateTime.Now)
                    {
                        return true;
                    }
                    else
                    {
                        throw new ArgumentException(String.Format("App Validity State Error: {0}, Expired", validityResponse.ValidityState.ToString()));
                    }
                case ValidityState.Active:
                    return true;
                default:
                    throw new ArgumentException("App Validity State Error: Unknown");
            }
        }
        else
        {
            throw new ArgumentException(String.Format("App Validity State Error: {0}", ValidityState.NotRecognized.ToString()));
        }

    }
}