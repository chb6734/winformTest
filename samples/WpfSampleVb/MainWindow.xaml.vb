Imports System.Windows

Class MainWindow
    Private Sub OnAdd(sender As Object, e As RoutedEventArgs)
        Dim a As Double, b As Double
        Double.TryParse(txtA.Text, a)
        Double.TryParse(txtB.Text, b)
        lblResult.Text = "결과: " & (a + b).ToString()
    End Sub

    Private Sub OnSub(sender As Object, e As RoutedEventArgs)
        Dim a As Double, b As Double
        Double.TryParse(txtA.Text, a)
        Double.TryParse(txtB.Text, b)
        lblResult.Text = "결과: " & (a - b).ToString()
    End Sub
End Class
