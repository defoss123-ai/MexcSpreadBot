using MexcSpreadBot.Data;
using MexcSpreadBot.Helpers;
using System.ComponentModel;

namespace MexcSpreadBot
{
    public partial class FormSpread : Form
    {
        private readonly RealtimeSpreadScanner _scanner;
        private readonly BindingList<SpreadRow> _rows = new();
        private readonly Dictionary<string, SpreadRow> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _aboveThresholdSince = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase) { "BTC_USDT" };

        private readonly CheckBox _checkBoxAutoMode = new();
        private readonly NumericUpDown _numericUpDownDexInterval = new();
        private readonly Button _buttonAutoMode = new();
        private readonly Label _labelAutoStatus = new();
        private readonly ListBox _listBoxLogs = new();
        private readonly NumericUpDown _numericMinSpread = new();
        private readonly CheckBox _checkOnlyFavorites = new();
        private readonly CheckBox _checkStableSpread = new();
        private readonly NumericUpDown _numericStableSeconds = new();

        private DateTime _lastUiRefreshUtc = DateTime.MinValue;

        public FormSpread()
        {
            InitializeComponent();
            ConfigureGrid();
            ConfigureRealtimeControls();

            // Old manual sync controls disabled for realtime mode
            парыToolStripMenuItem.Visible = false;
            button1.Visible = false;
            buttonFilter.Visible = false;

            _scanner = new RealtimeSpreadScanner(new[]
            {
                new QuotePair
                {
                    Symbol = "BTC_USDT",
                    Chain = ChainType.Evm,
                    BaseTokenAddress = "0x2260FAC5E5542a773Aa44fBCfeDf7C193bc2C599",
                    QuoteTokenAddress = "0xdAC17F958D2ee523a2206206994597C13D831ec7"
                }
            }, mexcFeePercent: 0.02, dexFeePercent: 0.08, slippagePercent: 0.02);

            _scanner.SpreadUpdated += OnSpreadUpdated;
            _scanner.Log += AppendLog;

            spreadBindingSource.DataSource = _rows;
            spreadBindingSource.ResetBindings(false);
            UpdateAutoModeState(false);
        }

        private void ConfigureGrid()
        {
            dataGridViewSpread.AutoGenerateColumns = false;
            dataGridViewSpread.Columns.Clear();

            AddColumn("Symbol", "Symbol");
            AddColumn("MexBid", "MexBid");
            AddColumn("MexAsk", "MexAsk");
            AddColumn("DexBid", "DexBid");
            AddColumn("DexAsk", "DexAsk");
            AddColumn("SpreadA", "SpreadA");
            AddColumn("SpreadB", "SpreadB");
            AddColumn("SpreadNetA", "SpreadNetA");
            AddColumn("SpreadNetB", "SpreadNetB");
            AddColumn("MexAgeMs", "MexAgeMs");
            AddColumn("DexAgeMs", "DexAgeMs");
            AddColumn("LastUpdateTime", "LastUpdateTime");
        }

        private void AddColumn(string property, string header)
        {
            dataGridViewSpread.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = property,
                HeaderText = header,
                ReadOnly = true,
                Width = 95
            });
        }

        private void ConfigureRealtimeControls()
        {
            _checkBoxAutoMode.Text = "Auto ON";
            _checkBoxAutoMode.Location = new Point(700, 368);
            _checkBoxAutoMode.AutoSize = true;

            _numericUpDownDexInterval.Minimum = 2;
            _numericUpDownDexInterval.Maximum = 5;
            _numericUpDownDexInterval.Value = 2;
            _numericUpDownDexInterval.Location = new Point(700, 394);
            _numericUpDownDexInterval.Size = new Size(70, 27);

            _buttonAutoMode.Text = "Apply";
            _buttonAutoMode.Location = new Point(780, 393);
            _buttonAutoMode.Size = new Size(75, 29);
            _buttonAutoMode.Click += buttonAutoMode_Click;

            _labelAutoStatus.Text = "Auto: OFF";
            _labelAutoStatus.Location = new Point(700, 426);
            _labelAutoStatus.AutoSize = true;

            _numericMinSpread.DecimalPlaces = 2;
            _numericMinSpread.Increment = 0.1M;
            _numericMinSpread.Minimum = 0;
            _numericMinSpread.Maximum = 100;
            _numericMinSpread.Value = 0.5M;
            _numericMinSpread.Location = new Point(700, 452);
            _numericMinSpread.Size = new Size(70, 27);
            _numericMinSpread.ValueChanged += (_, _) => RefreshRowsThrottled(force: true);

            _checkOnlyFavorites.Text = "Только избранные пары";
            _checkOnlyFavorites.Location = new Point(780, 452);
            _checkOnlyFavorites.AutoSize = true;
            _checkOnlyFavorites.CheckedChanged += (_, _) => RefreshRowsThrottled(force: true);

            _checkStableSpread.Text = "Спред держится N сек";
            _checkStableSpread.Location = new Point(700, 484);
            _checkStableSpread.AutoSize = true;
            _checkStableSpread.CheckedChanged += (_, _) => RefreshRowsThrottled(force: true);

            _numericStableSeconds.Minimum = 1;
            _numericStableSeconds.Maximum = 60;
            _numericStableSeconds.Value = 5;
            _numericStableSeconds.Location = new Point(850, 483);
            _numericStableSeconds.Size = new Size(50, 27);
            _numericStableSeconds.ValueChanged += (_, _) => RefreshRowsThrottled(force: true);

            _listBoxLogs.Location = new Point(12, 518);
            _listBoxLogs.Size = new Size(927, 124);

            ClientSize = new Size(951, 650);
            Controls.Add(_checkBoxAutoMode);
            Controls.Add(_numericUpDownDexInterval);
            Controls.Add(_buttonAutoMode);
            Controls.Add(_labelAutoStatus);
            Controls.Add(_numericMinSpread);
            Controls.Add(_checkOnlyFavorites);
            Controls.Add(_checkStableSpread);
            Controls.Add(_numericStableSeconds);
            Controls.Add(_listBoxLogs);
        }

        private void OnSpreadUpdated(SpreadRow row)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => OnSpreadUpdated(row));
                return;
            }

            _cache[row.Symbol] = row;

            var minSpread = (double)_numericMinSpread.Value;
            if (Math.Abs(row.SpreadNetA) >= minSpread || Math.Abs(row.SpreadNetB) >= minSpread)
            {
                _aboveThresholdSince.TryAdd(row.Symbol, DateTime.UtcNow);
            }
            else
            {
                _aboveThresholdSince.Remove(row.Symbol);
            }

            RefreshRowsThrottled();
        }

        private void RefreshRowsThrottled(bool force = false)
        {
            var now = DateTime.UtcNow;
            if (!force && now - _lastUiRefreshUtc < TimeSpan.FromSeconds(1))
            {
                return;
            }

            _lastUiRefreshUtc = now;

            var minSpread = (double)_numericMinSpread.Value;
            var minStableSec = (double)_numericStableSeconds.Value;

            var filtered = _cache.Values
                .Where(x => !_checkOnlyFavorites.Checked || _favorites.Contains(x.Symbol))
                .Where(x => Math.Max(Math.Abs(x.SpreadNetA), Math.Abs(x.SpreadNetB)) >= minSpread)
                .Where(x =>
                {
                    if (!_checkStableSpread.Checked)
                    {
                        return true;
                    }

                    return _aboveThresholdSince.TryGetValue(x.Symbol, out var since) &&
                        (DateTime.UtcNow - since).TotalSeconds >= minStableSec;
                })
                .OrderByDescending(x => Math.Max(Math.Abs(x.SpreadNetA), Math.Abs(x.SpreadNetB)))
                .ToList();

            _rows.Clear();
            foreach (var row in filtered)
            {
                _rows.Add(row);
            }
        }

        private void AppendLog(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => AppendLog(message));
                return;
            }

            _listBoxLogs.Items.Insert(0, message);
            while (_listBoxLogs.Items.Count > 300)
            {
                _listBoxLogs.Items.RemoveAt(_listBoxLogs.Items.Count - 1);
            }
        }

        private void открытьMexcToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var current = (SpreadRow?)spreadBindingSource.Current;
            if (current != null)
            {
                BrowserHelper.OpenUrl($"https://futures.mexc.com/exchange/{current.Symbol}");
            }
        }

        private void открытьDexcscreenerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BrowserHelper.OpenUrl("https://app.0x.org/swap");
        }

        private void dataGridViewSpread_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            // engine-sorted list, no manual sort
        }

        private void buttonFilter_Click(object sender, EventArgs e)
        {
            AppendLog("Manual filter disabled in realtime mode");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            AppendLog("Manual reset disabled in realtime mode");
        }

        private void получитьВсеЦеныToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AppendLog("Manual sync disabled in realtime mode");
        }

        private void buttonAutoMode_Click(object? sender, EventArgs e)
        {
            if (_checkBoxAutoMode.Checked)
            {
                _scanner.Start((int)_numericUpDownDexInterval.Value);
                UpdateAutoModeState(true);
            }
            else
            {
                _scanner.Stop();
                UpdateAutoModeState(false);
            }
        }

        private void UpdateAutoModeState(bool isRunning)
        {
            _labelAutoStatus.Text = isRunning ? "Auto: ON" : "Auto: OFF";
            _labelAutoStatus.ForeColor = isRunning ? Color.DarkGreen : Color.DarkRed;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _scanner.Dispose();
            base.OnFormClosing(e);
        }
    }
}
