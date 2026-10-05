namespace MiniDebit;

public sealed class SettingsForm : Form
{
    public bool MeasurementChanged { get; private set; }

    public SettingsForm(Settings settings, Icon icon)
    {
        Text = "MiniDébit — Paramètres";
        Icon = icon;
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(500, 525);
        BackColor = Color.FromArgb(246, 249, 253);
        ForeColor = Color.FromArgb(27, 42, 61);

        var title = new Label { Text = "Un widget à votre mesure", Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true, Location = new Point(24, 20) };
        Controls.Add(title);
        Controls.Add(new Label { Text = "Les changements s’appliquent dès l’enregistrement.", AutoSize = true, Location = new Point(25, 54) });
        var table = new TableLayoutPanel { Location = new Point(24, 86), Size = new Size(450, 320), ColumnCount = 2, RowCount = 9 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 9; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        Controls.Add(table);
        ComboBox Choice(params object[] values)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            combo.Items.AddRange(values); return combo;
        }
        NumericUpDown Number(decimal min, decimal max, decimal value, decimal step = 1) =>
            new() { Minimum = min, Maximum = max, Value = value, Increment = step, Dock = DockStyle.Fill, ThousandsSeparator = true };
        void Row(int row, string label, Control control)
        {
            table.Controls.Add(new Label { Text = label, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            table.Controls.Add(control, 1, row);
        }
        var adapter = Choice(new AdapterChoice("auto", "Automatique (une carte)"), new AdapterChoice("all", "Toutes les cartes (cumul)"));
        try { adapter.Items.AddRange(NetworkMonitor.Choices().Cast<object>().ToArray()); }
        catch (System.Net.NetworkInformation.NetworkInformationException) { }
        if (!adapter.Items.Cast<AdapterChoice>().Any(a => a.Id == settings.AdapterId))
            adapter.Items.Add(new AdapterChoice(settings.AdapterId, "Carte sélectionnée (indisponible)"));
        adapter.SelectedIndex = adapter.Items.Cast<AdapterChoice>().ToList().FindIndex(a => a.Id == settings.AdapterId);
        Row(0, "Carte réseau", adapter);
        var interval = Number(250, 5000, settings.IntervalMs, 250); Row(1, "Actualisation (ms)", interval);
        var history = Number(15, 600, settings.HistorySeconds, 15); Row(2, "Historique (secondes)", history);
        var units = Choice("Octets/s : Ko/s, Mo/s", "Bits/s : Mbit/s, Gbit/s");
        units.SelectedIndex = settings.Bits ? 1 : 0; Row(3, "Unités du débit", units);
        var theme = Choice("Sombre", "Clair"); theme.SelectedIndex = settings.Dark ? 0 : 1; Row(4, "Apparence", theme);
        var opacity = Number(35, 100, settings.OpacityPercent, 5); Row(5, "Opacité (%)", opacity);
        var scale = Number(0, 100000, (decimal)settings.FixedScaleMbps); scale.DecimalPlaces = 2;
        Row(6, "Échelle (Mbit/s, 0 = auto)", scale);
        var onTop = new CheckBox { Text = "Garder au premier plan", Checked = settings.AlwaysOnTop, Dock = DockStyle.Fill };
        Row(7, "Affichage", onTop);
        var hidden = new CheckBox { Text = "Dans la zone de notification", Checked = settings.StartHidden, Dock = DockStyle.Fill };
        Row(8, "Au lancement", hidden);
        Controls.Add(new Label { Location = new Point(24, 409), Size = new Size(450, 49), ForeColor = Color.FromArgb(94, 115, 141),
            Text = "Le cumul peut compter deux fois le trafic d’un VPN.\nPour une mesure précise, choisissez une seule carte réseau." });
        var save = new Button { Text = "Enregistrer", Location = new Point(340, 476), Size = new Size(134, 34), BackColor = Color.FromArgb(21, 110, 137),
            ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var cancel = new Button { Text = "Annuler", Location = new Point(226, 476), Size = new Size(104, 34), DialogResult = DialogResult.Cancel };
        Controls.AddRange([save, cancel]);
        AcceptButton = save; CancelButton = cancel;
        save.Click += (_, _) =>
        {
            var id = ((AdapterChoice)adapter.SelectedItem!).Id;
            MeasurementChanged = settings.AdapterId != id;
            settings.AdapterId = id;
            settings.IntervalMs = (int)interval.Value;
            settings.HistorySeconds = (int)history.Value;
            settings.Bits = units.SelectedIndex == 1;
            settings.Dark = theme.SelectedIndex == 0;
            settings.OpacityPercent = (int)opacity.Value;
            settings.FixedScaleMbps = (double)scale.Value;
            settings.AlwaysOnTop = onTop.Checked;
            settings.StartHidden = hidden.Checked;
            DialogResult = DialogResult.OK;
        };
    }
}
