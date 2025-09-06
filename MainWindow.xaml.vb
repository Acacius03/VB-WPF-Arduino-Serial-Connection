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
        Dim status As String = If(IsConnected,
                                  $"Connected to {SerialPort?.PortName} at {SerialPort?.BaudRate} baud",
                                  "Disconnected")

        TxtStatus.Text = status
        LogMessage("SYSTEM", status)
    End Sub

    Private Sub AnimateBar(rect As Rectangle, targetHeight As Double)
        Dim anim As New Animation.DoubleAnimation(targetHeight, TimeSpan.FromMilliseconds(400)) With {
        .EasingFunction = New Animation.CubicEase() With {.EasingMode = Animation.EasingMode.EaseOut}
    }
        rect.BeginAnimation(Rectangle.HeightProperty, anim)
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
            Dim line As String = SerialPort.ReadLine().Trim()
            Dispatcher.BeginInvoke(Sub() ParseAndDisplayData(line))
        Catch ex As Exception
            Dispatcher.BeginInvoke(
                Sub()
                    LogMessage("READ ERROR", ex.Message)
                    UpdateUiForConnection()
                End Sub
            )
        End Try
    End Sub

    Private Sub ParseAndDisplayData(line As String)
        If String.IsNullOrWhiteSpace(line) Then Return

        If line.StartsWith("H") Then
            Dim humVal As Double
            If Double.TryParse(line.Substring(1), humVal) Then
                HandleHumidity(humVal)
            Else
                LogMessage("PARSE ERROR", $"Invalid humidity: {line}")
            End If

        ElseIf line.StartsWith("T") Then
            Dim tempVal As Double
            If Double.TryParse(line.Substring(1), tempVal) Then
                HandleTemperature(tempVal)
            Else
                LogMessage("PARSE ERROR", $"Invalid temperature: {line}")
            End If

        Else
            LogMessage("PARSE ERROR", $"Unknown prefix: {line}")
        End If
    End Sub

    ' ----------------- Humidity -----------------
    Private Sub HandleHumidity(humVal As Double)
        UpdateHumidityArc(humVal)

        Dim nowX As Double = DateTimeAxis.ToDouble(DateTime.Now)
        HumiditySeries.Points.Add(New DataPoint(nowX, humVal))

        While HumiditySeries.Points.Count > ChartLimit
            HumiditySeries.Points.RemoveAt(0)
        End While

        UpdateXAxis(HumidityPlotModel, nowX)
        HumidityPlotModel.InvalidatePlot(True)
    End Sub

    Private Sub UpdateHumidityArc(value As Double)
        value = Math.Max(0, Math.Min(100, value))

        ' Update text
        TxtHumidityPercent.Text = $"{Math.Round(value)}%"

        If value <= 0 Then
            Return
        End If

        ' Calculate geometry parameters
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

        ' Target endpoint
        Dim newEndPoint As New Point(centerX + radius * Math.Sin(radians),
                                 centerY - radius * Math.Cos(radians))

        ' Animate endpoint smoothly
        Dim animX As New Animation.DoubleAnimation(newEndPoint.X, TimeSpan.FromMilliseconds(400)) With {
        .EasingFunction = New Animation.CubicEase() With {.EasingMode = Animation.EasingMode.EaseOut}
    }
        Dim animY As New Animation.DoubleAnimation(newEndPoint.Y, TimeSpan.FromMilliseconds(400)) With {
        .EasingFunction = New Animation.CubicEase() With {.EasingMode = Animation.EasingMode.EaseOut}
    }

        ' Apply to segment's point
        HumidityArcSegment.BeginAnimation(ArcSegment.PointProperty, Nothing) ' stop old animation
        HumidityArcSegment.BeginAnimation(ArcSegment.PointProperty, Nothing) ' ensure fresh start
        HumidityArcSegment.Point = newEndPoint
    End Sub

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

            AnimateBar(RectangleHotTemp, h)
            AnimateBar(RectangleColdTemp, 0)

        ElseIf tempVal < 0 Then
            ' Negative: cold bar fills down
            Dim h = MapVPB(tempVal, -20, 0, coldMaxHeight, 0)
            If h > coldMaxHeight Then h = coldMaxHeight

            AnimateBar(RectangleColdTemp, h)
            AnimateBar(RectangleHotTemp, 0)

        Else
            ' Exactly zero
            AnimateBar(RectangleHotTemp, 0)
            AnimateBar(RectangleColdTemp, 0)
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

    Private HumidityArcSegment As ArcSegment

    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        ' Humidity chart
        HumidityPlotModel = New PlotModel With {.Title = "Humidity (%)"}
        HumidityPlotModel.Axes.Add(New DateTimeAxis With {.Position = AxisPosition.Bottom, .StringFormat = "HH:mm:ss"})
        HumidityPlotModel.Axes.Add(New LinearAxis With {.Position = AxisPosition.Left, .Minimum = 0, .Maximum = 100})
        HumiditySeries = New LineSeries With {.Title = "humidity"}
        HumidityPlotModel.Series.Add(HumiditySeries)
        HumidityPlot.Model = HumidityPlotModel

        Dim figure As New PathFigure()
        HumidityArcSegment = New ArcSegment() With {
        .Size = New Size(50, 50),
        .Point = New Point(0, 0),
        .SweepDirection = SweepDirection.Clockwise
    }
        figure.Segments.Add(HumidityArcSegment)
        figure.StartPoint = New Point(80, 10) ' start top center (adjust depending on your control size)

        Dim geo As New PathGeometry()
        geo.Figures.Add(figure)
        ArcHumidity.Data = geo

        ' Temperature chart
        TemperaturePlotModel = New PlotModel With {.Title = "Temperature (°C)"}
        TemperaturePlotModel.Axes.Add(New DateTimeAxis With {.Position = AxisPosition.Bottom, .StringFormat = "HH:mm:ss"})
        TemperaturePlotModel.Axes.Add(New LinearAxis With {.Position = AxisPosition.Left, .Minimum = -20, .Maximum = 60})
        TemperatureSeries = New LineSeries With {.Title = "Temperature"}
        TemperaturePlotModel.Series.Add(TemperatureSeries)
        TemperaturePlot.Model = TemperaturePlotModel
    End Sub

    Private Sub Window_Closing(sender As Object, e As ComponentModel.CancelEventArgs) Handles Me.Closing
        If Not IsConnected Then Return
        Try
            SerialPort.Close()
            LogMessage("INFO", "Disconnected on window close")
        Catch ex As Exception
            LogMessage("ERROR", $"Error during disconnect: {ex.Message}")
        End Try
        UpdateUiForConnection()
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
