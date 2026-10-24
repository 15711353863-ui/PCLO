Imports PCLCS.Open

' Native PCL controls, placed inside the existing Personalization page.
Public Class OpenAppearanceSettings
    Inherits MyCard
    Public Sub New()
        Title = "主题与语言"
        Margin = New Thickness(0, 15, 0, 0)
        Dim Panel As New StackPanel With {.Margin = New Thickness(22, 42, 22, 18)}
        Children.Add(Panel)
        Dim Options As New Grid
        For I = 0 To 2 : Options.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)}) : Next
        Panel.Children.Add(Options)
        Dim Theme = AddChoice(Options, 0, "主题", {("system", "跟随系统"), ("light", "浅色"), ("dark", "深色")}, OpenPreferences.Theme, "ComboOpenTheme")
        Dim Schemes = {("blue", "经典蓝"), ("mist", "雾霭蓝"), ("ocean", "深海蓝")}
        Dim Light = AddChoice(Options, 1, "浅色配色", Schemes, OpenPreferences.LightScheme, "ComboOpenLightScheme")
        Dim Dark = AddChoice(Options, 2, "深色配色", Schemes, OpenPreferences.DarkScheme, "ComboOpenDarkScheme")
        Dim LanguageGrid As New Grid With {.Margin = New Thickness(0, 16, 0, 0)}
        LanguageGrid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        LanguageGrid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(2, GridUnitType.Star)})
        Panel.Children.Add(LanguageGrid)
        Dim Language = AddChoice(LanguageGrid, 0, "界面语言", {("system", "根据地区自动选择"), ("zh-CN", "简体中文"), ("zh-TW", "繁體中文"), ("en-US", "English")}, OpenPreferences.Language, "ComboOpenLanguage")
        Dim Note As New TextBlock With {.Text = "自动语言依据 Windows 地区设置；手动选择优先。浅色与深色配色分别保存。", .TextWrapping = TextWrapping.Wrap, .VerticalAlignment = VerticalAlignment.Bottom, .Margin = New Thickness(10, 0, 0, 0)}
        Grid.SetColumn(Note, 1) : LanguageGrid.Children.Add(Note)
        Dim Save As Action =
            Sub()
                OpenPreferences.SaveAppearance(CStr(CType(Theme.SelectedItem, MyComboBoxItem).Tag), CStr(CType(Language.SelectedItem, MyComboBoxItem).Tag), CStr(CType(Light.SelectedItem, MyComboBoxItem).Tag), CStr(CType(Dark.SelectedItem, MyComboBoxItem).Tag))
                OpenAppearance.RefreshOpenAppearance()
            End Sub
        For Each Combo In {Theme, Light, Dark, Language}
            AddHandler Combo.SelectionChanged, Sub() Save()
        Next
    End Sub
    Private Function AddChoice(Parent As Grid, Column As Integer, Label As String, Values As (String, String)(), Selected As String, ControlName As String) As MyComboBox
        Dim Container As New StackPanel With {.Margin = New Thickness(0, 0, If(Column = Parent.ColumnDefinitions.Count - 1, 0, 18), 0)}
        Container.Children.Add(New TextBlock With {.Text = Label, .Margin = New Thickness(0, 0, 0, 8)})
        Dim Combo As New MyComboBox With {.Height = 30, .Name = ControlName}
        For Each Value In Values
            Combo.Items.Add(New MyComboBoxItem With {.Tag = Value.Item1, .Content = Value.Item2})
            If Selected = Value.Item1 Then Combo.SelectedIndex = Combo.Items.Count - 1
        Next
        If Combo.SelectedIndex = -1 Then Combo.SelectedIndex = 0
        Container.Children.Add(Combo) : Grid.SetColumn(Container, Column) : Parent.Children.Add(Container)
        Return Combo
    End Function
End Class
