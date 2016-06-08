using System;
using Xamarin.Forms;

namespace XamarinWebSample
{
  public class MainPage : ContentPage
  {
      
    public MainPage (SampleWebView webView)
    {
      Content = new StackLayout {
        Padding = new Thickness(0,20,0,0),
        VerticalOptions = LayoutOptions.FillAndExpand,
        BackgroundColor = Color.FromHex("FFFFFF"),
        Children = {
          webView,
        }
      };
//      Content = webView;
    }
  }
}
