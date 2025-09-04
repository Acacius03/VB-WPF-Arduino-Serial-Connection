Imports System.IO.Ports

Class MainWindow
    Private WithEvents SerialPort As SerialPort
    ' Backing field
    Private _isConnected As Boolean = False

    Private Property IsConnected As Boolean
        Get
            Return _isConnected
        End Get
        Set(value As Boolean)
            _isConnected = value

            BtnToggleOnOff.IsEnabled = _isConnected

            If Not _isConnected Then
                BtnToggleOnOff.IsChecked = False
                BtnToggleOnOff.Content = "Turn ON"
                BtnToggleOnOff.Background = New SolidColorBrush(Color.FromRgb(76, 175, 80)) ' Green
                TxtStatus.Text = "Disconnected"
            End If
        End Set
    End Property

    Private Sub LogMessage(source As String, message As String)
        Dim timestamp As String = DateTime.Now.ToString("HH:mm:ss")
        Dim portLabel As String = If(String.IsNullOrEmpty(source), "UNKNOWN", source)
        Dim line As String = $"[{timestamp}] {portLabel}: {message}"
        TxtData.AppendText(line & Environment.NewLine)
        TxtData.ScrollToEnd()
    End Sub

    Private Sub SerialWrite(text As String)
        If Not isConnected OrElse SerialPort Is Nothing OrElse Not SerialPort.IsOpen Then Return
        Try
            SerialPort.Write(text)
        Catch ex As Exception
            LogMessage("WRITE ERROR", ex.Message)
        End Try
    End Sub

    Private Sub TryConnect()
        Dim dlg As New SerialPortConnectorWindow()
        Dim result? As Boolean = dlg.ShowDialog()

        If result.HasValue AndAlso result.Value Then
            ' Update status with chosen port/baud
            If SerialPort IsNot Nothing AndAlso SerialPort.IsOpen Then
                SerialPort.Close()
            End If

            Try
                ' Create new serial connection
                SerialPort = New SerialPort(dlg.SelectedPort, dlg.SelectedBaud) With {
                    .NewLine = vbCrLf,
                    .ReadTimeout = 2000,
                    .WriteTimeout = 2000
                }
                SerialPort.Open()
                IsConnected = True
                TxtStatus.Text = $"Connected to {dlg.SelectedPort} at {dlg.SelectedBaud} baud"
                LogMessage("SYSTEM", $"Connected to {dlg.SelectedPort} at {dlg.SelectedBaud} baud")

            Catch ex As Exception
                TxtStatus.Text = "Connection Failed"
                LogMessage("ERROR", $"Could not connect: {ex.Message}")
                isConnected = False
            End Try
        Else
            TxtStatus.Text = "Disconnected"
            isConnected = False
        End If
    End Sub

    ' 🔹 Capture Arduino output
    Private Sub SerialPort_DataReceived(sender As Object, e As SerialDataReceivedEventArgs) Handles SerialPort.DataReceived
        Try
            Dim raw As String = SerialPort.ReadLine().Trim()
            Dim portName As String = SerialPort.PortName
            Dispatcher.BeginInvoke(
                Sub()
                    LogMessage(portName, raw)
                End Sub
            )
        Catch ex As Exception
            Dispatcher.BeginInvoke(
                Sub()
                    LogMessage("READ ERROR", ex.Message)
                End Sub
            )
        End Try
    End Sub

    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs)
        TryConnect()
    End Sub

    Private Sub BtnToggleOnOff_Checked(sender As Object, e As RoutedEventArgs)
        BtnToggleOnOff.Content = "Turn OFF"
        BtnToggleOnOff.Background = New SolidColorBrush(Color.FromRgb(229, 57, 53)) ' 🔴 Red
        LogMessage("INFO", "Sent ON")
        SerialWrite("ON")
    End Sub

    Private Sub BtnToggleOnOff_Unchecked(sender As Object, e As RoutedEventArgs)
        BtnToggleOnOff.Content = "Turn ON"
        BtnToggleOnOff.Background = New SolidColorBrush(Color.FromRgb(76, 175, 80)) ' 🟢 Green
        LogMessage("INFO", "Sent OFF")
        SerialWrite("OFF")
    End Sub

    Private Sub BtnOpenConnector_Click(sender As Object, e As RoutedEventArgs)
        TryConnect()
    End Sub

    Private Sub Window_Closing(sender As Object, e As ComponentModel.CancelEventArgs)
        If SerialPort IsNot Nothing AndAlso SerialPort.IsOpen Then
            Try
                SerialPort.Close()
                LogMessage("INFO", "Disconnected on window close")
                IsConnected = False
            Catch ex As Exception
                LogMessage("ERROR", $"Error during disconnect: {ex.Message}")
            End Try
        End If
    End Sub
End Class
