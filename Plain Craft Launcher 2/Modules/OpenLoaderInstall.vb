Imports System.IO
Imports PCLCS.Open

Public Module OpenLoaderInstall
    Public Sub Start(Kind As String, Game As String, Release As JObject, Id As String)
        If String.IsNullOrWhiteSpace(Game) Then Throw New ArgumentException("请填写 Minecraft 版本。")
        If String.IsNullOrWhiteSpace(Id) Then Return
        If Id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 OrElse Id = "." OrElse Id = ".." Then Throw New IOException("版本名称无效。")
        Dim Target = Path.Combine(McFolderSelected, "versions", Id) & "\"
        If Directory.Exists(Target) OrElse File.Exists(Target.TrimEnd("\"c)) Then Throw New IOException("该版本已存在。")
        Directory.CreateDirectory(Target)
        Dim Instance As New McInstance(Target)
        Dim Work As New List(Of LoaderBase)
        Dim Installer As String = Nothing
        If Kind = "Cleanroom" Then
            If String.IsNullOrWhiteSpace(CStr(Release("sha256"))) Then Throw New IOException("官方尚未提供此安装器的 SHA256，请选择包含摘要的版本。")
            Installer = Path.Combine(PathTemp, "OpenInstallers", CStr(Release("name")))
            Work.Add(New LoaderDownload("下载并校验 Cleanroom 安装器", {New NetFile({CStr(Release("url"))}, Installer, New FileChecker With {.Hash = CStr(Release("sha256")), .ActualSize = CLng(Release("size"))})}))
        End If
        Work.Add(New LoaderTask(Of String, List(Of NetFile))("获取加载器与 Mojang 元数据",
            Sub(Task)
                Dim Json As JObject
                Select Case Kind
                    Case "Legacy Fabric" : Json = LoaderCatalog.LegacyProfile(Game, Id, CStr(Release("loader")("version")))
                    Case "Cleanroom" : Json = LoaderCatalog.CleanroomProfile(Installer, Id, McFolderSelected, CStr(Release("sha256")))
                    Case Else : Json = LoaderCatalog.LabyProfile(Game, Id, Release)
                End Select
                System.IO.File.WriteAllText(Target & Id & ".json", Json.ToString())
                Task.Output = New List(Of NetFile) From {DlClientJarGet(Instance, False)}
            End Sub))
        Work.Add(New LoaderDownload("下载游戏本体", New List(Of NetFile)))
        Work.AddRange(DlClientFix(Instance, True, False))
        Dim Loader As New LoaderCombo(Of String)(Id & " 安装", Work) With {.OnStateChanged =
            Sub(Task)
                LoaderStateChangedHintOnly(Task)
                If Task.State = LoadState.Finished Then McInstanceListLoader.Start(McFolderSelected, IsForceRestart:=True)
                If Task.State = LoadState.Failed OrElse Task.State = LoadState.Canceled Then
                    'Keep partial files for diagnosis/retry; never remove a path after installing to it.
                    Logger.Info("Open profile installation stopped: " & Id)
                End If
            End Sub}
        Loader.Start(Target) : LoaderTaskbarAdd(Loader) : FrmMain.BtnExtraDownload.ShowRefresh()
    End Sub
End Module
