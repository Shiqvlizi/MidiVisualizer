using NAudio.Midi;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.Versioning;
namespace MidiVisualizer
{
    class Program
    {
        private static int DefaultCanvasWidth = 1920;
        private static int DefaultCanvasHeight = 1080;
        private static int DefaultGuideLineX = 960;
        private static int DefaultNoteDisplayHeight = 10;
        private static int DefaultNoteDistance = 0;
        private static double DefaultRotationAngle = 0;// 默认旋转角度,单位度
        private static double DefaultRotationManner = 0; // 默认旋转方式,0:动态调整,音符越长,角度越小 1:固定角度
        private static double DefaultShakeAmplitude = 10; // 默认抖动幅度
        private static double DefaultShakeAmplitudeVariance = 0.0005; // 抖动幅度的方差
        public static double DefaultShakeActivation = 0;//音符第一次被激活时抖动幅度的百分比 
        public static int DefaultShakeManner = 0;//第一次抖动的方式
        private static double DefaultReturnToCenterTime = 0.5; // 回正时间,单位秒
        private static double DefaultPixelsPerSecond = 1500;//192.0是原来的默认值,1tick=1像素默认bpm
        private static int DefaultFps = 30;
        private static string DefaultMidiFilePath = "testttt.mid"; // 默认MIDI文件路径
        private static Color DefaultActiveNoteColor = Color.White; // 默认活跃音符颜色
        private static Color DefaultInactiveNoteColor = Color.FromArgb(100, 100, 100); // 默认非活跃音符颜色
        private static Color DefaultBackgroundColor = Color.Black; // 默认背景色
        private static Color DefaultGuidelineColor = Color.White; // 默认引导线颜色
        private static int DefaultGuidelineWidth = 1; // 默认判定线宽度

        private static double EaseOutCubic(double t)//缓动函数
        {
            if (t < 0 || t > 1)
            {
                return 0;
            }
            return Math.Pow(1 - t, 3);
        }
        // 其他的缓动函数：
        // EaseOutQuad (二次方缓出): return 1 - (1 - t) * (1 - t);
        // EaseOutExpo (指数缓出): return (t == 1.0) ? 1.0 : 1 - Math.Pow(2, -10 * t);

        public static double UniformRandom(double range)//均匀随机数
        {
            return (_random.NextDouble() * 2 - 1) * range;
        }

        public static double UniformRandomExcludeMiddle(double a, double b)//均匀随机数,排除中间部分,取值(-a,-b)∪(b,a)
        {

            //确保都是正数
            a = Math.Abs(a);
            b = Math.Abs(b);
            //确保 a > b，如果不满足就交换
            if (a <= b)
            {
                double temp = a;
                a = b;
                b = temp;
            }
            double random = _random.NextDouble();

            if (random < 0.5) // 50% 概率选择左区间 (-a, -b)
            {
                return -a + random * 2 * (a - b);
            }
            else // 50% 概率选择右区间 (b, a)
            {
                return b + (random - 0.5) * 2 * (a - b);
            }
        }

        private static Random _random = new Random();//正态分布概率分布的随机数
        public static double NormalRandom(double maxAmplitude, double variance)
        {
            double standardDeviation = Math.Sqrt(variance);

            // 使用中心极限定理，叠加12个均匀分布近似正态分布
            double sum = 0;
            for (int i = 0; i < 12; i++)
            {
                sum += _random.NextDouble();
            }

            // 标准化到均值0，标准差1
            double standardNormal = sum - 6.0;

            // 调整到指定的标准差
            double result = standardNormal * standardDeviation;

            // 限制在最大抖动幅度内
            result = Math.Max(-maxAmplitude, Math.Min(maxAmplitude, result));

            return result;
        }

        static void ParseMidiFile(string filePath, List<Note> notes)//解析MIDI文件
        {
            var midiFile = new MidiFile(filePath);
            for (int track = 0; track < midiFile.Tracks; track++)
            {
                foreach (var midiEvent in midiFile.Events[track])
                {
                    // 检查是否是 NoteOnEvent 并且力度大于 0 (即音符开启)
                    if (midiEvent is NoteOnEvent noteOn && noteOn.Velocity > 0)
                    {
                        //Console.Write($"音高: {noteOn.NoteNumber} ({noteOn.NoteName}) ");
                        //Console.Write($"力度: {noteOn.Velocity} ");
                        //Console.Write($"通道: {noteOn.Channel} ");
                        //Console.Write($"开始: {noteOn.AbsoluteTime} ticks ");


                        // 获取对应的音符关闭事件
                        if (noteOn.OffEvent != null)
                        {
                            notes.Add(new Note(noteOn.AbsoluteTime, noteOn.OffEvent.AbsoluteTime, noteOn.NoteNumber, noteOn.NoteName));
                        }
                        else
                        {
                            // 处理没有匹配的 OffEvent 的情况 (例如，MIDI 文件损坏或音符未正确关闭)
                            Console.WriteLine("结束:未找到对应的OffEvent");
                        }
                    }
                }
            }
        }
        static double GetBpmFromMidiFile(string filePath)
        {
            var midiFile = new MidiFile(filePath);
            double bpm = 120.000; // 默认值
            for (int track = 0; track < midiFile.Tracks; track++)
            {
                foreach (var midiEvent in midiFile.Events[track])
                {
                    if (midiEvent is TempoEvent tempoEvent)
                    {
                        return Math.Round(60000000.0 / tempoEvent.MicrosecondsPerQuarterNote, 3);

                    }
                }
            }
            return bpm;
        }
        static double GetDoubleInput(string prompt, double defaultValue)
        {
            Console.Write($"{prompt} (默认值:{defaultValue}): ");
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }
            if (double.TryParse(input, out double result))
            {
                return result;
            }
            Console.WriteLine("输入无效,将使用默认值。");
            return defaultValue;
        }
        static int GetIntInput(string prompt, int defaultValue)
        {
            Console.Write($"{prompt} (默认值:{defaultValue}): ");
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }
            if (int.TryParse(input, out int result))
            {
                return result;
            }
            Console.WriteLine("输入无效,将使用默认值");
            return defaultValue;
        }
        static string GetStringInput(string prompt, string defaultValue)
        {
            Console.Write($"{prompt} (默认值:{defaultValue}):");
            string? input = Console.ReadLine(); // 使用可空类型 string?
            return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
        }
        static Color GetColorInput(string prompt, Color defaultColor)
        {
            // 将默认颜色转换为可读的字符串形式，方便用户参考
            string defaultColorString = $"({defaultColor.R},{defaultColor.G},{defaultColor.B})";
            Console.Write($"{prompt} (默认值:{defaultColorString}): ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultColor;
            }
            // 尝试按十六进制解析 (如 "#FF0000" 或 "FF0000")
            string hex = input.Trim();
            if (hex.StartsWith("#"))
            {
                hex = hex.Substring(1);
            }
            if (hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hexValue))
            {
                try
                {
                    return Color.FromArgb(hexValue | (0xFF << 24)); // 确保A通道为255 (不透明)
                }
                catch { /* 解析失败 */ }
            }
            string[] rgbParts = input.Split(',');
            if (rgbParts.Length == 3 &&
                int.TryParse(rgbParts[0].Trim(), out int r) && r >= 0 && r <= 255 &&
                int.TryParse(rgbParts[1].Trim(), out int g) && g >= 0 && g <= 255 &&
                int.TryParse(rgbParts[2].Trim(), out int b) && b >= 0 && b <= 255)
            {
                return Color.FromArgb(r, g, b);
            }
            Console.WriteLine("颜色输入无效(请使用十六进制或R,G,B格式),将使用默认颜色。");
            return defaultColor;
        }
        [SupportedOSPlatform("windows6.1")]
        static void Main(string[] args)
        {
            Console.WriteLine
                (
                "===Midi可视化V1.1.0===\n" +
                "欢迎使用本工具\n" +
                "请按 Enter 键接受默认值,或输入新值\n" +
                "请注意音符宽度和画布纵向大小的关系,可能出现纵向无法容纳全部音符的情况\n" +
                "颜色输入支持RGB和十六进制\n" +
                "目前不支持变速mid\n" +
                "全部默认请长按 Enter"
                );


            // --- 获取用户输入 ---
            string filePath = GetStringInput("MIDI 文件路径", DefaultMidiFilePath);
            filePath = filePath.Trim('\"');

            var midiFile = new MidiFile(filePath);
            double bpm = 120.000;
            bpm = GetBpmFromMidiFile(filePath);

            double bpmInput = GetDoubleInput("BPM (默认使用mid内置)", bpm);
            bpm = bpmInput;

            int canvasWidth = GetIntInput("画布横向大小(像素)", DefaultCanvasWidth);
            int canvasHeight = GetIntInput("画布纵向大小(像素)", DefaultCanvasHeight);
            int LineEndX = GetIntInput("判定线X坐标(像素)", DefaultGuideLineX);
            int guideLineWidth = GetIntInput("判定线宽度(像素,0表示不渲染,仅视觉)", DefaultGuidelineWidth);
            int noteHeight = GetIntInput("音符宽度(像素)", DefaultNoteDisplayHeight);
            int noteDistance = GetIntInput("音符间距(像素)", DefaultNoteDistance);
            double rotationAngle = -GetDoubleInput("第一次旋转角度", DefaultRotationAngle);
            double rotationManner = GetDoubleInput("第一次旋转方式(0:动态调整,1:固定角度)", DefaultRotationManner);
            int ShakeManner = GetIntInput("第一次抖动方式(0:震动,1:单向)", DefaultShakeManner);
            double returnToCenterTime = GetDoubleInput("回正时间(秒)", DefaultReturnToCenterTime);
            double shakeAmplitude = GetDoubleInput("抖动幅度(%,相对于音符宽度)", DefaultShakeAmplitude);
            double shakeAmplitudeVariance = GetDoubleInput("抖动幅度方差", DefaultShakeAmplitudeVariance);
            double shakeActivation = GetDoubleInput("第一次抖动百分比(%,相对于音符宽度)", DefaultShakeActivation);
            double pixelsPerSecond = GetDoubleInput("每秒滚动像素/流速", DefaultPixelsPerSecond);
            int fps = GetIntInput("视频帧率(FPS)", DefaultFps);
            Color activeNoteColor = GetColorInput("活跃音符颜色", DefaultActiveNoteColor);
            Color inactiveNoteColor = GetColorInput("非活跃音符颜色", DefaultInactiveNoteColor);
            Color backgroundColor = GetColorInput("背景颜色", DefaultBackgroundColor);
            Color guidelineColor = GetColorInput("判定线颜色", DefaultGuidelineColor);



            Console.WriteLine("\n===参数确认===");
            Console.WriteLine($"MIDI 文件:{filePath}");
            Console.WriteLine($"画布尺寸:{canvasWidth}x{canvasHeight}");
            Console.WriteLine($"判定线X:{LineEndX}");
            Console.WriteLine($"判定线宽度: {guideLineWidth} ({(guideLineWidth > 0 ? "将渲染" : "不渲染")})"); // 显示判定线宽度和是否渲染
            Console.WriteLine($"音符宽度:{noteHeight}");
            Console.WriteLine($"每秒像素:{pixelsPerSecond}");
            Console.WriteLine($"帧率:{fps}");
            Console.WriteLine($"活跃音符颜色:({activeNoteColor.R},{activeNoteColor.G},{activeNoteColor.B})");
            Console.WriteLine($"非活跃音符颜色:({inactiveNoteColor.R},{inactiveNoteColor.G},{inactiveNoteColor.B})");
            Console.WriteLine($"背景颜色:({backgroundColor.R},{backgroundColor.G},{backgroundColor.B})");
            Console.WriteLine($"判定线颜色: ({guidelineColor.R},{guidelineColor.G},{guidelineColor.B})");
            Console.WriteLine("=============\n");

            List<Note> notes = new List<Note>();



            ParseMidiFile(filePath, notes);

            Console.WriteLine($"BPM:{bpm}");

            double totalDuration = 60 * notes[notes.Count - 1].End / (bpm * midiFile.DeltaTicksPerQuarterNote);



            Console.WriteLine($"每秒像素:{pixelsPerSecond}");
            double pixelsPerFrame = pixelsPerSecond / fps;
            double pixelsPerBeat = pixelsPerSecond * 60 / bpm;
            double pixelsPerTick = pixelsPerBeat / midiFile.DeltaTicksPerQuarterNote;
            Console.WriteLine($"每帧像素:{pixelsPerFrame}");


            using var activeBrush = new SolidBrush(activeNoteColor);
            using var inactiveBrush = new SolidBrush(inactiveNoteColor);
            using var backgroundBrush = new SolidBrush(backgroundColor);
            using var guidelineBrush = new SolidBrush(guidelineColor);

            int offestX = 0;
            int totalFrames = (int)Math.Ceiling((totalDuration * fps));
            int noteScreenStartX = 0;
            int noteScreenEndX = 0;
            //一堆乱七八糟初始化
            if (!notes.Any())
            {
                Console.WriteLine("音符列表为空");
                return;
            }
            //string midiFileDirectory = Path.GetDirectoryName(filePath); // 获取 MIDI 文件所在的目录
            string appDirectory = AppContext.BaseDirectory; // 获取程序 .exe 文件所在的目录
            string midiFileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath); // 获取不带扩展名的文件名
            string frameDir = Path.Combine(appDirectory, midiFileNameWithoutExtension + "_frames");
            int counter = 0;
            string BaseFrameDir = frameDir;
            // 循环检测文件夹是否存在，如果存在则在名称后加数字
            while (Directory.Exists(frameDir))
            {
                counter++;
                // 构建新的文件夹名称，例如 "MySong_frames_1", "MySong_frames_2"
                frameDir = $"{BaseFrameDir}_{counter}";
            }

            Directory.CreateDirectory(frameDir);
            int minPitch = notes.Min(note => note.Pitch);
            int maxPitch = notes.Max(note => note.Pitch);
            int mid = (minPitch + maxPitch) / 2;
            for (int i = 0; i < notes.Count; i++)
            {
                notes[i].PixelStartX = (long)(notes[i].Start * pixelsPerTick);
                notes[i].PixelLength = (long)((notes[i].End - notes[i].Start) * pixelsPerTick);
                notes[i].PixelEndX = (notes[i].PixelStartX + notes[i].PixelLength);
                notes[i].PixelY = canvasHeight / 2 + (mid - notes[i].Pitch) * noteHeight - noteHeight / 2;//其实是左上角
                notes[i].StartFrame = (int)(notes[i].Start * pixelsPerTick / pixelsPerFrame);
                notes[i].EndFrame = (int)(notes[i].End * pixelsPerTick / pixelsPerFrame);
                notes[i].UnidirectionalShake = UniformRandomExcludeMiddle(1, 0.7);
            }


            Console.CursorVisible = false;
            int lineStratX = LineEndX - guideLineWidth + 1;
            Action<Graphics, int, int> drawGuideline = (g, x, h) => { };
            int frameDigits = totalFrames.ToString().Length;
            string frameFormat = new string('0', Math.Max(2, frameDigits));
            Note? lastNote = notes.OrderByDescending(n => n.End).FirstOrDefault();
            int extraFrames = (int)Math.Ceiling(LineEndX / pixelsPerFrame);
            if (lastNote == null)
            {
                Console.WriteLine("没找到最后一个音符");
                return;
            }
            if (guideLineWidth > 0)
            {
                drawGuideline = (g, x, h) => g.FillRectangle(guidelineBrush, x, 0, guideLineWidth, h);
            }

            ConcurrentQueue<GeneratedFrame> framesQueue = new ConcurrentQueue<GeneratedFrame>();
            bool generationRunning = true;
            var saveFramesThread = new Thread(() =>
            {
                while (framesQueue.Count > 0 || generationRunning)
                {
                    if (!framesQueue.TryDequeue(out var generatedFrame))
                    {
                        Thread.Sleep(50);
                        continue;
                    }
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        string framePath = Path.Combine(frameDir, $"{generatedFrame.FrameIndex.ToString(frameFormat)}.png");
                        generatedFrame.Frame.Save(framePath, ImageFormat.Png);

                        generatedFrame.Frame.Dispose();
                    });
                }
            });
            saveFramesThread.IsBackground = true;
            saveFramesThread.Start();
            double shakePixels = (noteHeight * shakeAmplitude / 100.0);
            double shakeActivationPixels = (noteHeight * shakeActivation / 100.0);
            bool IsNoteActive = false;
            double timeToCenter = returnToCenterTime * fps;
            for (int frame = 0; frame <= (int)Math.Ceiling(lastNote.End * 60 * fps / (bpm * midiFile.DeltaTicksPerQuarterNote)) + extraFrames +0.5*fps; frame++)
            {
                offestX = (int)(pixelsPerFrame * frame);
                var bitmap = new Bitmap(canvasWidth, canvasHeight);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.FillRectangle(backgroundBrush, 0, 0, canvasWidth, canvasHeight);


                while (framesQueue.Count >= 200)
                {
                    Thread.Sleep(50);
                }

                foreach (var note in notes)
                {






                    IsNoteActive = (note.PixelStartX - offestX + LineEndX <= LineEndX) && (LineEndX < note.PixelEndX - offestX + LineEndX);
                    noteScreenStartX = (int)(note.PixelStartX - offestX + LineEndX);
                    noteScreenEndX = (int)(note.PixelEndX - offestX + LineEndX);







                    if (noteScreenStartX > canvasWidth || noteScreenEndX < 0)
                    {
                        continue; // 如果音符超出画布范围，则跳过
                    }

                    System.Drawing.Drawing2D.GraphicsState originalState = graphics.Save();
                    graphics.TranslateTransform(noteScreenStartX, (note.PixelY + noteHeight / 2));
                    graphics.RotateTransform
                        (
                        (float)
                            (
                             !IsNoteActive ? 0 :
                            ShakeManner == 0 ?
                            EaseOutCubic((frame - note.StartFrame) / timeToCenter) * rotationAngle * UniformRandomExcludeMiddle(1, 0.7) * 50 * noteHeight / note.PixelLength :
                            EaseOutCubic((frame - note.StartFrame) / timeToCenter) * rotationAngle * UniformRandomExcludeMiddle(1, 0.7)
                            )
                        );

                    graphics.FillRectangle
                        (
                        IsNoteActive ? activeBrush : inactiveBrush,
                        //note.PixelStartX - offestX + LineEndX
                        0,

                        -noteHeight / 2 +
                        (mid - note.Pitch) * noteDistance +
                        (float)(IsNoteActive ? NormalRandom(shakeAmplitude, shakeAmplitudeVariance) : 0) +

                        (float)(!IsNoteActive ? 0 :
                        (
                        ((ShakeManner == 0) ?
                        EaseOutCubic((frame - note.StartFrame) / timeToCenter) * shakeActivationPixels * UniformRandomExcludeMiddle(1, 0.7) :
                        EaseOutCubic((frame - note.StartFrame) / timeToCenter) * shakeActivationPixels * note.UnidirectionalShake * shakeActivationPixels)
                        )),

                        note.PixelLength,
                        noteHeight
                        );
                    //画笔  
                    //音符X
                    //音符Y
                    //音符长
                    //音符宽

                    graphics.Restore(originalState); // 恢复原始状态
                }
                drawGuideline(graphics, lineStratX, canvasHeight);
                framesQueue.Enqueue(new GeneratedFrame(frame, bitmap));
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write($"生成帧 {frame + 1}/{totalFrames + 1 + extraFrames+ 0.5 * fps}        ");//空格保证覆盖之前的输出
            }
            generationRunning = false;
            saveFramesThread.Join();
            Console.CursorVisible = true;
            Console.WriteLine("\n所有帧已生成");
            Console.WriteLine($"保存到: {Path.GetFullPath(frameDir)}");
            try
            {
                // 尝试打开生成的视频帧文件夹
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = Path.GetFullPath(frameDir),
                    UseShellExecute = true,
                    Verb = "open" // 明确指定打开操作
                });
                Console.WriteLine($"已自动打开文件夹: {Path.GetFullPath(frameDir)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"无法自动打开文件夹。错误: {ex.Message}");
                Console.WriteLine("请手动前往以下路径查看帧图片:");
                Console.WriteLine(Path.GetFullPath(frameDir));
            }

            Console.WriteLine("\n按任意键退出程序...");
            Console.ReadKey();
        }




        // 下面的代码是为了输出音符信息，已被注释掉
        //foreach (var note in notes)
        //{
        //    // 输出音符信息
        //    Console.WriteLine($"音符: {note.Name} ({note.Pitch}) 开始: {note.Start} ticks 结束: {note.End} ticks 持续时间: {note.End - note.Start} ticks");
        //}
    }
    class Note
    {
        public long Start { get; }
        public long End { get; }
        public int Pitch { get; }
        public string Name { get; }
        public long PixelStartX { get; set; }
        public long PixelEndX { get; set; }
        public long PixelLength { get; set; }
        public int PixelY { get; set; }
        public int StartFrame { get; set; }
        public int EndFrame { get; set; }
        public double UnidirectionalShake { get; set; }
        public Note(long start, long end, int pitch, string name)
        {
            Start = start;
            End = end;
            Pitch = pitch;
            Name = name;
        }
    }
    public class GeneratedFrame
    {
        public int FrameIndex { get; }
        public Bitmap Frame { get; }
        public GeneratedFrame(int index, Bitmap frame)
        {
            FrameIndex = index;
            Frame = frame;
        }
    }
}