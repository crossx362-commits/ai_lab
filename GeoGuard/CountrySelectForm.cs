using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GeoGuard
{
    // 차단할 국가를 검색/체크박스로 선택하는 창
    class CountrySelectForm : Form
    {
        readonly CheckedListBox listBox;
        readonly TextBox searchBox;
        readonly Label countLabel;
        readonly HashSet<string> selected;          // 국가 코드
        readonly List<KeyValuePair<string, string>> catalog;   // (코드, 표시명) 이름순

        public List<string> SelectedCountries
        {
            get { return selected.OrderBy(delegate(string c) { return c; }).ToList(); }
        }

        public CountrySelectForm(List<string> current)
        {
            selected = new HashSet<string>(current);

            Text = "GeoGuard - 차단 국가 설정";
            Width = 420;
            Height = 560;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            catalog = Countries.Names
                .Select(delegate(KeyValuePair<string, string> kv)
                {
                    return new KeyValuePair<string, string>(kv.Key, kv.Key + " " + kv.Value);
                })
                .OrderBy(delegate(KeyValuePair<string, string> kv) { return kv.Value.Substring(3); })
                .ToList();
            // 목록에 없는 코드가 선택되어 있으면 표시용으로 추가
            foreach (var cc in current)
                if (!Countries.Names.ContainsKey(cc))
                    catalog.Insert(0, new KeyValuePair<string, string>(cc, cc));

            var searchPanel = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(8, 5, 8, 3) };
            searchBox = new TextBox { Dock = DockStyle.Fill };
            searchBox.TextChanged += delegate { RebuildList(); };
            var searchLabel = new Label
            {
                Text = "검색:",
                Dock = DockStyle.Left,
                Width = 40,
                TextAlign = ContentAlignment.MiddleLeft
            };
            searchPanel.Controls.Add(searchBox);
            searchPanel.Controls.Add(searchLabel);

            countLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };

            listBox = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false,
            };
            listBox.ItemCheck += OnItemCheck;

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 42,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(6)
            };
            var cancelBtn = new Button { Text = "취소", Width = 90, DialogResult = DialogResult.Cancel };
            var okBtn = new Button { Text = "적용", Width = 90, DialogResult = DialogResult.OK };
            buttonPanel.Controls.Add(cancelBtn);
            buttonPanel.Controls.Add(okBtn);
            AcceptButton = okBtn;
            CancelButton = cancelBtn;

            Controls.Add(listBox);
            Controls.Add(countLabel);
            Controls.Add(searchPanel);
            Controls.Add(buttonPanel);

            RebuildList();
        }

        void RebuildList()
        {
            string filter = searchBox.Text.Trim();
            listBox.ItemCheck -= OnItemCheck;
            listBox.BeginUpdate();
            listBox.Items.Clear();
            foreach (var kv in catalog)
            {
                if (filter.Length > 0 &&
                    kv.Value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                int idx = listBox.Items.Add(kv.Value);
                listBox.SetItemChecked(idx, selected.Contains(kv.Key));
            }
            listBox.EndUpdate();
            listBox.ItemCheck += OnItemCheck;
            UpdateCount();
        }

        void OnItemCheck(object sender, ItemCheckEventArgs e)
        {
            string text = (string)listBox.Items[e.Index];
            string cc = text.Substring(0, 2).ToUpperInvariant();
            if (e.NewValue == CheckState.Checked) selected.Add(cc);
            else selected.Remove(cc);
            BeginInvoke(new Action(UpdateCount));
        }

        void UpdateCount()
        {
            var names = selected
                .OrderBy(delegate(string c) { return c; })
                .Select(new Func<string, string>(Countries.NameOnly));
            string joined = string.Join(", ", names.ToArray());
            countLabel.Text = selected.Count == 0
                ? "선택된 국가 없음 (차단 안 함)"
                : string.Format("차단 대상 {0}개국: {1}", selected.Count,
                    joined.Length > 45 ? joined.Substring(0, 45) + "..." : joined);
        }
    }
}
