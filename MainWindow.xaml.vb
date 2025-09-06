Imports System.IO.Ports
Imports OxyPlot
Imports OxyPlot.Series
Imports OxyPlot.Axes

Class MainWindow
    Private WithEvents SerialPort As SerialPort
    Private ReadOnly Property IsConnected As Boolean
        Get
            Return SerialPort IsNot Nothing AndAlso SerialPort.IsOpen
        End Get
    End Property
    Private Sub UpdateUiForConnection()
        Dim Status As String = "Disconnected"

        If IsConnected Then
            Status = $"Connected to {SerialPort?.PortName} at {SerialPort?.BaudRate} baud"
        End If

        TxtStatus.Text = Status
        LogMessage("SYSTEM", Status)
    End Sub

    Public Property HumidityPlotModel As PlotModel
    Public Property TemperaturePlotModel As PlotModel

    Private HumiditySeries As LineSeries
    Private TemperatureSeries As LineSeries

    Private ChartLimit As Integer = 30
    Private ChartWindowSeconds As Integer = 60

    ' ----------------- Logging -----------------
    Private Sub LogMessage(source As String, message As String)
        Dim timestamp As String = DateTime.Now.ToString("HH:mm:ss")
        Dim line As String = $"[{timestamp}] {source}: {message}"
        TxtData.AppendText(line & Environment.NewLine)
        TxtData.ScrollToEnd()
    End Sub

    ' ----------------- Connection -----------------
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

    ' ----------------- Serial Handling -----------------
    Private Sub SerialPort_DataReceived(sender As Object, e As SerialDataReceivedEventArgs) Handles SerialPort.DataReceived
        Try
            Dim raw As String = SerialPort.ReadExisting()
            Dispatcher.BeginInvoke(Sub() ParseAndDisplayData(raw))
        Catch ex As Exception
            Dispatcher.BeginInvoke(Sub() LogMessage("READ ERROR", ex.Message))
        End Try
    End Sub

    Private Sub ParseAndDisplayData(data As String)
        Dim lines = data.Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
        For Each line In lines
            If line.StartsWith("H") Then
                Dim humVal As Double
                If Double.TryParse(Mid(line, 2), humVal) Then
                    HandleHumidity(humVal)
                Else
                    LogMessage("PARSE ERROR", $"Invalid humidity data: {line}")
                End If

            ElseIf line.StartsWith("T") Then
                Dim tempVal As Double
                If Double.TryParse(Mid(line, 2), tempVal) Then
                    HandleTemperature(tempVal)
                Else
                    LogMessage("PARSE ERROR", $"Invalid temperature data: {line}")
                End If
            End If
        Next
    End Sub

    ' ----------------- Humidity -----------------
    Private Sub HandleHumidity(humVal As Double)
        UpdateHumidityArc(humVal)

        Dim nowX As Double = DateTimeAxis.ToDouble(DateTime.Now)
        HumiditySeries.Points.Add(New DataPoint(nowX, humVal))

        If HumiditySeries.Points.Count > ChartLimit Then HumiditySeries.Points.RemoveAt(0)

        UpdateXAxis(HumidityPlotModel, nowX)
        HumidityPlotModel.InvalidatePlot(True)
    End Sub

    Private Sub UpdateHumidityArc(value As Double)
        value = Math.Max(0, Math.Min(100, value))
        If value <= 0 Then
            ArcHumidity.Data = Nothing
            TxtHumidityPercent.Text = "0%"
            Return
        End If

        Dim angle As Double = value * 360.0 / 100.0
        Dim radians As Double = (Math.PI / 180.0) * angle

        Dim container = GaugeHumidity
        Dim width As Double = If(container.ActualWidth > 0, container.ActualWidth, container.Width)
        Dim height As Double = If(container.ActualHeight > 0, container.ActualHeight, container.Height)
        If Double.IsNaN(width) OrElse Double.IsNaN(height) OrElse width = 0 OrElse height = 0 Then
            width = 160 : height = 160
        End If

        Dim strokeThickness As Double = ArcHumidity.StrokeThickness
        If strokeThickness <= 0 Then strokeThickness = 28

        Dim centerX As Double = width / 2.0
        Dim centerY As Double = height / 2.0
        Dim radius As Double = Math.Min(width, height) / 2.0 - strokeThickness / 2.0
        If radius < 0 Then radius = 0

        Dim startPoint As New Point(centerX, centerY - radius)
        Dim endPoint As New Point(centerX + radius * Math.Sin(radians), centerY - radius * Math.Cos(radians))
        Dim isLargeArc As Boolean = angle > 180.0

        Dim figure As New PathFigure() With {.StartPoint = startPoint}
        Dim segment As New ArcSegment() With {
            .Point = endPoint,
            .Size = New Size(radius, radius),
            .IsLargeArc = isLargeArc,
            .SweepDirection = SweepDirection.Clockwise
        }
        figure.Segments.Clear()
        figure.Segments.Add(segment)

        Dim geo As New PathGeometry()
        geo.Figures.Add(figure)
        ArcHumidity.Data = geo

        TxtHumidityPercent.Text = $"{Math.Round(value)}%"
    End Sub

    ' ----------------- Temperature -----------------
    ' ----------------- Temperature -----------------
    Private Sub HandleTemperature(tempVal As Double)
        TxtTemperature.Text = $"{tempVal} °C"

        ' Max heights from your XAML layout
        Dim hotMaxHeight As Double = 128  ' upper grid row
        Dim coldMaxHeight As Double = 32  ' lower grid row

        If tempVal > 0 Then
            ' Positive: hot bar fills up
            Dim h = MapVPB(tempVal, 0, 60, 0, hotMaxHeight)
            If h > hotMaxHeight Then h = hotMaxHeight
            RectangleHotTemp.Height = h
            RectangleColdTemp.Height = 0

        ElseIf tempVal < 0 Then
            ' Negative: cold bar fills down
            Dim h = MapVPB(tempVal, -20, 0, coldMaxHeight, 0)
            If h > coldMaxHeight Then h = coldMaxHeight
            RectangleColdTemp.Height = h
            RectangleHotTemp.Height = 0

        Else
            ' Exactly zero
            RectangleHotTemp.Height = 0
            RectangleColdTemp.Height = 0
        End If

        ' Add to temperature chart
        Dim nowX As Double = DateTimeAxis.ToDouble(DateTime.Now)
        TemperatureSeries.Points.Add(New DataPoint(nowX, tempVal))

        If TemperatureSeries.Points.Count > ChartLimit Then TemperatureSeries.Points.RemoveAt(0)

        UpdateXAxis(TemperaturePlotModel, nowX)
        TemperaturePlotModel.InvalidatePlot(True)
    End Sub


    ' ----------------- Axis Helper -----------------
    Private Sub UpdateXAxis(model As PlotModel, nowX As Double)
        Dim xAxis = TryCast(model.Axes.FirstOrDefault(Function(a) TypeOf a Is DateTimeAxis), DateTimeAxis)
        If xAxis IsNot Nothing Then
            xAxis.Maximum = nowX
            xAxis.Minimum = DateTimeAxis.ToDouble(DateTime.Now.AddSeconds(-ChartWindowSeconds))
        End If
    End Sub

    ' ----------------- UI Events -----------------
    Private Sub BtnSelectPort_Click(sender As Object, e As RoutedEventArgs)
        TryConnect()
    End Sub

    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        ' Humidity chart
        HumidityPlotModel = New PlotModel With {.Title = "Humidity (%)"}
        HumidityPlotModel.Axes.Add(New DateTimeAxis With {.Position = AxisPosition.Bottom, .StringFormat = "HH:mm:ss"})
        HumidityPlotModel.Axes.Add(New LinearAxis With {.Position = AxisPosition.Left, .Minimum = 0, .Maximum = 100})
        HumiditySeries = New LineSeries With {.Title = "Humidity"}
        HumidityPlotModel.Series.Add(HumiditySeries)
        HumidityPlot.Model = HumidityPlotModel

        ' Temperature chart
        TemperaturePlotModel = New PlotModel With {.Title = "Temperature (°C)"}
        TemperaturePlotModel.Axes.Add(New DateTimeAxis With {.Position = AxisPosition.Bottom, .StringFormat = "HH:mm:ss"})
        TemperaturePlotModel.Axes.Add(New LinearAxis With {.Position = AxisPosition.Left, .Minimum = -20, .Maximum = 60})
        TemperatureSeries = New LineSeries With {.Title = "Temperature"}
        TemperaturePlotModel.Series.Add(TemperatureSeries)
        TemperaturePlot.Model = TemperaturePlotModel
    End Sub

    Private Sub Window_Closing(sender As Object, e As ComponentModel.CancelEventArgs) Handles Me.Closing
        If SerialPort Is Nothing OrElse Not SerialPort.IsOpen Then Return
        DisconnectSerial()
    End Sub

    ' ----------------- Helpers -----------------
    Private Function MapVPB(X As Double, InMin As Double, InMax As Double, OutMin As Double, OutMax As Double) As Double
        Dim A As Double = X - InMin
        Dim B As Double = OutMax - OutMin
        A *= B
        B = InMax - InMin
        A /= B
        Return A + OutMin
    End Function
End Class
