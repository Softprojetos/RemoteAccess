using System.Drawing;
using System.Drawing.Drawing2D;
using RemoteAccess.Desktop.Services;
using RemoteAccess.Shared;

namespace RemoteAccess.Desktop.Forms;

public class MainForm : Form
{
    // ── Config ────────────────────────────────────────────────────
    private const string DEFAULT_RELAY = "ws://localhost:5050";

    // ── Controls ──────────────────────────────────────────────────
    private Label _lblTitle = null!;
    private Label _lblYourId = null!;
    private TextBox _txtId = null!;
    private Label _lblPassword = null!;
    private TextBox _txtPassword = null!;
    private Label _lblRemoteId = null!;
    private TextBox _txtRemoteId = null!;
    private Label _lblRemotePassword = null!;
    private TextBox _txtRemotePassword = null!;
    private Button _btnConnect = null!;
    private Label _lblStatus = null!;
    private Label _lblRelayLabel = null!;
    private TextBox _txtRelay = null!;
    private Panel _panelLeft = null!;
    private Panel _panelRight = null!;
    private Button _btnCopyId = null!;
    private Button _btnNewPassword = null!;

    // ── State ─────────────────────────────────────────────────────
    private readonly string _myId;
    private string _myPassword;
    private ConnectionService? _hostConnection;
    private ScreenCaptureService? _screenCapture;
    private CancellationTokenSource? _streamCts;
    private bool _viewerConnected;

    // ── Colors ────────────────────────────────────────────────────
    private static readonly Color BgDark = Color.FromArgb(24, 24, 32);
    private static readonly Color BgCard = Color.FromArgb(32, 34, 46);
    private static readonly Color AccentBlue = Color.FromArgb(66, 135, 245);
    private static readonly Color AccentGreen = Color.FromArgb(72, 199, 142);
    private static readonly Color TextWhite = Color.FromArgb(235, 235, 245);
    private static readonly Color TextMuted = Color.FromArgb(150, 155, 175);
    private static readonly Color InputBg = Color.FromArgb(42, 44, 58);
    private static readonly Color BorderColor = Color.FromArgb(55, 58, 75);

    public MainForm()
    {
        _myId = GenerateId();
        _myPassword = GeneratePassword();
        InitializeUI();
        _ = ConnectToRelayAsHostAsync();
    }

    private static string GenerateId()
    {
        var rng = new Random();
        return $"{rng.Next(100, 999)} {rng.Next(100, 999)} {rng.Next(100, 999)}";
    }

    private static string GeneratePassword()
    {
        var chars = "abcdefghjkmnpqrstuvwxyz23456789";
        var rng = new Random();
        return new string(Enumerable.Range(0, 6).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
    }

    // ── UI Setup ──────────────────────────────────────────────────
    private void InitializeUI()
    {
        Text = "RemoteAccess";
        Size = new Size(680, 420);
        MinimumSize = new Size(680, 420);
        BackColor = BgDark;
        ForeColor = TextWhite;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9.5f);

        // Title
        _lblTitle = new Label
        {
            Text = "⬡ RemoteAccess",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = AccentBlue,
            Location = new Point(24, 16),
            AutoSize = true
        };

        // ── Left Panel (Your Machine) ─────────────────────────────
        _panelLeft = CreateCard(20, 56, 306, 260, "Esta Máquina");

        _lblYourId = CreateLabel("Seu ID", 20, 42);
        _txtId = CreateReadonlyBox(_myId, 20, 66, 200);
        _txtId.Font = new Font("Consolas", 16f, FontStyle.Bold);
        _txtId.ForeColor = AccentGreen;
        _txtId.Height = 40;

        _btnCopyId = CreateSmallButton("Copiar", 230, 70);
        _btnCopyId.Click += (_, _) => { Clipboard.SetText(_myId.Replace(" ", "")); SetStatus("ID copiado!"); };

        _lblPassword = CreateLabel("Senha", 20, 116);
        _txtPassword = CreateReadonlyBox(_myPassword, 20, 140, 160);
        _txtPassword.Font = new Font("Consolas", 13f, FontStyle.Bold);

        _btnNewPassword = CreateSmallButton("Nova", 190, 144);
        _btnNewPassword.Click += (_, _) => { _myPassword = GeneratePassword(); _txtPassword.Text = _myPassword; };

        _panelLeft.Controls.AddRange([_lblYourId, _txtId, _btnCopyId, _lblPassword, _txtPassword, _btnNewPassword]);

        // ── Right Panel (Connect) ─────────────────────────────────
        _panelRight = CreateCard(340, 56, 306, 260, "Conectar Remoto");

        _lblRemoteId = CreateLabel("ID Remoto", 20, 42);
        _txtRemoteId = new TextBox
        {
            Location = new Point(20, 66),
            Size = new Size(266, 36),
            Font = new Font("Consolas", 14f),
            BackColor = InputBg,
            ForeColor = TextWhite,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "000 000 000"
        };

        _lblRemotePassword = CreateLabel("Senha", 20, 110);
        _txtRemotePassword = new TextBox
        {
            Location = new Point(20, 134),
            Size = new Size(266, 30),
            Font = new Font("Consolas", 11f),
            BackColor = InputBg,
            ForeColor = TextWhite,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Senha do remoto"
        };

        _btnConnect = new Button
        {
            Text = "▶  Conectar",
            Location = new Point(20, 180),
            Size = new Size(266, 44),
            FlatStyle = FlatStyle.Flat,
            BackColor = AccentBlue,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnConnect.FlatAppearance.BorderSize = 0;
        _btnConnect.Click += BtnConnect_Click;

        _panelRight.Controls.AddRange([_lblRemoteId, _txtRemoteId, _lblRemotePassword, _txtRemotePassword, _btnConnect]);

        // ── Status bar ────────────────────────────────────────────
        _lblStatus = new Label
        {
            Text = "Conectando ao relay...",
            ForeColor = TextMuted,
            Location = new Point(24, 330),
            Size = new Size(400, 24),
            Font = new Font("Segoe UI", 8.5f)
        };

        _lblRelayLabel = new Label
        {
            Text = "Relay:",
            ForeColor = TextMuted,
            Location = new Point(24, 356),
            AutoSize = true,
            Font = new Font("Segoe UI", 8f)
        };

        _txtRelay = new TextBox
        {
            Text = DEFAULT_RELAY,
            Location = new Point(68, 353),
            Size = new Size(250, 22),
            BackColor = InputBg,
            ForeColor = TextMuted,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 8f)
        };

        Controls.AddRange([_lblTitle, _panelLeft, _panelRight, _lblStatus, _lblRelayLabel, _txtRelay]);
    }

    private Panel CreateCard(int x, int y, int w, int h, string title)
    {
        var panel = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(w, h),
            BackColor = BgCard
        };
        panel.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(BorderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, w - 1, h - 1);

            using var titleFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var titleBrush = new SolidBrush(TextMuted);
            e.Graphics.DrawString(title.ToUpper(), titleFont, titleBrush, 20, 14);
        };
        return panel;
    }

    private static Label CreateLabel(string text, int x, int y) => new()
    {
        Text = text,
        ForeColor = TextMuted,
        Location = new Point(x, y),
        AutoSize = true,
        Font = new Font("Segoe UI", 8.5f)
    };

    private static TextBox CreateReadonlyBox(string text, int x, int y, int w) => new()
    {
        Text = text,
        Location = new Point(x, y),
        Size = new Size(w, 30),
        ReadOnly = true,
        BackColor = InputBg,
        ForeColor = TextWhite,
        BorderStyle = BorderStyle.FixedSingle
    };

    private static Button CreateSmallButton(string text, int x, int y) => new()
    {
        Text = text,
        Location = new Point(x, y),
        Size = new Size(56, 28),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(50, 53, 70),
        ForeColor = TextMuted,
        Font = new Font("Segoe UI", 8f),
        Cursor = Cursors.Hand
    };

    // ── Host mode (always running) ────────────────────────────────
    private async Task ConnectToRelayAsHostAsync()
    {
        var relayUrl = _txtRelay.Text.Trim();
        _hostConnection = new ConnectionService(relayUrl);

        _hostConnection.OnConnected += () => Invoke(() => SetStatus("● Conectado ao relay — aguardando conexões", AccentGreen));
        _hostConnection.OnDisconnected += msg => Invoke(() =>
        {
            SetStatus("○ Desconectado do relay — reconectando...", Color.Orange);
            _ = Task.Delay(3000).ContinueWith(_ => Invoke(() => _ = ConnectToRelayAsHostAsync()));
        });

        _hostConnection.OnTextMessage += msg =>
        {
            switch (msg)
            {
                case ViewerConnectedMessage:
                    _viewerConnected = true;
                    Invoke(() => SetStatus("● Viewer conectado — compartilhando tela", AccentBlue));
                    StartScreenStream();
                    break;

                case ViewerDisconnectedMessage:
                    _viewerConnected = false;
                    StopScreenStream();
                    Invoke(() => SetStatus("● Conectado ao relay — aguardando conexões", AccentGreen));
                    break;

                case InputMessage input:
                    ProcessRemoteInput(input);
                    break;

                case ErrorMessage err:
                    Invoke(() => SetStatus($"Erro: {err.Message}", Color.Red));
                    break;
            }
        };

        var id = _myId.Replace(" ", "");
        await _hostConnection.ConnectAsHostAsync(id, _myPassword);
    }

    private void StartScreenStream()
    {
        _streamCts?.Cancel();
        _streamCts = new CancellationTokenSource();
        _screenCapture = new ScreenCaptureService();

        var ct = _streamCts.Token;
        _ = Task.Run(async () =>
        {
            // Send screen info first
            var (w, h) = _screenCapture.GetScreenSize();
            await _hostConnection!.SendTextAsync(new ScreenInfoMessage { Width = w, Height = h });

            while (!ct.IsCancellationRequested && _viewerConnected)
            {
                try
                {
                    var frame = _screenCapture.CaptureScreen();
                    await _hostConnection!.SendBinaryAsync(frame);
                    await Task.Delay(80, ct); // ~12 FPS
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(100, ct); }
            }
        }, ct);
    }

    private void StopScreenStream()
    {
        _streamCts?.Cancel();
        _screenCapture?.Dispose();
        _screenCapture = null;
    }

    private void ProcessRemoteInput(InputMessage input)
    {
        try
        {
            var bounds = Screen.PrimaryScreen!.Bounds;
            InputSimulator.SimulateInput(
                input.Action, input.X, input.Y,
                input.Button, input.KeyCode, input.Delta,
                bounds.Width, bounds.Height);
        }
        catch { }
    }

    // ── Viewer mode (on demand) ───────────────────────────────────
    private async void BtnConnect_Click(object? sender, EventArgs e)
    {
        var remoteId = _txtRemoteId.Text.Replace(" ", "").Trim();
        var remotePass = _txtRemotePassword.Text.Trim();

        if (string.IsNullOrEmpty(remoteId) || remoteId.Length < 9)
        {
            SetStatus("Informe um ID válido (9 dígitos)", Color.Orange);
            return;
        }
        if (string.IsNullOrEmpty(remotePass))
        {
            SetStatus("Informe a senha do remoto", Color.Orange);
            return;
        }

        _btnConnect.Enabled = false;
        _btnConnect.Text = "Conectando...";

        try
        {
            var relayUrl = _txtRelay.Text.Trim();
            var viewerForm = new ViewerForm(relayUrl, remoteId, remotePass);
            viewerForm.FormClosed += (_, _) =>
            {
                _btnConnect.Enabled = true;
                _btnConnect.Text = "▶  Conectar";
            };
            viewerForm.Show();
        }
        catch (Exception ex)
        {
            SetStatus($"Erro: {ex.Message}", Color.Red);
            _btnConnect.Enabled = true;
            _btnConnect.Text = "▶  Conectar";
        }
    }

    private void SetStatus(string text, Color? color = null)
    {
        _lblStatus.Text = text;
        _lblStatus.ForeColor = color ?? TextMuted;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopScreenStream();
        _hostConnection?.Dispose();
        base.OnFormClosing(e);
    }
}
