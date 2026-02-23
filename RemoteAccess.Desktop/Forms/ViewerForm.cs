using System.Drawing;
using RemoteAccess.Desktop.Services;
using RemoteAccess.Shared;

namespace RemoteAccess.Desktop.Forms;

public class ViewerForm : Form
{
    private ConnectionService _connection = null!;
    private PictureBox _screen = null!;
    private Label _lblStatus = null!;
    private Panel _toolbar = null!;
    private Button _btnQuality = null!;
    private int _remoteWidth = 1920;
    private int _remoteHeight = 1080;
    private int _qualityLevel = 1; // 0=low, 1=med, 2=high
    private bool _connected;
    private Image? _currentFrame;

    public ViewerForm(string relayUrl, string targetId, string password)
    {
        InitializeUI();

        _connection = new ConnectionService(relayUrl);

        _connection.OnTextMessage += msg =>
        {
            switch (msg)
            {
                case ScreenInfoMessage info:
                    _remoteWidth = info.Width;
                    _remoteHeight = info.Height;
                    Invoke(() => Text = $"RemoteAccess — {targetId} ({info.Width}x{info.Height})");
                    break;

                case ErrorMessage err:
                    Invoke(() =>
                    {
                        _lblStatus.Text = $"Erro: {err.Message}";
                        _lblStatus.Visible = true;
                    });
                    break;

                case HostDisconnectedMessage:
                    Invoke(() =>
                    {
                        _connected = false;
                        _lblStatus.Text = "Host desconectou";
                        _lblStatus.Visible = true;
                    });
                    break;
            }
        };

        _connection.OnBinaryMessage += frameData =>
        {
            try
            {
                using var ms = new MemoryStream(frameData);
                var newFrame = Image.FromStream(ms);
                var oldFrame = _currentFrame;
                _currentFrame = newFrame;

                _screen.Invoke(() =>
                {
                    _screen.Image = _currentFrame;
                    _connected = true;
                    if (_lblStatus.Visible)
                    {
                        _lblStatus.Visible = false;
                    }
                });

                oldFrame?.Dispose();
            }
            catch { }
        };

        _connection.OnDisconnected += msg =>
        {
            _connected = false;
            try
            {
                Invoke(() =>
                {
                    _lblStatus.Text = "Desconectado";
                    _lblStatus.Visible = true;
                });
            }
            catch { }
        };

        _ = _connection.ConnectAsViewerAsync(targetId, password);
    }

    private void InitializeUI()
    {
        Text = "RemoteAccess — Conectando...";
        Size = new Size(1280, 760);
        BackColor = Color.Black;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        DoubleBuffered = true;

        // Toolbar
        _toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(30, 30, 40)
        };

        _btnQuality = new Button
        {
            Text = "Qualidade: Média",
            Location = new Point(8, 4),
            Size = new Size(140, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(50, 53, 70),
            ForeColor = Color.FromArgb(180, 185, 200),
            Font = new Font("Segoe UI", 8.5f),
            Cursor = Cursors.Hand
        };
        _btnQuality.FlatAppearance.BorderSize = 0;
        _btnQuality.Click += (_, _) => CycleQuality();

        var btnFullscreen = new Button
        {
            Text = "⛶ Tela Cheia",
            Location = new Point(156, 4),
            Size = new Size(110, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(50, 53, 70),
            ForeColor = Color.FromArgb(180, 185, 200),
            Font = new Font("Segoe UI", 8.5f),
            Cursor = Cursors.Hand
        };
        btnFullscreen.FlatAppearance.BorderSize = 0;
        btnFullscreen.Click += (_, _) => ToggleFullscreen();

        var btnDisconnect = new Button
        {
            Text = "✕ Desconectar",
            Location = new Point(274, 4),
            Size = new Size(120, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(180, 60, 60),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5f),
            Cursor = Cursors.Hand
        };
        btnDisconnect.FlatAppearance.BorderSize = 0;
        btnDisconnect.Click += (_, _) => Close();

        _toolbar.Controls.AddRange([_btnQuality, btnFullscreen, btnDisconnect]);

        // Screen display
        _screen = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black,
            Cursor = Cursors.Cross
        };

        _screen.MouseMove += Screen_MouseMove;
        _screen.MouseDown += Screen_MouseDown;
        _screen.MouseUp += Screen_MouseUp;
        _screen.MouseWheel += Screen_MouseWheel;

        // Status overlay
        _lblStatus = new Label
        {
            Text = "Conectando...",
            ForeColor = Color.White,
            BackColor = Color.FromArgb(180, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 14f),
            Visible = true
        };

        _screen.Controls.Add(_lblStatus);
        Controls.Add(_screen);
        Controls.Add(_toolbar);

        KeyDown += ViewerForm_KeyDown;
        KeyUp += ViewerForm_KeyUp;
    }

    // ── Coordinate translation ────────────────────────────────────
    private (double xRatio, double yRatio) ScreenToRemoteRatio(int mouseX, int mouseY)
    {
        if (_screen.Image == null) return (0, 0);

        // Calculate the actual display area within the PictureBox (Zoom mode)
        var imgW = _screen.Image.Width;
        var imgH = _screen.Image.Height;
        var boxW = _screen.Width;
        var boxH = _screen.Height;

        var ratioX = (double)boxW / imgW;
        var ratioY = (double)boxH / imgH;
        var ratio = Math.Min(ratioX, ratioY);

        var displayW = (int)(imgW * ratio);
        var displayH = (int)(imgH * ratio);
        var offsetX = (boxW - displayW) / 2;
        var offsetY = (boxH - displayH) / 2;

        var relX = (mouseX - offsetX) / (double)displayW;
        var relY = (mouseY - offsetY) / (double)displayH;

        return (Math.Clamp(relX, 0, 1), Math.Clamp(relY, 0, 1));
    }

    // ── Mouse events ──────────────────────────────────────────────
    private void Screen_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_connected) return;
        var (x, y) = ScreenToRemoteRatio(e.X, e.Y);
        _ = _connection.SendTextAsync(new InputMessage { Action = "mouse_move", X = x, Y = y });
    }

    private void Screen_MouseDown(object? sender, MouseEventArgs e)
    {
        if (!_connected) return;
        _screen.Focus();
        var (x, y) = ScreenToRemoteRatio(e.X, e.Y);
        var btn = e.Button == MouseButtons.Right ? "right" : e.Button == MouseButtons.Middle ? "middle" : "left";
        _ = _connection.SendTextAsync(new InputMessage { Action = "mouse_down", X = x, Y = y, Button = btn });
    }

    private void Screen_MouseUp(object? sender, MouseEventArgs e)
    {
        if (!_connected) return;
        var (x, y) = ScreenToRemoteRatio(e.X, e.Y);
        var btn = e.Button == MouseButtons.Right ? "right" : e.Button == MouseButtons.Middle ? "middle" : "left";
        _ = _connection.SendTextAsync(new InputMessage { Action = "mouse_up", X = x, Y = y, Button = btn });
    }

    private void Screen_MouseWheel(object? sender, MouseEventArgs e)
    {
        if (!_connected) return;
        var (x, y) = ScreenToRemoteRatio(e.X, e.Y);
        _ = _connection.SendTextAsync(new InputMessage { Action = "mouse_wheel", X = x, Y = y, Delta = e.Delta });
    }

    // ── Keyboard events ───────────────────────────────────────────
    private void ViewerForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!_connected) return;
        e.Handled = true;
        e.SuppressKeyPress = true;
        _ = _connection.SendTextAsync(new InputMessage { Action = "key_down", KeyCode = (int)e.KeyCode });
    }

    private void ViewerForm_KeyUp(object? sender, KeyEventArgs e)
    {
        if (!_connected) return;
        e.Handled = true;
        _ = _connection.SendTextAsync(new InputMessage { Action = "key_up", KeyCode = (int)e.KeyCode });
    }

    // ── Quality toggle ────────────────────────────────────────────
    private void CycleQuality()
    {
        _qualityLevel = (_qualityLevel + 1) % 3;
        var (label, q, s) = _qualityLevel switch
        {
            0 => ("Baixa", 25, 0.4f),
            1 => ("Média", 40, 0.6f),
            _ => ("Alta", 70, 0.85f)
        };
        _btnQuality.Text = $"Qualidade: {label}";
        // Quality settings are applied on the host side; for now this is visual
    }

    // ── Fullscreen toggle ─────────────────────────────────────────
    private bool _isFullscreen;
    private FormBorderStyle _prevBorderStyle;
    private FormWindowState _prevWindowState;

    private void ToggleFullscreen()
    {
        if (_isFullscreen)
        {
            FormBorderStyle = _prevBorderStyle;
            WindowState = _prevWindowState;
            _toolbar.Visible = true;
            _isFullscreen = false;
        }
        else
        {
            _prevBorderStyle = FormBorderStyle;
            _prevWindowState = WindowState;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            _toolbar.Visible = false;
            _isFullscreen = true;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // ESC exits fullscreen
        if (keyData == Keys.Escape && _isFullscreen)
        {
            ToggleFullscreen();
            return true;
        }
        // F11 toggles fullscreen
        if (keyData == Keys.F11)
        {
            ToggleFullscreen();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _ = _connection.DisconnectAsync();
        _connection.Dispose();
        _currentFrame?.Dispose();
        base.OnFormClosing(e);
    }
}
