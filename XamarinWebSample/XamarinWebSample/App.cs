using System;

using Xamarin.Forms;
using SimpleInjector;
using Newtonsoft.Json;
using XLabs.Serialization;
using XLabs.Ioc;
using System.Collections.Generic;
using XLabs.Ioc.SimpleInjectorContainer;

namespace XamarinWebSample
{
  public class App : Application
  {
    public Container Container { get; private set; }

    public App (Container container)
    {
      Container = container;
    }

    public static void ConfigureContainer(Container container)
    {
      container.Register<IJsonSerializer> (() => new XLabs.Serialization.JsonNET.JsonSerializer());
      container.Register<SampleWebView> (Lifestyle.Singleton);

      container.Register<JsonSerializer>(() =>
        new JsonSerializer {
          ContractResolver = new UnderscoreContractResolver(),
        }
      );

      var callbackRepo = new WebCallbackRepository (container);
      container.Register<WebCallbackRepository> (() => callbackRepo, Lifestyle.Singleton);
      RegisterWebCallbacks (container, callbackRepo.HasWebCallbackTypes);

    }
    protected override void OnStart()
    {
      var webView = Container.GetInstance<SampleWebView> ();
      //webView.LoadContent("<html>Hello world!</html>");
      webView.LoadFromContent ("www/index.html");

      webView.LoadFinished += (sender, args) => webView.SetupCallbacks ();

    }


    public void Setup () {
      // Create the container and register with XLabs
      // Wire SimpleInjector into XLabs
      if (!Resolver.IsSet) {
        var resolver = new SimpleInjectorResolver (Container);
        Resolver.SetResolver(resolver);
      }


      // Set up application objects
      ConfigureContainer (Container);

      // Instantiate and wire up all WebCallbacks
      Container.GetInstance<WebCallbackRepository>().Autodiscover ();


      // Set the root page
      this.MainPage = Container.GetInstance<MainPage>();
    }

    static void RegisterWebCallbacks (Container container, IEnumerable<Type> types)
    {
      var dict = new Dictionary<Type, InstanceProducer> ();
      foreach (var x in container.GetCurrentRegistrations ()) {
        dict.Add (x.ServiceType, x);
      }
      foreach (var t in types) {
        if (dict.ContainsKey (t)) {
          var ip = dict [t];
          if (ip.Lifestyle != Lifestyle.Singleton) {
            throw new Exception (string.Format ("{0} already registered, but not as Singleton.", t.Name));
          }
        }
        else {
          container.Register (t, t, Lifestyle.Singleton);
        }
      }
    }


  }
}

