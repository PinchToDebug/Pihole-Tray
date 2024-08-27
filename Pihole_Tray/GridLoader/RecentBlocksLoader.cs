using System.Windows.Controls;
using System.Windows;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

public class QueriesType { 
    public string Time { get; set; }
    public string DomainName { get; set; }
}

public class RecentBlocksLoader
{


    public async Task LoadAsync(ItemsControl itemsControl, JArray arr, bool isV6)
    {
        await Task.Run(() =>
        {
            var items = new List<QueriesType>();
            if (arr == null)
            {

                items.Add(new QueriesType
                {
                    Time = "Array is null."
                });
                Application.Current.Dispatcher.Invoke(() =>
                {
                    itemsControl.ItemsSource = items;
                });
                return;
            }
            try
            {
               
                if (isV6)
                {
                    foreach (var item in arr)
                    {
                        var time = DateTimeOffset.FromUnixTimeSeconds((long)item["time"]).ToLocalTime().ToString("HH:mm:ss");
                        var domainName = item["domain"].ToString();

                        items.Add(new QueriesType
                        {
                            Time = time,
                            DomainName = domainName
                        });
                    }
                }
                else
                {
                    foreach (var item in arr)
                    {
                        if (item[4].ToString() != "1" && item[4].ToString() != "4") continue; // skipping if not blocked
                        var time = DateTimeOffset.FromUnixTimeSeconds((long)item[0]).ToLocalTime().ToString("HH:mm:ss");
                        var domainName = item[2].ToString();

                        items.Add(new QueriesType
                        {
                            Time = time,
                            DomainName = domainName
                        });
                    }
                }



                Application.Current.Dispatcher.Invoke(() =>
                {
                    itemsControl.ItemsSource = items;
                });
            }

            catch (Exception e)
            {
                items.Add(new QueriesType
                {
                    Time = e.Message
                });
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Debug.WriteLine(e.Message);
                    itemsControl.ItemsSource = items;
                });
            }

        });
    }
}
