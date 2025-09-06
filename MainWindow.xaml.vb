Imports System.IO.Ports
Imports OxyPlot
Imports OxyPlot.Series
Imports OxyPlot.Axes
Imports System.Windows.Media.Animation

Class MainWindow
    Private WithEvents SerialPort As SerialPort

    Private SimTimer As Windows.Threading.DispatcherTimer
    Private RandomGen As New Random()

    Private currentHumidity As Double = 0

    Private Sub StartSimulation()
        SimTimer = New Windows.Threading.DispatcherTimer() With {
            .Interval = TimeSpan.FromSeconds(2)
        }
        AddHandler SimTimer.Tick, AddressOf SimulateArduinoData
        SimTimer.Start()
        LogMessage("SIM", "Simulation started (Arduino emulation)")
    End Sub

    Private Sub StopSimulation()
        If SimTimer IsNot Nothing Then
            SimTimer.Stop()
            RemoveHandler SimTimer.Tick, AddressOf SimulateArduinoData
            LogMessage("SIM", "Simulation stopped")
        End If
    End Sub

    Private Sub SimulateArduinoData(sender As Object, e As EventArgs)
        ' Generate fake humidity between 20–90
        Dim hum As Double = RandomGen.Next(20, 91)
        ' Generate fake temperature between -10–50
        Dim temp As Double = RandomGen.Next(-10, 51)

        ' Fake Arduino format: Hxx and Txx
        Dim humLine As String = $"H{hum}"
        Dim tempLine As String = $"T{temp}"

        ' Pass into your normal parser (as if SerialPort sent it)
        ParseAndDisplayData(humLine)
        ParseAndDisplayData(tempLine)
    End Sub

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

    Public Shared ReadOnly AnimatedHumidityProperty As DependencyProperty =
    DependencyProperty.Register("AnimatedHumidity", GetType(Double), GetType(MainWindow),
                                New PropertyMetadata(0.0, AddressOf OnAnimatedHumidityChanged))

    Public Property AnimatedHumidity As Double
        Get
            Return CDbl(GetValue(AnimatedHumidityProperty))
        End Get
        Set(value As Double)
            SetValue(AnimatedHumidityProperty, value)
        End Set
    End Property

    Private Shared Sub OnAnimatedHumidityChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim wnd As MainWindow = CType(d, MainWindow)
        wnd.DrawHumidityArc(CDbl(e.NewValue))
    End Sub


    Private Sub UpdateHumidityArc(value As Double)
        value = Math.Max(0, Math.Min(100, value))

        Dim startValue As Double = AnimatedHumidity
        Dim anim As New DoubleAnimation(startValue, value, TimeSpan.FromMilliseconds(500)) With {
        .EasingFunction = New QuadraticEase() With {.EasingMode = EasingMode.EaseOut}
    }

        ' Animate our DP instead of using PathGeometry directly
        Me.BeginAnimation(AnimatedHumidityProperty, anim)
    End Sub



    Private Sub DrawHumidityArc(value As Double)
        Dim angle As Double = value * 360.0 / 100.0
        Dim radians As Double = Math.PI * angle / 180.0

        Dim width As Double = If(GaugeHumidity.ActualWidth > 0, GaugeHumidity.ActualWidth, GaugeHumidity.Width)
        Dim height As Double = If(GaugeHumidity.ActualHeight > 0, GaugeHumidity.ActualHeight, GaugeHumidity.Height)
        Dim strokeThickness As Double = If(ArcHumidity.StrokeThickness > 0, ArcHumidity.StrokeThickness, 28)

        Dim centerX = width / 2.0
        Dim centerY = height / 2.0
        Dim radius = Math.Min(width, height) / 2.0 - strokeThickness / 2.0

        Dim startPoint = New Point(centerX, centerY - radius)
        Dim endPoint = New Point(centerX + radius * Math.Sin(radians), centerY - radius * Math.Cos(radians))
        Dim isLargeArc = angle > 180.0

        Dim figure = New PathFigure() With {.StartPoint = startPoint}
        figure.Segments.Add(New ArcSegment() With {
                                .Point = endPoint,
                                .Size = New Size(radius, radius),
                                .IsLargeArc = isLargeArc,
                                .SweepDirection = SweepDirection.Clockwise
                            })

        Dim geo = New PathGeometry()
        geo.Figures.Add(figure)
        ArcHumidity.Data = geo

        TxtHumidityPercent.Text = $"{Math.Round(value)}%"
    End Sub

    ' ----------------- Temperature -----------------
    Private Sub HandleTemperature(tempVal As Double)
        TxtTemperature.Text = $"{tempVal} °C"

        Dim hotMaxHeight As Double = 128
        Dim coldMaxHeight As Double = 32

        Dim hotHeight As Double = 0
        Dim coldHeight As Double = 0

        If tempVal > 0 Then
            hotHeight = MapVPB(tempVal, 0, 60, 0, hotMaxHeight)
        ElseIf tempVal < 0 Then
            coldHeight = MapVPB(tempVal, -20, 0, coldMaxHeight, 0)
        End If

        Dim startHot As Double = If(Double.IsNaN(RectangleHotTemp.Height), 0, RectangleHotTemp.Height)
        Dim startCold As Double = If(Double.IsNaN(RectangleColdTemp.Height), 0, RectangleColdTemp.Height)

        ' Animate hot bar
        Dim animHot As New DoubleAnimation(startHot, hotHeight, TimeSpan.FromMilliseconds(500)) With {
    .EasingFunction = New QuadraticEase() With {.EasingMode = EasingMode.EaseOut}
}
        RectangleHotTemp.BeginAnimation(HeightProperty, animHot)

        ' Animate cold bar
        Dim animCold As New DoubleAnimation(startCold, coldHeight, TimeSpan.FromMilliseconds(500)) With {
    .EasingFunction = New QuadraticEase() With {.EasingMode = EasingMode.EaseOut}
}
        RectangleColdTemp.BeginAnimation(HeightProperty, animCold)

        ' Add to temperature chart
        Dim nowX As Double = DateTimeAxis.ToDouble(DateTime.Now)
        TemperatureSeries.Points.Add(New DataPoint(nowX, tempVal))
        While TemperatureSeries.Points.Count > ChartLimit
            TemperatureSeries.Points.RemoveAt(0)
        End While
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

        StartSimulation()
    End Sub

    Private Sub Window_Closing(sender As Object, e As ComponentModel.CancelEventArgs) Handles Me.Closing
        If IsConnected Then
            Try
                SerialPort.Close()
                LogMessage("INFO", "Disconnected on window close")
            Catch ex As Exception
                LogMessage("ERROR", $"Error during disconnect: {ex.Message}")
            End Try
        End If
        UpdateUiForConnection()
        StopSimulation()
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
