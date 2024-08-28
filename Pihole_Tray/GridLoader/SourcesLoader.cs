using System.Windows.Controls;
using System.Windows;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Windows.Media;

public class SourceType
{
    public string Device { get; set; }
    public string IPAddress { get; set; }
    public string RequestCount { get; set; }
    public Brush IpBrush { get; set; }
    public Brush BlueBrush { get; set; }

}

public class SourcesLoader
{
    public async Task LoadAsync(ItemsControl itemsControl, dynamic json, bool isV6, Brush ipBrush, Brush blueBrush)
    {
        var items = new List<SourceType>();
        await Task.Run(() =>
        {
            try
            {
              
                if (isV6)
                {
                    foreach (var item in (JArray)json)
                    {
                        items.Add(new SourceType
                        {
                            Device = (string)item["name"],
                            IPAddress = (string)item["ip"],
                            RequestCount = (string)item["count"]
                        });
                    }
                }
                else
                {
                    foreach (var item in (JObject)json)
                    {
                        var _ = item.Key.ToString().Split('|');

                        items.Add(new SourceType
                        {
                            Device = _[0],
                            IPAddress = _[1],
                            RequestCount = item.Value.ToString(),
                            IpBrush = ipBrush,
                            BlueBrush = blueBrush
                        });
                    }
                }


                Application.Current.Dispatcher.Invoke(() =>
                {
                    itemsControl.ItemsSource = items;
                });

            }
            catch (NullReferenceException)
            {
                items.Add(new SourceType
                {
                    Device = "Object is null"
                });
                Application.Current.Dispatcher.Invoke(() =>
                {
                    itemsControl.ItemsSource = items;
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                items.Add(new SourceType
                {
                    Device = e.Message
                });
                Application.Current.Dispatcher.Invoke(() =>
                {
                    itemsControl.ItemsSource = items;
                });
            }
        });
    }
}

