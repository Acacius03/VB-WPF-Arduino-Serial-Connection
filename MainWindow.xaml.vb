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
            If currentPort = "" OrElse currentBaud = 0 Then
                MessageBox.Show("⚠️ Please select a valid port and detect baud rate first.")
                Return
            End If

            Try
                serialPort = New SerialPort(currentPort, currentBaud)
                serialPort.Open()
                isConnected = True
                BtnConnectDisconnect.Content = "Disconnect"
                MessageBox.Show($"🔌 Connected to {currentPort} at {currentBaud} baud.")
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
                MessageBox.Show("🔌 Disconnected.")
            Catch ex As Exception
                MessageBox.Show("❌ Failed to disconnect: " & ex.Message)
            End Try
        End If
    End Sub
End Class
