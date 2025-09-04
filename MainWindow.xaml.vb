Imports System.Collections.ObjectModel
Imports System.IO.Ports
Imports LiveCharts

Class MainWindow

    Private WithEvents SerialPort As SerialPort
    Private _isConnected As Boolean = False

    Public Property HumidityValues As New ChartValues(Of Double)
    Public Property TemperatureValues As New ChartValues(Of Double)

    ' Temp / Hum
    Private TempResult As Double
    Private HumResult As Double
    Private ChartLimit As Integer = 30

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
                BtnToggleOnOff.Background = New SolidColorBrush(Color.FromRgb(76, 175, 80))
                TxtStatus.Text = "Disconnected"
            End If
        End Set
    End Property

    ' Logging helper
    Private Sub LogMessage(source As String, message As String)
        Dim timestamp As String = DateTime.Now.ToString("HH:mm:ss")
        Dim line As String = $"[{timestamp}] {source}: {message}"
        TxtData.AppendText(line & Environment.NewLine)
        TxtData.ScrollToEnd()
    End Sub

    ' Map function for vertical progress bar
    Private Function MapVPB(X As Double, InMin As Double, InMax As Double, OutMin As Double, OutMax As Double) As Double
        Dim A As Double = X - InMin
        Dim B As Double = OutMax - OutMin
        A *= B
        B = InMax - InMin
        A /= B
        Return A + OutMin
    End Function

    ' Serial write
    Private Sub SerialWrite(text As String)
        If Not IsConnected OrElse SerialPort Is Nothing OrElse Not SerialPort.IsOpen Then Return
        Try
            SerialPort.WriteLine(text)
        Catch ex As Exception
            LogMessage("WRITE ERROR", ex.Message)
        End Try
    End Sub

    ' Try connect
    Private Sub TryConnect()
        Dim dlg As New SerialPortConnectorWindow()
        Dim result? As Boolean = dlg.ShowDialog()

        If result.HasValue AndAlso result.Value Then
            If SerialPort IsNot Nothing AndAlso SerialPort.IsOpen Then SerialPort.Close()

            Try
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
                IsConnected = False
            End Try
        Else
            TxtStatus.Text = "Disconnected"
            IsConnected = False
        End If
    End Sub

    ' Serial data received
    Private Sub SerialPort_DataReceived(sender As Object, e As SerialDataReceivedEventArgs) Handles SerialPort.DataReceived
        Try
            Dim raw As String = SerialPort.ReadExisting()
            Dispatcher.BeginInvoke(Sub() ParseAndDisplayData(raw))
        Catch ex As Exception
            Dispatcher.BeginInvoke(Sub() LogMessage("READ ERROR", ex.Message))
        End Try
    End Sub

    Private Sub UpdateHumidityArc(value As Double)
        ' value = 0..100
        Dim angle As Double = value * 360 / 100
        Dim radians As Double = (Math.PI / 180) * angle
        Dim radius As Double = 27 ' Half of Grid size minus stroke

        Dim center As New Point(30, 30)
        Dim endPoint As New Point(center.X + radius * Math.Sin(radians),
                              center.Y - radius * Math.Cos(radians))

        Dim isLargeArc As Boolean = angle > 180

        Dim figure As New PathFigure() With {.StartPoint = New Point(center.X, center.Y - radius)}
        Dim segment As New ArcSegment() With {
        .Point = endPoint,
        .Size = New Size(radius, radius),
        .IsLargeArc = isLargeArc,
        .SweepDirection = SweepDirection.Clockwise
    }
        figure.Segments.Add(segment)

        Dim geo As New PathGeometry()
        geo.Figures.Add(figure)
        ArcHumidity.Data = geo

        ' Update text
        TxtHumidityPercent.Text = $"{Math.Round(value)}%"
    End Sub

    ' Parse Arduino data
    Private Sub ParseAndDisplayData(data As String)
        Dim lines = data.Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
        For Each line In lines
            If line.StartsWith("H") Then
                HumResult = CDbl(Mid(line, 2))
                UpdateHumidityArc(HumResult)

                ' Update Humidity chart
                If HumidityValues.Count >= ChartLimit Then HumidityValues.RemoveAt(0)
                HumidityValues.Add(HumResult)

            ElseIf line.StartsWith("T") Then
                TempResult = CDbl(Mid(line, 2))
                TxtTemperature.Text = $"{TempResult} °C"

                ' Map temperature to rectangle height (0–120 px)
                Dim maxHeight As Double = 120
                Dim vpb_sy = MapVPB(TempResult, -20, 60, 0, maxHeight)

                ' Clamp values
                If vpb_sy > maxHeight Then vpb_sy = maxHeight
                If vpb_sy < 0 Then vpb_sy = 0

                ' Update rectangle
                RectangleTemp.Height = vpb_sy

                ' Update Temperature chart
                If TemperatureValues.Count >= ChartLimit Then TemperatureValues.RemoveAt(0)
                TemperatureValues.Add(TempResult)
            End If
        Next
    End Sub

    ' Window loaded
    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs)
        ' Bind chart series to observable collections
        SeriesHumidity.Values = HumidityValues
        SeriesTemperature.Values = TemperatureValues
        TryConnect()
    End Sub

    ' Toggle ON
    Private Sub BtnToggleOnOff_Checked(sender As Object, e As RoutedEventArgs)
        BtnToggleOnOff.Content = "Turn OFF"
        BtnToggleOnOff.Background = New SolidColorBrush(Color.FromRgb(229, 57, 53))
        SerialWrite("ON")
        LogMessage("INFO", "Sent ON")
    End Sub

    ' Toggle OFF
    Private Sub BtnToggleOnOff_Unchecked(sender As Object, e As RoutedEventArgs)
        BtnToggleOnOff.Content = "Turn ON"
        BtnToggleOnOff.Background = New SolidColorBrush(Color.FromRgb(76, 175, 80))
        SerialWrite("OFF")
        LogMessage("INFO", "Sent OFF")
    End Sub

    ' Connect button
    Private Sub BtnOpenConnector_Click(sender As Object, e As RoutedEventArgs)
        TryConnect()
    End Sub

    ' Window closing
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
