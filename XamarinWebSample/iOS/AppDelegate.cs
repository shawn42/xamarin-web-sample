using System;
using System.Collections.Generic;
using System.Linq;

using Foundation;
using UIKit;
using SimpleInjector;

namespace XamarinWebSample.iOS
{
  [Register ("AppDelegate")]
  public partial class AppDelegate : global::Xamarin.Forms.Platform.iOS.FormsApplicationDelegate
  {
    public override bool FinishedLaunching (UIApplication app, NSDictionary options)
    {
      global::Xamarin.Forms.Forms.Init ();

      XLabs.Forms.Controls.HybridWebViewRenderer.CopyBundleDirectory("www");
      Container container = new Container ();

      // can load iOS specific things into container
      App ourApp = new App (container);
      ourApp.Setup ();
      LoadApplication (ourApp);

      return base.FinishedLaunching (app, options);
    }
  }
}

