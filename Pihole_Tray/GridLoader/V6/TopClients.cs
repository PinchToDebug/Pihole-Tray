using System.Windows.Controls;
using System.Windows;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

public class ClienType
{
    public string Device { get; set; }
    public string IPAddress { get; set; }
    public string RequestCount { get; set; }
}

public class TopClients
{
    public async Task LoadAsync(ItemsControl itemsControl, JArray arr)
    {
        var items = new List<ClienType>();

        try
        {
            foreach (var item in arr)
            {

                items.Add(new ClienType
                {
                    Device = (string)item["name"],            
                    IPAddress = (string)item["ip"],
                    RequestCount = (string)item["count"]
                });

            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                itemsControl.ItemsSource = items;
            });
        }
        catch (NullReferenceException)
        {
            items.Add(new ClienType
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
            items.Add(new ClienType
            {
                Device = e.Message
            });
            Application.Current.Dispatcher.Invoke(() =>
            {             
                itemsControl.ItemsSource = items;
            });
        }

    }


}

