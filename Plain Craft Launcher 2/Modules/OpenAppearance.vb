' New Open appearance/localization infrastructure; upstream UI controls retain their original license.
Imports Microsoft.Win32
Imports PCLCS.Open
Imports System.Globalization
Imports System.Windows.Media
Imports System.Runtime.CompilerServices

Public Module OpenAppearance
    Public IsDark As Boolean
    Private Started As Boolean
    Private PreferenceRefreshTimer As Windows.Threading.DispatcherTimer
    Private LastTranslatedLanguage As String
    Private ReadOnly OriginalText As New ConditionalWeakTable(Of DependencyObject, Dictionary(Of DependencyProperty, String))
    Private ReadOnly LastText As New ConditionalWeakTable(Of DependencyObject, Dictionary(Of DependencyProperty, String))
    Private English As Dictionary(Of String, String)
    Private TranslationQueued As Boolean

    Public Sub InitializeOpen()
        OAuthClientId = OpenPreferences.ClientId
        CurseForgeAPIKey = OpenPreferences.CurseForgeKey
        If Started Then Return
        Started = True
        EventManager.RegisterClassHandler(GetType(FrameworkElement), FrameworkElement.LoadedEvent, New RoutedEventHandler(AddressOf ElementLoaded))
        PreferenceRefreshTimer = New Windows.Threading.DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(180)}
        AddHandler PreferenceRefreshTimer.Tick,
            Sub(Sender As Object, Args As EventArgs)
                PreferenceRefreshTimer.Stop()
                RefreshOpenAppearance()
            End Sub
        AddHandler SystemEvents.UserPreferenceChanged,
            Sub()
                If Application.Current Is Nothing Then Return
                Application.Current.Dispatcher.BeginInvoke(New Action(
                    Sub()
                        If PreferenceRefreshTimer Is Nothing Then Return
                        PreferenceRefreshTimer.Stop()
                        PreferenceRefreshTimer.Start()
                    End Sub))
            End Sub
    End Sub

    Public Sub RefreshOpenAppearance()
        InitializeOpen()
        Dim Light As Boolean = True
        Try
            Light = CInt(Registry.GetValue("HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1)) <> 0
        Catch
        End Try
        IsDark = OpenPreferences.Theme = "dark" OrElse (OpenPreferences.Theme = "system" AndAlso Not Light)
        Dim Palette = If(IsDark,
            {"#F0F0F0", "#87B7FA", "#4F98FC", "#76AFF8", "#586B85", "#304159", "#29374B", "#252F3E"},
            {"#343D4A", "#0B5BCB", "#1370F3", "#4890F5", "#96C0F9", "#D5E6FD", "#E0EAFD", "#EAF2FE"})
        Dim Scheme = If(IsDark, OpenPreferences.DarkScheme, OpenPreferences.LightScheme)
        If Scheme = "mist" Then
            Palette(1) = If(IsDark, "#A9C5ED", "#3C6BA9")
            Palette(2) = If(IsDark, "#85AEDF", "#5485C5")
            Palette(3) = If(IsDark, "#A2C2EB", "#7CA4D8")
        ElseIf Scheme = "ocean" Then
            Palette(1) = If(IsDark, "#8DBAF5", "#16447C")
            Palette(2) = If(IsDark, "#5C9DEC", "#2467AE")
            Palette(3) = If(IsDark, "#89B8F0", "#4B8BCB")
        End If
        Dim Grays = If(IsDark,
            {"#ECECEC", "#BEBEBE", "#A2A2A2", "#8B8B8B", "#626262", "#3B3C40", "#303135", "#222327"},
            {"#404040", "#737373", "#8C8C8C", "#A6A6A6", "#CCCCCC", "#EBEBEB", "#F0F0F0", "#F5F5F5"})
        'Classic light keeps PCL's own palette and top-bar gradient. The CE-inspired
        'palette is only applied to the additional schemes; dark mode has its own surfaces.
        If Scheme <> "blue" OrElse IsDark Then
            For I = 1 To 8
                PutColor(I.ToString(), Palette(I - 1))
                PutColor("Gray" & I, Grays(I - 1))
            Next
            Color1 = Parse(Palette(0)) : Color2 = Parse(Palette(1)) : Color3 = Parse(Palette(2)) : Color4 = Parse(Palette(3))
            Color5 = Parse(Palette(4)) : Color6 = Parse(Palette(5)) : Color7 = Parse(Palette(6)) : Color8 = Parse(Palette(7))
            ColorGray1 = Parse(Grays(0)) : ColorGray2 = Parse(Grays(1)) : ColorGray3 = Parse(Grays(2)) : ColorGray4 = Parse(Grays(3))
            ColorGray5 = Parse(Grays(4)) : ColorGray6 = Parse(Grays(5)) : ColorGray7 = Parse(Grays(6)) : ColorGray8 = Parse(Grays(7))
            PutColor("Surface", If(IsDark, "#28292D", "#FAFFFFFF"))
            PutColor("DialogShadow", If(IsDark, "#101116", "#343D4A"))
            PutColor("Canvas", If(IsDark, "#202125", "#F5F5F5"))
            PutColor("White", If(IsDark, "#28292D", "#FFFFFF"))
            PutColor("HalfWhite", If(IsDark, "#5528292D", "#55FFFFFF"))
            PutColor("SemiWhite", If(IsDark, "#BB28292D", "#BBFFFFFF"))
            PutColor("BackgroundTransparentSidebar", If(IsDark, "#F126272B", "#F1FFFFFF"))
            PutColor("Bg0", If(IsDark, "#526D91", "#96C0F9"))
            PutColor("Bg1", If(IsDark, "#BE2D4664", "#BEE0EAFD"))
            PutColor("RedBack", If(IsDark, "#804D2931", "#80FBDDDD"))
            PutColor("RedDark", If(IsDark, "#FF9C96", "#CE2111"))
            ColorBg0 = Parse(If(IsDark, "#526D91", "#96C0F9"))
            ColorBg1 = Parse(If(IsDark, "#BE2D4664", "#BEE0EAFD"))
        End If
        If FrmMain IsNot Nothing Then
            If IsDark OrElse Not Settings.Get(Of Boolean)("UiBackgroundColorful") Then FrmMain.PanForm.SetResourceReference(Panel.BackgroundProperty, "ColorBrushCanvas")
            If Scheme <> "blue" OrElse IsDark Then
                Dim Header = If(IsDark,
                    Parse(If(Scheme = "mist", "#364A63", If(Scheme = "ocean", "#1E3F65", "#29466B"))),
                    Parse(Palette(2)))
                FrmMain.PanTitle.Background = New SolidColorBrush(Header)
            End If
            Dim BadgeBase = If(Scheme = "blue" AndAlso Not IsDark, CType(Color3, Color), CType(FrmMain.PanTitle.Background, SolidColorBrush).Color)
            PutColor("OpenBadge", Color.FromArgb(255, CByte(BadgeBase.R + (255 - BadgeBase.R) * 0.08), CByte(BadgeBase.G + (255 - BadgeBase.G) * 0.08), CByte(BadgeBase.B + (255 - BadgeBase.B) * 0.08)).ToString())
            Dim Language = If(OpenPreferences.Language = "system", OpenPreferences.AutoLanguage, OpenPreferences.Language)
            If LastTranslatedLanguage <> Language Then
                TranslateTree(FrmMain)
                LastTranslatedLanguage = Language
            End If
        End If
    End Sub

    Private Function Parse(Value As String) As Color
        Return CType(ColorConverter.ConvertFromString(Value), Color)
    End Function
    Private Sub PutColor(Key As String, Value As String)
        Dim C = Parse(Value)
        Application.Current.Resources("ColorBrush" & Key) = New SolidColorBrush(C)
        Application.Current.Resources("ColorObject" & Key) = C
    End Sub
    Public Function Tr(Text As String) As String
        Dim Language = OpenPreferences.Language
        If Language = "system" Then Language = OpenPreferences.AutoLanguage
        If Language.StartsWith("zh") AndAlso Language <> "zh-TW" AndAlso Language <> "zh-HK" Then Return Text
        If Language = "zh-TW" OrElse Language = "zh-HK" Then
            Return Microsoft.VisualBasic.Strings.StrConv(Text, VbStrConv.TraditionalChinese, 2052)
        End If
        If English Is Nothing Then
            Dim Stream = Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("PCL.Open.en-US.json")
            If Stream Is Nothing Then Return Text
            Using Reader As New IO.StreamReader(Stream)
                English = Newtonsoft.Json.JsonConvert.DeserializeObject(Of Dictionary(Of String, String))(Reader.ReadToEnd())
            End Using
        End If
        Dim Value As String = Nothing
        If English.TryGetValue(Text, Value) Then Return Value
        For Each Prefix In {"获取失败：", "收藏夹读取失败：", "不支持该文件类型：", "下载失败：", "今日提示："}
            If Text.StartsWith(Prefix, StringComparison.Ordinal) AndAlso English.TryGetValue(Prefix, Value) Then Return Value & Tr(Text.Substring(Prefix.Length))
        Next
        Return Text
    End Function

    Private Sub ElementLoaded(Sender As Object, E As RoutedEventArgs)
        TranslateElement(TryCast(Sender, DependencyObject))
        If TranslationQueued Then Return
        TranslationQueued = True
        Application.Current.Dispatcher.BeginInvoke(New Action(
            Sub()
                TranslationQueued = False
                If FrmMain IsNot Nothing Then TranslateTree(FrmMain)
            End Sub), Threading.DispatcherPriority.ContextIdle)
    End Sub
    Public Sub TranslateTree(Root As DependencyObject)
        If Root Is Nothing Then Return
        TranslateElement(Root)
        For I = 0 To VisualTreeHelper.GetChildrenCount(Root) - 1
            TranslateTree(VisualTreeHelper.GetChild(Root, I))
        Next
    End Sub
    Private Sub TranslateElement(Element As DependencyObject)
        If Element Is Nothing Then Return
        If TypeOf Element Is TextBlock Then
            Dim Ancestor = TryCast(Element, FrameworkElement).Parent
            While Ancestor IsNot Nothing
                If TypeOf Ancestor Is MyLocalModItem Then Return
                Ancestor = If(TryCast(Ancestor, FrameworkElement)?.Parent, Nothing)
            End While
        End If
        Dim Properties As New List(Of DependencyProperty)
        If TypeOf Element Is TextBlock Then Properties.Add(TextBlock.TextProperty)
        If TypeOf Element Is MyButton Then Properties.Add(MyButton.TextProperty)
        If TypeOf Element Is MyListItem Then Properties.Add(MyListItem.TitleProperty)
        If TypeOf Element Is MyRadioButton Then Properties.Add(MyRadioButton.TextProperty)
        If TypeOf Element Is MyCard Then Properties.Add(MyCard.TitleProperty)
        If TypeOf Element Is MyCheckBox Then Properties.Add(MyCheckBox.TextProperty)
        If TypeOf Element Is MyRadioBox Then Properties.Add(MyRadioBox.TextProperty)
        If TypeOf Element Is MyTextButton Then Properties.Add(MyTextButton.TextProperty)
        If TypeOf Element Is MyHint Then Properties.Add(MyHint.TextProperty)
        If TypeOf Element Is MyIconTextButton Then Properties.Add(MyIconTextButton.TextProperty)
        If TypeOf Element Is MyTextBox Then Properties.Add(MyTextBox.HintTextProperty)
        If TypeOf Element Is FrameworkElement AndAlso TypeOf CType(Element, FrameworkElement).ToolTip Is String Then Properties.Add(FrameworkElement.ToolTipProperty)
        If TypeOf Element Is MyComboBoxItem AndAlso TypeOf CType(Element, MyComboBoxItem).Content Is String Then Properties.Add(ContentControl.ContentProperty)
        For Each Prop In Properties
            'Data-bound project names, usernames and user input must remain unchanged.
            If System.Windows.Data.BindingOperations.IsDataBound(Element, Prop) Then Continue For
            Dim Current = TryCast(Element.GetValue(Prop), String)
            If String.IsNullOrEmpty(Current) Then Continue For
            Dim Originals = OriginalText.GetOrCreateValue(Element)
            Dim Last = LastText.GetOrCreateValue(Element)
            If Not Originals.ContainsKey(Prop) OrElse Not Last.ContainsKey(Prop) OrElse Current <> Last(Prop) Then Originals(Prop) = Current
            Dim Translated = Tr(Originals(Prop))
            If Current <> Translated Then Element.SetCurrentValue(Prop, Translated)
            Last(Prop) = Translated
        Next
    End Sub
End Module
