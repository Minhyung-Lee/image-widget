using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

internal sealed class ImageCanvas : PictureBox
{
    // Keep small images at their native pixel size until the user resizes them.
    internal bool PreferNativeSize = true;

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (Image == null || ClientSize.Width < 1 || ClientSize.Height < 1) return;
        double scale = Math.Min(ClientSize.Width / (double)Image.Width,
            ClientSize.Height / (double)Image.Height);
        if (PreferNativeSize) scale = Math.Min(1.0, scale);
        int width = Math.Max(1, (int)Math.Round(Image.Width * scale));
        int height = Math.Max(1, (int)Math.Round(Image.Height * scale));
        int left = (ClientSize.Width - width) / 2;
        int top = (ClientSize.Height - height) / 2;
        if (width == Image.Width && height == Image.Height)
            e.Graphics.DrawImageUnscaled(Image, left, top);
        else
        {
            bool integerEnlargement = scale >= 1.0 && Math.Abs(scale - Math.Round(scale)) < 0.000001;
            e.Graphics.InterpolationMode = integerEnlargement
                ? System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor
                : System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            e.Graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            e.Graphics.DrawImage(Image, new Rectangle(left, top, width, height),
                new Rectangle(0, 0, Image.Width, Image.Height), GraphicsUnit.Pixel);
        }
    }
}

internal sealed class WidgetForm : Form
{
    private Image original;
    internal readonly ImageCanvas Picture;
    internal readonly Button CloseButton;
    internal readonly Button ImageButton;
    internal readonly Panel ResizeGrip;
    private readonly Panel bar;
    private readonly Panel footer;
    private readonly Label title;
    private readonly Label resizeLabel;
    private readonly int barHeight;
    private readonly int footerHeight;
    private readonly int minimumImageWidth;
    private readonly double dpiScale;
    private readonly int buttonInset;
    private bool resizing;
    private Point resizeOrigin;
    private int resizeWidth;
    private Control dragControl;
    private Point dragOrigin;
    private Point windowOrigin;

    internal WidgetForm(string imagePath)
    {
        // Clone the decoded image so the original file is never locked or changed.
        original = LoadDetachedImage(imagePath);

        Text = "Image widget";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(70, 91, 111);
        Padding = new Padding(1);

        using (Graphics screen = Graphics.FromHwnd(IntPtr.Zero))
            dpiScale = Math.Max(1.0, screen.DpiX / 96.0);
        Rectangle work = Screen.PrimaryScreen.WorkingArea;
        barHeight = (int)Math.Round(28 * dpiScale);
        footerHeight = (int)Math.Round(28 * dpiScale);
        buttonInset = Math.Max(2, (int)Math.Round(2 * dpiScale));
        minimumImageWidth = (int)Math.Round(180 * dpiScale);
        int imageWidth = Math.Max(minimumImageWidth, original.Width);
        int imageHeight = (int)Math.Round((double)imageWidth * original.Height / original.Width);
        double fit = Math.Min(1.0, Math.Min((work.Width - 40.0) / (imageWidth + 2),
            (work.Height - 40.0 - barHeight - footerHeight) / (imageHeight + 2)));
        imageWidth = Math.Max(1, (int)Math.Round(imageWidth * fit));
        imageHeight = Math.Max(1, (int)Math.Round(imageHeight * fit));
        bar = new Panel();
        bar.BackColor = BackColor;
        Controls.Add(bar);

        title = new Label();
        title.Text = "Drag to move";
        title.ForeColor = Color.White;
        title.Font = new Font("Segoe UI", 9f);
        title.TextAlign = ContentAlignment.MiddleLeft;
        bar.Controls.Add(title);

        CloseButton = new Button();
        CloseButton.Text = "\u00d7";
        CloseButton.AccessibleName = "Close";
        CloseButton.TabStop = false;
        CloseButton.Font = new Font("Segoe UI", 12f);
        CloseButton.ForeColor = Color.White;
        CloseButton.BackColor = BackColor;
        CloseButton.FlatStyle = FlatStyle.Flat;
        CloseButton.FlatAppearance.BorderSize = 0;
        CloseButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(180, 55, 65);
        CloseButton.Click += delegate { Close(); };
        bar.Controls.Add(CloseButton);

        Picture = new ImageCanvas();
        Picture.Image = original;
        Picture.SizeMode = PictureBoxSizeMode.Zoom;
        Picture.BackColor = Color.FromArgb(184, 204, 221);
        Controls.Add(Picture);

        footer = new Panel();
        footer.BackColor = BackColor;
        Controls.Add(footer);
        ImageButton = new Button();
        ImageButton.Text = "이미지 설정";
        ImageButton.AccessibleName = "이미지 설정";
        ImageButton.TabStop = false;
        ImageButton.ForeColor = Color.White;
        ImageButton.BackColor = BackColor;
        ImageButton.Font = new Font("Malgun Gothic", 9f);
        ImageButton.TextAlign = ContentAlignment.MiddleCenter;
        ImageButton.Padding = new Padding((int)Math.Round(4 * dpiScale),
            (int)Math.Round(2 * dpiScale), (int)Math.Round(4 * dpiScale), (int)Math.Round(2 * dpiScale));
        ImageButton.FlatStyle = FlatStyle.Flat;
        ImageButton.FlatAppearance.BorderSize = 1;
        ImageButton.FlatAppearance.BorderColor = Color.FromArgb(210, 225, 240);
        Size measuredButtonText = TextRenderer.MeasureText(ImageButton.Text, ImageButton.Font);
        footerHeight = Math.Max(footerHeight, measuredButtonText.Height + ImageButton.Padding.Vertical
            + 2 * ImageButton.FlatAppearance.BorderSize + 2 * buttonInset);
        ImageButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(95, 116, 136);
        ImageButton.Click += delegate
        {
            using (OpenFileDialog picker = new OpenFileDialog())
            {
                picker.Title = "이미지 설정";
                picker.Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*";
                picker.CheckFileExists = true;
                picker.Multiselect = false;
                picker.RestoreDirectory = true;
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                string error;
                if (!TrySetImage(picker.FileName, out error))
                    MessageBox.Show(this, "Could not open this image.\n\n" + error,
                        "Image widget", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        footer.Controls.Add(ImageButton);
        resizeLabel = new Label();
        resizeLabel.Text = "Resize";
        resizeLabel.ForeColor = Color.White;
        resizeLabel.Font = new Font("Segoe UI", 8f);
        resizeLabel.TextAlign = ContentAlignment.MiddleRight;
        footer.Controls.Add(resizeLabel);
        ResizeGrip = new Panel();
        ResizeGrip.AccessibleName = "Resize";
        ResizeGrip.Cursor = Cursors.SizeNWSE;
        ResizeGrip.Paint += delegate(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(Color.White, (float)dpiScale))
                for (int line = 1; line <= 3; line++)
                {
                    int inset = (int)Math.Round(line * 4 * dpiScale);
                    e.Graphics.DrawLine(pen, ResizeGrip.Width - inset, ResizeGrip.Height - 3,
                        ResizeGrip.Width - 3, ResizeGrip.Height - inset);
                }
        };
        footer.Controls.Add(ResizeGrip);
        ResizeGrip.MouseDown += delegate(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            resizeOrigin = ResizeGrip.PointToScreen(e.Location);
            resizeWidth = Picture.Width;
            Picture.PreferNativeSize = false;
            resizing = true;
            ResizeGrip.Capture = true;
        };
        ResizeGrip.MouseMove += delegate(object sender, MouseEventArgs e)
        {
            if (!resizing) return;
            Point current = ResizeGrip.PointToScreen(e.Location);
            int dx = current.X - resizeOrigin.X;
            double scaledDy = (current.Y - resizeOrigin.Y) * (double)original.Width / original.Height;
            int width = resizeWidth + (int)Math.Round(Math.Abs(dx) >= Math.Abs(scaledDy) ? dx : scaledDy);
            LayoutImage(ClampImageWidth(width));
        };
        ResizeGrip.MouseUp += delegate
        {
            resizing = false;
            ResizeGrip.Capture = false;
            KeepOnScreen();
        };
        ResizeGrip.MouseCaptureChanged += delegate
        {
            if (resizing && !ResizeGrip.Capture)
            {
                resizing = false;
                KeepOnScreen();
            }
        };
        LayoutImage(imageWidth);
        Location = new Point(work.Right - Width - 20, work.Bottom - Height - 20);
        WireDrag(bar);
        WireDrag(title);
        WireDrag(Picture);
    }

    private static Image LoadDetachedImage(string path)
    {
        using (Image loaded = Image.FromFile(path))
        {
            // Copy decoded pixels 1:1, without retaining the input file or resampling.
            Bitmap pixels = new Bitmap(loaded.Width, loaded.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                using (Graphics copy = Graphics.FromImage(pixels))
                {
                    copy.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    copy.DrawImageUnscaled(loaded, 0, 0);
                }
                return pixels;
            }
            catch { pixels.Dispose(); throw; }
        }
    }

    // Decode into memory; selected files are never copied, saved, or uploaded.
    internal bool TrySetImage(string path, out string error)
    {
        Image replacement;
        try
        {
            replacement = LoadDetachedImage(path);
        }
        catch (Exception failure)
        {
            if (!(failure is ArgumentException) && !(failure is IOException) &&
                !(failure is UnauthorizedAccessException) && !(failure is OutOfMemoryException) &&
                !(failure is System.Runtime.InteropServices.ExternalException) && !(failure is NotSupportedException))
                throw;
            error = failure.Message;
            return false;
        }
        Image previous = original;
        original = replacement;
        Picture.Image = replacement;
        Picture.PreferNativeSize = true;
        LayoutImage(ClampImageWidth(Picture.Width));
        previous.Dispose();
        KeepOnScreen();
        error = null;
        return true;
    }

    private int ClampImageWidth(int width)
    {
        Rectangle area = Screen.FromRectangle(Bounds).WorkingArea;
        int maximumWidth = Math.Min(area.Width - 2,
            (int)Math.Floor((area.Height - barHeight - footerHeight - 2) * (double)original.Width / original.Height));
        return Math.Max(Math.Min(minimumImageWidth, maximumWidth), Math.Min(width, maximumWidth));
    }

    private void LayoutImage(int width)
    {
        int height = (int)Math.Round(width * (double)original.Height / original.Width);
        SuspendLayout();
        ClientSize = new Size(width + 2, height + barHeight + footerHeight + 2);
        bar.SetBounds(1, 1, width, barHeight);
        title.SetBounds((int)(8 * dpiScale), 0, Math.Max(1, width - barHeight - 10), barHeight);
        CloseButton.SetBounds(width - barHeight, 0, barHeight, barHeight);
        Picture.SetBounds(1, barHeight + 1, width, height);
        footer.SetBounds(1, height + barHeight + 1, width, footerHeight);
        ImageButton.SetBounds(buttonInset, buttonInset, (int)Math.Round(90 * dpiScale), footerHeight - 2 * buttonInset);
        resizeLabel.SetBounds(ImageButton.Right + 4, 0, Math.Max(1, width - ImageButton.Right - footerHeight - 8), footerHeight);
        ResizeGrip.SetBounds(width - footerHeight, 0, footerHeight, footerHeight);
        ResizeGrip.Invalidate();
        ResumeLayout(false);
    }

    // Opening, clicking, and dragging the widget leave the editor's keyboard focus alone.
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= 0x08000000 | 0x00000080; // NOACTIVATE | TOOLWINDOW
            return parameters;
        }
    }

    private void WireDrag(Control control)
    {
        control.Cursor = Cursors.SizeAll;
        control.MouseDown += delegate(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            dragControl = (Control)sender;
            dragOrigin = dragControl.PointToScreen(e.Location);
            windowOrigin = Location;
            dragControl.Capture = true;
        };
        control.MouseMove += delegate(object sender, MouseEventArgs e)
        {
            if (dragControl != sender) return;
            Point current = dragControl.PointToScreen(e.Location);
            Location = new Point(windowOrigin.X + current.X - dragOrigin.X,
                windowOrigin.Y + current.Y - dragOrigin.Y);
        };
        control.MouseUp += delegate
        {
            if (dragControl == null) return;
            Control released = dragControl;
            dragControl = null;
            released.Capture = false;
            KeepOnScreen();
        };
        control.MouseCaptureChanged += delegate
        {
            if (dragControl == control && !control.Capture)
            {
                dragControl = null;
                KeepOnScreen();
            }
        };
    }

    private void KeepOnScreen()
    {
        Rectangle work = Screen.FromRectangle(Bounds).WorkingArea;
        Location = new Point(Math.Max(work.Left, Math.Min(Left, work.Right - Width)),
            Math.Max(work.Top, Math.Min(Top, work.Bottom - Height)));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (Picture != null) Picture.Image = null;
            original.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal static class WidgetProgram
{
    [STAThread]
    private static void Main()
    {
        bool created;
        using (Mutex instance = new Mutex(true, "Local\\SimpleImageWidget_GenericDemo", out created))
        {
            if (!created) return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "image.png");
            try
            {
                using (WidgetForm widget = new WidgetForm(imagePath))
                    Application.Run(widget);
            }
            catch (Exception error)
            {
                MessageBox.Show("Could not open image.png. Keep it beside ImageWidget.exe.\n\n" + error.Message,
                    "Image widget", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { instance.ReleaseMutex(); }
        }
    }
}
