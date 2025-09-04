Imports System.IO.Ports
Imports System.Threading

Class MainWindow
    Private WithEvents serialPort As SerialPort
    Private currentPort As String = ""
    Private currentBaud As Integer = 0
    Private isConnected As Boolean = False

    Private Sub BtnScanPorts_Click(sender As Object, e As RoutedEventArgs)
        Dim ports() As String = SerialPort.GetPortNames()
        If ports.Length = 0 Then
            MessageBox.Show("No serial ports found.")
        Else
            CboPorts.ItemsSource = ports
        End If
    End Sub

    Private Sub CboPorts_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles CboPorts.SelectionChanged
        If CboPorts.SelectedItem Is Nothing Then Return
        Dim selectedPort As String = CboPorts.SelectedItem.ToString()
        Dim baudRates As Integer() = {9600, 19200, 38400, 57600, 115200}

        currentPort = ""
        currentBaud = 0

        For Each rate As Integer In baudRates
            Try
                Using sp As New SerialPort(selectedPort, rate)
                    sp.ReadTimeout = 500
                    sp.Open()
                    Thread.Sleep(200) ' give device time to send

                    Dim incoming As String = ""
                    Try
                        incoming = sp.ReadExisting()
                    Catch
                    End Try

                    If incoming.Length > 0 Then
                        MessageBox.Show($"✅ Device detected on {selectedPort} at {rate} baud." & vbCrLf & "Sample: " & incoming)
                        currentPort = selectedPort
                        currentBaud = rate
                        Return
                    End If
                End Using
            Catch
                ' ignore and try next baud
            End Try
        Next

        MessageBox.Show("❌ No response from device on " & selectedPort)
    End Sub

    Private Sub BtnConnectDisconnect_Click(sender As Object, e As RoutedEventArgs)
        If Not isConnected Then
            ' 🔹 If no port was chosen, use the first available one
            If currentPort = "" OrElse currentBaud = 0 Then
                If CboPorts.Items.Count > 0 Then
                    ' pick first item in ComboBox
                    CboPorts.SelectedIndex = 0
                    ' trigger detection routine
                    CboPorts_SelectionChanged(Nothing, Nothing)
                End If
            End If

            ' still no port? then error
            If currentPort = "" OrElse currentBaud = 0 Then
                MessageBox.Show("⚠️ Please select a valid port and detect baud rate first.")
                Return
            End If

            Try
                serialPort = New SerialPort(currentPort, currentBaud) With {
                .NewLine = vbLf ' Arduino usually ends with "\n"
            }
                serialPort.Open()
                isConnected = True
                BtnConnectDisconnect.Content = "Disconnect"
                LogMessage("INFO", $"Connected at {currentBaud} baud")
            Catch ex As Exception
                MessageBox.Show("❌ Failed to connect: " & ex.Message)
            End Try
        Else
            Try
                If serialPort IsNot Nothing AndAlso serialPort.IsOpen Then
                    serialPort.Close()
                End If
                isConnected = False
                BtnConnectDisconnect.Content = "Connect"
                LogMessage("INFO", "Disconnected")
            Catch ex As Exception
                MessageBox.Show("❌ Failed to disconnect: " & ex.Message)
            End Try
        End If
    End Sub


    ' 🔹 Helper: Log with timestamp + port name
    Private Sub LogMessage(source As String, message As String)
        Dim timestamp As String = DateTime.Now.ToString("HH:mm:ss")
        Dim portLabel As String = If(String.IsNullOrEmpty(source), "UNKNOWN", source)
        Dim line As String = $"[{timestamp}] {portLabel}: {message}"
        TxtData.AppendText(line & Environment.NewLine)
        TxtData.ScrollToEnd()
    End Sub

    ' 🔹 Capture Arduino output
    Private Sub serialPort_DataReceived(sender As Object, e As SerialDataReceivedEventArgs) Handles serialPort.DataReceived
        Try
            Dim raw As String = serialPort.ReadLine().Trim()
            Dispatcher.Invoke(Sub()
                                  LogMessage(currentPort, raw)
                              End Sub)
        Catch ex As Exception
            ' ignore timeouts / disconnects
        End Try
    End Sub

    ' 🔹 Detect spacebar key press and send to Arduino
    Private Sub Window_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
        If e.Key = Key.Space Then
            If isConnected AndAlso serialPort IsNot Nothing AndAlso serialPort.IsOpen Then
                serialPort.Write(" ") ' send space char (ASCII 32)
                LogMessage("PC", "Sent SPACE (toggle request)")
            End If
        End If
    End Sub

    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs)
        ' 🔹 Automatically scan ports when app starts
        BtnScanPorts_Click(Nothing, Nothing)

        ' If at least one port is found, preselect it
        If CboPorts.Items.Count > 0 Then
            CboPorts.SelectedIndex = 0
            ' run baud detection for the first port
            CboPorts_SelectionChanged(Nothing, Nothing)
        End If
    End Sub

    Private Sub BtnOff_Click(sender As Object, e As RoutedEventArgs)
        serialPort.Write("OFF")
    End Sub

    Private Sub BtnOn_Click(sender As Object, e As RoutedEventArgs)
        serialPort.Write("ON")
    End Sub
End Class
