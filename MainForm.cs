using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EReader
{
    // 设置保存类
    public class AppSettings
    {
        public string BackgroundColor { get; set; } = "#2D2D30"; // 默认深色背景
        public string TextColor { get; set; } = "#FFFFFF"; // 默认白色文字
        public string FontName { get; set; } = "微软雅黑";
        public float FontSize { get; set; } = 12F;
        public bool FontBold { get; set; } = false;
        public bool FontItalic { get; set; } = false;
        public int WindowWidth { get; set; } = 800;
        public int WindowHeight { get; set; } = 600;
        public int WindowX { get; set; } = -1; // -1表示居中
        public int WindowY { get; set; } = -1; // -1表示居中
        public bool CompressBlankLines { get; set; } = true; // 默认启用空白行压缩
        public int LineSpacing { get; set; } = 4; // 行间距（像素）
        public int ParagraphSpacing { get; set; } = 8; // 段落间距（像素）
        public int WindowOpacity { get; set; } = 100; // 窗口透明度（0-100，100为完全不透明）
        public string LastOpenedFilePath { get; set; } = "";
        public Dictionary<string, int> ReadingProgress { get; set; } = new Dictionary<string, int>(); // 文件路径 -> 滚动位置
        
        public static AppSettings Load()
        {
            try
            {
                string settingsPath = GetSettingsPath();
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                // 如果加载失败，返回默认设置
            }
            return new AppSettings();
        }
        
        public void Save()
        {
            try
            {
                string settingsPath = GetSettingsPath();
                string settingsDir = Path.GetDirectoryName(settingsPath);
                if (!Directory.Exists(settingsDir))
                {
                    Directory.CreateDirectory(settingsDir);
                }
                
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(settingsPath, json);
            }
            catch
            {
                // 保存失败时静默处理
            }
        }
        
        private static string GetSettingsPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "EReader", "settings.json");
        }
        
        public Color GetBackgroundColor()
        {
            try
            {
                return ColorTranslator.FromHtml(BackgroundColor);
            }
            catch
            {
                return Color.FromArgb(45, 45, 48);
            }
        }
        
        public Color GetTextColor()
        {
            try
            {
                return ColorTranslator.FromHtml(TextColor);
            }
            catch
            {
                return Color.White;
            }
        }
        
        public Font GetFont()
        {
            try
            {
                FontStyle style = FontStyle.Regular;
                if (FontBold) style |= FontStyle.Bold;
                if (FontItalic) style |= FontStyle.Italic;
                return new Font(FontName, FontSize, style);
            }
            catch
            {
                return new Font("微软雅黑", 12F);
            }
        }
        
        public void SetBackgroundColor(Color color)
        {
            BackgroundColor = ColorTranslator.ToHtml(color);
        }
        
        public void SetTextColor(Color color)
        {
            TextColor = ColorTranslator.ToHtml(color);
        }
        
        public void SetFont(Font font)
        {
            FontName = font.Name;
            FontSize = font.Size;
            FontBold = font.Bold;
            FontItalic = font.Italic;
        }
    }

    // 屏幕取色器窗体
    public class ColorPickerForm : Form
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);
        
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        
        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);
        
        private Color selectedColor = Color.Empty;
        private Label colorPreview;
        private Label instructionLabel;
        private bool isPickingColor = false;
        
        public Color SelectedColor => selectedColor;
        
        public ColorPickerForm()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = "颜色选择";
            this.Size = new Size(400, 220);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;
            
            instructionLabel = new Label();
            instructionLabel.Text = "选择颜色方式：";
            instructionLabel.Location = new Point(20, 15);
            instructionLabel.Size = new Size(360, 20);
            instructionLabel.ForeColor = Color.White;
            
            // 调色盘按钮
            Button paletteButton = new Button();
            paletteButton.Text = "🎨 打开调色盘";
            paletteButton.Location = new Point(20, 45);
            paletteButton.Size = new Size(170, 40);
            paletteButton.BackColor = Color.FromArgb(60, 60, 65);
            paletteButton.ForeColor = Color.White;
            paletteButton.FlatStyle = FlatStyle.Flat;
            paletteButton.Click += PaletteButton_Click;
            
            // 取色器按钮
            Button startPickButton = new Button();
            startPickButton.Text = "💉 屏幕取色";
            startPickButton.Location = new Point(200, 45);
            startPickButton.Size = new Size(170, 40);
            startPickButton.BackColor = Color.FromArgb(60, 60, 65);
            startPickButton.ForeColor = Color.White;
            startPickButton.FlatStyle = FlatStyle.Flat;
            startPickButton.Click += StartPick_Click;
            
            // 颜色预览区
            Label previewLabel = new Label();
            previewLabel.Text = "当前选择:";
            previewLabel.Location = new Point(20, 100);
            previewLabel.Size = new Size(70, 20);
            previewLabel.ForeColor = Color.White;
            
            colorPreview = new Label();
            colorPreview.Location = new Point(95, 95);
            colorPreview.Size = new Size(100, 30);
            colorPreview.BorderStyle = BorderStyle.FixedSingle;
            colorPreview.BackColor = Color.White;
            colorPreview.Text = "";
            colorPreview.TextAlign = ContentAlignment.MiddleCenter;
            
            Label rgbLabel = new Label();
            rgbLabel.Name = "rgbLabel";
            rgbLabel.Text = "未选择";
            rgbLabel.Location = new Point(205, 100);
            rgbLabel.Size = new Size(165, 20);
            rgbLabel.ForeColor = Color.LightGray;
            this.Controls.Add(rgbLabel);
            
            Button okButton = new Button();
            okButton.Text = "确定";
            okButton.Location = new Point(200, 145);
            okButton.Size = new Size(80, 35);
            okButton.BackColor = Color.FromArgb(0, 122, 204);
            okButton.ForeColor = Color.White;
            okButton.FlatStyle = FlatStyle.Flat;
            okButton.Click += (s, e) =>
            {
                if (selectedColor != Color.Empty)
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("请先选择一个颜色", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            
            Button cancelButton = new Button();
            cancelButton.Text = "取消";
            cancelButton.Location = new Point(290, 145);
            cancelButton.Size = new Size(80, 35);
            cancelButton.BackColor = Color.FromArgb(60, 60, 65);
            cancelButton.ForeColor = Color.White;
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.DialogResult = DialogResult.Cancel;
            
            this.Controls.AddRange(new Control[] { 
                instructionLabel, paletteButton, startPickButton, 
                previewLabel, colorPreview, okButton, cancelButton 
            });
            
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
        
        private void PaletteButton_Click(object sender, EventArgs e)
        {
            using (ColorDialog colorDialog = new ColorDialog())
            {
                colorDialog.AllowFullOpen = true;
                colorDialog.AnyColor = true;
                colorDialog.FullOpen = true; // 直接展开完整调色盘
                colorDialog.Color = selectedColor != Color.Empty ? selectedColor : Color.White;
                
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    selectedColor = colorDialog.Color;
                    colorPreview.BackColor = selectedColor;
                    
                    // 更新RGB显示
                    Label rgbLabel = this.Controls.Find("rgbLabel", false).FirstOrDefault() as Label;
                    if (rgbLabel != null)
                    {
                        rgbLabel.Text = $"RGB({selectedColor.R}, {selectedColor.G}, {selectedColor.B})";
                    }
                }
            }
        }
        
        private void StartPick_Click(object sender, EventArgs e)
        {
            isPickingColor = true;
            
            // 隐藏当前窗体而不是最小化
            this.Hide();
            
            // 创建全屏透明窗体用于取色
            using (Form pickForm = new Form())
            {
                pickForm.FormBorderStyle = FormBorderStyle.None;
                pickForm.WindowState = FormWindowState.Maximized;
                pickForm.BackColor = Color.Black;
                pickForm.Opacity = 0.01; // 几乎透明
                pickForm.TopMost = true;
                pickForm.Cursor = Cursors.Cross;
                pickForm.ShowInTaskbar = false;
                
                pickForm.MouseClick += (s, args) =>
                {
                    // 转换为屏幕坐标
                    Point screenPoint = pickForm.PointToScreen(args.Location);
                    selectedColor = GetColorAt(screenPoint);
                    colorPreview.BackColor = selectedColor;
                    
                    // 更新RGB显示
                    Label rgbLabel = this.Controls.Find("rgbLabel", false).FirstOrDefault() as Label;
                    if (rgbLabel != null)
                    {
                        rgbLabel.Text = $"RGB({selectedColor.R}, {selectedColor.G}, {selectedColor.B})";
                    }
                    
                    pickForm.Close();
                };
                
                pickForm.KeyDown += (s, args) =>
                {
                    if (args.KeyCode == Keys.Escape)
                    {
                        pickForm.Close();
                    }
                };
                
                // 添加提示标签
                Label hintLabel = new Label();
                hintLabel.Text = "点击屏幕任意位置取色，按ESC取消";
                hintLabel.ForeColor = Color.White;
                hintLabel.BackColor = Color.FromArgb(128, 0, 0, 0);
                hintLabel.AutoSize = true;
                hintLabel.Location = new Point(50, 50);
                pickForm.Controls.Add(hintLabel);
                
                pickForm.ShowDialog();
            }
            
            // 恢复显示取色器窗体
            this.Show();
            this.BringToFront();
            this.Focus();
            isPickingColor = false;
        }
        
        private Color GetColorAt(Point location)
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            uint pixel = GetPixel(hdc, location.X, location.Y);
            ReleaseDC(IntPtr.Zero, hdc);
            
            Color color = Color.FromArgb((int)(pixel & 0x000000FF),
                                       (int)(pixel & 0x0000FF00) >> 8,
                                       (int)(pixel & 0x00FF0000) >> 16);
            return color;
        }
    }

    // 自定义Panel用于显示文本内容，支持滚动和绘制
    public class TextDisplayPanel : Panel
    {
        private string[] textLines = new string[0];
        private int scrollPosition = 0; // 基于行的滚动位置（保持兼容性）
        private float pixelScrollOffset = 0; // 基于像素的滚动偏移
        private Font textFont = new Font("微软雅黑", 12F);
        private Color textColor = Color.White;
        private const int MAX_LINES_TO_PROCESS = 10000; // 限制处理的最大行数
        private List<ChapterInfo> chapters = new List<ChapterInfo>();
        private AppSettings settings; // 添加设置引用
        private int lineSpacing = 4; // 行间距
        private int paragraphSpacing = 8; // 段落间距
        
        public TextDisplayPanel(AppSettings appSettings)
        {
            this.settings = appSettings;
            this.lineSpacing = appSettings.LineSpacing;
            this.paragraphSpacing = appSettings.ParagraphSpacing;
            
            // 启用双缓冲和优化绘制
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | 
                         ControlStyles.UserPaint | 
                         ControlStyles.DoubleBuffer | 
                         ControlStyles.ResizeRedraw |
                         ControlStyles.OptimizedDoubleBuffer, true);
            
            // 减少重绘频率
            this.SetStyle(ControlStyles.Selectable, false);
        }
        
        public class ChapterInfo
        {
            public string Title { get; set; }
            public int LineNumber { get; set; }
            
            public ChapterInfo(string title, int lineNumber)
            {
                Title = title;
                LineNumber = lineNumber;
            }
        }
        
        public void SetText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                textLines = new string[0];
                chapters.Clear();
            }
            else
            {
                // 使用更准确的行分割方式
                string[] allLines = text.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                
                // 压缩连续的空白行（如果启用）
                if (settings != null && settings.CompressBlankLines)
                {
                    textLines = CompressBlankLines(allLines);
                }
                else
                {
                    textLines = allLines;
                }
                
                // 检测章节（只在前面部分检测以提高性能）
                DetectChapters();
            }
            scrollPosition = 0;
            pixelScrollOffset = 0; // 重置像素偏移
            this.Invalidate(); // 重绘
        }
        
        private string[] CompressBlankLines(string[] lines)
        {
            if (lines.Length == 0) return lines;
            
            List<string> compressedLines = new List<string>();
            int consecutiveBlankLines = 0;
            bool hasAddedContent = false; // 是否已经添加了非空内容
            
            for (int i = 0; i < lines.Length; i++)
            {
                string originalLine = lines[i];
                string trimmedLine = originalLine.Trim();
                
                if (string.IsNullOrEmpty(trimmedLine))
                {
                    consecutiveBlankLines++;
                    
                    // 文档开头的空白行完全移除
                    if (!hasAddedContent)
                    {
                        continue;
                    }
                    
                    // 中间最多保留1个空白行
                    if (consecutiveBlankLines == 1)
                    {
                        compressedLines.Add(""); // 添加一个空行
                    }
                    // 连续的多个空白行都忽略
                }
                else
                {
                    consecutiveBlankLines = 0;
                    hasAddedContent = true;
                    compressedLines.Add(originalLine); // 保留原始行（包含缩进）
                }
            }
            
            // 移除文档末尾的所有空白行
            while (compressedLines.Count > 0)
            {
                string lastLine = compressedLines[compressedLines.Count - 1];
                if (string.IsNullOrEmpty(lastLine.Trim()))
                {
                    compressedLines.RemoveAt(compressedLines.Count - 1);
                }
                else
                {
                    break;
                }
            }
            
            return compressedLines.ToArray();
        }
        
        private void DetectChapters()
        {
            chapters.Clear();
            
            for (int i = 0; i < textLines.Length; i++)
            {
                string line = textLines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                
                if (line.Length < 2 || line.Length > 80) continue;

                if (IsChapterTitle(line))
                {
                    chapters.Add(new ChapterInfo(line, i));
                }
            }

            RemoveTableOfContentsEntries();
        }

        private void RemoveTableOfContentsEntries()
        {
            int tableOfContentsLine = Array.FindIndex(textLines, line =>
                Regex.IsMatch(line.Trim(), @"^(目\s*录|contents?)$", RegexOptions.IgnoreCase));

            if (tableOfContentsLine < 0)
            {
                return;
            }

            int firstCatalogChapter = chapters.FindIndex(chapter => chapter.LineNumber > tableOfContentsLine);
            const int consecutiveMatchesRequired = 3;

            if (firstCatalogChapter < 0 || chapters.Count - firstCatalogChapter < consecutiveMatchesRequired * 2)
            {
                return;
            }

            // 正文通常会按相同顺序再次出现目录开头的章节。只有连续匹配多章时，
            // 才把第二组视为正文起点，避免因偶然重名而删除有效章节。
            for (int candidate = firstCatalogChapter + consecutiveMatchesRequired;
                 candidate <= chapters.Count - consecutiveMatchesRequired;
                 candidate++)
            {
                bool isRepeatedCatalogStart = true;

                for (int offset = 0; offset < consecutiveMatchesRequired; offset++)
                {
                    string catalogTitle = NormalizeChapterTitle(chapters[firstCatalogChapter + offset].Title);
                    string candidateTitle = NormalizeChapterTitle(chapters[candidate + offset].Title);

                    if (!string.Equals(catalogTitle, candidateTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        isRepeatedCatalogStart = false;
                        break;
                    }
                }

                if (isRepeatedCatalogStart)
                {
                    chapters = chapters.Skip(candidate).ToList();
                    return;
                }
            }
        }

        private static string NormalizeChapterTitle(string title)
        {
            return Regex.Replace(title.Trim(), @"\s+", " ");
        }
        
        private bool IsChapterTitle(string line)
        {
            const string chineseNumber = @"[零〇一二两三四五六七八九十百千万亿\d]+";
            string[] chapterPatterns = {
                $@"^第{chineseNumber}[章节回](?:\s*.*)?$",
                @"^Chapter\s+\d+(?:\s*[:：.、-]?\s*.*)?$",
                @"^(?:序章|楔子|引子|前言|后记|尾声|终章)(?:\s*.*)?$",
                @"^番外(?:篇)?(?:\s*[:：.、-]?\s*.*)?$"
            };
            
            foreach (string pattern in chapterPatterns)
            {
                if (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        
        
        public List<ChapterInfo> GetChapters()
        {
            return new List<ChapterInfo>(chapters);
        }
        
        
        public void ScrollUp()
        {
            ScrollByPixels(-30); // 向上滚动30像素，更平滑
        }
        
        public void ScrollDown()
        {
            ScrollByPixels(30); // 向下滚动30像素，更平滑
        }
        
        public void ScrollByPixels(float deltaPixels)
        {
            if (textLines.Length == 0) return;
            
            float oldOffset = pixelScrollOffset;
            
            // 更新像素偏移
            pixelScrollOffset += deltaPixels;
            
            // 改进的边界检查
            int lineHeight = Math.Max(textFont.Height / 2, textFont.Height + lineSpacing);
            int visibleAreaHeight = this.ClientSize.Height;
            int visibleLines = visibleAreaHeight / lineHeight; // 完全可见的行数
            
            // 计算更精确的最大滚动偏移
            // 确保最后一行完全显示时停止滚动
            float maxScrollOffset = Math.Max(0, (textLines.Length - visibleLines) * lineHeight);
            
            // 应用边界限制
            pixelScrollOffset = Math.Max(0, Math.Min(maxScrollOffset, pixelScrollOffset));
            
            // 在接近边界时，对齐到行边界，避免显示不完整的行
            if (pixelScrollOffset >= maxScrollOffset - lineHeight * 0.1f)
            {
                pixelScrollOffset = maxScrollOffset; // 精确对齐到底部
            }
            else if (pixelScrollOffset <= lineHeight * 0.1f)
            {
                pixelScrollOffset = 0; // 精确对齐到顶部
            }
            
            // 只有实际发生滚动时才重绘
            if (Math.Abs(pixelScrollOffset - oldOffset) > 0.1f)
            {
                // 更新基于行的滚动位置（用于兼容性）
                scrollPosition = (int)(pixelScrollOffset / lineHeight);
                
                // 立即重绘，不使用节流
                this.Invalidate();
            }
        }
        
        private float CalculateTotalContentHeight()
        {
            // 简化计算，提高性能
            int lineHeight = Math.Max(textFont.Height / 2, textFont.Height + lineSpacing);
            return textLines.Length * lineHeight;
        }
        
        public bool IsAtTop()
        {
            return pixelScrollOffset <= 0;
        }
        
        public bool IsAtBottom()
        {
            int lineHeight = Math.Max(textFont.Height / 2, textFont.Height + lineSpacing);
            int visibleAreaHeight = this.ClientSize.Height;
            int visibleLines = visibleAreaHeight / lineHeight; // 完全可见的行数
            
            // 计算最大滚动偏移：确保最后一行完全可见
            float maxScrollOffset = Math.Max(0, (textLines.Length - visibleLines) * lineHeight);
            return pixelScrollOffset >= maxScrollOffset - 1; // 允许1像素的误差
        }
        
        public void JumpToProgress(float percentage)
        {
            if (textLines.Length == 0) return;
            
            // 将百分比转换为行号
            int targetLine = (int)((percentage / 100.0f) * textLines.Length);
            targetLine = Math.Max(0, Math.Min(targetLine, textLines.Length - 1));
            
            JumpToLine(targetLine);
        }
        
        public void JumpToLine(int lineNumber)
        {
            if (textLines.Length == 0) return;
            
            // 确保行号在有效范围内
            lineNumber = Math.Max(0, Math.Min(lineNumber, textLines.Length - 1));
            
            // 计算目标像素偏移
            int lineHeight = Math.Max(textFont.Height / 2, textFont.Height + lineSpacing);
            float targetOffset = lineNumber * lineHeight;
            
            // 计算最大滚动偏移
            int visibleAreaHeight = this.ClientSize.Height;
            int visibleLines = visibleAreaHeight / lineHeight;
            float maxScrollOffset = Math.Max(0, (textLines.Length - visibleLines) * lineHeight);
            
            // 应用边界限制
            pixelScrollOffset = Math.Max(0, Math.Min(maxScrollOffset, targetOffset));
            
            // 更新基于行的滚动位置（用于兼容性）
            scrollPosition = (int)(pixelScrollOffset / lineHeight);
            
            // 立即重绘
            this.Invalidate();
        }
        
        public void JumpToChapter(int chapterIndex)
        {
            if (chapters.Count == 0 || chapterIndex < 0 || chapterIndex >= chapters.Count) return;
            
            // 跳转到指定章节的行号
            int targetLine = chapters[chapterIndex].LineNumber;
            JumpToLine(targetLine);
        }
        
        
        private int GetVisibleLines()
        {
            int lineHeight = Math.Max(textFont.Height / 2, textFont.Height + lineSpacing); // 允许负间距压缩
            int availableHeight = Math.Max(1, this.ClientSize.Height);
            return Math.Max(1, availableHeight / lineHeight);
        }
        
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            if (textLines.Length == 0) return;
            
            Graphics g = e.Graphics;
            // 使用清晰的文本渲染模式
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            
            // 预计算常用值（允许负的行间距来压缩字体自带的间距，但确保文字不会重叠）
            int lineHeight = Math.Max(textFont.Height / 2, textFont.Height + lineSpacing);
            const int startY = 0;
            int bottomY = this.ClientSize.Height;
            int leftPadding = Math.Min(15, this.Width / 12); // 减少左边距
            int rightPadding = Math.Min(15, this.Width / 12); // 添加右边距
            // 确保文本宽度随窗口宽度动态调整，最小宽度设为50像素以支持极窄窗口
            int textWidth = Math.Max(50, this.Width - leftPadding - rightPadding);
            
            // 完全清除整个控件背景，防止文字重叠
            g.Clear(this.BackColor);
            
            // 额外的背景填充，确保完全清除
            using (Brush bgBrush = new SolidBrush(this.BackColor))
            {
                g.FillRectangle(bgBrush, this.ClientRectangle);
            }
            
            using (Brush textBrush = new SolidBrush(textColor))
            {
                // 基于像素偏移的平滑滚动渲染
                float currentY = startY - (pixelScrollOffset % lineHeight);
                int startLineIndex = (int)(pixelScrollOffset / lineHeight);
                
                // 特殊处理：在顶部时，确保第一行完全显示
                if (IsAtTop())
                {
                    currentY = startY;
                    startLineIndex = 0;
                }
                
                // 确保起始行索引有效
                startLineIndex = Math.Max(0, Math.Min(startLineIndex, textLines.Length - 1));
                
                // 渲染可见区域的文本
                for (int i = startLineIndex; i < textLines.Length; i++)
                {
                    // 早期退出：如果超出底部边界，停止渲染
                    if (currentY >= bottomY) break;
                    
                    string line = textLines[i];
                    
                    if (string.IsNullOrEmpty(line))
                    {
                        // 空白行只表示段落间距；设置为0时不再额外占据一整行。
                        currentY += paragraphSpacing;
                        continue;
                    }
                    
                    // 计算实际需要的行高（支持自动换行）
                    float actualLineHeight = lineHeight;
                    
                    // 始终计算文本实际需要的高度，以支持自动换行
                    if (!string.IsNullOrEmpty(line))
                    {
                        Rectangle measureRect = new Rectangle(0, 0, textWidth, int.MaxValue);
                        Size textSize = TextRenderer.MeasureText(g, line, textFont, measureRect.Size, 
                            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak |
                            TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);
                        actualLineHeight = Math.Max(textFont.Height / 2, textSize.Height + lineSpacing);
                    }
                    
                    // 与窗口有交集的文字行都绘制，超出控件的部分由系统裁剪。
                    // 这样平滑滚动时不会因为丢弃半行而在顶部或底部留下空白。
                    bool isVisible = currentY < bottomY && currentY + actualLineHeight > startY;

                    if (isVisible)
                    {
                        Rectangle textRect = new Rectangle(
                            leftPadding, 
                            (int)currentY, 
                            textWidth, 
                            (int)actualLineHeight
                        );
                        
                        TextRenderer.DrawText(g, line, textFont, textRect, textColor, this.BackColor,
                            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | 
                            TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);
                    }
                    
                    currentY += actualLineHeight;
                }
            }
        }
        
        public void SetTextColor(Color color)
        {
            textColor = color;
            this.Invalidate();
        }
        
        public void SetTextFont(Font font)
        {
            textFont?.Dispose();
            textFont = font;
            this.Invalidate();
        }
        
        public void SetSpacing(int lineSpacing, int paragraphSpacing)
        {
            this.lineSpacing = lineSpacing;
            this.paragraphSpacing = Math.Max(0, paragraphSpacing);
            this.Invalidate();
        }
        
        public int GetScrollPosition()
        {
            return scrollPosition;
        }
        
        public void SetScrollPosition(int position)
        {
            scrollPosition = Math.Max(0, Math.Min(position, textLines.Length - 1));
            
            // 同步更新像素偏移
            int lineHeight = Math.Max(textFont.Height / 2, textFont.Height + lineSpacing);
            pixelScrollOffset = scrollPosition * lineHeight;
            
            this.Invalidate();
        }
        
        public float GetCurrentProgress()
        {
            if (textLines.Length == 0) return 0;
            
            // 计算当前的阅读进度百分比
            return (float)scrollPosition / textLines.Length * 100.0f;
        }
        
        public int GetCurrentChapterIndex()
        {
            if (chapters.Count == 0 || textLines.Length == 0) return -1;
            
            // 查找当前滚动位置所在的章节
            for (int i = chapters.Count - 1; i >= 0; i--)
            {
                if (scrollPosition >= chapters[i].LineNumber)
                {
                    return i;
                }
            }
            
            return -1;
        }
        
        public int GetTotalLines()
        {
            return textLines.Length;
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                textFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public partial class MainForm : Form
    {
        private TextDisplayPanel textDisplay;
        private ContextMenuStrip contextMenu;
        private ToolStripMenuItem openMenuItem;
        private ToolStripMenuItem openWebPageMenuItem;
        private ToolStripMenuItem backgroundColorMenuItem;
        private ToolStripMenuItem fontSettingsMenuItem;
        private ToolStripMenuItem chapterMenuItem;
        private ToolStripMenuItem progressMenuItem;
        private ToolStripMenuItem lastPositionMenuItem;
        private ToolStripMenuItem compressBlankLinesMenuItem;
        private ToolStripMenuItem spacingMenuItem;
        private ToolStripMenuItem exitMenuItem;
        private Label hoverLabel;
        
        private string currentFilePath = "";
        private bool isContentVisible = false;
        private bool hasTriedRestoringLastFile = false;
        private System.Windows.Forms.Timer progressSaveTimer; // 延迟保存进度的定时器
        
        // 网页导航相关
        private string currentWebPageUrl = "";      // 当前网页URL
        private string nextChapterUrl = "";         // 下一章链接
        private string prevChapterUrl = "";         // 上一章链接
        private ToolStripMenuItem nextChapterMenuItem;
        private ToolStripMenuItem prevChapterMenuItem;
        
        // 窗口拖拽相关
        private bool isDragging = false;
        private Point dragStartPoint;
        
        // 窗体大小调整相关
        private bool isResizing = false;
        private ResizeDirection resizeDirection = ResizeDirection.None;
        private const int ResizeBorderWidth = 2; // 调整边框宽度，设为最小
        private DateTime lastResizeTime = DateTime.MinValue;
        
        // 鼠标悬停检测定时器
        private System.Windows.Forms.Timer hoverTimer;
        
        // 应用设置
        private AppSettings settings;
        
        // 调整方向枚举
        private enum ResizeDirection
        {
            None,
            Left,
            Right,
            Top,
            Bottom,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        public MainForm()
        {
            // 注册编码提供程序，以支持GB18030等编码
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            
            // 加载设置
            settings = AppSettings.Load();
            
            InitializeComponent();

            Icon? applicationIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (applicationIcon != null)
            {
                this.Icon = applicationIcon;
            }

            SetupForm();
            CreateControls();
            CreateContextMenu();
            SetupLayout();
            InitializeHoverTimer();
            
            // 应用保存的设置
            ApplySettings();
            
            // 确保窗口调整大小时文本能正确换行
            this.Resize += MainForm_Resize;
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (hasTriedRestoringLastFile)
            {
                return;
            }

            hasTriedRestoringLastFile = true;
            string lastFilePath = settings.LastOpenedFilePath;

            if (string.IsNullOrWhiteSpace(lastFilePath))
            {
                return;
            }

            if (!File.Exists(lastFilePath))
            {
                settings.LastOpenedFilePath = "";
                settings.Save();
                return;
            }

            try
            {
                hoverLabel.Text = $"正在恢复上次打开的文件...\n{Path.GetFileName(lastFilePath)}";
                hoverLabel.ForeColor = Color.Yellow;
                await LoadFileAsync(lastFilePath);
            }
            catch
            {
                hoverLabel.Text = "欢迎使用极简单行阅读器，请点击右键菜单打开文本或者网页";
                hoverLabel.ForeColor = Color.LightGray;
                hoverLabel.Visible = false;
            }
        }
        
        private void MainForm_Resize(object sender, EventArgs e)
        {
            // 窗口大小改变时，强制文本显示区域重绘以更新文本换行
            textDisplay?.Invalidate();
        }
        
        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(value);
            if (value && hoverTimer != null)
            {
                hoverTimer.Start(); // 确保窗体显示时定时器启动
            }
        }
        
        private void ApplySettings()
        {
            try
            {
                // 应用窗体大小和位置
                this.Size = new Size(settings.WindowWidth, settings.WindowHeight);
                
                if (settings.WindowX >= 0 && settings.WindowY >= 0)
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = new Point(settings.WindowX, settings.WindowY);
                }
                
                // 应用背景颜色或透明背景
                if (settings.WindowOpacity == 0)
                {
                    // 启用背景透明模式
                    EnableTransparentBackground();
                }
                else
                {
                    // 正常背景模式
                    Color bgColor = settings.GetBackgroundColor();
                    this.BackColor = bgColor;
                    textDisplay.BackColor = bgColor;
                    this.TransparencyKey = Color.Empty;
                    this.Opacity = 1.0;
                }
                
                // 应用字体和颜色
                Font font = settings.GetFont();
                Color textColor = settings.GetTextColor();
                textDisplay.SetTextFont(font);
                textDisplay.SetTextColor(textColor);
                
                // 应用间距设置
                textDisplay.SetSpacing(settings.LineSpacing, settings.ParagraphSpacing);
            }
            catch
            {
                // 如果应用设置失败，使用默认设置
            }
        }
        
        private void SaveSettings()
        {
            try
            {
                // 保存窗体大小和位置
                settings.WindowWidth = this.Width;
                settings.WindowHeight = this.Height;
                settings.WindowX = this.Left;
                settings.WindowY = this.Top;
                
                // 保存设置到文件
                settings.Save();
            }
            catch
            {
                // 保存失败时静默处理
            }
        }

        private void SetupForm()
        {
            // 窗体基本设置 - 无标题栏
            this.Text = "";
            // 大小将在ApplySettings中设置
            this.MinimumSize = new Size(50, 50); // 保持最小尺寸限制
            this.StartPosition = FormStartPosition.CenterScreen; // 默认居中，可能被设置覆盖
            this.FormBorderStyle = FormBorderStyle.None; // 移除标题栏
            // 背景色将在ApplySettings中设置
            
            // 启用双缓冲减少闪烁
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
            
            // 允许拖拽移动窗口和调整大小
            this.MouseDown += MainForm_MouseDown;
            this.MouseMove += MainForm_MouseMove;
            this.MouseUp += MainForm_MouseUp;
        }

        private void CreateContextMenu()
        {
            // 创建右键菜单
            contextMenu = new ContextMenuStrip();
            
            // 菜单项
            openMenuItem = new ToolStripMenuItem("打开文本", null, OpenFile_Click);
            openWebPageMenuItem = new ToolStripMenuItem("打开网页", null, OpenWebPage_Click);
            prevChapterMenuItem = new ToolStripMenuItem("上一章 ←", null, PrevChapter_Click);
            nextChapterMenuItem = new ToolStripMenuItem("下一章 →", null, NextChapter_Click);
            prevChapterMenuItem.Enabled = false; // 初始禁用
            nextChapterMenuItem.Enabled = false; // 初始禁用
            chapterMenuItem = new ToolStripMenuItem("章节跳转", null, ShowChapterMenu_Click);
            progressMenuItem = new ToolStripMenuItem("进度跳转", null, ShowProgressDialog_Click);
            lastPositionMenuItem = new ToolStripMenuItem("跳转到上次位置", null, JumpToLastPosition_Click);
            compressBlankLinesMenuItem = new ToolStripMenuItem("压缩空白行", null, ToggleCompressBlankLines_Click);
            compressBlankLinesMenuItem.Checked = settings.CompressBlankLines;
            spacingMenuItem = new ToolStripMenuItem("间距设置", null, ShowSpacingDialog_Click);
            backgroundColorMenuItem = new ToolStripMenuItem("背景颜色", null, SetBackgroundColor_Click);
            var opacityMenuItem = new ToolStripMenuItem("窗口透明度", null, SetOpacity_Click);
            fontSettingsMenuItem = new ToolStripMenuItem("字体设置", null, SetFont_Click);
            exitMenuItem = new ToolStripMenuItem("退出", null, (s, e) => this.Close());
            
            // 添加菜单项
            contextMenu.Items.AddRange(new ToolStripItem[] {
                openMenuItem,
                openWebPageMenuItem,
                new ToolStripSeparator(),
                prevChapterMenuItem,
                nextChapterMenuItem,
                new ToolStripSeparator(),
                chapterMenuItem,
                progressMenuItem,
                lastPositionMenuItem,
                new ToolStripSeparator(),
                compressBlankLinesMenuItem,
                spacingMenuItem,
                new ToolStripSeparator(),
                backgroundColorMenuItem,
                opacityMenuItem,
                fontSettingsMenuItem,
                new ToolStripSeparator(),
                exitMenuItem
            });
            
            // 设置右键菜单
            this.ContextMenuStrip = contextMenu;
        }

        private void CreateControls()
        {
            // 创建悬停提示标签
            hoverLabel = new Label();
            hoverLabel.Text = "欢迎使用极简单行阅读器，请点击右键菜单打开文本或者网页";
            hoverLabel.Font = new Font("微软雅黑", 14F);
            hoverLabel.ForeColor = Color.LightGray;
            hoverLabel.BackColor = Color.Transparent;
            hoverLabel.TextAlign = ContentAlignment.MiddleCenter;
            hoverLabel.Dock = DockStyle.Fill;
            hoverLabel.Visible = false; // 初始隐藏，只有悬停时才显示
            
            // 为标签添加拖拽功能和右键菜单
            hoverLabel.MouseDown += MainForm_MouseDown;
            hoverLabel.MouseMove += MainForm_MouseMove;
            hoverLabel.MouseUp += MainForm_MouseUp;
            
            // 创建文本显示区域（初始隐藏）
            textDisplay = new TextDisplayPanel(settings);
            textDisplay.Dock = DockStyle.Fill;
            // 背景色和字体将在ApplySettings中设置
            textDisplay.Cursor = Cursors.Arrow; // 设置鼠标为箭头样式
            textDisplay.Visible = false;
            
            // 添加鼠标事件 - 监听所有控件的鼠标事件
            this.MouseEnter += MainForm_MouseEnter;
            this.MouseLeave += MainForm_MouseLeave;
            textDisplay.MouseEnter += MainForm_MouseEnter;
            textDisplay.MouseLeave += MainForm_MouseLeave;
            hoverLabel.MouseEnter += MainForm_MouseEnter;
            hoverLabel.MouseLeave += MainForm_MouseLeave;
            
            // 为文本显示区域添加拖拽和滚轮支持
            textDisplay.MouseDown += MainForm_MouseDown; // 支持拖拽窗体
            textDisplay.MouseMove += MainForm_MouseMove; // 支持拖拽窗体
            textDisplay.MouseUp += MainForm_MouseUp; // 支持拖拽窗体
            textDisplay.MouseWheel += TextDisplay_MouseWheel; // 滚轮滚动文本
        }

        private void SetupLayout()
        {
            // 添加控件到窗体
            this.Controls.Add(textDisplay);
            this.Controls.Add(hoverLabel);
            
            // 设置右键菜单（在创建contextMenu之后）
            this.ContextMenuStrip = contextMenu;
            hoverLabel.ContextMenuStrip = contextMenu;
            textDisplay.ContextMenuStrip = contextMenu;
        }
        
        private void MainForm_MouseEnter(object sender, EventArgs e)
        {
            // 定时器会处理显示逻辑，这里不需要做任何事
        }
        
        private void MainForm_MouseLeave(object sender, EventArgs e)
        {
            // 定时器会处理隐藏逻辑，这里不需要做任何事
        }
        
        private void InitializeHoverTimer()
        {
            hoverTimer = new System.Windows.Forms.Timer();
            hoverTimer.Interval = 200; // 200ms检查一次，降低频率
            
            // 创建进度保存定时器
            progressSaveTimer = new System.Windows.Forms.Timer();
            progressSaveTimer.Interval = 2000; // 2秒后保存，避免频繁写入
            progressSaveTimer.Tick += (s, e) =>
            {
                progressSaveTimer.Stop();
                SaveReadingProgress();
            };
            hoverTimer.Tick += CheckMousePosition;
            hoverTimer.Start();
        }
        
        private void CheckMousePosition(object sender, EventArgs e)
        {
            try
            {
                // 获取鼠标相对于窗体的位置
                Point mousePos = this.PointToClient(Control.MousePosition);
                bool mouseInWindow = this.ClientRectangle.Contains(mousePos);
                
                if (mouseInWindow)
                {
                    // 鼠标在窗体内，显示内容
                    if (isContentVisible)
                    {
                        // 有文件内容时显示文本
                        ShowTextContent();
                    }
                    else
                    {
                        // 没有文件时显示提示
                        ShowWelcomeMessage();
                    }
                }
                else
                {
                    // 鼠标不在窗体内，隐藏所有内容
                    HideAllContent();
                }
            }
            catch
            {
                // 忽略错误
            }
        }
        
        private void ShowTextContent()
        {
            textDisplay.Visible = true;
            hoverLabel.Visible = false;
            textDisplay.BringToFront();
        }
        
        private void ShowWelcomeMessage()
        {
            textDisplay.Visible = false;
            hoverLabel.Visible = true;
            hoverLabel.BringToFront();
        }
        
        private void HideAllContent()
        {
            textDisplay.Visible = false;
            hoverLabel.Visible = false;
        }
        
        
        // 鼠标滚轮控制文本滚动
        private void TextDisplay_MouseWheel(object sender, MouseEventArgs e)
        {
            if (textDisplay.Visible && isContentVisible)
            {
                // 检查滚动边界，提供边界反馈
                bool isAtTop = textDisplay.IsAtTop();
                bool isAtBottom = textDisplay.IsAtBottom();
                
                // 如果已经在边界，减少滚动量，提供阻尼效果
                float scrollAmount = e.Delta / 120.0f * 40;
                
                if ((e.Delta > 0 && isAtTop) || (e.Delta < 0 && isAtBottom))
                {
                    // 在边界处减少滚动量，提供阻尼感
                    scrollAmount *= 0.3f;
                }
                
                // 直接滚动，减少方法调用开销
                textDisplay.ScrollByPixels(-scrollAmount); // 注意：e.Delta为正时向上滚动
                
                // 延迟保存进度，避免影响滚动性能
                SaveReadingProgressDelayed();
            }
        }
        
        // 检测鼠标位置并设置光标
        private ResizeDirection GetResizeDirection(Point mousePos)
        {
            int x = mousePos.X;
            int y = mousePos.Y;
            int width = this.Width;
            int height = this.Height;
            
            // 更严格的边缘检测
            bool onLeft = x >= 0 && x <= ResizeBorderWidth;
            bool onRight = x >= width - ResizeBorderWidth && x < width;
            bool onTop = y >= 0 && y <= ResizeBorderWidth;
            bool onBottom = y >= height - ResizeBorderWidth && y < height;
            
            // 角落优先检测（需要同时满足两个边缘条件）
            if (onTop && onLeft) return ResizeDirection.TopLeft;
            if (onTop && onRight) return ResizeDirection.TopRight;
            if (onBottom && onLeft) return ResizeDirection.BottomLeft;
            if (onBottom && onRight) return ResizeDirection.BottomRight;
            
            // 边缘检测
            if (onTop) return ResizeDirection.Top;
            if (onBottom) return ResizeDirection.Bottom;
            if (onLeft) return ResizeDirection.Left;
            if (onRight) return ResizeDirection.Right;
            
            return ResizeDirection.None;
        }
        
        private void SetResizeCursor(ResizeDirection direction)
        {
            switch (direction)
            {
                case ResizeDirection.Left:
                case ResizeDirection.Right:
                    this.Cursor = Cursors.SizeWE;
                    break;
                case ResizeDirection.Top:
                case ResizeDirection.Bottom:
                    this.Cursor = Cursors.SizeNS;
                    break;
                case ResizeDirection.TopLeft:
                case ResizeDirection.BottomRight:
                    this.Cursor = Cursors.SizeNWSE;
                    break;
                case ResizeDirection.TopRight:
                case ResizeDirection.BottomLeft:
                    this.Cursor = Cursors.SizeNESW;
                    break;
                default:
                    this.Cursor = Cursors.Default;
                    break;
            }
        }
        
        // 窗口拖拽和调整大小功能
        private void MainForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                resizeDirection = GetResizeDirection(e.Location);
                
                if (resizeDirection != ResizeDirection.None)
                {
                    isResizing = true;
                    dragStartPoint = e.Location;
                }
                else
                {
                    isDragging = true;
                    dragStartPoint = e.Location;
                }
            }
        }
        
        private void MainForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (isResizing)
            {
                ResizeWindow(e.Location);
            }
            else if (isDragging)
            {
                Point newLocation = this.Location;
                newLocation.X += e.X - dragStartPoint.X;
                newLocation.Y += e.Y - dragStartPoint.Y;
                this.Location = newLocation;
            }
            else
            {
                // 只有在真正靠近边缘时才显示调整光标
                ResizeDirection direction = GetResizeDirection(e.Location);
                if (direction != ResizeDirection.None)
                {
                    SetResizeCursor(direction);
                }
                else
                {
                    this.Cursor = Cursors.Default;
                }
            }
        }
        
        private void MainForm_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
            isResizing = false;
            resizeDirection = ResizeDirection.None;
            this.Cursor = Cursors.Default;
        }
        
        private void ResizeWindow(Point mousePos)
        {
            // 防抖：限制调整频率
            DateTime now = DateTime.Now;
            if ((now - lastResizeTime).TotalMilliseconds < 16) // 限制为60fps
                return;
            lastResizeTime = now;
            
            int deltaX = mousePos.X - dragStartPoint.X;
            int deltaY = mousePos.Y - dragStartPoint.Y;
            
            // 限制调整幅度，防止变化过大
            const int MAX_DELTA = 20; // 减少到每次最大调整20像素
            deltaX = Math.Max(-MAX_DELTA, Math.Min(MAX_DELTA, deltaX));
            deltaY = Math.Max(-MAX_DELTA, Math.Min(MAX_DELTA, deltaY));
            
            // 如果变化太小，忽略
            if (Math.Abs(deltaX) < 2 && Math.Abs(deltaY) < 2)
                return;
            
            Rectangle bounds = this.Bounds;
            
            switch (resizeDirection)
            {
                case ResizeDirection.Left:
                    bounds.X += deltaX;
                    bounds.Width -= deltaX;
                    break;
                case ResizeDirection.Right:
                    bounds.Width += deltaX;
                    break;
                case ResizeDirection.Top:
                    bounds.Y += deltaY;
                    bounds.Height -= deltaY;
                    break;
                case ResizeDirection.Bottom:
                    bounds.Height += deltaY;
                    break;
                case ResizeDirection.TopLeft:
                    bounds.X += deltaX;
                    bounds.Y += deltaY;
                    bounds.Width -= deltaX;
                    bounds.Height -= deltaY;
                    break;
                case ResizeDirection.TopRight:
                    bounds.Y += deltaY;
                    bounds.Width += deltaX;
                    bounds.Height -= deltaY;
                    break;
                case ResizeDirection.BottomLeft:
                    bounds.X += deltaX;
                    bounds.Width -= deltaX;
                    bounds.Height += deltaY;
                    break;
                case ResizeDirection.BottomRight:
                    bounds.Width += deltaX;
                    bounds.Height += deltaY;
                    break;
            }
            
            // 设置合理的尺寸限制
            const int MIN_SIZE = 50; // 允许非常小的窗体
            const int MAX_SIZE = 1600;
            
            // 检查尺寸是否在合理范围内
            if (bounds.Width >= MIN_SIZE && bounds.Width <= MAX_SIZE && 
                bounds.Height >= MIN_SIZE && bounds.Height <= MAX_SIZE)
            {
                this.Bounds = bounds;
                // 更新拖拽起始点，使调整更平滑
                dragStartPoint = mousePos;
            }
        }

        private async void OpenFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择要打开的文件";
                dialog.Filter = "文本文件 (*.txt)|*.txt|" +
                               "电子书文件 (*.epub)|*.epub|" +
                               "Markdown文件 (*.md)|*.md|" +
                               "所有文件 (*.*)|*.*";
                dialog.FilterIndex = 1;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // 检查文件大小
                        FileInfo fileInfo = new FileInfo(dialog.FileName);
                        if (fileInfo.Length > 5 * 1024 * 1024) // 大于5MB
                        {
                            string message;
                            if (fileInfo.Length > 50 * 1024 * 1024)
                            {
                                message = $"文件很大 ({fileInfo.Length / 1024 / 1024:F1} MB)，将只加载前10MB内容以确保性能。是否继续？";
                            }
                            else
                            {
                                message = $"文件较大 ({fileInfo.Length / 1024 / 1024:F1} MB)，加载可能需要一些时间。是否继续？";
                            }
                            
                            var result = MessageBox.Show(message, "大文件提示", 
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            
                            if (result == DialogResult.No)
                                return;
                        }
                        
                        // 显示加载提示
                        hoverLabel.Text = $"正在加载文件，请稍候...\n文件大小: {fileInfo.Length / 1024 / 1024:F1} MB";
                        hoverLabel.ForeColor = Color.Yellow; // 改变颜色表示正在加载
                        Application.DoEvents(); // 立即更新UI
                        
                        await LoadFileAsync(dialog.FileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"打开文件时出错：{ex.Message}", "错误", 
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                        hoverLabel.Text = "欢迎使用极简单行阅读器，请点击右键菜单打开文本或者网页";
                        hoverLabel.Visible = false; // 错误后也保持空白
                    }
                }
            }
        }

        private async Task LoadFileAsync(string filePath)
        {
            try
            {
                // 异步读取文件内容
                string content = await ReadFileWithEncodingAsync(filePath);
                
                // 在UI线程上更新界面
                this.Invoke(new Action(() =>
                {
                    // 暂时禁用重绘以提高性能
                    textDisplay.SuspendLayout();
                    
                    // 使用新的Label方式设置文本
                    textDisplay.SetText(content);
                    
                    // 保持背景颜色一致
                    textDisplay.BackColor = this.BackColor;
                    
                    currentFilePath = filePath;
                    settings.LastOpenedFilePath = Path.GetFullPath(filePath);
                    settings.Save();

                    // 标记有内容，更新提示文字
                    isContentVisible = true;
                    hoverLabel.Text = $"已加载：{Path.GetFileName(filePath)}\n鼠标悬停查看内容";
                    hoverLabel.ForeColor = Color.LightGray; // 恢复正常颜色
                    
                    // 初始状态：隐藏所有内容，等待鼠标悬停
                    textDisplay.Visible = false;
                    hoverLabel.Visible = false;
                    
                    // 恢复阅读进度
                    RestoreReadingProgress();
                    
                    // 恢复重绘
                    textDisplay.ResumeLayout();
                }));
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    hoverLabel.Text = "欢迎使用极简单行阅读器，请点击右键菜单打开文本或者网页";
                    hoverLabel.Visible = false; // 错误时保持空白
                }));
                throw new Exception($"无法读取文件：{ex.Message}");
            }
        }

        private async Task<string> ReadFileWithEncodingAsync(string filePath)
        {
            FileInfo fileInfo = new FileInfo(filePath);
            
            // 移除大文件限制，允许加载完整文件
            // 但对于超大文件（>100MB）仍然进行限制
            if (fileInfo.Length > 100 * 1024 * 1024) // 大于100MB
            {
                return await ReadLargeFileAsync(filePath, 50 * 1024 * 1024); // 读取前50MB
            }
            
            // 使用简化的编码检测，避免复杂的异常
            Encoding detectedEncoding;
            try
            {
                detectedEncoding = await DetectFileEncodingAsync(filePath);
            }
            catch
            {
                // 如果编码检测失败，直接使用UTF-8
                detectedEncoding = Encoding.UTF8;
            }
            
            // 使用检测到的编码读取文件
            try
            {
                return await File.ReadAllTextAsync(filePath, detectedEncoding);
            }
            catch
            {
                // 如果检测的编码失败，按优先级尝试其他编码
                foreach (var encoding in GetSafeEncodings())
                {
                    try
                    {
                        return await File.ReadAllTextAsync(filePath, encoding);
                    }
                    catch
                    {
                        continue;
                    }
                }
                
                // 如果所有编码都失败，使用UTF-8强制读取
                try
                {
                    byte[] bytes = await File.ReadAllBytesAsync(filePath);
                    return Encoding.UTF8.GetString(bytes);
                }
                catch (Exception ex)
                {
                    throw new Exception($"无法读取文件: {ex.Message}");
                }
            }
        }
        
        private Encoding[] GetSafeEncodings()
        {
            // 只返回系统肯定支持的编码
            return new Encoding[]
            {
                Encoding.UTF8,
                Encoding.Default,
                Encoding.ASCII,
                Encoding.Unicode,
                Encoding.BigEndianUnicode
            };
        }
        
        private Encoding DetectFileEncoding(string filePath)
        {
            // 读取文件前几个字节来检测编码
            byte[] buffer = new byte[1024];
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                int bytesRead = fs.Read(buffer, 0, buffer.Length);
                
                // 检查BOM
                if (bytesRead >= 3)
                {
                    if (buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                        return Encoding.UTF8;
                    if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                        return Encoding.Unicode; // UTF-16 LE
                    if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                        return Encoding.BigEndianUnicode; // UTF-16 BE
                }
                
                // 检测中文编码
                if (IsLikelyGBK(buffer, bytesRead))
                {
                    return TryGetChineseEncoding();
                }
                
                // 检测是否为UTF-8
                if (IsValidUTF8(buffer, bytesRead))
                {
                    return Encoding.UTF8;
                }
            }
            
            // 默认返回UTF-8
            return Encoding.UTF8;
        }
        
        private bool IsLikelyGBK(byte[] buffer, int length)
        {
            int gbkCharCount = 0;
            int invalidCount = 0;
            
            for (int i = 0; i < length - 1; i++)
            {
                byte b1 = buffer[i];
                byte b2 = buffer[i + 1];
                
                // GBK编码范围：第一字节 0x81-0xFE，第二字节 0x40-0xFE（排除0x7F）
                if (b1 >= 0x81 && b1 <= 0xFE)
                {
                    if ((b2 >= 0x40 && b2 <= 0x7E) || (b2 >= 0x80 && b2 <= 0xFE))
                    {
                        gbkCharCount++;
                        i++; // 跳过第二个字节
                    }
                    else if (b1 >= 0x80)
                    {
                        invalidCount++;
                    }
                }
            }
            
            // 如果有足够多的GBK字符，且无效字符少，则认为是GBK
            return gbkCharCount > 5 && invalidCount < gbkCharCount;
        }
        
        private bool IsValidUTF8(byte[] buffer, int length)
        {
            int i = 0;
            int utf8CharCount = 0;
            int invalidCount = 0;
            
            while (i < length)
            {
                byte b = buffer[i];
                
                if (b < 0x80)
                {
                    // ASCII字符
                    i++;
                    continue;
                }
                else if ((b & 0xE0) == 0xC0)
                {
                    // 2字节UTF-8序列
                    if (i + 1 >= length || (buffer[i + 1] & 0xC0) != 0x80)
                    {
                        invalidCount++;
                        i++;
                        continue;
                    }
                    utf8CharCount++;
                    i += 2;
                }
                else if ((b & 0xF0) == 0xE0)
                {
                    // 3字节UTF-8序列（常见中文）
                    if (i + 2 >= length || 
                        (buffer[i + 1] & 0xC0) != 0x80 || 
                        (buffer[i + 2] & 0xC0) != 0x80)
                    {
                        invalidCount++;
                        i++;
                        continue;
                    }
                    utf8CharCount++;
                    i += 3;
                }
                else if ((b & 0xF8) == 0xF0)
                {
                    // 4字节UTF-8序列
                    if (i + 3 >= length || 
                        (buffer[i + 1] & 0xC0) != 0x80 || 
                        (buffer[i + 2] & 0xC0) != 0x80 || 
                        (buffer[i + 3] & 0xC0) != 0x80)
                    {
                        invalidCount++;
                        i++;
                        continue;
                    }
                    utf8CharCount++;
                    i += 4;
                }
                else
                {
                    // 无效的UTF-8起始字节
                    invalidCount++;
                    i++;
                }
            }
            
            // UTF-8有效条件：无效字符数很少
            return invalidCount == 0 || (utf8CharCount > 0 && invalidCount < utf8CharCount / 10);
        }
        
        private async Task<Encoding> DetectFileEncodingAsync(string filePath)
        {
            byte[] buffer = new byte[8192]; // 增加检测缓冲区大小
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                int bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length);
                
                // 1. 检测BOM（最可靠的方式）
                if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                    return Encoding.UTF8;
                if (bytesRead >= 4 && buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] == 0x00 && buffer[3] == 0x00)
                    return Encoding.UTF32; // UTF-32 LE (要在UTF-16 LE之前检查)
                if (bytesRead >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
                    return Encoding.Unicode; // UTF-16 LE
                if (bytesRead >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
                    return Encoding.BigEndianUnicode; // UTF-16 BE
                
                // 2. 先严格验证UTF-8（因为UTF-8有明确的格式规则）
                bool isValidUtf8 = IsValidUTF8(buffer, bytesRead);
                bool isLikelyGbk = IsLikelyGBK(buffer, bytesRead);
                
                // 3. 如果是有效的UTF-8且不像GBK，就用UTF-8
                if (isValidUtf8 && !isLikelyGbk)
                {
                    return Encoding.UTF8;
                }
                
                // 4. 如果像GBK编码
                if (isLikelyGbk)
                {
                    // 尝试用GBK和UTF-8分别解码，看哪个产生更少的错误
                    try
                    {
                        var gbkEncoding = Encoding.GetEncoding("GB18030");
                        string gbkText = gbkEncoding.GetString(buffer, 0, bytesRead);
                        string utf8Text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        
                        // 计算乱码特征（替换字符的数量）
                        int gbkBadChars = CountBadCharacters(gbkText);
                        int utf8BadChars = CountBadCharacters(utf8Text);
                        
                        // 选择产生更少乱码的编码
                        if (gbkBadChars < utf8BadChars)
                        {
                            return gbkEncoding;
                        }
                        else if (utf8BadChars < gbkBadChars)
                        {
                            return Encoding.UTF8;
                        }
                        else
                        {
                            // 如果差不多，优先GBK（因为IsLikelyGBK返回true）
                            return gbkEncoding;
                        }
                    }
                    catch
                    {
                        return TryGetChineseEncoding();
                    }
                }
                
                // 5. 如果是有效UTF-8（即使可能也像GBK）
                if (isValidUtf8)
                {
                    return Encoding.UTF8;
                }
                
                // 6. 统计字节特征作为最后手段
                var stats = AnalyzeBytePatterns(buffer, bytesRead);
                
                if (stats.HasChinesePattern)
                {
                    return TryGetChineseEncoding();
                }
                
                // 7. 默认使用系统默认编码（中文系统通常是GBK）
                return Encoding.Default;
            }
        }
        
        private int CountBadCharacters(string text)
        {
            int count = 0;
            foreach (char c in text)
            {
                // 统计替换字符和不可见控制字符
                if (c == '\uFFFD' ||  // 替换字符
                    (c < 0x20 && c != '\r' && c != '\n' && c != '\t') || // 控制字符
                    (c >= 0xE000 && c <= 0xF8FF)) // 私用区
                {
                    count++;
                }
            }
            return count;
        }
        
        private Encoding TryGetChineseEncoding()
        {
            // 按优先级尝试中文编码
            string[] chineseEncodings = { "GB18030", "GBK", "GB2312", "Big5" };
            
            foreach (string encodingName in chineseEncodings)
            {
                try
                {
                    return Encoding.GetEncoding(encodingName);
                }
                catch
                {
                    continue; // 如果不支持，尝试下一个
                }
            }
            
            // 如果所有中文编码都不支持，返回UTF-8
            return Encoding.UTF8;
        }
        
        private struct ByteStats
        {
            public bool HasHighAsciiRatio;
            public bool HasChinesePattern;
            public bool HasNullBytes;
            public double AsciiRatio;
        }
        
        private ByteStats AnalyzeBytePatterns(byte[] buffer, int length)
        {
            int asciiCount = 0;
            int chinesePatternCount = 0;
            int nullCount = 0;
            
            for (int i = 0; i < length; i++)
            {
                byte b = buffer[i];
                
                if (b == 0) nullCount++;
                else if (b < 128) asciiCount++;
                else if (i < length - 1)
                {
                    byte next = buffer[i + 1];
                    // 检测可能的中文字符模式
                    if ((b >= 0xA1 && b <= 0xFE) && (next >= 0xA1 && next <= 0xFE))
                        chinesePatternCount++;
                    else if ((b >= 0x81 && b <= 0xFE) && 
                            ((next >= 0x40 && next <= 0x7E) || (next >= 0x80 && next <= 0xFE)))
                        chinesePatternCount++;
                }
            }
            
            double asciiRatio = (double)asciiCount / length;
            
            return new ByteStats
            {
                HasHighAsciiRatio = asciiRatio > 0.7,
                HasChinesePattern = chinesePatternCount > length * 0.1,
                HasNullBytes = nullCount > 0,
                AsciiRatio = asciiRatio
            };
        }
        
        private Encoding[] GetFallbackEncodings()
        {
            List<Encoding> encodings = new List<Encoding>();
            
            // 始终可用的编码
            encodings.Add(Encoding.UTF8);
            encodings.Add(Encoding.Default);
            encodings.Add(Encoding.ASCII);
            encodings.Add(Encoding.Unicode);
            encodings.Add(Encoding.BigEndianUnicode);
            
            // 尝试添加中文编码（可能不支持）
            string[] chineseEncodings = { "GBK", "GB18030", "GB2312", "Big5" };
            foreach (string encodingName in chineseEncodings)
            {
                try
                {
                    encodings.Add(Encoding.GetEncoding(encodingName));
                }
                catch
                {
                    // 如果不支持该编码，跳过
                    continue;
                }
            }
            
            return encodings.ToArray();
        }
        
        private async Task<string> ReadLargeFileAsync(string filePath, int maxBytes)
        {
            // 先检测编码
            Encoding encoding = DetectFileEncoding(filePath);
            
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                // 读取指定字节数
                byte[] buffer = new byte[Math.Min(maxBytes, (int)fs.Length)];
                int bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length);
                
                // 使用检测到的编码解码
                try
                {
                    return encoding.GetString(buffer, 0, bytesRead);
                }
                catch
                {
                    // 如果解码失败，尝试其他编码
                    try
                    {
                        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    }
                    catch
                    {
                        return Encoding.Default.GetString(buffer, 0, bytesRead);
                    }
                }
            }
        }
        
        // 保留同步版本用于拖拽
        private string ReadFileWithEncoding(string filePath)
        {
            // 检测文件编码
            Encoding encoding = DetectFileEncoding(filePath);
            
            // 使用检测到的编码读取文件
            try
            {
                return File.ReadAllText(filePath, encoding);
            }
            catch
            {
                // 如果检测的编码失败，按优先级尝试其他编码
                foreach (var fallbackEncoding in GetFallbackEncodings())
                {
                    try
                    {
                        return File.ReadAllText(filePath, fallbackEncoding);
                    }
                    catch
                    {
                        continue;
                    }
                }
                
                // 如果所有编码都失败，抛出异常
                throw new Exception("无法以任何支持的编码读取文件");
            }
        }

        private void SetBackgroundColor_Click(object sender, EventArgs e)
        {
            using (ColorPickerForm colorPicker = new ColorPickerForm())
            {
                if (colorPicker.ShowDialog() == DialogResult.OK)
                {
                    Color selectedColor = colorPicker.SelectedColor;
                    if (selectedColor != Color.Empty)
                    {
                        // 如果之前是透明模式，先禁用它
                        if (settings.WindowOpacity == 0)
                        {
                            settings.WindowOpacity = 100;
                            this.TransparencyKey = Color.Empty;
                            this.Opacity = 1.0;
                        }
                        
                        // 设置文本框背景色
                        textDisplay.BackColor = selectedColor;
                        
                        // 同时设置窗体背景色
                        this.BackColor = selectedColor;
                        
                        // 保存背景颜色设置
                        settings.SetBackgroundColor(selectedColor);
                        settings.Save();
                        
                        // 更新提示信息
                        if (isContentVisible)
                        {
                            hoverLabel.Text = $"已加载：{Path.GetFileName(currentFilePath)}\n鼠标悬停查看内容\n背景色已更新";
                        }
                        else
                        {
                            hoverLabel.Text = "欢迎使用极简单行阅读器，请点击右键菜单打开文本或者网页\n背景色已更新";
                        }
                        
                        // 延迟恢复原始提示
                        System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
                        timer.Interval = 2000; // 2秒后恢复
                        timer.Tick += (s, args) =>
                        {
                            timer.Stop();
                            timer.Dispose();
                            if (isContentVisible)
                            {
                                hoverLabel.Text = $"已加载：{Path.GetFileName(currentFilePath)}\n鼠标悬停查看内容";
                            }
                            else
                            {
                                hoverLabel.Text = "欢迎使用极简单行阅读器，请点击右键菜单打开文本或者网页";
                            }
                        };
                        timer.Start();
                    }
                }
            }
        }
        
        private void SetOpacity_Click(object sender, EventArgs e)
        {
            using (Form opacityDialog = new Form())
            {
                opacityDialog.Text = "背景透明设置";
                opacityDialog.Size = new Size(320, 150);
                opacityDialog.StartPosition = FormStartPosition.CenterParent;
                opacityDialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                opacityDialog.MaximizeBox = false;
                opacityDialog.MinimizeBox = false;
                opacityDialog.BackColor = Color.FromArgb(45, 45, 48);
                opacityDialog.ForeColor = Color.White;
                
                CheckBox transparentCheckBox = new CheckBox();
                transparentCheckBox.Text = "启用背景透明（只有背景透明，文字保持清晰）";
                transparentCheckBox.Location = new Point(20, 25);
                transparentCheckBox.Size = new Size(280, 25);
                transparentCheckBox.ForeColor = Color.White;
                transparentCheckBox.Checked = settings.WindowOpacity == 0; // 0表示透明模式
                
                Label hintLabel = new Label();
                hintLabel.Text = "提示: 启用后窗口背景完全透明，文字悬浮显示";
                hintLabel.Location = new Point(20, 55);
                hintLabel.Size = new Size(280, 20);
                hintLabel.ForeColor = Color.Gray;
                hintLabel.Font = new Font("微软雅黑", 8.5F);
                
                Button okButton = new Button();
                okButton.Text = "确定";
                okButton.Location = new Point(130, 85);
                okButton.Size = new Size(70, 30);
                okButton.Click += (s, args) =>
                {
                    if (transparentCheckBox.Checked)
                    {
                        // 启用背景透明
                        settings.WindowOpacity = 0;
                        EnableTransparentBackground();
                    }
                    else
                    {
                        // 禁用背景透明
                        settings.WindowOpacity = 100;
                        DisableTransparentBackground();
                    }
                    settings.Save();
                    opacityDialog.Close();
                };
                
                Button cancelButton = new Button();
                cancelButton.Text = "取消";
                cancelButton.Location = new Point(210, 85);
                cancelButton.Size = new Size(70, 30);
                cancelButton.Click += (s, args) => opacityDialog.Close();
                
                opacityDialog.Controls.AddRange(new Control[] { 
                    transparentCheckBox, hintLabel, okButton, cancelButton 
                });
                
                opacityDialog.ShowDialog(this);
            }
        }
        
        private void EnableTransparentBackground()
        {
            // 使用一个极其特殊的颜色作为透明键（几乎是黑色但带一点点颜色，用户不会用到）
            Color transparentColor = Color.FromArgb(1, 1, 1);
            this.BackColor = transparentColor;
            this.TransparencyKey = transparentColor;
            textDisplay.BackColor = transparentColor;
            
            // 确保文字颜色使用用户设置的颜色（不受透明背景影响）
            Color textColorSetting = settings.GetTextColor();
            textDisplay.SetTextColor(textColorSetting);
            hoverLabel.ForeColor = textColorSetting;
            hoverLabel.BackColor = transparentColor; // 让 hoverLabel 背景也透明
            
            this.Opacity = 1.0; // 确保整体不透明，只有背景透明
        }
        
        private void DisableTransparentBackground()
        {
            // 恢复正常背景
            this.TransparencyKey = Color.Empty;
            Color bgColor = settings.GetBackgroundColor();
            this.BackColor = bgColor;
            textDisplay.BackColor = bgColor;
            
            // 恢复文字颜色
            Color textColorSetting = settings.GetTextColor();
            textDisplay.SetTextColor(textColorSetting);
            hoverLabel.ForeColor = Color.LightGray;
            hoverLabel.BackColor = Color.Transparent;
            
            this.Opacity = 1.0;
        }

        private void SetFont_Click(object sender, EventArgs e)
        {
            // 创建字体和颜色设置对话框
            using (Form fontForm = new Form())
            {
                fontForm.Text = "字体设置";
                fontForm.Size = new Size(400, 200);
                fontForm.StartPosition = FormStartPosition.CenterParent;
                fontForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                fontForm.MaximizeBox = false;
                fontForm.MinimizeBox = false;
                
                // 读取已保存的字体和颜色设置
                Font selectedFont = settings.GetFont();
                Color selectedColor = settings.GetTextColor();
                
                Label fontLabel = new Label();
                fontLabel.Text = "字体：";
                fontLabel.Location = new Point(20, 20);
                fontLabel.Size = new Size(50, 20);
                
                Button fontButton = new Button();
                fontButton.Text = $"{selectedFont.Name} {selectedFont.Size}pt"; // 显示当前字体
                fontButton.Location = new Point(80, 18);
                fontButton.Size = new Size(180, 25);
                
                fontButton.Click += (s, args) =>
                {
                    using (FontDialog dialog = new FontDialog())
                    {
                        dialog.Font = selectedFont;
                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            selectedFont = dialog.Font;
                            fontButton.Text = $"{selectedFont.Name} {selectedFont.Size}pt";
                        }
                    }
                };
                
                Label colorLabel = new Label();
                colorLabel.Text = "颜色：";
                colorLabel.Location = new Point(20, 60);
                colorLabel.Size = new Size(50, 20);
                
                Button colorButton = new Button();
                colorButton.Text = "选择颜色";
                colorButton.Location = new Point(80, 58);
                colorButton.Size = new Size(100, 25);
                colorButton.BackColor = selectedColor; // 显示当前颜色
                colorButton.ForeColor = GetContrastColor(selectedColor);
                
                colorButton.Click += (s, args) =>
                {
                    using (ColorPickerForm colorPicker = new ColorPickerForm())
                    {
                        if (colorPicker.ShowDialog() == DialogResult.OK)
                        {
                            selectedColor = colorPicker.SelectedColor;
                            if (selectedColor != Color.Empty)
                            {
                                colorButton.BackColor = selectedColor;
                                colorButton.ForeColor = GetContrastColor(selectedColor);
                            }
                        }
                    }
                };
                
                Button okButton = new Button();
                okButton.Text = "确定";
                okButton.Location = new Point(200, 120);
                okButton.Size = new Size(60, 30);
                okButton.Click += (s, args) =>
                {
                    // 在改变字体前，记录当前阅读的行号位置
                    int currentLine = textDisplay.GetScrollPosition();
                    
                    // 应用新字体和颜色
                    textDisplay.SetTextFont(selectedFont);
                    textDisplay.SetTextColor(selectedColor);
                    
                    // 字体改变后，重新跳转到原来的行号位置
                    textDisplay.JumpToLine(currentLine);
                    
                    // 保存字体和颜色设置
                    settings.SetFont(selectedFont);
                    settings.SetTextColor(selectedColor);
                    settings.Save();
                    
                    fontForm.Close();
                };
                
                Button cancelButton = new Button();
                cancelButton.Text = "取消";
                cancelButton.Location = new Point(270, 120);
                cancelButton.Size = new Size(60, 30);
                cancelButton.Click += (s, args) => fontForm.Close();
                
                fontForm.Controls.AddRange(new Control[] { 
                    fontLabel, fontButton, colorLabel, colorButton, okButton, cancelButton 
                });
                
                fontForm.ShowDialog(this);
            }
        }
        
        // 获取对比色，确保按钮文字可见
        private Color GetContrastColor(Color color)
        {
            double brightness = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255;
            return brightness > 0.5 ? Color.Black : Color.White;
        }
        
        // 保存当前阅读进度
        private void SaveReadingProgress()
        {
            if (!string.IsNullOrEmpty(currentFilePath))
            {
                int currentPosition = textDisplay.GetScrollPosition();
                settings.ReadingProgress[currentFilePath] = currentPosition;
                settings.Save();
            }
        }
        
        // 恢复阅读进度
        private void RestoreReadingProgress()
        {
            if (!string.IsNullOrEmpty(currentFilePath) && 
                settings.ReadingProgress.ContainsKey(currentFilePath))
            {
                int savedPosition = settings.ReadingProgress[currentFilePath];
                textDisplay.SetScrollPosition(savedPosition);
            }
        }
        
        // 延迟保存阅读进度
        private void SaveReadingProgressDelayed()
        {
            progressSaveTimer.Stop(); // 重置定时器
            progressSaveTimer.Start(); // 开始计时
        }
        
        private void JumpToLastPosition_Click(object sender, EventArgs e)
        {
            if (!isContentVisible)
            {
                MessageBox.Show("请先打开一个文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            if (!string.IsNullOrEmpty(currentFilePath) && 
                settings.ReadingProgress.ContainsKey(currentFilePath))
            {
                int savedPosition = settings.ReadingProgress[currentFilePath];
                textDisplay.SetScrollPosition(savedPosition);
                
                // 显示跳转信息
                MessageBox.Show($"已跳转到上次阅读位置（第{savedPosition + 1}行）", "跳转完成", 
                              MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("此文件没有保存的阅读进度", "提示", 
                              MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        
        private void ShowSpacingDialog_Click(object sender, EventArgs e)
        {
            // 创建间距设置对话框
            Form spacingDialog = new Form();
            spacingDialog.Text = "间距设置";
            spacingDialog.Size = new Size(300, 200);
            spacingDialog.StartPosition = FormStartPosition.CenterParent;
            spacingDialog.FormBorderStyle = FormBorderStyle.FixedDialog;
            spacingDialog.MaximizeBox = false;
            spacingDialog.MinimizeBox = false;
            spacingDialog.BackColor = Color.FromArgb(45, 45, 48);
            spacingDialog.ForeColor = Color.White;
            
            // 行间距设置
            Label lineSpacingLabel = new Label();
            lineSpacingLabel.Text = "行间距 (像素):";
            lineSpacingLabel.Location = new Point(20, 20);
            lineSpacingLabel.Size = new Size(100, 20);
            lineSpacingLabel.ForeColor = Color.White;
            
            NumericUpDown lineSpacingInput = new NumericUpDown();
            lineSpacingInput.Location = new Point(130, 18);
            lineSpacingInput.Size = new Size(120, 20);
            lineSpacingInput.Minimum = -10; // 允许负值来压缩字体自带的间距
            lineSpacingInput.Maximum = 20;
            lineSpacingInput.Value = settings.LineSpacing;
            lineSpacingInput.BackColor = Color.FromArgb(60, 60, 60);
            lineSpacingInput.ForeColor = Color.White;
            
            // 段落间距设置
            Label paragraphSpacingLabel = new Label();
            paragraphSpacingLabel.Text = "段落间距 (像素):";
            paragraphSpacingLabel.Location = new Point(20, 60);
            paragraphSpacingLabel.Size = new Size(100, 20);
            paragraphSpacingLabel.ForeColor = Color.White;
            
            NumericUpDown paragraphSpacingInput = new NumericUpDown();
            paragraphSpacingInput.Location = new Point(130, 58);
            paragraphSpacingInput.Size = new Size(120, 20);
            paragraphSpacingInput.Minimum = 0;
            paragraphSpacingInput.Maximum = 50;
            paragraphSpacingInput.Value = Math.Max((int)paragraphSpacingInput.Minimum,
                Math.Min((int)paragraphSpacingInput.Maximum, settings.ParagraphSpacing));
            paragraphSpacingInput.BackColor = Color.FromArgb(60, 60, 60);
            paragraphSpacingInput.ForeColor = Color.White;
            
            // 确定按钮
            Button okButton = new Button();
            okButton.Text = "确定";
            okButton.Location = new Point(100, 110);
            okButton.Size = new Size(80, 30);
            okButton.BackColor = Color.FromArgb(0, 122, 204);
            okButton.ForeColor = Color.White;
            okButton.FlatStyle = FlatStyle.Flat;
            okButton.Click += (s, args) =>
            {
                // 保存设置
                settings.LineSpacing = (int)lineSpacingInput.Value;
                settings.ParagraphSpacing = (int)paragraphSpacingInput.Value;
                settings.Save();
                
                // 更新文本显示
                textDisplay.SetSpacing(settings.LineSpacing, settings.ParagraphSpacing);
                
                spacingDialog.DialogResult = DialogResult.OK;
                spacingDialog.Close();
            };
            
            // 取消按钮
            Button cancelButton = new Button();
            cancelButton.Text = "取消";
            cancelButton.Location = new Point(190, 110);
            cancelButton.Size = new Size(80, 30);
            cancelButton.BackColor = Color.FromArgb(60, 60, 60);
            cancelButton.ForeColor = Color.White;
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.Click += (s, args) =>
            {
                spacingDialog.DialogResult = DialogResult.Cancel;
                spacingDialog.Close();
            };
            
            // 添加控件到对话框
            spacingDialog.Controls.AddRange(new Control[] {
                lineSpacingLabel, lineSpacingInput,
                paragraphSpacingLabel, paragraphSpacingInput,
                okButton, cancelButton
            });
            
            // 显示对话框
            spacingDialog.ShowDialog(this);
        }
        
        private void ToggleCompressBlankLines_Click(object sender, EventArgs e)
        {
            // 切换空白行压缩设置
            settings.CompressBlankLines = !settings.CompressBlankLines;
            compressBlankLinesMenuItem.Checked = settings.CompressBlankLines;
            settings.Save();
            
            // 如果有打开的文件，重新处理文本
            if (isContentVisible && !string.IsNullOrEmpty(currentFilePath))
            {
                try
                {
                    string content = ReadFileWithEncoding(currentFilePath);
                    
                    // 显示处理前后的行数对比（调试信息）
                    string[] originalLines = content.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    textDisplay.SetText(content);
                    
                    // 显示提示信息
                    string status = settings.CompressBlankLines ? "已启用" : "已禁用";
                    if (isContentVisible)
                    {
                        hoverLabel.Text = $"已加载：{Path.GetFileName(currentFilePath)}\n鼠标悬停查看内容\n空白行压缩{status}\n原始行数: {originalLines.Length}";
                    }
                    
                    // 延迟恢复原始提示
                    System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
                    timer.Interval = 3000; // 延长显示时间以便看到调试信息
                    timer.Tick += (s, args) =>
                    {
                        timer.Stop();
                        timer.Dispose();
                        if (isContentVisible)
                        {
                            hoverLabel.Text = $"已加载：{Path.GetFileName(currentFilePath)}\n鼠标悬停查看内容";
                        }
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"重新处理文件时出错：{ex.Message}", "错误", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                // 如果没有打开文件，只显示状态
                string status = settings.CompressBlankLines ? "已启用" : "已禁用";
                MessageBox.Show($"空白行压缩{status}", "设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        
        private void ShowChapterMenu_Click(object sender, EventArgs e)
        {
            if (!isContentVisible)
            {
                MessageBox.Show("请先打开一个文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            var chapters = textDisplay.GetChapters();
            if (chapters.Count == 0)
            {
                MessageBox.Show("未检测到章节信息", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            // 获取当前所在章节
            int currentChapterIndex = textDisplay.GetCurrentChapterIndex();
            
            // 创建章节选择对话框
            using (Form chapterForm = new Form())
            {
                chapterForm.Text = $"选择章节 (共{chapters.Count}章)";
                chapterForm.Size = new Size(500, 450);
                chapterForm.StartPosition = FormStartPosition.CenterParent;
                chapterForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                chapterForm.MaximizeBox = false;
                chapterForm.MinimizeBox = false;
                
                // 添加当前章节提示
                Label currentChapterLabel = new Label();
                if (currentChapterIndex >= 0)
                {
                    currentChapterLabel.Text = $"当前章节：{chapters[currentChapterIndex].Title}";
                }
                else
                {
                    currentChapterLabel.Text = "当前位置：未在任何章节中";
                }
                currentChapterLabel.Dock = DockStyle.Top;
                currentChapterLabel.Height = 30;
                currentChapterLabel.TextAlign = ContentAlignment.MiddleCenter;
                currentChapterLabel.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
                currentChapterLabel.ForeColor = Color.DarkBlue;
                currentChapterLabel.Padding = new Padding(5);
                
                ListBox listBox = new ListBox();
                listBox.Dock = DockStyle.Fill;
                listBox.Font = new Font("微软雅黑", 10F);
                listBox.Margin = new Padding(10);
                
                // 优化ListBox性能
                listBox.BeginUpdate(); // 暂停重绘
                listBox.IntegralHeight = false; // 允许部分项显示
                listBox.ScrollAlwaysVisible = true; // 始终显示滚动条
                
                for (int i = 0; i < chapters.Count; i++)
                {
                    string chapterTitle = chapters[i].Title.Trim();
                    
                    // 清理章节标题，移除多余的空白和特殊字符
                    chapterTitle = System.Text.RegularExpressions.Regex.Replace(chapterTitle, @"\s+", " ");
                    
                    // 限制显示长度，但保留更多内容
                    string displayText;
                    if (chapterTitle.Length > 60)
                    {
                        displayText = $"{i + 1}. {chapterTitle.Substring(0, 60)}...";
                    }
                    else
                    {
                        displayText = $"{i + 1}. {chapterTitle}";
                    }
                    
                    // 标记当前章节
                    if (i == currentChapterIndex)
                    {
                        displayText = "★ " + displayText + " (当前)";
                    }
                    
                    listBox.Items.Add(displayText);
                }
                
                // 选中当前章节
                if (currentChapterIndex >= 0 && currentChapterIndex < listBox.Items.Count)
                {
                    listBox.SelectedIndex = currentChapterIndex;
                    listBox.TopIndex = Math.Max(0, currentChapterIndex - 3); // 滚动到可见位置
                }
                
                listBox.EndUpdate(); // 恢复重绘，提高性能
                
                listBox.DoubleClick += (s, args) =>
                {
                    if (listBox.SelectedIndex >= 0)
                    {
                        textDisplay.JumpToChapter(listBox.SelectedIndex);
                        SaveReadingProgress(); // 跳转后保存进度
                        chapterForm.Close();
                    }
                };
                
                // 添加按钮面板
                Panel buttonPanel = new Panel();
                buttonPanel.Height = 50;
                buttonPanel.Dock = DockStyle.Bottom;
                
                Button jumpButton = new Button();
                jumpButton.Text = "跳转";
                jumpButton.Size = new Size(70, 30);
                jumpButton.Location = new Point(320, 10);
                jumpButton.Click += (s, args) =>
                {
                    if (listBox.SelectedIndex >= 0)
                    {
                        textDisplay.JumpToChapter(listBox.SelectedIndex);
                        SaveReadingProgress(); // 跳转后保存进度
                        chapterForm.Close();
                    }
                };
                
                Button cancelButton = new Button();
                cancelButton.Text = "取消";
                cancelButton.Size = new Size(70, 30);
                cancelButton.Location = new Point(400, 10);
                cancelButton.Click += (s, args) => chapterForm.Close();
                
                buttonPanel.Controls.AddRange(new Control[] { jumpButton, cancelButton });
                
                chapterForm.Controls.Add(currentChapterLabel);
                chapterForm.Controls.Add(listBox);
                chapterForm.Controls.Add(buttonPanel);
                chapterForm.ShowDialog(this);
            }
        }
        
        private void ShowProgressDialog_Click(object sender, EventArgs e)
        {
            if (!isContentVisible)
            {
                MessageBox.Show("请先打开一个文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            // 获取当前进度
            float currentProgress = textDisplay.GetCurrentProgress();
            int currentLine = textDisplay.GetScrollPosition();
            int totalLines = textDisplay.GetTotalLines();
            
            // 创建进度输入对话框
            using (Form progressForm = new Form())
            {
                progressForm.Text = "进度跳转";
                progressForm.Size = new Size(380, 240);
                progressForm.StartPosition = FormStartPosition.CenterParent;
                progressForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                progressForm.MaximizeBox = false;
                progressForm.MinimizeBox = false;
                
                // 当前进度显示
                Label currentProgressLabel = new Label();
                currentProgressLabel.Text = $"当前进度：{currentProgress:F2}% (第 {currentLine + 1}/{totalLines} 行)";
                currentProgressLabel.Location = new Point(20, 20);
                currentProgressLabel.Size = new Size(320, 20);
                currentProgressLabel.Font = new Font("微软雅黑", 9.5F, FontStyle.Bold);
                currentProgressLabel.ForeColor = Color.DarkGreen;
                
                // 进度条显示当前进度
                ProgressBar currentProgressBar = new ProgressBar();
                currentProgressBar.Location = new Point(20, 45);
                currentProgressBar.Size = new Size(320, 20);
                currentProgressBar.Value = Math.Min(100, (int)currentProgress);
                currentProgressBar.Style = ProgressBarStyle.Continuous;
                
                // 输入提示
                Label label = new Label();
                label.Text = "请输入跳转进度（0-100%）：";
                label.Location = new Point(20, 80);
                label.Size = new Size(200, 20);
                
                // 输入框
                TextBox textBox = new TextBox();
                textBox.Location = new Point(20, 105);
                textBox.Size = new Size(100, 25);
                textBox.Text = currentProgress.ToString("F2");
                textBox.SelectAll(); // 选中所有文本，方便直接输入
                
                // 滑动条
                TrackBar trackBar = new TrackBar();
                trackBar.Location = new Point(20, 135);
                trackBar.Size = new Size(320, 45);
                trackBar.Minimum = 0;
                trackBar.Maximum = 100;
                trackBar.TickFrequency = 10;
                trackBar.Value = Math.Min(100, (int)currentProgress);
                
                // 滑动条和文本框联动
                trackBar.ValueChanged += (s, args) =>
                {
                    textBox.Text = trackBar.Value.ToString();
                };
                
                textBox.TextChanged += (s, args) =>
                {
                    if (float.TryParse(textBox.Text, out float value))
                    {
                        if (value >= 0 && value <= 100)
                        {
                            trackBar.Value = (int)value;
                        }
                    }
                };
                
                // 确定按钮
                Button okButton = new Button();
                okButton.Text = "确定";
                okButton.Location = new Point(200, 185);
                okButton.Size = new Size(60, 28);
                okButton.Click += (s, args) =>
                {
                    if (float.TryParse(textBox.Text, out float percentage))
                    {
                        if (percentage >= 0 && percentage <= 100)
                        {
                            textDisplay.JumpToProgress(percentage);
                            SaveReadingProgress(); // 跳转后保存进度
                            progressForm.Close();
                        }
                        else
                        {
                            MessageBox.Show("请输入0-100之间的数值", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    else
                    {
                        MessageBox.Show("请输入有效的数值", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };
                
                // 取消按钮
                Button cancelButton = new Button();
                cancelButton.Text = "取消";
                cancelButton.Location = new Point(270, 185);
                cancelButton.Size = new Size(60, 28);
                cancelButton.Click += (s, args) => progressForm.Close();
                
                progressForm.Controls.AddRange(new Control[] { 
                    currentProgressLabel, currentProgressBar, 
                    label, textBox, trackBar, 
                    okButton, cancelButton 
                });
                
                // 设置焦点到文本框
                textBox.Focus();
                
                progressForm.ShowDialog(this);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 保存当前阅读进度
            SaveReadingProgress();
            
            // 保存设置
            SaveSettings();
            
            // 清理定时器
            hoverTimer?.Stop();
            hoverTimer?.Dispose();
            progressSaveTimer?.Stop();
            progressSaveTimer?.Dispose();
            
            base.OnFormClosing(e);
        }
        
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            // 移除边框绘制，保持完全无边框的外观
        }

        // 支持拖拽文件打开
        protected override void OnDragEnter(DragEventArgs drgevent)
        {
            if (drgevent.Data.GetDataPresent(DataFormats.FileDrop))
            {
                drgevent.Effect = DragDropEffects.Copy;
            }
            base.OnDragEnter(drgevent);
        }

        protected override async void OnDragDrop(DragEventArgs drgevent)
        {
            if (drgevent.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])drgevent.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    try
                    {
                        // 检查文件大小
                        FileInfo fileInfo = new FileInfo(files[0]);
                        if (fileInfo.Length > 5 * 1024 * 1024) // 大于5MB
                        {
                            string message;
                            if (fileInfo.Length > 50 * 1024 * 1024)
                            {
                                message = $"文件很大 ({fileInfo.Length / 1024 / 1024:F1} MB)，将只加载前10MB内容以确保性能。是否继续？";
                            }
                            else
                            {
                                message = $"文件较大 ({fileInfo.Length / 1024 / 1024:F1} MB)，加载可能需要一些时间。是否继续？";
                            }
                            
                            var result = MessageBox.Show(message, "大文件提示", 
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            
                            if (result == DialogResult.No)
                                return;
                        }
                        
                        hoverLabel.Text = "正在加载文件，请稍候...";
                        Application.DoEvents();
                        
                        await LoadFileAsync(files[0]);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"打开文件时出错：{ex.Message}", "错误", 
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                        hoverLabel.Text = "欢迎使用极简单行阅读器，请点击右键菜单打开文本或者网页";
                        hoverLabel.Visible = false; // 错误后也保持空白
                    }
                }
            }
            base.OnDragDrop(drgevent);
        }
        
        private async void OpenWebPage_Click(object sender, EventArgs e)
        {
            // 创建URL输入对话框
            using (Form urlForm = new Form())
            {
                urlForm.Text = "打开网页";
                urlForm.Size = new Size(500, 180);
                urlForm.StartPosition = FormStartPosition.CenterParent;
                urlForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                urlForm.MaximizeBox = false;
                urlForm.MinimizeBox = false;
                
                Label label = new Label();
                label.Text = "请输入网页地址（URL）：";
                label.Location = new Point(20, 20);
                label.Size = new Size(150, 20);
                
                TextBox urlTextBox = new TextBox();
                urlTextBox.Location = new Point(20, 45);
                urlTextBox.Size = new Size(440, 25);
                urlTextBox.Text = "https://";
                urlTextBox.SelectionStart = urlTextBox.Text.Length;
                
                Label hintLabel = new Label();
                hintLabel.Text = "提示：将提取网页中的纯文本内容";
                hintLabel.Location = new Point(20, 75);
                hintLabel.Size = new Size(440, 20);
                hintLabel.ForeColor = Color.Gray;
                hintLabel.Font = new Font("微软雅黑", 8.5F);
                
                Button okButton = new Button();
                okButton.Text = "打开";
                okButton.Location = new Point(300, 105);
                okButton.Size = new Size(70, 30);
                okButton.Click += async (s, args) =>
                {
                    string url = urlTextBox.Text.Trim();
                    if (string.IsNullOrEmpty(url) || url == "https://")
                    {
                        MessageBox.Show("请输入有效的网页地址", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    
                    // 确保URL格式正确
                    if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                        !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        url = "https://" + url;
                    }
                    
                    urlForm.Close();
                    await LoadWebPageAsync(url);
                };
                
                Button cancelButton = new Button();
                cancelButton.Text = "取消";
                cancelButton.Location = new Point(380, 105);
                cancelButton.Size = new Size(70, 30);
                cancelButton.Click += (s, args) => urlForm.Close();
                
                // 支持Enter键确认
                urlForm.AcceptButton = okButton;
                urlForm.CancelButton = cancelButton;
                
                urlForm.Controls.AddRange(new Control[] { 
                    label, urlTextBox, hintLabel, okButton, cancelButton 
                });
                
                urlTextBox.Focus();
                urlForm.ShowDialog(this);
            }
        }
        
        private async Task LoadWebPageAsync(string url)
        {
            try
            {
                // 更新提示信息
                this.Invoke(new Action(() =>
                {
                    hoverLabel.Text = "正在加载网页，请稍候...";
                    hoverLabel.ForeColor = Color.Yellow;
                    hoverLabel.Visible = true;
                    Application.DoEvents();
                }));
                
                // 保存当前URL
                currentWebPageUrl = url;
                
                // 下载网页内容
                string htmlContent = await DownloadWebPageAsync(url);
                
                // 提取上一章/下一章链接
                ExtractNavigationLinks(htmlContent, url);
                
                // 提取纯文本
                string textContent = ExtractTextFromHtml(htmlContent);
                
                if (string.IsNullOrWhiteSpace(textContent))
                {
                    this.Invoke(new Action(() =>
                    {
                        MessageBox.Show("无法从网页中提取有效的文本内容", "提示", 
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                        hoverLabel.Text = "欢迎使用极简单行阅读器，请点击右键菜单打开文本或者网页";
                        hoverLabel.ForeColor = Color.LightGray;
                        hoverLabel.Visible = false;
                    }));
                    return;
                }
                
                // 在UI线程上更新界面
                this.Invoke(new Action(() =>
                {
                    // 暂时禁用重绘以提高性能
                    textDisplay.SuspendLayout();
                    
                    // 设置文本
                    textDisplay.SetText(textContent);
                    
                    // 保持背景颜色一致
                    textDisplay.BackColor = this.BackColor;
                    
                    currentFilePath = url; // 使用URL作为"文件路径"
                    
                    // 标记有内容，更新提示文字
                    isContentVisible = true;
                    string navInfo = "";
                    if (!string.IsNullOrEmpty(prevChapterUrl) || !string.IsNullOrEmpty(nextChapterUrl))
                    {
                        navInfo = "\n右键菜单可跳转上/下一章";
                    }
                    hoverLabel.Text = $"已加载：{url}{navInfo}\n鼠标悬停查看内容";
                    hoverLabel.ForeColor = Color.LightGray;
                    
                    // 更新菜单项状态
                    prevChapterMenuItem.Enabled = !string.IsNullOrEmpty(prevChapterUrl);
                    nextChapterMenuItem.Enabled = !string.IsNullOrEmpty(nextChapterUrl);
                    
                    // 初始状态：隐藏所有内容，等待鼠标悬停
                    textDisplay.Visible = false;
                    hoverLabel.Visible = false;
                    
                    // 从头开始阅读
                    textDisplay.SetScrollPosition(0);
                    
                    // 恢复重绘
                    textDisplay.ResumeLayout();
                }));
            }
            catch (HttpRequestException ex)
            {
                this.Invoke(new Action(() =>
                {
                    string errorMsg = ex.Message;
                    if (ex.Message.Contains("403"))
                    {
                        errorMsg = "网站拒绝访问（403 Forbidden）\n\n可能原因：\n" +
                                  "1. 该网站限制程序访问，只允许浏览器访问\n" +
                                  "2. 网站需要登录才能查看内容\n" +
                                  "3. 网站有反爬虫机制\n\n" +
                                  "建议：请尝试其他网站或使用浏览器保存网页后再打开";
                    }
                    else if (ex.Message.Contains("404"))
                    {
                        errorMsg = "网页不存在（404 Not Found）\n请检查URL是否正确";
                    }
                    else if (ex.Message.Contains("500") || ex.Message.Contains("502") || ex.Message.Contains("503"))
                    {
                        errorMsg = "网站服务器错误\n请稍后再试";
                    }
                    
                    MessageBox.Show($"加载网页时出错：\n\n{errorMsg}", "错误", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                    hoverLabel.Text = "欢迎使用极简单行阅读器，请点击右键菜单打开文本或者网页";
                    hoverLabel.ForeColor = Color.LightGray;
                    hoverLabel.Visible = false;
                }));
            }
            catch (TaskCanceledException)
            {
                this.Invoke(new Action(() =>
                {
                    MessageBox.Show("网页加载超时（30秒）\n请检查网络连接或尝试其他网站", "超时", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    hoverLabel.Text = "欢迎使用极简单行阅读器，请点击右键菜单打开文本或者网页";
                    hoverLabel.ForeColor = Color.LightGray;
                    hoverLabel.Visible = false;
                }));
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    MessageBox.Show($"加载网页时出错：\n\n{ex.Message}", "错误", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                    hoverLabel.Text = "欢迎使用极简单行阅读器，请点击右键菜单打开文本或者网页";
                    hoverLabel.ForeColor = Color.LightGray;
                    hoverLabel.Visible = false;
                }));
            }
        }
        
        private async Task<string> DownloadWebPageAsync(string url)
        {
            // 创建 HttpClientHandler 并启用自动解压缩（解决乱码问题的关键）
            var handler = new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | 
                                         System.Net.DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                UseCookies = true,
                CookieContainer = new System.Net.CookieContainer()
            };
            
            // 忽略SSL证书错误（某些网站证书可能有问题）
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            
            using (HttpClient client = new HttpClient(handler))
            {
                // 设置超时时间
                client.Timeout = TimeSpan.FromSeconds(30);
                
                // 尝试多种User-Agent，有些网站对特定UA更友好
                string[] userAgents = new string[]
                {
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0"
                };
                
                HttpResponseMessage response = null;
                Exception lastException = null;
                
                foreach (string userAgent in userAgents)
                {
                    try
                    {
                        // 清除之前的请求头
                        client.DefaultRequestHeaders.Clear();
                        
                        // 设置完整的浏览器请求头，模拟真实浏览器访问
                        client.DefaultRequestHeaders.Add("User-Agent", userAgent);
                        client.DefaultRequestHeaders.Add("Accept", 
                            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                        client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
                        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
                        client.DefaultRequestHeaders.Add("Connection", "keep-alive");
                        client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
                        client.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
                        client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
                        client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
                        client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
                        client.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
                        client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"");
                        client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
                        client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
                        
                        // 如果URL中有域名，添加Referer
                        try
                        {
                            Uri uri = new Uri(url);
                            string referer = $"{uri.Scheme}://{uri.Host}/";
                            client.DefaultRequestHeaders.Add("Referer", referer);
                        }
                        catch { }
                        
                        response = await client.GetAsync(url);
                        
                        // 如果成功或者不是403，跳出循环
                        if (response.IsSuccessStatusCode || 
                            response.StatusCode != System.Net.HttpStatusCode.Forbidden)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        continue;
                    }
                }
                
                if (response == null)
                {
                    throw lastException ?? new Exception("无法连接到网站");
                }
                
                // 如果仍然是403，尝试使用最简单的请求
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("User-Agent", 
                        "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; Trident/6.0)");
                    response = await client.GetAsync(url);
                }
                
                response.EnsureSuccessStatusCode();
                
                // 读取内容
                byte[] contentBytes = await response.Content.ReadAsByteArrayAsync();
                
                // 尝试检测编码
                Encoding encoding = DetectHtmlEncoding(contentBytes, response);
                
                return encoding.GetString(contentBytes);
            }
        }
        
        private Encoding DetectHtmlEncoding(byte[] content, HttpResponseMessage response)
        {
            if (content == null || content.Length == 0)
                return Encoding.UTF8;
            
            // 1. 检测BOM（字节顺序标记）
            if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
                return Encoding.UTF8;
            if (content.Length >= 2 && content[0] == 0xFF && content[1] == 0xFE)
                return Encoding.Unicode; // UTF-16 LE
            if (content.Length >= 2 && content[0] == 0xFE && content[1] == 0xFF)
                return Encoding.BigEndianUnicode; // UTF-16 BE
            
            // 2. 尝试从HTTP响应头获取编码
            if (response.Content.Headers.ContentType?.CharSet != null)
            {
                try
                {
                    string charset = response.Content.Headers.ContentType.CharSet.ToLower();
                    // 标准化常见的charset名称
                    if (charset == "gb2312" || charset == "gbk" || charset == "gb18030")
                        return Encoding.GetEncoding("GB18030"); // GB18030兼容GBK和GB2312
                    return Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet);
                }
                catch { }
            }
            
            // 3. 尝试用多种编码读取HTML片段并查找charset
            int snippetLength = Math.Min(4096, content.Length);
            Encoding[] testEncodings = new Encoding[] 
            { 
                Encoding.UTF8,
                Encoding.GetEncoding("GB18030"), // 中文网站常用
                Encoding.Default
            };
            
            foreach (var encoding in testEncodings)
            {
                try
                {
                    string htmlSnippet = encoding.GetString(content, 0, snippetLength);
                    
                    // 查找charset（支持多种写法）
                    var patterns = new string[]
                    {
                        @"<meta[^>]+charset\s*=\s*[""']?([a-zA-Z0-9_-]+)",
                        @"charset\s*=\s*[""']?([a-zA-Z0-9_-]+)",
                        @"<meta[^>]+content\s*=\s*[""'][^""']*charset=([a-zA-Z0-9_-]+)"
                    };
                    
                    foreach (var pattern in patterns)
                    {
                        var charsetMatch = Regex.Match(htmlSnippet, pattern, RegexOptions.IgnoreCase);
                        if (charsetMatch.Success)
                        {
                            string detectedCharset = charsetMatch.Groups[1].Value.ToLower();
                            
                            // 标准化charset名称
                            if (detectedCharset == "gb2312" || detectedCharset == "gbk" || detectedCharset == "gb18030")
                                return Encoding.GetEncoding("GB18030");
                            if (detectedCharset == "utf-8" || detectedCharset == "utf8")
                                return Encoding.UTF8;
                            
                            try
                            {
                                return Encoding.GetEncoding(detectedCharset);
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            
            // 4. 使用启发式检测
            // 检测是否包含中文GBK特征
            bool likelyGBK = false;
            for (int i = 0; i < Math.Min(1000, content.Length - 1); i++)
            {
                // GBK编码的中文字符范围
                if (content[i] >= 0x81 && content[i] <= 0xFE && 
                    content[i + 1] >= 0x40 && content[i + 1] <= 0xFE)
                {
                    likelyGBK = true;
                    break;
                }
            }
            
            if (likelyGBK)
            {
                try
                {
                    return Encoding.GetEncoding("GB18030");
                }
                catch { }
            }
            
            // 5. 默认使用UTF-8
            return Encoding.UTF8;
        }
        
        private string ExtractTextFromHtml(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;
            
            // 移除script和style标签及其内容
            html = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
            
            // 移除注释
            html = Regex.Replace(html, @"<!--[\s\S]*?-->", "");
            
            // 将某些标签转换为换行
            html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</p>", "\n\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</div>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</h[1-6]>", "\n\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</li>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</tr>", "\n", RegexOptions.IgnoreCase);
            
            // 移除所有HTML标签
            html = Regex.Replace(html, @"<[^>]+>", "");
            
            // 解码HTML实体
            html = System.Net.WebUtility.HtmlDecode(html);
            
            // 移除多余的空白行
            html = Regex.Replace(html, @"^\s+", "", RegexOptions.Multiline);
            html = Regex.Replace(html, @"\s+$", "", RegexOptions.Multiline);
            html = Regex.Replace(html, @"\n{3,}", "\n\n");
            
            // 移除开头和结尾的空白
            html = html.Trim();
            
            return html;
        }
        
        private void ExtractNavigationLinks(string html, string baseUrl)
        {
            // 重置链接
            prevChapterUrl = "";
            nextChapterUrl = "";
            
            if (string.IsNullOrEmpty(html))
                return;
            
            try
            {
                Uri baseUri = new Uri(baseUrl);
                
                // 查找所有链接
                var linkPattern = @"<a[^>]+href\s*=\s*[""']([^""']+)[""'][^>]*>(.*?)</a>";
                var matches = Regex.Matches(html, linkPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                
                // 定义匹配"下一章"的关键词
                string[] nextKeywords = { "下一章", "下一页", "下章", "下一节", "next", "下页", "→", "»" };
                string[] prevKeywords = { "上一章", "上一页", "上章", "上一节", "prev", "previous", "上页", "←", "«" };
                
                foreach (Match match in matches)
                {
                    if (match.Groups.Count >= 3)
                    {
                        string href = match.Groups[1].Value.Trim();
                        string linkText = Regex.Replace(match.Groups[2].Value, @"<[^>]+>", "").Trim();
                        
                        // 检查是否是下一章链接
                        if (string.IsNullOrEmpty(nextChapterUrl))
                        {
                            foreach (string keyword in nextKeywords)
                            {
                                if (linkText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                                {
                                    nextChapterUrl = ResolveUrl(href, baseUri);
                                    break;
                                }
                            }
                        }
                        
                        // 检查是否是上一章链接
                        if (string.IsNullOrEmpty(prevChapterUrl))
                        {
                            foreach (string keyword in prevKeywords)
                            {
                                if (linkText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                                {
                                    prevChapterUrl = ResolveUrl(href, baseUri);
                                    break;
                                }
                            }
                        }
                    }
                    
                    // 如果都找到了就退出
                    if (!string.IsNullOrEmpty(prevChapterUrl) && !string.IsNullOrEmpty(nextChapterUrl))
                        break;
                }
            }
            catch
            {
                // 解析失败时静默忽略
            }
        }
        
        private string ResolveUrl(string href, Uri baseUri)
        {
            if (string.IsNullOrEmpty(href) || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                return "";
            
            try
            {
                // 处理相对URL
                if (href.StartsWith("//"))
                {
                    return baseUri.Scheme + ":" + href;
                }
                else if (href.StartsWith("/"))
                {
                    return $"{baseUri.Scheme}://{baseUri.Host}{href}";
                }
                else if (!href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                         !href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    // 相对路径
                    string basePath = baseUri.AbsolutePath;
                    int lastSlash = basePath.LastIndexOf('/');
                    if (lastSlash >= 0)
                    {
                        basePath = basePath.Substring(0, lastSlash + 1);
                    }
                    return $"{baseUri.Scheme}://{baseUri.Host}{basePath}{href}";
                }
                else
                {
                    return href;
                }
            }
            catch
            {
                return "";
            }
        }
        
        private async void PrevChapter_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(prevChapterUrl))
            {
                MessageBox.Show("没有找到上一章链接", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            await LoadWebPageAsync(prevChapterUrl);
        }
        
        private async void NextChapter_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(nextChapterUrl))
            {
                MessageBox.Show("没有找到下一章链接", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            await LoadWebPageAsync(nextChapterUrl);
        }
    }
}

