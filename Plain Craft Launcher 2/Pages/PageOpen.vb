Imports PCLCS.Open
Imports System.IO

' All interactive controls and navigation are the original PCL APIs.
Public Class OpenPage
    Inherits MyPageRight
    Protected ReadOnly Body As New StackPanel With {.Margin = New Thickness(25, 25, 25, 10)}
    Public Sub New()
        Child = New MyScrollViewer With {.Content = Body, .VerticalScrollBarVisibility = ScrollBarVisibility.Auto, .HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled}
    End Sub
    Protected Function Card(Title As String) As StackPanel
        Dim C As New MyCard With {.Title = Title, .Margin = New Thickness(0, 0, 0, 15)}
        Dim P As New StackPanel With {.Margin = New Thickness(20, 42, 20, 18)}
        C.Children.Add(P) : Body.Children.Add(C) : Return P
    End Function
    Protected Function Label(Panel As Panel, Text As String) As TextBlock
        Dim T As New TextBlock With {.Text = Text, .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 0, 0, 12)}
        Panel.Children.Add(T) : Return T
    End Function
    Protected Function Button(Panel As Panel, Text As String, Action As Action) As MyButton
        Dim B As New MyButton With {.Text = Text, .Height = 35, .MinWidth = 110, .HorizontalAlignment = HorizontalAlignment.Left, .Padding = New Thickness(15, 0, 15, 0), .Margin = New Thickness(0, 0, 12, 10)}
        AddHandler B.Click, Sub(Sender, E)
                                Try
                                    Action()
                                Catch Ex As Exception
                                    MyMsgBox(Ex.Message, OpenAppearance.Tr("操作失败"))
                                End Try
                            End Sub
        Panel.Children.Add(B) : Return B
    End Function
    Protected Function Input(Panel As Panel, HintText As String) As MyTextBox
        Label(Panel, HintText)
        Dim Box As New MyTextBox With {.Height = 30, .Margin = New Thickness(0, 0, 0, 12)}
        Panel.Children.Add(Box) : Return Box
    End Function
End Class

Public Class PageOpenSettings
    Inherits OpenPage
    Public Sub New()
        Dim Account = Card("正版登录与资源平台")
        Label(Account, "微软登录默认使用内置公共 Client ID；也可填写自己的应用 ID。登录仍在微软官网完成。")
        Dim Client = Input(Account, "Microsoft Client ID") : Client.Text = OpenPreferences.ClientId
        Label(Account, "CurseForge API Key（留空保留已保存的密钥）")
        Dim Key As New MyTextBox With {.Height = 30, .Margin = New Thickness(0, 0, 0, 12)} : Account.Children.Add(Key)
        Dim Status = Label(Account, If(String.IsNullOrEmpty(OpenPreferences.CurseForgeKey), "CurseForge 默认使用公益镜像，无需填写 API Key。Modrinth 仍为默认资源平台。", "已保存自定义官方 API Key。Modrinth 仍为默认资源平台。"))
        Dim Mirror As New MyCheckBox With {.Text = "CurseForge 使用公益镜像（无需 API Key）", .Checked = OpenPreferences.CurseForgeMirror, .Margin = New Thickness(0, 0, 0, 12)}
        Account.Children.Add(Mirror)
        AddHandler Mirror.Change, Sub() OpenPreferences.SaveCommunityMode(Mirror.Checked)
        If OpenPreferences.CredentialError <> "" Then Label(Account, OpenPreferences.CredentialError)
        Label(Account, "凭据使用 Windows 当前用户 DPAPI 加密保存在本机，不写入源码或日志。环境变量配置优先。")
        Button(Account, "保存并应用", Sub()
                                      OpenPreferences.Save(OpenPreferences.Theme, OpenPreferences.Language, Client.Text, If(String.IsNullOrEmpty(Key.Text), OpenPreferences.CurseForgeKey, Key.Text))
                                      Key.Text = ""
                                      OpenAppearance.InitializeOpen()
                                      OpenAppearance.RefreshOpenAppearance()
                                      Status.Text = OpenAppearance.Tr("设置已保存")
                                  End Sub)
        Button(Account, "清除 CurseForge 密钥", Sub()
                                               OpenPreferences.Save(OpenPreferences.Theme, OpenPreferences.Language, OpenPreferences.ClientId, "")
                                               Key.Text = "" : OpenAppearance.InitializeOpen() : Status.Text = OpenAppearance.Tr("CurseForge 默认使用公益镜像，无需填写 API Key。Modrinth 仍为默认资源平台。")
                                           End Sub)
    End Sub
End Class

Public Class PageOpenWorld
    Inherits MyPageRight
    Public ReadOnly Content As PageResource
    Public Sub New()
        Content = New PageResource With {.PageType = ResourceTypes.Map, .TypeName = "世界", .TypeNameSpaced = "世界"}
        Content.SearchTags.Add(New MyComboBoxItem With {.Content = "全部", .Tag = "", .IsSelected = True})
        Child = Content
    End Sub
End Class

Public Class PageOpenFavorites
    Inherits OpenPage
    Public Sub New()
        AddHandler Loaded, Sub() RefreshList()
    End Sub
    Private Sub RefreshList()
        Body.Children.Clear()
        Dim Rows = Card("收藏夹")
        Dim Render As Action =
            Sub()
                Rows.Children.Clear()
                Try
                    For Each Json As JObject In Favorites.Read()
                        Dim Project As New ResourceProject(Json)
                        Dim Row As New MyLocalModItem With {.PresentationOnly = True, .Tag = Project}
                        Dim Titles = Project.GetControlTitle(False)
                        Row.SetPresentation(Titles.Title, Project.Platform.ToString() & " · " & Project.Description)
                        Row.SubTitle = Titles.SubTitle
                        Row.Tags = Project.Tags
                        Project.ApplyLogoToMyImage(Row.PathLogo)
                        Dim OpenDetail As Action = Sub() FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.ResourceDetail, .Additional = {Project, New List(Of String) From {Project.RawName}, "", ModLoaders.None, Project.Types}})
                        AddHandler Row.Click, Sub() OpenDetail()
                        Row.Buttons = {OpenResourceUi.Icon("查看并下载", OpenResourceUi.OpenLogo, OpenDetail), OpenResourceUi.Icon("取消收藏", OpenResourceUi.DeleteLogo, Sub()
                                                                                                                                                                 Favorites.Toggle(Project.ToJson()) : RefreshList()
                                                                                                                                                             End Sub)}
                        Rows.Children.Add(Row)
                    Next
                    If Rows.Children.Count = 0 Then Label(Rows, "暂无收藏。在社区资源详情页点击「收藏」即可添加。")
                Catch Ex As Exception
                    Label(Rows, OpenAppearance.Tr("收藏夹读取失败：") & Ex.Message)
                End Try
            End Sub
        Render()
    End Sub
End Class

Public Module OpenResourceUi
    Public Const OpenLogo As String = "F0 M0,4 H9 L12,7 H26 V24 H0 Z M2,9 V22 H24 V9 Z"
    Public Const DeleteLogo As String = "F0 M4,6 H22 V24 H4 Z M7,9 V21 H19 V9 Z M1,2 H25 V5 H1 Z M8,0 H18 V2 H8 Z"
    Public Const ExportLogo As String = "M10,0 H14 V12 L19,7 L22,10 L12,20 L2,10 L5,7 L10,12 Z M0,21 H24 V25 H0 Z"
    Public Const RenameLogo As String = "M1,17 L17,1 L23,7 L7,23 L0,24 Z M19,0 L24,5 L26,3 L21,-2 Z"
    Public Function Icon(Text As String, Logo As String, Action As Action) As MyIconButton
        Dim Btn As New MyIconButton With {.ToolTip = Text, .Logo = Logo, .Width = 25, .Height = 25}
        AddHandler Btn.Click, Sub()
                                 Try
                                     Action()
                                 Catch Ex As Exception
                                     MyMsgBox(Ex.Message, OpenAppearance.Tr("操作失败"))
                                 End Try
                             End Sub
        Return Btn
    End Function
End Module

Public Class PageOpenInstanceResources
    Inherits OpenPage
    Private ReadOnly Kind As String
    Private ReadOnly Subfolder As String
    Private ReadOnly Extensions As String()
    Public Sub New(ResourceKind As String)
        Kind = ResourceKind
        Select Case Kind
            Case "存档" : Subfolder = "saves" : Extensions = {".zip"}
            Case "截图" : Subfolder = "screenshots" : Extensions = {".png", ".jpg", ".jpeg"}
            Case "资源包" : Subfolder = "resourcepacks" : Extensions = {".zip"}
            Case "光影包" : Subfolder = "shaderpacks" : Extensions = {".zip"}
            Case Else : Throw New ArgumentException(ResourceKind)
        End Select
        AddHandler Loaded, Sub() RefreshList()
    End Sub
    Private Sub EnsureEditable(Entry As String)
        If McInstanceLock.IsLocked(PageInstanceLeft.Instance) Then Throw New IOException("该版本正在被修改，请稍后再试。")
        Dim LockFile = Path.Combine(Entry, "session.lock")
        If Directory.Exists(Entry) AndAlso File.Exists(LockFile) Then
            Using Stream As New FileStream(LockFile, FileMode.Open, FileAccess.Read, FileShare.None)
            End Using
        End If
    End Sub
    Private Sub RefreshList()
        Body.Children.Clear()
        If PageInstanceLeft.Instance Is Nothing Then Return
        Body.Margin = New Thickness(25, 10, 25, 10)
        Dim TargetFolder = Path.Combine(PageInstanceLeft.Instance.PathIndie, Subfolder)
        Directory.CreateDirectory(TargetFolder)
        Dim Search As New MySearchBox With {.HintText = "搜索" & Kind & "名称", .Margin = New Thickness(0, 15, 0, 15)}
        Body.Children.Add(Search)
        Dim Head = Card("")
        Head.Margin = New Thickness(15, 15, 15, 5)
        Dim Toolbar As New WrapPanel : Head.Children.Add(Toolbar)
        Button(Toolbar, "打开文件夹", Sub() OpenExplorer(TargetFolder))
        Button(Toolbar, "从文件安装", Sub()
                                          EnsureEditable(TargetFolder)
                                          Dim Files = Dialogs.SelectFile(OpenAppearance.Tr("选择资源文件"), True)
                                          If Files Is Nothing Then Return
                                          For Each Source In Files
                                              If Not Extensions.Contains(Path.GetExtension(Source).ToLowerInvariant()) Then Throw New IOException(OpenAppearance.Tr("不支持该文件类型：") & Path.GetExtension(Source))
                                              InstanceFiles.Import(Source, TargetFolder, Kind = "存档")
                                          Next
                                          RefreshList()
                                      End Sub)
        Button(Toolbar, "刷新", AddressOf RefreshList)
        Dim Rows = Card(Kind & "列表")
        Rows.Margin = New Thickness(20, 48, 18, 22)
        Dim Render As Action =
            Sub()
                Rows.Children.Clear()
                Dim Entries = If(Kind = "存档", Directory.GetDirectories(TargetFolder).Where(Function(D) File.Exists(Path.Combine(D, "level.dat"))),
                    Directory.GetFiles(TargetFolder).Where(Function(F) Extensions.Contains(Path.GetExtension(F).ToLowerInvariant())).Concat(If(Kind = "截图", Enumerable.Empty(Of String)(), Directory.GetDirectories(TargetFolder))))
                For Each Entry In Entries.OrderByDescending(Function(F) File.GetLastWriteTime(F))
                    If Search.Text <> "" AndAlso Path.GetFileName(Entry).IndexOf(Search.Text, StringComparison.OrdinalIgnoreCase) < 0 Then Continue For
                    Dim Row As New MyLocalModItem With {.PresentationOnly = True, .Tag = Entry}
                    Dim Logo = If(Kind = "截图", Entry, If(Kind = "资源包", ResourcePackLogo(Entry), PathImage & If(Kind = "存档", "Blocks/Grass.png", "Blocks/GoldBlock.png")))
                    Row.SetPresentation(If(File.Exists(Entry), Path.GetFileNameWithoutExtension(Entry), Path.GetFileName(Entry)), Path.GetFileName(Entry) & " · " & File.GetLastWriteTime(Entry).ToString("yyyy-MM-dd HH:mm") & If(File.Exists(Entry), " · " & New FileInfo(Entry).Length.ToString("N0") & " B", ""), Logo)
                    AddHandler Row.Click, Sub() Row.Checked = Not Row.Checked
                    Row.Buttons = {
                        OpenResourceUi.Icon(If(Kind = "截图", "查看截图", "打开位置"), OpenResourceUi.OpenLogo,
                            Sub()
                                If Kind = "截图" Then
                                    Process.Start(New ProcessStartInfo(Entry) With {.UseShellExecute = True})
                                Else
                                    OpenExplorer(Entry)
                                End If
                            End Sub),
                        OpenResourceUi.Icon("导出", OpenResourceUi.ExportLogo,
                            Sub()
                                Dim Destination = Dialogs.SaveFile(OpenAppearance.Tr("选择保存位置"), Path.GetFileName(Entry) & If(Directory.Exists(Entry), ".zip", ""))
                                If String.IsNullOrEmpty(Destination) Then Return
                                EnsureEditable(Entry) : InstanceFiles.Export(Entry, Destination)
                                Hint(OpenAppearance.Tr("导出完成"))
                            End Sub),
                        OpenResourceUi.Icon("重命名", OpenResourceUi.RenameLogo,
                            Sub()
                                EnsureEditable(Entry)
                                Dim NewName = MyMsgBoxInput(OpenAppearance.Tr("重命名"), "", Path.GetFileName(Entry))
                                If String.IsNullOrWhiteSpace(NewName) OrElse NewName = Path.GetFileName(Entry) Then Return
                                If NewName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 OrElse NewName = "." OrElse NewName = ".." Then Throw New IOException("文件名无效。")
                                Dim Destination = Path.Combine(TargetFolder, NewName)
                                If Directory.Exists(Entry) Then Directory.Move(Entry, Destination) Else File.Move(Entry, Destination)
                                RefreshList()
                            End Sub),
                        OpenResourceUi.Icon("移到回收站", OpenResourceUi.DeleteLogo,
                            Sub()
                                EnsureEditable(Entry)
                                If MyMsgBox(OpenAppearance.Tr("将此资源移到回收站？") & vbCrLf & Path.GetFileName(Entry), OpenAppearance.Tr("确认"), "确定", "取消") <> 1 Then Return
                                If Directory.Exists(Entry) Then
                                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(Entry, FileIO.UIOption.OnlyErrorDialogs, FileIO.RecycleOption.SendToRecycleBin)
                                Else
                                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(Entry, FileIO.UIOption.OnlyErrorDialogs, FileIO.RecycleOption.SendToRecycleBin)
                                End If
                                RefreshList()
                            End Sub)}
                    Rows.Children.Add(Row)
                Next
                If Rows.Children.Count = 0 Then Label(Rows, "暂无资源")
            End Sub
        AddHandler Search.TextChanged, Sub() Render()
        Render()
    End Sub

    Private Shared Function ResourcePackLogo(Entry As String) As String
        Try
            If Directory.Exists(Entry) Then
                Dim Image = Path.Combine(Entry, "pack.png")
                If File.Exists(Image) Then Return Image
            ElseIf Path.GetExtension(Entry).Equals(".zip", StringComparison.OrdinalIgnoreCase) Then
                Dim Cache = Path.Combine(PathTemp, "Cache", "ResourcePackIcons")
                Directory.CreateDirectory(Cache)
                Dim Fingerprint = Entry & "|" & File.GetLastWriteTimeUtc(Entry).Ticks & "|" & New FileInfo(Entry).Length
                Dim Hash = System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(Fingerprint))
                Dim Image = Path.Combine(Cache, BitConverter.ToString(Hash).Replace("-", "") & ".png")
                If File.Exists(Image) Then Return Image
                Using Archive = System.IO.Compression.ZipFile.OpenRead(Entry)
                    Dim Icon = Archive.GetEntry("pack.png")
                    If Icon IsNot Nothing AndAlso Icon.Length > 0 AndAlso Icon.Length <= 1024 * 1024 Then
                        Using Input = Icon.Open(), Output = New FileStream(Image, FileMode.CreateNew, FileAccess.Write)
                            Dim Buffer(8191) As Byte
                            Dim Length As Integer, Total As Long = 0
                            Do
                                Length = Input.Read(Buffer, 0, Buffer.Length)
                                If Length = 0 Then Exit Do
                                Total += Length
                                If Total > 1024 * 1024 Then Throw New InvalidDataException("pack.png 过大")
                                Output.Write(Buffer, 0, Length)
                            Loop
                        End Using
                        Return Image
                    End If
                End Using
            End If
        Catch
            'A missing or malformed pack icon must not hide the pack itself.
        End Try
        Return PathImage & "Blocks/CobbleStone.png"
    End Function
End Class
