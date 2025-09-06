Imports System.IO.Ports

Class MainWindow
    Private WithEvents SerialPort As SerialPort
    Private ReadOnly Property IsConnected As Boolean
        Get
            Return SerialPort IsNot Nothing AndAlso SerialPort.IsOpen
        End Get
    End Property
    Private Sub UpdateUiForConnection()
        BtnToggleOnOff.IsEnabled = IsConnected

        Dim Status As String = "Disconnected"
        If IsConnected Then
            Status = $"Connected to {SerialPort?.PortName} at {SerialPort?.BaudRate} baud"
        Else
            BtnToggleOnOff.IsChecked = False
        End If

        TxtStatus.Text = Status
        LogMessage("SYSTEM", Status)
    End Sub

    Private Sub LogMessage(source As String, message As String)
        Dim timestamp As String = DateTime.Now.ToString("HH:mm:ss")
        Dim portLabel As String = If(String.IsNullOrEmpty(source), "UNKNOWN", source)
        Dim line As String = $"[{timestamp}] {portLabel}: {message}"
        TxtData.AppendText(line & Environment.NewLine)
        TxtData.ScrollToEnd()
    End Sub

    Private Sub SerialWrite(text As String)
        If Not IsConnected Then Return
        Try
            SerialPort.Write(text)
            LogMessage("WRITE", text)
        Catch ex As Exception
            LogMessage("WRITE ERROR", ex.Message)
        End Try
    End Sub

    Private Sub TryConnect()
        Dim dlg As New SerialPortConnectorWindow()
        Dim result? As Boolean = dlg.ShowDialog()

        If result.GetValueOrDefault() Then
            ConnectSerialPort(dlg.SelectedPort, dlg.SelectedBaud)
        End If
        UpdateUiForConnection()
    End Sub

    Private Sub ConnectSerialPort(portName As String, baudRate As Integer)
        If IsConnected Then SerialPort.Close()
        Try
            SerialPort = New SerialPort(portName, baudRate) With {
                .NewLine = vbCrLf,
                .ReadTimeout = 2000,
                .WriteTimeout = 2000
            }
            SerialPort.Open()
        Catch ex As Exception
            LogMessage("ERROR", $"Could not connect: {ex.Message}")
        End Try
    End Sub

    Private Sub SerialPort_DataReceived(sender As Object, e As SerialDataReceivedEventArgs) Handles SerialPort.DataReceived
        Try
            Dim raw As String = SerialPort.ReadLine().Trim()
            Dim portName As String = If(SerialPort IsNot Nothing, SerialPort.PortName, "UNKNOWN")
            Dispatcher.BeginInvoke(Sub() LogMessage(portName, raw))
        Catch ex As Exception
            Dispatcher.BeginInvoke(Sub() LogMessage("READ ERROR", ex.Message))
        End Try
    End Sub

    Private Sub BtnToggleOnOff_Checked(sender As Object, e As RoutedEventArgs)
        SerialWrite("ON")
    End Sub

    Private Sub BtnToggleOnOff_Unchecked(sender As Object, e As RoutedEventArgs)
        SerialWrite("OFF")
    End Sub

    Private Sub BtnOpenConnector_Click(sender As Object, e As RoutedEventArgs)
        TryConnect()
    End Sub

    Private Sub Window_Closing(sender As Object, e As ComponentModel.CancelEventArgs)
        If Not IsConnected Then Return
        Try
            SerialPort.Close()
            LogMessage("INFO", "Disconnected on window close")
        Catch ex As Exception
            LogMessage("ERROR", $"Error during disconnect: {ex.Message}")
        End Try
        UpdateUiForConnection()
    End Sub
End Class
