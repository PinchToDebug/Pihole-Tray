using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Dynamic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Windows.Documents;
using System.Windows.Input;
using System.Xml.Linq;
using static System.Net.WebRequestMethods;

public class Instance {

    private readonly HttpClient httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromMilliseconds(700)
    };

    private dynamic statusCheck = new ExpandoObject();
    public  string? API_KEY { get; set; }
    public  string? Name { get; set; }
    public  string? Address { get; set; }
    public  int? Order { get; set; }
    public  bool? IsDefault { get; set; }
    public  bool? isV6 { get; set; }
    public string? Password { get; set; }
    public string? SID { get; set; }


    public async Task<int> Status()
    {
        Debug.WriteLine("Status checking");
        Debug.WriteLine(Address);

        try
        {
            HttpResponseMessage response;
            if (isV6 == true)
            {
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("sid", SID);

                response = await httpClient.GetAsync($"{Address}/dns/blocking");
                statusCheck = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
            }
            else
            {
                response = await httpClient.GetAsync($"{Address}?summary&auth={API_KEY}");
                statusCheck = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
            }

            if (!response.IsSuccessStatusCode && isV6 == false)
            {
                return (int)1;
            }


            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("sid", SID);


            if (isV6 == true)
            {
                Debug.WriteLine($"Status from response: {Name},{statusCheck.blocking}");
                if (response.IsSuccessStatusCode)
                {
                    if (statusCheck.blocking == "enabled")
                    {
                        return 0;
                    }
                    else if (statusCheck.blocking == "disabled")
                    {
                        return 1;
                    }
                }
                else
                {
                    await Login(Password, httpClient);
                    httpClient.DefaultRequestHeaders.Clear();
                    httpClient.DefaultRequestHeaders.Add("sid", SID);
                    response = await httpClient.GetAsync($"{Address}/dns/blocking");
                    statusCheck = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
                    if (response.IsSuccessStatusCode)
                    {
                       
                        if (statusCheck.blocking == "enabled")
                        {
                            return 0;
                        }
                        else if (statusCheck.blocking == "disabled")
                        {
                            return 1;
                        }
                    }
                    else
                    {
                        return 2;
                    }
                }
            }
            else
            {
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
        }

        catch (Exception e)
        {
            Debug.WriteLine($"Status check error: {e.Message}");
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


    public async Task Login(string password, HttpClient client)
    {
        try
        {

            Debug.WriteLine($"\n--------------------------------");
            Debug.WriteLine($"LOGIN: SID Checking: {SID}");

            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("User-Agent", $"Pi-hole tray");
            client.DefaultRequestHeaders.Add("sid", SID);

            var validityResponse = await client.GetAsync($"{Address}/stats/summary");
            string content = await validityResponse.Content.ReadAsStringAsync();

            if (content.StartsWith("{\"error"))
            {
                Debug.WriteLine("LOGIN: Getting new SID");
                var loginResponse = await client.PostAsJsonAsync($"{Address}/auth", new { password });
                var content2 = await loginResponse.Content.ReadAsStringAsync();

                Debug.WriteLine("LOGIN: Got new SID + "+ content2);

                dynamic result = JsonConvert.DeserializeObject<dynamic>(content2)!;
                SID = result.session.sid;
                Debug.WriteLine("LOGIN: new sid: " + SID);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("User-Agent", $"Pi-hole tray");
                client.DefaultRequestHeaders.Add("sid", SID);
                await  Login(Password, client);
            }
            else
            {
                Debug.WriteLine("LOGIN: Successful login: " + SID);
            }

            Debug.WriteLine($"--------------------------------\n");
        }
        catch (Exception e)
        {
            Debug.WriteLine($"LOGIN: ERROR: {e.Message}");
        }
    }

    public string GetKeyLocation()
    {
        return @$"SOFTWARE\Pihole_Tray\Instances\{Name}";
    }


}


public class InstanceStorage {

    public List<Instance> Instances  = new List<Instance>();
    public void WroteOverInstanceToKey(Instance instance,string oldKey)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@$"SOFTWARE\Pihole_Tray\Instances\{instance.Name}"))
            {
                key.SetValue("API_KEY", instance.API_KEY ?? "");
                key.SetValue("Name", instance.Name ?? "");
                key.SetValue("Address", instance.Address ?? "");
                key.SetValue("Order", instance.Order!);
                key.SetValue("IsDefault", instance.IsDefault ?? false);
                key.SetValue("isV6", instance.isV6 ?? false);
                key.SetValue("Password", instance.Password ?? "");
                key.SetValue("SID", instance.SID ?? "");
            }
            Registry.CurrentUser.DeleteSubKey(@$"SOFTWARE\Pihole_Tray\Instances\{oldKey}", throwOnMissingSubKey: false);
        }
        catch { }
    }
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
                    key.SetValue("IsDefault", instance.IsDefault ?? false);
                    key.SetValue("isV6", instance.isV6 ?? false);
                    key.SetValue("Password", instance.Password ?? "");
                    key.SetValue("SID", instance.SID ?? "");
            }
        }
        catch { }
    }
    public Instance? DefaultInstance()
    {
        foreach (var instance in Instances)
        {
            if ((bool)instance.IsDefault == true)
            {
                Debug.WriteLine($"Found default instance: {instance.Name}");
                return instance;
            }
        }
        Debug.WriteLine("There's no default instance");
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
                                    object value = instanceKey.GetValue(valueName)!;

                                    switch (valueName)
                                    {
                                        case "API_KEY":
                                            temp.API_KEY = value.ToString();
                                            Debug.WriteLine($"API_KEY added\t{temp.API_KEY}");
                                            break;

                                        case "Name":
                                            temp.Name = (string)value;
                                            Debug.WriteLine($"Name added\t{temp.Name}");
                                            break;

                                        case "Address":
                                            temp.Address = (string)value;
                                            Debug.WriteLine($"Address added\t{temp.Address}");
                                            break;

                                        case "Order":
                                            temp.Order = Int32.Parse(value.ToString());
                                            Debug.WriteLine($"Order added\t{temp.Order.ToString()}");
                                            break;

                                        case "IsDefault":
                                            temp.IsDefault = bool.Parse(value.ToString());
                                            Debug.WriteLine($"IsDefault added\t{temp.IsDefault}");
                                            break;
                                        case "isV6":
                                            temp.isV6 = bool.Parse(value.ToString());
                                            Debug.WriteLine($"isV6 added\t{temp.isV6}");
                                            break;
                                        case "Password":
                                            temp.Password = (string)value;
                                            Debug.WriteLine($"Password added\t{temp.Password}");
                                            break;
                                        case "SID":
                                            temp.SID = (string)value;
                                            Debug.WriteLine($"SID added\t{temp.SID}");
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                Instances.Add(temp);
                            }
                            else
                            {
                                Debug.WriteLine("instance not valid");
                            }
                        }
                    }
                }
            }
        }
        catch { }
    }


}
