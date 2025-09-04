Imports System.IO.Ports
Imports System.Windows.Threading

Public Class SerialPortConnectorWindow
    Public Property SelectedPort As String
    Public Property SelectedBaud As Integer

    Private portRefreshTimer As DispatcherTimer

    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs)
        RefreshPorts()

        portRefreshTimer = New DispatcherTimer()
        portRefreshTimer.Interval = TimeSpan.FromSeconds(1)
        AddHandler portRefreshTimer.Tick, AddressOf PortRefreshTimer_Tick
        portRefreshTimer.Start()
    End Sub

    Private Sub PortRefreshTimer_Tick(sender As Object, e As EventArgs)
        RefreshPorts()
    End Sub

    Private Sub RefreshPorts()
        Dim currentPorts = SerialPort.GetPortNames()
        Dim selected As String = Nothing

        If CboPorts.SelectedItem IsNot Nothing Then
            selected = CboPorts.SelectedItem.ToString()
        End If

        If Not currentPorts.SequenceEqual(CboPorts.Items.Cast(Of String)()) Then
            CboPorts.ItemsSource = currentPorts

            If selected IsNot Nothing AndAlso currentPorts.Contains(selected) Then
                CboPorts.SelectedItem = selected
            ElseIf CboPorts.Items.Count > 0 Then
                CboPorts.SelectedIndex = 0
            End If
        End If
    End Sub

    Private Sub BtnConnect_Click(sender As Object, e As RoutedEventArgs)
        If CboPorts.SelectedItem Is Nothing OrElse CboBaudRates.SelectedItem Is Nothing Then
            MessageBox.Show("Please select a port and baud rate.", "Missing Info",
                            MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim portName As String = CboPorts.SelectedItem.ToString()
        Dim baudRate As Integer = Integer.Parse(CType(CboBaudRates.SelectedItem, ComboBoxItem).Content.ToString())

        Try
            Using sp As New SerialPort(portName, baudRate)
                sp.Open()
                SelectedPort = portName
                SelectedBaud = baudRate
            End Using

            Me.DialogResult = True
            Me.Close()
        Catch ex As Exception
            MessageBox.Show("Error connecting: " & ex.Message,
                            "Connection Failed",
                            MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Sub BtnCancel_Click(sender As Object, e As RoutedEventArgs)
        Me.DialogResult = False
        Me.Close()
    End Sub

    Private Sub Window_Closing(sender As Object, e As ComponentModel.CancelEventArgs) Handles Me.Closing
        portRefreshTimer?.Stop()
    End Sub
End Class
