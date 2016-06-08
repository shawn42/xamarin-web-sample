using System;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using SimpleInjector;

namespace XamarinWebSample.Droid
{
  [Activity (Label = "XamarinWebSample.Droid", Icon = "@drawable/icon", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
  public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsApplicationActivity
  {
    protected override void OnCreate (Bundle bundle)
    {
      base.OnCreate (bundle);

      global::Xamarin.Forms.Forms.Init (this, bundle);
      Container container = new Container ();

      App app = new App (container);

      app.Setup ();
      LoadApplication (app);

    }
  }
}

