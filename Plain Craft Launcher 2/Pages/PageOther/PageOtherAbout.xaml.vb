Public Class PageOtherAbout
    Private Sub PageOtherAbout_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PanBack.ScrollToHome()
    End Sub
    Private Sub LinkOriginalSponsor_RequestNavigate(Sender As Object, Args As Navigation.RequestNavigateEventArgs) Handles LinkOriginalSponsor.RequestNavigate
        OpenWebsite(Args.Uri.AbsoluteUri)
        Args.Handled = True
    End Sub
End Class
