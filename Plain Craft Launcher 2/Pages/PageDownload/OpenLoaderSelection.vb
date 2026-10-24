Imports PCLCS.Open

' Extend the original Minecraft installer; no parallel installer page or navigation API.
Partial Public Class PageDownloadInstall
    Private OpenLoaderKind As String
    Private OpenLoaderRelease As JObject
    Private OpenEpoch As Integer
    Private ReadOnly OpenCards As New Dictionary(Of String, MyCard)
    Private ReadOnly OpenPanels As New Dictionary(Of String, StackPanel)
    Private ReadOnly OpenStatuses As New Dictionary(Of String, TextBlock)
    Private ReadOnly OpenClearButtons As New Dictionary(Of String, MyIconButton)
    Private ReadOnly OpenAvailable As New Dictionary(Of String, Boolean)
    Private ReadOnly OpenTasks As New List(Of LoaderTask(Of String, List(Of JObject)))

    Private Sub EnsureOpenLoaderUi()
        If GetType(PageDownloadModify).IsAssignableFrom(Me.GetType()) OrElse OpenCards.Count > 0 Then Return
        For Each Kind In {"Legacy Fabric", "Cleanroom", "LabyMod"}
            Dim Card As New MyCard With {.Title = Kind, .Height = 40, .Margin = New Thickness(0, 12, 0, 0), .CanSwap = True, .IsSwapped = True, .SwapLogoRight = True}
            Dim Panel As New StackPanel With {.Margin = New Thickness(20, 40, 18, 15)}
            Card.Children.Add(Panel) : PanSelect.Children.Add(Card)
            OpenCards.Add(Kind, Card) : OpenPanels.Add(Kind, Panel)
            OpenAvailable.Add(Kind, False)
            AddHandler Card.PreviewSwap,
                Sub(Sender, E)
                    If Not OpenAvailable(Kind) Then E.Handled = True
                End Sub
            Dim Status As New TextBlock With {.Text = "正在检查…", .Foreground = ColorGray4, .VerticalAlignment = VerticalAlignment.Top, .Margin = New Thickness(132, 10, 50, 0), .IsHitTestVisible = False}
            Status.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray4")
            Card.Children.Add(Status) : OpenStatuses.Add(Kind, Status)
            Dim Clear As New MyIconButton With {.ToolTip = "取消选择", .Width = 24, .Height = 24, .HorizontalAlignment = HorizontalAlignment.Right, .VerticalAlignment = VerticalAlignment.Top, .Margin = New Thickness(0, 8, 38, 0), .Logo = "M0,2 L2,0 L10,8 L18,0 L20,2 L12,10 L20,18 L18,20 L10,12 L2,20 L0,18 L8,10 Z", .Visibility = Visibility.Collapsed}
            OpenClearButtons.Add(Kind, Clear)
            AddHandler Clear.Click, Sub()
                                        If OpenLoaderKind <> Kind Then Return
                                        OpenLoaderKind = Nothing : OpenLoaderRelease = Nothing
                                        UpdateOpenCardTitles() : SelectNameUpdate()
                                    End Sub
            Card.Children.Add(Clear)
        Next
    End Sub

    Private Sub LoadOpenLoaders()
        EnsureOpenLoaderUi()
        If OpenCards.Count = 0 Then Return
        OpenEpoch += 1 : Dim Ticket = OpenEpoch : Dim Game = VanillaName
        For Each Previous In OpenTasks : Previous.Cancel() : Next
        OpenTasks.Clear()
        OpenLoaderKind = Nothing : OpenLoaderRelease = Nothing : UpdateOpenCardTitles()
        For Each Kind In OpenCards.Keys
            Dim Panel = OpenPanels(Kind)
            Panel.Children.Clear()
            OpenAvailable(Kind) = False
            OpenStatuses(Kind).Text = "正在检查…"
            OpenCards(Kind).IsSwapped = True
            OpenCards(Kind).MainSwap.Visibility = Visibility.Collapsed
            Dim Loading As New MyLoading With {.Text = "正在获取 " & Kind & " 版本", .Margin = New Thickness(12)}
            Panel.Children.Add(Loading)
            Dim Worker As New LoaderTask(Of String, List(Of JObject))("Open " & Kind & " catalogue",
                Sub(Task)
                    Select Case Kind
                        Case "Legacy Fabric"
                            Task.Output = LoaderCatalog.LegacyVersions(Game).OfType(Of JObject)().ToList()
                        Case "Cleanroom"
                            Task.Output = If(Game = "1.12.2", LoaderCatalog.CleanroomInstallers().OfType(Of JObject)().ToList(), New List(Of JObject))
                        Case "LabyMod"
                            Dim Release = LoaderCatalog.LabyRelease()
                            Task.Output = If(Release("minecraftVersions").Any(Function(V) CStr(V("version")) = Game), New List(Of JObject) From {Release}, New List(Of JObject))
                    End Select
                End Sub)
            Loading.State = Worker
            AddHandler Worker.OnStateChangedUi,
                Sub(Task, State, OldState)
                    If Ticket <> OpenEpoch OrElse Game <> VanillaName Then Return
                    If State = LoadState.Finished Then
                        Panel.Children.Clear()
                        If Worker.Output.Count = 0 Then
                            OpenAvailable(Kind) = False
                            OpenStatuses(Kind).Text = "无"
                            OpenCards(Kind).IsSwapped = True
                            OpenCards(Kind).MainSwap.Visibility = Visibility.Collapsed
                            Panel.Children.Add(New MyHint With {.Theme = MyHint.Themes.Blue, .Text = Kind & " 暂不支持 Minecraft " & Game})
                            Return
                        End If
                        OpenAvailable(Kind) = True
                        OpenStatuses(Kind).Text = "可以添加"
                        OpenCards(Kind).MainSwap.Visibility = Visibility.Visible
                        For Each Release As JObject In Worker.Output.Take(100)
                            Dim Version = OpenVersion(Kind, Release)
                            Dim Row As New MyListItem With {.Title = Kind & " " & Version, .Info = "Minecraft " & Game, .Type = MyListItem.CheckType.Clickable, .Height = 42, .Tag = Release}
                            AddHandler Row.Click, Sub() ChooseOpenLoader(Kind, Release)
                            Panel.Children.Add(Row)
                        Next
                    ElseIf State = LoadState.Failed Then
                        Panel.Children.Clear()
                        OpenAvailable(Kind) = False
                        OpenStatuses(Kind).Text = "获取失败"
                        OpenCards(Kind).IsSwapped = True
                        OpenCards(Kind).MainSwap.Visibility = Visibility.Collapsed
                        Panel.Children.Add(New MyHint With {.Text = "获取失败：" & Worker.Error.Message})
                        Dim Retry As New MyButton With {.Text = "重试", .Width = 100, .Height = 32, .HorizontalAlignment = HorizontalAlignment.Left}
                        AddHandler Retry.Click, Sub() LoadOpenLoaders()
                        Panel.Children.Add(Retry)
                    End If
                End Sub
            OpenTasks.Add(Worker) : Worker.Start(Game)
        Next
    End Sub

    Private Shared Function OpenVersion(Kind As String, Release As JObject) As String
        Select Case Kind
            Case "Legacy Fabric" : Return CStr(Release("loader")("version"))
            Case "Cleanroom" : Return CStr(Release("version"))
            Case Else : Return CStr(Release("labyModVersion"))
        End Select
    End Function
    Private Sub ChooseOpenLoader(Kind As String, Release As JObject)
        SelectedOptiFine = Nothing : SelectedLiteLoader = Nothing : SelectedForge = Nothing : SelectedNeoForge = Nothing
        SelectedFabric = Nothing : SelectedFabricApi = Nothing : SelectedOptiFabric = Nothing
        OpenLoaderKind = Kind : OpenLoaderRelease = Release
        OpenCards(Kind).IsSwapped = True
        ReloadSelected() : UpdateOpenCardTitles() : SelectNameUpdate()
    End Sub
    Private Sub UpdateOpenCardTitles()
        For Each Pair In OpenCards
            Dim IsSelected = Pair.Key = OpenLoaderKind AndAlso OpenLoaderRelease IsNot Nothing
            Pair.Value.Title = Pair.Key
            OpenClearButtons(Pair.Key).Visibility = If(IsSelected, Visibility.Visible, Visibility.Collapsed)
            If IsSelected Then
                OpenStatuses(Pair.Key).Text = OpenVersion(Pair.Key, OpenLoaderRelease)
            ElseIf OpenAvailable(Pair.Key) Then
                OpenStatuses(Pair.Key).Text = "可以添加"
            End If
        Next
    End Sub
    Private Sub ReconcileOpenSelection()
        If OpenLoaderKind IsNot Nothing AndAlso (SelectedOptiFine IsNot Nothing OrElse SelectedLiteLoader IsNot Nothing OrElse SelectedForge IsNot Nothing OrElse SelectedNeoForge IsNot Nothing OrElse SelectedFabric IsNot Nothing) Then
            OpenLoaderKind = Nothing : OpenLoaderRelease = Nothing : UpdateOpenCardTitles()
        End If
    End Sub
    Private Sub CancelOpenLoaders()
        OpenEpoch += 1
        For Each Task In OpenTasks : Task.Cancel() : Next
        OpenLoaderKind = Nothing : OpenLoaderRelease = Nothing
        UpdateOpenCardTitles()
    End Sub
End Class
