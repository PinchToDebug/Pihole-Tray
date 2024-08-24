using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Dynamic;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shell;
using Wpf.Ui.Controls;
using Wpf.Ui.Interop;
using static System.Net.Mime.MediaTypeNames;
using Button = System.Windows.Controls.Button;
using MenuItem = Wpf.Ui.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace Pihole_Tray
{

    public partial class MainWindow : FluentWindow
    {


        [SecurityCritical]
        [DllImport("dwmapi.dll", SetLastError = false, ExactSpelling = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, uint dwAttribute, IntPtr pvAttribute, int cbAttribute);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private readonly string apiUrl = "http://pi.hole/admin/api.php";
        private readonly string regKeyName = "Pihole_Tray";
        private string API_KEY;
        private bool isPinned = false;
        private bool coldRun = true;
        private bool startOnLogin;
        private bool canResize = true;
        private bool allInstanceShown = true;

        private bool isAnimating = false;
        private bool enterAnim = false;
        private bool leaveAnim = false;

        private CancellationTokenSource cancelToken;

        private InstanceStorage storage;
        private Instance selectedInstance;

        private SliderValues slider;

        private readonly HttpClient httpClient;
        private JObject topSources;
        private JArray queries_data;
        private JObject forward_destinations;
        private JObject querytypes;

        private Slider DisableSlider;
        private TextBlock DisableEnableButton;
        private ContextMenu DisableEnableContextMenu;
        private ContextMenu InstanceContextMenu;
        private CalcHeight CalcHeight;



        private void OnAnimationCompleted(object sender, EventArgs e)
        {
            Debug.WriteLine("CAN RESIZE now");
            canResize = true;

        }
        protected override void OnExtendsContentIntoTitleBarChanged(bool oldValue, bool newValue)
        {
            SetCurrentValue(WindowStyleProperty, WindowStyle);
            // https://github.com/lepoco/wpfui/issues/576
            // this also fixes the thick shadow
            WindowChrome.SetWindowChrome(
                this,
                new WindowChrome
                {
                    CaptionHeight = 0,
                    CornerRadius = default,
                    GlassFrameThickness = new Thickness(-1),
                    ResizeBorderThickness = ResizeMode == ResizeMode.NoResize ? default : new Thickness(4),
                    UseAeroCaptionButtons = false,
                }
            );

            _ = UnsafeNativeMethods.RemoveWindowTitlebarContents(this);
        }

        public MainWindow()
        {
            this.Visibility = Visibility.Visible;
            this.WindowStyle = WindowStyle.None;

            InitializeComponent();
            storage = new InstanceStorage();
            slider = new SliderValues();
            CalcHeight = new CalcHeight();
            var mouseEnterStoryboard = (Storyboard)BlockHistoryCard.Resources["MouseEnterStoryboard"];
            var mouseLeaveStoryboard = (Storyboard)BlockHistoryCard.Resources["MouseLeaveStoryboard"];

            if (mouseEnterStoryboard != null)
            {
                mouseEnterStoryboard.Completed += OnAnimationCompleted!;
            }
            if (mouseLeaveStoryboard != null)
            {
                mouseLeaveStoryboard.Completed += OnAnimationCompleted!;
            }


            Default_StackPanel.Visibility = Visibility.Visible;
            Info_StackPanel.Visibility = Visibility.Hidden;

            if (KeyExistsRoot("RecentBlocksTS")) RecentBlocksTS.IsChecked = (bool)ReadKeyValueRoot("RecentBlocksTS"); HideShowElementPairs(RecentBlockLBL, BlockHistoryCard, RecentBlocksTS);
            if (KeyExistsRoot("QueryTS")) QueryTS.IsChecked = (bool)ReadKeyValueRoot("QueryTS"); HideShowElementPairs(QueryLBL, QueryCard, QueryTS); Debug.WriteLine(" ");
            if (KeyExistsRoot("SourcesTS")) SourcesTS.IsChecked = (bool)ReadKeyValueRoot("SourcesTS"); HideShowElementPairs(SourcesLBL, SourcesCard, SourcesTS);
            if (KeyExistsRoot("ForwardDestinationsTS")) ForwardDestinationsTS.IsChecked = (bool)ReadKeyValueRoot("ForwardDestinationsTS"); HideShowElementPairs(ForwardDestinationsLBL, ForwardDestinationsCard, ForwardDestinationsTS);
            if (KeyExistsRoot("Background"))
            {
                switch ((string)ReadKeyValueRoot("Background"))
                {
                    case "Mica":
                        MicaBG.IsChecked = true;
                        this.WindowBackdropType = WindowBackdropType.Mica;
                        MainGrid.Background = new BrushConverter().ConvertFrom("#0CFFFFFF") as Brush;
                        break;
                    case "Acrylic":
                        AcrylicBG.IsChecked = true;
                        this.WindowBackdropType = WindowBackdropType.Acrylic;
                        MainGrid.Background = new BrushConverter().ConvertFrom("#B2101010") as Brush;
                        break;
                    case "None":
                        NoneBG.IsChecked = true;
                        this.WindowBackdropType = WindowBackdropType.None;
                        MainGrid.Background = new BrushConverter().ConvertFrom("#B2101010") as Brush;
                        break;
                }
            }







            if (KeyExistsRoot("startOnLogin")) startOnLogin = (bool)ReadKeyValueRoot("startOnLogin");

            Autorun_Button.IsChecked = startOnLogin;

            this.Height = Info_StackPanel.ActualHeight + Info_StackPanel.Margin.Bottom + Info_StackPanel.Margin.Top;
            this.Left = (int)SystemParameters.PrimaryScreenWidth - this.Width - 12;
            this.Top = (int)SystemParameters.WorkArea.Bottom - this.Height - 12;
            if (Default_StackPanel.Visibility == Visibility.Visible)
            {
                this.Height = 500;
                //  this.Top = (int)SystemParameters.WorkArea.Bottom - Default_StackPanel.Height;
                Debug.WriteLine($"{(int)SystemParameters.PrimaryScreenWidth} {this.Width}");
                this.Left = (int)SystemParameters.PrimaryScreenWidth - this.Width - 12;
                this.Top = (int)SystemParameters.WorkArea.Bottom - this.Height - 12;
                Debug.WriteLine("OKKK " + Default_StackPanel.Height.ToString());
            }
            Debug.WriteLine(this.Height);
            httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(2000)
            };



            storage.FillUp();
            Debug.WriteLine($"da count::: {storage.Instances.Count}");
            // TODO: hangs here?

            if (storage.Instances.Count > 0)
            {
                // defaultInstance = storage.DefaultInstance();
                Debug.WriteLine("Using default API_KEY");
                API_KEY = storage.DefaultInstance()!.API_KEY ?? "";
                // ApiTB.Text = API_KEY;
                // this.Top = (int)SystemParameters.PrimaryScreenHeight;
                if (cancelToken != null)
                {
                    cancelToken.Cancel();
                    ClearElements();
                }
                cancelToken = new CancellationTokenSource();
                selectedInstance = storage.DefaultInstance();

                UpdateInfo(selectedInstance, cancelToken.Token);
            }

        }


        private void OnAnimationCompleted()
        {
            canResize = true;
        }
        private void OnAnimationStarted(Storyboard storyboard)
        {
            canResize = false;
        }

        private void StartMouseEnterAnimation()
        {
            var storyboard = (Storyboard)BlockHistoryCard.Resources["MouseEnterStoryboard"];
            if (storyboard != null)
            {
                OnAnimationStarted(storyboard);
                storyboard.Begin();
            }
        }

        private void StartMouseLeaveAnimation()
        {
            var storyboard = (Storyboard)BlockHistoryCard.Resources["MouseLeaveStoryboard"];
            if (storyboard != null)
            {
                OnAnimationStarted(storyboard);
                storyboard.Begin();
            }
        }
        private async void ApiSaveBTN_Click(object sender, RoutedEventArgs e)
        {
            bool setDefault = (bool)setDefaultTS.IsChecked;
            if (storage.Instances.Count == 0)
            {
                Debug.WriteLine("Couldn't find any instance");
                setDefault = true;
            }
            Debug.WriteLine($"tf: {storage.Instances.Count == 0}");
            Instance temp = new Instance
            {
                API_KEY = ApiTB.Text,
                Name = NameTB.Text,
                Address = AddressTB.Text,
                Order = storage.Instances.Count + 1,
                IsDefault = (bool)setDefaultTS.IsChecked ? true : setDefault,
            };



            stopUpdatingInfo = false;
            writeOnce = true;

            if (cancelToken != null)
            {
                cancelToken.Cancel();
                ClearElements();

            }
            cancelToken = new CancellationTokenSource();
            ClearElements();
            UpdateInfo(temp, cancelToken.Token);

        }

        private bool stopUpdatingInfo = false;

        bool writeOnce = true;
        private async void UpdateInfo(Instance instance, CancellationToken token)
        {
            try
            {
                if (coldRun) this.Top = (int)SystemParameters.PrimaryScreenHeight;

                selectedInstance = instance;
               
                if (!string.IsNullOrEmpty(ApiTB.Text))
                {
                    Debug.WriteLine("using key from TB");
                    //  WriteToRegistry("startOnLogin", Autorun_Button.IsChecked);
                }

                var contentDialog = new ContentDialog(RootContentDialogPresenter);

                try
                {

                    Debug.WriteLine($"this is address1: {instance.Address}");
                    bool pingFailed = false;
                    try
                    {
                        Debug.WriteLine(instance.Address);
                        Debug.WriteLine(new Uri(instance.Address).AbsoluteUri);
                        Debug.WriteLine(new Uri(instance.Address).Host);
                        Debug.WriteLine(new Uri(instance.Address).AbsolutePath);
                        Debug.WriteLine(new Uri(instance.Address).DnsSafeHost);
                        dynamic _ = new ExpandoObject();

                        _ = JsonConvert.DeserializeObject<dynamic>(await httpClient.GetStringAsync(instance.Address + "?summary&auth=" + instance.API_KEY))!;

                        DnsQueryTB.Text = DnsQueryTB.Text = _.dns_queries_all_types;

                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("ERROR 1: " + ex.Message);

                        using (Ping ping = new Ping())
                        {
                            try
                            {

                                PingReply reply = ping.Send(new Uri(instance.Address).AbsoluteUri, 2000);

                                if (reply.Status != IPStatus.Success)
                                {
                                    contentDialog.SetCurrentValue(ContentDialog.TitleProperty, $"Error");
                                    contentDialog.SetCurrentValue(ContentControl.ContentProperty, $"{reply.Status}");
                                }
                            }
                            catch
                            {
                                Debug.WriteLine("ERROR 2: " + ex.Message);

                                pingFailed = true;
                                contentDialog.SetCurrentValue(ContentDialog.TitleProperty, $"Couldn't reach host");
                                contentDialog.SetCurrentValue(ContentControl.ContentProperty, "Maybe DNS is not configured properly.");
                                if (KeyExists("API_KEY", instance))
                                {
                                    LoginBTN.Visibility = Visibility.Visible;

                                }
                            }
                        }
                        if (!pingFailed)
                        {
                            contentDialog.SetCurrentValue(ContentDialog.TitleProperty, "Error");
                            contentDialog.SetCurrentValue(ContentControl.ContentProperty, ex.Message);
                        }

                        Debug.WriteLine($"ERROR: {ex.Message}");
                        this.Top = (int)SystemParameters.WorkArea.Bottom - this.Height - 12;
                       await contentDialog.ShowAsync();

                       return;
                    }


                }
                catch (Exception ex)
                {

                    Debug.WriteLine($"Request error: {ex.Message} \n");
                    return;
                }

                Default_StackPanel.Visibility = Visibility.Hidden;
                Info_StackPanel.Visibility = Visibility.Visible;
                Debug.WriteLine("Connected successfully!");


                dynamic summary = new ExpandoObject();
                dynamic getAllQueries = new ExpandoObject();
                dynamic getQuerySources = new ExpandoObject();
                dynamic getForwardDestinations = new ExpandoObject();
                dynamic getQueryTypes = new ExpandoObject();


                if (coldRun)
                {
                    storage.WriteInstanceToKey(instance);
                    writeOnce = false;
                }

                bool showEffect = true;
                bool blurCard = true;


                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    // Debug.WriteLine($"api_i: {instance.API_KEY}");
                    if (storage.Instances.Count > 1)
                    {
                        CurrentNameTB.Text = $"{instance.Name}:";
                    }
                    else
                    {
                        CurrentNameTB.Text = "Status:";
                    }
                    //if (this.Visibility != Visibility.Visible && !coldRun)
                    //{
                    //   await Task.Delay(100);

                    //    continue;
                    //}
                    if (this.Visibility != Visibility.Visible)
                    {
                        Debug.WriteLine("GOT HIDDEN");
                        return;
                    }
                    var tasks = new List<Task>();

                    while (isAnimating)
                    {
                        await Task.Delay(10);

                        continue;
                    }
                    if (coldRun)
                    {
                        this.Visibility = Visibility.Hidden;
                    }

                    try
                    {
                        await Task.Delay(50);
                        summary = JsonConvert.DeserializeObject<dynamic>(await httpClient.GetStringAsync(instance.Address + "?summary&auth=" + instance.API_KEY))!;
                        await Task.Delay(50);
                        if ((bool)RecentBlocksTS.IsChecked!)
                        {
                            getAllQueries = JsonConvert.DeserializeObject<dynamic>(await httpClient.GetStringAsync(instance.Address + "?getAllQueries=250&auth=" + instance.API_KEY))!;
                            var queriesData = (JArray)getAllQueries.data;
                            queries_data = new JArray(queriesData.Reverse());
                            await Task.Delay(50);
                        }
                        if ((bool)SourcesTS.IsChecked!)
                        {
                            getQuerySources = JsonConvert.DeserializeObject<dynamic>(await httpClient.GetStringAsync(instance.Address + "?getQuerySources&auth=" + instance.API_KEY))!;
                            topSources = (JObject)getQuerySources.top_sources;
                            await Task.Delay(50);

                        }
                        if ((bool)ForwardDestinationsTS.IsChecked!)
                        {
                            getForwardDestinations = JsonConvert.DeserializeObject<dynamic>(await httpClient.GetStringAsync(instance.Address + "?getForwardDestinations&auth=" + instance.API_KEY))!;
                            forward_destinations = (JObject)getForwardDestinations.forward_destinations;
                            await Task.Delay(50);

                        }
                        if ((bool)QueryTS.IsChecked!)
                        {
                            getQueryTypes = JsonConvert.DeserializeObject<dynamic>(await httpClient.GetStringAsync(instance.Address + "?getQueryTypes&auth=" + instance.API_KEY))!;
                            querytypes = (JObject)getQueryTypes.querytypes;
                            await Task.Delay(50);
                        }

                        DnsQueryTB.Text = summary.dns_queries_all_types;

                        ContentGrid.Effect = null;
                        showEffect = true;
                        LostConnectionGrid.Visibility = Visibility.Hidden;
                    }
                    catch (TaskCanceledException to) {
                        Debug.WriteLine("ERROR 3: " + to.Message);
                        if (showEffect)
                        {
                            ContentGrid.Effect = new BlurEffect
                            {
                                Radius = 20,
                                RenderingBias = RenderingBias.Quality,
                                KernelType = KernelType.Gaussian
                            };
                            showEffect = false;
                            LostConnectionGrid.Visibility = Visibility.Visible;
                            LostConnectionTB.Text = "lost connection, trying to reconnect";
                        }
                        continue;

                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("ERROR 3: " + e.Message);
                        if (showEffect)
                        {
                            ContentGrid.Effect = new BlurEffect
                            {
                                Radius = 20,
                                RenderingBias = RenderingBias.Quality,
                                KernelType = KernelType.Gaussian
                            };
                            showEffect = false;
                            LostConnectionGrid.Visibility = Visibility.Visible;
                            LostConnectionTB.Text = e.Message;
                        }
                        continue;

                    }


                    while (isAnimating)
                    {
                        await Task.Delay(15, token);
                        continue;
                    }

                    #region basic1, basic 2
                    AdsBlockedTB.Text = summary.ads_blocked_today;
                    GravityTB.Text = $"{(summary.gravity_last_updated.relative.days > 0 ? summary.gravity_last_updated.relative.days + "d" : string.Empty)} " + $"{(summary.gravity_last_updated.relative.hours > 0 ? summary.gravity_last_updated.relative.hours + "h" : string.Empty)} " + $"{(summary.gravity_last_updated.relative.minutes > 0 ? summary.gravity_last_updated.relative.minutes + "m" : string.Empty)}".Replace("  ", " ");
                    DomainsBlockedTB.Text = summary.domains_being_blocked;
                    StatusTB.Text = summary.status;

                    if (StatusTB.Text == "enabled") StatusTB.Foreground = new SolidColorBrush(Color.FromRgb(110, 245, 99));
                    else StatusTB.Foreground = new SolidColorBrush(Color.FromRgb(255, 73, 73));
                    #endregion


                    if ((bool)RecentBlocksTS.IsChecked) await new AllQueriesLoader().LoadAsync(BlockHistoryItemsControl, queries_data);
                    if ((bool)SourcesTS.IsChecked) await new QuerySourcesLoader().LoadAsync(SourcesItemsControl, topSources);
                    if ((bool)ForwardDestinationsTS.IsChecked) await new ForwardDestinationsLoader().LoadAsync(ForwardDestinationsGrid, forward_destinations);
                    if ((bool)QueryTS.IsChecked) await new QueryTypesLoader().LoadAsync(QueryTypesGrid, querytypes);
                    if (writeOnce && !coldRun)
                    {
                        Debug.Write("WROTE ONCE");
                        bool shouldWrite = true;
                        foreach (Instance i in storage.Instances)
                        {
                            if (i.Name == instance.Name)
                            {
                                Debug.WriteLine($"i: {i.Name}");
                                Debug.WriteLine($"s: {instance.Name}");

                                shouldWrite = false;
                            }
                        }
                        if (shouldWrite)
                        {

                            selectedInstance = instance;
                            if ((bool)setDefaultTS.IsChecked)
                            {
                                foreach (var i in storage.Instances)
                                {
                                    if (i.IsDefault == true)
                                    {
                                        i.IsDefault = false;
                                        storage.WriteInstanceToKey(i);
                                    }
                                }
                            }
                            storage.WriteInstanceToKey(instance);
                            storage.Instances.Add(instance);
                        }
                        writeOnce = false;
                    }

                    if (canResize /*&& !BlockHistoryCard.IsMouseOver*/)
                    {
                        Info_StackPanel.Height = CalcHeight.Calc(Info_StackPanel);

                        this.Height = CalcHeight.Calc(Info_StackPanel);
                        this.Left = (int)SystemParameters.PrimaryScreenWidth - this.Width - 12;

                        if (coldRun) this.Top = (int)SystemParameters.PrimaryScreenHeight;
                        else this.Top = (int)SystemParameters.WorkArea.Bottom - this.Height - 12;
                    }

               
                    if (coldRun)
                    {
                        coldRun = false;
                        
                       // this.Visibility = Visibility.Hidden;
                         await Task.Delay(50, token);
                        continue;
                    }
                    await Task.Delay(1000, token);

                }

            }
            catch (Exception)
            {
                //   throw;
            }

        }





        private async void OnMouseEnterBlockHistoryCard(object sender, MouseEventArgs e)
        {
            await Task.Delay(300);
            if (BlockHistoryCard.IsMouseOver && BlockHistoryItemsControl.Items.Count > 8)
            {
                BlockHistorySV.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;

                if (BlockHistoryCard != null)
                {
                    isAnimating = true;
                    enterAnim = true;
                    double targetHeight = this.ActualHeight - BlockHistoryCard.TransformToVisual(this).Transform(new Point(0, 0)).Y - 16;
                    var animation = new DoubleAnimation
                    {
                        To = targetHeight,
                        Duration = TimeSpan.FromSeconds(0.15)
                    };

                    animation.Completed += (s, e) =>
                    {
                        if (!leaveAnim)
                        {
                            isAnimating = false;
                        }
                        enterAnim = false;
                    };
                    BlockHistoryCard.BeginAnimation(FrameworkElement.HeightProperty, animation);
                }
            }
            else
            {
                isAnimating = false;
                enterAnim = false;
            }
        }
        private void OnMouseLeaveBlockHistoryCard(object sender, MouseEventArgs e)
        {
            if (BlockHistoryCard != null)
            {
                isAnimating = true;
                leaveAnim = true;
                var animation = new DoubleAnimation
                {
                    To = 161, // original height of blockHistoryCard
                    Duration = TimeSpan.FromSeconds(0.15)
                };

                animation.Completed += (s, e) =>
                {
                    if (!enterAnim)
                    {
                        isAnimating = false;
                        BlockHistorySV.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                    }

                    leaveAnim = false;
                };
                BlockHistoryCard.BeginAnimation(FrameworkElement.HeightProperty, animation);
                BlockHistorySV.ScrollToVerticalOffset(0);

            }
        }






        private static int CountProperties(dynamic obj)
        {
            return ((JObject)obj.forward_destinations).Count;
        }
        protected override void OnDeactivated(EventArgs e)
        {
            if (!isPinned)
            {
                this.Hide();
            }
        }

        private double GetTotalVisibleHeightWithPadding(Grid grid)
        {
            double totalHeight = 0;

            foreach (UIElement child in grid.Children)
            {
                if (child is FrameworkElement frameworkElement && frameworkElement.Visibility == Visibility.Visible)
                {

                    frameworkElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                    totalHeight += frameworkElement.DesiredSize.Height +
                                   frameworkElement.Margin.Top +
                                   frameworkElement.Margin.Bottom;
                }
            }
            return totalHeight;
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (storage.Instances.Count() > 0)
            {
                if (cancelToken != null)
                {
                    cancelToken.Cancel();
                }
                cancelToken = new CancellationTokenSource();
                if (selectedInstance != null)
                {
                    UpdateInfo(selectedInstance, cancelToken.Token);

                }
            }
            this.Show();
        }

        private void NotifyIcon_LeftClick(Wpf.Ui.Tray.Controls.NotifyIcon sender, RoutedEventArgs e)
        {
            base.OnActivated(e); this.Activate();
            if (!this.IsVisible || this.Top >= SystemParameters.PrimaryScreenHeight)
            {
                this.Show(); this.Activate();
                this.Top = (int)SystemParameters.WorkArea.Bottom - this.Height - 12;
            }
            this.Activate();
            this.Activate();
            if (storage.Instances.Count() > 0)
            {
                if (cancelToken != null)
                {
                    cancelToken.Cancel();
                }
                cancelToken = new CancellationTokenSource();
                UpdateInfo(selectedInstance, cancelToken.Token);
            }
            this.Activate();


        }



        private void fluentWindow_SourceInitialized(object sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int GWL_STYLE = -16;
            int WS_CAPTION = 0x00020000;
            int style = GetWindowLong(hwnd, GWL_STYLE);
            int newStyle = style & ~WS_CAPTION;

            SetWindowLong(hwnd, GWL_STYLE, style & ~WS_CAPTION); // removes the animations from the window


        }





        private void AddToAutoRun(string appName, string appPath)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true)!)
                {
                    key.SetValue(appName, appPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error adding to autorun: " + ex.Message);
            }
        }
        private void WriteToRegistry(string keyName, object value, Instance instance)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(instance.GetKeyLocation()))
                {
                    key.SetValue(keyName, value);
                }
            }
            catch { }

        }
        private void WriteToRegistryRoot(string keyName, object value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\{regKeyName}"))
                {
                    key.SetValue(keyName, value);
                }
            }
            catch { }
        }
        private void RemoveFromAutoRun(string appName)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true)!)
                {
                    key.DeleteValue(appName, false);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error removing from autorun: " + ex.Message);
            }
        }
        private object ReadKeyValue(string keyName, Instance instance)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(instance.GetKeyLocation())!)
                {
                    if (key != null)
                    {
                        object value = key.GetValue(keyName)!;
                        if (value != null)
                        {
                            if (value is bool)
                            {
                                Debug.WriteLine($"returned bool for {keyName}");
                                return (bool)value;
                            }
                            else if (bool.TryParse(value.ToString(), out bool boolValue))
                            {
                                Debug.WriteLine($"returned Parsed bool for {keyName}");
                                return boolValue;
                            }
                            else
                            {
                                Debug.WriteLine($"returned string for {keyName}");
                                return (string)value;
                            }

                        }
                    }
                }
                Debug.WriteLine($"couldn't return for {keyName}");
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading key: " + ex.Message);
                return false;
            }
        }
        private object ReadKeyValueRoot(string keyName)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey($"SOFTWARE\\{regKeyName}")!)
                {
                    if (key != null)
                    {
                        object value = key.GetValue(keyName)!;
                        if (value != null)
                        {
                            if (value is bool)
                            {
                                Debug.WriteLine($"returned bool for {keyName}");
                                return (bool)value;
                            }
                            else if (bool.TryParse(value.ToString(), out bool boolValue))
                            {
                                Debug.WriteLine($"returned Parsed bool for {keyName}");
                                return boolValue;
                            }
                            else
                            {
                                Debug.WriteLine($"returned string for {keyName}");
                                return (string)value;
                            }

                        }
                    }
                }
                Debug.WriteLine($"couldn't return for {keyName}");
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading key: " + ex.Message);
                return false;
            }
        }
        private bool KeyExists(string keyName, Instance instance)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(instance.GetKeyLocation())!)
                {
                    if (key != null)
                    {
                        if (key.GetValue(keyName) != null)
                        {
                            Debug.WriteLine($"exists: {keyName},{key.GetValue(keyName)}");

                            return true;
                        }
                    }
                }
                Debug.WriteLine($"doesnt exist: {keyName}");
                return false;
            }
            catch
            {
                Debug.WriteLine($"error opening HKCU\\SOFTWARE\\{regKeyName}, {keyName}");
                return false;
            }
        }

        private bool KeyExistsRoot(string keyName)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey($"SOFTWARE\\{regKeyName}")!)
                {
                    if (key != null)
                    {
                        if (key.GetValue(keyName) != null)
                        {
                            Debug.WriteLine($"exists: {keyName},{key.GetValue(keyName)}");

                            return true;
                        }
                    }
                }
                Debug.WriteLine($"doesnt exist: {keyName}");
                return false;
            }
            catch
            {
                Debug.WriteLine($"error opening HKCU\\SOFTWARE\\{regKeyName}, {keyName}");
                return false;
            }
        }




        private void ExitApp(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void Exit_Button_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Exit_Button.Foreground = new SolidColorBrush(Color.FromArgb(255, 254, 107, 107));

        }

        private void Exit_Button_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Exit_Button.Foreground = Brushes.White;
        }


        private void Autorun_Checked(object sender, RoutedEventArgs e)
        {
            if (Autorun_Button.IsChecked)
            {

                AddToAutoRun(regKeyName, Process.GetCurrentProcess().MainModule!.FileName);
            }
            else
            {

                RemoveFromAutoRun(regKeyName);
            }
            WriteToRegistryRoot("startOnLogin", Autorun_Button.IsChecked);
        }

        private void OpenInBrowser_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProcessStartInfo sInfo = new(new Uri("http://pi.hole").AbsoluteUri) { UseShellExecute = true };
                _ = Process.Start(sInfo);
            }
            catch
            {
            }
        }


        private void PinApp(object sender, RoutedEventArgs e)
        {
            if (!isPinned)
            {
                Pin_Button.Icon = new SymbolIcon(SymbolRegular.PinOff20);
                Pin_Button.Header = "Unpin App";
                isPinned = true;
                this.Activate();
                this.Show();
                this.Topmost = true;

                if (cancelToken != null)
                {
                    cancelToken.Cancel();

                }
                cancelToken = new CancellationTokenSource();
                if (selectedInstance != null)
                {
                    UpdateInfo(selectedInstance, cancelToken.Token);

                }
            }
            else
            {
                Pin_Button.Icon = new SymbolIcon(SymbolRegular.Pin20);
                isPinned = false;
                this.Topmost = false;
                Pin_Button.Header = "Pin App";
                this.Hide();
            }
        }

        private void LoginBTN_Click(object sender, RoutedEventArgs e)
        {
            if (cancelToken != null)
            {
                cancelToken.Cancel();
                ClearElements();
            }
            cancelToken = new CancellationTokenSource();

            UpdateInfo(selectedInstance, cancelToken.Token);
        }



        private void HideShowElementPairs(Label label, Card card, ToggleSwitch ts)
        {

            if (ts.IsChecked == true)
            {
                label.Visibility = Visibility.Visible;
                card.Visibility = Visibility.Visible;
                WriteToRegistryRoot(ts.Name, ts.IsChecked);
            }
            else
            {
                label.Visibility = Visibility.Collapsed;
                card.Visibility = Visibility.Collapsed;
                WriteToRegistryRoot(ts.Name, ts.IsChecked);
            }
            Resize();
        }



        private void Resize()
        {
            if (canResize && !BlockHistoryCard.IsMouseOver)
            {
                Info_StackPanel.Height = CalcHeight.Calc(Info_StackPanel);

                this.Height = CalcHeight.Calc(Info_StackPanel);
                this.Left = (int)SystemParameters.PrimaryScreenWidth - this.Width - 12;

                if (coldRun) this.Top = (int)SystemParameters.PrimaryScreenHeight;
                else this.Top = (int)SystemParameters.WorkArea.Bottom - this.Height - 12;
            }
        }


        private void RecentBlocksTS_Click(object sender, RoutedEventArgs e)
        {
            HideShowElementPairs(RecentBlockLBL, BlockHistoryCard, RecentBlocksTS);

        }
        private void SourcesTS_Click(object sender, RoutedEventArgs e)
        {
            HideShowElementPairs(SourcesLBL, SourcesCard, SourcesTS);

        }
        private void ForwardDestinationsTS_Click(object sender, RoutedEventArgs e)
        {
            HideShowElementPairs(ForwardDestinationsLBL, ForwardDestinationsCard, ForwardDestinationsTS);


        }
        private void QueryTS_Click(object sender, RoutedEventArgs e)
        {
            HideShowElementPairs(QueryLBL, QueryCard, QueryTS);

        }



        private void DisableSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Debug.WriteLine(DisableSlider.Value);

            if (slider.Values.TryGetValue((int)DisableSlider.Value, out var data))
            {
                DisableEnableButton.Text = data.Item1;
            }

        }

        private async void DisableButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("HEY");

            if (DisableSlider != null && slider.Values.TryGetValue((int)DisableSlider.Value, out var data) && StatusTB.Text == "enabled")
            {
                Debug.WriteLine($"Trying to disable");

                HttpResponseMessage response = await httpClient.GetAsync(selectedInstance.Address + $"?disable={data.Item2}&auth=" + selectedInstance.API_KEY);
                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Disabled for: {data.Item1}");
                }
                else
                {
                    Debug.WriteLine($"FAILED to disable");

                }
                DisableSlider.Value = 0;

            }
            else if (StatusTB.Text == "disabled")
            {
                Debug.WriteLine($"Trying to enable");
                HttpResponseMessage response = await httpClient.GetAsync(selectedInstance.Address + "?enable&auth=" + selectedInstance.API_KEY);
                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Enabled");
                }
                else
                {
                    Debug.WriteLine($"FAILED to enable");
                }
            }

            DisableEnableContextMenu.IsOpen = false;
        }


        private async void Instance_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // InstanceContextMenu.ClearValue();
            InstanceContextMenu = new ContextMenu();
            if (storage.Instances.Count > 1)
            {
                //TODO: implement view all
                goto Skip;
                Debug.WriteLine("da count: " + storage.Instances.Count);

                MenuItem allView = new MenuItem
                {
                    FontSize = 13,
                    IsEnabled = false,
                    FontWeight = FontWeights.Normal,
                    Header = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children = {
                          new TextBlock
                          {
                              Text = "\u25CF",
                              FontSize = 16,
                              Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                              Margin = new Thickness(0, 0, 7, 2),
                              VerticalAlignment = VerticalAlignment.Center
                          },
                          new TextBlock
                          {
                              Text = "View all",
                              VerticalAlignment = VerticalAlignment.Center
                          }
                    }
                    }
                };

                InstanceContextMenu.Items.Add(allView);
                InstanceContextMenu.Items.Add(new Separator());
            }
            
        Skip:


            foreach (Instance instance in storage.Instances)
            {
                MenuItem menuItem = new MenuItem
                {
                    FontSize = 13,
                    FontWeight = FontWeights.Normal,
                    Header = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children = {
                            new TextBlock
                            {
                                ToolTip = "",
                                Text = "\u25CF",
                                FontSize = 16,
                                Foreground =  new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                                Margin = new Thickness(0, 0, 7, 2),
                                VerticalAlignment = VerticalAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = instance.Name,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        }
                    }
                };
                Debug.WriteLine($"instance menu added:{instance.Name}");
                menuItem.Click += InstanceSelected_Click;
                InstanceContextMenu.Items.Add(menuItem);
            }


            MenuItem AddButton = new MenuItem
            {

                Header = new TextBlock
                {
                    Text = "\u002B",
                    FontSize = 18,
                    Margin = new Thickness(0, 0, 7, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                },

            };
            AddButton.Click += Addbutton_Click;
            if (storage.Instances.Count >1)
            {
                InstanceContextMenu.Items.Add(new Separator());

            }
            InstanceContextMenu.Items.Add(AddButton);
            InstanceContextMenu.IsOpen = true;

            foreach (var item in InstanceContextMenu.Items)
            {
                if (item is MenuItem menuItem && menuItem.Header is StackPanel sp)
                {

                    foreach (var instance in storage.Instances)
                    {

                        if ((sp.Children[1] as TextBlock).Text == instance.Name)
                        {
                            _ = Task.Run(() =>
                            {
                                int status = instance.Status().Result;
                                Dispatcher.Invoke(() =>
                                {
                                    SolidColorBrush brush = new SolidColorBrush();
                                    switch (status)
                                    {
                                        case 0: // Enabled
                                            brush = new SolidColorBrush(Color.FromRgb(70, 244, 64)); // Green
                                            break;

                                        case 1: // Disabled
                                            brush = new SolidColorBrush(Color.FromRgb(244, 64, 64)); // Red
                                            break;

                                        case 2: // Reachable but can't reach API
                                            brush = new SolidColorBrush(Color.FromRgb(64, 116, 244)); // Blue
                                            menuItem.Click -= InstanceSelected_Click;
                                            menuItem.ToolTip = "Address reachable, but API inaccessible.";
                                            menuItem.Cursor = Cursors.Help;
                                            break;

                                        case -1: // Unreachable
                                            brush = new SolidColorBrush(Color.FromRgb(244, 207, 64)); // Orange-ish
                                            menuItem.ToolTip = "Address unreachable.";
                                            menuItem.Click -= InstanceSelected_Click;
                                            menuItem.Cursor = Cursors.Help;

                                            break;
                                        default:
                                            break;
                                    }

                                    (sp.Children[0] as TextBlock).Foreground = brush;

                                });

                            });

                        }
                    }
                }

            }

        }
        private void ClearElements()
        {

            BlockHistoryItemsControl.ItemsSource = null;
            SourcesItemsControl.ItemsSource = null;
            ForwardDestinationsGrid.Children.Clear();
            ForwardDestinationsGrid.RowDefinitions.Clear();
            QueryTypesGrid.Children.Clear();
            QueryTypesGrid.RowDefinitions.Clear();
        }
        private async void InstanceSelected_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;

            if (menuItem != null)
            {
                StackPanel sp = menuItem.Header as StackPanel;

                TextBlock secondTextBlock = sp.Children[1] as TextBlock;


                string secondText = secondTextBlock.Text;
                foreach (Instance i in storage.Instances)
                {
                    if (i.Name == secondTextBlock.Text)
                    {
                        if (cancelToken != null)
                        {
                            cancelToken.Cancel();
                            ClearElements();
                        }
                        cancelToken = new CancellationTokenSource();
                        selectedInstance = i;
                        UpdateInfo(i, cancelToken.Token);
                    }
                }

            }

        }
        private void Addbutton_Click(object sender, RoutedEventArgs e)
        {
            LoginBTN.Visibility = Visibility.Collapsed;
            BackButton.Visibility = Visibility.Visible;
            Info_StackPanel.Visibility = Visibility.Hidden;
            Default_StackPanel.Visibility = Visibility.Visible;
            stopUpdatingInfo = true;
            if (storage.Instances.Count != 0)
            {
                setDefaultTS.Visibility = Visibility.Visible;
            }
            if (cancelToken != null)
            {
                cancelToken.Cancel();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) //TODO: rename it
        {
            stopUpdatingInfo = false;
            if (cancelToken != null)
            {
                cancelToken.Cancel();
            }
            cancelToken = new CancellationTokenSource();
            // ClearElements();
            UpdateInfo(selectedInstance, cancelToken.Token);

            Info_StackPanel.Visibility = Visibility.Visible;
            Default_StackPanel.Visibility = Visibility.Hidden;
        }

        private void StatusTB_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            DisableEnableContextMenu = new ContextMenu();
            if (StatusTB.Text == "disabled")
            {
                MenuItem DisableEnableButton = new MenuItem
                {
                    Header = "Enable",
                    Foreground = new SolidColorBrush(Color.FromRgb(110, 245, 99)),
                    FontSize = 13,
                    FontWeight = FontWeights.Normal
                };
                DisableEnableContextMenu.Width = 20;
                DisableEnableContextMenu.Items.Add(DisableEnableButton);
                DisableEnableButton.Click += DisableButton_Click;
            }
            else
            {
                MenuItem disableBlockingItem = new MenuItem
                {
                    IsEnabled = false,
                    Header = "Disable blocking for:",
                    Foreground = Brushes.White,
                    FontSize = 12,
                    FontWeight = FontWeights.Normal
                };

                DisableSlider = new Slider
                {
                    TickPlacement = TickPlacement.Both,
                    TickFrequency = 1,
                    IsSnapToTickEnabled = true
                };
                DisableSlider.ValueChanged += DisableSlider_ValueChanged;

                DisableEnableButton = new TextBlock
                {
                    Text = "15 seconds",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF6E6E")),
                    TextAlignment = TextAlignment.Center,
                    FontSize = 13,
                    Margin = new Thickness(3)
                };
                DisableEnableButton.MouseLeftButtonUp += DisableButton_Click;

                DisableEnableContextMenu.Items.Add(disableBlockingItem);
                DisableEnableContextMenu.Items.Add(DisableSlider);
                DisableEnableContextMenu.Items.Add(new Separator());
                DisableEnableContextMenu.Items.Add(DisableEnableButton);
            }

            DisableEnableContextMenu.IsOpen = true;
        }

        private async void ChangeBackground_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch ts)
            {
                MicaBG.IsChecked = false;
                AcrylicBG.IsChecked = false;
                NoneBG.IsChecked = false;

                ts.IsChecked = true;

                if (ts.Name == "MicaBG")
                {
                    this.WindowBackdropType = WindowBackdropType.Mica;
                    MainGrid.Background = (Brush)new BrushConverter().ConvertFrom("#0CFFFFFF");
                }
                else if (ts.Name == "AcrylicBG")
                {
                    this.WindowBackdropType = WindowBackdropType.Acrylic;
                    MainGrid.Background = (Brush)new BrushConverter().ConvertFrom("#B2101010");
                }
                else
                {
                    this.WindowBackdropType = WindowBackdropType.None;
                    MainGrid.Background = (Brush)new BrushConverter().ConvertFrom("#B2101010");
                }
                base.OnActivated(e);

                if (!this.IsVisible || this.Top >= SystemParameters.PrimaryScreenHeight)
                {
                    this.Show(); this.Activate();
                    this.Top = (int)SystemParameters.WorkArea.Bottom - this.Height - 12;
                }
                if (cancelToken != null)
                {
                    cancelToken.Cancel();
                }

                cancelToken = new CancellationTokenSource();
                // ClearElements();
                UpdateInfo(selectedInstance, cancelToken.Token);

                WriteToRegistryRoot("Background", ts.Name.Replace("BG", ""));
            }
        }









        private void fluentWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (FileVersionInfo.GetVersionInfo("C:\\Windows\\System32\\kernel32.dll").FileBuildPart >= 22000) // Makes sure it doesn't change on Windows 10 as it crashes the program
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var attribute = GCHandle.Alloc((uint)4, GCHandleType.Pinned);
                var result = DwmSetWindowAttribute(hwnd, (uint)33, attribute.AddrOfPinnedObject(), sizeof(uint));
                attribute.Free();
                if (result != 0)
                {
                    Debug.WriteLine("Couldn't change DWM");
                }
            }
        }




        private void SelectOtherInstnaceBTN_Click(object sender, RoutedEventArgs e)
        {
            OtherInstanceContextMenu.Items.Clear();
            foreach (Instance instance in storage.Instances)
            {
                MenuItem menuItem = new MenuItem
                {
                    FontSize = 13,
                    FontWeight = FontWeights.Normal,
                    Header = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children = {
                  new TextBlock
                  {
                      ToolTip = "",
                      Text = "\u25CF",
                      FontSize = 16,
                      Foreground =  new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                      Margin = new Thickness(0, 0, 7, 2),
                      VerticalAlignment = VerticalAlignment.Center
                  },
                  new TextBlock
                  {
                      Text = instance.Name,
                      VerticalAlignment = VerticalAlignment.Center
                  }
              }
                    }
                };
                Debug.WriteLine($"instance menu added:{instance.Name}");
                menuItem.Click += InstanceSelected_Click;
                OtherInstanceContextMenu.Items.Add(menuItem);
            }
            foreach (var item in OtherInstanceContextMenu.Items)
            {
                if (item is MenuItem menuItem && menuItem.Header is StackPanel sp)
                {

                    foreach (var instance in storage.Instances)
                    {

                        if ((sp.Children[1] as TextBlock).Text == instance.Name)
                        {
                            _ = Task.Run(() =>
                            {
                                int status = instance.Status().Result;
                                Dispatcher.Invoke(() =>
                                {
                                    SolidColorBrush brush = new SolidColorBrush();
                                    switch (status)
                                    {
                                        case 0: // Enabled
                                            brush = new SolidColorBrush(Color.FromRgb(70, 244, 64)); // Green
                                            break;

                                        case 1: // Disabled
                                            brush = new SolidColorBrush(Color.FromRgb(244, 64, 64)); // Red
                                            break;

                                        case 2: // Reachable but can't reach API
                                            brush = new SolidColorBrush(Color.FromRgb(64, 116, 244)); // Blue
                                            menuItem.Click -= InstanceSelected_Click;
                                            menuItem.ToolTip = "Address reachable, but API inaccessible.";
                                            menuItem.Cursor = Cursors.Help;
                                            break;

                                        case -1: // Unreachable
                                            brush = new SolidColorBrush(Color.FromRgb(244, 207, 64)); // Orange-ish
                                            menuItem.ToolTip = "Address unreachable.";
                                            menuItem.Click -= InstanceSelected_Click;
                                            menuItem.Cursor = Cursors.Help;

                                            break;
                                        default:
                                            break;
                                    }

                                    (sp.Children[0] as TextBlock).Foreground = brush;

                                });

                            });

                        }
                    }
                }
            }
            OtherInstanceContextMenu.IsOpen = true;
        }



    }
}