using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Dynamic;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;

public class Instance {
    private readonly HttpClient httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromMilliseconds(300)
    };
    private dynamic statusCheck = new ExpandoObject();
    public  string? API_KEY { get; set; }
    public  string? Name { get; set; }
    public  string? Address { get; set; }
    public  int? Order { get; set; }
    public  bool? IsDefault { get; set; }
    public Instance() {

    }
    public async Task<int> Status()
    {
       // await Task.Delay(1000);
        Debug.WriteLine("status checking");
        Debug.WriteLine(Address);

        // Ensure httpClient.Timeout is set appropriately


        try
        {
            var response = await httpClient.GetAsync(Address + "?summary&auth=" + API_KEY);

            if (!response.IsSuccessStatusCode)
            {
                return (int)1;
            }


            statusCheck = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());

            Debug.WriteLine($"Status from response: {statusCheck.status}");
            if (statusCheck.status == "enabled")
            {
                return 0;
            }
            else if (statusCheck.status == "disabled")
            {
                return 1;
            }
        }

        catch 
        {
            using (Ping ping = new Ping())
            {
                try
                {
                    PingReply reply = ping.Send(new Uri(Address).AbsoluteUri, 300);

                    if (reply.Status == IPStatus.Success)
                    {
                        return 2;
                    }
                    else
                    {
                        return -1;
                    }
                }
                catch
                {
                    return -1;
                }
            }
           
        }
        return -1;
    }


    public string GetKeyLocation()
    {
        return @$"SOFTWARE\Pihole_Tray\Instances\{Name}";
    }


}


public class InstanceStorage {
    public List<Instance> Instances  = new List<Instance>();
  
    
    public void WriteInstanceToKey(Instance instance)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(instance.GetKeyLocation()))
            {
                
                    key.SetValue("API_KEY", instance.API_KEY ?? "");
                    key.SetValue("Name", instance.Name ?? "");
                    key.SetValue("Address", instance.Address ?? "");
                    key.SetValue("Order", instance.Order!) ;
                    key.SetValue("IsDefault", instance.IsDefault!);
            }
        }
        catch { }
    }
    public Instance? DefaultInstance()
    {

        foreach (var instance in Instances)
        {
            string isDefault = instance.IsDefault.ToString();
            if (bool.TryParse(isDefault, out bool value))
            {
             //   Debug.WriteLine($"found default instance: {instance.Name}");
               // return instance;
            }
        }
        foreach (var instance in Instances)
        {
            if ((bool)instance.IsDefault == true)
            {
                Debug.WriteLine($"found default instance: {instance.Name}");

                return instance;
            }
        }
        Debug.WriteLine("no default instance");
        return null;
    }

  
    public void FillUp()
    {
        Debug.WriteLine("FILLING");
        try
        {
            using (RegistryKey instancesKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Pihole_Tray\Instances")!)
            {
                if (instancesKey != null)
                {
                    string[] instanceNames = instancesKey.GetSubKeyNames();
                    Debug.WriteLine($"\n");

                    foreach (var item in instanceNames)
                    {
                        Debug.WriteLine($"subkeyname: {item}");
                    }
                   
                    
                    
                    
                    foreach (string instance in instanceNames) 
                    {
                       
                        Debug.WriteLine($"instanceNames: {instance}");


                        using (RegistryKey instanceKey = Registry.CurrentUser.OpenSubKey($@"SOFTWARE\Pihole_Tray\Instances\{instance}")!)
                        {
                            Debug.WriteLine("valid");
                            if (instanceKey != null)
                            {
                                Instance temp = new Instance();
                                Debug.WriteLine("valied 2");
                                // Read all values under the current subkey
                                foreach (var valueName in instanceKey.GetValueNames())
                                {
                                 //   Debug.WriteLine($"values:::: {valueName}");
                                    object value = instanceKey.GetValue(valueName)!;

                                    switch (valueName)
                                    {
                                        case "API_KEY":
                                            temp.API_KEY = (string)value;
                                            Debug.WriteLine($"API_KEY added\t{valueName}");
                                            break;

                                        case "Name":
                                            temp.Name = (string)value;
                                            Debug.WriteLine($"Name added\t{valueName}");
                                            break;

                                        case "Address":
                                            temp.Address = (string)value;
                                            Debug.WriteLine($"Address added\t{valueName}");
                                            break;

                                        case "Order":
                                            temp.Order = Int32.Parse(value.ToString());
                                            Debug.WriteLine($"Order added\t{valueName}");
                                            break;
                                         
                                        case "IsDefault": 
                                            temp.IsDefault = bool.Parse( value.ToString());
                                            Debug.WriteLine($"IsDefault added\t{valueName}");

                                            break;
                                        
                                        default:
                                            Debug.WriteLine($"wtf: {valueName}");

                                            break;
                                    }
                                }
                                Instances.Add(temp);
                            }
                            else
                            {
                                Debug.WriteLine("instance not valid fuck");

                            }
                        }
                    }
                    Debug.WriteLine($"count of i: {Instances.Count}");
                    Debug.WriteLine($"count of x: {Instances.Count()}");
                }
            }
        }
        catch { }
    }

















}

