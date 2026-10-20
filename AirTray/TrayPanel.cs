using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

internal sealed class TrayPanel : IDisposable
{
    readonly System.Windows.Application app = new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
    readonly ElginCloud cloud = new();
    readonly Artwork artwork = new();
    readonly Window window;
    DeviceState? state;
    bool busy, updating, connected, exiting;
    Window? loginWindow;
    Forms.NotifyIcon? tray;
    DispatcherTimer? timer;
    internal TrayPanel() { window = Load("Panel.xaml"); window.Icon = artwork.Off; }
    static Window Load(string name)
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("ElginTray." + name)!;
        return (Window)XamlReader.Load(stream);
    }
    T Element<T>(string name) where T : class => (T)window.FindName(name);
    Button Button(string name) => Element<Button>(name);
    TextBlock Text(string name) => Element<TextBlock>(name);
    void Message(string text) => Text("Status").Text = text;

    internal void Run(string[] args)
    {
        if(args.Length == 2 && args[0] == "--self-test-ui")
        {
            Directory.CreateDirectory(args[1]);
            WireControls(); window.Closing += OnWindowClosing;
            state = new DeviceState(new Dictionary<string,int> { ["pwr"]=0,["temp"]=260,["tempunit"]=1 }, DateTimeOffset.Now); connected=true;
            ApplyState();
            if(Element<Image>("AirImage").Source != artwork.Off) throw new InvalidOperationException("Ilustração desligada incorreta.");
            Render(window,Path.Combine(args[1],"preview-off.png"));
            state = state with { Values = new Dictionary<string,int> { ["pwr"]=1,["temp"]=260,["tempunit"]=1 } }; ApplyState();
            if(Element<Image>("AirImage").Source != artwork.On) throw new InvalidOperationException("Ilustração ligada incorreta.");
            Render(window,Path.Combine(args[1],"preview-on.png"));
            Show(); Button("ExitButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            if(window.IsVisible || exiting) throw new InvalidOperationException("Recolher encerrou o aplicativo ou não ocultou o painel.");
            Show(); window.Close();
            if(window.IsVisible || exiting) throw new InvalidOperationException("Fechar encerrou o aplicativo ou não ocultou o painel.");
            Show(); if(!window.IsVisible) throw new InvalidOperationException("O painel não reabriu após fechar.");
            window.Hide();
            Console.WriteLine("PASS: imagens ligada/desligada, recolhimento, fechamento sem encerrar e reabertura. Teste offline, sem acesso à conta ou ao ar.");
            return;
        }
        if(args.Length == 2 && (args[0] == "--preview" || args[0] == "--preview-login"))
        {
            ApplyState();
            Render(args[0] == "--preview-login" ? Load("LoginPanel.xaml") : window,args[1]); return;
        }
        if(args.Length == 2 && args[0] == "--preview-connected")
        {
            var account = CredentialStore.Load() ?? throw new InvalidOperationException("Conta não salva no Windows.");
            cloud.Login(account.Username,account.Password,account.Region).GetAwaiter().GetResult();
            if(!cloud.FindTarget().GetAwaiter().GetResult()) throw new InvalidOperationException("Ar não encontrado.");
            state = cloud.ReadState().GetAwaiter().GetResult(); connected = true;
            ApplyState(); Message("Conectado pela nuvem · estado confirmado no aparelho.");
            Render(window,args[1]); return;
        }
        using var mutex = new Mutex(true,@"Local\ElginTray.ControlPanel",out var first);
        using var open = new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\ElginTray.OpenPanel");
        if(!first) { open.Set(); return; }
        try
        {
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Abrir painel",null,(_,_) => Show());
            menu.Items.Add("Atualizar estado",null,async (_,_) => await Refresh());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Encerrar aplicativo…",null,(_,_) => ConfirmExit());
            tray = new Forms.NotifyIcon { Text = "Elgin · Meu ar",Icon = artwork.OffIcon,ContextMenuStrip = menu,Visible = true };
            tray.MouseClick += (_,e) => { if(e.Button == Forms.MouseButtons.Left) { if(window.IsVisible) window.Hide(); else Show(); } };
            WireControls();
            window.Closing += OnWindowClosing;
            var openTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            openTimer.Tick += (_,_) => { if(open.WaitOne(0)) Show(); };
            openTimer.Start();
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            timer.Tick += async (_,_) => { if(cloud.IsAuthenticated && state != null && !busy) await Refresh(); };
            timer.Start();
            Show();
            app.Dispatcher.BeginInvoke(new Action(async () => { if(args.Contains("--login")) OpenLogin(); else await ConnectSaved(); }));
            app.Run();
            openTimer.Stop(); timer.Stop(); menu.Dispose();
        }
        finally { mutex.ReleaseMutex(); }
    }

    void Show()
    {
        var area = SystemParameters.WorkArea;
        window.Left = Math.Max(area.Left,area.Right-window.Width-12);
        window.Top = Math.Max(area.Top,area.Bottom-window.Height-8);
        window.Show(); window.Activate();
    }
    void Exit() { exiting = true; timer?.Stop(); app.Shutdown(); }
    void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if(!exiting) { e.Cancel = true; window.Hide(); }
    }
    void ConfirmExit()
    {
        if(busy) { Show(); Message("Aguarde a operação terminar antes de encerrar o aplicativo."); return; }
        Show();
        if(MessageBox.Show(window,"Encerrar o painel? O ar continuará no estado atual.\nPara manter o painel na bandeja, escolha Não.","Encerrar aplicativo",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No) == MessageBoxResult.Yes) Exit();
    }
    void WireControls()
    {
        Button("HideButton").Click += (_,_) => window.Hide();
        Button("ExitButton").Click += (_,_) => window.Hide();
        Element<Grid>("Header").MouseLeftButtonDown += (_,e) => { if(e.OriginalSource is TextBlock && e.ButtonState == MouseButtonState.Pressed) window.DragMove(); };
        Button("RefreshButton").Click += async (_,_) => await Refresh();
        Button("AccountButton").Click += (_,_) => OpenLogin();
        Text("AuthorName").Text="por " + ProjectInfo.Author;
        Button("AuthorButton").ToolTip=ProjectInfo.Author;
        Button("AuthorButton").Click += (_,_) =>
        {
            try { Process.Start(new ProcessStartInfo(ProjectInfo.Repository.AbsoluteUri) { UseShellExecute=true }); }
            catch(Exception) { Message("Projeto: github.com/JoseAlexandreMeloPacheco/airtray"); }
        };
        Button("PowerButton").Click += async (_,_) => { if(state != null) await Set("pwr",state.Power ? 0 : 1); };
        Button("MinusButton").Click += async (_,_) => { if(state != null) await Set("temp",Math.Max(160,state.Value("temp")-10)); };
        Button("PlusButton").Click += async (_,_) => { if(state != null) await Set("temp",Math.Min(320,state.Value("temp")+10)); };
        var slider = Element<Slider>("TemperatureSlider");
        async Task SetSlider()
        {
            if(updating || busy || !connected || state == null) return;
            var target = (int)Math.Round(slider.Value,MidpointRounding.AwayFromZero)*10;
            if(target != state.Value("temp")) await Set("temp",target);
        }
        slider.PreviewMouseLeftButtonUp += async (_,_) => await SetSlider();
        slider.KeyUp += async (_,e) => { if(e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End) await SetSlider(); };
        Menu("ModeButton","ac_mode",new[]{"Frio","Quente","Seco","Ventilar","Auto"});
        Menu("FanButton","ac_mark",new[]{"Auto","Baixo","Médio","Alto","Turbo","Silencioso"});
        Button("SleepButton").Click += async (_,_) => await Toggle("ac_slp");
        Button("SwingButton").Click += async (_,_) => await Toggle("ac_vdir");
        Button("EcoButton").Click += async (_,_) => await Toggle("ecomode");
        Button("DisplayButton").Click += async (_,_) => await Toggle("scrdisp");
        var accountMenu = new ContextMenu();
        var forget=new MenuItem { Header="Esquecer conta neste computador…" };
        forget.Click += (_,_) =>
        {
            if(busy) return;
            if(MessageBox.Show(window,"Remover a conta salva neste Windows? O ar continuará no estado atual.","Esquecer conta",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes) return;
            try { CredentialStore.Delete(); cloud.SignOut(); state=null; connected=false; ApplyState(); Message("Conta removida deste computador."); }
            catch(Exception) { Message("Não foi possível remover a credencial do Windows."); }
        };
        accountMenu.Items.Add(forget);
        var disconnect = new MenuItem { Header = "Desconectar esta sessão" };
        disconnect.Click += (_,_) => { if(!busy) { cloud.SignOut(); state = null; connected = false; ApplyState(); Message("Conta desconectada. A credencial permanece guardada no Windows."); } };
        accountMenu.Items.Add(disconnect); Button("AccountButton").ContextMenu = accountMenu;
        var chooseDevice=new MenuItem { Header="Escolher outro aparelho desta conta" };
        chooseDevice.Click += async (_,_) =>
        {
            if(!cloud.IsAuthenticated || busy) return;
            await Operation(async () => { connected=false; state=null; await IdentifyDevice(); state=await cloud.ReadState(); connected=true; Message("Aparelho selecionado · estado confirmado."); });
        };
        accountMenu.Items.Insert(0,chooseDevice);
        ApplyState();
    }
    void Menu(string buttonName,string property,string[] labels)
    {
        var menu = new ContextMenu();
        for(var i=0;i<labels.Length;i++)
        {
            var value = i;
            var item = new MenuItem { Header = labels[i] };
            item.Click += async (_,_) => await Set(property,value);
            menu.Items.Add(item);
        }
        var button = Button(buttonName); button.ContextMenu = menu;
        button.Click += (_,_) => { if(connected && !busy) { menu.PlacementTarget = button; menu.IsOpen = true; } };
    }
    Task Toggle(string property) => state != null && state.Has(property) ? Set(property,state.Value(property)==0 ? 1 : 0) : Task.CompletedTask;
    async Task Set(string property,int value)
    {
        if(!connected || state == null || !state.Has(property) || busy) return;
        if(state.Value(property)==value) { ApplyState(); return; }
        await Operation(async () =>
        {
            Message("Enviando ajuste ao ar…");
            var confirmed = await cloud.SetValues(new Dictionary<string,int> { [property] = value });
            state = confirmed; connected = true;
            Message(confirmed.Value(property) == value ? "Ajuste confirmado no aparelho." : "O aparelho respondeu com outro valor. O painel mostra o estado confirmado.");
        });
    }
    async Task Refresh()
    {
        if(busy) return;
        if(!cloud.IsAuthenticated) { await ConnectSaved(); return; }
        await Operation(async () =>
        {
            Message("Consultando o ar…");
            if(cloud.TargetDevice == null) await IdentifyDevice();
            state = await cloud.ReadState(); connected = true; Message("Estado atualizado no aparelho.");
        });
    }
    async Task ConnectSaved()
    {
        if(busy || cloud.IsAuthenticated) return;
        CredentialStore.Account? account;
        try { account = CredentialStore.Load(); }
        catch(Exception) { Message("Não foi possível acessar a credencial do Windows."); OpenLogin(); return; }
        if(account == null) { OpenLogin(); return; }
        await Operation(async () =>
        {
            Message("Entrando na sua conta Elgin Air…");
            await cloud.Login(account.Username,account.Password,account.Region);
            Message("Identificando seu ar…");
            await IdentifyDevice();
            state = await cloud.ReadState(); connected = true;
            Message("Conectado pela nuvem · pronto para controlar.");
        });
    }
    async Task Operation(Func<Task> action)
    {
        if(busy || exiting) return;
        busy = true; ApplyState();
        try { await action(); }
        catch(CloudException ex) { connected = false; Message(ex.Message); }
        catch(Exception) { connected = false; Message("Não foi possível confirmar o estado. Clique em atualizar para tentar novamente."); }
        finally { busy = false; if(!exiting) ApplyState(); }
    }
    void ApplyState()
    {
        updating = true;
        try
        {
            var ready = connected && !busy && state != null;
            var celsius = state == null || !state.Has("tempunit") || state.Value("tempunit") == 1;
            Element<UIElement>("TempControls").IsEnabled = ready && celsius;
            Element<UIElement>("FeatureControls").IsEnabled = ready;
            Button("PowerButton").IsEnabled = ready;
            Button("EcoButton").IsEnabled = ready && state!.Has("ecomode");
            Button("DisplayButton").IsEnabled = ready && state!.Has("scrdisp");
            Button("RefreshButton").IsEnabled = !busy;
            Button("AccountButton").IsEnabled = !busy;
            foreach(var pair in new[]{("ModeButton","ac_mode"),("FanButton","ac_mark"),("SleepButton","ac_slp"),("SwingButton","ac_vdir")}) Button(pair.Item1).IsEnabled = ready && state!.Has(pair.Item2);
            Text("TemperatureText").Text = state?.Temperature.ToString("0.#",CultureInfo.GetCultureInfo("pt-BR")) ?? "—";
            Text("TemperatureUnit").Text = celsius ? "°C" : "°F";
            if(state != null && celsius) Element<Slider>("TemperatureSlider").Value = Math.Clamp(state.Temperature,16,32);
            Text("ConnectionText").Text = busy ? "Conectando…" : connected ? state!.Power ? "Ligado" : "Desligado" : "Sem conexão";
            Text("Footer").Text = connected ? "●  Conectado · " + state!.Updated.ToString("HH:mm:ss") : state != null ? "○  Última leitura · " + state.Updated.ToString("HH:mm:ss") : "○  Aguardando conexão";
            Text("ModeText").Text = Label("ac_mode",new[]{"Frio","Quente","Seco","Ventilar","Auto"});
            Text("FanText").Text = Label("ac_mark",new[]{"Auto","Baixo","Médio","Alto","Turbo","Silêncio"});
            Text("SleepText").Text = Label("ac_slp",new[]{"Desligado","Ligado"});
            Text("SwingText").Text = Label("ac_vdir",new[]{"Fixo","Oscilando"});
            Text("EcoText").Text = Label("ecomode",new[]{"Desligado","Ligado"});
            Text("DisplayText").Text = Label("scrdisp",new[]{"Apagado","Aceso"});
            Button("PowerButton").ToolTip = state?.Power == true ? "Desligar ar" : "Ligar ar";
            Button("PowerButton").Foreground = new SolidColorBrush(state?.Power == true && connected ? Color.FromRgb(145,214,237) : Color.FromRgb(170,185,193));
            var powered = connected && state?.Power == true;
            var illustration = Element<Image>("AirImage");
            illustration.Source = powered ? artwork.On : artwork.Off;
            illustration.Opacity = connected ? 1 : .5;
            illustration.ToolTip = connected ? powered ? "Ar ligado · estado confirmado" : "Ar desligado · estado confirmado" : "Estado do ar desconhecido";
            if(tray != null)
            {
                tray.Icon = powered ? artwork.OnIcon : artwork.OffIcon;
                tray.Text = connected ? "Elgin · " + (state!.Power ? "Ligado" : "Desligado") + " · " + state.Temperature.ToString("0.#") + (celsius ? " °C" : " °F") : "Elgin · sem conexão";
            }
        }
        finally { updating = false; }
    }
    string Label(string property,string[] labels)
    {
        if(state == null || !state.Has(property)) return "—";
        var value = state.Value(property);
        return value >= 0 && value < labels.Length ? labels[value] : value.ToString(CultureInfo.InvariantCulture);
    }
    void OpenLogin()
    {
        if(busy) return;
        if(loginWindow != null) { loginWindow.Activate(); return; }
        var login = Load("LoginPanel.xaml"); loginWindow = login;
        var username=(TextBox)login.FindName("Username"); var password=(PasswordBox)login.FindName("Password");
        var region=(ComboBox)login.FindName("Region"); var signIn=(Button)login.FindName("SignIn"); var info=(TextBlock)login.FindName("LoginStatus");
        CredentialStore.Account? saved=null;
        try { saved=CredentialStore.Load(); if(saved!=null) { username.Text=saved.Username; region.SelectedIndex=saved.Region; info.Text="Senha em branco usa a conta salva no Windows."; } } catch(Exception) { }
        bool closed=false;
        login.Closed += (_,_) => { closed=true; password.Clear(); saved=null; loginWindow=null; };
        signIn.Click += async (_,_) =>
        {
            if(busy) return;
            signIn.IsEnabled=false;
            var user=username.Text.Trim(); var selected=region.SelectedIndex;
            var secret=password.Password;
            if(string.IsNullOrEmpty(secret))
            {
                try { saved=CredentialStore.Load(); } catch(Exception) { }
                if(saved?.Username==user) secret=saved.Password;
            }
            password.Clear(); info.Text="Conectando…";
            await Operation(async () =>
            {
                try
                {
                    await cloud.Login(user,secret,selected);
                    if(closed) { secret=string.Empty; cloud.SignOut(); state=null; connected=false; Message("Entrada cancelada. Nenhuma nova credencial foi salva."); return; }
                    CredentialStore.Save(new CredentialStore.Account(user,secret,selected));
                    secret=string.Empty; saved=null;
                    await IdentifyDevice();
                    state=await cloud.ReadState(); connected=true;
                    Message("Conectado pela nuvem · pronto para controlar.");
                    if(!closed) login.Close();
                }
                catch(Exception) { if(!closed) info.Text="Não foi possível concluir. Confira a mensagem no painel."; throw; }
            });
            secret=string.Empty;
            if(!closed) signIn.IsEnabled=true;
        };
        login.Show(); username.Focus();
    }
    async Task IdentifyDevice()
    {
        await cloud.DiscoverDevices();
        if(cloud.AvailableDevices.Count==0) throw new CloudException("Nenhum aparelho da família suportada foi encontrado. Este aplicativo não é compatível com todos os modelos Elgin.");
        var choice=cloud.AvailableDevices[0];
        if(cloud.AvailableDevices.Count>1)
        {
            var picker=new Window { Title="Escolha seu ar-condicionado", Width=390, Height=340, ResizeMode=ResizeMode.NoResize, WindowStartupLocation=WindowStartupLocation.CenterOwner, Owner=loginWindow ?? window, Background=new SolidColorBrush(Color.FromRgb(21,28,32)), Foreground=Brushes.White };
            var layout=new DockPanel { Margin=new Thickness(20) };
            var description=new TextBlock { Text="Selecione o aparelho que deseja controlar nesta sessão.", TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,12) }; DockPanel.SetDock(description,Dock.Top); layout.Children.Add(description);
            var confirm=new Button { Content="Usar este aparelho", Margin=new Thickness(0,12,0,0), Padding=new Thickness(10), IsDefault=true }; DockPanel.SetDock(confirm,Dock.Bottom); layout.Children.Add(confirm);
            var list=new ListBox { DisplayMemberPath="Name", ItemsSource=cloud.AvailableDevices, SelectedIndex=0 }; layout.Children.Add(list);
            confirm.Click += (_,_) => { if(list.SelectedItem!=null) picker.DialogResult=true; };
            picker.Content=layout;
            if(picker.ShowDialog()!=true || list.SelectedItem is not ElginCloud.DeviceChoice selected) throw new CloudException("Seleção cancelada. Nenhum comando foi enviado a um aparelho.");
            choice=selected;
        }
        cloud.SelectDevice(choice);
        Text("TitleText").Text=choice.Name;
    }
    static void Render(Window source,string path)
    {
        var content=(FrameworkElement)source.Content;
        content.Measure(new Size(source.Width,source.Height)); content.Arrange(new Rect(0,0,source.Width,source.Height)); content.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)source.Width,(int)source.Height,96,96,PixelFormats.Pbgra32); bitmap.Render(content);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file=File.Create(path); encoder.Save(file);
    }
    public void Dispose() { exiting=true; timer?.Stop(); tray?.Dispose(); artwork.Dispose(); cloud.Dispose(); }
}
