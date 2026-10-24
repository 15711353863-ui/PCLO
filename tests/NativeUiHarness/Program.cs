// SPDX-License-Identifier: MIT
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using PCL;
using PCLCS.Open;

internal static class Program {
    [STAThread]
    private static int Main() {
        var root = Path.Combine(Path.GetTempPath(), "pclo-native-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("PCL_OPEN_DATA_DIR", root);
        int exitCode = 0;
        try {
            var app = (PCL.Application)Activator.CreateInstance(typeof(PCL.Application), true);
            app.InitializeComponent();
            OpenAppearance.InitializeOpen();
            int count = 0;
            void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS: " + label); count++; }
            var row = new MyLocalModItem();
            row.SetPresentation("Resource title", "Resource description");
            row.Checked = true; row.Checked = false;
            Check(row.Entry == null && row.Title == "Resource title" && row.Description == "Resource description", "native mod row supports non-mod presentation without a fake file entry");
            row.Buttons = new[] { OpenResourceUi.Icon("Open", OpenResourceUi.OpenLogo, () => { }) };
            Check(row.Buttons.Count() == 1 && ((TextBlock)row.FindName("LabTitle")).TextDecorations == null, "native row actions and enabled text remain intact");
            var nativeMod = new MyLocalModItem { Entry = new LocalResourceFile(Path.Combine(root, "original.jar")) };
            nativeMod.Title = "Original mod";
            Check(nativeMod.Entry.IsEnabled && !nativeMod.PresentationOnly, "original mod rows retain their original mode");
            var instance = new PageInstanceLeft();
            Check(instance.FindName("ItemSchematics") == null && instance.FindName("ItemServers") == null, "removed instance resource entries are absent from compiled XAML");
            Check(((MyListItem)instance.FindName("ItemResourcePacks")).Logo != ((MyListItem)instance.FindName("ItemSaves")).Logo, "resource packs have a distinct pack icon instead of the saves folder");
            var download = new PageDownloadLeft();
            Check(download.FindName("ItemPackages") == null && ((MyListItem)download.FindName("ItemInstall")).Title == "Minecraft", "redundant installer entry is removed from compiled navigation");
            var installer = new PageDownloadInstall();
            var cards = ((StackPanel)installer.FindName("PanSelect")).Children.OfType<MyCard>().Select(c => c.Title).ToList();
            Check(cards.Contains("Legacy Fabric") && cards.Contains("Cleanroom") && cards.Contains("LabyMod"), "all added loaders are part of the original Minecraft installation selector");
            foreach (var item in ((StackPanel)instance.FindName("PanItem")).Children.OfType<MyListItem>()) {
                item.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, item));
                Check(item.UseSidebarLayout && item.MinPaddingRight >= 32 && ((ColumnDefinition)item.FindName("ColumnPaddingRight")).Width.Value >= 32, "sidebar reserves right gutter: " + item.Name);
                if (item.PathLogo is System.Windows.Shapes.Path path) Check(Math.Abs(path.Data.Bounds.X) < 0.01 && Math.Abs(path.Width - 20) < 0.01, "sidebar glyph starts at the shared left edge: " + item.Name);
            }
            var appearance = new OpenAppearanceSettings();
            var controls = Descendants(appearance).OfType<MyComboBox>().ToList();
            Check(controls.Count == 4, "personalization exposes theme, light scheme, dark scheme and region language");
            controls.Single(c => c.Name == "ComboOpenTheme").SelectedIndex = 2;
            controls.Single(c => c.Name == "ComboOpenLightScheme").SelectedIndex = 1;
            controls.Single(c => c.Name == "ComboOpenDarkScheme").SelectedIndex = 2;
            Check(OpenPreferences.Theme == "dark" && OpenPreferences.LightScheme == "mist" && OpenPreferences.DarkScheme == "ocean", "light and dark choices persist independently through native controls");
            Check(OpenAppearance.IsDark && ((SolidColorBrush)app.Resources["ColorBrushSurface"]).Color.R == 40, "dark surface uses a neutral gray palette");
            controls.Single(c => c.Name == "ComboOpenLanguage").SelectedIndex = 3;
            Check(OpenAppearance.Tr("版本设置") == "Version settings" && OpenAppearance.Tr("下载失败：network") == "Download failed: network", "English covers key navigation and dynamic error prefixes");
            var dialog = new MyMsgText(new ModMain.MyMsgBoxConverter { Title = "Error", Text = "Test" });
            var shadow = (DropShadowEffect)((Border)dialog.FindName("PanBorder")).Effect;
            Check(shadow.Color.R < 50 && shadow.Color.G < 50, "dialog shadow remains dark in dark mode");
            var utilities = new PageOtherTest();
            Check(utilities.FindName("BtnOptimizeMemory") is MyButton && utilities.FindName("BtnOpenCache") is MyButton, "utilities page uses working native PCL controls");
            var about = new PageOtherAbout();
            Check(!Descendants(about).OfType<TextBlock>().Any(t => t.Text.Contains("赞助") || t.Text.Contains("龙猫")), "about page no longer displays sponsorship prompts");
            var favorites = new PageOpenFavorites();
            Check(!Descendants(favorites).OfType<MyTextBox>().Any(), "favorites has no search field");
            var splash = System.Windows.Application.GetResourceStream(new Uri("/Plain Craft Launcher 2;component/Images/Heads/OpenSplash.png", UriKind.Relative));
            using (var stream = splash.Stream) {
                var bytes = new byte[24]; stream.Read(bytes, 0, bytes.Length);
                int ReadInt(int i) => bytes[i] << 24 | bytes[i + 1] << 16 | bytes[i + 2] << 8 | bytes[i + 3];
                Check(ReadInt(16) == 256 && ReadInt(20) == 256, "startup uses a fixed 256px PNG instead of an ICO frame");
            }
            Console.WriteLine("PASS: " + count + " native WPF integration checks (no desktop input)");
            return 0;
        } catch (Exception error) {
            exitCode = 1;
            Console.WriteLine("FAIL: " + error.GetType().FullName + ": " + error.Message);
            Console.WriteLine(error.StackTrace);
            return 1;
        } finally {
            Directory.Delete(root, true);
            // The original animation/loader infrastructure creates lifetime threads. This
            // isolated console test has no dispatcher loop and ends after all assertions.
            Environment.Exit(exitCode);
        }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root) {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach (var descendant in Descendants(child)) yield return descendant;
    }
}
