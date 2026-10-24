Imports System.IO

Public Class PageOtherTest
    Private Sub OpenCache(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenCache.Click
        Directory.CreateDirectory(PathTemp)
        OpenExplorer(PathTemp)
    End Sub

    Private Sub OpenLauncher(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenLauncher.Click
        OpenExplorer(Path.GetDirectoryName(PathExe))
    End Sub

    Private Sub Optimize(sender As Object, e As MouseButtonEventArgs) Handles BtnOptimizeMemory.Click
        MemoryOptimize(True)
    End Sub

    Private Sub DailyTip(sender As Object, e As MouseButtonEventArgs) Handles BtnDailyTip.Click
        Jrrp()
    End Sub

    Private Sub VerifiedDownload(sender As Object, e As MouseButtonEventArgs) Handles BtnVerifiedDownload.Click
        Dim Address = InputDownloadUrl.Text.Trim()
        Dim Digest = InputDownloadHash.Text.Trim()
        Dim Source As Uri = Nothing
        If Not Uri.TryCreate(Address, UriKind.Absolute, Source) OrElse Source.Scheme <> Uri.UriSchemeHttps Then
            MyMsgBox("请输入有效的 HTTPS 下载地址。", "校验下载") : Return
        End If
        If Not System.Text.RegularExpressions.Regex.IsMatch(Digest, "\A[0-9a-fA-F]{64}\z") Then
            MyMsgBox("请输入发布者提供的 64 位 SHA256 校验值。", "校验下载") : Return
        End If
        Dim DefaultName = Path.GetFileName(Source.LocalPath)
        If String.IsNullOrWhiteSpace(DefaultName) OrElse DefaultName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 Then DefaultName = "download.bin"
        Dim Target = Dialogs.SaveFile(OpenAppearance.Tr("选择保存位置"), DefaultName)
        If String.IsNullOrWhiteSpace(Target) Then Return
        If File.Exists(Target) Then
            MyMsgBox("目标文件已存在，请另选位置。", "校验下载") : Return
        End If
        Dim Temporary = Target & "." & Guid.NewGuid().ToString("N") & ".pclo-tmp"
        RunInNewThread(Sub()
                           Try
                               NetDownloadByLoader({Address}, Temporary, Check:=New FileChecker With {.Hash = Digest})
                               Using Hasher = System.Security.Cryptography.SHA256.Create(), Stream = File.OpenRead(Temporary)
                                   Dim Actual = BitConverter.ToString(Hasher.ComputeHash(Stream)).Replace("-", "")
                                   If Not Actual.Equals(Digest, StringComparison.OrdinalIgnoreCase) Then Throw New InvalidDataException("SHA256 校验失败。")
                               End Using
                               File.Move(Temporary, Target)
                               Hint("文件下载并校验完成。")
                           Catch Ex As Exception
                               Logger.Warn(Ex, "百宝箱校验下载失败")
                               Hint("下载失败：" & Ex.Message, HintType.Red)
                           Finally
                               If File.Exists(Temporary) Then File.Delete(Temporary)
                           End Try
                       End Sub, "PCLO Verified Download")
    End Sub

    Public Shared Sub StartCustomDownload(Url As String, FileName As String, Optional Folder As String = Nothing)
        Hint("请在百宝箱的校验下载中填写发布者 SHA256 后下载。")
    End Sub

    Public Shared Sub Jrrp()
        Hint("今日提示：" & DailyMessages(Date.Today.DayOfYear Mod DailyMessages.Length))
    End Sub

    Public Shared Sub RubbishClear()
        Directory.CreateDirectory(PathTemp)
        OpenExplorer(PathTemp)
        Hint("已打开缓存文件夹，请手动检查后清理。")
    End Sub

    Public Shared Sub MemoryOptimize(ShowHint As Boolean)
        GC.Collect()
        GC.WaitForPendingFinalizers()
        GC.Collect()
        If ShowHint Then Hint("内存整理完成。")
    End Sub

    Public Shared Sub MemoryOptimizeInternal(ShowHint As Boolean)
        MemoryOptimize(ShowHint)
    End Sub

    Public Shared Function GetRandomCave() As String
        Return DailyMessages(Date.Today.DayOfYear Mod DailyMessages.Length)
    End Function

    Public Shared Function GetRandomHint() As String
        Return DailyMessages((Date.Today.DayOfYear + 2) Mod DailyMessages.Length)
    End Function

    Public Shared Function GetRandomPresetHint() As String
        Return "随时可以在设置中调整启动器外观。"
    End Function
    Private Shared ReadOnly DailyMessages As String() = {
        "探索新的世界，发现新的故事。",
        "记得为重要存档做备份。",
        "资源包可以在版本设置的游戏资源中管理。",
        "安装 Mod 前先确认 Minecraft 与加载器版本。",
        "祝你今天游戏愉快！"
    }
End Class
