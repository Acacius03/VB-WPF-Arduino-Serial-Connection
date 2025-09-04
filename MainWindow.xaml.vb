Imports System.IO.Ports

Class MainWindow
    Private WithEvents SerialPort As SerialPort
    Private isConnected As Boolean = False

    Private Sub LogMessage(source As String, message As String)
        Dim timestamp As String = DateTime.Now.ToString("HH:mm:ss")
        Dim portLabel As String = If(String.IsNullOrEmpty(source), "UNKNOWN", source)
        Dim line As String = $"[{timestamp}] {portLabel}: {message}"
        TxtData.AppendText(line & Environment.NewLine)
        TxtData.ScrollToEnd()
    End Sub

    Private Sub PopulatePorts()
        Dim ports() As String = SerialPort.GetPortNames()
        CboPorts.ItemsSource = ports
        If ports.Length = 0 Then LogMessage("INFO", "No serial ports found.")
    End Sub

    Private Sub SerialWrite(text As String)
        If Not isConnected OrElse SerialPort Is Nothing OrElse Not SerialPort.IsOpen Then Return
        Try
            SerialPort.Write(text)
        Catch ex As Exception
            LogMessage("WRITE ERROR", ex.Message)
        End Try
    End Sub

    ' 🔹 Capture Arduino output
    Private Sub SerialPort_DataReceived(sender As Object, e As SerialDataReceivedEventArgs) Handles SerialPort.DataReceived
        Try
            Dim raw As String = SerialPort.ReadLine().Trim()
            Dispatcher.BeginInvoke(
                Sub()
                    LogMessage(CboPorts.SelectedItem?.ToString(), raw)
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
        Dim baudRates As Integer() = {9600, 19200, 38400, 57600, 115200}
        CboBaudRates.ItemsSource = baudRates
        PopulatePorts()
    End Sub

    Private Sub BtnScanPorts_Click(sender As Object, e As RoutedEventArgs)
        PopulatePorts()
    End Sub

    Private Sub BtnConnectDisconnect_Click(sender As Object, e As RoutedEventArgs)
        If Not isConnected Then
            ' 🔹 If no port or baud rate is chosen, stop
            If CboPorts.SelectedItem Is Nothing Then
                LogMessage("WARNING", "No port selected.")
                Return
            End If
            If CboBaudRates.SelectedItem Is Nothing Then
                LogMessage("WARNING", "No baud rate selected.")
                Return
            End If

            Try
                SerialPort = New SerialPort(CboPorts.SelectedItem.ToString(), CInt(CboBaudRates.SelectedItem)) With {
                    .NewLine = vbLf
                }
                SerialPort.Open()
                isConnected = True
                BtnConnectDisconnect.Content = "Disconnect"
                LogMessage("INFO", $"Connected at {CboPorts.SelectedItem}, baud: {CboBaudRates.SelectedItem}")
            Catch ex As Exception
                LogMessage("ERROR", "Failed to connect: " & ex.Message)
            End Try
        Else
            Try
                If SerialPort IsNot Nothing Then
                    If SerialPort.IsOpen Then SerialPort.Close()
                    SerialPort.Dispose()
                    SerialPort = Nothing
                End If
                isConnected = False
                BtnConnectDisconnect.Content = "Connect"
                LogMessage("INFO", "Disconnected")
            Catch ex As Exception
                LogMessage("ERROR", "Failed to disconnect: " & ex.Message)
            End Try
        End If
    End Sub

    Private Sub BtnOff_Click(sender As Object, e As RoutedEventArgs)
        LogMessage("INFO", "Sent OFF")
        SerialWrite("OFF")
    End Sub

    Private Sub BtnOn_Click(sender As Object, e As RoutedEventArgs)
        LogMessage("INFO", "Sent ON")
        SerialWrite("ON")
    End Sub

End Class
