using System;
using System.Threading.Tasks;
using System.Reactive.Subjects;

namespace XamarinWebSample
{
  [HasWebCallbacks]
  public class SampleService
  {
    Subject<object> callCounts;
    int callCount;
    public SampleService() 
    {
      callCounts = new Subject<object> ();
    }

    [WebCallback]
    public async Task<object> Square(int num)
    {
      callCounts.OnNext (callCount++);
      return num * num;
    }
    [WebEvent]
    public async Task<IObservable<object>> CallCount()
    {
      return callCounts;
    }

  }
}

