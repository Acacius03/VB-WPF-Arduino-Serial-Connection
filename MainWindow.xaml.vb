Imports System.IO.Ports
Imports System.Windows.Media.Animation
Imports System.Windows.Threading
Imports OxyPlot
Imports OxyPlot.Axes
Imports OxyPlot.Series

Class MainWindow
    Private WithEvents SerialPort As SerialPort

    Private Const ChartLimit As Integer = 30
    Private Const ChartWindowSeconds As Integer = 60

    Private HumiditySeries As LineSeries
    Private TemperatureSeries As LineSeries

    Public Property HumidityPlotModel As PlotModel
    Public Property TemperaturePlotModel As PlotModel

    Private ReadOnly Property IsConnected As Boolean
        Get
            Return SerialPort IsNot Nothing AndAlso SerialPort.IsOpen
        End Get
    End Property

    Private Sub UpdateUiForConnection()
        Dim status = If(IsConnected,
                        $"Connected to {SerialPort?.PortName} at {SerialPort?.BaudRate} baud",
                        "Disconnected")
        TxtStatus.Text = status
        LogMessage("SYSTEM", status)
    End Sub

    ' ----------------- Logging -----------------
    Private Sub LogMessage(source As String, message As String)
        Dim timestamp = DateTime.Now.ToString("HH:mm:ss")
        TxtData.AppendText($"[{timestamp}] {source}: {message}" & Environment.NewLine)
        TxtData.ScrollToEnd()
    End Sub

    ' ----------------- Serial Connection -----------------
    Private Sub TryConnect()
        Dim dlg As New SerialPortConnectorWindow()
        If dlg.ShowDialog().GetValueOrDefault() Then
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
            Dim line = SerialPort.ReadLine().Trim()
            Dispatcher.BeginInvoke(Sub() ParseAndDisplayData(line))
        Catch ex As Exception
            Dispatcher.BeginInvoke(Sub()
                                       LogMessage("READ ERROR", ex.Message)
                                       UpdateUiForConnection()
                                   End Sub)
        End Try
    End Sub

    ' ----------------- Data Parsing -----------------
    Private Sub ParseAndDisplayData(line As String)
        If String.IsNullOrWhiteSpace(line) Then Return

        Dim prefix = line(0)
        Dim valueStr = line.Substring(1)
        Dim val As Double

        If Not Double.TryParse(valueStr, val) Then
            LogMessage("PARSE ERROR", $"Invalid {If(prefix = "H"c, "humidity", "temperature")}: {line}")
            Return
        End If

        Select Case prefix
            Case "H"c : HandleHumidity(val)
            Case "T"c : HandleTemperature(val)
            Case Else : LogMessage("PARSE ERROR", $"Unknown prefix: {line}")
        End Select
    End Sub

    ' ----------------- Humidity -----------------
    Private Sub HandleHumidity(humVal As Double)
        AnimateProperty(AnimatedHumidityProperty, AnimatedHumidity, Clamp(humVal, 0, 100))
        UpdateChart(HumidityPlotModel, HumiditySeries, humVal)
    End Sub

    Public Shared ReadOnly AnimatedHumidityProperty As DependencyProperty =
        DependencyProperty.Register("AnimatedHumidity", GetType(Double), GetType(MainWindow),
                                    New PropertyMetadata(0.0, AddressOf OnAnimatedHumidityChanged))

    Public Property AnimatedHumidity As Double
        Get
            Return CDbl(GetValue(AnimatedHumidityProperty))
        End Get
        Set
            SetValue(AnimatedHumidityProperty, Value)
        End Set
    End Property

    Private Shared Sub OnAnimatedHumidityChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        CType(d, MainWindow).DrawHumidityArc(CDbl(e.NewValue))
    End Sub

    Private Sub DrawHumidityArc(value As Double)
        Dim angle = value * 360 / 100
        Dim radians = Math.PI * angle / 180
        Dim width = If(GaugeHumidity.ActualWidth > 0, GaugeHumidity.ActualWidth, GaugeHumidity.Width)
        Dim height = If(GaugeHumidity.ActualHeight > 0, GaugeHumidity.ActualHeight, GaugeHumidity.Height)
        Dim stroke = If(ArcHumidity.StrokeThickness > 0, ArcHumidity.StrokeThickness, 28)
        Dim cx = width / 2, cy = height / 2
        Dim r = Math.Min(width, height) / 2 - stroke / 2
        Dim startPoint = New Point(cx, cy - r)
        Dim endPoint = New Point(cx + r * Math.Sin(radians), cy - r * Math.Cos(radians))
        Dim figure = New PathFigure() With {.StartPoint = startPoint}
        figure.Segments.Add(New ArcSegment() With {
                                .Point = endPoint,
                                .Size = New Size(r, r),
                                .IsLargeArc = angle > 180,
                                .SweepDirection = SweepDirection.Clockwise
                            })
        ArcHumidity.Data = New PathGeometry() With {.Figures = New PathFigureCollection({figure})}
        TxtHumidityPercent.Text = $"{Math.Round(value)}%"
    End Sub

    ' ----------------- Temperature -----------------
    Private Sub HandleTemperature(tempVal As Double)
        TxtTemperature.Text = $"{tempVal} °C"
        Dim hotHeight = If(tempVal > 0, MapVPB(tempVal, 0, 60, 0, 128), 0)
        Dim coldHeight = If(tempVal < 0, MapVPB(tempVal, -20, 0, 32, 0), 0)
        AnimateProperty(HeightProperty, RectangleHotTemp.Height, hotHeight, RectangleHotTemp)
        AnimateProperty(HeightProperty, RectangleColdTemp.Height, coldHeight, RectangleColdTemp)
        UpdateChart(TemperaturePlotModel, TemperatureSeries, tempVal)
    End Sub

    ' ----------------- Helpers -----------------
    Private Sub AnimateProperty(dp As DependencyProperty, fromValue As Double, toValue As Double, Optional target As FrameworkElement = Nothing)
        ' Ensure values are valid for animation
        If Double.IsNaN(fromValue) Then fromValue = 0
        If Double.IsNaN(toValue) Then toValue = 0

        Dim anim As New DoubleAnimation(fromValue, toValue, TimeSpan.FromMilliseconds(500)) With {
        .EasingFunction = New QuadraticEase() With {.EasingMode = EasingMode.EaseOut}
    }

        If target IsNot Nothing Then
            target.BeginAnimation(dp, anim)
        Else
            Me.BeginAnimation(dp, anim)
        End If
    End Sub



    Private Sub UpdateChart(model As PlotModel, series As LineSeries, value As Double)
        Dim nowX = DateTimeAxis.ToDouble(DateTime.Now)
        series.Points.Add(New DataPoint(nowX, value))
        While series.Points.Count > ChartLimit
            series.Points.RemoveAt(0)
        End While
        Dim xAxis = TryCast(model.Axes.FirstOrDefault(Function(a) TypeOf a Is DateTimeAxis), DateTimeAxis)
        If xAxis IsNot Nothing Then
            xAxis.Maximum = nowX
            xAxis.Minimum = DateTimeAxis.ToDouble(DateTime.Now.AddSeconds(-ChartWindowSeconds))
        End If
        model.InvalidatePlot(True)
    End Sub

    Private Function Clamp(value As Double, min As Double, max As Double) As Double
        Return Math.Max(min, Math.Min(max, value))
    End Function

    Private Function MapVPB(X As Double, InMin As Double, InMax As Double, OutMin As Double, OutMax As Double) As Double
        Return (X - InMin) * (OutMax - OutMin) / (InMax - InMin) + OutMin
    End Function

    ' ----------------- UI Events -----------------
    Private Sub BtnSelectPort_Click(sender As Object, e As RoutedEventArgs)
        TryConnect()
    End Sub

    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        InitializeChart(HumidityPlotModel, HumiditySeries, "Humidity (%)", 0, 100, HumidityPlot)
        InitializeChart(TemperaturePlotModel, TemperatureSeries, "Temperature (°C)", -20, 60, TemperaturePlot)
    End Sub

    Private Sub Window_Closing(sender As Object, e As ComponentModel.CancelEventArgs) Handles Me.Closing
        If IsConnected Then
            Try : SerialPort.Close() : LogMessage("INFO", "Disconnected on window close")
            Catch ex As Exception
                LogMessage("ERROR", $"Error during disconnect: {ex.Message}")
            End Try
        End If
        UpdateUiForConnection()
    End Sub

    Private Sub InitializeChart(ByRef model As PlotModel, ByRef series As LineSeries, title As String, minY As Double, maxY As Double, plotControl As OxyPlot.Wpf.PlotView)
        model = New PlotModel With {.Title = title}
        model.Axes.Add(New DateTimeAxis With {.Position = AxisPosition.Bottom, .StringFormat = "HH:mm:ss"})
        model.Axes.Add(New LinearAxis With {.Position = AxisPosition.Left, .Minimum = minY, .Maximum = maxY})
        series = New LineSeries With {.Title = title}
        model.Series.Add(series)
        plotControl.Model = model
    End Sub
End Class
