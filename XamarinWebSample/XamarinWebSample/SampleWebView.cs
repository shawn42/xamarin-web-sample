using System;
using Newtonsoft.Json;
using Xamarin.Forms;
using XLabs.Forms.Controls;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;


namespace XamarinWebSample
{
  // Must be on a class to be instantiated (on boot) and callbacks setup
  public class HasWebCallbacksAttribute : Attribute { }
  public class WebCallbackAttribute : Attribute { }
  public class WebEventAttribute : Attribute { }

  public class SampleWebView : HybridWebView
  {
    WebCallbackRepository _webCallbackRepository;
    private bool callbacksRegistered;
    public SampleWebView (WebCallbackRepository webCallbackRepository)
    {
      this._webCallbackRepository = webCallbackRepository;
      this.VerticalOptions = LayoutOptions.FillAndExpand;
      this.HorizontalOptions = LayoutOptions.FillAndExpand;

    }
    public void SetupCallbacks()
    {
      if (!callbacksRegistered) {
        callbacksRegistered = true;
        this.RegisterCallback ("Invoke", InvokeFunc);
        this.RegisterCallback ("Subscribe", Subscribe);
        this.RegisterCallback ("Unsubscribe", Unsubscribe);
      }
    }

    public async void InvokeFunc (string argJson)
    {
      var invocation = JsonConvert.DeserializeObject<Invocation> (argJson);

      if (_webCallbackRepository.ContainsCallback (invocation.FuncName)) {

        var callback = _webCallbackRepository.FindCallback (invocation.FuncName);

        try {
          var results = await callback.Invoke (invocation.Payload);
          Xamarin.Forms.Device.BeginInvokeOnMainThread (() => {
            this.CallJsFunction ("xamarinCallbacks.returnValues", invocation.Callback, null, results);
          });
        } catch (Exception e) {
          var err = new {
            Message = e.Message,
            Type = e.GetType ().Name,
          };

          var token = JToken.FromObject (err, _webCallbackRepository.JsonSerializer);
          Xamarin.Forms.Device.BeginInvokeOnMainThread (() => {
            this.CallJsFunction ("xamarinCallbacks.returnValues", invocation.Callback, token);
          });
        }

      } else {
        this.CallJsFunction("xamarinCallbacks.returnValues", invocation.Callback, new {
          Message = string.Format("Unknown web callback {0}", invocation.FuncName),
          Type = "KeyNotFoundException",
        });
      }

    }

    public async void Subscribe (string argJson)
    {
      var invocation = JsonConvert.DeserializeObject<Invocation> (argJson);
      if (_webCallbackRepository.ContainsEvent (invocation.FuncName)) {

        try {
          Action<string> onNext = (string results) => {
            this.CallJsFunction ("xamarinCallbacks.eventOccurred", invocation.Callback, results);
          };
          Action onCompleted = () => {
            this.CallJsFunction ("xamarinCallbacks.destroyCallback", invocation.Callback);
          };
          await _webCallbackRepository.SubscribeEvent(
            invocation.FuncName,
            invocation.Payload,
            invocation.Callback,
            onNext,
            onCompleted);
        } catch (Exception e) {
          var err = new {
            Message = e.Message,
            Type = e.GetType ().Name,
          };

          var token = JToken.FromObject (err, _webCallbackRepository.JsonSerializer);
          Xamarin.Forms.Device.BeginInvokeOnMainThread (() => {
            this.CallJsFunction ("xamarinCallbacks.returnValues", invocation.Callback, token);
          });
        }

      } else {
        this.CallJsFunction("xamarinCallbacks.returnValues", invocation.Callback, new {
          Message = string.Format("Unknown web callback {0}", invocation.FuncName),
          Type = "KeyNotFoundException",
        });
      }

    }

    public async void Unsubscribe (string argJson)
    {
      var invocation = JsonConvert.DeserializeObject<Invocation> (argJson);
      _webCallbackRepository.UnsubscribeEvent(invocation.Callback);
    }
  }

}

