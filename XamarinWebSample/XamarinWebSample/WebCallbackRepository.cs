using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SimpleInjector;
using Newtonsoft.Json.Serialization;
using System.Linq;
using System.Reactive.Linq;
using Newtonsoft.Json.Linq;
using Xamarin.Forms;

namespace XamarinWebSample
{
  public struct Invocation
  {
    public string FuncName {get; set;}
    public string Payload {get; set;}
    public string Callback {get; set;}
  }

  public struct InvocationResult
  {
    public bool Success {get; set;}
    public object[] Results { get; set; }
  }

  public class WebCallbackRepository
  {
    private Dictionary<string, object> _callbackMap;
    private Dictionary<string, Func<string, Task<IObservable<string>>>> _eventMap;
    private Dictionary<string, IDisposable> _subscriptions;
    private IEnumerable<TypeInfo> _hasWebCallbackTypes;

    private Container container;

    public readonly JsonSerializerSettings Settings;
    public readonly JsonSerializer JsonSerializer;

    public WebCallbackRepository (Container container)
    {
      this.container = container;
      _callbackMap = new Dictionary<string, object> ();
      _eventMap = new Dictionary<string, Func<string, Task<IObservable<string>>>>();
      _subscriptions = new Dictionary<string, IDisposable>();

      Settings = new JsonSerializerSettings{
        ContractResolver = new CamelCasePropertyNamesContractResolver ()
      };

      JsonSerializer = JsonSerializer.Create (Settings);
    }

    public IEnumerable<TypeInfo> HasWebCallbackTypeInfos {
      get {
        if (_hasWebCallbackTypes == null) {
          var asm = typeof(WebCallbackRepository).GetTypeInfo ().Assembly;
          _hasWebCallbackTypes = asm.DefinedTypes.Where ((t) => t.GetCustomAttributes (typeof(HasWebCallbacksAttribute), false).Any ());
        }
        return _hasWebCallbackTypes;
      }
    }
    public IEnumerable<Type> HasWebCallbackTypes{
      get {
        return HasWebCallbackTypeInfos.Select ((info) => info.AsType());
      }
    }

    public void Autodiscover()
    {
      var classes = HasWebCallbackTypeInfos;

      foreach (var cls in classes) {
        var callbacks = cls.GetMethods().Where ((m) => m.GetCustomAttributes (typeof(WebCallbackAttribute), false).Any ()).ToArray();
        var events = cls.GetMethods().Where ((m) => m.GetCustomAttributes (typeof(WebEventAttribute), false).Any ()).ToArray();

        if (container.GetRegistration (cls.AsType()).Lifestyle != Lifestyle.Singleton) {
          throw new ArgumentException ("You forgot to register " + cls.Name + " as a singleton.");
        }

        if (callbacks.Length > 0) {
          var instance = container.GetInstance (cls.AsType ());
          foreach (var method in callbacks) {
            RegisterCallbackInfo (
              cls.Name + "." + method.Name,
              instance,
              method
            );
          }
        }

        if (events.Length > 0) {
          var instance = container.GetInstance (cls.AsType ());
          foreach (var method in events) {
            RegisterEventInfo (
              cls.Name + "." + method.Name,
              instance,
              method
            );
          }
        }
      }
    }

    // Given an instance of an object and a MethodInfo for one of its methods,
    // register a callback for funcName which invokes the method with the deserialized
    // argument (given json) and converts the result to json.
    public void RegisterCallbackInfo(string funcName, object instance, MethodInfo info)
    {
      var paramInfos = info.GetParameters ();

      var genericTypeArguments = info.ReturnType.GenericTypeArguments;
      var resultT =  genericTypeArguments.Length > 0 ? genericTypeArguments[0] : info.ReturnType;
      var serializeMethod = typeof(WebCallbackRepository).GetTypeInfo ().GetDeclaredMethod ("Serialize1").MakeGenericMethod (resultT);

      var buildArgs = MakeArgsBuilder (funcName, paramInfos);

      Func<string, Task<string[]>> func = async (string argJson) => {
        var results = await Task.Run(async () => {
          string[] ret = new string[]{};
          try {
            var args = await buildArgs(argJson);
            dynamic task = info.Invoke (instance, args);
            dynamic result = await task;
            ret = (string[])serializeMethod.Invoke(this, new object[]{ result });
          } catch (Exception ex) {
            Console.WriteLine("BOOM:");
            Console.WriteLine(ex);
            throw;
          }
          return ret;
        });
        return results;
      };

      _callbackMap.Add(funcName, func);
    }

    // Given an instance of an object and a MethodInfo for one of its methods,
    // register a callback for funcName which invokes the method with the deserialized
    // argument (given json) and converts the result to json.
    public void RegisterEventInfo(string funcName, object instance, MethodInfo info)
    {
      var paramInfos = info.GetParameters ();
      var buildArgs = MakeArgsBuilder (funcName, paramInfos);

      Func<string, Task<IObservable<string>>> func = async (string argJson) => {
        var results = await Task.Run(async () => {
          var args = await buildArgs(argJson);
          dynamic task = info.Invoke (instance, args);
          dynamic observable = await task;
          Func<object, string> cast = e => FormatJson(e);
          return Observable.Select<object,string>(observable, cast);
        });
        return results;
      };

      _eventMap.Add(funcName, func);
    }

    static Func<string, Task<object[]>> MakeArgsBuilder (string funcName, ParameterInfo[] paramInfos)
    {
      Func<string, Task<object[]>> buildArgs;

      switch (paramInfos.Length) {
      case 0:
        // Ignore the JSON args. This is a zero-arg function.
        buildArgs = (string s) => Task.FromResult(new object[0]);
        break;

      default:
        var types = paramInfos.Select (i => i.ParameterType);
        var count = paramInfos.Length;
        buildArgs = (string argJson) =>  {
          var argToken = JToken.Parse (argJson);
          if (argToken.Type != JTokenType.Array) {
            throw new ArgumentException (string.Format ("Non-array passed to {0}. (Json was a {1})", funcName, JTokenType.GetName (typeof(JTokenType), argToken.Type)));
          }

          var argArray = argToken as JArray;
          if (argArray.Count != count) {
            throw new ArgumentException (string.Format ("Array of invalid size passed to {0}. (expected {1}, got {2})", funcName, paramInfos.Length, argArray.Count));
          }
          var args = argArray.Zip (types, (entry, type) => entry.ToObject (type));
          return Task.FromResult(args.ToArray ());
        };
        break;
      }
      return buildArgs;
    }

    public Func<string,Task<string[]>> FindCallback (string funcName)
    {
      return (Func<string, Task<string[]>>) _callbackMap [funcName];
    }

    public bool ContainsCallback(string funcName)
    {
      return _callbackMap.ContainsKey (funcName);
    }

    public bool ContainsEvent(string funcName)
    {
      return _eventMap.ContainsKey (funcName);
    }

    public async Task SubscribeEvent(string funcName, string argJson, string subscriptionKey, Action<string> onNext, Action onCompleted)
    {
      var observableFactory = _eventMap [funcName];
      var observable = await observableFactory.Invoke(argJson);
      var observer = new EventObserver (onNext, onCompleted);
      var handle = observable.Subscribe(observer);
      _subscriptions[subscriptionKey] = handle;
    }

    public void UnsubscribeEvent(string subscriptionKey)
    {
      IDisposable handle;
      if (_subscriptions.TryGetValue(subscriptionKey, out handle))
      {
        Device.BeginInvokeOnMainThread(() => {
          handle.Dispose();
          _subscriptions.Remove(subscriptionKey);
        });
      }
    }

    class EventObserver : IObserver<string>
    {
      private Action _onCompleted;
      private Action<string> _onNext;

      public EventObserver(Action<string> onNext, Action onCompleted)
      {
        _onNext = onNext;
        _onCompleted = onCompleted;
      }


      public void OnCompleted()
      {
        // Remove this observable
        // clean up subscriptions map in the future
        Xamarin.Forms.Device.BeginInvokeOnMainThread (() => {
          _onCompleted();
        });

      }

      public void OnError(Exception error)
      {
        // ...
      }

      public void OnNext(string value)
      {
        Xamarin.Forms.Device.BeginInvokeOnMainThread (() => {
          _onNext(value);
        });
      }
    }

    public string FormatJson<T> (T value)
    {
      return JsonConvert.SerializeObject (value, Formatting.None, Settings);
    }

    public struct Named {
      public string Name { get; set;}
    }

    private string[] Serialize1<T>(object value)
    {
      return new[] { FormatJson (value) };
    }
  }

}

